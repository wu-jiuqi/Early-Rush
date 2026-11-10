using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace EarlyRush.V9
{
    /// <summary>Rendered Player checks driven through Tick and public action inputs. Fixture setup is explicit.</summary>
    public sealed class RushRuntimeCheckV11 : MonoBehaviour
    {
        const float Step = 1f / 60;
        [Serializable] public sealed class CheckRecord { public string name, evidence; public bool passed; }
        [Serializable] public sealed class SampleRecord
        {
            public string name, scope;
            public int stage, population, visiblePeak, movingPeople, charges, launches, blocks;
            public double elapsed;
            public float movementMeters, playerDistance, peakHeight;
        }
        [Serializable] public sealed class Report
        {
            public string version = "v11-player-runtime-1", utc, device, unity;
            public string scope = "Generated-population local input samples and isolated controlled action fixtures; no complete-route or human-playtest claim. Player observer/fixture initial positions are explicitly arranged, then all motion and actions use Tick/public inputs.";
            public bool passed;
            public float wallSeconds;
            public List<CheckRecord> checks = new List<CheckRecord>();
            public List<SampleRecord> samples = new List<SampleRecord>();
            public List<string> screenshots = new List<string>();
            public List<string> errors = new List<string>();
        }

        readonly Report report = new Report();
        RushPlayV9 game;
        string output;
        float began;
        bool finished;

        void Awake()
        {
            Application.runInBackground = true;
            began = Time.realtimeSinceStartup;
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../QA/runtime-v11"));
            foreach (string arg in Environment.GetCommandLineArgs())
                if (arg.StartsWith("--v11-qa-output=", StringComparison.Ordinal)) output = Path.GetFullPath(arg.Substring("--v11-qa-output=".Length));
            Directory.CreateDirectory(output);
            report.utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            report.device = SystemInfo.graphicsDeviceName; report.unity = Application.unityVersion;
            Application.logMessageReceived += OnLog;
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (!finished && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) report.errors.Add(message + "\n" + stack);
        }

        void Update()
        {
            // A real-time watchdog still terminates a stalled coroutine or a frozen simulation clock.
            if (!finished && Time.realtimeSinceStartup - began > 90)
            { report.errors.Add("Runtime suite exceeded the 90-second wall-clock budget"); Finish(); }
        }

        IEnumerator Start()
        {
            yield return null;
            game = GetComponent<RushPlayV9>();
            if (!game) game = FindFirstObjectByType<RushPlayV9>();
            if (!game) { report.errors.Add("RushPlayV9 not found"); Finish(); yield break; }
            IEnumerator suite = Suite();
            while (!finished)
            {
                bool moved = false; object next = null;
                try { moved = suite.MoveNext(); if (moved) next = suite.Current; }
                catch (Exception error) { report.errors.Add(error.ToString()); Finish(); }
                if (finished) yield break;
                if (!moved) break;
                yield return next;
            }
            if (!finished) Finish();
        }

        IEnumerator Suite()
        {
            for (int stage = 0; stage < 3; stage++)
            {
                game.Load(stage, 91026);
                yield return null;
                var run = game.run;
                // This changes only the initial observer position. It does not claim travel from spawn.
                Vector2 direction; run.player = PopulationObservationPoint(run, out direction); run.velocity = Vector2.zero;
                game.FrameForReview(); run.Begin(); game.RenderFrame();
                var sample = new SampleRecord { name = "population-stage-" + stage, stage = stage, scope = "Generated population, arranged local observer start, two seconds of real movement input; not a stage playthrough", population = ActivePeople(run) };
                Vector2 playerStart = run.player;
                var previous = new Dictionary<int, Vector2>(); var distances = new Dictionary<int, float>();
                foreach (var person in run.people) { previous[person.id] = person.position; distances[person.id] = 0; }
                for (int tick = 0; tick < 120; tick++)
                {
                    run.Tick(Step, direction * .35f); game.RenderFrame();
                    foreach (var person in run.people)
                    {
                        if (person.exited || !previous.ContainsKey(person.id)) continue;
                        float distance = Vector2.Distance(previous[person.id], person.position);
                        distances[person.id] += distance; previous[person.id] = person.position; sample.movementMeters += distance;
                    }
                    sample.visiblePeak = Math.Max(sample.visiblePeak, VisiblePeople(run));
                    sample.peakHeight = Mathf.Max(sample.peakHeight, run.PlayerHeight);
                    yield return null;
                }
                foreach (float distance in distances.Values) if (distance > .1f) sample.movingPeople++;
                sample.elapsed = run.elapsed; sample.playerDistance = Vector2.Distance(playerStart, run.player);
                sample.charges = run.NpcChargeCount; sample.launches = run.PlayerLaunchCount; sample.blocks = run.BraceBlockCount;
                report.samples.Add(sample);
                Check(sample.population > 0 && sample.visiblePeak > 0, "Generated crowd is visible stage=" + stage, "active=" + sample.population + " visiblePeak=" + sample.visiblePeak);
                Check(sample.movingPeople > 0, "Generated crowd actually moves stage=" + stage, "movingPeople=" + sample.movingPeople + " totalMeters=" + sample.movementMeters);
                Check(run.map.IsWalkable(run.player, RunV9.PlayerRadius), "Local input stays in walkable area stage=" + stage, run.player.ToString());
                yield return new WaitForEndOfFrame(); Capture(sample.name + "-local-input.png"); SaveTrace(run, sample.name);
            }

            game.Load(0, 91026); yield return null;
            var dash = game.run; HideGeneratedPeople(dash); dash.Begin(); game.FrameForReview();
            Vector2 dashStart = dash.player;
            Check(dash.TryDash(Vector2.up), "Dash accepts a real public input", "isolated local fixture at spawn");
            for (int tick = 0; tick < 6; tick++) { dash.Tick(Step, Vector2.up); game.RenderFrame(); yield return null; }
            Check(dash.IsDashing && Vector2.Distance(dashStart, dash.player) > dash.config.walkSpeed * .1f, "Dash has observed increased displacement", "distance=" + Vector2.Distance(dashStart, dash.player));
            yield return new WaitForEndOfFrame(); Capture("fixture-player-dash.png");
            dash.SetBracing(true); dash.Tick(Step, Vector2.up); game.RenderFrame();
            Check(dash.IsBracing && !dash.IsDashing, "Steady stance cancels dash", "public SetBracing(true), then real Tick");
            yield return new WaitForEndOfFrame(); Capture("fixture-player-brace.png");
            double pausedTime = dash.elapsed; Vector2 pausedPosition = dash.player; float cooldown = dash.DashReady01;
            dash.Pause(); Vector3 cameraPosition = game.GameCamera.transform.position;
            dash.Tick(5, Vector2.up);
            for (int frame = 0; frame < 3; frame++) { game.RenderFrame(); yield return null; }
            Check(dash.elapsed == pausedTime && dash.player == pausedPosition && dash.DashReady01 == cooldown && game.GameCamera.transform.position == cameraPosition,
                "Pause freezes actual runtime simulation and follow camera", "three rendered frames and a rejected five-second Tick");
            SaveTrace(dash, "fixture-dash-brace-pause");

            for (int defence = 0; defence < 2; defence++)
            {
                game.Load(0, 91026); yield return null;
                var run = game.run; PersonV9 attacker = PrepareChargeFixture(run);
                bool braced = defence == 1;
                run.Begin(); run.SetBracing(braced); game.FrameForReview();
                bool sawWindup = false, capturedWindup = false, capturedImpact = false;
                Vector2 start = run.player; float height = 0;
                for (int tick = 0; tick < 180 && run.phase == RunPhase.Playing; tick++)
                {
                    run.Tick(Step, Vector2.zero); game.RenderFrame();
                    height = Mathf.Max(height, run.PlayerHeight);
                    if (attacker.actionPhase == NpcActionPhase.Windup)
                    {
                        sawWindup = true;
                        if (!capturedWindup && run.NpcWindup01(attacker) >= .25f)
                        {
                            capturedWindup = true;
                            yield return new WaitForEndOfFrame(); Capture(braced ? "fixture-braced-npc-windup.png" : "fixture-unbraced-npc-windup.png");
                        }
                    }
                    if (!capturedImpact && (braced ? run.BraceBlockCount > 0 : run.PlayerHeight > .1f))
                    {
                        capturedImpact = true;
                        yield return new WaitForEndOfFrame(); Capture(braced ? "fixture-brace-block.png" : "fixture-player-knockback.png");
                    }
                    if (capturedImpact && !run.IsAirborne && attacker.actionPhase == NpcActionPhase.Recovery && (braced || tick > 65)) break;
                    yield return null;
                }
                string name = braced ? "fixture-brace-block" : "fixture-unbraced-knockback";
                Check(sawWindup && run.NpcChargeCount > 0, name + " observes windup before an actual charge", "charges=" + run.NpcChargeCount + " windup=" + sawWindup);
                Check(braced ? run.BraceBlockCount == 1 && run.PlayerLaunchCount == 0 : run.PlayerLaunchCount == 1 && height > 0,
                    name + " observed outcome", "blocks=" + run.BraceBlockCount + " launches=" + run.PlayerLaunchCount + " peakHeight=" + height);
                Check(capturedImpact, name + " captured the observed outcome", "No attack-result or task-state assignment is used");
                for (int tick = 0; tick < 40; tick++) { run.Tick(Step, Vector2.zero); game.RenderFrame(); yield return null; }
                Check(!run.IsAirborne && run.PlayerHeight == 0, name + " leaves bounded recovery", "height=" + run.PlayerHeight);
                report.samples.Add(new SampleRecord { name = name, scope = "Controlled fixture: generated actors hidden, one ordinary attacker and player arranged on a verified lane; attack state develops only via Tick", stage = 0, population = 1,
                    charges = run.NpcChargeCount, launches = run.PlayerLaunchCount, blocks = run.BraceBlockCount, elapsed = run.elapsed, peakHeight = height, playerDistance = Vector2.Distance(start, run.player) });
                SaveTrace(run, name);
            }
            yield return null;
        }

        static void HideGeneratedPeople(RunV9 run) { foreach (var person in run.people) person.exited = true; }

        static PersonV9 PrepareChargeFixture(RunV9 run)
        {
            HideGeneratedPeople(run);
            for (float distance = 18; distance < run.map.PathLength - 18; distance += 2)
            {
                Vector2 point = run.map.PointAtDistance(distance), direction = (run.map.PointAtDistance(distance + 1) - point).normalized;
                bool clear = direction.sqrMagnitude > .9f;
                for (float along = 0; along < 4; along += .3f)
                {
                    Vector2 sample = point + direction * along;
                    if (run.IsCrowdProtected(sample, 1) || !run.map.IsWalkable(sample, .7f)) clear = false;
                }
                if (!clear) continue;
                run.player = point + direction * 2.2f; run.velocity = Vector2.zero;
                var attacker = new PersonV9 { id = 8100, kind = PersonKind.Ordinary, position = point, home = point, target = run.player, canRush = true, actionInitialized = true };
                run.people.Add(attacker); return attacker;
            }
            throw new InvalidOperationException("No clear unprotected lane for the runtime charge fixture");
        }

        static Vector2 PopulationObservationPoint(RunV9 run, out Vector2 direction)
        {
            int best = -1; Vector2 chosen = run.map.spawn; direction = Vector2.up;
            for (float distance = 18; distance < run.map.PathLength - 18; distance += 4)
            {
                Vector2 point = run.map.PointAtDistance(distance);
                if (!run.map.IsWalkable(point, RunV9.PlayerRadius)) continue;
                int count = 0;
                foreach (var person in run.people)
                    if (!person.exited && Mathf.Abs(person.position.x - point.x) < 10 && Mathf.Abs(person.position.y - point.y) < 6) count++;
                if (count <= best) continue;
                best = count; chosen = point; direction = (run.map.PointAtDistance(distance + 1) - point).normalized;
            }
            return chosen;
        }

        static int ActivePeople(RunV9 run) { int count = 0; foreach (var person in run.people) if (!person.exited) count++; return count; }
        int VisiblePeople(RunV9 run)
        {
            int count = 0;
            foreach (var person in run.people)
            {
                Vector3 view = game.GameCamera.WorldToViewportPoint(new Vector3(person.position.x, person.position.y, 0));
                if (!person.exited && view.z > 0 && view.x >= 0 && view.x <= 1 && view.y >= 0 && view.y <= 1) count++;
            }
            return count;
        }

        void Check(bool passed, string name, string evidence)
        {
            report.checks.Add(new CheckRecord { name = name, passed = passed, evidence = evidence });
            Debug.Log("V11_RUNTIME_CHECK " + (passed ? "PASS " : "FAIL ") + name + " " + evidence);
        }

        void Capture(string name)
        {
            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            if (!image) throw new InvalidOperationException("Screenshot capture failed: " + name);
            try { File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG()); report.screenshots.Add(name); }
            finally { Destroy(image); }
        }

        void SaveTrace(RunV9 run, string name)
        {
            var text = new StringBuilder("time,type,id,x,y\n"); var culture = CultureInfo.InvariantCulture;
            foreach (var entry in run.telemetry)
                text.AppendLine(string.Join(",", entry.time.ToString(culture), entry.type, entry.id.ToString(culture), entry.position.x.ToString(culture), entry.position.y.ToString(culture)));
            File.WriteAllText(Path.Combine(output, name + ".csv"), text.ToString());
        }

        void Finish()
        {
            if (finished) return;
            finished = true; report.wallSeconds = Time.realtimeSinceStartup - began;
            report.passed = report.errors.Count == 0 && report.checks.Count > 0 && report.checks.TrueForAll(check => check.passed);
            File.WriteAllText(Path.Combine(output, "runtime-report.json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(output, "README.txt"), report.scope + "\nThe population screenshots begin at an arranged observer position. Action screenshots use a controlled one-attacker fixture. No screenshot proves a complete route or natural attack frequency.\nPassed: " + report.passed + "\nErrors: " + report.errors.Count);
            Debug.Log("V11_RUNTIME_RESULT passed=" + report.passed + " checks=" + report.checks.Count + " errors=" + report.errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }

        void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}
