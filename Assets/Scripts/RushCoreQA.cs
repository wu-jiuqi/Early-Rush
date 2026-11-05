using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EarlyRush
{
    // Natural station-only input runs. No fixtures, actor relocation or scheduled attacks.
    public sealed class RushCoreQA : MonoBehaviour
    {
        [Serializable] public class Check { public string name, evidence; public bool passed; }
        [Serializable] public class Frames
        {
            public int count; public float totalSeconds, meanMilliseconds, worstMilliseconds;
            public void Add(float dt) { if(dt<=0)return; count++;totalSeconds+=dt;worstMilliseconds=Mathf.Max(worstMilliseconds,dt*1000);meanMilliseconds=totalSeconds/count*1000; }
        }
        [Serializable] public class Attack
        {
            public int slot, generation, id;
            public Vector2 origin, direction, responsePosition;
            public float radius, length, duration, firstSeen, firstWarning=-1, firstWarningLeft=-1, rushAt=-1;
            public float opportunityAt=-1, reactionAt=-1, minimumMargin=999, nearestActualSweep=999;
            public float lateralDisplacement, firstRushMargin=999, minimumRushMargin=999;
            public bool visibleWarning, projectedOpportunity, currentPositionOpportunity, actualSweepOpportunity;
            public bool reacted, cancelled, launchedPlayer, blocked, protectedContact;
            public float maxDirectionError, maxOriginError;
        }
        [Serializable] public class Contact
        {
            public float at, relativeSweepDistance, observedWarningLead;
            public int attackId, sourceSlot=-1, launchDelta, blockDelta, protectionDelta;
            public bool launch, block, protection, attributed, observedWarning;
        }
        [Serializable] public class Progress
        {
            public float time, forward, distance, progress;
        }
        [Serializable] public class DashWindow
        {
            public int id, ownedLaunches, lateOwnedLaunches, ledgerTargetCount;
            public List<string> probeTargets=new List<string>();
            public float startTime, endTime, startForward, endForward, actualDistance, forwardGain;
            public float walkWindowForward, forwardDifferenceFromWalk, walkWindowDuration;
            public bool walkMatch, complete;
        }
        [Serializable] public class ClearancePass
        {
            public int slot, generation, dashId;
            public Vector2 occupiedPosition, launchDirection, playerBeforeLaunch;
            public float previousGroundSampleTime, launchObservedAt, enteredAt=-1, crossedAt=-1;
            public float minimumDistance=999, forwardGainAtCrossing;
            public bool enteredVacancy, crossedVacancy;
        }
        [Serializable] public class Run
        {
            public string strategy;
            public bool reachedGoal, naturalStart, finite=true, contained=true, visible=true, rootStable=true, exclusive=true;
            public bool strategyKeysValid=true, contactAttribution=true;
            public int frames, collisions, launches, lands, braceBlocks, protectedHits, npcToNpcLaunches, ownedLaunches;
            public int warningCount, rushCount, opportunityCount, sweepOpportunityCount, recoveryAttempts, recoveryExhausted;
            public int dashRequests, braceRequests, dodgeDecisions, noClearDodge, maxThreats;
            public int clearanceEntries, clearanceCrossings;
            public float ordinaryBlockedDistance;
            public float gameSeconds, realSeconds, loadMilliseconds, actualDistance, forwardDistance, braceSeconds, dashSeconds;
            public float playerPeakHeight, maximumPositionSpeed, longestStall, longestAir, minimumViewportX=1, maximumViewportX;
            public float minimumViewportY=1, maximumViewportY, screenshotMilliseconds, cameraSpanX, cameraSpanZ;
            public float[] milestoneTimes={-1,-1,-1,-1};
            public Frames allFrames=new Frames(), ordinaryPlayingFrames=new Frames(), captureFrames=new Frames(), loadingFrames=new Frames();
            public List<Attack> attacks=new List<Attack>();
            public List<Contact> contacts=new List<Contact>();
            public List<Progress> progress=new List<Progress>();
            public List<DashWindow> dashWindows=new List<DashWindow>();
            public List<ClearancePass> clearancePasses=new List<ClearancePass>();
        }
        [Serializable] public class Shot
        {
            public string path, strategy, requestedPhase;
            public float gameTime, height, spin, startReal, endReal;
            public int frame, warnings, rushes, playerLaunches, braceBlocks;
            public bool airborne, bracing, dashing;
        }
        [Serializable] public class Comparison
        {
            public string strategy;
            public float secondsVersusWalk, distanceVersusWalk;
            public float[] milestoneSecondsVersusWalk=new float[4];
        }
        [Serializable] public class Report
        {
            public string schema="v5-natural-core-2", utc, engine, device;
            public string scope="Four natural station spawn-to-goal input runs. StartCoreRun only initializes the ordinary station. No fixture, actor relocation, attacker selection or forced hit. Menu/pause use game actions; not OS keyboard automation or human play.";
            public string opportunityScope="Visible warning capsule versus current position or 0.45s of ordinary intended movement is a prediction, not a hit. Actual swept proximity and counter-confirmed launch/block are separate.";
            public string performanceScope="All-frame and Playing metrics retain capture/loading frames; tagged subsets are also listed. Tags include the request and two subsequent frames and are not a causal attribution of every stall.";
            public string comparisonScope="One run per strategy. Natural trajectories diverge. Total-time and matched-progress dash-window differences are observations, not isolated knockback effects or statistical estimates. Dash targets are bound to the exact positive dash ID using probes and its actual visited ledger; late-owned targets are separate from the measured window.";
            public string clearanceScope="A vacancy uses the last sampled grounded position of a same-slot/generation NPC subsequently launched by the exact player dash ID. After observing launch, a grounded player must enter its former 0.72m exclusion circle and cross its forward plane while that NPC remains airborne/recovering. This is observed use of vacated ground, not an isolated causal estimate of time saved; origin sampling age and each dash window remain recorded.";
            public bool passed, coverageComplete, readyForAcceptance, pauseCleanupCovered;
            public int failureCount;
            public float elapsedRealSeconds;
            public Frames allFrames=new Frames(), playingFrames=new Frames(), captureFrames=new Frames(), loadingFrames=new Frames();
            public List<Check> checks=new List<Check>();
            public List<string> coverageGaps=new List<string>();
            public List<Run> runs=new List<Run>();
            public List<Comparison> comparisons=new List<Comparison>();
            public List<Shot> screenshots=new List<Shot>();
        }

        RushGame game;
        readonly Report report=new Report();
        Run current;
        string output;
        float began, runBegan, previousFixed, previousSampleTime, lastStatus, lastProgressSample;
        Vector2 previousPosition, spawn, routeAxis;
        Vector3 cameraMin, cameraMax;
        RushGame.CombatProbe[] previousCombat;
        RushGame.CrowdProbe[] previousCrowd;
        RushGame.ImpactProbe[] previousImpacts;
        RushGame.PlayerCombatProbe previousPlayer;
        readonly Dictionary<string,Attack> known=new Dictionary<string,Attack>();
        List<Vector2> route;
        int waypoint, captureUntil=-1, loadingUntil=-1, lastDashId;
        bool finished, capturing, loading, shotWarning, shotResponse;
        Vector2 intended, issuedMove, dodgeTarget, recoveryDirection;
        bool issuedBrace, issuedDash;
        float lastDashRequest=-10, dodgeUntil, recoveryUntil, lastAdvanceAt, bestForward, airBegan=-1;
        const float Clearance=.44f, Reaction=.18f, RunLimit=80f, SuiteLimit=360f;

        IEnumerator Start()
        {
            if(!Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--core-qa")) { enabled=false; yield break; }
            game=GetComponent<RushGame>(); began=Time.realtimeSinceStartup;
            output=Path.Combine(Application.persistentDataPath,"core-qa");
            string[] args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length;i++)
            {
                if(args[i].StartsWith("--core-qa-output="))output=args[i].Substring("--core-qa-output=".Length);
                else if(args[i]=="--core-qa-output"&&i+1<args.Length)output=args[++i];
            }
            Directory.CreateDirectory(output);
            report.engine=Application.unityVersion;report.device=SystemInfo.graphicsDeviceName;
            game.QAInput=true;
            foreach(string strategy in new[]{"walk","brace","dodge","offense"})
            {
                if(finished)yield break;
                yield return Traverse(strategy);
            }
            if(!finished)yield return Controls();
            if(!finished){Compare();Coverage();Finish();}
        }
        void Update()
        {
            if(finished||game==null)return;
            float dt=Time.unscaledDeltaTime;
            bool capture=capturing||Time.frameCount<=captureUntil;
            bool load=loading||Time.frameCount<=loadingUntil;
            report.allFrames.Add(dt);
            if(game.State==RushState.Playing)report.playingFrames.Add(dt);
            if(capture)report.captureFrames.Add(dt);
            if(load)report.loadingFrames.Add(dt);
            if(current!=null)
            {
                current.allFrames.Add(dt);
                if(capture)current.captureFrames.Add(dt);
                if(load)current.loadingFrames.Add(dt);
                if(game.State==RushState.Playing&&!capture&&!load)current.ordinaryPlayingFrames.Add(dt);
            }
            if(Time.realtimeSinceStartup-began>SuiteLimit)
            {
                CheckThat("Suite finishes within six minutes",false,"elapsed="+(Time.realtimeSinceStartup-began));
                report.coverageGaps.Add("Suite timed out before all measurements completed.");
                Finish(); return;
            }
            if(Time.realtimeSinceStartup-lastStatus>2f)
            {
                lastStatus=Time.realtimeSinceStartup;
                File.WriteAllText(Path.Combine(output,"core-status.json"),
                    "{\"strategy\":\""+(current==null?"controls":current.strategy)+"\",\"state\":\""+game.State+
                    "\",\"stage\":"+game.Stage+",\"gameTime\":"+game.TotalTime.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"}");
            }
        }
        void CheckThat(string name,bool ok,string evidence)
        {
            report.checks.Add(new Check{name=name,passed=ok,evidence=evidence});
            if(!ok)report.failureCount++;
            Debug.Log("RUSH_CORE_QA_CHECK "+(ok?"PASS ":"FAIL ")+name+" | "+evidence);
        }
        void Input(Vector2 move,bool brace=false,bool dash=false)
        {
            issuedMove=Vector2.ClampMagnitude(move,1);issuedBrace=brace;issuedDash=dash;
            game.QAMove=issuedMove;game.QABrace=brace;game.QADash=dash;
        }
        void StartNatural()
        {
            Input(Vector2.zero); loading=true; loadingUntil=Time.frameCount+2;
            RushPerformance.SetQaPhase(current==null?"core-controls":"core-"+current.strategy);
            Debug.Log("RUSH_CORE_LOAD_BEGIN frame="+Time.frameCount+" real="+Time.realtimeSinceStartup);
            var watch=System.Diagnostics.Stopwatch.StartNew();
            game.StartCoreRun();
            watch.Stop();
            if(current!=null)current.loadMilliseconds=(float)watch.Elapsed.TotalMilliseconds;
            Debug.Log("RUSH_CORE_LOAD_END frame="+Time.frameCount+" milliseconds="+watch.Elapsed.TotalMilliseconds);
            loading=false;
        }
        IEnumerator Traverse(string strategy)
        {
            current=new Run{strategy=strategy};report.runs.Add(current);
            StartNatural(); runBegan=Time.realtimeSinceStartup;
            spawn=new Vector2(game.World.spawn.x,game.World.spawn.z);
            routeAxis=(new Vector2(game.World.goal.x,game.World.goal.z)-spawn).normalized;
            current.naturalStart=game.CoreMode&&game.Stage==1&&game.State==RushState.Playing&&
                Vector2.Distance(game.Position,spawn)<.01f&&game.TotalTime<.01f&&game.TransitionCount==0;
            CheckThat(strategy+": ordinary station starts cleanly",current.naturalStart,
                "stage="+game.Stage+" position="+game.Position+" core="+game.CoreMode);
            route=new List<Vector2>(game.World.riskRoute);
            if(route.Count==0)route.Add(new Vector2(game.World.goal.x,game.World.goal.z));
            waypoint=0; known.Clear();previousCombat=null;previousCrowd=null;
            previousPosition=game.Position;previousFixed=Time.fixedTime;previousSampleTime=game.TotalTime;
            previousPlayer=game.PlayerCombatProbeSnapshot();
            previousImpacts=game.ImpactProbeSnapshot();
            lastDashId=game.CurrentDashId;lastDashRequest=-10;dodgeUntil=recoveryUntil=0;
            bestForward=0;lastAdvanceAt=game.TotalTime;lastProgressSample=-1;airBegan=-1;
            cameraMin=cameraMax=game.GameCamera.transform.position;shotWarning=shotResponse=false;
            yield return null;
            while(!finished&&game.State==RushState.Playing&&game.Stage==1&&Time.realtimeSinceStartup-runBegan<RunLimit)
            {
                Vector2 p=game.Position;
                Vector2 normal=RouteDirection(p);
                intended=normal;
                var combat=game.CombatProbeSnapshot();
                var crowd=game.CrowdProbeSnapshot();
                var player=game.PlayerCombatProbeSnapshot();
                Sample(combat,crowd,player);
                Vector2 move=normal; bool brace=false,dash=false;
                Attack threat=null; RushGame.CombatProbe source=new RushGame.CombatProbe();
                for(int i=0;i<combat.Length&&i<crowd.Length;i++)
                {
                    var c=combat[i];
                    if(!c.active||!c.visible||(c.phase!=1&&c.phase!=2))continue;
                    Attack a; if(!known.TryGetValue(Key(i,crowd[i].generation,c.attackId),out a))continue;
                    if(!a.visibleWarning||a.opportunityAt<0||game.TotalTime-a.opportunityAt<Reaction)continue;
                    if(DangerMargin(p,c)>.18f&&DangerMargin(p+normal*2.2f,c)>.18f)continue;
                    if(threat==null||a.opportunityAt<threat.opportunityAt){threat=a;source=c;}
                }
                if(!player.airborne&&!player.recovering)
                {
                    if(strategy=="brace"&&threat!=null)
                    {
                        brace=true;
                        if(!threat.reacted){threat.reacted=true;threat.reactionAt=game.TotalTime;threat.responsePosition=p;current.braceRequests++;}
                    }
                    if(strategy=="dodge"&&threat!=null&&game.TotalTime>=dodgeUntil)
                    {
                        Vector2 side=new Vector2(-source.direction.y,source.direction.x);
                        Vector2 left=p+side*1.35f,right=p-side*1.35f;
                        bool l=LineClear(p,left),r=LineClear(p,right);
                        if(l||r)
                        {
                            dodgeTarget=l&&(!r||DangerMargin(left,source)>DangerMargin(right,source))?left:right;
                            dodgeUntil=game.TotalTime+.7f;current.dodgeDecisions++;
                        }
                        else
                        {
                            dodgeTarget=LineClear(p,p-normal*.8f)?p-normal*.8f:p;
                            dodgeUntil=game.TotalTime+.25f;current.noClearDodge++;
                        }
                        if(!threat.reacted){threat.reacted=true;threat.reactionAt=game.TotalTime;threat.responsePosition=p;}
                    }
                    if(strategy=="dodge"&&game.TotalTime<dodgeUntil)
                        move=Vector2.Distance(p,dodgeTarget)>.15f?(dodgeTarget-p).normalized:normal;
                    if(strategy=="offense"&&game.DashCooldown01>.99f&&game.TotalTime-lastDashRequest>1.16f&&
                        LineClear(p,p+normal*2.4f)&&VisibleTargetAhead(p,normal,crowd))
                    {dash=true;lastDashRequest=game.TotalTime;current.dashRequests++;}
                    float forward=Vector2.Dot(p-spawn,routeAxis);
                    if(forward>bestForward+.35f){bestForward=forward;lastAdvanceAt=game.TotalTime;}
                    float stalled=game.TotalTime-lastAdvanceAt;
                    current.longestStall=Mathf.Max(current.longestStall,stalled);
                    if(stalled>1.2f&&game.TotalTime>=recoveryUntil&&current.recoveryAttempts<6)
                    {
                        current.recoveryAttempts++;
                        float side=current.recoveryAttempts%2==0?1:-1;
                        Vector2 attempt=(new Vector2(normal.y,-normal.x)*side-normal*.25f).normalized;
                        recoveryDirection=LineClear(p,p+attempt*.9f)?attempt:
                            (LineClear(p,p-normal*.6f)?-normal:Vector2.zero);
                        recoveryUntil=game.TotalTime+.45f;lastAdvanceAt=game.TotalTime;
                    }
                    if(game.TotalTime<recoveryUntil){move=recoveryDirection;dash=false;}
                    if(stalled>1.2f&&current.recoveryAttempts>=6)current.recoveryExhausted=1;
                }
                else {move=Vector2.zero;brace=dash=false;lastAdvanceAt=game.TotalTime;}
                move=StaticSteer(p,move);
                Input(move,brace,dash);
                if(!capturing)
                {
                    if(!shotWarning&&current.warningCount>0)
                    {shotWarning=true;StartCoroutine(Capture(strategy,"warning"));}
                    else if(!shotResponse&&((player.airborne&&player.height>1f)||game.BraceSuccessRemaining>.1f||
                        (strategy=="offense"&&current.ownedLaunches>0)))
                    {shotResponse=true;StartCoroutine(Capture(strategy,player.airborne?"air":game.BraceSuccessRemaining>.1f?"brace":"dash-impact"));}
                }
                yield return null;
            }
            Input(Vector2.zero);
            if(finished)yield break;
            // Include the completion tick. Counter reconciliation below rejects any
            // event that was cleared or aggregated before it could be attributed.
            Sample(game.CombatProbeSnapshot(),game.CrowdProbeSnapshot(),game.PlayerCombatProbeSnapshot());
            // Counters are captured before the next natural reset; completion itself clears transient presentation.
            current.gameSeconds=game.TotalTime;current.realSeconds=Time.realtimeSinceStartup-runBegan;
            current.collisions=game.Collisions;current.launches=game.NpcToPlayerLaunches;
            current.braceBlocks=game.BraceBlocks;current.npcToNpcLaunches=game.NpcToNpcLaunches;
            current.ownedLaunches=game.TotalLaunches-game.NpcToNpcLaunches;
            current.ordinaryBlockedDistance=game.OrdinaryBlockedDistance;
            current.reachedGoal=game.State==RushState.Results&&game.Stage==1&&game.TransitionCount==1;
            current.forwardDistance=Vector2.Dot(game.Position-spawn,routeAxis);
            if(current.reachedGoal)current.milestoneTimes[3]=game.TotalTime;
            CloseDashWindow(true);
            CheckThat(strategy+": reaches station goal through input within 80s",current.reachedGoal&&current.realSeconds<=RunLimit,
                "state="+game.State+" stage="+game.Stage+" transitions="+game.TransitionCount+" seconds="+current.gameSeconds);
            CheckThat(strategy+": finite, contained and camera-visible",current.finite&&current.contained&&current.visible,
                "actual peak speed="+current.maximumPositionSpeed+" viewport="+current.minimumViewportX+","+current.maximumViewportX+"/"+current.minimumViewportY+","+current.maximumViewportY);
            CheckThat(strategy+": sampled player displacement stays bounded",current.maximumPositionSpeed<=18f,
                "position difference / fixed elapsed="+current.maximumPositionSpeed+" m/s");
            CheckThat(strategy+": physical root stable and defense/dash exclusive",current.rootStable&&current.exclusive&&current.strategyKeysValid,
                "brace seconds="+current.braceSeconds+" dash seconds="+current.dashSeconds);
            CheckThat(strategy+": observed contacts have prior natural warning",current.contactAttribution,
                "contacts="+current.contacts.Count+" launches="+current.launches+" blocks="+current.braceBlocks);
            int recordedLaunches=0,recordedBlocks=0,recordedProtection=0;
            foreach(var contact in current.contacts)
            {recordedLaunches+=contact.launchDelta;recordedBlocks+=contact.blockDelta;recordedProtection+=contact.protectionDelta;}
            CheckThat(strategy+": final contact counters reconcile including completion tick",
                recordedLaunches==current.launches&&recordedBlocks==current.braceBlocks&&recordedProtection==current.protectedHits,
                "recorded launch/block/protected="+recordedLaunches+"/"+recordedBlocks+"/"+recordedProtection+
                " final="+current.launches+"/"+current.braceBlocks+"/"+current.protectedHits);
            foreach(var attack in current.attacks)
            {
                CheckThat(strategy+": warning geometry stays locked "+attack.slot+"/"+attack.id,
                    attack.maxDirectionError<.1f&&attack.maxOriginError<.005f,
                    "angle="+attack.maxDirectionError+" origin="+attack.maxOriginError);
                if(attack.rushAt>=0)
                    CheckThat(strategy+": observed warning precedes rush "+attack.slot+"/"+attack.id,
                        attack.visibleWarning&&attack.firstWarningLeft>=attack.duration-.06f&&attack.rushAt-attack.firstWarning>=attack.duration-.06f,
                        "first remaining="+attack.firstWarningLeft+" lead="+(attack.rushAt-attack.firstWarning)+" duration="+attack.duration+" tolerance=.06s");
            }
            CheckThat(strategy+": finite impact chains",game.MaxObservedDashTargets<=6&&game.MaxObservedChainDepth<=2,
                "targets="+game.MaxObservedDashTargets+" depth="+game.MaxObservedChainDepth);
            while(capturing&&!finished)yield return null;
            if(finished)yield break;
            yield return Capture(strategy,"result");
            Debug.Log("RUSH_CORE_RUN "+JsonUtility.ToJson(current));
            current=null;
            yield return null;
        }
        string Key(int slot,int generation,int id){return slot+":"+generation+":"+id;}
        float DangerMargin(Vector2 point,RushGame.CombatProbe c)
        { return SegmentDistance(point,c.warningOrigin,c.warningOrigin+c.direction*c.warningLength)-c.warningRadius; }
        static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
        { Vector2 v=b-a;float t=v.sqrMagnitude>.000001f?Mathf.Clamp01(Vector2.Dot(p-a,v)/v.sqrMagnitude):0;return Vector2.Distance(p,a+v*t); }
        bool VisibleTargetAhead(Vector2 p,Vector2 direction,RushGame.CrowdProbe[] crowd)
        {
            var impacts=game.ImpactProbeSnapshot();
            for(int i=0;i<crowd.Length&&i<impacts.Length;i++)
            {
                if(!crowd[i].active||!crowd[i].visible||impacts[i].airborne||impacts[i].recovering)continue;
                Vector2 delta=crowd[i].position-p;
                float ahead=Vector2.Dot(delta,direction),side=Mathf.Abs(delta.x*direction.y-delta.y*direction.x);
                if(ahead>.75f&&ahead<3.6f&&side<.72f)return true;
            }
            return false;
        }
        void Sample(RushGame.CombatProbe[] combat,RushGame.CrowdProbe[] crowd,RushGame.PlayerCombatProbe player)
        {
            if(current==null)return;
            float now=game.TotalTime,dt=Mathf.Max(0,now-previousSampleTime);
            Vector2 p=game.Position;
            float distanceBefore=current.actualDistance;
            current.frames++;current.actualDistance+=Vector2.Distance(p,previousPosition);
            float fixedDt=Time.fixedTime-previousFixed;
            if(fixedDt>.0001f)current.maximumPositionSpeed=Mathf.Max(current.maximumPositionSpeed,Vector2.Distance(p,previousPosition)/fixedDt);
            current.finite&=Finite(p.x)&&Finite(p.y)&&Finite(player.height)&&Finite(game.Velocity.sqrMagnitude);
            current.contained&=Free(p,game.PlayerCollisionRadius-.03f);
            current.rootStable&=Vector3.Distance(player.rootScale,Vector3.one)<.001f;
            current.exclusive&=!(game.IsBracing&&game.IsDashing);
            current.strategyKeysValid&=current.strategy=="brace"||!issuedBrace;
            current.strategyKeysValid&=current.strategy=="offense"||!issuedDash;
            if(game.IsBracing)current.braceSeconds+=dt;if(game.IsDashing)current.dashSeconds+=dt;
            current.playerPeakHeight=Mathf.Max(current.playerPeakHeight,player.height);
            current.lands=player.landCount;current.protectedHits=player.protectedHitCount;
            current.ownedLaunches=game.TotalLaunches-game.NpcToNpcLaunches;
            if(player.airborne){if(airBegan<0)airBegan=now;current.longestAir=Mathf.Max(current.longestAir,now-airBegan);}
            else airBegan=-1;
            Vector3 vp=game.GameCamera.WorldToViewportPoint(new Vector3(p.x,1+player.height,p.y));
            current.visible&=vp.z>0&&vp.x>=0&&vp.x<=1&&vp.y>=0&&vp.y<=1;
            current.minimumViewportX=Mathf.Min(current.minimumViewportX,vp.x);current.maximumViewportX=Mathf.Max(current.maximumViewportX,vp.x);
            current.minimumViewportY=Mathf.Min(current.minimumViewportY,vp.y);current.maximumViewportY=Mathf.Max(current.maximumViewportY,vp.y);
            cameraMin=Vector3.Min(cameraMin,game.GameCamera.transform.position);cameraMax=Vector3.Max(cameraMax,game.GameCamera.transform.position);
            current.cameraSpanX=cameraMax.x-cameraMin.x;current.cameraSpanZ=cameraMax.z-cameraMin.z;
            for(int k=0;k<3;k++)if(current.milestoneTimes[k]<0&&game.StageProgress01>=(k+1)*.25f)current.milestoneTimes[k]=now;
            if(now-lastProgressSample>=.2f)
            {
                lastProgressSample=now;
                current.progress.Add(new Progress{time=now,forward=Vector2.Dot(p-spawn,routeAxis),distance=current.actualDistance,progress=game.StageProgress01});
            }
            int threats=0;
            for(int i=0;i<combat.Length&&i<crowd.Length;i++)
            {
                var c=combat[i];if(!c.active)continue;
                string key=Key(i,crowd[i].generation,c.attackId);Attack a;
                if(!known.TryGetValue(key,out a))
                {
                    if(c.phase!=1&&c.phase!=2)continue;
                    a=new Attack{slot=i,generation=crowd[i].generation,id=c.attackId,origin=c.warningOrigin,direction=c.direction,
                        radius=c.warningRadius,length=c.warningLength,duration=c.warningDuration,firstSeen=now};
                    known.Add(key,a);current.attacks.Add(a);
                }
                if(c.visible&&(c.phase==1||c.phase==2))threats++;
                if(c.phase==1&&c.visible)
                {
                    if(!a.visibleWarning){a.visibleWarning=true;a.firstWarning=now;a.firstWarningLeft=c.warningLeft;current.warningCount++;}
                    float margin=DangerMargin(p,c),forecast=DangerMargin(p+intended*4.9f*.45f,c);
                    a.minimumMargin=Mathf.Min(a.minimumMargin,margin);
                    a.currentPositionOpportunity|=margin<=0;a.projectedOpportunity|=forecast<=0;
                    if((margin<=0||forecast<=0)&&a.opportunityAt<0){a.opportunityAt=now;current.opportunityCount++;}
                }
                if(a.reacted&&(c.phase==1||c.phase==2))
                    a.lateralDisplacement=Mathf.Max(a.lateralDisplacement,
                        Mathf.Abs(Vector2.Dot(p-a.responsePosition,new Vector2(-a.direction.y,a.direction.x))));
                if(c.phase==2)
                {
                    float margin=DangerMargin(p,c);
                    if(a.rushAt<0){a.rushAt=now;a.firstRushMargin=margin;current.rushCount++;}
                    a.minimumRushMargin=Mathf.Min(a.minimumRushMargin,margin);
                }
                if((c.phase==0||c.phase==3)&&a.rushAt<0)a.cancelled=true;
                if(c.phase==1||c.phase==2)
                {
                    a.maxDirectionError=Mathf.Max(a.maxDirectionError,Vector2.Angle(a.direction,c.direction));
                    a.maxOriginError=Mathf.Max(a.maxOriginError,Vector2.Distance(a.origin,c.warningOrigin));
                }
                if(previousCombat!=null&&i<previousCombat.Length&&previousCrowd[i].generation==crowd[i].generation&&
                    (c.phase==2||previousCombat[i].phase==2))
                {
                    float distance=SegmentDistance(Vector2.zero,previousCombat[i].position-previousPosition,c.position-p);
                    a.nearestActualSweep=Mathf.Min(a.nearestActualSweep,distance);
                    if(distance<=c.warningRadius&&!a.actualSweepOpportunity){a.actualSweepOpportunity=true;current.sweepOpportunityCount++;}
                }
            }
            current.maxThreats=Mathf.Max(current.maxThreats,threats);
            int launchDelta=player.launchCount-previousPlayer.launchCount,blockDelta=player.braceSuccessCount-previousPlayer.braceSuccessCount,
                protectionDelta=player.protectedHitCount-previousPlayer.protectedHitCount;
            if(launchDelta<0||blockDelta<0||protectionDelta<0)current.contactAttribution=false;
            if(launchDelta>0||blockDelta>0||protectionDelta>0)AttributeContact(combat,crowd,player,launchDelta,blockDelta,protectionDelta);
            if(game.CurrentDashId>lastDashId)
            {
                CloseDashWindow(true);
                current.dashWindows.Add(new DashWindow{id=game.CurrentDashId,startTime=previousSampleTime,startForward=Vector2.Dot(previousPosition-spawn,routeAxis),
                    endForward=Vector2.Dot(p-spawn,routeAxis),actualDistance=-distanceBefore});
                lastDashId=game.CurrentDashId;
            }
            var impacts=game.ImpactProbeSnapshot();
            ObserveDashTargets(impacts,player);
            CloseDashWindow(false);
            previousCombat=combat;previousCrowd=crowd;previousPlayer=player;previousPosition=p;
            previousImpacts=impacts;
            previousFixed=Time.fixedTime;previousSampleTime=now;
        }
        void AttributeContact(RushGame.CombatProbe[] combat,RushGame.CrowdProbe[] crowd,RushGame.PlayerCombatProbe player,int launchDelta,int blockDelta,int protectionDelta)
        {
            bool launch=launchDelta>0,block=blockDelta>0,protection=protectionDelta>0;
            var contact=new Contact{at=game.TotalTime,launch=launch,block=block,protection=protection,
                launchDelta=launchDelta,blockDelta=blockDelta,protectionDelta=protectionDelta,
                attackId=player.lastContactAttackId,relativeSweepDistance=999};
            // A last-ID probe cannot attribute multiple events between samples.
            // Preserve their deltas and fail closed rather than inventing a source.
            if(launchDelta+blockDelta+protectionDelta!=1)
            {current.contactAttribution=false;current.contacts.Add(contact);return;}
            Attack best=null;float closest=999;
            for(int i=0;previousCombat!=null&&i<combat.Length&&i<previousCombat.Length&&i<crowd.Length;i++)
            {
                if(previousCrowd[i].generation!=crowd[i].generation)continue;
                var c=combat[i];var before=previousCombat[i];
                if(c.phase!=2&&before.phase!=2&&before.phase!=1)continue;
                Attack a;if(!known.TryGetValue(Key(i,crowd[i].generation,before.attackId),out a))continue;
                float distance=SegmentDistance(Vector2.zero,before.position-previousPosition,c.position-game.Position);
                if(distance<closest&&distance<=a.radius+.10f&&a.id==player.lastContactAttackId){best=a;closest=distance;}
            }
            contact.relativeSweepDistance=closest;
            if(best!=null)
            {
                contact.attributed=true;contact.attackId=best.id;contact.sourceSlot=best.slot;
                contact.observedWarning=best.visibleWarning;
                contact.observedWarningLead=best.firstWarning<0?-1:game.TotalTime-best.firstWarning;
                best.launchedPlayer|=launch;best.blocked|=block;best.protectedContact|=protection;
                if(!best.actualSweepOpportunity){best.actualSweepOpportunity=true;current.sweepOpportunityCount++;}
            }
            current.contactAttribution&=contact.attributed&&contact.observedWarning&&
                best!=null&&contact.observedWarningLead>=best.duration-.06f;
            current.contacts.Add(contact);
        }
        void ObserveDashTargets(RushGame.ImpactProbe[] impacts,RushGame.PlayerCombatProbe player)
        {
            if(current.dashWindows.Count==0)return;
            foreach(var window in current.dashWindows)
            {
                for(int i=0;i<impacts.Length;i++)
                    if(impacts[i].dashId==window.id&&impacts[i].launchCount>0)
                    {
                        string target=i+":"+impacts[i].generation;
                        if(!window.probeTargets.Contains(target))
                        {
                            window.probeTargets.Add(target);
                            if(previousImpacts!=null&&i<previousImpacts.Length)
                            {
                                var before=previousImpacts[i];
                                if(before.generation==impacts[i].generation&&before.active&&!before.airborne&&!before.recovering&&
                                    impacts[i].launchCount>before.launchCount)
                                    current.clearancePasses.Add(new ClearancePass{slot=i,generation=impacts[i].generation,dashId=window.id,
                                        occupiedPosition=before.position,launchDirection=impacts[i].launchDirection.normalized,
                                        playerBeforeLaunch=previousPosition,previousGroundSampleTime=previousSampleTime,launchObservedAt=game.TotalTime});
                            }
                        }
                    }
                // The same-ID visited ledger survives completion clearing of body
                // presentation and can corroborate a hit on the final physics tick.
                if(game.CurrentDashId==window.id)window.ledgerTargetCount=Mathf.Max(window.ledgerTargetCount,game.DashImpactCount);
                int attributed=Mathf.Max(window.probeTargets.Count,window.ledgerTargetCount);
                if(!window.complete)window.ownedLaunches=attributed;
                else window.lateOwnedLaunches=Mathf.Max(0,attributed-window.ownedLaunches);
            }
            foreach(var pass in current.clearancePasses)
            {
                if(pass.crossedVacancy||pass.slot>=impacts.Length||previousSampleTime<pass.launchObservedAt||
                    player.airborne||player.recovering||previousPlayer.airborne||previousPlayer.recovering)continue;
                var target=impacts[pass.slot];
                if(!target.active||target.generation!=pass.generation||target.dashId!=pass.dashId||
                    (!target.airborne&&!target.recovering))continue;
                float distance=SegmentDistance(pass.occupiedPosition,previousPosition,game.Position);
                pass.minimumDistance=Mathf.Min(pass.minimumDistance,distance);
                if(!pass.enteredVacancy&&distance<.72f)
                {pass.enteredVacancy=true;pass.enteredAt=game.TotalTime;current.clearanceEntries++;}
                if(pass.enteredVacancy&&Vector2.Dot(pass.playerBeforeLaunch-pass.occupiedPosition,pass.launchDirection)<-.1f&&
                    Vector2.Dot(game.Position-pass.occupiedPosition,pass.launchDirection)>.15f)
                {
                    pass.crossedVacancy=true;pass.crossedAt=game.TotalTime;current.clearanceCrossings++;
                    pass.forwardGainAtCrossing=Vector2.Dot(game.Position-pass.playerBeforeLaunch,routeAxis);
                }
            }
        }
        void CloseDashWindow(bool force)
        {
            if(current==null||current.dashWindows.Count==0)return;
            var w=current.dashWindows[current.dashWindows.Count-1];if(w.complete)return;
            if(!force&&game.TotalTime-w.startTime<.8f)return;
            w.endTime=game.TotalTime;w.endForward=Vector2.Dot(game.Position-spawn,routeAxis);
            w.actualDistance+=current.actualDistance;
            w.forwardGain=w.endForward-w.startForward;w.complete=true;
        }
        static bool Finite(float v){return !float.IsNaN(v)&&!float.IsInfinity(v);}
        bool Free(Vector2 p,float clearance=Clearance)
        {
            var w=game.World;
            if(p.x<w.boundsMin.x+clearance||p.x>w.boundsMax.x-clearance||p.y<w.boundsMin.y+clearance||p.y>w.boundsMax.y-clearance)return false;
            foreach(Rect r in w.obstacles)
            {
                Vector2 closest=new Vector2(Mathf.Clamp(p.x,r.xMin,r.xMax),Mathf.Clamp(p.y,r.yMin,r.yMax));
                if(Vector2.Distance(p,closest)<clearance)return false;
            }
            return true;
        }
        bool LineClear(Vector2 a,Vector2 b)
        {
            int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/.12f));
            for(int k=1;k<=n;k++)if(!Free(Vector2.Lerp(a,b,(float)k/n)))return false;return true;
        }
        Vector2 RouteDirection(Vector2 p)
        {
            while(waypoint<route.Count-1)
            {
                Vector2 a=waypoint==0?spawn:route[waypoint-1],b=route[waypoint];
                bool passed=(b-a).sqrMagnitude>.01f&&Vector2.Dot(p-b,b-a)>=0&&LineClear(p,route[waypoint+1]);
                if(Vector2.Distance(p,b)<.65f||passed)waypoint++;else break;
            }
            Vector2 delta=route[waypoint]-p;
            return delta.sqrMagnitude>.01f?delta.normalized:Vector2.zero;
        }
        Vector2 StaticSteer(Vector2 p,Vector2 wanted)
        {
            if(wanted.sqrMagnitude<.01f||LineClear(p,p+wanted*.8f))return wanted;
            Vector2 best=Vector2.zero;float score=-2;
            for(int k=-3;k<=3;k++)
            {
                float angle=k*25*Mathf.Deg2Rad;
                Vector2 direction=new Vector2(wanted.x*Mathf.Cos(angle)-wanted.y*Mathf.Sin(angle),wanted.x*Mathf.Sin(angle)+wanted.y*Mathf.Cos(angle));
                if(!LineClear(p,p+direction*.7f))continue;
                float value=Vector2.Dot(direction,wanted);
                if(value>score){score=value;best=direction;}
            }
            return best;
        }
        IEnumerator Capture(string strategy,string requested)
        {
            if(capturing||finished)yield break;
            capturing=true;captureUntil=Time.frameCount+2;
            float before=Time.realtimeSinceStartup;
            yield return new WaitForEndOfFrame();
            RushPerformance.MarkCapture(strategy+"-"+requested,true);
            Texture2D texture=null;
            try
            {
                var p=game.PlayerCombatProbeSnapshot();
                string path=Path.Combine(output,strategy+"-"+requested+".png");
                texture=ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(path,texture.EncodeToPNG());
                report.screenshots.Add(new Shot{path=path,strategy=strategy,requestedPhase=requested,frame=Time.frameCount,
                    gameTime=game.TotalTime,height=p.height,spin=p.spinDegrees,airborne=p.airborne,bracing=game.IsBracing,dashing=game.IsDashing,
                    warnings=game.CombatWarningCount,rushes=game.CombatRushCount,playerLaunches=game.NpcToPlayerLaunches,
                    braceBlocks=game.BraceBlocks,startReal=before,endReal=Time.realtimeSinceStartup});
            }
            finally
            {
                if(texture!=null)Destroy(texture);
                if(current!=null)current.screenshotMilliseconds+=(Time.realtimeSinceStartup-before)*1000;
                captureUntil=Time.frameCount+2;capturing=false;
                RushPerformance.MarkCapture(strategy+"-"+requested,false);
            }
        }
        IEnumerator WaitReal(float seconds)
        { float end=Time.realtimeSinceStartup+seconds;while(!finished&&Time.realtimeSinceStartup<end)yield return null; }
        IEnumerator Controls()
        {
            StartNatural();yield return null;yield return null;
            CheckThat("Natural restart clears active combat",game.NpcToPlayerLaunches==0&&game.TotalLaunches==0&&
                game.CombatWarningCount==0&&game.CombatRushCount==0&&!game.PlayerAirborne&&game.PlayerProtectionRemaining==0,
                "launches="+game.NpcToPlayerLaunches+" warnings="+game.CombatWarningCount);
            Input(Vector2.up,true,true);yield return WaitReal(.15f);
            CheckThat("Simultaneous input selects brace, not dash",game.IsBracing&&!game.IsDashing&&game.CurrentDashId==0,
                "brace="+game.IsBracing+" dash="+game.IsDashing+" dashId="+game.CurrentDashId);
            Input(Vector2.zero);yield return WaitReal(.1f);
            float end=Time.realtimeSinceStartup+8f;
            while(!finished&&Time.realtimeSinceStartup<end&&game.State==RushState.Playing&&game.CombatWarningCount+game.CombatRushCount==0)
            {Input(StaticSteer(game.Position,Vector2.up));yield return null;}
            Input(Vector2.zero);
            bool active=game.CombatWarningCount+game.CombatRushCount>0;
            game.TogglePause();yield return new WaitForEndOfFrame();
            var player=game.PlayerCombatProbeSnapshot();var bodies=game.ImpactProbeSnapshot();
            float time=game.TotalTime,fx=game.ImpactFxClock;
            Vector3 camera=game.GameCamera.transform.position;
            bool clean=game.State==RushState.Paused&&game.CombatWarningCount==0&&game.CombatRushCount==0&&
                game.ImpactFreezeRemaining==0&&game.ActiveImpactFxCount==0&&Vector3.Distance(game.PlayerVisualScale,Vector3.one)<.001f;
            CheckThat("Pause clears threats and transient effects",clean,"active before pause="+active+" warnings/rush="+game.CombatWarningCount+"/"+game.CombatRushCount);
            report.pauseCleanupCovered=active&&clean;
            yield return WaitReal(.3f);
            var after=game.PlayerCombatProbeSnapshot();var afterBodies=game.ImpactProbeSnapshot();
            bool stable=bodies.Length==afterBodies.Length&&Vector2.Distance(player.position,after.position)<.001f&&
                Mathf.Abs(player.height-after.height)<.001f&&Mathf.Abs(game.TotalTime-time)<.001f&&
                Mathf.Abs(game.ImpactFxClock-fx)<.001f&&Vector3.Distance(game.GameCamera.transform.position,camera)<.001f;
            for(int i=0;i<Mathf.Min(bodies.Length,afterBodies.Length);i++)
                stable&=Vector2.Distance(bodies[i].position,afterBodies[i].position)<.001f&&Mathf.Abs(bodies[i].height-afterBodies[i].height)<.001f;
            CheckThat("Paused actors, timer, FX and camera freeze",stable,"timer delta="+(game.TotalTime-time));
            yield return Capture("controls","paused");
            game.TogglePause();Input(Vector2.zero,true,true);yield return WaitReal(.1f);
            CheckThat("Resume suppresses held actions",!game.IsDashing&&game.InputLocked,"locked="+game.InputLocked);
            Input(Vector2.zero);yield return WaitReal(.1f);
            CheckThat("Release rearms actions",!game.InputLocked,"locked="+game.InputLocked);
            game.ToMenu();Input(Vector2.zero);
        }
        void Compare()
        {
            if(report.runs.Count!=4)return;
            Run walk=report.runs[0];
            for(int i=1;i<report.runs.Count;i++)
            {
                Run r=report.runs[i];
                var c=new Comparison{strategy=r.strategy,secondsVersusWalk=r.gameSeconds-walk.gameSeconds,distanceVersusWalk=r.actualDistance-walk.actualDistance};
                for(int k=0;k<4;k++)c.milestoneSecondsVersusWalk[k]=r.milestoneTimes[k]>=0&&walk.milestoneTimes[k]>=0?r.milestoneTimes[k]-walk.milestoneTimes[k]:999;
                report.comparisons.Add(c);
                foreach(var window in r.dashWindows)
                {
                    Progress start=null,finish=null;float nearest=3f;
                    foreach(var p in walk.progress)if(Mathf.Abs(p.forward-window.startForward)<nearest)
                    {nearest=Mathf.Abs(p.forward-window.startForward);start=p;}
                    if(start==null)continue;
                    foreach(var p in walk.progress)if(p.time>=start.time+window.endTime-window.startTime){finish=p;break;}
                    if(finish==null)continue;
                    window.walkMatch=true;window.walkWindowForward=finish.forward-start.forward;
                    window.walkWindowDuration=finish.time-start.time;
                    window.forwardDifferenceFromWalk=window.forwardGain-window.walkWindowForward;
                }
            }
        }
        void Coverage()
        {
            if(report.runs.Count!=4){report.coverageGaps.Add("Four strategies not completed.");return;}
            Run walk=report.runs[0],brace=report.runs[1],dodge=report.runs[2],offense=report.runs[3];
            if(walk.launches==0)report.coverageGaps.Add("Walk: natural player launch NOT_OBSERVED; baseline vulnerability not covered.");
            if(brace.braceBlocks==0)report.coverageGaps.Add("Brace: real warned contact block NOT_OBSERVED.");
            bool dodgeThreat=false;
            foreach(var a in dodge.attacks)
                if(a.reacted&&a.rushAt>=a.reactionAt&&(a.projectedOpportunity||a.currentPositionOpportunity)&&
                    a.lateralDisplacement>=.35f&&a.firstRushMargin>.05f&&a.minimumRushMargin>.05f&&
                    !a.actualSweepOpportunity&&!a.launchedPlayer&&!a.blocked&&!a.protectedContact)dodgeThreat=true;
            if(!dodgeThreat||dodge.dodgeDecisions==0)report.coverageGaps.Add("Dodge: same warned attack, actual lateral displacement >=.35m, outside during its rush and no contact NOT_OBSERVED; prediction alone is insufficient.");
            bool impact=false;foreach(var w in offense.dashWindows)if(w.ownedLaunches>0&&w.complete&&w.walkMatch)impact=true;
            if(!impact)report.coverageGaps.Add("Offense: player-owned impact window with matched walk progression NOT_OBSERVED.");
            if(offense.clearanceCrossings==0)report.coverageGaps.Add("Offense: grounded passage through a player-launched NPC's vacated 0.72m ground circle while it remains airborne/recovering NOT_OBSERVED.");
            if(!report.pauseCleanupCovered)report.coverageGaps.Add("Controls: pause cancellation of an active natural warning/rush NOT_OBSERVED.");
            report.coverageComplete=report.coverageGaps.Count==0;
        }
        void Finish()
        {
            if(finished)return;finished=true;Input(Vector2.zero);
            RushPerformance.SetQaPhase("core-finish");
            RushPerformance.Flush();
            if(RushPerformance.IsEnabled)CheckThat("Frame diagnostics saved",RushPerformance.SaveSucceeded,"flush at suite end");
            report.utc=DateTime.UtcNow.ToString("o");report.elapsedRealSeconds=Time.realtimeSinceStartup-began;
            report.passed=report.failureCount==0;report.readyForAcceptance=report.passed&&report.coverageComplete;
            File.WriteAllText(Path.Combine(output,"core-report.json"),JsonUtility.ToJson(report,true));
            Debug.Log("RUSH_CORE_QA_FINISHED passed="+report.passed+" coverageComplete="+report.coverageComplete+
                " readyForAcceptance="+report.readyForAcceptance+" failures="+report.failureCount+" output="+output);
            Application.Quit(report.readyForAcceptance?0:1);
        }
    }
}
