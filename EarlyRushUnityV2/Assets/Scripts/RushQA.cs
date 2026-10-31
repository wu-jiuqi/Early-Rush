using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace EarlyRush
{
    /// <summary>
    /// UNCOMPILED v2 acceptance draft. Opt-in Windows Player input driver.
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
        [Serializable] public class StageEvidence
        {
            public int stage, collisions, offscreenCrowdSamples;
            public float maxCrowdSpeed;
            public bool crowdFiniteAndInBounds = true;
            public float mapWidth, mapDepth, plannedRouteLength;
            public float actualDistance, splitSeconds, stoppedSeconds;
            public float commandedWaitSeconds, bracedSeconds, dashedSeconds;
            public float maxSpeed, longestStallSeconds;
            public float cameraSpanX, cameraSpanZ;
            public float minViewportX = 1, maxViewportX;
            public float minViewportY = 1, maxViewportY;
            public int samples;
            public bool inBounds = true, clearOfWalls = true, playerVisible = true;
        }
        [Serializable] public class RunEvidence
        {
            public string name, route;
            public bool includesDeliberateDoorProbe;
            public float totalSeconds;
            public int collisions, transitions;
            public bool reachedResults;
            public float[] splits;
            public int[] splitCollisions;
            public List<StageEvidence> stages = new List<StageEvidence>();
        }
        [Serializable] public class Report
        {
            public string utc, engine, device, graphicsApi;
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
        float previousCrowdAt;
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
            ScreenCapture.CaptureScreenshot(path);
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
            currentRun = "late-wait";
            while (!finished && game.TotalTime <= game.TimeLimit + .3f) yield return null;
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

        IEnumerator PauseCheck(string label)
        {
            Neutral();
            game.TogglePause();
            yield return Wait(.15f);
            float total = game.TotalTime, split = game.StageTime;
            Vector2 position = game.Position;
            Vector3 camera = game.GameCamera.transform.position;
            Vector2[] crowd = game.CrowdSnapshot();
            yield return Wait(.4f);
            Vector2[] after = game.CrowdSnapshot();
            bool crowdFrozen = crowd.Length == after.Length;
            for (int i = 0; i < Mathf.Min(crowd.Length, after.Length); i++)
                crowdFrozen &= Vector2.Distance(crowd[i], after[i]) < .001f;
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
            float lastProgress = started, lastDash = -10, hazardSince = -1;
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
                    if (metric != null) FinalizeStage(metric, cameraMin, cameraMax, name);
                    previousStage = game.Stage;
                    Neutral();
                    yield return Wait(.15f);
                    CheckThat(name + ": stage " + game.Stage + " resets momentum",
                        game.Velocity.sqrMagnitude < .001f && game.DashCooldown01 > .99f,
                        "speed=" + game.Velocity.magnitude + " ready=" + game.DashCooldown01);
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
                    run.stages.Add(metric);
                    CheckThat(name + ": stage " + game.Stage + " is a large map",
                        metric.mapDepth >= 75f && metric.mapWidth >= 30f,
                        metric.mapWidth + " x " + metric.mapDepth + " metres");
                    index = 0; midShot = false; hazardSince = -1;
                    previousCrowd = null;
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
                metric.actualDistance += Vector2.Distance(previousPosition, p);
                previousPosition = p;
                metric.maxSpeed = Mathf.Max(metric.maxSpeed, game.Velocity.magnitude);
                if (game.Velocity.magnitude < .35f) metric.stoppedSeconds += dt;
                if (game.QAMove.sqrMagnitude < .01f) metric.commandedWaitSeconds += dt;
                if (game.IsBracing) metric.bracedSeconds += dt;
                if (game.IsDashing) metric.dashedSeconds += dt;
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
                    game.QABrace = false;
                    game.QADash = game.DashCooldown01 > .99f &&
                        Time.realtimeSinceStartup - doorPhaseBegan < .4f;
                    if (Time.realtimeSinceStartup - doorPhaseBegan >= 1.25f)
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
                float ahead = NearestCrowdAhead(p, direction);
                if (safe && ahead < 1.7f)
                {
                    if (hazardSince < 0) hazardSince = Time.realtimeSinceStartup;
                    if (Time.realtimeSinceStartup - hazardSince < .65f)
                        game.QAMove = Vector2.zero;
                    else
                        game.QABrace = true;
                }
                else
                {
                    hazardSince = -1;
                    if (stalled > 1.3f) game.QABrace = true;
                    bool clearDash = delta.magnitude > 5f &&
                        LineClear(p, p + direction * 3.2f, game.World);
                    if (!game.QABrace && clearDash && (!safe || ahead > 3.5f) &&
                        game.DashCooldown01 > .99f && Time.realtimeSinceStartup - lastDash > 1.3f)
                    {
                        game.QADash = true;
                        lastDash = Time.realtimeSinceStartup;
                    }
                }
                if (stalled > 2f)
                {
                    Vector2 side = new Vector2(-direction.y, direction.x);
                    if (((int)(stalled * 2f) & 1) == 0) side = -side;
                    Vector2 escape = (direction + side * .9f).normalized;
                    if (LineClear(p, p + escape * .8f, game.World)) game.QAMove = escape;
                    game.QABrace = true; game.QADash = false;
                }
                yield return null;
            }
            Neutral();
            if (metric != null) FinalizeStage(metric, cameraMin, cameraMax, name);
            run.totalSeconds = game.TotalTime;
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

        void SampleGeometry(StageEvidence metric, Vector2 p)
        {
            WorldData w = game.World;
            metric.inBounds &= p.x >= w.boundsMin.x + .40f && p.x <= w.boundsMax.x - .40f &&
                p.y >= w.boundsMin.y + .40f && p.y <= w.boundsMax.y - .40f;
            foreach (Rect obstacle in w.obstacles)
            {
                Vector2 closest = new Vector2(Mathf.Clamp(p.x, obstacle.xMin, obstacle.xMax),
                    Mathf.Clamp(p.y, obstacle.yMin, obstacle.yMax));
                metric.clearOfWalls &= Vector2.Distance(p, closest) >= .39f;
            }
            Vector2[] crowd = game.CrowdSnapshot();
            float crowdDelta = Time.realtimeSinceStartup - previousCrowdAt;
            for (int i = 0; i < crowd.Length; i++)
            {
                Vector2 c = crowd[i];
                bool finite = !float.IsNaN(c.x) && !float.IsNaN(c.y) &&
                    !float.IsInfinity(c.x) && !float.IsInfinity(c.y);
                metric.crowdFiniteAndInBounds &= finite &&
                    c.x >= w.boundsMin.x + .38f && c.x <= w.boundsMax.x - .38f &&
                    c.y >= w.boundsMin.y + .38f && c.y <= w.boundsMax.y - .38f;
                if (previousCrowd != null && previousCrowd.Length == crowd.Length && crowdDelta > .0001f)
                    metric.maxCrowdSpeed = Mathf.Max(metric.maxCrowdSpeed,
                        Vector2.Distance(c, previousCrowd[i]) / crowdDelta);
                Vector3 view = game.GameCamera.WorldToViewportPoint(new Vector3(c.x, 1f, c.y));
                if (view.x < 0 || view.x > 1 || view.y < 0 || view.y > 1 || view.z <= 0)
                    metric.offscreenCrowdSamples++;
            }
            previousCrowd = crowd;
            previousCrowdAt = Time.realtimeSinceStartup;
            Vector3 viewport = game.GameCamera.WorldToViewportPoint(new Vector3(p.x, 1f, p.y));
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
            string label = name + " stage " + (metric.stage + 1);
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
            foreach (Vector2 crowd in game.CrowdSnapshot())
            {
                Vector2 delta = crowd - p;
                float along = Vector2.Dot(delta, direction);
                float side = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
                if (along > 0 && side < 1f) nearest = Mathf.Min(nearest, delta.magnitude);
            }
            return nearest;
        }

        bool Free(Vector2 point, WorldData world)
        {
            if (point.x < world.boundsMin.x + Margin || point.x > world.boundsMax.x - Margin ||
                point.y < world.boundsMin.y + Margin || point.y > world.boundsMax.y - Margin) return false;
            foreach (Rect obstacle in world.obstacles)
            {
                Vector2 closest = new Vector2(Mathf.Clamp(point.x, obstacle.xMin, obstacle.xMax),
                    Mathf.Clamp(point.y, obstacle.yMin, obstacle.yMax));
                if (Vector2.Distance(point, closest) < Margin) return false;
            }
            return true;
        }

        bool LineClear(Vector2 a, Vector2 b, WorldData world)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / .16f));
            for (int i = 1; i <= samples; i++)
                if (!Free(Vector2.Lerp(a, b, (float)i / samples), world)) return false;
            return true;
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

