using EarlyRush.V9;
using UnityEngine;
using UnityEngine.UI;

namespace EarlyRush.V15
{
    /// <summary>Read-only HUD binding. All completion, timing and movement decisions stay in RunV9.</summary>
    public sealed class RushHudV15
    {
        sealed class TaskSlot
        {
            public RectTransform Root;
            public Button Button;
            public Image Icon, State, ProgressTrack, ProgressFill, Risk, BeatTrack, BeatWindow, BeatCursor;
            public Text Number, RiskNumber, Secondary;
            public readonly Image[] Goal = new Image[5], Current = new Image[5];
            public readonly Image[] Counters = new Image[6], Portraits = new Image[5], Delivered = new Image[5];
            public readonly bool[] BoardShown = new bool[5], BoardTarget = new bool[5];
            public readonly float[] BoardFlipLeft = new float[5];
            public Image GoalLegend, CurrentLegend;
            public TaskState PreviousState;
            public int PreviousCount;
            public float Pulse;
            public string DisplayId;
            public bool WasHidden;
        }

        readonly RushUIV15 owner;
        readonly Text timer;
        readonly Image timeRing, dashRing, dashIcon, braceIcon, travelIcon;
        readonly TaskSlot[] slots = new TaskSlot[3];
        RunV9 boundRun;
        int previousDashLaunches, previousPlayerLaunches, previousBraceBlocks;
        float actionPulse;

        public RectTransform Root { get; }
        public RushMinimapV15 Minimap { get; }
        public bool HiddenTaskIsNeutralForReview { get; private set; } = true;

        public RushHudV15(RushUIV15 owner, RectTransform parent)
        {
            this.owner = owner;
            Root = U15.Rect(parent, "HudV15", new Rect(0, 0, 1280, 720));
            U15.Image(Root, "TimerPanel", "component.panel_cream", new Rect(18, 18, 262, 74));
            U15.Image(Root, "TimeRingTrack", "component.ring_track", new Rect(32, 27, 56, 56), new Color(.08f, .26f, .29f, .16f));
            timeRing = U15.Image(Root, "TimeRemainingRing", "component.ring_fill", new Rect(32, 27, 56, 56), U15.Gold);
            Radial(timeRing);
            U15.Image(Root, "Stopwatch", "stopwatch", new Rect(39, 34, 42, 42));
            timer = U15.Text(Root, "Countdown", "00:00", new Rect(96, 27, 165, 54), 34, TextAnchor.MiddleCenter, U15.Ink);
            U15.Button(Root, "PauseButton", "pause", new Rect(292, 25, 58, 58), () => this.owner.Host.TogglePauseFromUI(), "暂停 / Esc");
            U15.Button(Root, "HelpButton", "help", new Rect(358, 25, 58, 58), () => this.owner.ToggleHelp(), "操作提示 / F1");

            for (int i = 0; i < slots.Length; i++) slots[i] = CreateTaskSlot(i);

            U15.Image(Root, "ActionPanel", "component.panel_cream", new Rect(1008, 594, 254, 108));
            U15.Image(Root, "DashTrack", "component.ring_track", new Rect(1021, 608, 74, 74), new Color(.08f, .26f, .29f, .18f));
            dashRing = U15.Image(Root, "DashCooldown", "component.ring_fill", new Rect(1021, 608, 74, 74), U15.Gold);
            Radial(dashRing);
            dashIcon = U15.Image(Root, "DashState", "action_dash", new Rect(1033, 620, 50, 50));
            braceIcon = U15.Image(Root, "BraceState", "action_brace", new Rect(1108, 619, 58, 58));
            travelIcon = U15.Image(Root, "TravelState", "stage_street", new Rect(1184, 619, 58, 58));
            Minimap = new RushMinimapV15(owner, Root);
        }

        TaskSlot CreateTaskSlot(int index)
        {
            var slot = new TaskSlot { Root = U15.Rect(Root, "Task" + index + "Button", new Rect(18 + index * 204, 104, 198, 118)) };
            Image background = U15.Image(slot.Root, "Panel", "component.panel_cream", new Rect(0, 0, 198, 118));
            background.raycastTarget = true;
            slot.Button = slot.Root.gameObject.AddComponent<Button>();
            slot.Button.targetGraphic = background;
            slot.Button.onClick.AddListener(() =>
            {
                if (boundRun != null && (boundRun.phase == RunPhase.Playing || boundRun.phase == RunPhase.Paused) &&
                    index < boundRun.tasks.Count && !(boundRun.stage == 1 && boundRun.tasks[index].id == "M1"))
                    owner.ShowTaskHelp(boundRun.tasks[index].id);
            });
            slot.Icon = U15.Image(slot.Root, "TaskIcon", "task_m1_neutral", new Rect(12, 10, 50, 50));
            slot.State = U15.Image(slot.Root, "TaskState", "check", new Rect(165, 10, 23, 23));
            slot.Number = U15.Text(slot.Root, "Amount", "", new Rect(70, 16, 91, 27), 21, TextAnchor.MiddleCenter, U15.Ink);
            slot.Risk = U15.Image(slot.Root, "RiskGlyph", "cross", new Rect(76, 49, 17, 17));
            slot.RiskNumber = U15.Text(slot.Root, "RiskCount", "", new Rect(99, 43, 80, 28), 17, TextAnchor.MiddleLeft, U15.Ink);
            slot.Secondary = U15.Text(slot.Root, "TaskTimer", "", new Rect(72, 44, 98, 26), 17, TextAnchor.MiddleCenter, U15.Ink);
            slot.ProgressTrack = U15.Image(slot.Root, "ProgressTrack", "component.progress_track", new Rect(14, 91, 170, 10), new Color(.08f, .26f, .29f, .16f));
            slot.ProgressFill = U15.Image(slot.Root, "ProgressFill", "component.progress_fill", new Rect(14, 91, 170, 10), U15.Teal);
            slot.ProgressFill.type = Image.Type.Filled;
            slot.ProgressFill.fillMethod = Image.FillMethod.Horizontal;
            slot.ProgressFill.fillOrigin = 0;

            slot.GoalLegend = U15.Image(slot.Root, "GoalLegend", "star_outline", new Rect(25, 61, 16, 16));
            slot.CurrentLegend = U15.Image(slot.Root, "CurrentLegend", "player_arrow", new Rect(25, 85, 16, 16));
            for (int i = 0; i < 5; i++)
            {
                slot.Goal[i] = U15.Image(slot.Root, "Goal" + i, "sun", new Rect(50 + i * 27, 58, 22, 22));
                slot.Current[i] = U15.Image(slot.Root, "Current" + i, "moon", new Rect(50 + i * 27, 82, 22, 22));
                slot.Current[i].preserveAspect = false; // Horizontal flip squeeze must not shrink the Y axis.
                slot.Portraits[i] = U15.Image(slot.Root, "Portrait" + i, "portrait_" + i.ToString("00"), new Rect(12 + i * 35, 75, 31, 31));
                slot.Delivered[i] = U15.Image(slot.Root, "Delivered" + i, "check", new Rect(31 + i * 35, 95, 14, 14));
            }
            for (int i = 0; i < 6; i++)
                slot.Counters[i] = U15.Image(slot.Root, "Counter" + i, "component.slot_empty", new Rect(14 + i * 28, 76, 24, 24));
            slot.BeatTrack = U15.Image(slot.Root, "BeatTrack", "component.progress_track", new Rect(14, 69, 170, 9), new Color(.08f, .26f, .29f, .3f));
            slot.BeatWindow = U15.Image(slot.Root, "BeatWindow", "component.white_pixel", new Rect(75, 69, 40, 9), U15.Gold);
            slot.BeatCursor = U15.Image(slot.Root, "BeatCursor", "component.white_pixel", new Rect(14, 65, 3, 17), U15.Ink);
            slot.BeatWindow.preserveAspect = slot.BeatCursor.preserveAspect = false;
            foreach (Graphic graphic in slot.Root.GetComponentsInChildren<Graphic>())
                if (graphic != background) graphic.raycastTarget = false;
            ResetDetails(slot);
            return slot;
        }

        public void Refresh(RunV9 run, float gameDt)
        {
            if (run == null) return;
            bool newRun = !ReferenceEquals(run, boundRun);
            if (newRun)
            {
                boundRun = run;
                previousDashLaunches = run.DashLaunchCount;
                previousPlayerLaunches = run.PlayerLaunchCount;
                previousBraceBlocks = run.BraceBlockCount;
                actionPulse = 0;
                foreach (TaskSlot slot in slots) slot.Pulse = 0;
            }
            float dt = run.phase == RunPhase.Playing ? Mathf.Max(0, gameDt) : 0;
            timer.text = TimeString(run.Remaining);
            timeRing.fillAmount = run.limit > 0 ? Mathf.Clamp01(run.Remaining / (float)run.limit) : 0;
            timeRing.color = run.Remaining <= 30 ? U15.Coral : U15.Gold;
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Root.gameObject.SetActive(i < run.tasks.Count);
                if (i < run.tasks.Count) RefreshTask(slots[i], run.tasks[i], run, i, dt, newRun);
            }

            dashRing.fillAmount = run.DashReady01;
            dashRing.color = run.DashBuffered ? U15.Teal : U15.Gold;
            dashIcon.color = new Color(1, 1, 1, run.IsBracing || run.IsAirborne ? .4f : run.DashReady01 < .999f && !run.IsDashing ? .65f : 1f);
            braceIcon.color = new Color(1, 1, 1, run.IsBracing ? 1 : .35f);
            travelIcon.sprite = U15.Sprite(run.IsAirborne ? "action_airborne" : run.bicycle ? "action_bike" : "stage_street");
            if (!newRun && run.phase == RunPhase.Playing &&
                (run.DashLaunchCount > previousDashLaunches || run.PlayerLaunchCount > previousPlayerLaunches))
                owner.PlayFx("impact_puff", new Vector2(1058, 645));
            if (!newRun && run.phase == RunPhase.Playing && run.BraceBlockCount > previousBraceBlocks) actionPulse = .24f;
            previousDashLaunches = run.DashLaunchCount;
            previousPlayerLaunches = run.PlayerLaunchCount;
            previousBraceBlocks = run.BraceBlockCount;
            actionPulse = owner.ReducedMotion || run.phase == RunPhase.Results ? 0 : Mathf.Max(0, actionPulse - dt);
            float glow = actionPulse > 0 ? Mathf.Sin((1 - actionPulse / .24f) * Mathf.PI) : 0;
            braceIcon.color = Color.Lerp(braceIcon.color, new Color(.4f, 1, .72f, 1), glow);
            Minimap.Refresh(run, gameDt);
        }

        void RefreshTask(TaskSlot slot, TaskV9 task, RunV9 run, int index, float dt, bool newRun)
        {
            bool hidden = run.stage == 1 && task.id == "M1" && task.state != TaskState.Failed;
            if (newRun || slot.DisplayId != task.id || slot.WasHidden != hidden || slot.PreviousState != task.state)
                ResetDetails(slot);
            slot.DisplayId = task.id;
            slot.WasHidden = hidden;
            if (owner.ReducedMotion || run.phase == RunPhase.Ready || run.phase == RunPhase.Results) slot.Pulse = 0;
            // M1's task content is only revealed by the results view, including after failure.
            slot.Icon.sprite = U15.Sprite(task.id == "M1" ? "task_m1_neutral" : U15.TaskIcon(task.id));
            slot.Icon.color = Color.white;
            slot.Button.interactable = task.id != "M1";
            if (hidden)
            {
                slot.Pulse = 0;
                U15.SetRect(slot.Icon.rectTransform, new Rect(74, 31, 50, 50));
                HiddenTaskIsNeutralForReview = slot.Icon.sprite == U15.Sprite("task_m1_neutral") &&
                    !slot.State.gameObject.activeSelf && !slot.ProgressFill.gameObject.activeSelf && slot.Number.text.Length == 0 && !slot.Button.interactable;
                slot.PreviousState = task.state;
                slot.PreviousCount = task.count;
                return;
            }
            if (run.stage != 1 || task.id == "M1") HiddenTaskIsNeutralForReview = true;
            bool changed = !newRun && slot.PreviousState != task.state;
            if (changed && run.phase == RunPhase.Playing)
            {
                slot.Pulse = .26f;
                Vector2 fxPosition = new Vector2(18 + index * 204 + 38, 140);
                if (task.state == TaskState.Complete) owner.PlayFx("success_sparkle", fxPosition);
                else if (task.state == TaskState.Failed && (task.id == "S1" || task.id == "O1")) owner.PlayFx("failure_spill", fxPosition);
            }
            else if (!newRun && run.phase == RunPhase.Playing && task.id == "O1" && task.count > slot.PreviousCount)
                owner.PlayFx("failure_spill", new Vector2(18 + index * 204 + 38, 140));
            if (!newRun && run.phase == RunPhase.Playing && task.count > slot.PreviousCount &&
                (task.id == "S2" || task.id == "O2" || task.id == "O3")) slot.Pulse = .26f;
            slot.PreviousState = task.state;
            slot.PreviousCount = task.count;
            slot.Pulse = owner.ReducedMotion ? 0 : Mathf.Max(0, slot.Pulse - dt);
            float scale = slot.Pulse > 0 ? 1 + .12f * Mathf.Sin((1 - slot.Pulse / .26f) * Mathf.PI) : 1;
            U15.SetRect(slot.Icon.rectTransform, new Rect(12 + 25 * (1 - scale), 10 + 25 * (1 - scale), 50 * scale, 50 * scale));

            bool done = task.state == TaskState.Complete;
            bool failed = task.state == TaskState.Failed;
            slot.State.gameObject.SetActive(done || failed);
            if (done || failed) slot.State.sprite = U15.Sprite(done ? "check" : "cross");
            if (task.id == "M1") return; // Failure may be shown, but never a gameplay progress/navigation hint.

            switch (task.id)
            {
                case "S1":
                    slot.Number.text = Mathf.RoundToInt(task.progress * 100) + "%";
                    ShowRisk(slot, task);
                    Progress(slot, task.progress, failed);
                    break;
                case "S2":
                    slot.Number.text = Count(task);
                    for (int i = 0; i < Mathf.Min(2, slot.Counters.Length); i++)
                    {
                        slot.Counters[i].gameObject.SetActive(true);
                        slot.Counters[i].sprite = U15.Sprite("wheel");
                        slot.Counters[i].color = new Color(1, 1, 1, i < task.count ? 1 : .24f);
                        U15.SetRect(slot.Counters[i].rectTransform, new Rect(64 + i * 42, 72, 32, 32));
                    }
                    break;
                case "S3":
                    int matched = 0;
                    slot.GoalLegend.gameObject.SetActive(true);
                    slot.CurrentLegend.gameObject.SetActive(true);
                    for (int i = 0; i < Mathf.Min(5, run.boards.Length); i++)
                    {
                        slot.Goal[i].gameObject.SetActive(true); slot.Current[i].gameObject.SetActive(true);
                        slot.Goal[i].sprite = U15.Sprite(run.boardGoal[i] ? "sun" : "moon");
                        RefreshBoard(slot, run, i, dt, newRun);
                        if (run.boards[i] == run.boardGoal[i]) matched++;
                    }
                    slot.Number.text = matched + "/" + run.boards.Length;
                    break;
                case "M2":
                    slot.Number.text = Count(task);
                    Progress(slot, task.limit > 0 ? (float)task.count / task.limit : 0, failed);
                    if (!done && !failed)
                    {
                        slot.BeatTrack.gameObject.SetActive(true); slot.BeatWindow.gameObject.SetActive(true); slot.BeatCursor.gameObject.SetActive(true);
                        float window = Mathf.Clamp01(run.config.beatWindow / run.config.beatPeriod);
                        U15.SetRect(slot.BeatWindow.rectTransform, new Rect(14 + 170 * (.5f - window * .5f), 69, 170 * window, 9));
                        U15.SetRect(slot.BeatCursor.rectTransform, new Rect(14 + 170 * Mathf.Clamp01(run.BeatPhase) - 1.5f, 65, 3, 17));
                    }
                    break;
                case "M3":
                    slot.Number.text = Mathf.RoundToInt(task.progress * 100) + "%";
                    Progress(slot, task.progress, failed);
                    break;
                case "O1":
                    ShowRisk(slot, task);
                    U15.SetRect(slot.Risk.rectTransform, new Rect(75, 26, 18, 18));
                    U15.SetRect(slot.RiskNumber.rectTransform, new Rect(101, 21, 70, 28));
                    break;
                case "O2":
                    slot.Number.text = Count(task);
                    if (task.state == TaskState.Active)
                        slot.Secondary.text = TimeString(Mathf.Max(0, (float)(run.config.deliveryDuration - run.elapsed + task.startedAt)));
                    for (int i = 0; i < slot.Portraits.Length; i++)
                    {
                        bool present = i < task.limit;
                        slot.Portraits[i].gameObject.SetActive(present);
                        bool delivered = run.IsDelivered(i);
                        slot.Portraits[i].color = new Color(1, 1, 1, delivered ? .35f : 1);
                        slot.Delivered[i].gameObject.SetActive(present && delivered);
                    }
                    break;
                case "O3":
                    slot.Number.text = Count(task);
                    for (int i = 0; i < slot.Counters.Length; i++)
                    {
                        slot.Counters[i].gameObject.SetActive(i < task.limit);
                        slot.Counters[i].sprite = U15.Sprite(i < task.count ? "component.slot_filled" : "component.slot_empty");
                        slot.Counters[i].color = Color.white;
                        U15.SetRect(slot.Counters[i].rectTransform, new Rect(14 + i * 28, 76, 24, 24));
                    }
                    break;
            }
        }

        static void ResetDetails(TaskSlot slot)
        {
            slot.Number.text = slot.RiskNumber.text = slot.Secondary.text = "";
            slot.State.gameObject.SetActive(false);
            slot.Risk.gameObject.SetActive(false);
            U15.SetRect(slot.Risk.rectTransform, new Rect(76, 49, 17, 17));
            U15.SetRect(slot.RiskNumber.rectTransform, new Rect(99, 43, 80, 28));
            slot.ProgressTrack.gameObject.SetActive(false); slot.ProgressFill.gameObject.SetActive(false);
            slot.GoalLegend.gameObject.SetActive(false); slot.CurrentLegend.gameObject.SetActive(false);
            slot.BeatTrack.gameObject.SetActive(false); slot.BeatWindow.gameObject.SetActive(false); slot.BeatCursor.gameObject.SetActive(false);
            for (int i = 0; i < 5; i++)
            {
                slot.Goal[i].gameObject.SetActive(false); slot.Current[i].gameObject.SetActive(false);
                slot.Portraits[i].gameObject.SetActive(false); slot.Delivered[i].gameObject.SetActive(false);
            }
            foreach (Image counter in slot.Counters) counter.gameObject.SetActive(false);
        }

        void RefreshBoard(TaskSlot slot, RunV9 run, int index, float dt, bool newRun)
        {
            bool value = run.boards[index];
            if (newRun || owner.ReducedMotion || run.phase == RunPhase.Ready || run.phase == RunPhase.Results)
            {
                slot.BoardShown[index] = slot.BoardTarget[index] = value;
                slot.BoardFlipLeft[index] = 0;
            }
            else if (slot.BoardTarget[index] != value)
            {
                slot.BoardTarget[index] = value;
                slot.BoardFlipLeft[index] = .18f;
            }
            slot.BoardFlipLeft[index] = Mathf.Max(0, slot.BoardFlipLeft[index] - dt);
            float widthScale = 1;
            if (slot.BoardFlipLeft[index] > 0)
            {
                float phase = 1 - slot.BoardFlipLeft[index] / .18f;
                if (phase >= .5f) slot.BoardShown[index] = slot.BoardTarget[index];
                widthScale = Mathf.Max(.08f, Mathf.Abs(phase * 2 - 1));
            }
            else slot.BoardShown[index] = slot.BoardTarget[index];
            slot.Current[index].sprite = U15.Sprite(slot.BoardShown[index] ? "sun" : "moon");
            U15.SetRect(slot.Current[index].rectTransform,
                new Rect(50 + index * 27 + 11 * (1 - widthScale), 82, 22 * widthScale, 22));
        }

        static void ShowRisk(TaskSlot slot, TaskV9 task)
        {
            slot.Risk.gameObject.SetActive(true);
            slot.RiskNumber.text = task.count + "/" + task.limit;
            slot.RiskNumber.color = task.state == TaskState.Failed ? U15.Coral : U15.Ink;
        }

        static void Progress(TaskSlot slot, float progress, bool failed)
        {
            slot.ProgressTrack.gameObject.SetActive(true); slot.ProgressFill.gameObject.SetActive(true);
            slot.ProgressFill.fillAmount = Mathf.Clamp01(progress);
            slot.ProgressFill.color = failed ? U15.Coral : U15.Teal;
        }

        static string Count(TaskV9 task) => task.count + "/" + task.limit;
        static string TimeString(float seconds)
        {
            int value = Mathf.CeilToInt(Mathf.Max(0, seconds));
            return (value / 60).ToString("00") + ":" + (value % 60).ToString("00");
        }
        static void Radial(Image image)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
        }
    }
}
