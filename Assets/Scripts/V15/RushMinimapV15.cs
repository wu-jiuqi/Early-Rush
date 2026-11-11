using System.Collections.Generic;
using EarlyRush.V9;
using UnityEngine;
using UnityEngine.UI;

namespace EarlyRush.V15
{
    /// <summary>
    /// uGUI view of the production north-up projection. Projection coordinates are GUI
    /// top-left coordinates; only SetCentered/SetRect convert their Y to RectTransform Y.
    /// The simulation and map texture remain owned by RushPlayV9.
    /// </summary>
    public sealed class RushMinimapV15
    {
        sealed class Marker
        {
            public RectTransform Root;
            public Image Icon, Arrow, State;
        }

        readonly RushUIV15 owner;
        readonly RawImage map;
        readonly Image background, frame, player, toggleIcon;
        readonly RectTransform toggleRoot;
        readonly List<Marker> tasks = new List<Marker>();
        readonly Marker destination, document;
        RunV9 boundRun;

        public RectTransform Root { get; }
        public Rect PanelRect { get; private set; }
        public Vector2 ProjectPlayerForReview { get; private set; }
        public RectTransform PlayerMarkerForReview => player.rectTransform;
        public Rect TextureRectForReview { get; private set; }
        public Rect TextureUvForReview { get; private set; }
        public bool HiddenTaskMarkerVisibleForReview => tasks.Count > 0 && boundRun != null &&
            boundRun.stage == 1 && tasks[0].Root.gameObject.activeSelf;

        public RushMinimapV15(RushUIV15 owner, RectTransform parent)
        {
            this.owner = owner;
            PanelRect = SmallPanel;
            Root = U15.Rect(parent, "MinimapV15", PanelRect);
            background = U15.Image(Root, "MapBackground", "component.panel_cream", new Rect(0, 0, 254, 210));
            background.raycastTarget = false;

            var mapRoot = U15.Rect(Root, "MapTexture", new Rect(10, 10, 234, 178));
            map = mapRoot.gameObject.AddComponent<RawImage>();
            map.raycastTarget = false;
            map.color = Color.white;

            for (int i = 0; i < 3; i++) tasks.Add(CreateMarker("TaskMapMarker" + i));
            destination = CreateMarker("DestinationMapMarker");
            document = CreateMarker("DocumentRegionMarker");
            player = U15.Image(Root, "PlayerArrow", "player_arrow", new Rect(0, 0, 24, 24));
            player.raycastTarget = false;
            frame = U15.Image(Root, "MapFrame", "component.minimap_frame", new Rect(0, 0, 254, 210));
            frame.raycastTarget = false;

            toggleRoot = U15.Rect(Root, "MapToggleButton", new Rect(218, 177, 28, 28));
            var toggleBackground = U15.Image(toggleRoot, "Background", "component.tile_cream", new Rect(0, 0, 28, 28));
            toggleBackground.raycastTarget = true;
            var toggle = toggleRoot.gameObject.AddComponent<Button>();
            toggle.targetGraphic = toggleBackground;
            toggle.onClick.AddListener(() => this.owner.Host.SetExpandedMap(!this.owner.Host.FullMap));
            U15.Tooltip(toggleRoot.gameObject, "展开 / 收起地图 · Tab");
            toggleIcon = U15.Image(toggleRoot, "Icon", "map_expand", new Rect(3, 3, 22, 22));
            toggleIcon.raycastTarget = false;
        }

        static Rect SmallPanel => new Rect(1008, 18, 254, 210);
        static Rect LargePanel => new Rect(660, 106, 590, 585);

        Marker CreateMarker(string name)
        {
            var marker = new Marker { Root = U15.Rect(Root, name, new Rect(0, 0, 28, 28)) };
            marker.Icon = U15.Image(marker.Root, "Icon", "task_m1_neutral", new Rect(2, 2, 24, 24));
            marker.Arrow = U15.Image(marker.Root, "OffscreenArrow", "offscreen_arrow", new Rect(0, 0, 15, 15));
            marker.State = U15.Image(marker.Root, "State", "cross", new Rect(17, 17, 13, 13));
            marker.Icon.raycastTarget = marker.Arrow.raycastTarget = marker.State.raycastTarget = false;
            marker.Arrow.gameObject.SetActive(false);
            marker.State.gameObject.SetActive(false);
            return marker;
        }

        public void Refresh(RunV9 run, float gameDt)
        {
            if (run == null || owner.Host == null) return;
            bool expanded = owner.Host.FullMap;
            Rect nextPanel = expanded ? LargePanel : SmallPanel;
            if (PanelRect != nextPanel)
            {
                PanelRect = nextPanel;
                U15.SetRect(Root, PanelRect);
                U15.SetRect(background.rectTransform, new Rect(0, 0, PanelRect.width, PanelRect.height));
                U15.SetRect(frame.rectTransform, new Rect(0, 0, PanelRect.width, PanelRect.height));
                U15.SetRect(toggleRoot, new Rect(PanelRect.width - 36, PanelRect.height - 33, 28, 28));
            }
            toggleIcon.sprite = U15.Sprite(expanded ? "map_collapse" : "map_expand");
            if (!ReferenceEquals(boundRun, run) || map.texture != owner.Host.UIMapTexture)
            {
                boundRun = run;
                map.texture = owner.Host.UIMapTexture;
            }

            RushMinimapProjectionV13 projection = owner.Host.CreateMinimapProjection(PanelRect, expanded);
            TextureRectForReview = projection.DrawRect;
            TextureUvForReview = projection.UvRect;
            map.enabled = map.texture != null && projection.DrawRect.width > 0 && projection.DrawRect.height > 0;
            U15.SetRect(map.rectTransform, Local(projection.DrawRect));
            // RawImage UVs already use the texture's bottom-left convention. No flip is needed.
            map.uvRect = projection.UvRect;

            for (int i = 0; i < tasks.Count; i++)
            {
                if (i >= run.tasks.Count || (run.stage == 1 && run.tasks[i].id == "M1"))
                {
                    tasks[i].Root.gameObject.SetActive(false);
                    continue;
                }
                TaskV9 task = run.tasks[i];
                float alpha = task.done ? .28f : task.state == TaskState.Failed ? .25f : 1f;
                Present(tasks[i], projection, task.position, U15.TaskIcon(task.id), alpha,
                    task.state == TaskState.Failed ? "cross" : task.done ? "check" : null, expanded);
            }

            string exitIcon = run.stage == 0 ? "stage_metro" : run.stage == 1 ? "destination_exit" : "stage_office";
            Present(destination, projection, run.map.exit, exitIcon, 1, null, expanded);
            RefreshDocumentRegion(run, projection, expanded);

            ProjectPlayerForReview = projection.WorldToGui(run.player);
            SetCentered(player.rectTransform, ProjectPlayerForReview - PanelRect.position, 24);
            player.rectTransform.localRotation = Quaternion.Euler(0, 0, Rotation(run.facing));
        }

        void RefreshDocumentRegion(RunV9 run, RushMinimapProjectionV13 projection, bool expanded)
        {
            if (run.stage != 2 || run.tasks.Count < 2 || run.tasks[1].state != TaskState.Active)
            {
                document.Root.gameObject.SetActive(false);
                return;
            }
            FixtureV9 closest = null;
            float distance = float.MaxValue;
            foreach (FixtureV9 fixture in run.fixtures)
            {
                if (fixture.kind != FixtureKind.FileDesk || run.IsDelivered(fixture.index)) continue;
                float d = (fixture.position - run.player).sqrMagnitude;
                if (d < distance) { distance = d; closest = fixture; }
            }
            if (closest == null) { document.Root.gameObject.SetActive(false); return; }
            // Preserve the original coarse region hint, not an exact route to the receiving desk.
            Vector2 region = new Vector2(Mathf.Round(closest.position.x / 8) * 8, Mathf.Round(closest.position.y / 8) * 8);
            Present(document, projection, region, "portrait_" + Mathf.Clamp(closest.index, 0, 4).ToString("00"), 1, null, expanded);
        }

        void Present(Marker marker, RushMinimapProjectionV13 projection, Vector2 world, string icon,
            float alpha, string state, bool expanded)
        {
            bool offscreen;
            Vector2 p = projection.ProjectMarker(world, 14, out offscreen);
            bool show = !(offscreen && !expanded && alpha < .5f);
            marker.Root.gameObject.SetActive(show);
            if (!show) return;
            marker.Icon.sprite = U15.Sprite(icon);
            marker.Icon.color = new Color(1, 1, 1, alpha);
            marker.State.gameObject.SetActive(state != null);
            if (state != null) { marker.State.sprite = U15.Sprite(state); marker.State.color = new Color(1, 1, 1, .95f); }
            bool edge = offscreen && !expanded;
            marker.Arrow.gameObject.SetActive(edge);
            if (edge)
            {
                Vector2 arrowPoint = p;
                p += (projection.ContentRect.center - p).normalized * 18;
                // Arrow is a child of the icon group. Express its GUI offset once, then invert Y once.
                Vector2 localArrow = arrowPoint - p + new Vector2(14, 14);
                SetCentered(marker.Arrow.rectTransform, localArrow, 15);
                marker.Arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, Rotation(world - projection.SourceRect.center));
            }
            U15.SetRect(marker.Root, new Rect(p.x - PanelRect.x - 14, p.y - PanelRect.y - 14, 28, 28));
        }

        Rect Local(Rect absolute) => new Rect(absolute.x - PanelRect.x, absolute.y - PanelRect.y, absolute.width, absolute.height);
        static float Rotation(Vector2 direction) => direction.sqrMagnitude > .00001f ? -Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg : 0;

        static void SetCentered(RectTransform rect, Vector2 guiCenter, float size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(guiCenter.x, -guiCenter.y);
            rect.sizeDelta = new Vector2(size, size);
        }
    }
}
