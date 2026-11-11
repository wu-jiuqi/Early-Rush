using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using EarlyRush.UIKit;
using EarlyRush.V9;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EarlyRush.V15
{
    /// <summary>Button-listener and rendered-UI checks. Explicit state fixtures are not complete gameplay routes.</summary>
    public sealed class RushUIRuntimeCheckV15 : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, evidence; public bool passed; }
        [Serializable] public sealed class Screenshot
        {
            public string file, phase, scope;
            public int width, height, stage, seed, captureAttempts;
            public float nonBlackFraction;
        }
        [Serializable] public sealed class MapCheck
        {
            public int width, height, stage;
            public bool expanded;
            public Rect actualPanel;
            public Vector2 worldPlayer, projectedGui, expectedGui, actualMarkerScreenTopLeft, expectedMarkerScreenTopLeft;
            public float projectionError, renderedMarkerError;
        }
        [Serializable] public sealed class Report
        {
            public string version = "V15-UI-runtime-3", utc, unity;
            public string scope = "Actual GraphicRaycaster center-hit checks and uGUI button.onClick listeners, manually advanced existing RunV9.Tick, and explicitly arranged HUD/result/focus state fixtures. Three chapter HUDs at three resolutions; actual new uGUI marker coordinates and beat-window mesh sizes. Not OS mouse automation, complete level routes, human timing, FPS or a game-completion claim.";
            public bool passed, preferencesRestored;
            public List<Check> checks = new List<Check>();
            public List<Screenshot> screenshots = new List<Screenshot>();
            public List<MapCheck> maps = new List<MapCheck>();
            public List<string> errors = new List<string>();
        }
        sealed class SavedPreference { public string key; public bool existed, isFloat; public int integer; public float number; }

        readonly Report report = new Report();
        readonly List<SavedPreference> preferences = new List<SavedPreference>();
        RushPlayV9 game;
        RushUIV15 ui;
        string output;
        float began, previousAudioVolume;
        int productionSeed;
        bool finished, backedUp, restored, surfaceWarmed;

        void Awake()
        {
            Application.runInBackground = true;
            began = Time.realtimeSinceStartup;
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../QA/ui-v15"));
            foreach (string arg in Environment.GetCommandLineArgs())
                if (arg.StartsWith("--v15-qa-output=", StringComparison.Ordinal)) output = Path.GetFullPath(arg.Substring("--v15-qa-output=".Length));
            Directory.CreateDirectory(output);
            report.utc = DateTime.UtcNow.ToString("o"); report.unity = Application.unityVersion;
            Application.logMessageReceived += OnLog;
            BackupPreferences();
        }

        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - began > 240)
            { report.errors.Add("UI QA exceeded its 240 second wall-clock budget"); Finish(); }
        }

        IEnumerator Start()
        {
            yield return null;
            game = GetComponent<RushPlayV9>(); if (!game) game = FindFirstObjectByType<RushPlayV9>();
            ui = game ? game.UI : null;
            if (!game || !ui) { report.errors.Add("V15 host or UI was not created"); Finish(); yield break; }
            productionSeed = game.run.seed; previousAudioVolume = AudioListener.volume;
            // Flatten nested enumerators so fixture/capture exceptions reach one cleanup path.
            var stack = new Stack<IEnumerator>(); stack.Push(Suite());
            while (stack.Count > 0 && !finished)
            {
                bool moved = false; object next = null;
                try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; }
                catch (Exception error) { report.errors.Add(error.ToString()); Finish(); }
                if (finished) yield break;
                if (!moved) { stack.Pop(); continue; }
                if (next is IEnumerator child) { stack.Push(child); continue; }
                yield return next;
            }
            if (!finished) Finish();
        }

        IEnumerator Suite()
        {
            for (int i = 0; i < 3; i++) PlayerPrefs.DeleteKey("V9Best" + i);
            PlayerPrefs.SetInt("V10Unlocked", 0);
            yield return Resize(1280, 720);
            LoadFixture(0, false);
            CheckThat("catalogue loads all sprites", ui.UILoadedAssetCount == 110, "U15.LoadedCount=" + ui.UILoadedAssetCount);
            CheckThat("Resources has exactly 110 sprites", Resources.LoadAll<Sprite>("UIV15").Length == 110, "Actual Resources.LoadAll<Sprite>, not manifest-only count");
            bool missingRejected = false;
            try { U15.Sprite("__qa_intentionally_unknown_asset__"); } catch (ArgumentException) { missingRejected = true; }
            CheckThat("unknown asset fails explicitly", missingRejected, "Intentionally unknown catalogue key rejected; actual manifest assets all loaded");
            yield return Capture("ready-1280x720.png", "Fresh locked-progress ready state; no route simulation");
            double readyTime = game.run.elapsed;
            game.run.Tick(.5f, Vector2.zero); ui.RefreshNow();
            CheckThat("ready does not consume time", game.run.phase == RunPhase.Ready && game.run.elapsed == readyTime, "Existing Tick called with 0.5 seconds while Ready");
            for (int chapter = 1; chapter < 3; chapter++)
            {
                Button locked = ui.FindButton("Chapter" + chapter + "Button");
                var before = game.run;
                CheckThat("chapter " + chapter + " locked", locked && !locked.interactable, "Fresh progress fixture");
                if (locked) locked.onClick.Invoke();
                CheckThat("locked chapter listener rejects", ReferenceEquals(before, game.run), "Invoked disabled button.onClick directly to check its guard");
            }
            yield return Click("StartButton");
            CheckThat("start button begins stage", game.run.phase == RunPhase.Playing, "Actual StartButton listener");
            game.run.Tick(.25f, Vector2.zero); ui.RefreshNow();
            CheckThat("playing consumes time", game.run.elapsed > readyTime, "Existing Tick, not direct elapsed assignment");
            yield return Click("PauseButton");
            double pausedTime = game.run.elapsed; game.run.Tick(.5f, Vector2.zero); ui.RefreshNow();
            CheckThat("pause button freezes time", game.run.phase == RunPhase.Paused && game.run.elapsed == pausedTime, "PauseButton listener + existing Tick");
            yield return Capture("paused-1280x720.png", "Button-driven pause menu");
            yield return Click("PauseResumeButton");
            game.run.Tick(.25f, Vector2.zero); ui.RefreshNow();
            CheckThat("resume button resumes time", game.run.phase == RunPhase.Playing && game.run.elapsed > pausedTime, "PauseResumeButton listener + existing Tick");
            yield return Click("PauseButton");
            int seed = game.run.seed, stage = game.run.stage;
            yield return Click("RetryButton");
            CheckThat("retry retains seed", game.run.phase == RunPhase.Ready && game.run.seed == seed && game.run.stage == stage, "Actual RetryButton listener");
            yield return Click("NewSeedButton");
            CheckThat("dice changes seed", game.run.phase == RunPhase.Ready && game.run.seed == seed + 1 && game.run.stage == stage, "Actual NewSeedButton listener");
            yield return Click("HelpButton"); CheckThat("help button opens overlay", ui.Menus.HasOverlay, "Actual HelpButton listener");
            CheckBlockedBehindOverlay("StartButton");
            yield return Capture("help-1280x720.png", "Actual HelpButton opened the help overlay");
            string[] tutorialButtons = { "RepairTutorialButton", "FlipTutorialButton", "DeliveryTutorialButton" };
            string[] tutorialFiles = { "tutorial-repair.png", "tutorial-flip.png", "tutorial-delivery.png" };
            for (int i = 0; i < tutorialButtons.Length; i++)
            {
                if (i > 0) yield return Click("HelpButton");
                yield return Click(tutorialButtons[i]);
                yield return Capture(tutorialFiles[i], "Actual help/tutorial buttons; static tutorial illustrations, not played task completion");
                yield return Click("CloseOverlayButton");
            }
            CheckThat("help close returns", !ui.Menus.HasOverlay, "Actual CloseOverlayButton listener");
            yield return Click("SettingsButton"); CheckThat("settings button opens overlay", ui.Menus.HasOverlay, "Actual SettingsButton listener");
            yield return Capture("settings-1280x720.png", "Button-driven settings overlay");
            yield return Click("CloseOverlayButton");

            foreach (Vector2Int size in new[] { new Vector2Int(1280,720), new Vector2Int(1920,1080), new Vector2Int(1024,768) })
            {
                yield return Resize(size.x, size.y);
                for (int chapter = 0; chapter < 3; chapter++)
                {
                    LoadFixture(chapter, true);
                    if (chapter == 1)
                    {
                        foreach (TaskState state in new[] { TaskState.NotStarted, TaskState.Active, TaskState.Complete })
                        {
                            game.run.tasks[0].state = state; ui.RefreshNow();
                            CheckThat("M1 hidden " + state + " " + size, ui.Hud.HiddenTaskIsNeutralForReview && !ui.Hud.Minimap.HiddenTaskMarkerVisibleForReview,
                                "Explicit M1 state fixture: neutral HUD, no minimap marker");
                        }
                        game.run.tasks[0].state = TaskState.NotStarted; ui.RefreshNow();
                        CheckBeatRectangles();
                    }
                    foreach (Vector2 facing in new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left })
                    { game.run.facing = facing; ui.RefreshNow(); CheckMap(false); }
                    CheckButtonsInsideScreen();
                    yield return Capture("hud-stage-" + chapter + "-" + size.x + "x" + size.y + ".png", "Explicit stage HUD fixture; not travelled/unlocked through gameplay");
                    yield return Click("MapToggleButton");
                    CheckThat("map toggle expands", game.FullMap, "Actual MapToggleButton listener");
                    CheckMap(true);
                    if (chapter == 0) yield return Capture("map-full-" + size.x + "x" + size.y + ".png", "Expanded actual uGUI map, no legacy OnGUI sampling");
                    yield return Click("MapToggleButton");
                    CheckThat("map toggle contracts", !game.FullMap, "Actual MapToggleButton listener");
                }
            }

            yield return Resize(1280, 720);
            LoadFixture(0, true);
            CheckFxSequences();
            yield return CheckSameRunResultFxCleanup();
            LoadResultFixture(0, false);
            int unlockedBefore = game.UnlockedStageForUI;
            yield return null; ui.RefreshNow();
            CheckThat("timeout blocks next", !ui.Menus.NextAvailable && ui.FindButton("NextButton") == null, "Explicit timeout/all-optional-tasks-complete result fixture");
            CheckThat("timeout does not unlock", game.UnlockedStageForUI == unlockedBefore, "Task stars do not authorize progression");
            yield return Capture("result-timeout.png", "Explicit result fixture, not actual elapsed route; all optional tasks complete but timeout");
            yield return Click("RetryButton");
            CheckThat("timeout retry returns ready", game.run.phase == RunPhase.Ready && game.run.stage == 0, "Actual result RetryButton listener");
            LoadResultFixture(0, true);
            yield return null; ui.RefreshNow();
            CheckThat("success enables next", ui.Menus.NextAvailable && ui.FindButton("NextButton") && ui.FindButton("NextButton").interactable, "Explicit successful result fixture");
            yield return Capture("result-success.png", "Explicit three-star success fixture; no full-route claim");
            yield return Click("NextButton");
            CheckThat("next advances one stage", game.run.stage == 1 && game.run.phase == RunPhase.Ready, "Actual NextButton listener");
            LoadResultFixture(2, true); yield return null; ui.RefreshNow();
            yield return Click("NextButton");
            CheckThat("final chapter loops to street", game.run.stage == 0 && game.run.phase == RunPhase.Ready, "Actual final result NextButton listener");
        }

        void LoadFixture(int chapter, bool playing)
        {
            game.LoadModelForReview(new RunV9(chapter, productionSeed));
            game.SetExpandedMap(false);
            if (playing) game.run.Begin();
            game.FrameForReview(); game.RenderFrame(); ui.RefreshNow(); Canvas.ForceUpdateCanvases();
        }
        void LoadResultFixture(int chapter, bool success)
        {
            LoadFixture(chapter, false);
            foreach (var task in game.run.tasks) task.state = TaskState.Complete;
            game.run.success = success; game.run.elapsed = success ? 123.0 : game.run.limit;
            game.run.stars = RunV9.Score(success, game.run.Completed); game.run.phase = RunPhase.Results;
            ui.RefreshNow();
        }
        IEnumerator Click(string name)
        {
            // Newly created/activated Graphics need a rendered frame before GraphicRaycaster depth is valid.
            // ForceUpdateCanvases alone performs layout/mesh rebuilds but does not establish rendering depth.
            ui.RefreshNow(); Canvas.ForceUpdateCanvases();
            yield return null;
            yield return new WaitForEndOfFrame();
            Button button = ui.FindButton(name);
            if (!button || !button.interactable) throw new InvalidOperationException("Required enabled button missing: " + name);
            CheckButtonRaycast(button, true);
            button.onClick.Invoke(); ui.RefreshNow(); Canvas.ForceUpdateCanvases();
            // The listener may construct an overlay; callers may immediately test its blocking raycast.
            yield return null;
            yield return new WaitForEndOfFrame();
        }
        void CheckButtonRaycast(Button button, bool shouldHit)
        {
            Canvas.ForceUpdateCanvases();
            EventSystem events = EventSystem.current;
            if (!events) { CheckThat("GraphicRaycaster " + button.name, false, "EventSystem missing"); return; }
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            Camera camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            events.RaycastAll(pointer, hits);
            Button hitButton = hits.Count > 0 ? hits[0].gameObject.GetComponentInParent<Button>() : null;
            bool reached = hits.Count > 0 && hits[0].module is GraphicRaycaster && hitButton == button;
            CheckThat("GraphicRaycaster " + button.name + (shouldHit ? " reachable" : " blocked"), reached == shouldHit,
                "screen=" + point + " top=" + (hits.Count > 0 ? hits[0].gameObject.name : "none") + "; synthetic pointer data, no OS mouse event");
        }
        void CheckBlockedBehindOverlay(string name)
        {
            Button button = ui.FindButton(name);
            if (button) CheckButtonRaycast(button, false);
            else CheckThat("overlay hides " + name, true, "Underlying button is inactive and cannot receive a raycast");
        }
        void CheckBeatRectangles()
        {
            TaskV9 capsule = game.run.tasks[1]; TaskState previous = capsule.state;
            capsule.state = TaskState.Active; ui.RefreshNow(); Canvas.ForceUpdateCanvases();
            Image window = null, cursor = null;
            foreach (Image image in ui.Hud.Root.GetComponentsInChildren<Image>(false))
            {
                if (image.name == "BeatWindow") window = image;
                if (image.name == "BeatCursor") cursor = image;
            }
            float ratio = Mathf.Clamp01(game.run.config.beatWindow / game.run.config.beatPeriod);
            CheckBeatMesh(window, "active timing window", 170 * ratio, 9);
            CheckBeatMesh(cursor, "timing cursor", 3, 17);
            capsule.state = previous; ui.RefreshNow();
        }
        void CheckBeatMesh(Image image, string label, float width, float height)
        {
            if (!image) { CheckThat("beat " + label, false, "Active Image missing in capsule fixture"); return; }
            Mesh mesh = image.canvasRenderer.GetMesh();
            if (!mesh || mesh.vertexCount < 4)
            { CheckThat("beat " + label, false, "CanvasRenderer has no populated quad"); return; }
            {
                // GetMesh returns the renderer's mesh in Unity 6000; read it without mutating or destroying it.
                Vector3[] vertices = mesh.vertices;
                var bounds = new Bounds(vertices[0], Vector3.zero);
                foreach (Vector3 vertex in vertices) bounds.Encapsulate(vertex);
                Vector3 size = bounds.size;
                bool correct = !image.preserveAspect && Mathf.Abs(image.rectTransform.rect.width - width) < .05f &&
                    Mathf.Abs(image.rectTransform.rect.height - height) < .05f && mesh.vertexCount >= 4 &&
                    Mathf.Abs(size.x - width) < 1.1f && Mathf.Abs(size.y - height) < 1.1f;
                CheckThat("beat " + label, correct, "preserveAspect=" + image.preserveAspect + " target=" + width + "x" + height +
                    " actual CanvasRenderer mesh=" + size.x + "x" + size.y + " at " + Screen.width + "x" + Screen.height);
            }
        }
        void CheckMap(bool expanded)
        {
            Canvas.ForceUpdateCanvases();
            var map = ui.Hud.Minimap;
            var projection = game.CreateMinimapProjection(map.PanelRect, expanded);
            Vector2 expectedGui = projection.WorldToGui(game.run.player);
            Vector2 expectedScreen = RushMinimapProjectionV13.GuiToScreenPixel(expectedGui, Screen.width, Screen.height);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, map.PlayerMarkerForReview.TransformPoint(map.PlayerMarkerForReview.rect.center));
            Vector2 actualScreen = new Vector2(screen.x, Screen.height - screen.y);
            var observation = new MapCheck { width = Screen.width, height = Screen.height, stage = game.run.stage, expanded = expanded,
                actualPanel = map.PanelRect, worldPlayer = game.run.player, projectedGui = map.ProjectPlayerForReview, expectedGui = expectedGui,
                actualMarkerScreenTopLeft = actualScreen, expectedMarkerScreenTopLeft = expectedScreen,
                projectionError = Vector2.Distance(map.ProjectPlayerForReview, expectedGui), renderedMarkerError = Vector2.Distance(actualScreen, expectedScreen) };
            report.maps.Add(observation);
            CheckThat("actual uGUI map marker", observation.projectionError < .1f && observation.renderedMarkerError < 1.5f,
                "stage=" + game.run.stage + " " + Screen.width + "x" + Screen.height + " expanded=" + expanded + " projectionError=" + observation.projectionError + " pixelError=" + observation.renderedMarkerError);
        }
        void CheckButtonsInsideScreen()
        {
            foreach (Button button in ui.GetComponentsInChildren<Button>(false))
            {
                Vector3[] corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
                bool inside = true;
                foreach (Vector3 corner in corners)
                { Vector2 point = RectTransformUtility.WorldToScreenPoint(null, corner); inside &= point.x >= -2 && point.x <= Screen.width + 2 && point.y >= -2 && point.y <= Screen.height + 2; }
                CheckThat("button viewport " + button.name, inside, Screen.width + "x" + Screen.height);
            }
        }
        void CheckFxSequences()
        {
            ui.ReducedMotion = false;
            foreach (string key in new[] { "success_sparkle", "impact_puff", "failure_spill" })
            {
                var frames = new Sprite[6]; var unique = new HashSet<Sprite>();
                for (int i = 0; i < frames.Length; i++) { frames[i] = U15.Sprite(key + ".frame_" + i.ToString("00")); unique.Add(frames[i]); }
                CheckThat(key + " six frames", unique.Count == 6, "Six distinct imported Sprites, no missing frame");
                ui.PlayFx(key, new Vector2(640, 360));
                EarlyRushUISequence sequence = null;
                foreach (var candidate in ui.GetComponentsInChildren<EarlyRushUISequence>(false)) if (candidate.IsPlaying) { sequence = candidate; break; }
                CheckThat(key + " integrated pool playback", sequence != null, "RushUIV15.PlayFx created or reused a playing UI Image sequence");
                if (!sequence) continue;
                sequence.Clock = EarlyRushUISequence.ClockMode.Manual; sequence.SetPaused(false);
                var image = sequence.GetComponent<Image>();
                int completion = 0; Action completed = () => completion++; sequence.Completed += completed;
                float fps = key == "impact_puff" ? 18 : 12;
                CheckThat(key + " first frame", image.sprite == frames[0], "Actual pool Image.sprite");
                sequence.Advance(1.01f / fps); Sprite atPause = image.sprite; sequence.SetPaused(true); sequence.Advance(1);
                CheckThat(key + " pause", image.sprite == atPause && completion == 0, "Manual sequence clock paused");
                sequence.SetPaused(false);
                for (int i = 2; i < 6; i++) { sequence.Advance(1.0f / fps); CheckThat(key + " frame " + i, image.sprite == frames[i], "Actual pool Image.sprite"); }
                sequence.Advance(1.0f / fps); sequence.Advance(1);
                CheckThat(key + " completion and pooling", completion == 1 && !sequence.gameObject.activeSelf, "One completion callback; root pool hides finished FX");
                sequence.Completed -= completed;
            }
        }

        IEnumerator CheckSameRunResultFxCleanup()
        {
            // Explicit focused UI fixture: test clock separation independently of the QA window's OS focus.
            ui.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
            ui.PlayFx("impact_puff", new Vector2(640, 360), false);
            EarlyRushUISequence gameplayFx = null;
            foreach (var candidate in ui.GetComponentsInChildren<EarlyRushUISequence>(false))
                if (candidate.IsPlaying && candidate.Clock == EarlyRushUISequence.ClockMode.Manual) { gameplayFx = candidate; break; }
            CheckThat("gameplay FX active before results", gameplayFx != null, "Actual PlayFx pool with Manual clock");
            RunV9 sameRun = game.run;
            game.run.success = false; game.run.stars = 0; game.run.phase = RunPhase.Results;
            ui.RefreshNow();
            CheckThat("same-run results cancels gameplay FX", ReferenceEquals(sameRun, game.run) && gameplayFx &&
                !gameplayFx.gameObject.activeSelf && !gameplayFx.IsPlaying,
                "Changed phase on same RunV9 instance; effect must be cancelled and hidden, not left paused above results");

            ui.PlayFx("success_sparkle", new Vector2(640, 360), true);
            EarlyRushUISequence menuFx = null;
            foreach (var candidate in ui.GetComponentsInChildren<EarlyRushUISequence>(false))
                if (candidate.IsPlaying && candidate.Clock == EarlyRushUISequence.ClockMode.Unscaled) { menuFx = candidate; break; }
            ui.RefreshNow();
            CheckThat("results menu FX remains usable", menuFx && menuFx.gameObject.activeSelf && menuFx.IsPlaying && !menuFx.IsPaused,
                "Explicit focused fixture; menu Unscaled effect remains active while RunV9 is Results");
            if (menuFx)
            {
                Sprite first = menuFx.GetComponent<Image>().sprite;
                double elapsed = game.run.elapsed;
                int completions = 0;
                bool frameChanged = false, gameplayTimeChanged = false;
                Action completed = () => completions++;
                menuFx.Completed += completed;
                try
                {
                    float finishUntil = Time.realtimeSinceStartup + 2;
                    while (completions == 0 && menuFx.gameObject.activeSelf && Time.realtimeSinceStartup < finishUntil)
                    {
                        yield return null;
                        frameChanged |= menuFx.GetComponent<Image>().sprite != first;
                        gameplayTimeChanged |= game.run.elapsed != elapsed;
                    }
                    yield return null; // Keep listening one more update to reject repeated completion callbacks.
                    // A long rendered frame may legitimately finish all six frames before observation.
                    // Completion is accepted only via the real Completed event, never merely inactivity/cancellation.
                    CheckThat("menu FX advances without gameplay time", (frameChanged || completions == 1) && !gameplayTimeChanged && game.run.elapsed == elapsed,
                        "Actual Unscaled Update playback; frameChanged=" + frameChanged + " completed=" + completions + "; no manual Advance or RunV9.Tick");
                    CheckThat("menu FX completes in results", completions == 1 && !menuFx.gameObject.activeSelf && !menuFx.IsPlaying,
                        "Exactly one real completion event, then pooled; cancellation alone does not pass");
                }
                finally { menuFx.Completed -= completed; }
            }
            ui.SendMessage("OnApplicationFocus", Application.isFocused, SendMessageOptions.DontRequireReceiver);
        }

        IEnumerator Resize(int width, int height, bool forceWarmup = false)
        {
            if (!surfaceWarmed || forceWarmup)
            {
                // A hidden Windows player can initially keep a black swapchain at unchanged dimensions.
                // Force a real size transition before requesting the target; never waive screenshot checks.
                int warmWidth = Mathf.Max(640, width - 16), warmHeight = Mathf.Max(360, height - 16);
                Screen.SetResolution(warmWidth, warmHeight, FullScreenMode.Windowed);
                float warmEnd = Time.realtimeSinceStartup + 3;
                do { yield return null; } while ((Screen.width != warmWidth || Screen.height != warmHeight) && Time.realtimeSinceStartup < warmEnd);
                yield return new WaitForEndOfFrame(); yield return new WaitForEndOfFrame();
                surfaceWarmed = true;
            }
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            float end = Time.realtimeSinceStartup + 8;
            do { yield return null; } while ((Screen.width != width || Screen.height != height) && Time.realtimeSinceStartup < end);
            yield return new WaitForEndOfFrame(); yield return new WaitForEndOfFrame();
            CheckThat("requested resolution", Screen.width == width && Screen.height == height,
                "requested=" + width + "x" + height + " actual=" + Screen.width + "x" + Screen.height);
        }
        IEnumerator Capture(string name, string scope)
        {
            // Let menu entry/award tweens settle; gameplay Tick remains explicitly controlled by this suite.
            float settle = Time.realtimeSinceStartup + .75f;
            while (Time.realtimeSinceStartup < settle) yield return null;
            string path = Path.Combine(output, name);
            int width = Screen.width, height = Screen.height, capturedWidth = 0, capturedHeight = 0, attempts = 0;
            float fraction = 0; bool valid = false;
            for (int attempt = 0; attempt < 3 && !valid; attempt++)
            {
                attempts++;
                ui.RefreshNow(); Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
                if (File.Exists(path)) File.Delete(path);
                ScreenCapture.CaptureScreenshot(path);
                yield return new WaitForEndOfFrame();
                byte[] bytes = null; float end = Time.realtimeSinceStartup + 6;
                while (bytes == null && Time.realtimeSinceStartup < end)
                {
                    if (File.Exists(path))
                    {
                        try { byte[] candidate = File.ReadAllBytes(path); if (candidate.Length > 24 && candidate[candidate.Length - 8] == 73 && candidate[candidate.Length - 7] == 69 && candidate[candidate.Length - 6] == 78 && candidate[candidate.Length - 5] == 68) bytes = candidate; }
                        catch (IOException) { }
                    }
                    if (bytes == null) yield return null;
                }
                if (bytes == null) throw new IOException("Screenshot did not finish writing: " + name);
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                try
                {
                    if (!texture.LoadImage(bytes)) throw new IOException("Invalid screenshot PNG: " + name);
                    int visible = 0, samples = 0;
                    for (int y = 16; y < texture.height; y += 32) for (int x = 16; x < texture.width; x += 32)
                    { Color color = texture.GetPixel(x, y); if (color.r + color.g + color.b > .12f) visible++; samples++; }
                    fraction = samples > 0 ? visible / (float)samples : 0;
                    capturedWidth = texture.width; capturedHeight = texture.height;
                    valid = capturedWidth == width && capturedHeight == height && fraction > .05f;
                }
                finally { Destroy(texture); }
                if (!valid && attempt < 2)
                {
                    Debug.Log("V15_UI_CAPTURE_RETRY " + name + " attempt=" + attempts + " nonblack=" + fraction);
                    yield return Resize(width, height, true);
                }
            }
            CheckThat("rendered screenshot " + name, valid, "actual PNG=" + capturedWidth + "x" + capturedHeight + " nonblack=" + fraction + " attempts=" + attempts);
            report.screenshots.Add(new Screenshot { file = name, phase = game.run.phase.ToString(), scope = scope,
                width = capturedWidth, height = capturedHeight, stage = game.run.stage, seed = game.run.seed, nonBlackFraction = fraction, captureAttempts = attempts });
        }

        void CheckThat(string name, bool passed, string evidence) { report.checks.Add(new Check { name = name, passed = passed, evidence = evidence }); }
        void OnLog(string message, string stack, LogType type)
        { if (!finished && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) report.errors.Add(message + "\n" + stack); }
        void BackupPreferences()
        {
            foreach (string key in new[] { "V10Unlocked", "V9Best0", "V9Best1", "V9Best2", "V15_UiMuted", "V15_ReducedMotion" })
                preferences.Add(new SavedPreference { key = key, existed = PlayerPrefs.HasKey(key), integer = PlayerPrefs.GetInt(key, 0) });
            preferences.Add(new SavedPreference { key = "V15_UiVolume", existed = PlayerPrefs.HasKey("V15_UiVolume"), isFloat = true, number = PlayerPrefs.GetFloat("V15_UiVolume", .8f) });
            backedUp = true;
        }
        void RestorePreferences()
        {
            if (!backedUp || restored) return;
            foreach (var item in preferences)
            {
                if (!item.existed) PlayerPrefs.DeleteKey(item.key);
                else if (item.isFloat) PlayerPrefs.SetFloat(item.key, item.number);
                else PlayerPrefs.SetInt(item.key, item.integer);
            }
            PlayerPrefs.Save(); AudioListener.volume = previousAudioVolume; restored = true;
            report.preferencesRestored = true;
        }
        void Finish()
        {
            if (finished) return;
            RestorePreferences();
            finished = true; report.passed = report.errors.Count == 0 && report.preferencesRestored;
            foreach (var check in report.checks) report.passed &= check.passed;
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(output, "README.txt"), report.scope + "\nPreferences are restored in normal/error/watchdog shutdown paths. Screenshots explicitly identify fixtures.\n");
            Debug.Log("V15_UI_QA " + (report.passed ? "PASS" : "FAIL") + " checks=" + report.checks.Count + " screenshots=" + report.screenshots.Count + " errors=" + report.errors.Count);
            Application.logMessageReceived -= OnLog;
            Application.Quit(report.passed ? 0 : 1);
        }
        void OnApplicationQuit() { RestorePreferences(); }
        void OnDestroy() { RestorePreferences(); Application.logMessageReceived -= OnLog; }
    }
}
