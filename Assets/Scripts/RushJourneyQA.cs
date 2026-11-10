using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EarlyRush
{
    // Natural three-scene journeys. Only the ordinary input/UI methods are used.
    public sealed class RushJourneyQA : MonoBehaviour
    {
        [Serializable] public class Check { public string name, evidence; public bool passed; }
        [Serializable] public class Frames
        {
            public int count;
            public float seconds, meanMilliseconds, worstMilliseconds;
            public void Add(float dt) { if(dt<=0)return;count++;seconds+=dt;meanMilliseconds=1000*seconds/count;worstMilliseconds=Mathf.Max(worstMilliseconds,1000*dt); }
        }
        [Serializable] public class Performance
        {
            public Frames all=new Frames(), playing=new Frames(), captureTagged=new Frames(), loadingTagged=new Frames();
            public bool diagnosticsSaved;
        }
        [Serializable] public class EventEvidence
        {
            public int stage, id, activation, cumulativeStageEventHits, cumulativeStageEventBlocks;
            public string kind;
            public Rect area;
            public Vector2 direction, playerAtWarning, playerAtPulse;
            public float duration, firstWarning=-1, firstRemaining=-1, pulseAt=-1;
            public float maximumDirectionError, maximumAreaError;
            public bool warningObserved, pulseObserved, cancelledByQaPause, broken;
        }
        [Serializable] public class StageEvidence
        {
            public int stage, samples, waypointCount, visitedWaypoints, collisions;
            public int warnings, eventHits, eventBlocks, breaks, completed, playerLaunches, playerLands;
            public int groundPileSamples, groundPilePeak, allActivePilePeak;
            public float mapWidth, mapDepth, plannedDistance, actualDistance, gameSeconds;
            public float cameraSpanX, cameraSpanZ, minViewportX=1,maxViewportX,minViewportY=1,maxViewportY;
            public float braceSeconds,dashSeconds,movingSeconds,commandedIdleSeconds,eventBraceSeconds,clockHoldSeconds,clockEHeldMovementSeconds;
            public float maximumPositionSpeed,peakPlayerHeight,clockHoldPeak;
            public bool finite=true,inBounds=true,clearOfWalls=true,playerVisible=true,crowdFinite=true;
            public Frames frames=new Frames();
            public List<Vector2> visitedPoints=new List<Vector2>();
            public int counterflowPopulation,counterflowNearFrames,counterflowActorObservations,counterflowDistinctObserved,counterflowDistinctConfirmed,counterflowVisibleNegativePeak;
            public float counterflowReverseMetres,counterflowLongestContinuousSeconds,counterflowClosestDistance=999f;
            public bool counterflowReleaseObserved,counterflowScreenshotCaptured;
            public List<CounterflowActor> counterflowActors=new List<CounterflowActor>();
        }
        [Serializable] public class CounterflowActor
        {
            public int slot,generation,observedFrames;
            public float firstObservedAt,lastObservedAt,totalReverseMetres,continuousSeconds,continuousReverseMetres,longestContinuousSeconds;
            public float closestDistance=999f,maximumObservedReverseSpeed;
            public Vector2 firstPosition,lastPosition;
            public bool confirmed;
        }
        [Serializable] public class CounterflowFrame
        {
            public int slot,generation,frame;
            public Vector2 previousPosition,position;
            public float fixedSeconds,deltaZ,playerDistance;
        }
        [Serializable] public class RunEvidence
        {
            public string name,route;
            public bool reachedResults,lateWaitApplied,lateWaitStayedInStage=true,lateResult,onTime;
            public int transitions,collisions,eventWarnings,eventHits,eventBlocks,breaks,completed,clockIns;
            public int lateWaitStage=-1;
            public int playerLaunches,playerLands,ownedNpcLaunches,recoveryAttempts;
            public int eventHitsObservedAirborne,eventHitsRecovered;
            public float totalSeconds,realSeconds,naturalGameSeconds,deliberateLateWaitSeconds,qaInterventionGameSeconds;
            public float actualDistance,braceSeconds,dashSeconds,ordinaryBlockedDistance;
            public bool naturalDurationWithin180To300;
            public bool abortedForStall;
            public List<StallSnapshot> stalls=new List<StallSnapshot>();
            public List<StageEvidence> stages=new List<StageEvidence>();
            public List<EventEvidence> events=new List<EventEvidence>();
        }
        [Serializable] public class Shot
        {
            public string path,run,phase,state;
            public int stage,frame,warnings,eventHits,eventBlocks,breaks;
            public float gameSeconds,height,clockHold01,captureMilliseconds;
            public Vector2 position;
            public bool airborne,bracing,dashing,journeyWarningActive,stageParcelBroken;
            public int stageBreaks,activeJourneyId=-1,activeJourneyActivation;
            public float journeyWarningRemaining;
            public List<CounterflowFrame> counterflowActors=new List<CounterflowFrame>();
        }
        [Serializable] public class StallBody
        {
            public int slot,generation,combatPhase;
            public Vector2 position,velocity,separationOffset;
            public bool airborne,recovering;
        }
        [Serializable] public class StallSnapshot
        {
            public string run;
            public int stage,waypoint;
            public float gameTime,secondsWithoutWaypointProgress,closestWaypointDistance;
            public Vector2 position,target,input,velocity;
            public bool bracing,dashing;
            public List<Rect> nearbyObstacles=new List<Rect>();
            public List<StallBody> nearbyCrowd=new List<StallBody>();
        }
        [Serializable] public class Report
        {
            public string schema="v6-natural-journey-1",utc,engine,device;
            public string scope="Three natural input-driven journeys: risk, safe, charge. StartRun/BeginPlay/Restart are ordinary UI actions. No fixture, actor relocation, time override, event completion setter or stage skip. Charge deliberately waits after real travel to verify lateness.";
            public string timingScope="The 180-300s design target is reported for natural game time, not forced by idle input. Deliberate late waiting and QA pause/re-warning intervention game time are separate; necessary event response and final clock-in remain in natural time.";
            public string performanceScope="All rendered frames retain capture/loading stalls. Tagged subsets are diagnostic, not causal deductions. Actual capture CPU time is recorded separately; full RushPerformance diagnostics are flushed once at exit.";
            public string observationScope="Event warning/pulse observations use frame samples and preserve event id/activation. Movement follows author waypoints with bounded local recovery. Viewport checks do not prove absence of visual occlusion; screenshots need separate visual review.";
            public string counterflowScope="Station observations require an identified alighting slot, unchanged generation, both samples grounded, actual delta-z/fixed-time < -0.4m/s, player distance <=12m and body centre inside viewport. Confirmation requires >=0.25 continuous sampled seconds and >=0.20m actual reverse travel. Airborne/recovering bodies, teleports and static route intentions do not count. Screenshots bind to a qualifying observed frame; viewport membership does not prove no occlusion.";
            public bool passed,coverageComplete,readyForAcceptance,warningPauseCovered,warningReissuedCovered,restartEventCovered;
            public int failureCount;
            public float elapsedRealSeconds;
            public Performance perf=new Performance();
            public List<Check> checks=new List<Check>();
            public List<string> coverageGaps=new List<string>();
            public List<RunEvidence> runs=new List<RunEvidence>();
            public List<Shot> screenshots=new List<Shot>();
        }
        [Serializable] public class Status
        {
            public string run,state,hint;
            public int stage,waypoint;
            public float gameSeconds,realSeconds;
            public Vector2 position;
        }

        RushGame game;
        readonly Report report=new Report();
        RunEvidence run;
        StageEvidence stage;
        string output,currentName="controls";
        float began,runBegan,lastStatus,previousGameTime,previousFixedTime,lastCrowdSample=-10;
        Vector2 previousPosition,spawn,axis,recoveryTarget,dodgeTarget;
        Vector3 cameraMin,cameraMax;
        List<Vector2> route;
        int waypoint,captureUntil=-1,loadingUntil=-1,lastStage=-1;
        int baseWarnings,baseHits,baseBlocks,baseBreaks,baseCompleted,baseLaunches,baseLands;
        int previousEventHits,pendingEventLands;
        float bestAdvance,lastAdvanceAt,recoveryUntil,dodgeUntil,lastDashRequest=-10;
        float bestWaypointDistance,lastWaypointAdvanceAt;
        int progressWaypoint=-1;
        float lateWaitStart,lateWaitEnd,lateWaitRealStart;
        bool finished,capturing,lateWaiting,pauseUsed,midShot,warningShot,airShot,boxShot,clockShot;
        bool eventAirPending,eventAirSeen;
        int eventLandBaseline;
        readonly Dictionary<string,EventEvidence> events=new Dictionary<string,EventEvidence>();
        readonly Dictionary<string,CounterflowActor> counterflowActors=new Dictionary<string,CounterflowActor>();
        readonly List<CounterflowFrame> counterflowFrame=new List<CounterflowFrame>();
        RushGame.ImpactProbe[] previousCounterflowCrowd;
        const float Radius=.42f,PlanningMargin=.46f,Reaction=.18f,RunLimit=360f,SuiteLimit=1300f;
        const float WarningTolerance=.08f;

        IEnumerator Start()
        {
            if(!Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--journey-qa")){enabled=false;yield break;}
            game=GetComponent<RushGame>();game.QAInput=true;began=Time.realtimeSinceStartup;
            output=Path.Combine(Application.persistentDataPath,"journey-qa");
            foreach(string arg in Environment.GetCommandLineArgs())
                if(arg.StartsWith("--journey-qa-output=",StringComparison.Ordinal))output=arg.Substring("--journey-qa-output=".Length);
            Directory.CreateDirectory(output);
            report.utc=DateTime.UtcNow.ToString("o");report.engine=Application.unityVersion;report.device=SystemInfo.graphicsDeviceName;
            Application.logMessageReceived+=OnLog;
            yield return Controls();
            foreach(string name in new[]{"risk","safe","charge"})
            {if(finished)yield break;yield return Traverse(name);}
            if(!finished){CompareRoutes();Coverage();Finish();}
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
        void OnLog(string message,string trace,LogType type)
        {
            if(!finished&&(type==LogType.Exception||type==LogType.Error))
            {CheckThat("No runtime error",false,message+"\n"+trace);Finish();}
        }
        void Update()
        {
            if(finished||game==null)return;
            float dt=Time.unscaledDeltaTime;report.perf.all.Add(dt);
            if(game.State==RushState.Playing)report.perf.playing.Add(dt);
            if(capturing||Time.frameCount<=captureUntil)report.perf.captureTagged.Add(dt);
            if(Time.frameCount<=loadingUntil)report.perf.loadingTagged.Add(dt);
            if(Time.realtimeSinceStartup-lastStatus>2f)
            {
                lastStatus=Time.realtimeSinceStartup;
                long token=RushPerformance.Begin(RushPerformance.Scope.StatusWrite);
                try{File.WriteAllText(Path.Combine(output,"journey-status.json"),JsonUtility.ToJson(new Status{
                    run=currentName,state=game.State.ToString(),hint=game.JourneyHint,stage=game.Stage,waypoint=waypoint,
                    gameSeconds=game.TotalTime,realSeconds=Time.realtimeSinceStartup-began,position=game.Position},true));}
                finally{RushPerformance.End(RushPerformance.Scope.StatusWrite,token);}
            }
            if(Time.realtimeSinceStartup-began>SuiteLimit)
            {CheckThat("Suite safety timeout",false,"limit="+SuiteLimit+" run="+currentName+" position="+game.Position);Coverage();Finish();}
        }
        void CheckThat(string name,bool passed,string evidence)
        {
            if(finished)return;
            report.checks.Add(new Check{name=name,passed=passed,evidence=evidence});if(!passed)report.failureCount++;
            Debug.Log("RUSH_JOURNEY_QA_CHECK "+(passed?"PASS ":"FAIL ")+name+" | "+evidence);
        }
        void Input(Vector2 move,bool brace=false,bool dash=false,bool interact=false)
        {game.QAMove=Vector2.ClampMagnitude(move,1);game.QABrace=brace;game.QADash=dash;game.QAInteract=interact;}
        void ClearInput()=>Input(Vector2.zero);
        IEnumerator WaitReal(float seconds)
        {float end=Time.realtimeSinceStartup+seconds;while(!finished&&Time.realtimeSinceStartup<end)yield return null;}
        IEnumerator WaitObserved(float seconds)
        {
            float end=Time.realtimeSinceStartup+seconds;
            while(!finished&&Time.realtimeSinceStartup<end)
            {if(stage!=null&&game.Stage==stage.stage)Sample(game.JourneyProbeSnapshot());yield return null;}
        }
        void RestartNatural()
        {
            ClearInput();loadingUntil=Time.frameCount+2;
            Debug.Log("RUSH_JOURNEY_LOAD_BEGIN frame="+Time.frameCount+" real="+Time.realtimeSinceStartup);
            game.Restart();
            Debug.Log("RUSH_JOURNEY_LOAD_END frame="+Time.frameCount+" real="+Time.realtimeSinceStartup);
        }
        IEnumerator Controls()
        {
            RushPerformance.SetQaPhase("journey-controls");
            yield return WaitReal(.4f);
            CheckThat("Opens to menu",game.State==RushState.Menu,game.State.ToString());
            yield return Capture("menu");
            game.StartRun();ClearInput();yield return null;
            CheckThat("Start opens ordinary tutorial",game.State==RushState.Tutorial,game.State.ToString());
            yield return Capture("tutorial");
            game.BeginPlay();ClearInput();yield return null;yield return null;
            CheckThat("Ordinary run starts at first natural spawn",game.Stage==0&&game.TransitionCount==0&&
                Vector2.Distance(game.Position,new Vector2(game.World.spawn.x,game.World.spawn.z))<.05f,
                "stage="+game.Stage+" position="+game.Position);
            route=new List<Vector2>(game.World.riskRoute);waypoint=0;
            float end=Time.realtimeSinceStartup+45f;
            while(!finished&&Time.realtimeSinceStartup<end&&game.State==RushState.Playing&&!game.JourneyWarning)
            {Input(RouteDirection(game.Position,false));yield return null;}
            bool warned=game.JourneyWarning;
            RestartNatural();
            bool clean=game.Stage==0&&game.TransitionCount==0&&game.TotalTime==0&&
                game.JourneyWarnings==0&&game.JourneyHits==0&&game.JourneyBlocks==0&&game.JourneyBreaks==0&&
                game.JourneyCompleted==0&&game.JourneyClockIns==0&&!game.JourneyWarning&&game.ClockHold01==0;
            foreach(var p in game.JourneyProbeSnapshot())clean&=p.phase==0&&!p.broken&&p.hits==0&&p.blocks==0;
            report.restartEventCovered=warned&&clean;
            CheckThat("Restart clears journey events and clock-in",clean,"active warning before restart="+warned);
            ClearInput();yield return WaitReal(.1f);
        }
        IEnumerator Traverse(string name)
        {
            currentName=name;run=new RunEvidence{name=name,route=name=="safe"?"safe":"risk"};report.runs.Add(run);
            RushPerformance.SetQaPhase("journey-"+name);
            RestartNatural();runBegan=Time.realtimeSinceStartup;
            stage=null;lastStage=-1;events.Clear();pauseUsed=false;lateWaiting=false;
            previousEventHits=0;eventAirPending=eventAirSeen=false;pendingEventLands=0;
            recoveryUntil=dodgeUntil=0;lastDashRequest=-10;
            yield return null;yield return null;
            while(!finished&&Time.realtimeSinceStartup-runBegan<RunLimit)
            {
                if(game.Stage!=lastStage&&game.State==RushState.Playing)
                {
                    if(stage!=null)FinalizeStage();
                    BeginStage(name=="safe");
                    yield return Capture("stage-"+(game.Stage+1)+"-start");
                }
                var probes=game.JourneyProbeSnapshot();
                if(stage!=null&&game.Stage==stage.stage)Sample(probes);
                if(game.State==RushState.Results)break;
                if(game.State!=RushState.Playing){ClearInput();yield return null;continue;}
                if(stage==null){yield return null;continue;}
                Vector2 p=game.Position;
                // Finish rendering a live warning before the pause check may cancel it.
                if(!warningShot&&game.JourneyWarning&&!capturing)
                {warningShot=true;yield return Capture("stage-"+(game.Stage+1)+"-warning");continue;}
                if(!midShot&&game.StageProgress01>=.5f&&!capturing){midShot=true;StartCoroutine(Capture("stage-"+(game.Stage+1)+"-middle"));}
                var player=game.PlayerCombatProbeSnapshot();
                if(!airShot&&player.airborne&&player.height>1f&&!capturing){airShot=true;StartCoroutine(Capture("stage-"+(game.Stage+1)+"-air"));}
                bool stageParcelBroken=false;
                foreach(var q in probes)stageParcelBroken|=q.kind=="parcel-shortcut"&&q.broken;
                if(!boxShot&&game.JourneyBreaks>baseBreaks&&stageParcelBroken&&!capturing)
                {boxShot=true;StartCoroutine(Capture("stage-"+(game.Stage+1)+"-broken-box"));}
                if(game.Stage==1&&!stage.counterflowScreenshotCaptured&&counterflowFrame.Count>0&&!capturing)
                    StartCoroutine(Capture("stage-2-counterflow"));
                if(name=="risk"&&!pauseUsed&&warningShot&&!capturing&&game.JourneyWarning&&game.JourneyWarningRemaining>.35f)
                {
                    pauseUsed=true;float before=game.TotalTime;
                    yield return PauseEvent();
                    run.qaInterventionGameSeconds+=Mathf.Max(0,game.TotalTime-before);
                    previousPosition=game.Position;previousFixedTime=Time.fixedTime;previousGameTime=game.TotalTime;
                    continue;
                }
                if(name=="charge"&&!run.lateWaitApplied&&game.Stage==2&&game.StageProgress01>=.45f)
                {
                    run.lateWaitApplied=true;lateWaiting=true;lateWaitStart=game.TotalTime;
                    run.lateWaitStage=game.Stage;
                    lateWaitEnd=Mathf.Max(game.TimeLimit+1f,game.TotalTime+.1f);lateWaitRealStart=Time.realtimeSinceStartup;
                    Debug.Log("RUSH_JOURNEY_LATE_WAIT_BEGIN time="+lateWaitStart+" position="+p+" deadline="+game.TimeLimit);
                }
                if(lateWaiting)
                {
                    run.lateWaitStayedInStage&=game.Stage==run.lateWaitStage&&game.TransitionCount==run.lateWaitStage;
                    ClearInput();
                    run.deliberateLateWaitSeconds=Mathf.Max(0,game.TotalTime-lateWaitStart);
                    if(game.TotalTime>=lateWaitEnd)
                    {
                        lateWaiting=false;
                        lastWaypointAdvanceAt=game.TotalTime;progressWaypoint=-1;
                        CheckThat("Deadline wait preserves current stage after two completed stages",run.lateWaitStayedInStage,
                            "stage="+game.Stage+" transitions="+game.TransitionCount+" wait="+run.deliberateLateWaitSeconds);
                        CheckThat("Late journey remains playable",game.State==RushState.Playing&&game.TotalTime>game.TimeLimit,
                            "game="+game.TotalTime+" limit="+game.TimeLimit+" real wait="+(Time.realtimeSinceStartup-lateWaitRealStart));
                        yield return Capture("late-still-playing");
                    }
                    yield return null;continue;
                }
                Vector2 move=RouteDirection(p,name!="safe");
                float waypointDistance=Vector2.Distance(p,route[Mathf.Min(waypoint,route.Count-1)]);
                if(progressWaypoint!=waypoint||waypointDistance<bestWaypointDistance-.25f)
                {progressWaypoint=waypoint;bestWaypointDistance=waypointDistance;lastWaypointAdvanceAt=game.TotalTime;lastAdvanceAt=game.TotalTime;}
                if(game.TotalTime-lastWaypointAdvanceAt>=8f)
                {
                    var snapshot=CaptureStall();run.stalls.Add(snapshot);run.abortedForStall=true;
                    Debug.Log("RUSH_JOURNEY_STALL "+JsonUtility.ToJson(snapshot));
                    CheckThat(name+": waypoint progress watchdog",false,"8s without .25m progress; stage="+game.Stage+" waypoint="+waypoint+" position="+p);
                    break;
                }
                bool brace=false,dash=false,interact=false;
                bool atClock=game.Stage==2&&Vector2.Distance(p,new Vector2(game.World.goal.x,game.World.goal.z))<.9f;
                if(atClock)
                {
                    if(name=="risk")
                    {
                        // Real route arrival, no teleport: keep W held while E must brake automatically.
                        move=Vector2.up;interact=true;brace=false;
                    }
                    else {move=Vector2.zero;brace=true;}
                    if(!clockShot&&game.ClockHold01>.25f&&!capturing){clockShot=true;StartCoroutine(Capture("clock-in-hold"));}
                }
                else if(!player.airborne&&!player.recovering)
                {
                    RushGame.JourneyProbe? danger=null;
                    foreach(var q in probes)
                    {
                        if(q.kind=="parcel-shortcut"||q.phase!=1)continue;
                        EventEvidence seen;
                        if(events.TryGetValue(EventKey(game.Stage,q.id,q.activations),out seen)&&seen.warningObserved&&
                            game.TotalTime-seen.firstWarning>=Reaction&&q.area.Contains(p))
                        {danger=q;break;}
                    }
                    if(danger.HasValue&&name=="risk")brace=true;
                    if(danger.HasValue&&name=="safe"&&game.TotalTime>=dodgeUntil)
                    {
                        Vector2 choice;
                        if(ChooseDodge(p,danger.Value.area,out choice)){dodgeTarget=choice;dodgeUntil=game.TotalTime+.8f;}
                    }
                    if(name=="safe"&&game.TotalTime<dodgeUntil&&Vector2.Distance(p,dodgeTarget)>.25f)
                        move=StaticSteer(p,(dodgeTarget-p).normalized,false);
                    if(game.TotalTime<recoveryUntil&&Vector2.Distance(p,recoveryTarget)>.25f)
                        move=EscapeLineClear(p,recoveryTarget,name!="safe")?(recoveryTarget-p).normalized:
                            StaticSteer(p,(recoveryTarget-p).normalized,name!="safe");
                    if(!brace&&game.TotalTime-lastAdvanceAt>1.4f&&game.TotalTime>=recoveryUntil)
                    {
                        Vector2 choice;
                        if(ChooseRecovery(p,move,name!="safe",out choice))
                        {recoveryTarget=choice;recoveryUntil=game.TotalTime+.6f;run.recoveryAttempts++;}
                        lastAdvanceAt=game.TotalTime;
                    }
                    bool parcelAhead=name!="safe"&&BreakableAhead(p,move,probes);
                    bool ahead=parcelAhead||CrowdAhead(p,move);
                    if(!brace&&game.DashCooldown01>.99f&&game.TotalTime-lastDashRequest>1.16f&&
                        move.sqrMagnitude>.1f&&(parcelAhead||Vector2.Distance(p,route[Mathf.Min(waypoint,route.Count-1)])>1.2f)&&
                        (name=="charge"||ahead)&&LineClear(p,p+move*1.5f,name!="safe")&&
                        (name!="safe"||!DashWouldBreakParcel(p,move,probes)))
                    {dash=true;lastDashRequest=game.TotalTime;}
                }
                if(player.airborne||player.recovering){move=Vector2.zero;brace=false;dash=false;interact=false;}
                Input(move,brace,dash,interact);
                yield return null;
            }
            ClearInput();
            if(finished)yield break;
            if(stage!=null&&game.Stage==stage.stage)Sample(game.JourneyProbeSnapshot());
            if(stage!=null)FinalizeStage();
            run.reachedResults=game.State==RushState.Results;run.transitions=game.TransitionCount;
            run.totalSeconds=game.TotalTime;run.realSeconds=Time.realtimeSinceStartup-runBegan;
            run.collisions=game.Collisions;run.eventWarnings=game.JourneyWarnings;run.eventHits=game.JourneyHits;
            run.eventBlocks=game.JourneyBlocks;run.breaks=game.JourneyBreaks;run.completed=game.JourneyCompleted;
            run.clockIns=game.JourneyClockIns;run.playerLaunches=game.PlayerCombatProbeSnapshot().launchCount;
            run.playerLands=game.PlayerCombatProbeSnapshot().landCount;
            run.ownedNpcLaunches=game.TotalLaunches-game.NpcToNpcLaunches-game.JourneyCrowdLaunches;
            run.ordinaryBlockedDistance=game.OrdinaryBlockedDistance;run.lateResult=run.reachedResults&&game.TotalTime>game.TimeLimit;
            run.onTime=run.reachedResults&&game.TotalTime<=game.TimeLimit;
            run.naturalGameSeconds=Mathf.Max(0,run.totalSeconds-run.deliberateLateWaitSeconds-run.qaInterventionGameSeconds);
            run.naturalDurationWithin180To300=run.naturalGameSeconds>=180f&&run.naturalGameSeconds<=300f;
            foreach(var part in run.stages){run.actualDistance+=part.actualDistance;run.braceSeconds+=part.braceSeconds;run.dashSeconds+=part.dashSeconds;}
            CheckThat(name+": completes all three natural stages",run.reachedResults&&run.transitions==3&&run.stages.Count==3,
                "state="+game.State+" transitions="+run.transitions+" real="+run.realSeconds+" budget="+RunLimit);
            CheckThat(name+(name=="risk"?": final clock-in uses E while movement input remains held":": final clock-in retains stationary Shift compatibility"),
                run.clockIns==1&&(name!="risk"||(run.stages.Count>2&&run.stages[2].clockEHeldMovementSeconds>.1f)),
                "clock-ins="+run.clockIns+" hold peak="+(run.stages.Count>2?run.stages[2].clockHoldPeak:0)+
                " E+movement progress seconds="+(run.stages.Count>2?run.stages[2].clockEHeldMovementSeconds:0));
            if(name=="risk")CheckThat("Risk journey finishes on time",run.onTime,"seconds="+run.totalSeconds+" deadline="+game.TimeLimit);
            if(name=="safe")CheckThat("Safe journey preserves the parcel obstacle",run.breaks==0,"actual parcel breaks="+run.breaks);
            if(name=="charge")CheckThat("Charge journey completes after natural deadline wait",
                run.lateWaitApplied&&run.deliberateLateWaitSeconds>0&&run.lateWaitStayedInStage&&run.lateResult,
                "wait="+run.deliberateLateWaitSeconds+" game="+run.totalSeconds+" natural="+run.naturalGameSeconds);
            foreach(var e in run.events)
            {
                if(e.kind=="parcel-shortcut")continue;
                CheckThat(name+": event warning geometry stays locked "+e.id+"/"+e.activation,
                    e.maximumDirectionError<.1f&&e.maximumAreaError<.005f,
                    "direction="+e.maximumDirectionError+" area="+e.maximumAreaError);
                if(e.pulseObserved)
                    CheckThat(name+": event pulse follows complete observed warning "+e.id+"/"+e.activation,
                        e.warningObserved&&!e.cancelledByQaPause&&e.firstRemaining>=e.duration-WarningTolerance&&
                        e.pulseAt-e.firstWarning>=e.duration-WarningTolerance,
                        "first remaining="+e.firstRemaining+" observed lead="+(e.pulseAt-e.firstWarning)+" duration="+e.duration);
            }
            while(capturing&&!finished)yield return null;
            yield return Capture(name+(run.reachedResults?"-results":"-aborted"));
            Debug.Log("RUSH_JOURNEY_RUN "+JsonUtility.ToJson(run));
            run=null;stage=null;yield return null;
        }
        void BeginStage(bool safe)
        {
            lastStage=game.Stage;route=new List<Vector2>(safe?game.World.safeRoute:game.World.riskRoute);waypoint=0;
            if(route.Count==0){CheckThat(currentName+": author route exists",false,"stage="+game.Stage);route.Add(game.JourneyTarget);}
            stage=new StageEvidence{stage=game.Stage,waypointCount=route.Count,
                mapWidth=game.World.boundsMax.x-game.World.boundsMin.x,mapDepth=game.World.boundsMax.y-game.World.boundsMin.y};
            run.stages.Add(stage);spawn=new Vector2(game.World.spawn.x,game.World.spawn.z);
            axis=(new Vector2(game.World.goal.x,game.World.goal.z)-spawn).normalized;
            previousPosition=game.Position;previousGameTime=game.TotalTime;previousFixedTime=Time.fixedTime;
            cameraMin=cameraMax=game.GameCamera.transform.position;bestAdvance=0;lastAdvanceAt=game.TotalTime;
            progressWaypoint=-1;bestWaypointDistance=float.MaxValue;lastWaypointAdvanceAt=game.TotalTime;
            recoveryUntil=dodgeUntil=0;lastCrowdSample=-10;midShot=warningShot=airShot=boxShot=clockShot=false;
            previousCounterflowCrowd=null;counterflowActors.Clear();counterflowFrame.Clear();
            baseWarnings=game.JourneyWarnings;baseHits=game.JourneyHits;baseBlocks=game.JourneyBlocks;baseBreaks=game.JourneyBreaks;baseCompleted=game.JourneyCompleted;
            var player=game.PlayerCombatProbeSnapshot();baseLaunches=player.launchCount;baseLands=player.landCount;
            float length=0;Vector2 before=game.Position;bool clear=true;
            long token=RushPerformance.Begin(RushPerformance.Scope.QaValidation);
            try{foreach(Vector2 point in route){length+=Vector2.Distance(before,point);clear&=LineClear(before,point,!safe);before=point;}}
            finally{RushPerformance.End(RushPerformance.Scope.QaValidation,token);}
            stage.plannedDistance=length;
            CheckThat(currentName+": stage "+(game.Stage+1)+" route respects static geometry",clear,
                "planned metres="+length+" route="+run.route+" breakable parcel permitted only as an input obstacle");
            CheckThat(currentName+": stage "+(game.Stage+1)+" events start clean",
                !game.JourneyWarning&&game.ClockHold01==0,"warnings active="+game.JourneyWarning+" clock="+game.ClockHold01);
            loadingUntil=Time.frameCount+2;
        }
        void Sample(RushGame.JourneyProbe[] probes)
        {
            long token=RushPerformance.Begin(RushPerformance.Scope.QaSample);
            try{
            float now=game.TotalTime,dt=Mathf.Max(0,now-previousGameTime);
            Vector2 p=game.Position;var player=game.PlayerCombatProbeSnapshot();
            stage.samples++;if(game.State==RushState.Playing)stage.frames.Add(Time.unscaledDeltaTime);
            stage.actualDistance+=Vector2.Distance(previousPosition,p);
            float physicsDelta=Time.fixedTime-previousFixedTime;
            var stationCrowd=game.Stage==1?game.ImpactProbeSnapshot():null;
            if(stationCrowd!=null)ObserveCounterflow(stationCrowd,physicsDelta,now);
            if(physicsDelta>.00001f)stage.maximumPositionSpeed=Mathf.Max(stage.maximumPositionSpeed,Vector2.Distance(previousPosition,p)/physicsDelta);
            stage.finite&=Finite(p.x)&&Finite(p.y)&&Finite(player.height)&&Finite(game.Velocity.sqrMagnitude);
            stage.inBounds&=InsideBounds(p,Radius-.02f);
            stage.clearOfWalls&=Free(p,Radius-.03f,false);
            stage.peakPlayerHeight=Mathf.Max(stage.peakPlayerHeight,player.height);
            stage.clockHoldPeak=Mathf.Max(stage.clockHoldPeak,game.ClockHold01);
            if(game.IsBracing)stage.braceSeconds+=dt;if(game.IsDashing)stage.dashSeconds+=dt;
            if(game.QAMove.sqrMagnitude>.01f&&game.Velocity.sqrMagnitude>.09f)stage.movingSeconds+=dt;
            if(game.QAMove.sqrMagnitude<.01f)stage.commandedIdleSeconds+=dt;
            if(game.JourneyWarning&&game.IsBracing)stage.eventBraceSeconds+=dt;
            if(game.Stage==2&&game.ClockHold01>0)
            {
                if(game.QAInteract||(game.QABrace&&game.QAMove.sqrMagnitude<.01f))stage.clockHoldSeconds+=dt;
                if(game.QAInteract&&!game.QABrace&&game.QAMove.sqrMagnitude>.5f)stage.clockEHeldMovementSeconds+=dt;
            }
            stage.warnings=game.JourneyWarnings-baseWarnings;stage.eventHits=game.JourneyHits-baseHits;
            stage.eventBlocks=game.JourneyBlocks-baseBlocks;stage.breaks=game.JourneyBreaks-baseBreaks;stage.completed=game.JourneyCompleted-baseCompleted;
            stage.playerLaunches=player.launchCount-baseLaunches;stage.playerLands=player.landCount-baseLands;
            foreach(var q in probes)
            {
                string key=EventKey(game.Stage,q.id,q.activations);EventEvidence e;
                if(!events.TryGetValue(key,out e))
                {
                    if(q.phase==0&&!q.broken)continue;
                    e=new EventEvidence{stage=game.Stage,id=q.id,activation=q.activations,kind=q.kind,
                        area=q.area,direction=q.direction,duration=q.warningDuration};
                    events.Add(key,e);run.events.Add(e);
                }
                e.cumulativeStageEventHits=Mathf.Max(e.cumulativeStageEventHits,q.hits);
                e.cumulativeStageEventBlocks=Mathf.Max(e.cumulativeStageEventBlocks,q.blocks);e.broken|=q.broken;
                if(q.phase==1||q.phase==2)
                {
                    e.maximumDirectionError=Mathf.Max(e.maximumDirectionError,Vector2.Angle(e.direction,q.direction));
                    e.maximumAreaError=Mathf.Max(e.maximumAreaError,RectDifference(e.area,q.area));
                }
                if(q.phase==1&&!e.warningObserved)
                {e.warningObserved=true;e.firstWarning=now;e.firstRemaining=q.warningRemaining;e.playerAtWarning=p;}
                if(q.phase==2&&!e.pulseObserved&&q.kind!="parcel-shortcut")
                {e.pulseObserved=true;e.pulseAt=now;e.playerAtPulse=p;}
            }
            if(game.JourneyHits>previousEventHits)
            {
                pendingEventLands+=game.JourneyHits-previousEventHits;eventAirPending=true;
                eventAirSeen=false;eventLandBaseline=player.landCount;
            }
            if(eventAirPending&&player.airborne&&player.height>.15f&&!eventAirSeen)
            {run.eventHitsObservedAirborne+=pendingEventLands;eventAirSeen=true;}
            if(eventAirPending&&eventAirSeen&&!player.airborne&&!player.recovering&&player.landCount>eventLandBaseline)
            {run.eventHitsRecovered+=pendingEventLands;pendingEventLands=0;eventAirPending=eventAirSeen=false;}
            previousEventHits=game.JourneyHits;
            Vector3 view=game.GameCamera.WorldToViewportPoint(new Vector3(p.x,1+player.height,p.y));
            stage.minViewportX=Mathf.Min(stage.minViewportX,view.x);stage.maxViewportX=Mathf.Max(stage.maxViewportX,view.x);
            stage.minViewportY=Mathf.Min(stage.minViewportY,view.y);stage.maxViewportY=Mathf.Max(stage.maxViewportY,view.y);
            stage.playerVisible&=view.z>0&&view.x>=.02f&&view.x<=.98f&&view.y>=.02f&&view.y<=.98f;
            cameraMin=Vector3.Min(cameraMin,game.GameCamera.transform.position);cameraMax=Vector3.Max(cameraMax,game.GameCamera.transform.position);
            if(now-lastCrowdSample>=.5f)
            {
                lastCrowdSample=now;stage.groundPileSamples++;var crowd=stationCrowd??game.ImpactProbeSnapshot();
                for(int i=0;i<crowd.Length;i++)
                {
                    if(!crowd[i].active)continue;
                    Vector2 c=crowd[i].position;stage.crowdFinite&=Finite(c.x)&&Finite(c.y)&&InsideBounds(c,.30f)&&Finite(crowd[i].height);
                    int all=0,ground=0;
                    for(int j=0;j<crowd.Length;j++)
                        if(crowd[j].active&&(crowd[j].position-c).sqrMagnitude<.25f*.25f)
                        {all++;if(!crowd[j].airborne&&!crowd[j].recovering)ground++;}
                    stage.allActivePilePeak=Mathf.Max(stage.allActivePilePeak,all);
                    stage.groundPilePeak=Mathf.Max(stage.groundPilePeak,ground);
                }
            }
            previousPosition=p;previousGameTime=now;previousFixedTime=Time.fixedTime;
            }finally{RushPerformance.End(RushPerformance.Scope.QaSample,token);}
        }
        void FinalizeStage()
        {
            stage.gameSeconds=game.Stage==stage.stage&&game.State==RushState.Playing?game.StageTime:game.SplitTimes[stage.stage];
            stage.collisions=game.SplitCollisions[stage.stage];
            stage.cameraSpanX=cameraMax.x-cameraMin.x;stage.cameraSpanZ=cameraMax.z-cameraMin.z;
            string label=currentName+" stage "+(stage.stage+1);
            CheckThat(label+": continuous movement, walls and camera containment",
                stage.samples>0&&stage.finite&&stage.inBounds&&stage.clearOfWalls&&stage.playerVisible&&stage.maximumPositionSpeed<=18f,
                "distance="+stage.actualDistance+" max speed="+stage.maximumPositionSpeed+" walls="+stage.clearOfWalls+
                " bounds="+stage.inBounds+" viewport="+stage.minViewportX+","+stage.maxViewportX+"/"+stage.minViewportY+","+stage.maxViewportY);
            CheckThat(label+": follows the expanded map",stage.mapDepth>=190f&&stage.actualDistance>150f&&stage.cameraSpanZ>120f,
                "depth="+stage.mapDepth+" distance="+stage.actualDistance+" camera span="+stage.cameraSpanZ);
            CheckThat(label+": samples finite crowd without ground pile",stage.crowdFinite&&stage.groundPileSamples>0&&stage.groundPilePeak<=3,
                "samples="+stage.groundPileSamples+" ground peak="+stage.groundPilePeak+" all-active peak="+stage.allActivePilePeak);
            if(stage.stage==1)
            {
                CheckThat(currentName+": station releases its natural alighting group",
                    stage.counterflowPopulation>0&&stage.counterflowReleaseObserved,
                    "identified population="+stage.counterflowPopulation+" release observed="+stage.counterflowReleaseObserved+
                    "; release alone is not encounter evidence");
                if(currentName=="risk")
                    CheckThat("Risk: real nearby counterflow encounter and screenshot",
                        stage.counterflowDistinctConfirmed>0&&stage.counterflowScreenshotCaptured,
                        "confirmed actors="+stage.counterflowDistinctConfirmed+" observed frames="+stage.counterflowNearFrames+
                        " closest metres="+stage.counterflowClosestDistance+" reverse actor-metres="+stage.counterflowReverseMetres+
                        " screenshot="+stage.counterflowScreenshotCaptured);
            }
            Debug.Log("RUSH_JOURNEY_STAGE "+JsonUtility.ToJson(stage));
        }
        void ObserveCounterflow(RushGame.ImpactProbe[] crowd,float fixedSeconds,float now)
        {
            counterflowFrame.Clear();
            stage.counterflowPopulation=game.JourneyAlightingCount;
            stage.counterflowReleaseObserved|=game.JourneyAlightingReleased;
            // Render frames without a physics step provide no displacement evidence.
            if(fixedSeconds<=.00001f){previousCounterflowCrowd=crowd;return;}
            bool comparable=previousCounterflowCrowd!=null&&previousCounterflowCrowd.Length==crowd.Length&&fixedSeconds<=.2f;
            int observedThisFrame=0;
            for(int i=0;i<crowd.Length;i++)
            {
                if(!game.IsJourneyAlighting(i))continue;
                var body=crowd[i];string key=i+":"+body.generation;
                CounterflowActor actor;counterflowActors.TryGetValue(key,out actor);
                bool observed=false;Vector2 delta=Vector2.zero;float distance=Vector2.Distance(game.Position,body.position);
                if(comparable)
                {
                    var previous=previousCounterflowCrowd[i];delta=body.position-previous.position;
                    Vector3 view=game.GameCamera.WorldToViewportPoint(new Vector3(body.position.x,1f,body.position.y));
                    observed=game.JourneyAlightingReleased&&body.routeTarget.y<body.position.y-.2f&&
                        body.active&&previous.active&&body.generation==previous.generation&&
                        !body.airborne&&!body.recovering&&!previous.airborne&&!previous.recovering&&
                        Finite(delta.x)&&Finite(delta.y)&&delta.magnitude/fixedSeconds<=12.25f&&
                        delta.y/fixedSeconds<-.4f&&distance<=12f&&
                        view.z>0&&view.x>=.02f&&view.x<=.98f&&view.y>=.02f&&view.y<=.98f;
                }
                if(!observed)
                {
                    if(actor!=null){actor.continuousSeconds=0;actor.continuousReverseMetres=0;}
                    continue;
                }
                if(actor==null)
                {
                    actor=new CounterflowActor{slot=i,generation=body.generation,firstObservedAt=now,
                        firstPosition=previousCounterflowCrowd[i].position};
                    counterflowActors.Add(key,actor);stage.counterflowActors.Add(actor);stage.counterflowDistinctObserved++;
                }
                actor.observedFrames++;actor.lastObservedAt=now;actor.lastPosition=body.position;
                actor.totalReverseMetres+=-delta.y;actor.continuousSeconds+=fixedSeconds;actor.continuousReverseMetres+=-delta.y;
                actor.longestContinuousSeconds=Mathf.Max(actor.longestContinuousSeconds,actor.continuousSeconds);
                actor.closestDistance=Mathf.Min(actor.closestDistance,distance);
                actor.maximumObservedReverseSpeed=Mathf.Max(actor.maximumObservedReverseSpeed,-delta.y/fixedSeconds);
                stage.counterflowReverseMetres+=-delta.y;stage.counterflowActorObservations++;observedThisFrame++;
                stage.counterflowClosestDistance=Mathf.Min(stage.counterflowClosestDistance,distance);
                stage.counterflowLongestContinuousSeconds=Mathf.Max(stage.counterflowLongestContinuousSeconds,actor.continuousSeconds);
                if(!actor.confirmed&&actor.continuousSeconds>=.25f&&actor.continuousReverseMetres>=.20f)
                {actor.confirmed=true;stage.counterflowDistinctConfirmed++;}
                if(actor.confirmed)
                    counterflowFrame.Add(new CounterflowFrame{slot=i,generation=body.generation,frame=Time.frameCount,
                        previousPosition=previousCounterflowCrowd[i].position,position=body.position,fixedSeconds=fixedSeconds,
                        deltaZ=delta.y,playerDistance=distance});
            }
            if(observedThisFrame>0)stage.counterflowNearFrames++;
            stage.counterflowVisibleNegativePeak=Mathf.Max(stage.counterflowVisibleNegativePeak,observedThisFrame);
            previousCounterflowCrowd=crowd;
        }
        IEnumerator PauseEvent()
        {
            ClearInput();var before=game.JourneyProbeSnapshot();int id=-1,activation=-1;
            foreach(var p in before)if(p.phase==1){id=p.id;activation=p.activations;break;}
            foreach(var e in run.events)if(e.id==id&&e.activation==activation)e.cancelledByQaPause=true;
            game.TogglePause();yield return new WaitForEndOfFrame();
            bool clean=!game.JourneyWarning&&game.JourneyWarningRemaining==0&&game.ImpactFreezeRemaining==0&&game.ActiveImpactFxCount==0;
            foreach(var p in game.JourneyProbeSnapshot())clean&=p.phase!=1;
            float time=game.TotalTime,clock=game.ClockHold01;Vector2 position=game.Position;
            var player=game.PlayerCombatProbeSnapshot();var crowd=game.ImpactProbeSnapshot();Vector3 camera=game.GameCamera.transform.position;
            yield return WaitReal(.35f);
            var after=game.ImpactProbeSnapshot();var afterPlayer=game.PlayerCombatProbeSnapshot();
            bool frozen=game.State==RushState.Paused&&Mathf.Abs(game.TotalTime-time)<.001f&&
                Vector2.Distance(position,game.Position)<.001f&&Mathf.Abs(player.height-afterPlayer.height)<.001f&&
                Mathf.Abs(clock-game.ClockHold01)<.001f&&Vector3.Distance(camera,game.GameCamera.transform.position)<.001f&&crowd.Length==after.Length;
            for(int i=0;i<Mathf.Min(crowd.Length,after.Length);i++)
                frozen&=Vector2.Distance(crowd[i].position,after[i].position)<.001f&&Mathf.Abs(crowd[i].height-after[i].height)<.001f;
            CheckThat("Pause clears event warning and freezes world",clean&&frozen,"warning id="+id+" clean="+clean+" frozen="+frozen);
            report.warningPauseCovered=id>=0&&clean&&frozen;
            yield return Capture("event-paused");
            game.TogglePause();Input(Vector2.zero,true,true,true);yield return WaitObserved(.08f);
            CheckThat("Resume does not replay held Space / Shift / E actions",game.InputLocked&&!game.IsDashing&&!game.IsBracing,"locked="+game.InputLocked);
            ClearInput();yield return WaitObserved(.08f);
            float until=Time.realtimeSinceStartup+2f;bool reissued=false;float remaining=-1,duration=-1;
            while(!finished&&Time.realtimeSinceStartup<until&&!reissued)
            {
                Sample(game.JourneyProbeSnapshot());
                foreach(var p in game.JourneyProbeSnapshot())
                    if(p.id==id&&p.activations>activation&&p.phase==1)
                    {
                        EventEvidence observed;
                        if(events.TryGetValue(EventKey(game.Stage,p.id,p.activations),out observed))
                        {remaining=observed.firstRemaining;duration=observed.duration;reissued=observed.warningObserved;}
                        break;
                    }
                if(!reissued)yield return null;
            }
            report.warningReissuedCovered=reissued&&remaining>=duration-WarningTolerance;
            CheckThat("Resume supplies a fresh full event warning",report.warningReissuedCovered,
                "id="+id+" reissued="+reissued+" first observed remaining="+remaining+" duration="+duration+" sampled throughout held/release checks");
        }
        bool ChooseDodge(Vector2 p,Rect area,out Vector2 choice)
        {
            choice=p;float best=float.MaxValue;
            foreach(Vector2 candidate in new[]{new Vector2(area.xMin-.65f,p.y),new Vector2(area.xMax+.65f,p.y),
                new Vector2(p.x,area.yMin-.65f),new Vector2(p.x,area.yMax+.65f)})
            {
                float distance=Vector2.Distance(p,candidate);
                if(distance<best&&distance<5f&&LineClear(p,candidate,false)){best=distance;choice=candidate;}
            }
            return best<float.MaxValue;
        }
        bool ChooseRecovery(Vector2 p,Vector2 direction,bool breakable,out Vector2 choice)
        {
            choice=p;if(direction.sqrMagnitude<.1f)direction=axis;
            Vector2 side=new Vector2(-direction.y,direction.x);
            foreach(float sign in new[]{1f,-1f})
            {
                Vector2 candidate=p+side*sign*1.15f+direction*.6f;
                if(LineClear(p,candidate,breakable)){choice=candidate;return true;}
            }
            // Physical contact can be inside the extra .04m planning buffer.
            // Retreat through input without deepening that contact; restore the
            // full planning margin at the endpoint before following the route.
            foreach(Vector2 offset in new[]{-direction*.8f,(-direction+side).normalized*.9f,
                (-direction-side).normalized*.9f,side*.9f,-side*.9f})
            {
                Vector2 candidate=p+offset;
                if(EscapeLineClear(p,candidate,breakable)){choice=candidate;return true;}
            }
            return false;
        }
        bool EscapeLineClear(Vector2 from,Vector2 to,bool allowBreakable)
        {
            if(!Free(to,PlanningMargin,allowBreakable))return false;
            var parcels=allowBreakable?game.JourneyProbeSnapshot():null;
            int samples=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(from,to)/.08f));
            for(int i=1;i<=samples;i++)
            {
                Vector2 point=Vector2.Lerp(from,to,(float)i/samples);
                if(!InsideBounds(point,Radius-.001f))return false;
                foreach(Rect obstacle in game.World.obstacles)
                {
                    bool permitted=false;
                    if(parcels!=null)foreach(var q in parcels)
                        if(q.kind=="parcel-shortcut"&&!q.broken&&q.area.Equals(obstacle)){permitted=true;break;}
                    if(permitted)continue;
                    float startClearance=DistanceToRect(from,obstacle);
                    float floor=Mathf.Max(Radius-.001f,Mathf.Min(startClearance,PlanningMargin));
                    if(DistanceToRect(point,obstacle)+.0001f<floor)return false;
                }
            }
            return true;
        }
        static float DistanceToRect(Vector2 point,Rect rect)
        {
            return Vector2.Distance(point,new Vector2(Mathf.Clamp(point.x,rect.xMin,rect.xMax),
                Mathf.Clamp(point.y,rect.yMin,rect.yMax)));
        }
        StallSnapshot CaptureStall()
        {
            var evidence=new StallSnapshot{run=currentName,stage=game.Stage,waypoint=waypoint,
                gameTime=game.TotalTime,secondsWithoutWaypointProgress=game.TotalTime-lastWaypointAdvanceAt,
                closestWaypointDistance=bestWaypointDistance,position=game.Position,
                target=route[Mathf.Min(waypoint,route.Count-1)],input=game.QAMove,velocity=game.Velocity,
                bracing=game.IsBracing,dashing=game.IsDashing};
            foreach(Rect obstacle in game.World.obstacles)
                if(DistanceToRect(game.Position,obstacle)<4f)evidence.nearbyObstacles.Add(obstacle);
            var bodies=game.ImpactProbeSnapshot();var combat=game.CombatProbeSnapshot();
            for(int i=0;i<bodies.Length;i++)
            {
                var body=bodies[i];if(!body.active||Vector2.Distance(body.position,game.Position)>4f)continue;
                evidence.nearbyCrowd.Add(new StallBody{slot=i,generation=body.generation,
                    combatPhase=i<combat.Length?combat[i].phase:-1,position=body.position,velocity=body.velocity,
                    separationOffset=body.separationOffset,airborne=body.airborne,recovering=body.recovering});
            }
            return evidence;
        }
        Vector2 RouteDirection(Vector2 p,bool breakable)
        {
            if(route==null||route.Count==0)return Vector2.zero;
            while(waypoint<route.Count-1)
            {
                Vector2 segmentStart=waypoint==0?new Vector2(game.World.spawn.x,game.World.spawn.z):route[waypoint-1];
                Vector2 segment=route[waypoint]-segmentStart;
                bool reached=Vector2.Distance(p,route[waypoint])<.7f;
                bool passed=segment.sqrMagnitude>.01f&&Vector2.Dot(p-route[waypoint],segment)>=0&&
                    Vector2.Distance(p,route[waypoint])<2f&&LineClear(p,route[waypoint+1],breakable);
                if(!reached&&!passed)break;
                if(stage!=null){stage.visitedWaypoints++;stage.visitedPoints.Add(p);}waypoint++;
            }
            Vector2 delta=route[waypoint]-p;
            if(delta.sqrMagnitude<.05f)return Vector2.zero;
            return StaticSteer(p,delta.normalized,breakable)*Mathf.Clamp(delta.magnitude/1.2f,.35f,1f);
        }
        Vector2 StaticSteer(Vector2 p,Vector2 direction,bool breakable)
        {
            if(LineClear(p,p+direction*.8f,breakable))return direction;
            foreach(float angle in new[]{25f,-25f,50f,-50f,75f,-75f})
            {
                float radians=angle*Mathf.Deg2Rad;
                Vector2 test=new Vector2(direction.x*Mathf.Cos(radians)-direction.y*Mathf.Sin(radians),
                    direction.x*Mathf.Sin(radians)+direction.y*Mathf.Cos(radians));
                if(LineClear(p,p+test*.8f,breakable))return test;
            }
            return Vector2.zero;
        }
        bool CrowdAhead(Vector2 p,Vector2 direction)
        {
            if(direction.sqrMagnitude<.1f)return false;
            foreach(var c in game.ImpactProbeSnapshot())
            {
                if(!c.active||c.airborne||c.recovering)continue;
                Vector2 delta=c.position-p;float ahead=Vector2.Dot(delta,direction);
                if(ahead>.7f&&ahead<3.5f&&Mathf.Abs(delta.x*direction.y-delta.y*direction.x)<.85f)return true;
            }
            return false;
        }
        bool BreakableAhead(Vector2 p,Vector2 direction,RushGame.JourneyProbe[] probes)
        {
            foreach(var q in probes)
                if(q.kind=="parcel-shortcut"&&!q.broken)
                    for(float distance=.2f;distance<=3.2f;distance+=.2f)
                        if(q.area.Contains(p+direction*distance))return true;
            return false;
        }
        bool DashWouldBreakParcel(Vector2 from,Vector2 direction,RushGame.JourneyProbe[] probes)
        {
            Vector2 to=from+direction*((game.Stage==0?13f:11.3f)*.20f+.1f);
            foreach(var parcel in probes)
            {
                if(parcel.kind!="parcel-shortcut"||parcel.broken)continue;
                Rect area=parcel.area;float padding=Radius+.05f;
                area=new Rect(area.xMin-padding,area.yMin-padding,area.width+2*padding,area.height+2*padding);
                float enter=0,exit=1;Vector2 delta=to-from;bool intersects=true;
                for(int component=0;component<2;component++)
                {
                    float origin=component==0?from.x:from.y,step=component==0?delta.x:delta.y;
                    float lower=component==0?area.xMin:area.yMin,upper=component==0?area.xMax:area.yMax;
                    if(Mathf.Abs(step)<.00001f){if(origin<lower||origin>upper){intersects=false;break;}continue;}
                    float a=(lower-origin)/step,b=(upper-origin)/step;if(a>b){float swap=a;a=b;b=swap;}
                    enter=Mathf.Max(enter,a);exit=Mathf.Min(exit,b);if(enter>exit){intersects=false;break;}
                }
                if(intersects)return true;
            }
            return false;
        }
        bool InsideBounds(Vector2 p,float margin)
        {return p.x>=game.World.boundsMin.x+margin&&p.x<=game.World.boundsMax.x-margin&&p.y>=game.World.boundsMin.y+margin&&p.y<=game.World.boundsMax.y-margin;}
        bool Free(Vector2 point,float clearance,bool allowBreakable,RushGame.JourneyProbe[] cachedParcels=null)
        {
            if(!InsideBounds(point,clearance))return false;
            RushGame.JourneyProbe[] parcels=allowBreakable?(cachedParcels??game.JourneyProbeSnapshot()):null;
            foreach(Rect obstacle in game.World.obstacles)
            {
                bool permitted=false;
                if(parcels!=null)foreach(var q in parcels)
                    if(q.kind=="parcel-shortcut"&&!q.broken&&q.area.Equals(obstacle)){permitted=true;break;}
                if(permitted)continue;
                float x=Mathf.Clamp(point.x,obstacle.xMin,obstacle.xMax),y=Mathf.Clamp(point.y,obstacle.yMin,obstacle.yMax);
                if((point-new Vector2(x,y)).sqrMagnitude<clearance*clearance)return false;
            }
            return true;
        }
        bool LineClear(Vector2 a,Vector2 b,bool allowBreakable)
        {
            int count=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/.16f));
            var parcels=allowBreakable?game.JourneyProbeSnapshot():null;
            for(int i=1;i<=count;i++)if(!Free(Vector2.Lerp(a,b,(float)i/count),PlanningMargin,allowBreakable,parcels))return false;
            return true;
        }
        static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
        static string EventKey(int s,int id,int activation){return s+":"+id+":"+activation;}
        static float RectDifference(Rect a,Rect b)
        {return Mathf.Max(Mathf.Abs(a.x-b.x),Mathf.Abs(a.y-b.y),Mathf.Abs(a.width-b.width),Mathf.Abs(a.height-b.height));}
        IEnumerator Capture(string phase)
        {
            if(capturing||finished)yield break;capturing=true;
            yield return new WaitForEndOfFrame();
            bool isCounterflow=phase.EndsWith("-counterflow",StringComparison.Ordinal);
            var counterflowEvidence=new List<CounterflowFrame>();
            if(isCounterflow)
            {
                var bodies=game.ImpactProbeSnapshot();
                foreach(var observed in counterflowFrame)
                {
                    if(observed.frame!=Time.frameCount||observed.slot>=bodies.Length)continue;
                    var body=bodies[observed.slot];
                    Vector3 view=game.GameCamera.WorldToViewportPoint(new Vector3(body.position.x,1f,body.position.y));
                    if(game.JourneyAlightingReleased&&body.routeTarget.y<body.position.y-.2f&&
                        body.active&&body.generation==observed.generation&&!body.airborne&&!body.recovering&&
                        Vector2.Distance(body.position,observed.position)<.001f&&Vector2.Distance(body.position,game.Position)<=12f&&
                        view.z>0&&view.x>=.02f&&view.x<=.98f&&view.y>=.02f&&view.y<=.98f)
                        counterflowEvidence.Add(observed);
                }
                // Do not label a later, unmatched frame as an encounter screenshot.
                if(counterflowEvidence.Count==0){capturing=false;yield break;}
            }
            string path=Path.Combine(output,currentName+"-"+phase+"-"+Time.frameCount+".png");
            var player=game.PlayerCombatProbeSnapshot();
            var shot=new Shot{path=path,run=currentName,phase=phase,state=game.State.ToString(),stage=game.Stage,frame=Time.frameCount,gameSeconds=game.TotalTime,
                position=game.Position,height=player.height,airborne=player.airborne,bracing=game.IsBracing,dashing=game.IsDashing,
                warnings=game.JourneyWarnings,eventHits=game.JourneyHits,eventBlocks=game.JourneyBlocks,breaks=game.JourneyBreaks,clockHold01=game.ClockHold01,
                journeyWarningActive=game.JourneyWarning,journeyWarningRemaining=game.JourneyWarningRemaining,
                stageBreaks=run==null?0:game.JourneyBreaks-baseBreaks};
            shot.counterflowActors.AddRange(counterflowEvidence);
            foreach(var q in game.JourneyProbeSnapshot())
            {
                if(q.phase==1){shot.activeJourneyId=q.id;shot.activeJourneyActivation=q.activations;}
                if(q.kind=="parcel-shortcut"&&q.broken)shot.stageParcelBroken=true;
            }
            var timer=System.Diagnostics.Stopwatch.StartNew();
            RushPerformance.MarkCapture(phase,true);
            try{Texture2D texture=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(path,texture.EncodeToPNG());}finally{Destroy(texture);}}
            finally{RushPerformance.MarkCapture(phase,false);timer.Stop();shot.captureMilliseconds=(float)timer.Elapsed.TotalMilliseconds;capturing=false;captureUntil=Time.frameCount+2;}
            report.screenshots.Add(shot);
            if(isCounterflow&&stage!=null&&stage.stage==1)stage.counterflowScreenshotCaptured=true;
            Debug.Log("RUSH_JOURNEY_CAPTURE "+JsonUtility.ToJson(shot));yield return null;
        }
        void CompareRoutes()
        {
            if(report.runs.Count<2)return;
            RunEvidence risk=report.runs[0],safe=report.runs[1];
            bool different=false;
            for(int i=0;i<Mathf.Min(risk.stages.Count,safe.stages.Count);i++)
            {
                var a=risk.stages[i];var b=safe.stages[i];
                different|=Mathf.Abs(a.plannedDistance-b.plannedDistance)>3f;
                CheckThat("Stage "+(i+1)+": both author routes traversed",a.visitedWaypoints>=a.waypointCount-1&&b.visitedWaypoints>=b.waypointCount-1,
                    "risk waypoints="+a.visitedWaypoints+"/"+a.waypointCount+" safe="+b.visitedWaypoints+"/"+b.waypointCount+
                    " distance="+a.actualDistance+"/"+b.actualDistance+" seconds="+a.gameSeconds+"/"+b.gameSeconds);
            }
            CheckThat("Safe route is a measured alternate author route",different,
                "risk/safe metres="+risk.actualDistance+"/"+safe.actualDistance+" game seconds="+risk.totalSeconds+"/"+safe.totalSeconds);
        }
        void Coverage()
        {
            report.coverageGaps.Clear();
            if(report.runs.Count!=3)report.coverageGaps.Add("Three complete natural journeys NOT_OBSERVED.");
            RunEvidence risk=report.runs.Find(r=>r.name=="risk"),safe=report.runs.Find(r=>r.name=="safe"),charge=report.runs.Find(r=>r.name=="charge");
            foreach(var r in report.runs)if(!r.reachedResults||r.transitions!=3||r.stages.Count!=3)report.coverageGaps.Add(r.name+": complete three-stage journey NOT_OBSERVED.");
            if(risk==null||risk.eventBlocks==0)report.coverageGaps.Add("Risk: actual Shift event block NOT_OBSERVED.");
            StageEvidence riskStation=risk==null?null:risk.stages.Find(s=>s.stage==1);
            if(riskStation==null||riskStation.counterflowDistinctConfirmed==0||!riskStation.counterflowScreenshotCaptured)
                report.coverageGaps.Add("Risk: identified alighting NPC actually walking toward -z within 12m and viewport, confirmed continuously and captured in the same frame NOT_OBSERVED.");
            if(charge==null||charge.eventHits==0||charge.eventHitsObservedAirborne==0||charge.eventHitsRecovered==0)
                report.coverageGaps.Add("Charge: actual environmental launch, visible-height sample and recovery NOT_OBSERVED.");
            if(charge==null||charge.breaks==0)report.coverageGaps.Add("Charge: real dash parcel destruction NOT_OBSERVED.");
            if(charge==null||!charge.lateWaitApplied||charge.deliberateLateWaitSeconds<=0||!charge.lateResult)
                report.coverageGaps.Add("Charge: genuine delayed but completed result NOT_OBSERVED.");
            if(risk!=null&&charge!=null)
                CheckThat("Defensive and undefended event outcomes differ",risk.eventBlocks>0&&charge.eventHits>0&&
                    risk.eventBlocks>charge.eventBlocks&&risk.eventHits<charge.eventHits,
                    "risk hit/block="+risk.eventHits+"/"+risk.eventBlocks+" charge="+charge.eventHits+"/"+charge.eventBlocks+
                    "; natural trajectories differ; not a controlled causal estimate");
            if(safe==null||!safe.reachedResults)report.coverageGaps.Add("Safe: alternate route completion NOT_OBSERVED.");
            if(!report.warningPauseCovered||!report.warningReissuedCovered)report.coverageGaps.Add("Event pause cancellation and fresh warning NOT_OBSERVED.");
            if(!report.restartEventCovered)report.coverageGaps.Add("Restart during an actual event warning NOT_OBSERVED.");
            report.coverageComplete=report.coverageGaps.Count==0;
        }
        void Finish()
        {
            if(finished)return;ClearInput();
            RushPerformance.SetQaPhase("journey-finish");RushPerformance.Flush();
            report.perf.diagnosticsSaved=!RushPerformance.IsEnabled||RushPerformance.SaveSucceeded;
            CheckThat("Performance diagnostics saved",report.perf.diagnosticsSaved,"enabled="+RushPerformance.IsEnabled);
            report.elapsedRealSeconds=Time.realtimeSinceStartup-began;report.passed=report.failureCount==0;
            report.readyForAcceptance=report.passed&&report.coverageComplete;finished=true;
            File.WriteAllText(Path.Combine(output,"journey-report.json"),JsonUtility.ToJson(report,true));
            Debug.Log("RUSH_JOURNEY_QA_FINISHED passed="+report.passed+" coverageComplete="+report.coverageComplete+
                " readyForAcceptance="+report.readyForAcceptance+" failures="+report.failureCount+" output="+output);
            Application.Quit(report.readyForAcceptance?0:1);
        }
    }
}

