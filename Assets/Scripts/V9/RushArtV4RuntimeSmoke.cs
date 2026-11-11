using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EarlyRush.V9
{
    // CLI host must set runtimeQa=true so only this driver advances RunV9.
    public sealed class RushArtV4RuntimeSmoke : MonoBehaviour
    {
        [Serializable] public class StageResult
        {
            public int stage, frames, unfocusedFrames;
            public Vector2 start, end;
            public double elapsed, expectedElapsed;
            public float wallSeconds, meanFrameMs, maxFrameMs, movement;
            public bool pausedStable, resumed, singleTickClock;
            public List<float> frameMs = new List<float>();
        }
        [Serializable] public class Report
        {
            public string scope = "Windows rendered smoke only: each chapter loads seed 91027 at spawn, begins, moves 50 rendered frames, pauses 20, resumes/moves 50. One Tick using actual unscaled frame delta per frame; no teleport, accelerated simulation or full-route claim. Frame intervals include QA/render overhead, are not production benchmark or human 7-10 minute evidence. Focus records background status proxy, not OS window visibility. Nonblack screenshots are readiness evidence, not visual-quality acceptance.";
            public string utc, unity, gpu;
            public bool passed, runInBackground;
            public List<StageResult> stages = new List<StageResult>();
            public List<string> screenshots = new List<string>(), notes = new List<string>(), errors = new List<string>();
        }
        readonly Report report = new Report();
        RushPlayV9 game;
        string output;
        bool finished;
        float began;
        void Awake()
        {
            began = Time.realtimeSinceStartup; Application.runInBackground = true;
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../QA/runtime-art-v4"));
            Directory.CreateDirectory(output);
            report.utc = DateTime.UtcNow.ToString("o"); report.unity = Application.unityVersion;
            report.gpu = SystemInfo.graphicsDeviceName; report.runInBackground = Application.runInBackground;
            Application.logMessageReceived += OnLog;
        }
        void OnLog(string message, string stack, LogType type)
        { if (!finished && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)) report.errors.Add(message); }
        void Update()
        { if (!finished && Time.realtimeSinceStartup - began > 120) { report.errors.Add("120-second watchdog expired; rendered smoke incomplete."); Finish(); } }
        IEnumerator Start()
        {
            // Flatten nested iterators so screenshot/readiness exceptions produce a failed report, not a silent stalled test.
            var stack = new Stack<IEnumerator>(); stack.Push(Suite());
            while (stack.Count > 0 && !finished)
            {
                object next = null; bool moved = false; Exception failure = null;
                try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; }
                catch (Exception ex) { failure = ex; }
                if (failure != null) { report.errors.Add(failure.ToString()); Finish(); yield break; }
                if (!moved) { stack.Pop(); continue; }
                if (next is IEnumerator child) { stack.Push(child); continue; }
                yield return next;
            }
            Finish();
        }
        IEnumerator Suite()
        {
            yield return null;
            game = GetComponent<RushPlayV9>(); if (!game) game = FindFirstObjectByType<RushPlayV9>();
            if (!game) throw new InvalidOperationException("RushPlayV9 host missing");
            for (int stage = 0; stage < 3; stage++)
            {
                game.Load(stage, 91027); game.run.Begin(); game.FrameForReview(); game.RenderFrame();
                yield return Surface(stage == 0);
                var run = game.run; var s = new StageResult { stage = stage, start = run.player, pausedStable = true };
                report.stages.Add(s); Capture(stage + "-start.png");
                double initialElapsed = run.elapsed, expected = 0; float wallStart = Time.realtimeSinceStartup;
                Vector2 pausePosition = Vector2.zero; double pauseClock = 0;
                Vector2 heading = Heading(run);
                for (int f = 0; f < 120; f++)
                {
                    yield return null;
                    float dt = Time.unscaledDeltaTime;
                    if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) throw new Exception("Invalid observed frame interval");
                    s.frames++; s.frameMs.Add(dt * 1000); s.meanFrameMs += dt * 1000; s.maxFrameMs = Mathf.Max(s.maxFrameMs, dt * 1000);
                    if (!Application.isFocused) s.unfocusedFrames++;
                    if (f == 50) { run.Pause(); pausePosition = run.player; pauseClock = run.elapsed; }
                    if (f == 70) { run.Resume(); s.resumed = run.phase == RunPhase.Playing; }
                    bool paused = f >= 50 && f < 70;
                    Vector2 direction = heading;
                    run.Tick(dt, direction);
                    if (!paused) expected += dt;
                    else s.pausedStable &= run.phase == RunPhase.Paused && Vector2.Distance(run.player, pausePosition) < .00001f && Math.Abs(run.elapsed - pauseClock) < .000001;
                    game.FrameForReview(); game.RenderFrame();
                    yield return new WaitForEndOfFrame();
                    if (f == 69) Capture(stage + "-paused.png");
                }
                s.end = run.player; s.movement = Vector2.Distance(s.start, s.end); s.elapsed = run.elapsed - initialElapsed;
                s.expectedElapsed = expected; s.singleTickClock = Math.Abs(s.elapsed - expected) < .005;
                s.wallSeconds = Time.realtimeSinceStartup - wallStart; s.meanFrameMs /= s.frames;
                Capture(stage + "-resumed.png");
                if (!s.pausedStable || !s.resumed || !s.singleTickClock || s.movement <= .1f || s.frames != 120)
                    report.errors.Add("Stage " + stage + " movement/pause/resume/single-Tick checks failed; inspect stage record.");
            }
        }
        static Vector2 Heading(RunV9 run)
        {
            // Follow the first authored leg from actual spawn, without relocating the actor.
            foreach (var p in run.map.route) if (Vector2.Distance(p, run.player) > 1.5f) return (p - run.player).normalized;
            return (run.map.exit - run.player).normalized;
        }
        IEnumerator Surface(bool allowResize)
        {
            int width = Screen.width, height = Screen.height, consecutive = 0;
            float start = Time.realtimeSinceStartup; bool reset = false;
            while (Time.realtimeSinceStartup - start < 8)
            {
                yield return null; game.FrameForReview(); game.RenderFrame(); yield return new WaitForEndOfFrame();
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                bool valid = texture && texture.width == width && texture.height == height && Screen.width == width && Screen.height == height
                    && UnityEngine.Rendering.SplashScreen.isFinished && Nonblack(texture);
                if (texture) Destroy(texture);
                consecutive = valid ? consecutive + 1 : 0; if (consecutive >= 2) yield break;
                if (allowResize && !reset && Time.realtimeSinceStartup - start > 2)
                {
                    reset = true; report.notes.Add("Initial surface black: explicit alternate-resolution swapchain recovery, returning to " + width + "x" + height);
                    Screen.SetResolution(width == 1024 ? 1280 : 1024, 768, FullScreenMode.Windowed);
                    yield return null; yield return new WaitForEndOfFrame(); yield return null; yield return new WaitForEndOfFrame();
                    Screen.SetResolution(width, height, FullScreenMode.Windowed);
                }
            }
            throw new Exception("No valid nonblack framebuffer after 8 seconds; window/render availability blocked. Not a pass.");
        }
        static bool Nonblack(Texture2D texture)
        { foreach (var p in texture.GetPixels32()) if (p.r > 4 || p.g > 4 || p.b > 4) return true; return false; }
        void Capture(string name)
        {
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                if (!texture) throw new Exception("Framebuffer capture missing: " + name);
                File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG());
                if (texture.width != Screen.width || texture.height != Screen.height || !Nonblack(texture)) throw new Exception("Invalid/black framebuffer: " + name);
                report.screenshots.Add(name);
            }
            finally { if (texture) Destroy(texture); }
        }
        void Finish()
        {
            if (finished) return; finished = true;
            report.passed = report.errors.Count == 0 && report.stages.Count == 3 && report.screenshots.Count == 9;
            File.WriteAllText(Path.Combine(output, "runtime-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("ART_V4_RUNTIME_SMOKE passed=" + report.passed);
            Application.Quit(report.passed ? 0 : 1);
        }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}

