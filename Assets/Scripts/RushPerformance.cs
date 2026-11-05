using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace EarlyRush
{
    /// <summary>
    /// Diagnostic-only frame journal. No per-frame logging or disk writes.
    /// Delta reported by frame N+1 is attached to frame N, with both states retained.
    /// Scope CPU times are inclusive (BuildStage can be inside GameUpdate).
    /// Screenshot markers measure request CPU only, never GPU readback/PNG completion.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class RushPerformance : MonoBehaviour
    {
        public enum Scope { GameUpdate, FixedUpdate, BuildStage, QaValidation, QaSample, StatusWrite, ImpactFx, WarningVisual }
        const int Capacity = 131072;
        static RushPerformance instance;
        static readonly double TickMilliseconds = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        struct FrameRecord
        {
            public int frame, firstStage, lastStage, nextStage, fixedCalls, buildCalls, buildTarget;
            public RushState firstState, lastState, nextState;
            public double realStart, nextDeltaMs, wallToNextMs;
            public double updateMs, fixedMs, buildMs, validationMs, qaMs, statusMs, fxMs, warningMs, captureRequestMs;
            public int gc0, gc1, gc2, gc0Delta, gc1Delta, gc2Delta;
            public int captureBegins, captureEnds, captureAge;
            public string phase, captureName;
            public bool complete;
        }

        [Serializable] public sealed class LongFrame
        {
            public int frame, stageBefore, stageAfter, nextStage, fixedCalls, buildCalls, buildTarget;
            public string stateBefore, stateAfter, nextState, phase, capture;
            public int captureAgeFrames, gc0Collections, gc1Collections, gc2Collections;
            public double realStart, frameMilliseconds, wallToNextMilliseconds;
            public double updateCpuMs, fixedCpuMs, buildCpuMs, validationCpuMs, qaSampleCpuMs, statusCpuMs, fxCpuMs, warningCpuMs, captureRequestCpuMs;
        }
        [Serializable] public sealed class Summary
        {
            public string utc, unity, device, api, csvPath;
            public string measurement = "Diagnostic frame journal. nextDeltaMs is Unity unscaledDeltaTime read only in monitor.Update on the next frame and assigned to the preceding recorded frame; it is never read from FixedUpdate hooks. States before/after and next observed state are explicit. wallToNextMs is time between first instrumentation touches, not GPU timing. Scope CPU is inclusive: do not sum nested scopes. Screenshot markers cover request CPU and two following frames only, not verified capture completion. Raw CSV preserves all retained states, including startup, transition and pause. No samples are removed from raw data.";
            public int retainedFrames, completedFrames, overwrittenFrames, over33Ms, over50Ms, over100Ms;
            public double meanMs, p95Ms, p99Ms, worstMs;
            public LongFrame worst;
            public LongFrame[] longFrames;
        }

        RushGame game;
        FrameRecord[] journal;
        int head, count, overwritten, pendingCompletion = -1, lastCaptureFrame = int.MinValue;
        string output, phase = "unspecified", lastCaptureName = "";
        long captureStarted;
        bool flushed, saveSucceeded;
        public static bool IsEnabled { get { return instance != null; } }
        public static bool SaveSucceeded { get { return instance != null && instance.saveSucceeded; } }

        public static void Initialize(RushGame owner)
        {
            if (instance != null) return;
            bool enabled = false;
            string requested = null, qaOutput = null;
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (arg == "--perf" || arg == "--qa" || arg == "--impact-qa" || arg == "--combat-qa" || arg == "--core-qa" || arg == "--journey-qa") enabled = true;
                if (arg.StartsWith("--perf-output=", StringComparison.Ordinal)) { requested = arg.Substring(14); enabled = true; }
                if (arg.StartsWith("--qa-output=", StringComparison.Ordinal)) qaOutput = arg.Substring(12);
                if (arg.StartsWith("--impact-qa-output=", StringComparison.Ordinal)) qaOutput = arg.Substring(19);
                if (arg.StartsWith("--combat-qa-output=", StringComparison.Ordinal)) qaOutput = arg.Substring(19);
                if (arg.StartsWith("--core-qa-output=", StringComparison.Ordinal)) qaOutput = arg.Substring(17);
                if (arg.StartsWith("--journey-qa-output=", StringComparison.Ordinal)) qaOutput = arg.Substring(20);
            }
            if (!enabled) return;
            var monitor = owner.gameObject.AddComponent<RushPerformance>();
            monitor.game = owner;
            monitor.output = !string.IsNullOrEmpty(requested) ? requested : qaOutput;
            if (string.IsNullOrEmpty(monitor.output)) monitor.output = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Performance-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmss", Invariant));
            monitor.journal = new FrameRecord[Capacity];
            Directory.CreateDirectory(monitor.output);
            instance = monitor;
            monitor.Touch();
        }

        public static long Begin(Scope scope, int targetStage = -1)
        {
            if (instance == null || instance.flushed) return 0;
            instance.Touch();
            ref FrameRecord row = ref instance.Current;
            if (scope == Scope.FixedUpdate) row.fixedCalls++;
            if (scope == Scope.BuildStage) { row.buildCalls++; row.buildTarget = targetStage; }
            return System.Diagnostics.Stopwatch.GetTimestamp();
        }

        public static void End(Scope scope, long began)
        {
            if (began == 0 || instance == null || instance.flushed) return;
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - began) * TickMilliseconds;
            instance.Touch();
            ref FrameRecord row = ref instance.Current;
            switch (scope)
            {
                case Scope.GameUpdate: row.updateMs += ms; break;
                case Scope.FixedUpdate: row.fixedMs += ms; break;
                case Scope.BuildStage: row.buildMs += ms; break;
                case Scope.QaValidation: row.validationMs += ms; break;
                case Scope.QaSample: row.qaMs += ms; break;
                case Scope.StatusWrite: row.statusMs += ms; break;
                case Scope.ImpactFx: row.fxMs += ms; break;
                case Scope.WarningVisual: row.warningMs += ms; break;
            }
        }

        public static void SetQaPhase(string value)
        {
            if (instance == null || instance.flushed) return;
            instance.phase = value ?? "unspecified";
            instance.Touch();
            instance.Current.phase = instance.phase;
        }

        public static void MarkCapture(string name, bool begin)
        {
            if (instance == null || instance.flushed) return;
            instance.Touch();
            ref FrameRecord row = ref instance.Current;
            row.captureName = name ?? "";
            if (begin)
            {
                row.captureBegins++;
                instance.lastCaptureFrame = Time.frameCount;
                instance.lastCaptureName = row.captureName;
                row.captureAge = 0;
                instance.captureStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            }
            else
            {
                row.captureEnds++;
                if (instance.captureStarted != 0) row.captureRequestMs += (System.Diagnostics.Stopwatch.GetTimestamp() - instance.captureStarted) * TickMilliseconds;
                instance.captureStarted = 0;
            }
        }

        ref FrameRecord Current { get { return ref journal[(head + count - 1) % Capacity]; } }

        void Touch()
        {
            int frame = Time.frameCount;
            double now = Time.realtimeSinceStartupAsDouble;
            if (count == 0 || Current.frame != frame)
            {
                if (count != 0)
                {
                    ref FrameRecord previous = ref Current;
                    // First touch can occur inside FixedUpdate, where unscaledDeltaTime
                    // denotes a physics step. Only Update may fill the render-frame delta.
                    pendingCompletion = (head + count - 1) % Capacity;
                    previous.wallToNextMs = (now - previous.realStart) * 1000.0;
                    previous.nextState = game.State; previous.nextStage = game.Stage;
                    previous.gc0Delta = GC.CollectionCount(0) - previous.gc0;
                    previous.gc1Delta = GC.CollectionCount(1) - previous.gc1;
                    previous.gc2Delta = GC.CollectionCount(2) - previous.gc2;
                }
                if (count == Capacity) { head = (head + 1) % Capacity; count--; overwritten++; }
                int index = (head + count) % Capacity;
                count++;
                int captureAge = lastCaptureFrame == int.MinValue ? -1 : frame - lastCaptureFrame;
                journal[index] = new FrameRecord
                {
                    frame = frame, realStart = now, firstState = game.State, lastState = game.State,
                    firstStage = game.Stage, lastStage = game.Stage, buildTarget = -1,
                    gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2),
                    phase = phase, captureAge = captureAge >= 0 && captureAge <= 2 ? captureAge : -1,
                    captureName = captureAge >= 0 && captureAge <= 2 ? lastCaptureName : ""
                };
            }
            Current.lastState = game.State; Current.lastStage = game.Stage;
        }

        void Update()
        {
            if (flushed || game == null) return;
            Touch();
            if (pendingCompletion >= 0)
            {
                journal[pendingCompletion].nextDeltaMs = Time.unscaledDeltaTime * 1000.0;
                journal[pendingCompletion].complete = true;
                pendingCompletion = -1;
            }
        }
        void LateUpdate() { if (!flushed && game != null) Touch(); }

        static LongFrame Describe(FrameRecord r)
        {
            return new LongFrame
            {
                frame = r.frame, realStart = r.realStart, stageBefore = r.firstStage, stageAfter = r.lastStage, nextStage = r.nextStage,
                stateBefore = r.firstState.ToString(), stateAfter = r.lastState.ToString(), nextState = r.nextState.ToString(),
                phase = r.phase, capture = r.captureName, captureAgeFrames = r.captureAge,
                frameMilliseconds = r.nextDeltaMs, wallToNextMilliseconds = r.wallToNextMs,
                fixedCalls = r.fixedCalls, buildCalls = r.buildCalls, buildTarget = r.buildTarget,
                gc0Collections = r.gc0Delta, gc1Collections = r.gc1Delta, gc2Collections = r.gc2Delta,
                updateCpuMs = r.updateMs, fixedCpuMs = r.fixedMs, buildCpuMs = r.buildMs,
                validationCpuMs = r.validationMs, qaSampleCpuMs = r.qaMs, statusCpuMs = r.statusMs,
                fxCpuMs = r.fxMs, warningCpuMs = r.warningMs, captureRequestCpuMs = r.captureRequestMs
            };
        }

        static string Csv(string value) { return "\"" + (value ?? "").Replace("\"", "\"\"") + "\""; }
        static void Number(StringBuilder text, double value) { text.Append(',').Append(value.ToString("F4", Invariant)); }
        public static bool Flush()
        {
            if (instance == null) return false;
            instance.WriteReport();
            return instance.saveSucceeded;
        }

        void WriteReport()
        {
            if (flushed || journal == null) return;
            Touch();
            flushed = true;
            try
            {
                string csv = Path.Combine(output, "performance-frames.csv");
                var summary = new Summary { utc = DateTime.UtcNow.ToString("o", Invariant), unity = Application.unityVersion,
                    device = SystemInfo.graphicsDeviceName, api = SystemInfo.graphicsDeviceType.ToString(), csvPath = csv,
                    retainedFrames = count, overwrittenFrames = overwritten };
                double[] durations = new double[count];
                var longFrames = new System.Collections.Generic.List<LongFrame>();
                var line = new StringBuilder(768);
                using (var writer = new StreamWriter(csv, false, new UTF8Encoding(false)))
                {
                    writer.WriteLine("frame,complete,realStart,stateBefore,stageBefore,stateAfter,stageAfter,nextState,nextStage,phase,capture,captureAgeFrames,fixedCalls,buildCalls,buildTarget,gc0Total,gc1Total,gc2Total,gc0Delta,gc1Delta,gc2Delta,nextDeltaMs,wallToNextMs,updateCpuMs,fixedCpuMs,buildCpuMs,validationCpuMs,qaSampleCpuMs,statusCpuMs,fxCpuMs,warningCpuMs,captureRequestCpuMs,captureBegins,captureEnds");
                    for (int i = 0; i < count; i++)
                    {
                        FrameRecord r = journal[(head + i) % Capacity];
                        line.Length = 0;
                        line.Append(r.frame).Append(',').Append(r.complete ? 1 : 0).Append(',').Append(r.realStart.ToString("F6", Invariant));
                        line.Append(',').Append(r.firstState).Append(',').Append(r.firstStage).Append(',').Append(r.lastState).Append(',').Append(r.lastStage);
                        line.Append(',').Append(r.nextState).Append(',').Append(r.nextStage).Append(',').Append(Csv(r.phase)).Append(',').Append(Csv(r.captureName));
                        line.Append(',').Append(r.captureAge).Append(',').Append(r.fixedCalls).Append(',').Append(r.buildCalls).Append(',').Append(r.buildTarget);
                        line.Append(',').Append(r.gc0).Append(',').Append(r.gc1).Append(',').Append(r.gc2).Append(',').Append(r.gc0Delta).Append(',').Append(r.gc1Delta).Append(',').Append(r.gc2Delta);
                        Number(line, r.nextDeltaMs); Number(line, r.wallToNextMs); Number(line, r.updateMs); Number(line, r.fixedMs); Number(line, r.buildMs);
                        Number(line, r.validationMs); Number(line, r.qaMs); Number(line, r.statusMs); Number(line, r.fxMs); Number(line, r.warningMs); Number(line, r.captureRequestMs);
                        line.Append(',').Append(r.captureBegins).Append(',').Append(r.captureEnds);
                        writer.WriteLine(line.ToString());
                        if (!r.complete) continue;
                        durations[summary.completedFrames++] = r.nextDeltaMs;
                        summary.meanMs += r.nextDeltaMs;
                        if (r.nextDeltaMs > 33.333) summary.over33Ms++;
                        if (r.nextDeltaMs > 50) summary.over50Ms++;
                        if (r.nextDeltaMs > 100) summary.over100Ms++;
                        if (r.nextDeltaMs >= summary.worstMs) { summary.worstMs = r.nextDeltaMs; summary.worst = Describe(r); }
                        if (r.nextDeltaMs > 50) longFrames.Add(Describe(r));
                    }
                }
                if (summary.completedFrames > 0)
                {
                    summary.meanMs /= summary.completedFrames;
                    Array.Sort(durations, 0, summary.completedFrames);
                    summary.p95Ms = durations[Math.Max(0, (int)Math.Ceiling(summary.completedFrames * .95) - 1)];
                    summary.p99Ms = durations[Math.Max(0, (int)Math.Ceiling(summary.completedFrames * .99) - 1)];
                }
                summary.longFrames = longFrames.ToArray();
                File.WriteAllText(Path.Combine(output, "performance-summary.json"), JsonUtility.ToJson(summary, true));
                saveSucceeded = true;
                Debug.Log("RUSH_PERFORMANCE_SAVED " + csv + " completed=" + summary.completedFrames + " worstMs=" + summary.worstMs.ToString("F3", Invariant));
            }
            catch (Exception e) { Debug.LogWarning("RUSH_PERFORMANCE_SAVE_FAILED " + e.Message); }
        }

        void OnApplicationQuit() { WriteReport(); }
        void OnDestroy() { WriteReport(); if (instance == this) instance = null; }
    }
}
