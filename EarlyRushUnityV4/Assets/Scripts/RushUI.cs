using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush
{
    /// <summary>Resolution-independent, keyboard-friendly presentation for the commuter MVP.</summary>
    public static class RushUI
    {
        static readonly Color Ink = new Color32(26, 47, 62, 255);
        static readonly Color Cream = new Color32(255, 249, 235, 255);
        static readonly Color Orange = new Color32(255, 135, 55, 255);
        static readonly Color Teal = new Color32(43, 190, 177, 255);
        static readonly Color Red = new Color32(224, 83, 79, 255);
        static readonly Color Muted = new Color32(112, 132, 140, 255);
        static readonly string[] Titles = { "骑车赶地铁", "挤上早高峰", "抢最后的打卡" };
        static readonly string[] Objectives = { "绕开路障，骑到青色地铁入口", "穿过人群，进入青色车厢区域", "稳住重心，冲到青色打卡点" };
        static Font font;
        static Texture2D rounded, white, circle;
        static GUIStyle panelStyle, blankButton;
        static readonly Dictionary<string, GUIStyle> textStyles = new Dictionary<string, GUIStyle>();
        static RushGame active;
        static float width = 1280f;
        static float height = 720f;
        static float uiScale = 1f;

        static void Init()
        {
            if (white != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial" }, 26);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            white = Texture2D.whiteTexture;
            rounded = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            circle = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            rounded.hideFlags = HideFlags.HideAndDontSave;
            circle.hideFlags = HideFlags.HideAndDontSave;
            rounded.wrapMode = TextureWrapMode.Clamp;
            circle.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float px = Mathf.Max(0, Mathf.Abs(x - 15.5f) - 2f);
                float py = Mathf.Max(0, Mathf.Abs(y - 15.5f) - 2f);
                float a = Mathf.Clamp01(14f - Mathf.Sqrt(px * px + py * py));
                rounded.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            rounded.Apply();
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                circle.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(32f - Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)))));
            circle.Apply();
            panelStyle = new GUIStyle();
            panelStyle.normal.background = rounded;
            panelStyle.border = new RectOffset(14, 14, 14, 14);
            blankButton = new GUIStyle();
        }

        public static void Draw(RushGame game)
        {
            Init();
            active = game;
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            int previousDepth = GUI.depth;
            GUI.depth = -20;
            uiScale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            width = Screen.width / uiScale;
            height = Screen.height / uiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));
            GUI.color = Color.white;
            switch (game.State)
            {
                case RushState.Menu: Menu(); break;
                case RushState.Tutorial: Tutorial(); break;
                case RushState.Playing: Hud(); break;
                case RushState.Paused: Hud(); Pause(); break;
                case RushState.Transition: Hud(); Transition(); break;
                case RushState.Results: Results(); break;
            }
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        static int Stage => Mathf.Clamp(active.Stage, 0, 2);
        static float X(float x) => (width - 1280f) * .5f + x;
        static float Y(float y) => (height - 720f) * .5f + y;
        static Rect R(float x, float y, float w, float h) => new Rect(X(x), Y(y), w, h);

        static void Menu()
        {
            Box(new Rect(0, 0, width, height), new Color(Ink.r, Ink.g, Ink.b, .26f), false);
            Panel(R(62, 67, 518, 588), Cream);
            Pill(R(94, 97, 156, 28), "抢道反击  /  v4", Ink, Cream, 12);
            Text(R(90, 139, 440, 74), "早八冲刺", 62, Ink, true);
            Text(R(96, 217, 410, 28), "E A R L Y   R U S H", 22, Teal, true);
            Text(R(96, 268, 410, 44), "早餐可以等，打卡不能晚。", 24, Ink, true);
            Text(R(96, 318, 410, 60), "大家抢着上班，也会向前冲撞。\n看准预警，稳住，再撞开人群。", 20, Muted);
            Box(R(96, 397, 410, 1), new Color(Ink.r, Ink.g, Ink.b, .13f), false);
            Text(R(96, 415, 410, 48), "橙色背包是你  ·  青色区域是目标", 17, Ink);
            if (Button(R(96, 482, 410, 64), "开始赶路    →", Orange, Ink, 25)) active.StartRun();
            if (Button(R(96, 562, 126, 44), active.Muted ? "声音：关 [M]" : "声音：开 [M]", new Color32(233, 236, 222, 255), Ink, 13)) active.ToggleSound();
            if (Button(R(232, 562, 138, 44), active.ScreenShakeEnabled ? "震屏：开" : "震屏：关", new Color32(233, 236, 222, 255), Ink, 14)) active.ToggleScreenShake();
            if (Button(R(380, 562, 126, 44), "退出游戏", new Color32(233, 236, 222, 255), Ink, 15)) active.Quit();
            Text(R(96, 617, 410, 18), "ENTER 开始    ·    WASD / 方向键操作", 12, Muted);

            float routeX = 892;
            Panel(R(routeX, 126, 316, 84), new Color(Cream.r, Cream.g, Cream.b, .96f));
            Icon(0, R(routeX + 17, 145, 48, 44), Orange);
            Text(R(routeX + 80, 139, 204, 30), "01  骑车赶地铁", 20, Ink, true);
            Text(R(routeX + 80, 172, 210, 23), "看地面预警，留点刹车距离", 14, Muted);
            DashLine(R(routeX + 39, 218, 4, 34), Cream);
            Panel(R(routeX, 262, 316, 84), new Color(Cream.r, Cream.g, Cream.b, .96f));
            Icon(1, R(routeX + 17, 281, 48, 44), Teal);
            Text(R(routeX + 80, 275, 204, 30), "02  挤上早高峰", 20, Ink, true);
            Text(R(routeX + 80, 308, 210, 23), "冲刺撞飞，连锁冲开人群", 14, Muted);
            DashLine(R(routeX + 39, 354, 4, 34), Cream);
            Panel(R(routeX, 398, 316, 84), new Color(Cream.r, Cream.g, Cream.b, .96f));
            Icon(2, R(routeX + 17, 417, 48, 44), Orange);
            Text(R(routeX + 80, 411, 204, 30), "03  抢最后的打卡", 20, Ink, true);
            Text(R(routeX + 80, 444, 210, 23), "SHIFT 稳住，别被抢道撞飞", 14, Muted);
            Pill(R(914, 532, 270, 38), "迟到了也没关系，继续冲！", Ink, Cream, 16);
            Text(R(924, 578, 250, 22), "单人 · 俯视角 · 三段连续挑战", 13, Cream, false, TextAnchor.MiddleCenter);
        }

        static void Tutorial()
        {
            Scrim(.76f);
            Text(R(0, 64, 1280, 28), "你的通勤生存手册", 18, Teal, true, TextAnchor.MiddleCenter);
            Text(R(0, 104, 1280, 64), "撞飞赶路，看准预警", 44, Cream, true, TextAnchor.MiddleCenter);
            Text(R(140, 178, 1000, 26), "NPC 会沿赶路方向冲撞：先看地面预警，再侧移或按住 SHIFT。", 17, Cream, false, TextAnchor.MiddleCenter);
            TutorialCard(100, 0, "移动与转向", "W A S D", "或方向键  ↑ ↓ ← →", "骑车有惯性。\n提前转弯，松手减速。", Orange);
            TutorialCard(466, 1, "冲刺撞飞", "SPACE", "撞上、回弹，再向前！", "冲刺能把通勤者撞飞。\n连锁撞翻，顺势冲过！", Teal);
            TutorialCard(832, 2, "稳住免击飞", "SHIFT", "地面按住免击飞 · 移动变慢", "看到预警先稳住或侧移。\n松开稳住，SPACE 撞飞！", Orange);
            if (Button(R(435, 566, 410, 64), "出发！    ENTER  →", Orange, Ink, 25)) active.BeginPlay();
            Text(R(140, 639, 1000, 25), "落地后短暂恢复，并有 1.2 秒免再击飞保护；右下角显示状态与倒计时。", 15, Cream, false, TextAnchor.MiddleCenter);
            Text(R(140, 670, 1000, 24), "寻找青色目标  ·  三段共用计时  ·  超时仍可完成打卡  ·  ESC 暂停", 14, Cream, false, TextAnchor.MiddleCenter);
        }

        static void TutorialCard(float x, int type, string title, string key, string caption, string detail, Color accent)
        {
            Panel(R(x, 214, 348, 306), Cream);
            Circle(R(x + 144, 235, 60, 60), accent);
            if (type == 0) Text(R(x + 144, 235, 60, 60), "↗", 34, Ink, true, TextAnchor.MiddleCenter);
            if (type == 1) { Line(R(x + 157, 254, 32, 4), Ink); Line(R(x + 151, 265, 32, 4), Ink); Line(R(x + 162, 276, 21, 4), Ink); }
            if (type == 2) { Box(R(x + 162, 249, 24, 30), Ink, true); Text(R(x + 162, 249, 24, 30), "✓", 21, Cream, true, TextAnchor.MiddleCenter); }
            Text(R(x + 20, 309, 308, 32), title, 24, Ink, true, TextAnchor.MiddleCenter);
            Pill(R(x + 91, 355, 166, 42), key, Ink, Cream, 21);
            Text(R(x + 20, 407, 308, 26), caption, 15, Muted, false, TextAnchor.MiddleCenter);
            Text(R(x + 26, 445, 296, 54), detail, 17, Ink, false, TextAnchor.MiddleCenter);
        }

        static void Hud()
        {
            bool late = active.TotalTime > active.TimeLimit;
            Panel(new Rect(24, 20, 322, 92), Cream);
            Pill(new Rect(40, 36, 54, 26), (Stage + 1).ToString("00") + " / 03", Teal, Ink, 12);
            Text(new Rect(108, 30, 218, 38), Titles[Stage], 23, Ink, true);
            Text(new Rect(40, 75, 286, 22), Objectives[Stage], 14, Muted);

            Panel(new Rect(width * .5f - 159, 20, 318, 72), Ink);
            for (int i = 0; i < 3; i++)
            {
                float x = width * .5f - 139 + i * 94;
                Color c = i < Stage ? Teal : i == Stage ? Orange : new Color32(73, 94, 105, 255);
                Circle(new Rect(x, 38, 29, 29), c);
                Text(new Rect(x, 38, 29, 29), i < Stage ? "✓" : (i + 1).ToString(), 15, i <= Stage ? Ink : Cream, true, TextAnchor.MiddleCenter);
                if (i < 2) Box(new Rect(x + 34, 51, 54, 3), i < Stage ? Teal : new Color32(73, 94, 105, 255), false);
            }
            // Fit the goal cue inside the existing stage bar, leaving the playfield clear for warnings.
            if (active.World != null)
            {
                Vector2 direction = active.GoalViewport - new Vector2(.5f, .5f);
                string arrow = Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ?
                    (direction.x > 0 ? "→" : "←") : (direction.y > 0 ? "↑" : "↓");
                Text(new Rect(width * .5f - 139, 68, 278, 20),
                    arrow + " 目标 " + active.GoalDistance.ToString("F0") + " 米", 14, Cream, false, TextAnchor.MiddleCenter);
            }

            Panel(new Rect(width - 312, 20, 218, 92), late ? Red : Cream);
            Text(new Rect(width - 296, 31, 188, 22), late ? "已迟到 · 继续打卡" : "距离迟到还有", 13, late ? Cream : Muted);
            Text(new Rect(width - 296, 52, 188, 49), (late ? "+" : "") + TimeText(Mathf.Abs(active.TimeLimit - active.TotalTime)), 35, late ? Cream : Ink, true);
            if (Button(new Rect(width - 78, 20, 54, 54), "Ⅱ", Ink, Cream, 26)) active.TogglePause();

            // The unobtrusive route marker gives players distance without covering the scene.
            Panel(new Rect(24, height - 86, 350, 62), Ink);
            Text(new Rect(40, height - 79, 315, 22), "本段进度", 12, Cream);
            Bar(new Rect(40, height - 48, 314, 6), Mathf.Clamp01(active.StageProgress01), Teal, new Color32(68, 87, 100, 255));
            Text(new Rect(24, height - 118, 360, 24), "碰撞  " + active.Collisions + "     本段  " + TimeText(active.StageTime), 14, Ink, true);

            Panel(new Rect(width - 356, height - 136, 332, 112), Cream);
            string dash = active.PlayerAirborne ? "SPACE  腾空中 · 落地再冲刺" :
                active.PlayerRecovering ? "SPACE  落地恢复中" :
                active.IsBracing ? "SPACE  松开稳住后冲刺" :
                active.IsDashing ? "SPACE  冲撞中！" :
                active.DashCooldown01 < .99f ? "SPACE  冲刺恢复中" : "SPACE  冲刺撞飞";
            Text(new Rect(width - 338, height - 127, 296, 26), dash, 16, Ink, true);
            Bar(new Rect(width - 338, height - 94, 295, 6), Mathf.Clamp01(active.DashCooldown01), Orange, new Color32(221, 226, 214, 255));
            bool protectedNow = active.PlayerProtectionRemaining > 0f;
            string protection = (Mathf.Ceil(active.PlayerProtectionRemaining * 10f) / 10f).ToString("F1");
            string response = active.PlayerAirborne ? "被撞飞了 · 等待落地" :
                active.PlayerRecovering ? "落地恢复 · 保护 " + protection + " 秒" :
                active.BraceSuccessRemaining > 0f ? "稳住成功！挡住冲撞  " + active.BraceBlocks + " 次" :
                protectedNow ? "落地保护  " + protection + " 秒 · 免再击飞" :
                "地面预警 → 侧移 / SHIFT 稳住";
            Color responseColor = active.PlayerAirborne ? Red : active.PlayerRecovering || protectedNow || active.BraceSuccessRemaining > 0f ? Teal : Muted;
            Text(new Rect(width - 338, height - 82, 296, 24), response, 14, responseColor, true);
            string brace = active.PlayerAirborne || active.PlayerRecovering ? "SHIFT  回到地面后稳住" :
                active.IsBracing ? "SHIFT  正在稳住 · 免击飞 / 减速" : "SHIFT  地面按住免击飞 · 移动变慢";
            Text(new Rect(width - 338, height - 51, 296, 21), brace, 12, active.IsBracing ? Teal : Muted, active.IsBracing);
            Text(new Rect(width * .5f - 230, height - 55, 460, 26), "WASD / 方向键  移动    ·    ESC  暂停", 14, Ink, true, TextAnchor.MiddleCenter);
        }

        static void Pause()
        {
            Scrim(.72f);
            Panel(R(398, 116, 484, 492), Cream);
            Pill(R(553, 145, 174, 28), "深呼吸，歇一会儿", Teal, Ink, 13);
            Text(R(428, 193, 424, 60), "通勤暂停", 40, Ink, true, TextAnchor.MiddleCenter);
            Text(R(428, 254, 424, 28), "计时已停下，准备好再继续。", 17, Muted, false, TextAnchor.MiddleCenter);
            if (Button(R(440, 308, 400, 58), "继续赶路    ESC", Orange, Ink, 23)) active.TogglePause();
            if (Button(R(440, 380, 192, 48), "重新出发  R", Ink, Cream, 17)) active.Restart();
            if (Button(R(648, 380, 192, 48), active.Muted ? "声音：关  M" : "声音：开  M", Ink, Cream, 17)) active.ToggleSound();
            if (Button(R(440, 442, 400, 48), active.ScreenShakeEnabled ? "轻微震屏：开" : "轻微震屏：关", new Color32(228, 234, 220, 255), Ink, 17)) active.ToggleScreenShake();
            if (Button(R(440, 500, 400, 48), "返回主菜单", new Color32(228, 234, 220, 255), Ink, 17)) active.ToMenu();
            Text(R(428, 570, 424, 24), "WASD 移动   ·   SPACE 冲刺   ·   SHIFT 稳住", 13, Muted, false, TextAnchor.MiddleCenter);
        }

        static void Transition()
        {
            Scrim(.42f);
            Panel(R(330, 244, 620, 222), Cream);
            Pill(R(537, 269, 206, 31), "已抵达 · 继续赶路", Teal, Ink, 15);
            Text(R(355, 318, 570, 50), "下一站，好运上班人！", 34, Ink, true, TextAnchor.MiddleCenter);
            Text(R(355, 379, 570, 30), "通勤计时继续保留", 17, Muted, false, TextAnchor.MiddleCenter);
            Bar(R(445, 432, 390, 5), 1f - Mathf.Clamp01(active.TransitionLeft / 1.55f), Orange, new Color32(225, 231, 219, 255));
        }

        static void Results()
        {
            bool ontime = active.TotalTime <= active.TimeLimit;
            Scrim(.73f);
            Panel(R(278, 45, 724, 632), Cream);
            Pill(R(531, 69, 218, 29), ontime ? "通勤完成  /  ON TIME" : "通勤完成  /  MADE IT", ontime ? Teal : Orange, Ink, 13);
            Text(R(308, 115, 664, 59), ontime ? "打卡成功，刚刚好！" : "迟到了，但没有放弃！", 38, Ink, true, TextAnchor.MiddleCenter);
            Text(R(308, 178, 664, 27), ontime ? "今天的早八，被你拿捏了。" : "撞开人群，下一趟早一点到。", 18, Muted, false, TextAnchor.MiddleCenter);
            StatCard(322, "总通勤时间", TimeText(active.TotalTime), ontime ? Teal : Orange);
            StatCard(546, "全程碰撞", active.Collisions + " 次", Orange);
            StatCard(770, ontime ? "提前到达" : "迟到时间", TimeText(Mathf.Abs(active.TimeLimit - active.TotalTime)), ontime ? Teal : Red);
            Text(R(325, 343, 630, 25), "通勤路线", 17, Ink, true);
            for (int i = 0; i < 3; i++)
            {
                float y = 383 + i * 43;
                if (i % 2 == 0) Box(R(322, y - 3, 636, 41), new Color32(238, 241, 226, 255), true);
                Circle(R(337, y + 4, 24, 24), Teal);
                Text(R(337, y + 4, 24, 24), (i + 1).ToString(), 13, Ink, true, TextAnchor.MiddleCenter);
                Text(R(375, y, 270, 31), Titles[i], 17, Ink);
                float time = active.SplitTimes != null && active.SplitTimes.Length > i ? active.SplitTimes[i] : 0;
                int hits = active.SplitCollisions != null && active.SplitCollisions.Length > i ? active.SplitCollisions[i] : 0;
                Text(R(664, y, 135, 31), TimeText(time), 18, Ink, true, TextAnchor.MiddleRight);
                Text(R(817, y, 123, 31), hits + " 次碰撞", 15, Muted, false, TextAnchor.MiddleRight);
            }
            if (Button(R(322, 548, 354, 60), "再赶一次    R", Orange, Ink, 23)) active.Restart();
            if (Button(R(694, 548, 264, 60), "返回主菜单", Ink, Cream, 20)) active.ToMenu();
            Text(R(308, 632, 664, 22), "小提示：看地面预警，SHIFT 稳住免击飞，再松开冲刺。", 14, Muted, false, TextAnchor.MiddleCenter);
        }

        static void StatCard(float x, string caption, string value, Color accent)
        {
            Box(R(x, 239, 188, 79), new Color32(235, 240, 225, 255), true);
            Box(R(x + 14, 252, 4, 51), accent, true);
            Text(R(x + 30, 247, 147, 22), caption, 13, Muted);
            Text(R(x + 30, 271, 147, 36), value, 27, Ink, true);
        }

        static string TimeText(float value)
        {
            value = Mathf.Max(0, value);
            int minutes = (int)(value / 60f);
            int seconds = (int)(value % 60f);
            int tenths = (int)((value * 10f) % 10f);
            return minutes.ToString("00") + ":" + seconds.ToString("00") + "." + tenths;
        }

        static void Scrim(float alpha) => Box(new Rect(0, 0, width, height), new Color(Ink.r, Ink.g, Ink.b, alpha), false);
        static void Panel(Rect r, Color color)
        {
            Box(new Rect(r.x + 2, r.y + 6, r.width, r.height), new Color(0, 0, 0, .12f), true);
            Box(r, color, true);
        }
        static void Pill(Rect r, string text, Color background, Color foreground, int size)
        {
            Box(r, background, true);
            Text(r, text, size, foreground, true, TextAnchor.MiddleCenter);
        }
        static void Box(Rect rect, Color color, bool round)
        {
            Color old = GUI.color;
            GUI.color = color;
            // Nine-slice borders cannot fit thin icon strokes and progress bars.
            // Stretch the small texture inside their exact rectangle instead.
            if (round && rect.width >= 28 && rect.height >= 28) GUI.Box(rect, GUIContent.none, panelStyle);
            else if (round) GUI.DrawTexture(rect, rounded);
            else GUI.DrawTexture(rect, white);
            GUI.color = old;
        }
        static void Circle(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, circle);
            GUI.color = old;
        }
        static void Line(Rect r, Color color) => Box(r, color, true);
        static void DashLine(Rect r, Color color)
        {
            for (int i = 0; i < 3; i++) Box(new Rect(r.x, r.y + 12 * i, r.width, 6), color, false);
        }
        static void Bar(Rect r, float amount, Color fill, Color track)
        {
            Box(r, track, true);
            if (amount > .005f) Box(new Rect(r.x, r.y, Mathf.Max(r.height, r.width * Mathf.Clamp01(amount)), r.height), fill, true);
        }
        static GUIStyle Style(int size, bool bold, TextAnchor align)
        {
            string key = size + "/" + bold + "/" + (int)align;
            if (!textStyles.TryGetValue(key, out GUIStyle style))
            {
                style = new GUIStyle();
                style.font = font;
                style.fontSize = size;
                style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
                style.alignment = align;
                style.wordWrap = true;
                style.richText = false;
                style.padding = new RectOffset(0, 0, 0, 0);
                style.normal.textColor = Color.white;
                textStyles.Add(key, style);
            }
            return style;
        }
        static void Text(Rect r, string text, int size, Color color, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.Label(r, text, Style(size, bold, align));
            GUI.color = old;
        }
        static bool Button(Rect r, string text, Color background, Color foreground, int size)
        {
            bool hovered = r.Contains(Event.current.mousePosition);
            Color fill = hovered ? Color.Lerp(background, Color.white, .13f) : background;
            Box(new Rect(r.x, r.y + 3, r.width, r.height), new Color(0, 0, 0, .12f), true);
            Box(r, fill, true);
            Text(r, text, size, foreground, true, TextAnchor.MiddleCenter);
            return GUI.Button(r, GUIContent.none, blankButton);
        }

        static void Icon(int icon, Rect r, Color accent)
        {
            if (icon == 0)
            {
                Circle(new Rect(r.x, r.y + 20, 23, 23), Ink);
                Circle(new Rect(r.x + 29, r.y + 20, 23, 23), Ink);
                Circle(new Rect(r.x + 5, r.y + 25, 13, 13), Cream);
                Circle(new Rect(r.x + 34, r.y + 25, 13, 13), Cream);
                Box(new Rect(r.x + 8, r.y + 16, 34, 5), accent, true);
                Box(new Rect(r.x + 19, r.y + 6, 7, 27), accent, true);
                Box(new Rect(r.x + 13, r.y + 3, 20, 5), Ink, true);
                Box(new Rect(r.x + 37, r.y + 9, 6, 23), accent, true);
            }
            else if (icon == 1)
            {
                Box(new Rect(r.x + 4, r.y, 42, 41), accent, true);
                Box(new Rect(r.x + 10, r.y + 8, 30, 17), Ink, true);
                Box(new Rect(r.x + 23, r.y + 8, 4, 17), accent, false);
                Circle(new Rect(r.x + 10, r.y + 30, 7, 7), Cream);
                Circle(new Rect(r.x + 33, r.y + 30, 7, 7), Cream);
                Box(new Rect(r.x + 9, r.y + 40, 6, 5), Ink, false);
                Box(new Rect(r.x + 35, r.y + 40, 6, 5), Ink, false);
            }
            else
            {
                Circle(new Rect(r.x + 3, r.y + 1, 44, 44), accent);
                Circle(new Rect(r.x + 8, r.y + 6, 34, 34), Cream);
                Box(new Rect(r.x + 23, r.y + 11, 4, 17), Ink, true);
                Box(new Rect(r.x + 23, r.y + 24, 14, 4), Ink, true);
            }
        }
    }
}
