using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace EarlyRush
{
    /// <summary>
    /// V3 normal-route acceptance. Impact fixtures are run separately with --impact-qa.
    /// Movement uses the ordinary game input channel, never position/time overrides.
    /// UI actions use existing methods; this is not OS keyboard automation.
    /// </summary>
    public class RushQA : MonoBehaviour
    {
        [Serializable] public class Check
        {
            public string name, evidence;
            public bool passed;
        }
        [Serializable] public class GroundPileMemberEvidence
        {
            public int slot, generation, combatPhase, attackId;
            public Vector2 position, separationOffset;
            public float separationOffsetLength, correctionBudgetLeft;
            public bool active, ground, airborne, recovering;
        }
        [Serializable] public class GroundPileSnapshot
        {
            public string run;
            public int stage, frame, centerSlot, centerGeneration;
            public int allCount, groundCount, airborneCount, recoveringCount;
            public float realSinceStartup, totalSeconds, stageSeconds;
            public Vector2 centerPosition, playerPosition;
            public bool combatProbeAligned;
            public List<GroundPileMemberEvidence> members = new List<GroundPileMemberEvidence>();
        }
        [Serializable] public class StageEvidence
        {
            public int stage, collisions, offscreenCrowdSamples;
            public int crowdPopulation, activePeak, visiblePeak, progressedSlots;
            public int arrivals, goalCrossings, recycles, visibleRecycles, qaVisibleRecycleEvents;
            public int maxNearCoincidentCount, pileSamples;
            public int maxGroundNearCoincidentCount, maxNearbyAirborneCount, maxNearbyRecoveringCount;
            public int allPilePeakGround, allPilePeakAirborne, allPilePeakRecovering;
            public int allActivePileSamples, groundPileSamples, nonGroundInPileSamples;
            public float longestAllPileSampleSpan, longestGroundPileSampleSpan, longestNonGroundPileSampleSpan;
            public bool pileProbeAlignment = true;
            // Diagnostic snapshots only; these fields never determine a check result.
            public GroundPileSnapshot firstGroundPile, peakGroundPile;
            public float activeSamples, visibleSamples, meanActiveCrowd, meanVisibleCrowd;
            public float crowdForwardDistance, crowdBackwardDistance;
            public float traversalFrameSeconds, meanTraversalFrameMilliseconds, worstTraversalFrameMilliseconds;
            public float sampledStumbleSeconds, longestSampledStumbleSeconds;
            public int sampledStumbleEntries;
            public float maxCrowdSpeed;
            public bool crowdFiniteAndInBounds = true;
            public float mapWidth, mapDepth, plannedRouteLength;
            public float actualDistance, splitSeconds, stoppedSeconds;
            public float commandedWaitSeconds, bracedSeconds, dashedSeconds;
            public float maxSpeed, longestStallSeconds;
            public float cameraSpanX, cameraSpanZ;
            public float minViewportX = 1, maxViewportX;
            public float minViewportY = 1, maxViewportY;
            public int visibleWarningPeak, visibleRushPeak, visibleThreatPeak, warningSamples, rushSamples, playerAirSamples, maxNpcBurstHistory;
            public float visibleWarningTotal, visibleRushTotal, maxPlayerAirHeight;
            public int combatNpcLaunchesCumulative, combatPlayerLaunchesCumulative, braceBlocksCumulative;
            public int samples;
            public bool inBounds = true, clearOfWalls = true, playerVisible = true;
        }
        [Serializable] public class RunEvidence
        {
            public string name, route;
            public bool includesDeliberateDoorProbe;
            public float totalSeconds, totalStumbleSeconds, longestStumbleSeconds;
            public int stumbleTriggers;
            public int impactLaunches, chainLaunches, maxDashTargets, maxChainDepth;
            public int npcCombatLaunches, playerCombatLaunches, braceBlocks;
            public int collisions, transitions;
            public bool reachedResults;
            public float[] splits;
            public int[] splitCollisions;
            public List<StageEvidence> stages = new List<StageEvidence>();
        }
        [Serializable] public class Report
        {
            public string utc, engine, device, graphicsApi;
            public string schemaVersion = "v4-combat-routes-1";
            public string pileSamplingScope = "All-active 2D clusters remain diagnostics. The unchanged <=3 pile limit applies to active non-airborne, non-recovering colliders. Counts are sampled twice per second; persistence is the span of consecutive positive samples for a stable slot/generation, not continuous observation.";
            public string performanceScope = "Overall frame statistics include the late wait; per-stage traversal timing excludes deliberate pauses.";
            public string driver =
                "Windows Player QAMove/QADash/QABrace input driver. " +
                "Menu/pause/restart use public UI actions. No teleport, time override, " +
                "scene skip, OS keyboard automation or human playtest.";
            public bool passed;
            public int failureCount, sampledFrames;
            public float elapsedRealSeconds, meanFrameMilliseconds, worstFrameMilliseconds;
            public List<Check> checks = new List<Check>();
            public List<RunEvidence> runs = new List<RunEvidence>();
            public List<string> screenshots = new List<string>();
        }
        [Serializable] public class Status
        {
            public string run, state;
            public int stage, waypoint;
            public float time, realSeconds;
            public Vector2 position;
        }

        RushGame game;
        readonly Report report = new Report();
        string output, currentRun = "controls";
        float began, frameTotal, frameWorst, lastStatus;
        int frames, currentWaypoint;
        bool finished;
        Vector2[] previousCrowd;
        float previousCrowdAt, lastPileSampleAt;
        float[] allPileBeganAt, groundPileBeganAt, transientPileBeganAt;
        int[] pileGenerations;
        int[] previousGenerations;
        bool[] previousActive, previousVisible;
        float[] previousProgress, observedForwardBySlot;
        const float Margin = .46f;
        const float Timeout = 650f;

        void Start()
        {
            game = GetComponent<RushGame>();
            output = Path.Combine(Path.GetDirectoryName(Application.dataPath), "QA");
            foreach (string arg in Environment.GetCommandLineArgs())
                if (arg.StartsWith("--qa-output=", StringComparison.Ordinal))
                    output = arg.Substring("--qa-output=".Length);
            Directory.CreateDirectory(output);
            began = Time.realtimeSinceStartup;
            report.utc = DateTime.UtcNow.ToString("o");
            report.engine = Application.unityVersion;
            report.device = SystemInfo.graphicsDeviceName;
            report.graphicsApi = SystemInfo.graphicsDeviceType.ToString();
            game.QAInput = true;
            Application.logMessageReceived += OnLog;
            StartCoroutine(Suite());
        }

        void OnDestroy() { Application.logMessageReceived -= OnLog; }

        void OnLog(string message, string trace, LogType type)
        {
            if (!finished && (type == LogType.Exception || type == LogType.Error))
                CheckThat("No runtime error", false, message + "\n" + trace);
        }

        void Update()
        {
            if (finished || game == null) return;
            if (game.State == RushState.Playing && Time.unscaledDeltaTime > 0)
            {
                frames++;
                frameTotal += Time.unscaledDeltaTime;
                frameWorst = Mathf.Max(frameWorst, Time.unscaledDeltaTime);
            }
            if (Time.realtimeSinceStartup - lastStatus > 2f)
            {
                lastStatus = Time.realtimeSinceStartup;
                var status = new Status
                {
                    run = currentRun, state = game.State.ToString(),
                    stage = game.Stage, waypoint = currentWaypoint,
                    time = game.TotalTime, realSeconds = Time.realtimeSinceStartup - began,
                    position = game.Position
                };
                File.WriteAllText(Path.Combine(output, "qa-status.json"),
                    JsonUtility.ToJson(status, true));
            }
            if (Time.realtimeSinceStartup - began > Timeout)
            {
                CheckThat("Suite completes within 650 seconds", false,
                    game.State + " stage=" + game.Stage + " position=" + game.Position);
                Finish();
            }
        }

        void CheckThat(string name, bool passed, string evidence)
        {
            if (finished) return;
            report.checks.Add(new Check { name = name, passed = passed, evidence = evidence });
            if (!passed) report.failureCount++;
            Debug.Log("RUSH_QA_CHECK " + (passed ? "PASS " : "FAIL ") + name + " | " + evidence);
        }

        void Neutral()
        {
            game.QAMove = Vector2.zero;
            game.QADash = false;
            game.QABrace = false;
        }

        IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!finished && Time.realtimeSinceStartup < end) yield return null;
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(output, name + ".png");
            Debug.Log("RUSH_QA_CAPTURE_REQUEST_BEGIN name=" + name + " realtime=" + Time.realtimeSinceStartup + " frame=" + Time.frameCount);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("RUSH_QA_CAPTURE_REQUEST_END name=" + name + " realtime=" + Time.realtimeSinceStartup + " frame=" + Time.frameCount);
            report.screenshots.Add(path);
            yield return null;
        }

        IEnumerator Suite()
        {
            yield return Wait(.5f);
            CheckThat("Opens to menu", game.State == RushState.Menu, game.State.ToString());
            yield return Shot("01-menu");
            game.StartRun();
            yield return Wait(.15f);
            CheckThat("Start opens tutorial", game.State == RushState.Tutorial, game.State.ToString());
            yield return Shot("02-tutorial");
            game.BeginPlay(); Neutral();
            yield return Wait(.2f);

            game.QAMove = Vector2.right;
            yield return Wait(.6f);
            float cardinal = game.Velocity.magnitude;
            game.Restart(); Neutral();
            yield return Wait(.2f);
            game.QAMove = Vector2.one;
            yield return Wait(.6f);
            float diagonal = game.Velocity.magnitude;
            CheckThat("Diagonal movement is normalized",
                Mathf.Abs(cardinal - diagonal) < .15f && diagonal <= 6.3f,
                "cardinal=" + cardinal + " diagonal=" + diagonal);

            game.Restart(); Neutral();
            yield return Wait(.2f);
            game.QAMove = Vector2.up; game.QABrace = true; game.QADash = true;
            yield return Wait(.25f);
            CheckThat("Brace wins simultaneous dash",
                game.IsBracing && !game.IsDashing && game.Velocity.magnitude < 3.3f,
                "brace=" + game.IsBracing + " dash=" + game.IsDashing);
            Neutral();
            yield return Wait(.2f);
            game.QAMove = Vector2.up; game.QADash = true;
            yield return Wait(.07f);
            CheckThat("Dash accelerates and consumes cooldown",
                game.IsDashing && game.Velocity.magnitude > 10 && game.DashCooldown01 < .3f,
                "speed=" + game.Velocity.magnitude + " ready=" + game.DashCooldown01);
            game.QABrace = true;
            yield return Wait(.1f);
            CheckThat("Brace cancels dash", game.IsBracing && !game.IsDashing,
                "brace=" + game.IsBracing + " dash=" + game.IsDashing);
            Neutral();
            yield return PauseCheck("controls");

            for (int i = 0; i < 3; i++)
            {
                game.Restart(); Neutral();
                yield return Wait(.15f);
                bool clean = game.Stage == 0 && game.Collisions == 0 &&
                    game.TransitionCount == 0 && game.Velocity.sqrMagnitude < .001f &&
                    game.TotalTime < .4f && !game.IsDashing && !game.IsBracing;
                for (int j = 0; j < 3; j++)
                    clean &= game.SplitTimes[j] == 0 && game.SplitCollisions[j] == 0;
                CheckThat("Restart clears state " + (i + 1), clean,
                    "stage=" + game.Stage + " time=" + game.TotalTime + " position=" + game.Position);
                CheckImpactReset("Restart " + (i + 1));
            }

            game.Restart(); Neutral();
            yield return Wait(.2f);
            yield return Traverse("risk", false);
            CheckThat("Risk run finishes on time",
                game.State == RushState.Results && game.TotalTime <= game.TimeLimit,
                "time=" + game.TotalTime + " limit=" + game.TimeLimit);
            yield return Shot("risk-results");

            game.Restart(); Neutral();
            yield return Wait(.2f);
            yield return Traverse("safe", true);
            yield return Shot("safe-results");
            CompareRoutes();

            game.Restart(); Neutral();
            yield return Wait(.2f);
            yield return Traverse("charge", false);
            yield return Shot("charge-results");
            game.Restart(); Neutral();
            CheckImpactReset("late: fresh restart before deadline wait");
            bool lateWaitStayedInFirstStage = game.Stage == 0 && game.TransitionCount == 0;
            yield return Wait(.2f);
            currentRun = "late-wait";
            while (!finished && game.TotalTime <= game.TimeLimit + .3f)
            {
                lateWaitStayedInFirstStage &= game.Stage == 0 && game.TransitionCount == 0;
                yield return null;
            }
            CheckThat("Deadline wait stays in first stage without transitions",
                lateWaitStayedInFirstStage && game.Stage == 0 && game.TransitionCount == 0,
                "stage=" + game.Stage + " transitions=" + game.TransitionCount + " time=" + game.TotalTime);
            CheckThat("Deadline does not stop play",
                game.State == RushState.Playing && game.TotalTime > game.TimeLimit,
                "state=" + game.State + " time=" + game.TotalTime);
            yield return Shot("late-still-playing");
            yield return Traverse("late", false);
            CheckThat("Late player can still finish all stages",
                game.State == RushState.Results && game.TotalTime > game.TimeLimit,
                "time=" + game.TotalTime + " transitions=" + game.TransitionCount);
            yield return Shot("late-results");

            game.ToMenu(); Neutral();
            yield return Wait(.2f);
            CheckThat("Return to menu clears run",
                game.State == RushState.Menu && game.TotalTime == 0 &&
                game.Collisions == 0 && game.TransitionCount == 0,
                game.State + " time=" + game.TotalTime);
            bool originalMute = game.Muted;
            game.ToggleSound();
            CheckThat("Sound toggle changes state", game.Muted != originalMute, "muted=" + game.Muted);
            game.ToggleSound();
            CheckThat("Sound toggle restores state", game.Muted == originalMute, "muted=" + game.Muted);
            yield return Wait(.3f);
            Finish();
        }

        void CheckImpactReset(string label)
        {
            bool clean = game.ImpactFreezeRemaining < .00001f && game.ActiveImpactFxCount == 0 &&
                Vector3.Distance(game.PlayerVisualScale, Vector3.one) < .001f &&
                Vector3.Distance(game.PlayerRootScale, Vector3.one) < .001f;
            var playerCombat = game.PlayerCombatProbeSnapshot();
            CheckThat(label + ": combat state clears on scene/reset",
                !playerCombat.airborne && !playerCombat.recovering && playerCombat.height == 0 &&
                playerCombat.protectionLeft == 0 && game.CombatWarningCount == 0 && game.CombatRushCount == 0 && game.NpcBurstCount == 0,
                "player height/protection=" + playerCombat.height + "/" + playerCombat.protectionLeft +
                " threats=" + game.CombatWarningCount + "/" + game.CombatRushCount + " burst history=" + game.NpcBurstCount);
            var impacts = game.ImpactProbeSnapshot();
            foreach (var p in impacts)
                clean &= !p.airborne && !p.recovering && Mathf.Abs(p.height) < .001f &&
                    p.launchCount == 0 && p.landCount == 0 && Mathf.Abs(p.spinDegrees) < .001f &&
                    Vector3.Distance(p.visualScale, Vector3.one) < .001f &&
                    Vector3.Distance(p.rootScale, Vector3.one) < .001f;
            CheckThat(label + ": impact state clears on reset", clean,
                "freeze=" + game.ImpactFreezeRemaining + " FX=" + game.ActiveImpactFxCount +
                " NPC slots=" + impacts.Length);
        }

        IEnumerator PauseCheck(string label)
        {
            Neutral();
            game.TogglePause();
            yield return Wait(.15f);
            float total = game.TotalTime, split = game.StageTime;
            Vector2 position = game.Position;
            Vector3 camera = game.GameCamera.transform.position;
            var crowd = game.CrowdProbeSnapshot();
            int arrivals = game.CrowdArrivals, recycles = game.CrowdRecycles;
            float forward = game.CrowdForwardDistance;
            yield return Wait(.4f);
            var after = game.CrowdProbeSnapshot();
            bool crowdFrozen = crowd.Length == after.Length &&
                arrivals == game.CrowdArrivals && recycles == game.CrowdRecycles &&
                Mathf.Abs(forward - game.CrowdForwardDistance) < .001f;
            for (int i = 0; i < Mathf.Min(crowd.Length, after.Length); i++)
                crowdFrozen &= Vector2.Distance(crowd[i].position, after[i].position) < .001f &&
                    crowd[i].generation == after[i].generation && crowd[i].active == after[i].active &&
                    Mathf.Abs(crowd[i].progress - after[i].progress) < .001f;
            CheckThat(label + ": pause freezes simulation and camera",
                game.State == RushState.Paused &&
                Mathf.Abs(total - game.TotalTime) < .001f &&
                Mathf.Abs(split - game.StageTime) < .001f &&
                Vector2.Distance(position, game.Position) < .001f &&
                Vector3.Distance(camera, game.GameCamera.transform.position) < .001f && crowdFrozen,
                "timer delta=" + (game.TotalTime - total) +
                " camera delta=" + Vector3.Distance(camera, game.GameCamera.transform.position) +
                " crowd frozen=" + crowdFrozen);
            game.TogglePause();
            game.QABrace = true; game.QADash = true;
            yield return Wait(.1f);
            CheckThat(label + ": resume suppresses held actions",
                game.InputLocked && !game.IsBracing && !game.IsDashing, "locked=" + game.InputLocked);
            Neutral();
            yield return Wait(.15f);
            CheckThat(label + ": releasing rearms actions", !game.InputLocked, "locked=" + game.InputLocked);
        }

        IEnumerator Traverse(string name, bool safe)
        {
            currentRun = name;
            var run = new RunEvidence { name = name, route = safe ? "safe" : "risk", includesDeliberateDoorProbe = name == "late" };
            report.runs.Add(run);
            int previousStage = -1, index = 0;
            List<Vector2> route = null;
            StageEvidence metric = null;
            Vector2 previousPosition = game.Position, progressPoint = game.Position;
            Vector3 cameraMin = Vector3.zero, cameraMax = Vector3.zero;
            float started = Time.realtimeSinceStartup;
            float lastProgress = started, lastDash = -10;
            bool skilled = name != "charge";
            float stumbleSeen = -1, recoverUntil = -1, passingUntil = -1, nextPassingChoice = -1;
            float retreatUntil = -1, nextRetreat = -1, sampledStumble = 0;
            bool previouslyStumbling = false, approachShot = false, entryShot = false;
            Vector2 passingTarget = Vector2.zero, retreatDirection = Vector2.zero;
            bool midShot = false, doorAttempted = false, doorContact = false;
            bool doorRetreated = false, doorCrossed = false;
            int doorPhase = 0, doorHitBaseline = 0;
            float doorPhaseBegan = 0, retreatStartZ = 0, closestJamb = float.MaxValue;

            while (!finished && game.State != RushState.Results &&
                   Time.realtimeSinceStartup - started < 170f)
            {
                if (game.State != RushState.Playing)
                {
                    Neutral();
                    yield return null;
                    continue;
                }
                if (game.Stage != previousStage)
                {
                    bool existingLateStage = name == "late" && previousStage < 0;
                    if (metric != null) FinalizeStage(metric, cameraMin, cameraMax, name);
                    previousStage = game.Stage;
                    Debug.Log("RUSH_QA_STAGE_OBSERVED stage=" + game.Stage + " realtime=" + Time.realtimeSinceStartup + " frame=" + Time.frameCount);
                    Neutral();
                    yield return Wait(.15f);
                    if (!existingLateStage)
                    {
                        CheckThat(name + ": stage " + game.Stage + " resets momentum",
                            game.Velocity.sqrMagnitude < .001f && game.DashCooldown01 > .99f,
                            "speed=" + game.Velocity.magnitude + " ready=" + game.DashCooldown01);
                        CheckImpactReset(name + ": stage " + game.Stage);
                    }
                    else
                        Debug.Log("RUSH_QA_EXISTING_STAGE_AFTER_WAIT stage=" + game.Stage + " time=" + game.TotalTime +
                            " threats=" + game.CombatWarningCount + "/" + game.CombatRushCount + " burstHistory=" + game.NpcBurstCount);
                    route = new List<Vector2>(safe ? game.World.safeRoute : game.World.riskRoute);
                    if (route.Count == 0)
                    {
                        CheckThat(name + ": route exists", false, "stage=" + game.Stage);
                        break;
                    }
                    metric = new StageEvidence
                    {
                        stage = game.Stage,
                        mapWidth = game.World.boundsMax.x - game.World.boundsMin.x,
                        mapDepth = game.World.boundsMax.y - game.World.boundsMin.y
                    };
                    Vector2 last = game.Position;
                    bool segmentsClear = true;
                    int segmentIndex = 0;
                    string blockedSegments = "";
                    foreach (Vector2 point in route)
                    {
                        metric.plannedRouteLength += Vector2.Distance(last, point);
                        if (!LineClear(last, point, game.World))
                        {
                            segmentsClear = false;
                            blockedSegments += segmentIndex + " ";
                        }
                        segmentIndex++;
                        last = point;
                    }
                    CheckThat(name + ": stage " + game.Stage + " waypoint segments clear static walls",
                        segmentsClear, "margin=" + Margin + " blocked segments=" + blockedSegments);
                    CheckNpcRoutes(name);
                    CheckThat(name + ": destination has a usable arrival pad",
                        game.World.goalHalfSize.x >= 2.5f,
                        "goal half width=" + game.World.goalHalfSize.x);
                    run.stages.Add(metric);
                    CheckThat(name + ": stage " + game.Stage + " is a large map",
                        metric.mapDepth >= 75f && metric.mapWidth >= 30f,
                        metric.mapWidth + " x " + metric.mapDepth + " metres");
                    index = 0; midShot = false; approachShot = false; entryShot = false;
                    stumbleSeen = -1; recoverUntil = -1; passingUntil = -1;
                    nextPassingChoice = -1; retreatUntil = -1; nextRetreat = -1;
                    sampledStumble = 0; previouslyStumbling = false;
                    previousCrowd = null;
                    lastPileSampleAt = -100f;
                    previousPosition = progressPoint = game.Position;
                    lastProgress = Time.realtimeSinceStartup;
                    cameraMin = cameraMax = game.GameCamera.transform.position;
                    yield return Shot(name + "-stage-" + (game.Stage + 1) + "-start");
                    // Three full runs repeat this check in every scene.
                    yield return PauseCheck(name + " stage " + (game.Stage + 1));
                    // Deliberate pause time is not a movement stall.
                    previousPosition = progressPoint = game.Position;
                    lastProgress = Time.realtimeSinceStartup;
                    currentWaypoint = 0;
                }

                Vector2 p = game.Position;
                float dt = Time.unscaledDeltaTime;
                metric.samples++;
                metric.traversalFrameSeconds += dt;
                metric.worstTraversalFrameMilliseconds =
                    Mathf.Max(metric.worstTraversalFrameMilliseconds, dt * 1000f);
                metric.actualDistance += Vector2.Distance(previousPosition, p);
                previousPosition = p;
                metric.maxSpeed = Mathf.Max(metric.maxSpeed, game.Velocity.magnitude);
                if (game.Velocity.magnitude < .35f) metric.stoppedSeconds += dt;
                if (game.QAMove.sqrMagnitude < .01f) metric.commandedWaitSeconds += dt;
                if (game.IsBracing) metric.bracedSeconds += dt;
                if (game.IsDashing) metric.dashedSeconds += dt;
                if (game.IsStumbling)
                {
                    metric.sampledStumbleSeconds += dt;
                    sampledStumble += dt;
                    if (!previouslyStumbling) metric.sampledStumbleEntries++;
                    metric.longestSampledStumbleSeconds =
                        Mathf.Max(metric.longestSampledStumbleSeconds, sampledStumble);
                }
                else sampledStumble = 0;
                previouslyStumbling = game.IsStumbling;
                SampleGeometry(metric, p);
                Vector3 cam = game.GameCamera.transform.position;
                cameraMin = Vector3.Min(cameraMin, cam);
                cameraMax = Vector3.Max(cameraMax, cam);
                if (Vector2.Distance(p, progressPoint) > .6f)
                {
                    progressPoint = p;
                    lastProgress = Time.realtimeSinceStartup;
                }
                float stalled = Time.realtimeSinceStartup - lastProgress;
                metric.longestStallSeconds = Mathf.Max(metric.longestStallSeconds, stalled);
                if (stalled > 8f)
                {
                    CheckThat(name + ": no permanent stall", false,
                        "stage=" + game.Stage + " position=" + p + " waypoint=" + index);
                    break;
                }
                if (!midShot && game.StageProgress01 > .46f)
                {
                    midShot = true;
                    yield return Shot(name + "-stage-" + (game.Stage + 1) + "-middle");
                }
                if (!approachShot && p.y >= 82f)
                {
                    approachShot = true;
                    yield return Shot(name + "-stage-" + (game.Stage + 1) + "-approach-z82");
                }
                if (!entryShot && p.y >= 90f)
                {
                    entryShot = true;
                    yield return Shot(name + "-stage-" + (game.Stage + 1) + "-entry-z90");
                }
                if (name == "late" && game.Stage == 1 && !doorAttempted &&
                    p.y >= 85.5f && p.y < 87f && Mathf.Abs(p.x) < 1.4f)
                {
                    // Deliberate input-only contact with the right door jamb.
                    // A state machine keeps every probe frame in the metrics above.
                    doorAttempted = true;
                    doorPhase = 1;
                    doorPhaseBegan = Time.realtimeSinceStartup;
                    doorHitBaseline = game.Collisions;
                    closestJamb = float.MaxValue;
                }
                if (doorPhase == 1)
                {
                    Vector2 jamb = new Vector2(1.9f, 89f);
                    foreach (Rect wall in game.World.obstacles)
                    {
                        Vector2 closest = new Vector2(Mathf.Clamp(p.x, wall.xMin, wall.xMax),
                            Mathf.Clamp(p.y, wall.yMin, wall.yMax));
                        float clearance = Vector2.Distance(p, closest);
                        if (p.x > 1.05f && p.x < 3.5f && p.y > 87.8f && p.y < 89.7f)
                        {
                            closestJamb = Mathf.Min(closestJamb, clearance);
                            doorContact |= clearance <= .45f && game.Collisions > doorHitBaseline;
                        }
                    }
                    game.QAMove = (jamb - p).normalized;
                    bool nearJamb = Vector2.Distance(jamb, p) < 1.25f;
                    game.QABrace = !nearJamb;
                    game.QADash = nearJamb && game.DashCooldown01 > .99f;
                    if (doorContact || Time.realtimeSinceStartup - doorPhaseBegan >= 3f)
                    {
                        CheckThat("Narrow-door probe makes static contact", doorContact,
                            "closest clearance=" + closestJamb +
                            " collision increase=" + (game.Collisions - doorHitBaseline));
                        doorPhase = 2;
                        doorPhaseBegan = Time.realtimeSinceStartup;
                        retreatStartZ = p.y;
                        game.QADash = false;
                    }
                    yield return null;
                    continue;
                }
                if (doorPhase == 2)
                {
                    game.QAMove = Vector2.down;
                    game.QABrace = true;
                    game.QADash = false;
                    if (Time.realtimeSinceStartup - doorPhaseBegan >= .7f)
                    {
                        doorRetreated = retreatStartZ - p.y > .7f;
                        CheckThat("Retreat from contacted door jamb", doorRetreated,
                            "before z=" + retreatStartZ + " after z=" + p.y);
                        doorPhase = 0;
                        progressPoint = p;
                        lastProgress = Time.realtimeSinceStartup;
                    }
                    yield return null;
                    continue;
                }
                if (name == "late" && game.Stage == 1 && doorRetreated && p.y > 91f)
                    doorCrossed = true;
                while (index < route.Count - 1 && Vector2.Distance(p, route[index]) < .75f)
                    index++;
                currentWaypoint = index;
                Vector2 delta = route[index] - p;
                Vector2 direction = delta.sqrMagnitude > .01f ? delta.normalized : Vector2.zero;
                // Brake near corners. No waypoint skipping: safe detours remain measurable.
                Vector2 aim = delta - game.Velocity * .10f;
                Vector2 desired = aim.sqrMagnitude > .01f ? aim.normalized : direction;
                float throttle = Mathf.Clamp(delta.magnitude / 1.6f, .3f, 1f);
                game.QAMove = desired * throttle;
                game.QABrace = false; game.QADash = false;
                float now = Time.realtimeSinceStartup;
                float ahead = NearestCrowdAhead(p, direction);
                if (skilled)
                {
                    if (game.IsStumbling)
                    {
                        if (stumbleSeen < 0) stumbleSeen = now;
                        if (now - stumbleSeen >= .12f)
                            recoverUntil = Mathf.Max(recoverUntil, now + .2f);
                    }
                    else stumbleSeen = -1;
                    Vector2 segmentStart = index > 0 ? route[index - 1] :
                        new Vector2(game.World.spawn.x, game.World.spawn.z);
                    if (now >= nextPassingChoice && ahead < 3.5f && delta.magnitude > 3f)
                    {
                        nextPassingChoice = now + .3f;
                        if (TryLocalPass(p, route[index], segmentStart, game.World, out Vector2 choice))
                        { passingTarget = choice; passingUntil = now + .3f; }
                        else passingUntil = -1;
                    }
                    if (now < passingUntil && Vector2.Distance(p, passingTarget) > .4f &&
                        LineClear(p, passingTarget, game.World))
                    {
                        direction = (passingTarget - p).normalized;
                        game.QAMove = direction * throttle;
                        ahead = NearestCrowdAhead(p, direction);
                    }
                    // Both routes use the same local controls; the safe route is a real detour.
                    // No fixed .65s stop on every encounter with a forward-moving commuter.
                    game.QABrace = !game.IsStumbling && ahead < 1.25f;
                }
                if (stalled > 1.3f) game.QABrace = true;
                bool clearDash = delta.magnitude > 5f &&
                    LineClear(p, p + direction * 3.2f, game.World);
                bool readyDecision = !skilled ||
                    (!game.IsStumbling && now >= recoverUntil && ahead > 3.2f);
                if (!game.QABrace && clearDash && readyDecision &&
                    game.DashCooldown01 > .99f && now - lastDash > 1.3f)
                { game.QADash = true; lastDash = now; }

                // Charge retains the old risk driver's exact stalled-side-step fallback.
                if (stalled > 2f)
                {
                    Vector2 side = new Vector2(-direction.y, direction.x);
                    if (((int)(stalled * 2f) & 1) == 0) side = -side;
                    Vector2 escape = (direction + side * .9f).normalized;
                    if (LineClear(p, p + escape * .8f, game.World)) game.QAMove = escape;
                    game.QABrace = true; game.QADash = false;
                }
                if (skilled)
                {
                    if (stalled > 1.5f && now >= nextRetreat && now >= passingUntil &&
                        ahead < 1.5f && LineClear(p, p - direction * .65f, game.World))
                    {
                        retreatDirection = -direction;
                        retreatUntil = now + .3f;
                        nextRetreat = now + 2f;
                    }
                    if (now < retreatUntil)
                    { game.QAMove = retreatDirection; game.QABrace = true; game.QADash = false; }
                    if (now < recoverUntil)
                    { game.QABrace = true; game.QADash = false; }
                    else if (game.IsStumbling && stumbleSeen >= 0 && now - stumbleSeen < .12f)
                    { game.QABrace = false; game.QADash = false; }
                }
                yield return null;
            }
            Neutral();
            if (metric != null) FinalizeStage(metric, cameraMin, cameraMax, name);
            run.totalSeconds = game.TotalTime;
            run.totalStumbleSeconds = game.TotalStumbleTime;
            run.longestStumbleSeconds = game.LongestStumbleTime;
            run.stumbleTriggers = game.StumbleTriggers;
            run.npcCombatLaunches = game.NpcToNpcLaunches;
            run.playerCombatLaunches = game.NpcToPlayerLaunches;
            run.braceBlocks = game.BraceBlocks;
            if (name == "charge")
            {
                int visibleWarningSamples = 0, visibleRushSamples = 0;
                foreach (var stage in run.stages)
                {
                    visibleWarningSamples += stage.warningSamples;
                    visibleRushSamples += stage.rushSamples;
                }
                CheckThat("Charge route observes visible NPC warnings, rushes and NPC-to-NPC impacts",
                    visibleWarningSamples > 0 && visibleRushSamples > 0 && run.npcCombatLaunches > 0,
                    "visible warning/rush samples=" + visibleWarningSamples + "/" + visibleRushSamples +
                    " NPC-owned launches=" + run.npcCombatLaunches + " player launches=" + run.playerCombatLaunches +
                    " brace blocks=" + run.braceBlocks + " (player contact is diagnostic, not required)");
            }
            run.impactLaunches = game.TotalLaunches;
            run.chainLaunches = game.ChainLaunches;
            run.maxDashTargets = game.MaxObservedDashTargets;
            run.maxChainDepth = game.MaxObservedChainDepth;
            CheckThat(name + ": ordinary-route impact chains stay bounded",
                run.maxDashTargets <= 6 && run.maxChainDepth <= 2,
                "launches=" + run.impactLaunches + " secondary=" + run.chainLaunches +
                " max targets=" + run.maxDashTargets + " depth=" + run.maxChainDepth);
            if (name == "charge")
                CheckThat("Charge route exercises real dash launches", run.impactLaunches - run.npcCombatLaunches > 0,
                    "all launches=" + run.impactLaunches + " NPC-owned=" + run.npcCombatLaunches + " secondary=" + run.chainLaunches);
            CheckThat(name + ": individual stumble duration stays bounded",
                game.LongestStumbleTime <= .47f,
                "total seconds=" + game.TotalStumbleTime + " longest=" + game.LongestStumbleTime +
                " triggers=" + game.StumbleTriggers);
            run.collisions = game.Collisions;
            run.transitions = game.TransitionCount;
            run.reachedResults = game.State == RushState.Results;
            run.splits = (float[])game.SplitTimes.Clone();
            run.splitCollisions = (int[])game.SplitCollisions.Clone();
            CheckThat(name + ": three stages completed through input",
                run.reachedResults && run.transitions == 3 && run.stages.Count == 3,
                "state=" + game.State + " transitions=" + run.transitions);
            float splitSum = 0;
            int collisionSum = 0;
            foreach (float split in run.splits) splitSum += split;
            foreach (int hits in run.splitCollisions) collisionSum += hits;
            CheckThat(name + ": timer and collision splits reconcile",
                run.reachedResults && Mathf.Abs(splitSum - run.totalSeconds) < .1f &&
                collisionSum == run.collisions,
                "split time=" + splitSum + " total=" + run.totalSeconds +
                " split hits=" + collisionSum + " hits=" + run.collisions);
            if (name == "late")
                CheckThat("Narrow door crossed after jamb contact and retreat",
                    doorContact && doorRetreated && doorCrossed,
                    "contact=" + doorContact + " retreated=" + doorRetreated +
                    " crossed=" + doorCrossed);
        }

        static float TrackPileSpan(bool present, float now, ref float began)
        {
            if (!present) { began = -1; return 0; }
            if (began < 0) began = now;
            return now - began;
        }

        GroundPileSnapshot CaptureGroundPile(StageEvidence metric, int center, float sampleAt,
            Vector2 playerPosition, RushGame.CrowdProbe[] probes, RushGame.ImpactProbe[] impacts,
            RushGame.CombatProbe[] combat, int allCount, int groundCount, int airborneCount, int recoveringCount)
        {
            // Combat probes share stable slots but have no generation field. Validate
            // this synchronous snapshot before attaching phases; unknown is diagnostic only.
            bool combatAligned = combat.Length == probes.Length;
            if (combatAligned)
                for (int i = 0; i < probes.Length; i++)
                    combatAligned &= combat[i].active == probes[i].active &&
                        Vector2.Distance(combat[i].position, probes[i].position) < .001f;
            var snapshot = new GroundPileSnapshot
            {
                run = currentRun, stage = metric.stage, frame = Time.frameCount,
                realSinceStartup = sampleAt, totalSeconds = game.TotalTime, stageSeconds = game.StageTime,
                centerSlot = center, centerGeneration = probes[center].generation,
                centerPosition = probes[center].position, playerPosition = playerPosition,
                allCount = allCount, groundCount = groundCount,
                airborneCount = airborneCount, recoveringCount = recoveringCount,
                combatProbeAligned = combatAligned
            };
            for (int j = 0; j < probes.Length; j++)
            {
                if (!probes[j].active ||
                    (probes[center].position - probes[j].position).sqrMagnitude >= .25f * .25f) continue;
                snapshot.members.Add(new GroundPileMemberEvidence
                {
                    slot = j, generation = probes[j].generation, position = probes[j].position,
                    separationOffset = impacts[j].separationOffset,
                    separationOffsetLength = impacts[j].separationOffset.magnitude,
                    correctionBudgetLeft = impacts[j].correctionBudgetLeft,
                    active = probes[j].active, airborne = impacts[j].airborne, recovering = impacts[j].recovering,
                    ground = !impacts[j].airborne && !impacts[j].recovering,
                    combatPhase = combatAligned ? combat[j].phase : -1,
                    attackId = combatAligned ? combat[j].attackId : -1
                });
            }
            return snapshot;
        }

        void SampleGeometry(StageEvidence metric, Vector2 p)
        {
            var combat = game.CombatProbeSnapshot();
            var playerCombat = game.PlayerCombatProbeSnapshot();
            int warnings = 0, rushes = 0;
            foreach (var threat in combat)
                if (threat.active && threat.visible)
                { if (threat.phase == 1) warnings++; else if (threat.phase == 2) rushes++; }
            metric.visibleWarningPeak = Mathf.Max(metric.visibleWarningPeak, warnings);
            metric.visibleRushPeak = Mathf.Max(metric.visibleRushPeak, rushes);
            metric.visibleThreatPeak = Mathf.Max(metric.visibleThreatPeak, warnings + rushes);
            metric.maxNpcBurstHistory = Mathf.Max(metric.maxNpcBurstHistory, game.NpcBurstCount);
            metric.visibleWarningTotal += warnings; metric.visibleRushTotal += rushes;
            if (warnings > 0) metric.warningSamples++;
            if (rushes > 0) metric.rushSamples++;
            if (playerCombat.airborne) metric.playerAirSamples++;
            metric.maxPlayerAirHeight = Mathf.Max(metric.maxPlayerAirHeight, playerCombat.height);
            metric.combatNpcLaunchesCumulative = game.NpcToNpcLaunches;
            metric.combatPlayerLaunchesCumulative = game.NpcToPlayerLaunches;
            metric.braceBlocksCumulative = game.BraceBlocks;
            WorldData w = game.World;
            metric.inBounds &= p.x >= w.boundsMin.x + .40f && p.x <= w.boundsMax.x - .40f &&
                p.y >= w.boundsMin.y + .40f && p.y <= w.boundsMax.y - .40f;
            foreach (Rect obstacle in w.obstacles)
            {
                Vector2 closest = new Vector2(Mathf.Clamp(p.x, obstacle.xMin, obstacle.xMax),
                    Mathf.Clamp(p.y, obstacle.yMin, obstacle.yMax));
                metric.clearOfWalls &= Vector2.Distance(p, closest) >= .39f;
            }
            var probes = game.CrowdProbeSnapshot();
            bool fresh = previousCrowd == null || previousCrowd.Length != probes.Length;
            if (fresh)
            {
                previousCrowd = new Vector2[probes.Length];
                previousGenerations = new int[probes.Length];
                previousActive = new bool[probes.Length];
                previousVisible = new bool[probes.Length];
                previousProgress = new float[probes.Length];
                observedForwardBySlot = new float[probes.Length];
                allPileBeganAt = new float[probes.Length];
                groundPileBeganAt = new float[probes.Length];
                transientPileBeganAt = new float[probes.Length];
                pileGenerations = new int[probes.Length];
                for (int i = 0; i < probes.Length; i++)
                {
                    allPileBeganAt[i] = groundPileBeganAt[i] = transientPileBeganAt[i] = -1;
                    pileGenerations[i] = -1;
                }
            }
            float crowdDelta = Time.realtimeSinceStartup - previousCrowdAt;
            int active = 0, visible = 0, progressed = 0;
            for (int i = 0; i < probes.Length; i++)
            {
                var probe = probes[i];
                Vector2 c = probe.position;
                bool finite = !float.IsNaN(c.x) && !float.IsNaN(c.y) &&
                    !float.IsInfinity(c.x) && !float.IsInfinity(c.y);
                metric.crowdFiniteAndInBounds &= finite &&
                    c.x >= w.boundsMin.x + .38f && c.x <= w.boundsMax.x - .38f &&
                    c.y >= w.boundsMin.y + .38f && c.y <= w.boundsMax.y - .38f;
                Vector3 view = game.GameCamera.WorldToViewportPoint(new Vector3(c.x, 1f, c.y));
                bool inView = probe.active && view.z > 0 && view.x >= 0 &&
                    view.x <= 1 && view.y >= 0 && view.y <= 1;
                if (probe.active)
                {
                    active++;
                    if (inView) visible++; else metric.offscreenCrowdSamples++;
                }
                if (!fresh)
                {
                    bool recycled = probe.generation != previousGenerations[i];
                    bool visibleActivation = probe.generation > 0 && !previousActive[i] &&
                        probe.active && inView;
                    Rect cover = w.crowdExitCover;
                    Vector2 old = previousCrowd[i];
                    Vector2 nearestCover = new Vector2(Mathf.Clamp(old.x, cover.xMin, cover.xMax),
                        Mathf.Clamp(old.y, cover.yMin, cover.yMax));
                    // A visible old sample may legitimately walk under the canopy and
                    // complete between rendered frames. New births still require margin.
                    bool couldEnterCover = Vector2.Distance(old, nearestCover) <=
                        15f * Mathf.Max(0, crowdDelta) + .5f;
                    bool expandedView = probe.active && view.z > 0 &&
                        view.x >= -.18f && view.x <= 1.18f && view.y >= -.18f && view.y <= 1.18f;
                    visibleActivation = probe.generation > 0 && !previousActive[i] &&
                        probe.active && expandedView;
                    if ((recycled && ((previousVisible[i] && !couldEnterCover) || expandedView)) ||
                        visibleActivation)
                        metric.qaVisibleRecycleEvents++;
                    if (!recycled && probe.active && previousActive[i] && crowdDelta > .0001f)
                    {
                        metric.maxCrowdSpeed = Mathf.Max(metric.maxCrowdSpeed,
                            Vector2.Distance(c, previousCrowd[i]) / crowdDelta);
                        observedForwardBySlot[i] += Mathf.Max(0, probe.progress - previousProgress[i]);
                    }
                }
                if (observedForwardBySlot[i] > 1f) progressed++;
                previousCrowd[i] = c;
                previousGenerations[i] = probe.generation;
                previousActive[i] = probe.active;
                previousVisible[i] = inView;
                previousProgress[i] = probe.progress;
            }
            previousCrowdAt = Time.realtimeSinceStartup;
            metric.crowdPopulation = probes.Length;
            metric.activeSamples += active;
            metric.visibleSamples += visible;
            metric.activePeak = Mathf.Max(metric.activePeak, active);
            metric.visiblePeak = Mathf.Max(metric.visiblePeak, visible);
            metric.progressedSlots = progressed;
            metric.arrivals = game.CrowdArrivals;
            metric.goalCrossings = game.CrowdGoalCrossings;
            metric.recycles = game.CrowdRecycles;
            metric.visibleRecycles = game.CrowdVisibleRecycles;
            metric.crowdForwardDistance = game.CrowdForwardDistance;
            metric.crowdBackwardDistance = game.CrowdBackwardDistance;
            // Preserve the old all-active 2D statistic. Only bodies which currently
            // participate in ground collision are subject to the existing pile limit.
            if (Time.realtimeSinceStartup - lastPileSampleAt >= .5f)
            {
                float sampleAt = Time.realtimeSinceStartup;
                lastPileSampleAt = sampleAt;
                metric.pileSamples++;
                var impacts = game.ImpactProbeSnapshot();
                bool aligned = impacts.Length == probes.Length;
                if (aligned)
                    for (int i = 0; i < probes.Length; i++)
                        aligned &= impacts[i].generation == probes[i].generation &&
                            impacts[i].active == probes[i].active &&
                            Vector2.Distance(impacts[i].position, probes[i].position) < .001f;
                metric.pileProbeAlignment &= aligned;
                bool anyAll = false, anyGround = false, anyTransient = false;
                for (int i = 0; i < probes.Length; i++)
                {
                    if (pileGenerations[i] != probes[i].generation || !probes[i].active)
                    {
                        allPileBeganAt[i] = groundPileBeganAt[i] = transientPileBeganAt[i] = -1;
                        pileGenerations[i] = probes[i].generation;
                    }
                    if (!probes[i].active) continue;
                    int neighbors = 0, ground = 0, air = 0, recovering = 0;
                    for (int j = 0; j < probes.Length; j++)
                    {
                        if (!probes[j].active ||
                            (probes[i].position - probes[j].position).sqrMagnitude >= .25f * .25f) continue;
                        neighbors++;
                        if (aligned)
                        {
                            if (impacts[j].airborne) air++;
                            else if (impacts[j].recovering) recovering++;
                            else ground++;
                        }
                    }
                    if (neighbors > metric.maxNearCoincidentCount)
                    {
                        metric.maxNearCoincidentCount = neighbors;
                        metric.allPilePeakGround = ground;
                        metric.allPilePeakAirborne = air;
                        metric.allPilePeakRecovering = recovering;
                    }
                    // Keep every active body as a geometric sample center; only
                    // exclude non-blocking bodies from the neighborhood count.
                    if (aligned)
                        metric.maxGroundNearCoincidentCount = Mathf.Max(metric.maxGroundNearCoincidentCount, ground);
                    metric.maxNearbyAirborneCount = Mathf.Max(metric.maxNearbyAirborneCount, air);
                    metric.maxNearbyRecoveringCount = Mathf.Max(metric.maxNearbyRecoveringCount, recovering);
                    bool allPile = neighbors > 3;
                    bool groundPile = aligned && ground > 3;
                    bool transientPile = allPile && aligned && air + recovering > 0;
                    if (groundPile && (metric.peakGroundPile == null || ground > metric.peakGroundPile.groundCount))
                    {
                        var snapshot = CaptureGroundPile(metric, i, sampleAt, p, probes, impacts, combat,
                            neighbors, ground, air, recovering);
                        if (metric.firstGroundPile == null) metric.firstGroundPile = snapshot;
                        metric.peakGroundPile = snapshot;
                        Debug.Log("RUSH_QA_PILE_SNAPSHOT " + JsonUtility.ToJson(snapshot));
                    }
                    anyAll |= allPile; anyGround |= groundPile; anyTransient |= transientPile;
                    metric.longestAllPileSampleSpan = Mathf.Max(metric.longestAllPileSampleSpan,
                        TrackPileSpan(allPile, sampleAt, ref allPileBeganAt[i]));
                    metric.longestGroundPileSampleSpan = Mathf.Max(metric.longestGroundPileSampleSpan,
                        TrackPileSpan(groundPile, sampleAt, ref groundPileBeganAt[i]));
                    metric.longestNonGroundPileSampleSpan = Mathf.Max(metric.longestNonGroundPileSampleSpan,
                        TrackPileSpan(transientPile, sampleAt, ref transientPileBeganAt[i]));
                }
                if (anyAll) metric.allActivePileSamples++;
                if (anyGround) metric.groundPileSamples++;
                if (anyTransient) metric.nonGroundInPileSamples++;
            }
            Vector3 viewport = game.GameCamera.WorldToViewportPoint(new Vector3(p.x, 1f + playerCombat.height, p.y));
            metric.minViewportX = Mathf.Min(metric.minViewportX, viewport.x);
            metric.maxViewportX = Mathf.Max(metric.maxViewportX, viewport.x);
            metric.minViewportY = Mathf.Min(metric.minViewportY, viewport.y);
            metric.maxViewportY = Mathf.Max(metric.maxViewportY, viewport.y);
            metric.playerVisible &= viewport.z > 0 && viewport.x >= .02f &&
                viewport.x <= .98f && viewport.y >= .02f && viewport.y <= .98f;
        }

        void FinalizeStage(StageEvidence metric, Vector3 cameraMin, Vector3 cameraMax, string name)
        {
            metric.splitSeconds = game.SplitTimes[metric.stage];
            metric.collisions = game.SplitCollisions[metric.stage];
            metric.cameraSpanX = cameraMax.x - cameraMin.x;
            metric.cameraSpanZ = cameraMax.z - cameraMin.z;
            metric.meanActiveCrowd = metric.activeSamples / Mathf.Max(1, metric.samples);
            metric.meanVisibleCrowd = metric.visibleSamples / Mathf.Max(1, metric.samples);
            metric.meanTraversalFrameMilliseconds =
                metric.traversalFrameSeconds * 1000f / Mathf.Max(1, metric.samples);
            string label = name + " stage " + (metric.stage + 1);
            CheckThat(label + ": visible NPC warnings/rushes stay bounded", metric.visibleThreatPeak <= 2 && metric.maxNpcBurstHistory <= 8,
                "warning/rush peaks=" + metric.visibleWarningPeak + "/" + metric.visibleRushPeak +
                " samples=" + metric.warningSamples + "/" + metric.rushSamples +
                " player air samples/height=" + metric.playerAirSamples + "/" + metric.maxPlayerAirHeight + " burst history=" + metric.maxNpcBurstHistory);
            bool preWaitStage = name == "late" && metric.stage == 0;
            CheckThat(label + ": dense population is present",
                metric.crowdPopulation >= 120 && (preWaitStage || metric.visiblePeak >= 10),
                "population=" + metric.crowdPopulation + " visible mean/peak=" +
                metric.meanVisibleCrowd + "/" + metric.visiblePeak +
                " active mean/peak=" + metric.meanActiveCrowd + "/" + metric.activePeak);
            CheckThat(label + ": crowd makes forward progress",
                (preWaitStage || metric.progressedSlots >= Mathf.CeilToInt(metric.crowdPopulation * .75f)) &&
                metric.crowdForwardDistance > metric.crowdBackwardDistance * 2f + metric.crowdPopulation,
                "pre-wait stage=" + preWaitStage + " progressing slots=" + metric.progressedSlots + "/" + metric.crowdPopulation +
                " forward/backward=" + metric.crowdForwardDistance + "/" + metric.crowdBackwardDistance);
            CheckThat(label + ": commuters reach destinations and recycle",
                metric.arrivals > 0 && metric.goalCrossings > 0 && metric.recycles > 0,
                "goal crossings=" + metric.goalCrossings + " arrivals=" + metric.arrivals +
                " recycles=" + metric.recycles);
            CheckThat(label + ": recycling stays invisible",
                metric.visibleRecycles == 0 && metric.qaVisibleRecycleEvents == 0,
                "guard violations=" + metric.visibleRecycles +
                " independently observed visible recycling=" + metric.qaVisibleRecycleEvents);
            CheckThat(label + ": no sampled ground-collider pile",
                metric.pileSamples > 0 && metric.pileProbeAlignment && metric.maxGroundNearCoincidentCount <= 3,
                "samples=" + metric.pileSamples + " aligned=" + metric.pileProbeAlignment +
                " ground max=" + metric.maxGroundNearCoincidentCount +
                " all-active max=" + metric.maxNearCoincidentCount +
                " first all-active peak ground/air/recovering=" + metric.allPilePeakGround + "/" +
                metric.allPilePeakAirborne + "/" + metric.allPilePeakRecovering +
                " over-limit samples all/ground/transient=" + metric.allActivePileSamples + "/" +
                metric.groundPileSamples + "/" + metric.nonGroundInPileSamples +
                " consecutive sample spans all/ground/transient=" + metric.longestAllPileSampleSpan + "/" +
                metric.longestGroundPileSampleSpan + "/" + metric.longestNonGroundPileSampleSpan);
            CheckThat(label + ": follows across several screens",
                metric.actualDistance > 70f && metric.cameraSpanZ > 45f,
                "distance=" + metric.actualDistance + " camera z span=" + metric.cameraSpanZ);
            CheckThat(label + ": player remains visible", metric.samples > 0 && metric.playerVisible,
                "viewport x=" + metric.minViewportX + ".." + metric.maxViewportX +
                " y=" + metric.minViewportY + ".." + metric.maxViewportY);
            CheckThat(label + ": crowd remains finite and within map",
                metric.crowdFiniteAndInBounds && metric.offscreenCrowdSamples > 0,
                "offscreen samples=" + metric.offscreenCrowdSamples +
                " peak observed crowd speed=" + metric.maxCrowdSpeed);
            CheckThat(label + ": collision containment",
                metric.inBounds && metric.clearOfWalls && metric.maxSpeed <= 14f,
                "bounds=" + metric.inBounds + " walls=" + metric.clearOfWalls +
                " max speed=" + metric.maxSpeed);
        }

        float NearestCrowdAhead(Vector2 p, Vector2 direction)
        {
            float nearest = 100f;
            if (previousCrowd == null || previousActive == null) return nearest;
            for (int i = 0; i < previousCrowd.Length; i++)
            {
                if (!previousActive[i]) continue;
                Vector2 delta = previousCrowd[i] - p;
                if (delta.sqrMagnitude > 25f) continue;
                float along = Vector2.Dot(delta, direction);
                float side = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
                if (along > 0 && side < 1f) nearest = Mathf.Min(nearest, delta.magnitude);
            }
            return nearest;
        }

        bool TryLocalPass(Vector2 p, Vector2 waypoint, Vector2 segmentStart,
            WorldData world, out Vector2 choice)
        {
            choice = p;
            Vector2 forward = (waypoint - p).normalized;
            Vector2 side = new Vector2(-forward.y, forward.x);
            Vector2 rejoin = p + forward * Mathf.Min(4f, Vector2.Distance(p, waypoint));
            float best = float.NegativeInfinity;
            foreach (float offset in new float[] { -.8f, .8f, -1.5f, 1.5f })
            {
                Vector2 candidate = p + forward * 2.3f + side * offset;
                Vector2 segment = waypoint - segmentStart;
                float t = segment.sqrMagnitude > .001f ?
                    Mathf.Clamp01(Vector2.Dot(candidate - segmentStart, segment) / segment.sqrMagnitude) : 1;
                if (Vector2.Distance(candidate, segmentStart + segment * t) > 1.6f) continue;
                if (!LineClear(p, candidate, world) || !LineClear(candidate, rejoin, world)) continue;
                Vector2 move = (candidate - p).normalized;
                float nearest = 3f;
                for (int i = 0; previousCrowd != null && i < previousCrowd.Length; i++)
                {
                    if (!previousActive[i]) continue;
                    Vector2 delta = previousCrowd[i] - p;
                    if (delta.sqrMagnitude > 25f) continue;
                    // Already touching: an outward move may separate through the usual solver.
                    if (delta.magnitude < .9f && Vector2.Dot(move, delta.normalized) <= 0) continue;
                    Vector2 leg = candidate - p;
                    float along = Mathf.Clamp01(Vector2.Dot(delta, leg) / leg.sqrMagnitude);
                    nearest = Mathf.Min(nearest, Vector2.Distance(previousCrowd[i], p + leg * along));
                }
                if (nearest < .88f) continue;
                float score = Mathf.Min(nearest, 2f) - Mathf.Abs(offset) * .1f;
                if (score > best) { best = score; choice = candidate; }
            }
            return !float.IsNegativeInfinity(best);
        }

        bool Free(Vector2 point, WorldData world, float clearance = Margin)
        {
            if (point.x < world.boundsMin.x + clearance || point.x > world.boundsMax.x - clearance ||
                point.y < world.boundsMin.y + clearance || point.y > world.boundsMax.y - clearance) return false;
            foreach (Rect obstacle in world.obstacles)
            {
                Vector2 closest = new Vector2(Mathf.Clamp(point.x, obstacle.xMin, obstacle.xMax),
                    Mathf.Clamp(point.y, obstacle.yMin, obstacle.yMax));
                if (Vector2.Distance(point, closest) < clearance) return false;
            }
            return true;
        }

        bool LineClear(Vector2 a, Vector2 b, WorldData world, float clearance = Margin)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / .16f));
            for (int i = 1; i <= samples; i++)
                if (!Free(Vector2.Lerp(a, b, (float)i / samples), world, clearance)) return false;
            return true;
        }

        void CheckNpcRoutes(string name)
        {
            bool clear = true;
            int segments = 0, invalidRoutes = 0;
            string blocked = "";
            foreach (CrowdSpawn spawn in game.World.crowd)
            {
                if (spawn.route == null || spawn.route.Length < 2)
                { invalidRoutes++; clear = false; continue; }
                for (int j = 1; j < spawn.route.Length; j++)
                {
                    if (!LineClear(spawn.route[j - 1], spawn.route[j], game.World, .43f))
                    {
                        clear = false;
                        if (blocked.Length < 500) blocked += segments + " ";
                    }
                    segments++;
                }
                if (spawn.routeIndex >= 0 && spawn.routeIndex < spawn.route.Length &&
                    !LineClear(spawn.position, spawn.route[spawn.routeIndex], game.World, .43f))
                {
                    clear = false;
                    if (blocked.Length < 500) blocked += "spawn ";
                }
            }
            CheckThat(name + ": stage " + game.Stage + " NPC routes have 0.43m clearance",
                clear && segments > 0, "segments=" + segments + " invalid routes=" +
                invalidRoutes + " blocked=" + blocked);
        }

        void CompareRoutes()
        {
            if (report.runs.Count < 2) return;
            RunEvidence risk = report.runs[0], safe = report.runs[1];
            for (int i = 0; i < Mathf.Min(risk.stages.Count, safe.stages.Count); i++)
            {
                StageEvidence a = risk.stages[i], b = safe.stages[i];
                CheckThat("Stage " + (i + 1) + ": safe route is a real detour",
                    b.plannedRouteLength > a.plannedRouteLength + 5f &&
                    b.actualDistance > a.actualDistance + 3f,
                    "risk length/time/hits/wait=" + a.actualDistance + "/" +
                    a.splitSeconds + "/" + a.collisions + "/" + a.commandedWaitSeconds +
                    "; safe=" + b.actualDistance + "/" + b.splitSeconds + "/" +
                    b.collisions + "/" + b.commandedWaitSeconds);
            }
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            StopAllCoroutines();
            Neutral();
            report.elapsedRealSeconds = Time.realtimeSinceStartup - began;
            report.sampledFrames = frames;
            report.meanFrameMilliseconds = frames > 0 ? frameTotal / frames * 1000 : 0;
            report.worstFrameMilliseconds = frameWorst * 1000;
            report.passed = report.failureCount == 0;
            File.WriteAllText(Path.Combine(output, "qa-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("RUSH_QA_FINISHED passed=" + report.passed +
                " failures=" + report.failureCount + " output=" + output);
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}

