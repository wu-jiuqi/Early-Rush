using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace EarlyRush
{
    // Opt-in fixture initialization followed exclusively by normal player input.
    public class RushCombatQA : MonoBehaviour
    {
        [Serializable] public class Check { public string name,evidence; public bool passed; }
        [Serializable] public class AttackEvidence
        {
            public int slot,id; public Vector2 origin,direction;
            public float length,radius,duration,warningAt,rushAt=-1,observedLead,firstObservedWarningLeft;
            public float maxAngleError,maxOriginError,maxPathError;
            public bool warningInViewport,cancelled;
        }
        [Serializable] public class CaseEvidence
        {
            public string name,fixture,strategy;
            public int samples,playerLaunches,lands,braceBlocks,protectedHits,npcLaunches,maxThreats,maxBurstHistory;
            public int groundedProtectedContacts,verifiedHitSweeps,outsideRushSamples;
            public float peakHeight,peakSpin,maxPlayerSpeed,maxPositionSpeed,maxNpcSpeed,maxWarningFrameGap;
            public float firstWarningAt=-1,firstLaunchAt=-1,movedAfterRecovery,minimumOutsideMargin=999;
            public float longestAirSeconds,finalScaleError,minimumWarningLead=999,pauseRushDistance;
            public bool finiteAndContained=true,rootStable=true,pauseExercised,shotWarning,shotCompression,shotAir,shotBrace;
            public List<AttackEvidence> attacks=new List<AttackEvidence>();
        }
        [Serializable] public class Shot
        {
            public string path,phase,scenario; public int frame;
            public float gameTime,realBefore,realAfter,height,spin,warningLeft,protection;
            public Vector3 playerScale;
        }
        [Serializable] public class Report
        {
            public string utc,engine,device;
            public string schema="v4-combat-2-edges";
            public string scope="Actual Windows Player input-driven fixtures. Setup only seeds actors and schedules warnings. No forced hits, teleport or time override; not a human or OS-keyboard playtest. Full routes are separate.";
            public bool passed; public int failureCount; public float elapsedRealSeconds;
            public List<Check> checks=new List<Check>(); public List<CaseEvidence> cases=new List<CaseEvidence>();
            public List<Shot> screenshots=new List<Shot>();
        }
        RushGame game; string output; float began; bool finished;
        readonly Report report=new Report();
        readonly WaitForEndOfFrame eof=new WaitForEndOfFrame();
        void Start()
        {
            game=GetComponent<RushGame>();game.QAInput=true;
            output=Path.Combine(Path.GetDirectoryName(Application.dataPath),"CombatQA");
            foreach(string a in Environment.GetCommandLineArgs())
                if(a.StartsWith("--combat-qa-output=",StringComparison.Ordinal))output=a.Substring(19);
            Directory.CreateDirectory(output);began=Time.realtimeSinceStartup;
            report.utc=DateTime.UtcNow.ToString("o");report.engine=Application.unityVersion;report.device=SystemInfo.graphicsDeviceName;
            Application.logMessageReceived+=OnLog;StartCoroutine(Suite());
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
        void OnLog(string text,string trace,LogType type)
        {if(!finished&&(type==LogType.Exception||type==LogType.Error))Assert("No runtime error",false,text+"\n"+trace);}
        void Update(){if(!finished&&Time.realtimeSinceStartup-began>180){Assert("Suite timeout",false,"180 real seconds");Finish();}}
        void Assert(string name,bool passed,string evidence)
        {
            if(finished)return;report.checks.Add(new Check{name=name,passed=passed,evidence=evidence});
            if(!passed)report.failureCount++;
            Debug.Log("RUSH_COMBAT_QA_CHECK "+(passed?"PASS ":"FAIL ")+name+" | "+evidence);
        }
        void Neutral(){game.QAMove=Vector2.zero;game.QADash=false;game.QABrace=false;}
        IEnumerator Wait(float seconds){float end=Time.realtimeSinceStartup+seconds;while(!finished&&Time.realtimeSinceStartup<end)yield return null;}
        static float ScaleError(Vector3 v)=>Mathf.Max(Mathf.Abs(v.x-1),Mathf.Abs(v.y-1),Mathf.Abs(v.z-1));
        static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
        {Vector2 ab=b-a;return Vector2.Distance(p,a+ab*(ab.sqrMagnitude>.000001f?Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude):0));}
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        bool Free(Vector2 p,float radius)
        {
            var w=game.World;
            if(p.x<w.boundsMin.x+radius||p.x>w.boundsMax.x-radius||p.y<w.boundsMin.y+radius||p.y>w.boundsMax.y-radius)return false;
            foreach(Rect r in w.obstacles)
                if(Vector2.Distance(p,new Vector2(Mathf.Clamp(p.x,r.xMin,r.xMax),Mathf.Clamp(p.y,r.yMin,r.yMax)))<radius)return false;
            return true;
        }
        IEnumerator Capture(string scenario,string phase)
        {
            yield return new WaitForEndOfFrame();
            var p=game.PlayerCombatProbeSnapshot();var npcs=game.CombatProbeSnapshot();
            bool phaseOK=phase=="before"||phase=="settled"||
                (phase=="warning"&&game.CombatWarningCount>0)||
                (phase=="compression"&&p.airborne&&p.height<.3f&&ScaleError(p.visualScale)>.03f)||
                (phase=="air"&&p.airborne&&p.height>=1.1f&&p.spinDegrees>=100f)||
                (phase=="brace"&&p.braceSuccessLeft>0);
            Assert(scenario+": capture "+phase,phaseOK,"frame="+Time.frameCount+" height="+p.height+" scale="+p.visualScale);
            if(!phaseOK)yield break;
            string path=Path.Combine(output,scenario+"-"+phase+".png");
            float before=Time.realtimeSinceStartup;
            Debug.Log("RUSH_COMBAT_CAPTURE_BEGIN "+phase+" realtime="+before+" frame="+Time.frameCount);
            Texture2D texture=null;
            try
            {
                texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(path,texture.EncodeToPNG());
                report.screenshots.Add(new Shot{path=path,phase=phase,scenario=scenario,frame=Time.frameCount,
                    gameTime=game.TotalTime,realBefore=before,realAfter=Time.realtimeSinceStartup,height=p.height,
                    spin=p.spinDegrees,playerScale=p.visualScale,protection=p.protectionLeft,
                    warningLeft=npcs.Length>0?npcs[0].warningLeft:0});
            }
            finally{if(texture)Destroy(texture);}
            Debug.Log("RUSH_COMBAT_CAPTURE_END "+phase+" realtime="+Time.realtimeSinceStartup+" frame="+Time.frameCount);
        }
        IEnumerator Suite()
        {
            yield return Wait(.2f);
            yield return Run("front","front","");
            yield return Run("brace","brace","");
            yield return Run("dodge","dodge","");
            yield return Run("near-outside","near-outside","");
            yield return Run("near-inside","near-inside","");
            yield return Run("npc-chain","npc-chain","");
            yield return Run("interrupt-warning","front","");
            yield return Run("protection","protection","");
            yield return Run("boundary","boundary","");
            yield return Run("pause-warning","front","warning");
            yield return Run("pause-rush","front","rush");
            yield return Run("pause-air","front","air");
            yield return RestartAir();
            Finish();
        }
        IEnumerator Run(string name,string fixture,string pausePhase)
        {
            Neutral();bool ready=game.SetupCombatFixture(fixture);
            Assert(name+": initial fixture ready",ready&&game.CombatFixtureReady,"kind="+game.CombatFixtureKind);
            if(!ready){Finish();yield break;}
            yield return eof;Neutral();yield return eof;
            var c=new CaseEvidence{name=name,fixture=fixture,strategy=
                name=="interrupt-warning"?"After observing windup, move toward the NPC with a real downward Space dash to interrupt; stop issuing movement once the NPC launches.":
                fixture=="brace"?"Hold Shift after observing warning; no movement.":
                fixture=="dodge"?"Move right only after warning is observed; never brace.":
                fixture=="protection"?"Remain stationary for both independently scheduled attacks.":
                fixture=="boundary"?"After landing and recovery, use real left movement to leave the wall.":
                name=="front"?"Remain stationary until a hit, then move right after landing and recovery.":
                "Remain stationary; pause cases only toggle pause at the observed requested phase."};
            report.cases.Add(c);
            float start=game.TotalTime,wallEnd=Time.realtimeSinceStartup+18,duration=pausePhase!=""?6.5f:5.5f;
            float lastTime=game.TotalTime,lastFixed=Time.fixedTime,lastReal=Time.realtimeSinceStartup,airStart=-1;
            bool skipDiff=true,moveStarted=false,paused=false,interruptDashSent=false;
            Vector2 moveBase=game.Position;
            var previousPlayer=game.PlayerCombatProbeSnapshot();
            var previousNpc=game.CombatProbeSnapshot();
            var known=new AttackEvidence[previousNpc.Length];
            string pendingShot=null;
            while(!finished&&game.TotalTime-start<duration&&Time.realtimeSinceStartup<wallEnd)
            {
                var inputState=game.PlayerCombatProbeSnapshot();Neutral();
                if(fixture=="brace"&&c.firstWarningAt>=0)game.QABrace=true;
                if(fixture=="dodge"&&c.firstWarningAt>=0)game.QAMove=Vector2.right;
                if(name=="interrupt-warning"&&c.firstWarningAt>=0&&game.TotalLaunches==0)
                {
                    game.QAMove=Vector2.down;
                    if(!interruptDashSent){game.QADash=true;interruptDashSent=true;}
                }
                if((fixture=="boundary"||name=="front")&&inputState.landCount>0&&!inputState.airborne&&!inputState.recovering)
                {
                    if(!moveStarted){moveStarted=true;moveBase=game.Position;}
                    if(Vector2.Distance(moveBase,game.Position)<2)game.QAMove=fixture=="boundary"?Vector2.left:Vector2.right;
                }
                if(pendingShot!=null){string shot=pendingShot;pendingShot=null;yield return Capture(name,shot);}
                else yield return eof;
                if(finished)yield break;
                var p=game.PlayerCombatProbeSnapshot();var npc=game.CombatProbeSnapshot();
                float now=game.TotalTime,physicsGap=Time.fixedTime-lastFixed,real=Time.realtimeSinceStartup;
                c.samples++;c.maxWarningFrameGap=Mathf.Max(c.maxWarningFrameGap,real-lastReal);
                c.rootStable &= ScaleError(p.rootScale)<.001f;
                c.finiteAndContained &= Finite(p.position.x)&&Finite(p.position.y)&&Finite(p.height)&&
                    p.height>=-.005f&&p.height<3&&Free(p.position,.414f);
                c.peakHeight=Mathf.Max(c.peakHeight,p.height);c.peakSpin=Mathf.Max(c.peakSpin,Mathf.Abs(p.spinDegrees));
                c.maxPlayerSpeed=Mathf.Max(c.maxPlayerSpeed,p.velocity.magnitude);
                if(!skipDiff&&physicsGap>.0001f)
                    c.maxPositionSpeed=Mathf.Max(c.maxPositionSpeed,Vector2.Distance(p.position,previousPlayer.position)/physicsGap);
                if(p.airborne){if(airStart<0)airStart=now;c.longestAirSeconds=Mathf.Max(c.longestAirSeconds,now-airStart);}else airStart=-1;
                if(moveStarted)c.movedAfterRecovery=Mathf.Max(c.movedAfterRecovery,Vector2.Distance(moveBase,p.position));
                int threats=0;
                for(int i=0;i<npc.Length;i++)
                {
                    var n=npc[i];if(!n.active)continue;
                    if(n.visible&&(n.phase==1||n.phase==2))threats++;
                    if(!skipDiff&&i<previousNpc.Length&&previousNpc[i].active&&physicsGap>.0001f)
                        c.maxNpcSpeed=Mathf.Max(c.maxNpcSpeed,Vector2.Distance(n.position,previousNpc[i].position)/physicsGap);
                    bool newWarning=n.phase==1&&(known[i]==null||known[i].id!=n.attackId||
                        (i<previousNpc.Length&&previousNpc[i].phase!=1));
                    if(newWarning)
                    {
                        known[i]=new AttackEvidence{slot=i,id=n.attackId,origin=n.warningOrigin,direction=n.direction,
                            length=n.warningLength,radius=n.warningRadius,duration=n.warningDuration,
                            warningAt=now,warningInViewport=n.visible,firstObservedWarningLeft=n.warningLeft};
                        c.attacks.Add(known[i]);if(c.firstWarningAt<0)c.firstWarningAt=now-start;
                    }
                    var a=known[i];
                    if((n.phase==1||n.phase==2)&&a!=null&&a.id==n.attackId)
                    {
                        a.maxAngleError=Mathf.Max(a.maxAngleError,Vector2.Angle(a.direction,n.direction));
                        a.maxOriginError=Mathf.Max(a.maxOriginError,Vector2.Distance(a.origin,n.warningOrigin));
                        if(n.phase==2)
                        {
                            a.maxPathError=Mathf.Max(a.maxPathError,SegmentDistance(n.position,a.origin,a.origin+a.direction*a.length));
                            if(a.rushAt<0){a.rushAt=now;a.observedLead=now-a.warningAt;c.minimumWarningLead=Mathf.Min(c.minimumWarningLead,a.observedLead);}
                            if(fixture=="dodge"||fixture=="near-outside"||fixture=="near-inside")
                            {
                                c.outsideRushSamples++;
                                c.minimumOutsideMargin=Mathf.Min(c.minimumOutsideMargin,
                                    SegmentDistance(p.position,a.origin,a.origin+a.direction*a.length)-a.radius);
                            }
                        }
                    }
                    else if(n.phase==2)Assert(name+": every rush has observed warning",false,"slot="+i+" id="+n.attackId);
                    else if(a!=null&&a.rushAt<0&&n.phase!=1)a.cancelled=true;
                }
                c.maxThreats=Mathf.Max(c.maxThreats,threats);
                c.maxBurstHistory=Mathf.Max(c.maxBurstHistory,game.NpcBurstCount);
                bool newLaunch=p.launchCount>previousPlayer.launchCount;
                bool newBlock=p.braceSuccessCount>previousPlayer.braceSuccessCount;
                bool newProtected=p.protectedHitCount>previousPlayer.protectedHitCount;
                if(newLaunch||newBlock||newProtected)
                {
                    bool swept=false;int source=-1;
                    for(int i=0;i<npc.Length&&i<previousNpc.Length;i++)
                    {
                        bool rush=npc[i].phase==2||previousNpc[i].phase==2;
                        if(!npc[i].active||!rush)continue;
                        float distance=SegmentDistance(Vector2.zero,
                            previousNpc[i].position-previousPlayer.position,npc[i].position-p.position);
                        if(distance<=npc[i].warningRadius+.08f){swept=true;source=i;break;}
                    }
                    Assert(name+": contact follows actual warned sweep",swept,
                        "launch="+newLaunch+" brace="+newBlock+" protected="+newProtected+" source="+source);
                    if(swept)c.verifiedHitSweeps++;
                    if(newLaunch&&c.firstLaunchAt<0)c.firstLaunchAt=now-start;
                    if(newProtected&&swept&&source==1&&!p.airborne&&!p.recovering&&p.landCount>0&&p.protectionLeft>0)
                        c.groundedProtectedContacts++;
                }
                if(pendingShot==null&&pausePhase==""&&fixture!="protection"&&fixture!="boundary")
                {
                    if(!c.shotWarning&&game.CombatWarningCount>0&&npc.Length>0&&npc[0].warningLeft>.45f)
                    {c.shotWarning=true;pendingShot="warning";}
                    else if(name=="front"&&!c.shotCompression&&p.airborne&&p.height<.2f&&ScaleError(p.visualScale)>.03f)
                    {c.shotCompression=true;pendingShot="compression";}
                    else if(name=="front"&&!c.shotAir&&p.airborne&&p.height>=1.1f&&p.spinDegrees>=100f)
                    {c.shotAir=true;pendingShot="air";}
                    else if(fixture=="brace"&&!c.shotBrace&&p.braceSuccessLeft>.3f)
                    {c.shotBrace=true;pendingShot="brace";}
                }
                bool trigger=!paused&&((pausePhase=="warning"&&game.CombatWarningCount>0&&npc[0].warningLeft>.25f)||
                    (pausePhase=="rush"&&game.CombatRushCount>0&&!p.airborne&&Vector2.Distance(npc[0].position,npc[0].warningOrigin)>.7f)||
                    (pausePhase=="air"&&p.airborne&&p.height>.3f));
                if(trigger)
                {
                    paused=true;c.pauseExercised=true;
                    if(pausePhase=="rush")c.pauseRushDistance=Vector2.Distance(npc[0].position,npc[0].warningOrigin);
                    for(int i=0;i<known.Length&&i<npc.Length;i++)
                        if(known[i]!=null&&(npc[i].phase==1||npc[i].phase==2))known[i].cancelled=true;
                    yield return PauseCheck(name);
                    previousPlayer=game.PlayerCombatProbeSnapshot();previousNpc=game.CombatProbeSnapshot();
                    for(int i=0;i<known.Length;i++)known[i]=null;
                    lastTime=game.TotalTime;lastFixed=Time.fixedTime;lastReal=Time.realtimeSinceStartup;skipDiff=true;
                    continue;
                }
                previousPlayer=p;previousNpc=npc;lastTime=now;lastFixed=Time.fixedTime;lastReal=real;skipDiff=false;
            }
            Neutral();var end=game.PlayerCombatProbeSnapshot();
            c.playerLaunches=end.launchCount;c.lands=end.landCount;c.braceBlocks=game.BraceBlocks;
            c.protectedHits=end.protectedHitCount;c.npcLaunches=game.NpcToNpcLaunches;c.finalScaleError=ScaleError(end.visualScale);
            Assert(name+": fixture completes",game.TotalTime-start>=duration-.05f,"simulation seconds="+(game.TotalTime-start));
            Assert(name+": finite swept movement and unchanged physical root",c.finiteAndContained&&c.rootStable&&
                c.maxPlayerSpeed<=13.1f&&c.maxPositionSpeed<=18&&c.maxNpcSpeed<=14.1f,
                "player speed/actual="+c.maxPlayerSpeed+"/"+c.maxPositionSpeed+" NPC actual="+c.maxNpcSpeed+" contained="+c.finiteAndContained);
            Assert(name+": threats and attack history remain bounded",c.maxThreats<=2&&game.MaxVisibleCombatThreats<=2&&c.maxBurstHistory<=8,
                "observed="+c.maxThreats+" game="+game.MaxVisibleCombatThreats+" burst history="+c.maxBurstHistory);
            foreach(var a in c.attacks)
            {
                Assert(name+": warning locks geometry "+a.slot+"/"+a.id,
                    a.length>0&&a.length<=3.7f&&Mathf.Abs(a.radius-.82f)<.005f&&a.maxAngleError<.1f&&a.maxOriginError<.005f&&a.maxPathError<.05f,
                    "length/radius="+a.length+"/"+a.radius+" angle/origin/path="+a.maxAngleError+"/"+a.maxOriginError+"/"+a.maxPathError);
                if(a.rushAt>=0)Assert(name+": sampled warning precedes rush "+a.slot+"/"+a.id,
                    a.warningInViewport&&a.firstObservedWarningLeft>=a.duration-.06f&&a.observedLead>=a.duration-.06f,
                    "first sampled remaining="+a.firstObservedWarningLeft+" observed lead="+a.observedLead+
                    " required="+a.duration+" tolerance=.06s; viewport sampling does not prove an unoccluded outline");
            }
            Assert(name+": player lands and recovers",!end.airborne&&!end.recovering&&c.longestAirSeconds<1.2f&&c.finalScaleError<.03f,
                "height="+end.height+" longest air="+c.longestAirSeconds+" scale error="+c.finalScaleError);
            if(name=="front"||fixture=="boundary")
            {
                Assert(name+": unbraced player really launches and rotates",c.playerLaunches==1&&c.peakHeight>.3f&&c.peakSpin>30&&c.lands==1,
                    "launches="+c.playerLaunches+" height="+c.peakHeight+" spin="+c.peakSpin+" lands="+c.lands);
                Assert(name+": controls restore after landing",c.movedAfterRecovery>.8f,"actual input displacement="+c.movedAfterRecovery);
            }
            if(name=="interrupt-warning")
                Assert("Player dash interrupts windup without a residual rush",
                    game.TotalLaunches>0&&c.playerLaunches==0&&c.attacks.Count>0&&c.attacks.TrueForAll(a=>a.rushAt<0)&&
                    game.CombatWarningCount==0&&game.CombatRushCount==0,
                    "NPC launched="+game.TotalLaunches+" player launched="+c.playerLaunches+" rushes="+game.CombatRushCount);
            if(fixture=="brace")Assert("Brace blocks a real contact",c.braceBlocks>0&&c.verifiedHitSweeps>0&&c.playerLaunches==0&&c.peakHeight<.025f,
                "blocks="+c.braceBlocks+" launch="+c.playerLaunches+" height="+c.peakHeight);
            if(fixture=="dodge")Assert("Input dodge leaves the locked danger area before a real rush",
                c.outsideRushSamples>0&&c.minimumOutsideMargin>.15f&&c.playerLaunches==0&&c.braceBlocks==0,
                "rush samples="+c.outsideRushSamples+" outside margin="+c.minimumOutsideMargin+" launches="+c.playerLaunches);
            if(fixture=="npc-chain")Assert("Warned NPC rush launches other NPCs with finite chain",
                c.npcLaunches>0&&game.TotalLaunches>0&&game.MaxObservedDashTargets<=6&&game.MaxObservedChainDepth<=2&&c.playerLaunches==0,
                "NPC launches="+c.npcLaunches+" max targets="+game.MaxObservedDashTargets+" depth="+game.MaxObservedChainDepth);
            if(fixture=="near-outside")Assert("Stationary point 0.10m outside warned capsule is not hit",
                c.outsideRushSamples>0&&c.minimumOutsideMargin>=.085f&&c.minimumOutsideMargin<=.115f&&
                c.playerLaunches==0&&c.braceBlocks==0,
                "actual rush samples="+c.outsideRushSamples+" measured margin="+c.minimumOutsideMargin+" launches="+c.playerLaunches);
            if(fixture=="near-inside")Assert("Stationary point 0.10m inside warned capsule is really hit",
                c.outsideRushSamples>0&&c.minimumOutsideMargin<=-.085f&&c.minimumOutsideMargin>=-.115f&&
                c.playerLaunches==1&&c.lands==1&&c.verifiedHitSweeps>0&&c.peakHeight>.3f,
                "measured margin="+c.minimumOutsideMargin+" launches="+c.playerLaunches+" lands="+c.lands+" height="+c.peakHeight);
            if(fixture=="protection")Assert("Second real ground contact is blocked by landing protection",
                c.groundedProtectedContacts>0&&c.playerLaunches==1&&c.lands==1,
                "ground protected contacts="+c.groundedProtectedContacts+" all protected="+c.protectedHits+" launches="+c.playerLaunches);
            if(pausePhase!="")
            {
                Assert(name+": requested pause phase was exercised",c.pauseExercised,pausePhase+" travelled before pause="+c.pauseRushDistance);
                if(pausePhase!="air")Assert(name+": cancelled attack cannot resume-hit",c.playerLaunches==0,"launches="+c.playerLaunches);
            }
            Debug.Log("RUSH_COMBAT_CASE "+JsonUtility.ToJson(c));
        }
        IEnumerator PauseCheck(string label)
        {
            var before=game.PlayerCombatProbeSnapshot();Neutral();game.TogglePause();yield return eof;
            var a=game.PlayerCombatProbeSnapshot();var threats=game.CombatProbeSnapshot();
            float timer=game.TotalTime,fx=game.ImpactFxClock;
            Vector3 camera=game.GameCamera.transform.position;Quaternion rotation=game.GameCamera.transform.rotation;
            Assert(label+": pause cancels warnings/rushes and preserves existing flight",
                game.CombatWarningCount==0&&game.CombatRushCount==0&&game.ImpactFreezeRemaining==0&&
                Vector2.Distance(before.position,a.position)<.002f&&Mathf.Abs(before.height-a.height)<.002f&&before.airborne==a.airborne,
                "warning/rush="+game.CombatWarningCount+"/"+game.CombatRushCount+" height="+a.height);
            yield return Wait(.3f);yield return eof;
            var b=game.PlayerCombatProbeSnapshot();var after=game.CombatProbeSnapshot();
            bool stable=game.State==RushState.Paused&&Mathf.Abs(timer-game.TotalTime)<.001f&&
                Vector2.Distance(a.position,b.position)<.001f&&Mathf.Abs(a.height-b.height)<.001f&&
                Mathf.Abs(a.spinDegrees-b.spinDegrees)<.001f&&Mathf.Abs(a.recoveryLeft-b.recoveryLeft)<.001f&&
                Mathf.Abs(a.protectionLeft-b.protectionLeft)<.001f&&Vector3.Distance(a.visualScale,b.visualScale)<.001f&&
                Vector3.Distance(camera,game.GameCamera.transform.position)<.001f&&Quaternion.Angle(rotation,game.GameCamera.transform.rotation)<.01f&&
                Mathf.Abs(fx-game.ImpactFxClock)<.001f&&threats.Length==after.Length;
            for(int i=0;i<Mathf.Min(threats.Length,after.Length);i++)
                stable &= Vector2.Distance(threats[i].position,after[i].position)<.001f&&threats[i].phase==after[i].phase&&
                    Mathf.Abs(threats[i].warningLeft-after[i].warningLeft)<.001f&&Mathf.Abs(threats[i].rushLeft-after[i].rushLeft)<.001f;
            Assert(label+": pause freezes actors, timers, protection, FX and camera",stable,"timer delta="+(game.TotalTime-timer));
            game.TogglePause();Neutral();yield return eof;
            Assert(label+": no unannounced attack immediately resumes",game.CombatRushCount==0&&game.ImpactFreezeRemaining==0,
                "rushes="+game.CombatRushCount);
        }
        IEnumerator RestartAir()
        {
            game.SetupCombatFixture("front");Neutral();float until=Time.realtimeSinceStartup+5;bool observed=false;
            while(!finished&&Time.realtimeSinceStartup<until)
            {yield return eof;var p=game.PlayerCombatProbeSnapshot();if(p.airborne&&p.height>.3f){observed=true;break;}}
            Assert("Restart interrupts actual player flight",observed,"launches="+game.NpcToPlayerLaunches);
            game.Restart();Neutral();yield return eof;
            var a=game.PlayerCombatProbeSnapshot();
            Assert("Restart clears combat and presentation",
                !a.airborne&&!a.recovering&&a.height==0&&a.protectionLeft==0&&a.launchCount==0&&a.landCount==0&&
                game.CombatWarningCount==0&&game.CombatRushCount==0&&game.ActiveImpactFxCount==0&&ScaleError(a.visualScale)<.001f,
                "launches="+a.launchCount+" threats="+game.CombatWarningCount+"/"+game.CombatRushCount+" protection="+a.protectionLeft);
        }
        void Finish()
        {
            if(finished)return;finished=true;StopAllCoroutines();Neutral();
            report.elapsedRealSeconds=Time.realtimeSinceStartup-began;report.passed=report.failureCount==0;
            File.WriteAllText(Path.Combine(output,"combat-report.json"),JsonUtility.ToJson(report,true));
            Debug.Log("RUSH_COMBAT_QA_FINISHED passed="+report.passed+" failures="+report.failureCount+" output="+output);
            Application.Quit(report.passed?0:1);
        }
    }
}

