using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using EarlyRush.V9;

namespace EarlyRush.V15
{
    /// <summary>Icon-first menus. The host remains the owner of simulation, progression and input.</summary>
    public sealed class RushMenusV15
    {
        enum Overlay { None, Help, Tutorial, Task, Settings }

        readonly RushUIV15 owner;
        readonly RectTransform parent;
        RectTransform pageRoot, overlayRoot;
        CanvasGroup pageInput;
        RunV9 run, overlayRun, resultRun;
        RunPhase renderedPhase;
        Overlay overlay;
        bool resumeAfterOverlay, dirty, disposed;
        string tutorialKind = "repair", taskId;
        float resultAge;
        readonly Image[] resultStars = new Image[3];
        readonly bool[] starEarned = new bool[3], starSparkled = new bool[3];
        Text volumeValue;
        Slider volumeSlider;

        static readonly string[] ChapterIcons = { "stage_street", "stage_metro", "stage_office" };
        static readonly string[] ChapterArt = { "chapter_street", "chapter_metro", "chapter_office" };
        static readonly string[] ChapterNames = { "街道通勤", "地铁通勤", "办公室早会" };
        static readonly string[][] TaskIds = {
            new[] { "S1", "S2", "S3" }, new[] { "M1", "M2", "M3" }, new[] { "O1", "O2", "O3" }
        };

        public bool HasOverlay => overlay != Overlay.None;
        public bool NextAvailable => run != null && run.phase == RunPhase.Results && run.success;

        public RushMenusV15(RushUIV15 owner, RectTransform parent)
        {
            this.owner = owner;
            this.parent = parent;
        }

        public void Refresh(RunV9 current, float menuDt)
        {
            if (disposed) return;
            if (!ReferenceEquals(run, current))
            {
                CloseOverlayInternal(false);
                run = current;
                dirty = true;
            }
            if (run == null)
            {
                DestroyRoot(ref pageRoot);
                return;
            }
            if (dirty || pageRoot == null || renderedPhase != run.phase)
            {
                dirty = false;
                renderedPhase = run.phase;
                BuildPage();
            }
            if (run.phase == RunPhase.Results) UpdateResultStars(Mathf.Max(0, menuDt));
            if (volumeValue != null)
                volumeValue.text = Mathf.RoundToInt(owner.Volume * 100) + "%";
            if (volumeSlider != null && !Mathf.Approximately(volumeSlider.value, owner.Volume))
                volumeSlider.SetValueWithoutNotify(owner.Volume);
        }

        public void ToggleHelp()
        {
            if (overlay == Overlay.Help) CloseOverlay();
            else OpenOverlay(Overlay.Help);
        }

        public void ShowTutorial(string kind)
        {
            switch ((kind ?? "").ToLowerInvariant())
            {
                case "s2": case "repair": tutorialKind = "repair"; break;
                case "s3": case "flip": tutorialKind = "flip"; break;
                case "o2": case "delivery": tutorialKind = "delivery"; break;
                default: ShowTaskHelp(kind); return;
            }
            OpenOverlay(Overlay.Tutorial);
        }

        public void ShowTaskHelp(string id)
        {
            if (id == "S2" || id == "S3" || id == "O2") { ShowTutorial(id); return; }
            if (id == "M1" && (run == null || run.phase != RunPhase.Results)) return;
            taskId = id;
            OpenOverlay(Overlay.Task);
        }

        public void CloseOverlay() => CloseOverlayInternal(true);

        public bool CloseOverlayIfOpen()
        {
            if (!HasOverlay) return false;
            CloseOverlay();
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CloseOverlayInternal(false);
            DestroyRoot(ref pageRoot);
            run = null;
        }

        void BuildPage()
        {
            DestroyRoot(ref pageRoot);
            pageRoot = U15.Rect(parent, "Menus", new Rect(0, 0, 1280, 720));
            pageInput = pageRoot.gameObject.AddComponent<CanvasGroup>();
            pageInput.interactable = pageInput.blocksRaycasts = !HasOverlay;
            Array.Clear(resultStars, 0, resultStars.Length);
            if (run.phase == RunPhase.Ready) BuildReady();
            else if (run.phase == RunPhase.Paused) BuildPause();
            else if (run.phase == RunPhase.Results) BuildResults();
            if (overlayRoot != null) overlayRoot.SetAsLastSibling();
        }

        void BuildReady()
        {
            Background(pageRoot);
            Panel(pageRoot, "TimerPanel", new Rect(483, 35, 314, 83));
            Picture(pageRoot, "TimerIcon", "stopwatch", new Rect(504, 48, 59, 59));
            U15.Text(pageRoot, "StageLimit", TimeSpan.FromSeconds(run.limit).ToString(@"mm\:ss"), new Rect(567, 49, 201, 56), 42);

            int unlocked = owner.Host.UnlockedStageForUI;
            for (int i = 0; i < 3; i++)
            {
                int chapter = i;
                bool available = chapter <= unlocked;
                bool selected = chapter == run.stage;
                float x = 106 + i * 357;
                var card = U15.Rect(pageRoot, "Chapter" + chapter, new Rect(x, 150, 326, 404));
                var cardBackground = Panel(card, "Chapter" + chapter + "Button", new Rect(0, 0, 326, 404));
                var cardButton = cardBackground.gameObject.AddComponent<Button>();
                cardButton.targetGraphic = cardBackground;
                cardBackground.raycastTarget = true;
                cardButton.interactable = available;
                U15.Tooltip(cardButton.gameObject, available ? ChapterNames[i] : "完成上一章后解锁");
                cardButton.onClick.AddListener(() => { if (chapter <= owner.Host.UnlockedStageForUI) owner.Host.SelectStageFromUI(chapter); });
                var scene = Picture(card, "ChapterScene", ChapterArt[i], new Rect(8, 9, 310, 301));
                scene.color = available ? Color.white : new Color(.68f, .74f, .72f, .72f);
                Picture(card, "ChapterSymbol", ChapterIcons[i], new Rect(19, 18, 48, 48));
                if (selected) Picture(card, "SelectedOutline", "component.card_frame_active", new Rect(-5, -5, 336, 414), false);
                for (int t = 0; t < 3; t++)
                {
                    string id = TaskIds[i][t];
                    string icon = id == "M1" ? "task_m1_neutral" : U15.TaskIcon(id);
                    Picture(card, "TaskBadge" + t, "component.badge_cream", new Rect(29 + 94 * t, 315, 74, 74));
                    Picture(card, "TaskPreview" + t, icon, new Rect(39 + 94 * t, 325, 54, 54));
                }
                if (!available)
                {
                    Picture(card, "LockedBadge", "component.badge_cream", new Rect(122, 234, 82, 82));
                    Picture(card, "Locked", "lock", new Rect(132, 244, 62, 62));
                }
                if (selected)
                {
                    Picture(card, "SelectedBadge", "component.badge_teal", new Rect(270, -21, 68, 68));
                    Picture(card, "SelectedCheck", "check", new Rect(281, -10, 46, 46));
                }
                if (i < 2) Picture(pageRoot, "ChapterConnector" + i, "next", new Rect(x + 329, 328, 26, 26));
            }

            var start = U15.Button(pageRoot, "StartButton", "component.glyph_play_cream", new Rect(470, 592, 340, 82), () => owner.Host.BeginFromUI(), "出发", true);
            U15.Button(pageRoot, "NewSeedButton", "dice", new Rect(835, 604, 62, 62), () => owner.Host.RetryFromUI(true), "换地图种子");
            BuildUtilities(pageRoot, 642);
            Select(start);
        }

        void BuildPause()
        {
            Blocker(pageRoot, "PauseBackdrop", new Color(.05f, .17f, .17f, .62f));
            Panel(pageRoot, "PausePanel", new Rect(410, 115, 460, 495));
            Picture(pageRoot, "PauseHeading", "pause", new Rect(591, 144, 98, 98));
            var resume = U15.Button(pageRoot, "PauseResumeButton", "component.glyph_play_cream", new Rect(492, 267, 296, 76), () => owner.Host.TogglePauseFromUI(), "继续", true);
            U15.Button(pageRoot, "RetryButton", "retry", new Rect(462, 379, 96, 76), () => owner.Host.RetryFromUI(false), "同种子重试");
            U15.Button(pageRoot, "NewSeedButton", "dice", new Rect(592, 379, 96, 76), () => owner.Host.RetryFromUI(true), "换种子挑战");
            U15.Button(pageRoot, "HomeButton", "home", new Rect(722, 379, 96, 76), () => owner.Host.ReturnToReadyFromUI(), "返回准备页");
            U15.Button(pageRoot, "HelpButton", "help", new Rect(504, 495, 66, 66), ToggleHelp, "操作与教学");
            U15.Button(pageRoot, "SettingsButton", "settings", new Rect(607, 495, 66, 66), OpenSettings, "设置");
            SoundButton(pageRoot, new Rect(710, 495, 66, 66));
            Select(resume);
        }

        void BuildResults()
        {
            Background(pageRoot);
            Blocker(pageRoot, "ResultsBackdrop", new Color(.08f, .18f, .18f, .12f));
            Panel(pageRoot, "ResultsPanel", new Rect(339, 30, 602, 660));
            Picture(pageRoot, "Destination", run.stage == 0 ? "metro_entrance" : ChapterArt[run.stage], new Rect(536, 69, 251, 246));
            Picture(pageRoot, "Commuter", run.success ? "commuter_happy" : "commuter_tired", new Rect(397, 120, 211, 211));
            Picture(pageRoot, "ResultStopwatch", "stopwatch", new Rect(730, 179, 104, 104));
            Picture(pageRoot, "ArrivalStatus", run.success ? "check" : "cross", new Rect(787, 239, 59, 59));
            if (!run.success) U15.Text(pageRoot, "Timeout", "00:00", new Rect(717, 284, 125, 29), 25, TextAnchor.MiddleCenter, U15.Coral);

            if (!ReferenceEquals(resultRun, run))
            {
                resultRun = run;
                resultAge = 0;
                Array.Clear(starSparkled, 0, starSparkled.Length);
            }
            starEarned[0] = run.success;
            starEarned[1] = run.Completed >= 1;
            starEarned[2] = run.Completed == 3;
            for (int i = 0; i < 3; i++)
            {
                Rect r = new Rect(480 + i * 113, 325, 94, 94);
                Picture(pageRoot, "StarOutline" + i, "star_outline", r);
                resultStars[i] = Picture(pageRoot, "StarFilled" + i, "star_filled", r);
                resultStars[i].rectTransform.pivot = new Vector2(.5f, .5f);
                resultStars[i].rectTransform.anchoredPosition = new Vector2(r.center.x, -r.center.y);
                resultStars[i].gameObject.SetActive(owner.ReducedMotion && starEarned[i]);
            }

            Panel(pageRoot, "TaskResults", new Rect(402, 448, 476, 114));
            for (int i = 0; i < run.tasks.Count && i < 3; i++)
            {
                var task = run.tasks[i];
                float x = 430 + i * 151;
                Picture(pageRoot, "ResultTask" + task.id, task.id == "M1" ? "task_m1_alighting" : U15.TaskIcon(task.id), new Rect(x, 461, 76, 76));
                string status = task.state == TaskState.Complete ? "check" : task.state == TaskState.Failed ? "cross" : "component.slot_empty";
                Picture(pageRoot, "ResultTaskState" + task.id, status, new Rect(x + 55, 512, 35, 35));
            }
            U15.Button(pageRoot, "HomeButton", "home", new Rect(38, 36, 72, 72), () => owner.Host.ReturnToReadyFromUI(), "返回准备页");
            if (run.success)
            {
                var next = U15.Button(pageRoot, "NextButton", "component.glyph_next_cream", new Rect(420, 593, 327, 64), NextStage, run.stage == 2 ? "再来一轮" : "下一关", true);
                U15.Button(pageRoot, "RetryButton", "retry", new Rect(775, 593, 84, 64), () => owner.Host.RetryFromUI(false), "重试本关");
                Select(next);
            }
            else
            {
                var retry = U15.Button(pageRoot, "RetryButton", "component.glyph_retry_cream", new Rect(473, 593, 334, 64), () => owner.Host.RetryFromUI(false), "重试本关", true);
                Select(retry);
            }
        }

        void UpdateResultStars(float dt)
        {
            resultAge += dt;
            for (int i = 0; i < resultStars.Length; i++)
            {
                var star = resultStars[i];
                if (star == null) continue;
                float age = resultAge - (.25f + i * .18f);
                bool visible = starEarned[i] && (owner.ReducedMotion || age >= 0);
                star.gameObject.SetActive(visible);
                if (!visible) continue;
                float t = owner.ReducedMotion ? 1 : Mathf.Clamp01(age / .24f);
                float scale = t < .7f ? Mathf.Lerp(.55f, 1.1f, t / .7f) : Mathf.Lerp(1.1f, 1, (t - .7f) / .3f);
                star.rectTransform.localScale = Vector3.one * scale;
                if (!starSparkled[i])
                {
                    starSparkled[i] = true;
                    if (!owner.ReducedMotion) owner.PlayFx("success_sparkle", new Vector2(527 + i * 113, 372), true);
                }
            }
        }

        void NextStage()
        {
            if (!NextAvailable) return;
            owner.Host.SelectStageFromUI(run.stage == 2 ? 0 : run.stage + 1);
        }

        void BuildUtilities(RectTransform root, float y)
        {
            U15.Button(root, "SettingsButton", "settings", new Rect(34, y, 58, 58), OpenSettings, "设置");
            U15.Button(root, "HelpButton", "help", new Rect(110, y, 58, 58), ToggleHelp, "操作与教学");
            SoundButton(root, new Rect(1188, y, 58, 58));
        }

        void SoundButton(RectTransform root, Rect rect)
        {
            U15.Button(root, "SoundButton", owner.Muted ? "speaker_off" : "speaker_on", rect, () => {
                owner.ToggleSound(); dirty = true;
            }, owner.Muted ? "开启声音" : "静音");
        }

        void OpenSettings() => OpenOverlay(Overlay.Settings);

        void OpenOverlay(Overlay next)
        {
            if (disposed) return;
            RunV9 current = owner.Host.run;
            if (current == null) return;
            if (!ReferenceEquals(run, current)) Refresh(current, 0);
            if (!HasOverlay)
            {
                overlayRun = run;
                resumeAfterOverlay = run.phase == RunPhase.Playing;
                if (resumeAfterOverlay) owner.Host.TogglePauseFromUI();
            }
            overlay = next;
            if (pageInput != null) pageInput.interactable = pageInput.blocksRaycasts = false;
            BuildOverlay();
        }

        void BuildOverlay()
        {
            DestroyRoot(ref overlayRoot);
            volumeValue = null;
            volumeSlider = null;
            overlayRoot = U15.Rect(parent, "MenuOverlay", new Rect(0, 0, 1280, 720));
            Blocker(overlayRoot, "OverlayBackdrop", new Color(.04f, .15f, .16f, .7f));
            if (overlay == Overlay.Tutorial) BuildTutorial();
            else if (overlay == Overlay.Settings) BuildSettings();
            else if (overlay == Overlay.Task) BuildTaskHelp();
            else BuildHelp();
            overlayRoot.SetAsLastSibling();
        }

        void BuildHelp()
        {
            Panel(overlayRoot, "HelpPanel", new Rect(180, 62, 920, 596));
            Picture(overlayRoot, "HelpHeadingIcon", "help", new Rect(222, 88, 64, 64));
            U15.Text(overlayRoot, "HelpHeading", "操作与教学", new Rect(312, 97, 620, 45), 29);
            CloseButton(new Rect(1011, 91, 53, 53));
            HelpRow(169, "stage_street", "WASD / 方向键", "移动与转向");
            HelpRow(222, "action_dash", "Space", "点按冲刺 · 按住不会连发");
            HelpRow(275, "action_brace", "Shift", "按住稳步 / 稳车 · 抵挡冲撞");
            HelpRow(328, "rescue", "R", "救回附近未完成的物体");
            HelpRow(381, "map_expand", "Tab", "展开 / 收起地图");
            HelpRow(434, "pause", "Esc / F1", "暂停 / 帮助");
            U15.Button(overlayRoot, "RepairTutorialButton", "task_s2_wheel_repair", new Rect(276, 530, 188, 70), () => ShowTutorial("repair"), "推轮修车");
            U15.Button(overlayRoot, "FlipTutorialButton", "task_s3_flip", new Rect(546, 530, 188, 70), () => ShowTutorial("flip"), "联动翻板");
            U15.Button(overlayRoot, "DeliveryTutorialButton", "task_o2_document", new Rect(816, 530, 188, 70), () => ShowTutorial("delivery"), "按头像送文件");
        }

        void HelpRow(float y, string icon, string keys, string description)
        {
            Picture(overlayRoot, "HelpIcon" + y, icon, new Rect(244, y, 42, 42));
            U15.Text(overlayRoot, "HelpKeys" + y, keys, new Rect(308, y, 245, 42), 22, TextAnchor.MiddleLeft);
            U15.Text(overlayRoot, "HelpText" + y, description, new Rect(557, y, 443, 42), 21, TextAnchor.MiddleLeft);
        }

        void BuildTutorial()
        {
            string title = tutorialKind == "repair" ? "把两个车轮推回修理区" : tutorialKind == "flip" ? "碰撞翻板，同时翻转相邻板" : "文件和工位使用同一个头像";
            string icon = tutorialKind == "repair" ? "task_s2_wheel_repair" : tutorialKind == "flip" ? "task_s3_flip" : "task_o2_document";
            string[] captions = tutorialKind == "repair" ? new[] { "找到两个车轮", "推入框内，停稳", "全部归位后获得自行车" } :
                tutorialKind == "flip" ? new[] { "对照上方目标图案", "当前板与相邻板一起翻面", "所有图案与目标一致" } :
                new[] { "领取带头像的文件", "寻找相同头像的工位", "在工位前停稳完成交付" };
            Panel(overlayRoot, "TutorialPanel", new Rect(38, 126, 1204, 480));
            Picture(overlayRoot, "TutorialSymbol", icon, new Rect(79, 156, 61, 61));
            U15.Text(overlayRoot, "TutorialTitle", title, new Rect(167, 165, 916, 47), 27);
            CloseButton(new Rect(1153, 156, 54, 54));
            for (int i = 0; i < 3; i++)
            {
                float x = 75 + i * 383;
                Picture(overlayRoot, "TutorialStep" + (i + 1), "tutorial_" + tutorialKind + "_0" + (i + 1), new Rect(x, 247, 362, 205));
                U15.Text(overlayRoot, "TutorialCaption" + i, captions[i], new Rect(x - 4, 476, 370, 53), 21);
                if (i < 2) Picture(overlayRoot, "StepArrow" + i, "next", new Rect(x + 363, 329, 22, 32));
            }
            if (tutorialKind == "flip")
                U15.Text(overlayRoot, "FlipGoalNote", "图解只是示例，请以本局任务栏的目标图案为准。", new Rect(205, 545, 870, 28), 17);
        }

        void BuildTaskHelp()
        {
            string description = TaskDescription(taskId);
            string icon = taskId == "M1" ? "task_m1_alighting" : U15.TaskIcon(taskId);
            Panel(overlayRoot, "TaskHelpPanel", new Rect(325, 168, 630, 385));
            Picture(overlayRoot, "TaskHelpIcon", icon, new Rect(573, 206, 134, 134));
            U15.Text(overlayRoot, "TaskHelpDescription", description, new Rect(374, 368, 532, 112), 24);
            CloseButton(new Rect(871, 191, 53, 53));
        }

        string TaskDescription(string id)
        {
            switch (id)
            {
                case "S1": return "在早餐摊前停稳领取。\n保护早餐，等待进食进度完成。\n碰撞超过 " + run.config.breakfastTolerance + " 次会失败。";
                case "M1": return "先让下车乘客离开，再上车。\n这项挑战在结算时揭晓。";
                case "M2": return "退开，再在指针进入金色窗口时碰撞机器。\n成功增加进度，错过会减少进度。";
                case "M3": return "推动抹布覆盖整片水渍。\n进度条显示已经擦干的真实面积。";
                case "O1": return "留意端咖啡的同事。\n打翻超过 " + run.config.coffeeTolerance + " 杯会失败。\n准时抵达后结算。";
                case "O3": return "把办公椅推入任意空位后停稳。\n每个位置只能停一把椅子。";
                default: return "按时抵达终点即可推进。\n沿路任务可以选择完成，获得额外星星。";
            }
        }

        void BuildSettings()
        {
            Panel(overlayRoot, "SettingsPanel", new Rect(324, 139, 632, 441));
            Picture(overlayRoot, "SettingsHeadingIcon", "settings", new Rect(369, 176, 64, 64));
            U15.Text(overlayRoot, "SettingsHeading", "设置", new Rect(457, 184, 339, 44), 30);
            CloseButton(new Rect(869, 177, 53, 53));
            U15.Button(overlayRoot, "SoundButton", owner.Muted ? "speaker_off" : "speaker_on", new Rect(369, 285, 52, 52), () => {
                owner.ToggleSound();
                dirty = true;
                BuildOverlay();
            }, owner.Muted ? "开启声音" : "静音");
            U15.Text(overlayRoot, "VolumeLabel", "音量", new Rect(442, 254, 180, 35), 22, TextAnchor.MiddleLeft);
            volumeValue = U15.Text(overlayRoot, "VolumeValue", Mathf.RoundToInt(owner.Volume * 100) + "%", new Rect(778, 254, 93, 35), 22, TextAnchor.MiddleRight);
            var sliderRoot = U15.Rect(overlayRoot, "VolumeSlider", new Rect(441, 294, 425, 48));
            var slider = sliderRoot.gameObject.AddComponent<Slider>();
            slider.minValue = 0; slider.maxValue = 1; slider.wholeNumbers = false;
            var track = U15.Image(sliderRoot, "Track", "component.progress_track", new Rect(10, 17, 405, 14), new Color(.09f, .26f, .29f, .2f));
            track.raycastTarget = true;
            var fillArea = U15.Rect(sliderRoot, "FillArea", new Rect(10, 17, 405, 14));
            var fill = U15.Image(fillArea, "Fill", "component.progress_fill", new Rect(0, 0, 405, 14), U15.Teal);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.pivot = new Vector2(.5f, .5f);
            fill.rectTransform.anchoredPosition = Vector2.zero;
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = U15.Rect(sliderRoot, "HandleArea", new Rect(10, 0, 405, 48));
            var handle = U15.Image(handleArea, "Handle", "component.badge_teal", new Rect(-17, 7, 34, 34));
            handle.raycastTarget = true;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.SetValueWithoutNotify(owner.Volume);
            slider.onValueChanged.AddListener(value => { owner.Volume = value; });
            volumeSlider = slider;

            U15.Text(overlayRoot, "ReducedMotionLabel", "减弱动态效果", new Rect(371, 391, 347, 50), 24, TextAnchor.MiddleLeft);
            U15.Button(overlayRoot, "ReducedMotionButton", owner.ReducedMotion ? "check" : "cross", new Rect(783, 387, 82, 57), () => {
                owner.ReducedMotion = !owner.ReducedMotion;
                BuildOverlay();
            }, owner.ReducedMotion ? "已开启，点击关闭" : "已关闭，点击开启");
            U15.Text(overlayRoot, "ReducedMotionHint", "保留状态与结果，减少弹跳和装饰闪光。", new Rect(373, 460, 495, 46), 18);
        }

        void CloseButton(Rect rect)
        {
            var close = U15.Button(overlayRoot, "CloseOverlayButton", "cross", rect, CloseOverlay, "关闭");
            Select(close);
        }

        void CloseOverlayInternal(bool resume)
        {
            bool shouldResume = resume && resumeAfterOverlay && ReferenceEquals(owner.Host.run, overlayRun) && overlayRun != null && overlayRun.phase == RunPhase.Paused;
            overlay = Overlay.None;
            resumeAfterOverlay = false;
            overlayRun = null;
            volumeValue = null;
            volumeSlider = null;
            DestroyRoot(ref overlayRoot);
            if (pageInput != null) pageInput.interactable = pageInput.blocksRaycasts = true;
            if (shouldResume) owner.Host.TogglePauseFromUI();
            dirty = true;
        }

        static Image Picture(RectTransform root, string name, string id, Rect rect, bool preserve = true)
        {
            var image = U15.Image(root, name, id, rect);
            image.preserveAspect = preserve;
            image.raycastTarget = false;
            return image;
        }

        static Image Panel(RectTransform root, string name, Rect rect)
        {
            var panel = U15.Image(root, name, "component.panel_cream", rect);
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = false;
            return panel;
        }

        static void Background(RectTransform root)
        {
            Picture(root, "MenuTownBackground", "menu_town_background", new Rect(0, 0, 1280, 720), false);
            Blocker(root, "BackgroundWash", new Color(1, .97f, .88f, .22f));
        }

        static void Blocker(RectTransform root, string name, Color color)
        {
            var image = U15.Image(root, name, "component.white_pixel", new Rect(0, 0, 1280, 720), color);
            image.preserveAspect = false;
            image.raycastTarget = true;
        }

        static void Select(Button button)
        {
            if (EventSystem.current != null && button != null && button.IsInteractable())
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        static void DestroyRoot(ref RectTransform root)
        {
            if (root == null) return;
            root.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(root.gameObject);
            root = null;
        }
    }
}
