using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace EarlyRush
{
    /// <summary>
    /// Opt-in Windows Player impact fixtures. The fixture hook only seeds a scene.
    /// Every contact is driven by the ordinary QA input channel. Normal route QA is separate.
    /// </summary>
    public class RushImpactQA : MonoBehaviour
    {
        [Serializable] public class Check { public string name, evidence; public bool passed; }
        [Serializable] public class ShotEvidence
        {
            public string path, scenario, phase;
            public float time, height, spinDegrees, freezeRemaining;
            public Vector2 position;
            public Vector3 visualScale;
            public int landCount, renderedFrame;
        }
        [Serializable] public class CaseEvidence
        {
            public string name, fixture;
            public bool dash, finiteAndContained=true, physicalRootsStable=true;
            public bool sawAir, sawRecovery, sawCompression, sawLandingRebound, recoveredNavigation;
            public bool noPlayerStumble=true, playerForward=true, noAirborneRecycle=true;
            public bool pauseHitstop, pauseAirborne, pauseLanding;
            public bool capturedCompression, capturedAir, capturedApex, capturedTumble, capturedLanding;
            public int samples, initialActive, uniqueTargets, duplicateLaunches, launches, chainLaunches;
            public int maxDepth, maxTargets, normalContacts, primaryLandCount, freezeEpisodes, frozenPhysicsSamples;
            public float duration, comparisonTime, earlyDisplacement, peakHeight, peakSpin;
            public float maxVelocity, maxDifferentialSpeed, maxPlayerDifferentialSpeed, maxPlayerVelocity, maxFrameGap, maxFreezeRemaining;
            public float maxGroundVelocity, maxAirVelocity, maxGroundDifferentialSpeed, maxAirDifferentialSpeed;
            public float longestFreezeWallSeconds, maxFrozenPhysicsDelta, frozenTimerAdvance, maxRootScaleError;
            public float primaryLaunchTime=-1, navigationProgress, navigationDistance, playerAdvance;
            public float finalScaleError, peakPlayerScaleError, initialCollisionRadius;
            public Vector2 primaryStart, primaryEnd, earlyDelta, launchDirection;
            public int peakVelocitySlot;
            public string peakVelocityPhase;
            public float peakVelocityAt, peakVelocityHeight, peakVelocityRecovery;
            public Vector2 peakVelocityPosition, peakVelocityPrevious, peakVelocityPlayer;
            public List<int> launchedSlots=new List<int>();
        }
        [Serializable] public class Report
        {
            public string utc, engine, device, graphicsApi;
            public string schema="v3-impact-fixtures-1";
            public string driver="Actual Windows Player QAMove/QADash/QABrace input. --impact-qa alone enables fixture initialization. No runtime position/time overrides after setup. Not OS keyboard automation or a human playtest. Normal three-stage route validation is reported separately.";
            public string sampling="Impact telemetry is sampled after rendered frames. Screenshot cases are separate from the front displacement comparison. Hitstop wall-clock duration includes real frame delays; the remaining budget and repeated refresh are checked independently.";
            public bool passed;
            public int failureCount;
            public float elapsedRealSeconds;
            public List<Check> checks=new List<Check>();
            public List<CaseEvidence> cases=new List<CaseEvidence>();
            public List<ShotEvidence> screenshots=new List<ShotEvidence>();
        }

        RushGame game;
        readonly Report report=new Report();
        string output;
        float started;
        bool finished, lastCaptureOK;
        CaseEvidence running;
        readonly WaitForEndOfFrame endFrame=new WaitForEndOfFrame();
        const float NpcClearance=.395f;
        const float GroundSpeedLimit=12.25f;
        const float AirSpeedLimit=14f;
        const float FreezeBudget=.06f;

        void Start()
        {
            game=GetComponent<RushGame>();
            if(game==null) { Debug.LogError("RushImpactQA requires RushGame"); return; }
            output=Path.Combine(Path.GetDirectoryName(Application.dataPath),"ImpactQA");
            foreach(string arg in Environment.GetCommandLineArgs())
                if(arg.StartsWith("--impact-qa-output=",StringComparison.Ordinal))
                    output=arg.Substring("--impact-qa-output=".Length);
            Directory.CreateDirectory(output);
            started=Time.realtimeSinceStartup;
            report.utc=DateTime.UtcNow.ToString("o");
            report.engine=Application.unityVersion;
            report.device=SystemInfo.graphicsDeviceName;
            report.graphicsApi=SystemInfo.graphicsDeviceType.ToString();
            game.QAInput=true;
            Application.logMessageReceived+=OnLog;
            StartCoroutine(Suite());
        }

        void OnDestroy(){Application.logMessageReceived-=OnLog;}
        void OnLog(string message,string trace,LogType type)
        {
            if(!finished&&(type==LogType.Error||type==LogType.Exception))
                Assert("No runtime error",false,message+"\n"+trace);
        }
        void Update()
        {
            if(!finished&&game!=null&&Time.realtimeSinceStartup-started>150f)
            {
                Assert("Impact suite completes within 150 seconds",false,
                    "state="+game.State+" case="+(running==null?"none":running.name));
                Finish();
            }
        }
        void Assert(string name,bool ok,string evidence)
        {
            if(finished)return;
            report.checks.Add(new Check{name=name,passed=ok,evidence=evidence});
            if(!ok)report.failureCount++;
            Debug.Log("RUSH_IMPACT_QA_CHECK "+(ok?"PASS ":"FAIL ")+name+" | "+evidence);
        }
        void Neutral(){game.QAMove=Vector2.zero;game.QADash=false;game.QABrace=false;}
        IEnumerator Wait(float seconds)
        {
            float until=Time.realtimeSinceStartup+seconds;
            while(!finished&&Time.realtimeSinceStartup<until)yield return null;
        }
        static bool Finite(float f){return !float.IsNaN(f)&&!float.IsInfinity(f);}
        static float ScaleError(Vector3 s)
        {
            return Mathf.Max(Mathf.Abs(s.x-1),Mathf.Abs(s.y-1),Mathf.Abs(s.z-1));
        }
        static bool Free(Vector2 p,WorldData world,float margin)
        {
            if(p.x<world.boundsMin.x+margin||p.x>world.boundsMax.x-margin||
               p.y<world.boundsMin.y+margin||p.y>world.boundsMax.y-margin)return false;
            foreach(Rect r in world.obstacles)
            {
                Vector2 near=new Vector2(Mathf.Clamp(p.x,r.xMin,r.xMax),Mathf.Clamp(p.y,r.yMin,r.yMax));
                if(Vector2.Distance(p,near)<margin)return false;
            }
            return true;
        }
        void Shot(string scenario,string phase,RushGame.ImpactProbe p)
        {
            string path=Path.Combine(output,scenario+"-"+phase+".png");
            Texture2D texture=null;
            try
            {
                texture=ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(path,texture.EncodeToPNG());
                report.screenshots.Add(new ShotEvidence{
                    path=path,scenario=scenario,phase=phase,time=game.TotalTime,
                    height=p.height,spinDegrees=p.spinDegrees,freezeRemaining=game.ImpactFreezeRemaining,
                    position=p.position,visualScale=p.visualScale,landCount=p.landCount,renderedFrame=Time.frameCount
                });
            }
            catch(Exception e){Assert("Screenshot "+scenario+"/"+phase,false,e.Message);}
            finally{if(texture!=null)Destroy(texture);}
        }

        static bool Compressed(RushGame.ImpactProbe p)
        {
            if(p.launchCount<=0||p.height>=.25f||p.landCount!=0)return false;
            Vector3 direction=new Vector3(p.launchDirection.x,0,p.launchDirection.y);
            Vector3 local=Quaternion.Inverse(p.visualRotation)*direction;
            return Vector3.Scale(local.normalized,p.visualScale).magnitude<.98f&&p.visualScale.y>1.02f;
        }

        IEnumerator CapturePhase(string scenario,string phase)
        {
            // Detecting a phase only schedules a capture. A subsequent rendered frame
            // supplies both the framebuffer and a fresh, checked telemetry snapshot.
            yield return new WaitForEndOfFrame();
            var probes=game.ImpactProbeSnapshot();
            lastCaptureOK=probes.Length>0;
            if(lastCaptureOK)
            {
                var p=probes[0];
                if(phase=="compression")lastCaptureOK=Compressed(p);
                else if(phase=="airborne")lastCaptureOK=p.airborne&&p.height>.35f;
                else if(phase=="apex")lastCaptureOK=p.airborne&&p.height>1f;
                else if(phase=="tumble")lastCaptureOK=p.airborne&&p.spinDegrees>120f&&p.height>.25f;
                else if(phase=="landing-rebound")lastCaptureOK=!p.airborne&&p.landCount>0&&ScaleError(p.visualScale)>.025f;
                else if(phase=="settled")lastCaptureOK=!p.airborne&&!p.recovering&&p.height<.005f&&ScaleError(p.visualScale)<.03f;
                Assert(scenario+": screenshot phase "+phase,lastCaptureOK,
                    "render frame="+Time.frameCount+" height="+p.height+" spin="+p.spinDegrees+" scale="+p.visualScale);
                if(lastCaptureOK)Shot(scenario,phase,p);
            }
            else Assert(scenario+": screenshot target exists",false,phase);
        }

        IEnumerator Suite()
        {
            yield return Wait(.25f);
            yield return RunCase("front-idle","front",false,false,true);
            yield return RunCase("front-normal","front",false,false,false);
            yield return RunCase("front-dash","front",true,false,false);
            if(report.cases.Count>=3)
            {
                var idle=report.cases[0];var normal=report.cases[1];var dash=report.cases[2];
                Assert("Ordinary contact remains a grounded light push",
                    normal.normalContacts>0&&normal.launches==0&&normal.peakHeight<.025f,
                    "contacts="+normal.normalContacts+" launches="+normal.launches+" height="+normal.peakHeight);
                Assert("Dash displacement clearly exceeds ordinary push",
                    dash.earlyDisplacement>normal.earlyDisplacement+.6f&&
                    dash.earlyDisplacement>normal.earlyDisplacement*1.5f,
                    "at ~0.9s idle/normal/dash="+idle.earlyDisplacement+"/"+normal.earlyDisplacement+"/"+dash.earlyDisplacement+
                    " sample times="+idle.comparisonTime+"/"+normal.comparisonTime+"/"+dash.comparisonTime);
                Assert("Front impact travels mainly forward",
                    dash.earlyDelta.y>1f&&Mathf.Abs(dash.earlyDelta.x)<dash.earlyDelta.y*.5f,
                    "actual delta="+dash.earlyDelta+" launch direction="+dash.launchDirection);
            }
            yield return RunCase("side-dash","side",true,false,false);
            var side=report.cases[report.cases.Count-1];
            Assert("Side impact sends the target outward and forward",
                side.earlyDelta.x>.12f&&side.earlyDelta.y>.5f,
                "actual delta="+side.earlyDelta+" launch direction="+side.launchDirection);
            yield return RunCase("chain-dash","chain",true,false,false);
            var chain=report.cases[report.cases.Count-1];
            Assert("Crowd fixture exercises a real secondary chain",
                chain.chainLaunches>0&&chain.maxDepth>0&&chain.uniqueTargets>=3,
                "unique="+chain.uniqueTargets+" secondary="+chain.chainLaunches+" depth="+chain.maxDepth);
            Assert("Nine-target fixture reaches the six-target pressure limit",
                chain.initialActive>=9&&chain.uniqueTargets==6,
                "available="+chain.initialActive+" launched="+chain.uniqueTargets+
                "; fewer than six means the saturation limit was not exercised");
            yield return RunCase("boundary-dash","boundary",true,false,false);
            yield return RunCase("front-visual","front",true,false,false,true);
            yield return RunCase("visual-pause","front",true,true,false);
            var visual=report.cases[report.cases.Count-1];
            Assert("Impact visual phases were actually sampled",
                visual.sawCompression&&visual.sawAir&&visual.sawLandingRebound,
                "compression="+visual.sawCompression+" airborne="+visual.sawAir+" rebound="+visual.sawLandingRebound);
            Assert("Pause was exercised during hitstop, flight and landing rebound",
                visual.pauseHitstop&&visual.pauseAirborne&&visual.pauseLanding,
                "hitstop="+visual.pauseHitstop+" airborne="+visual.pauseAirborne+" landing="+visual.pauseLanding);
            yield return RestartDuringFlight();
            Neutral();
            yield return Wait(.1f);
            Finish();
        }

        IEnumerator RunCase(string name,string fixture,bool dash,bool pausePhases,bool idle,bool captureOnly=false)
        {
            Neutral();
            bool seeded=game.SetupImpactFixture(fixture);
            Assert(name+": fixture initializes only through the gated hook",
                seeded&&game.ImpactFixtureReady&&game.ImpactFixtureKind==fixture,
                "ready="+game.ImpactFixtureReady+" kind="+game.ImpactFixtureKind);
            if(!seeded){Finish();yield break;}
            yield return endFrame;
            Neutral();
            yield return endFrame;
            var initial=game.ImpactProbeSnapshot();
            Assert(name+": fixture has a primary active target",
                initial.Length>0&&initial[0].active,"slots="+initial.Length);
            if(initial.Length==0){Finish();yield break;}
            var evidence=new CaseEvidence{name=name,fixture=fixture,dash=dash,
                primaryStart=initial[0].position,initialCollisionRadius=game.PlayerCollisionRadius};
            running=evidence;
            report.cases.Add(evidence);
            foreach(var p in initial)if(p.active)evidence.initialActive++;
            bool screenshotCase=pausePhases||fixture=="chain"||captureOnly;
            if(screenshotCase)
            {
                yield return CapturePhase(name,"before");
                initial=game.ImpactProbeSnapshot();
                evidence.primaryStart=initial[0].position;
            }
            float begin=game.TotalTime;
            float wallLimit=Time.realtimeSinceStartup+18f;
            float desiredDuration=pausePhases||captureOnly?5.5f:4.5f;
            Vector2 direction=fixture=="boundary"?Vector2.right:Vector2.up;
            Vector2 launchPlayer=game.Position;
            bool dashSent=false,compared=false,advanceMeasured=false;
            bool compressionAttempted=false,airAttempted=false,apexAttempted=false,tumbleAttempted=false,landingAttempted=false;
            string pendingShot=null;
            bool navStarted=false,skipDiff=true;
            float navBegin=0,navProgress=0;
            int navGeneration=0;
            Vector2 navPosition=Vector2.zero;
            var previous=initial;
            Vector2 previousPlayer=game.Position;
            float previousGameTime=game.TotalTime,previousFixedTime=Time.fixedTime,previousFreeze=0,previousReal=Time.realtimeSinceStartup;
            float freezeWallStart=-1;
            int[] launchBase=new int[initial.Length];
            bool[] hit=new bool[initial.Length];
            for(int i=0;i<initial.Length;i++){launchBase[i]=initial[i].launchCount;}
            int firstDashId=-1;
            while(!finished&&game.TotalTime-begin<desiredDuration&&Time.realtimeSinceStartup<wallLimit)
            {
                float elapsed=game.TotalTime-begin;
                game.QAMove=!idle&&elapsed<.72f?direction:Vector2.zero;
                game.QABrace=false;
                if(dash&&!dashSent&&elapsed>=.04f){game.QADash=true;dashSent=true;}
                if(pendingShot!=null)
                {
                    string phase=pendingShot;pendingShot=null;
                    yield return CapturePhase(name,phase);
                    if(lastCaptureOK)
                    {
                        if(phase=="compression")evidence.capturedCompression=true;
                        else if(phase=="airborne")evidence.capturedAir=true;
                        else if(phase=="apex")evidence.capturedApex=true;
                        else if(phase=="tumble")evidence.capturedTumble=true;
                        else if(phase=="landing-rebound")evidence.capturedLanding=true;
                    }
                }
                else yield return endFrame;
                if(finished)yield break;
                elapsed=game.TotalTime-begin;
                var probes=game.ImpactProbeSnapshot();
                var crowd=game.CrowdProbeSnapshot();
                if(probes.Length==0){Assert(name+": target snapshot remains available",false,"empty");Finish();yield break;}
                var primary=probes[0];
                evidence.samples++;
                float real=Time.realtimeSinceStartup;
                float gap=Mathf.Max(.0001f,real-previousReal);
                float physicsGap=Time.fixedTime-previousFixedTime;
                if(!skipDiff&&physicsGap>.0001f)
                    evidence.maxPlayerDifferentialSpeed=Mathf.Max(evidence.maxPlayerDifferentialSpeed,
                        Vector2.Distance(game.Position,previousPlayer)/physicsGap);
                evidence.maxPlayerVelocity=Mathf.Max(evidence.maxPlayerVelocity,game.Velocity.magnitude);
                if(!skipDiff)evidence.maxFrameGap=Mathf.Max(evidence.maxFrameGap,gap);
                float freeze=game.ImpactFreezeRemaining;
                evidence.maxFreezeRemaining=Mathf.Max(evidence.maxFreezeRemaining,freeze);
                if(freeze>0&&previousFreeze<=0)
                {
                    evidence.freezeEpisodes++;
                    freezeWallStart=real;
                }
                if(freeze<=0&&previousFreeze>0&&freezeWallStart>=0)
                {
                    evidence.longestFreezeWallSeconds=Mathf.Max(evidence.longestFreezeWallSeconds,real-freezeWallStart);
                    freezeWallStart=-1;
                }
                if(!skipDiff&&freeze>0&&previousFreeze>0)
                {
                    evidence.frozenPhysicsSamples++;
                    evidence.frozenTimerAdvance+=Mathf.Max(0,game.TotalTime-previousGameTime);
                    float delta=Vector2.Distance(game.Position,previousPlayer);
                    for(int i=0;i<Mathf.Min(previous.Length,probes.Length);i++)
                        delta=Mathf.Max(delta,Vector2.Distance(previous[i].position,probes[i].position)+Mathf.Abs(previous[i].height-probes[i].height));
                    evidence.maxFrozenPhysicsDelta=Mathf.Max(evidence.maxFrozenPhysicsDelta,delta);
                }
                evidence.maxRootScaleError=Mathf.Max(evidence.maxRootScaleError,ScaleError(game.PlayerRootScale));
                evidence.physicalRootsStable &= ScaleError(game.PlayerRootScale)<.001f&&
                    Mathf.Abs(game.PlayerCollisionRadius-evidence.initialCollisionRadius)<.00001f;
                evidence.peakPlayerScaleError=Mathf.Max(evidence.peakPlayerScaleError,ScaleError(game.PlayerVisualScale));
                evidence.finiteAndContained &= Finite(game.Position.x)&&Finite(game.Position.y)&&
                    Free(game.Position,game.World,game.PlayerCollisionRadius-.006f);
                if(dash)evidence.noPlayerStumble &= !game.IsStumbling;
                for(int i=0;i<probes.Length;i++)
                {
                    var p=probes[i];
                    if(p.active)
                    {
                        evidence.finiteAndContained &= Finite(p.position.x)&&Finite(p.position.y)&&Finite(p.height)&&
                            Finite(p.velocity.x)&&Finite(p.velocity.y)&&p.height>=-.005f&&p.height<4f&&Free(p.position,game.World,NpcClearance);
                        if(p.velocity.magnitude>evidence.maxVelocity)
                        {
                            evidence.maxVelocity=p.velocity.magnitude;evidence.peakVelocitySlot=i;
                            evidence.peakVelocityAt=elapsed;evidence.peakVelocityHeight=p.height;evidence.peakVelocityRecovery=p.recoveryLeft;
                            evidence.peakVelocityPhase=p.airborne?"airborne":p.recovering?"recovering":"grounded";
                            evidence.peakVelocityPosition=p.position;
                            evidence.peakVelocityPrevious=i<previous.Length?previous[i].position:p.position;
                            evidence.peakVelocityPlayer=game.Position;
                        }
                        if(p.airborne)evidence.maxAirVelocity=Mathf.Max(evidence.maxAirVelocity,p.velocity.magnitude);
                        else evidence.maxGroundVelocity=Mathf.Max(evidence.maxGroundVelocity,p.velocity.magnitude);
                        evidence.maxRootScaleError=Mathf.Max(evidence.maxRootScaleError,ScaleError(p.rootScale));
                        evidence.physicalRootsStable &= ScaleError(p.rootScale)<.001f;
                    }
                    if(i<previous.Length&&!skipDiff&&p.generation==previous[i].generation&&p.active&&previous[i].active&&physicsGap>.0001f)
                    {
                        float actualSpeed=Vector2.Distance(p.position,previous[i].position)/physicsGap;
                        evidence.maxDifferentialSpeed=Mathf.Max(evidence.maxDifferentialSpeed,actualSpeed);
                        // The frame which lands can still contain a final flight step.
                        if(p.airborne||previous[i].airborne)
                            evidence.maxAirDifferentialSpeed=Mathf.Max(evidence.maxAirDifferentialSpeed,actualSpeed);
                        else evidence.maxGroundDifferentialSpeed=Mathf.Max(evidence.maxGroundDifferentialSpeed,actualSpeed);
                    }
                    if(i<previous.Length&&previous[i].airborne)
                        evidence.noAirborneRecycle &= p.active&&p.generation==previous[i].generation;
                    if(i<launchBase.Length&&p.launchCount>launchBase[i])
                    {
                        if(!hit[i]){hit[i]=true;evidence.launchedSlots.Add(i);}
                        evidence.duplicateLaunches=Mathf.Max(evidence.duplicateLaunches,p.launchCount-launchBase[i]-1);
                        if(firstDashId<0)firstDashId=p.dashId;
                        if(p.dashId!=firstDashId)Assert(name+": one input pulse owns all launches",false,"slot="+i+" dashId="+p.dashId+" expected="+firstDashId);
                    }
                }
                evidence.peakHeight=Mathf.Max(evidence.peakHeight,primary.height);
                evidence.peakSpin=Mathf.Max(evidence.peakSpin,Mathf.Abs(primary.spinDegrees));
                evidence.sawAir |= primary.airborne&&primary.height>.25f;
                evidence.sawRecovery |= primary.recovering;
                evidence.primaryLandCount=Mathf.Max(evidence.primaryLandCount,primary.landCount);
                if(primary.launchCount>launchBase[0]&&evidence.primaryLaunchTime<0)
                {
                    evidence.primaryLaunchTime=elapsed;
                    evidence.launchDirection=primary.launchDirection;
                    launchPlayer=game.Position;
                }
                if(evidence.primaryLaunchTime>=0&&!advanceMeasured&&elapsed>=evidence.primaryLaunchTime+.45f)
                {
                    evidence.playerAdvance=Vector2.Dot(game.Position-launchPlayer,direction);
                    advanceMeasured=true;
                }
                if(evidence.primaryLaunchTime>=0&&elapsed<evidence.primaryLaunchTime+.45f&&fixture!="boundary")
                    evidence.playerForward &= Vector2.Dot(game.Velocity,direction)>-.1f;
                if(!compared&&elapsed>=.9f)
                {
                    evidence.comparisonTime=elapsed;
                    evidence.earlyDelta=primary.position-evidence.primaryStart;
                    evidence.earlyDisplacement=evidence.earlyDelta.magnitude;
                    compared=true;
                }
                evidence.sawCompression |= Compressed(primary);
                bool landingRebound=primary.landCount>0&&!primary.airborne&&ScaleError(primary.visualScale)>.025f;
                evidence.sawLandingRebound |= landingRebound;
                if(primary.landCount>0&&!primary.airborne&&!primary.recovering&&crowd.Length>0)
                {
                    if(!navStarted)
                    {
                        navStarted=true;navBegin=elapsed;navProgress=crowd[0].progress;
                        navGeneration=crowd[0].generation;navPosition=primary.position;
                    }
                    else if(crowd[0].generation==navGeneration&&elapsed-navBegin>.5f)
                    {
                        evidence.navigationProgress=crowd[0].progress-navProgress;
                        evidence.navigationDistance=Vector2.Distance(primary.position,navPosition);
                        evidence.recoveredNavigation |= evidence.navigationProgress>.4f&&evidence.navigationDistance>.4f;
                    }
                }
                bool paused=false;
                if(screenshotCase&&pendingShot==null)
                {
                    if(!compressionAttempted&&Compressed(primary))
                    {pendingShot="compression";compressionAttempted=true;}
                    else if(!airAttempted&&primary.airborne&&primary.height>.55f)
                    {pendingShot="airborne";airAttempted=true;}
                    else if(!apexAttempted&&primary.airborne&&primary.height>1.02f)
                    {pendingShot="apex";apexAttempted=true;}
                    else if(!tumbleAttempted&&primary.airborne&&primary.spinDegrees>125f&&primary.height>.3f)
                    {pendingShot="tumble";tumbleAttempted=true;}
                    else if(!landingAttempted&&landingRebound)
                    {pendingShot="landing-rebound";landingAttempted=true;}
                }
                if(pausePhases&&!evidence.pauseHitstop&&freeze>0&&evidence.capturedCompression)
                {
                    evidence.pauseHitstop=true;yield return PauseCheck(name+" hitstop");
                    paused=true;
                }
                else if(pausePhases&&!evidence.pauseAirborne&&primary.airborne&&primary.height>.35f&&evidence.capturedAir)
                {
                    evidence.pauseAirborne=true;yield return PauseCheck(name+" airborne");
                    paused=true;
                }
                else if(pausePhases&&!evidence.pauseLanding&&landingRebound&&evidence.capturedLanding)
                {
                    evidence.pauseLanding=true;yield return PauseCheck(name+" landing");
                    paused=true;
                }
                if(paused)
                {
                    // Pausing intentionally clears short effects, including hitstop and squash.
                    // Reset differential baselines so a UI action is never mistaken for movement.
                    probes=game.ImpactProbeSnapshot();
                    skipDiff=true;previousFreeze=game.ImpactFreezeRemaining;
                    freezeWallStart=-1;
                }
                else{skipDiff=false;previousFreeze=freeze;}
                previous=probes;previousPlayer=game.Position;previousGameTime=game.TotalTime;previousFixedTime=Time.fixedTime;
                previousReal=paused?Time.realtimeSinceStartup:real;
            }
            Neutral();
            var last=game.ImpactProbeSnapshot();
            evidence.duration=game.TotalTime-begin;
            evidence.primaryEnd=last[0].position;
            evidence.uniqueTargets=evidence.launchedSlots.Count;
            evidence.launches=game.TotalLaunches;
            evidence.chainLaunches=game.ChainLaunches;
            evidence.maxDepth=game.MaxObservedChainDepth;
            evidence.maxTargets=game.MaxObservedDashTargets;
            evidence.normalContacts=game.NormalContactCount;
            evidence.finalScaleError=ScaleError(game.PlayerVisualScale);
            bool allSettled=true;
            foreach(var p in last)
            {
                if(p.active)
                {
                    allSettled &= !p.airborne&&!p.recovering&&Mathf.Abs(p.height)<.005f;
                    evidence.finalScaleError=Mathf.Max(evidence.finalScaleError,ScaleError(p.visualScale));
                }
            }
            if(screenshotCase)
            {
                yield return CapturePhase(name,"settled");
                Assert(name+": compression, apex, tumble and rebound are captured",
                    evidence.capturedCompression&&evidence.capturedAir&&evidence.capturedApex&&evidence.capturedTumble&&evidence.capturedLanding,
                    "compression="+evidence.capturedCompression+" air="+evidence.capturedAir+" apex="+evidence.capturedApex+
                    " tumble="+evidence.capturedTumble+" rebound="+evidence.capturedLanding);
            }
            Assert(name+": fixture completes without an endless freeze",evidence.duration>=desiredDuration-.05f,
                "simulation seconds="+evidence.duration+" samples="+evidence.samples);
            Assert(name+": bodies remain finite and contained",evidence.finiteAndContained,
                "NPC clearance="+NpcClearance+" player radius="+evidence.initialCollisionRadius);
            Assert(name+": physical roots and collision radius stay unchanged",evidence.physicalRootsStable,
                "max root scale error="+evidence.maxRootScaleError);
            Assert(name+": airborne bodies never disappear or recycle",evidence.noAirborneRecycle,"sampled slots="+last.Length);
            Assert(name+": planar impulse and actual displacement stay bounded",
                evidence.maxGroundVelocity<=GroundSpeedLimit&&evidence.maxGroundDifferentialSpeed<=GroundSpeedLimit&&
                evidence.maxAirVelocity<=AirSpeedLimit&&evidence.maxAirDifferentialSpeed<=AirSpeedLimit,
                "all velocity/differential="+evidence.maxVelocity+"/"+evidence.maxDifferentialSpeed+
                " ground="+evidence.maxGroundVelocity+"/"+evidence.maxGroundDifferentialSpeed+
                " air="+evidence.maxAirVelocity+"/"+evidence.maxAirDifferentialSpeed+
                "; independent position differences use elapsed FixedUpdate clock; ground limit=12.25 air limit=14");
            Assert(name+": player separation does not transfer a movement spike",
                evidence.maxPlayerVelocity<=13.1f&&evidence.maxPlayerDifferentialSpeed<=18f,
                "velocity="+evidence.maxPlayerVelocity+" actual fixed-time differential="+evidence.maxPlayerDifferentialSpeed+
                "; limit includes 13m/s dash plus bounded separation correction");
            Assert(name+": all bodies land and finish recovery",allSettled,
                "primary lands="+evidence.primaryLandCount+" final height="+last[0].height);
            Assert(name+": jelly returns to neutral",evidence.finalScaleError<.03f,"max scale error="+evidence.finalScaleError);
            Assert(name+": hitstop budget is bounded and not repeatedly refreshed",
                evidence.maxFreezeRemaining<=FreezeBudget+.002f&&evidence.freezeEpisodes<=1&&
                evidence.longestFreezeWallSeconds<=FreezeBudget+evidence.maxFrameGap+.04f,
                "remaining max="+evidence.maxFreezeRemaining+" episodes="+evidence.freezeEpisodes+
                " longest wall="+evidence.longestFreezeWallSeconds+" max sample gap="+evidence.maxFrameGap);
            Assert(name+": hitstop holds physical motion",evidence.maxFrozenPhysicsDelta<.003f&&
                (!dash||pausePhases||fixture!="front"||evidence.frozenPhysicsSamples>0),
                "samples="+evidence.frozenPhysicsSamples+" max physical delta="+evidence.maxFrozenPhysicsDelta+
                " countdown advances="+evidence.frozenTimerAdvance);
            if(dash)
            {
                Assert(name+": primary visibly launches, rotates and lands",
                    evidence.sawAir&&evidence.peakSpin>15&&evidence.primaryLandCount>=1,
                    "height="+evidence.peakHeight+" spin="+evidence.peakSpin+" lands="+evidence.primaryLandCount);
                Assert(name+": landing restores forward navigation",evidence.recoveredNavigation,
                    "route progress="+evidence.navigationProgress+" displacement="+evidence.navigationDistance);
                Assert(name+": one dash has unique targets and bounded chain depth",
                    evidence.uniqueTargets>0&&evidence.uniqueTargets<=6&&evidence.maxTargets<=6&&
                    evidence.maxDepth<=2&&evidence.duplicateLaunches==0&&evidence.uniqueTargets==evidence.launches,
                    "independent unique="+evidence.uniqueTargets+" public launches="+evidence.launches+
                    " max targets="+evidence.maxTargets+" depth="+evidence.maxDepth+" repeats="+evidence.duplicateLaunches);
                Assert(name+": own dash causes no player stumble",evidence.noPlayerStumble,"total stumble="+game.TotalStumbleTime);
                if(fixture!="boundary"&&!pausePhases)
                    Assert(name+": player keeps moving through its impact",
                        evidence.playerForward&&evidence.playerAdvance>1f,
                        "forward="+evidence.playerForward+" advance in .45s="+evidence.playerAdvance);
            }
            Debug.Log("RUSH_IMPACT_CASE "+JsonUtility.ToJson(evidence));
            running=null;
        }

        IEnumerator PauseCheck(string label)
        {
            var before=game.ImpactProbeSnapshot();
            Neutral();game.TogglePause();
            yield return endFrame;
            var frozen=game.ImpactProbeSnapshot();
            float time=game.TotalTime,stageTime=game.StageTime,fxClock=game.ImpactFxClock;
            int fxCount=game.ActiveImpactFxCount;
            Vector2 player=game.Position;
            Vector3 camera=game.GameCamera.transform.position;
            Quaternion cameraRotation=game.GameCamera.transform.rotation;
            bool neutral=game.ImpactFreezeRemaining<.00001f&&ScaleError(game.PlayerVisualScale)<.001f&&fxCount==0;
            bool flightPreserved=before.Length==frozen.Length;
            for(int i=0;i<frozen.Length;i++)
            {
                neutral &= ScaleError(frozen[i].visualScale)<.001f;
                if(i<before.Length)
                    flightPreserved &= Vector2.Distance(before[i].position,frozen[i].position)<.002f&&
                        Mathf.Abs(before[i].height-frozen[i].height)<.002f&&before[i].airborne==frozen[i].airborne;
            }
            Assert(label+": pause clears short effects but preserves flight",neutral&&flightPreserved,
                "neutral="+neutral+" preserves flight="+flightPreserved+" freeze="+game.ImpactFreezeRemaining+" fx="+fxCount);
            yield return Wait(.25f);
            yield return endFrame;
            var after=game.ImpactProbeSnapshot();
            bool stable=after.Length==frozen.Length;
            for(int i=0;i<Mathf.Min(after.Length,frozen.Length);i++)
            {
                var a=frozen[i];var b=after[i];
                stable &= Vector2.Distance(a.position,b.position)<.001f&&Mathf.Abs(a.height-b.height)<.001f&&
                    Vector2.Distance(a.velocity,b.velocity)<.001f&&Mathf.Abs(a.recoveryLeft-b.recoveryLeft)<.001f&&
                    Mathf.Abs(a.spinDegrees-b.spinDegrees)<.001f&&Vector3.Distance(a.visualScale,b.visualScale)<.001f&&
                    Quaternion.Angle(a.visualRotation,b.visualRotation)<.01f&&a.airborne==b.airborne&&
                    a.active==b.active&&a.generation==b.generation&&a.launchCount==b.launchCount&&a.landCount==b.landCount;
            }
            stable &= game.State==RushState.Paused&&Mathf.Abs(game.TotalTime-time)<.001f&&
                Mathf.Abs(game.StageTime-stageTime)<.001f&&Vector2.Distance(game.Position,player)<.001f&&
                Vector3.Distance(game.GameCamera.transform.position,camera)<.001f&&
                Quaternion.Angle(game.GameCamera.transform.rotation,cameraRotation)<.01f&&
                Mathf.Abs(game.ImpactFxClock-fxClock)<.001f&&game.ActiveImpactFxCount==fxCount&&game.ImpactFreezeRemaining<.00001f;
            Assert(label+": paused flight, FX, camera and timers remain frozen",stable,
                "timer delta="+(game.TotalTime-time)+" fx delta="+(game.ImpactFxClock-fxClock));
            game.TogglePause();Neutral();
            yield return endFrame;
            Assert(label+": resume does not replay old hitstop",game.ImpactFreezeRemaining<.00001f,
                "remaining="+game.ImpactFreezeRemaining);
        }

        IEnumerator RestartDuringFlight()
        {
            Neutral();
            bool seeded=game.SetupImpactFixture("front");
            Assert("Restart fixture initializes",seeded,"front");
            if(!seeded)yield break;
            yield return endFrame;
            Neutral();yield return endFrame;
            game.QAMove=Vector2.up;game.QADash=true;
            float limit=Time.realtimeSinceStartup+3f;
            bool airborne=false;
            while(!finished&&Time.realtimeSinceStartup<limit)
            {
                yield return endFrame;
                var p=game.ImpactProbeSnapshot();
                if(p.Length>0&&p[0].airborne&&p[0].height>.3f){airborne=true;break;}
            }
            Assert("Restart actually interrupts airborne impact",airborne,"launches="+game.TotalLaunches);
            game.Restart();Neutral();
            yield return endFrame;
            var reset=game.ImpactProbeSnapshot();
            bool clean=game.Stage==0&&game.ImpactFreezeRemaining<.00001f&&game.ActiveImpactFxCount==0&&
                game.TotalLaunches==0&&game.ChainLaunches==0&&ScaleError(game.PlayerVisualScale)<.001f&&
                ScaleError(game.PlayerRootScale)<.001f&&!game.IsStumbling&&!game.IsDashing;
            foreach(var p in reset)
                clean &= !p.airborne&&!p.recovering&&Mathf.Abs(p.height)<.001f&&p.launchCount==0&&
                    p.landCount==0&&Mathf.Abs(p.spinDegrees)<.001f&&ScaleError(p.visualScale)<.001f&&ScaleError(p.rootScale)<.001f;
            Assert("Restart clears every impact and visual state",clean,
                "slots="+reset.Length+" launches="+game.TotalLaunches+" FX="+game.ActiveImpactFxCount+" freeze="+game.ImpactFreezeRemaining);
        }

        void Finish()
        {
            if(finished)return;
            finished=true;StopAllCoroutines();Neutral();
            report.elapsedRealSeconds=Time.realtimeSinceStartup-started;
            report.passed=report.failureCount==0;
            File.WriteAllText(Path.Combine(output,"impact-report.json"),JsonUtility.ToJson(report,true));
            Debug.Log("RUSH_IMPACT_QA_FINISHED passed="+report.passed+" failures="+report.failureCount+" output="+output);
            Application.Quit(report.passed?0:1);
        }
    }
}

