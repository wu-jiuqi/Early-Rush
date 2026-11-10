using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush.V9
{
    [Serializable]
    public sealed class TaskSite
    {
        public int index;
        public Vector2 center;
        // Ordered intermediate points only; final target is deliberately separate.
        public Vector2[] checkpoints = new Vector2[0];
        public Vector2 start, target;
        public Vector2 entrance, rejoin;
        public float routeDistance;
        public string kind;
    }

    /// <summary>
    /// One unit square per integer centre. Coordinates are fixed world right/up,
    /// not a character-relative or road-relative input frame. No GameObjects.
    /// </summary>
    public sealed class ModuleV9
    {
        public int index;public string kind,floorKey;public Vector2 entrance,exit;public Rect bounds;
        public readonly List<Vector2> fixtureSlots=new List<Vector2>();
        public readonly List<Vector2> decorSlots=new List<Vector2>();
    }

    public sealed class MapData
    {
        public HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        public List<Vector2> route = new List<Vector2>();
        public Vector2 spawn, exit, signal, platform, trainDoor;
        public List<ModuleV9> modules=new List<ModuleV9>();
        public List<TaskSite> tasks = new List<TaskSite>();
        public int seed, stage;
        public float corridorWidth;
        public float PathLength
        {
            get { float result = 0; for (int i = 1; i < route.Count; i++) result += Vector2.Distance(route[i - 1], route[i]); return result; }
        }

        public bool IsWalkable(Vector2 p, float radius)
        {
            if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || radius < 0 || float.IsNaN(radius) || float.IsInfinity(radius)) return false;
            if (!cells.Contains(Cell(p))) return false;
            int minX = Mathf.FloorToInt(p.x - radius + .5f), maxX = Mathf.FloorToInt(p.x + radius + .5f);
            int minY = Mathf.FloorToInt(p.y - radius + .5f), maxY = Mathf.FloorToInt(p.y + radius + .5f);
            for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
            {
                if (cells.Contains(new Vector2Int(x, y))) continue;
                float dx = Mathf.Max(Mathf.Abs(p.x - x) - .5f, 0);
                float dy = Mathf.Max(Mathf.Abs(p.y - y) - .5f, 0);
                if (dx * dx + dy * dy < radius * radius + .000001f) return false;
            }
            return true;
        }

        public static Vector2Int Cell(Vector2 p) { return new Vector2Int(Mathf.FloorToInt(p.x + .5f), Mathf.FloorToInt(p.y + .5f)); }

        // Swept substeps prevent crossing a thin non-walkable region. Axis fallback
        // preserves wall sliding; the caller retains velocity/impulse ownership.
        public Vector2 ResolveMove(Vector2 from, Vector2 delta, float radius)
        {
            if (!IsWalkable(from, radius) || float.IsNaN(delta.x) || float.IsNaN(delta.y) || float.IsInfinity(delta.x) || float.IsInfinity(delta.y)) return from;
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .2f));
            // A corrupt impulse cannot stall a frame or teleport across the world.
            if (steps > 4096) return from;
            Vector2 step = delta / steps, p = from;
            for (int i = 0; i < steps; i++)
            {
                if (IsWalkable(p + step, radius)) { p += step; continue; }
                // Use the larger axis first to reduce corner sticking.
                Vector2 a = Mathf.Abs(step.x) >= Mathf.Abs(step.y) ? new Vector2(step.x, 0) : new Vector2(0, step.y);
                Vector2 b = step - a;
                if (IsWalkable(p + a, radius)) p += a;
                if (IsWalkable(p + b, radius)) p += b;
            }
            return p;
        }

        public Vector2 PointAtDistance(float distance)
        {
            if (route.Count == 0) return Vector2.zero;
            distance = Mathf.Clamp(distance, 0, PathLength);
            for (int i = 1; i < route.Count; i++)
            {
                float length = Vector2.Distance(route[i - 1], route[i]);
                if (distance <= length) return Vector2.Lerp(route[i - 1], route[i], length > 0 ? distance / length : 0);
                distance -= length;
            }
            return route[route.Count - 1];
        }

        public float NearestRouteDistance(Vector2 p)
        {
            float bestSquared = float.MaxValue, bestDistance = 0, along = 0;
            for (int i = 1; i < route.Count; i++)
            {
                Vector2 a = route[i - 1], d = route[i] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, d) / Mathf.Max(.00001f, d.sqrMagnitude));
                float error = (p - a - d * t).sqrMagnitude;
                if (error < bestSquared) { bestSquared = error; bestDistance = along + d.magnitude * t; }
                along += d.magnitude;
            }
            return bestDistance;
        }

        public bool Validate(out string reason)
        {
            reason = "";
            if (route.Count < 8 || tasks.Count != 3) { reason = "route/task cardinality"; return false; }
            if (PathLength < 500 || PathLength > 650) { reason = "route length outside budget"; return false; }
            if (Vector2.Distance(spawn, exit) < 100) { reason = "exit too close to spawn"; return false; }
            bool left = false, right = false;
            for (int i = 2; i < route.Count; i++)
            {
                Vector2 a = route[i - 1] - route[i - 2], b = route[i] - route[i - 1];
                float cross = a.x * b.y - a.y * b.x;
                left |= cross > .01f; right |= cross < -.01f;
            }
            if (!left || !right) { reason = "missing a turn direction"; return false; }
            if (!IsWalkable(spawn, .5f) || !IsWalkable(exit, .5f)) { reason = "endpoint clearance"; return false; }
            // Radius-inflated reachability for both the player and a wide pushcart.
            var reachable = Flood(spawn, 1.05f);
            if (!reachable.Contains(Cell(exit))) { reason = "exit unreachable for cart"; return false; }
            foreach (TaskSite t in tasks)
            {
                if (t.index < 0 || t.index > 2) { reason = "task index"; return false; }
                var points = new List<Vector2> { t.start, t.target, t.center, t.entrance, t.rejoin };
                points.AddRange(t.checkpoints);
                foreach (Vector2 p in points)
                    if (!IsWalkable(p, 1.05f) || !reachable.Contains(Cell(p))) { reason = "task clearance/connectivity " + t.index; return false; }
                if (Vector2.Distance(t.start, t.target) < 3) { reason = "task already in target"; return false; }
            }
            return true;
        }

        HashSet<Vector2Int> Flood(Vector2 p, float radius)
        {
            var found = new HashSet<Vector2Int>(); var queue = new Queue<Vector2Int>();
            Vector2Int root = Cell(p); found.Add(root); queue.Enqueue(root);
            Vector2Int[] offsets = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (queue.Count > 0)
            {
                Vector2Int c = queue.Dequeue();
                foreach (Vector2Int offset in offsets)
                {
                    Vector2Int n = c + offset;
                    if (!found.Contains(n) && IsWalkable(new Vector2(n.x, n.y), radius)) { found.Add(n); queue.Enqueue(n); }
                }
            }
            return found;
        }
    }

    public static class RushMapV9
    {
        // A deterministic bounded grammar, not arbitrary room placement. Alternating
        // turns and separated horizontal legs guarantee readable through routes.
        public static MapData Create(int stage, int seed)
        {
            if (stage < 0 || stage > 2) throw new ArgumentOutOfRangeException("stage");
            MapData result = Generate(stage, seed);
            string reason;
            if (!result.Validate(out reason)) throw new InvalidOperationException("Map validation failed: " + reason);
            return result;
        }

        static MapData Generate(int stage, int seed)
        {
            var rng = new System.Random(unchecked(seed * 397 ^ (stage + 1) * 7919));
            var m = new MapData { seed = seed, stage = stage, corridorWidth = 11 + rng.Next(0, 2) * 2 };
            int horizontal = rng.Next(60, 71), vertical = rng.Next(58, 66);
            int mirror = (seed & 1) == 0 ? 1 : -1;
            Vector2 p = Vector2.zero; m.route.Add(p);
            Vector2[] directions = { Vector2.up, Vector2.right, Vector2.up, Vector2.left, Vector2.up, Vector2.left, Vector2.up, Vector2.right, Vector2.up };
            foreach (Vector2 direction in directions)
            {
                Vector2 d = new Vector2(direction.x * mirror, direction.y);
                p += d * (d.x != 0 ? horizontal : vertical);
                m.route.Add(p);
            }
            m.spawn = m.route[0]; m.exit = m.route[m.route.Count - 1];
            int half = Mathf.FloorToInt(m.corridorWidth / 2);
            for (int i = 1; i < m.route.Count; i++) CarveSegment(m, m.route[i - 1], m.route[i], half);
            // Rooms are attached at the centre of three separated upward legs.
            // A parallel side lane offers distinct entry and rejoin, not a dead end.
            int[] segments = { 2, 4, 6 };
            for (int i = 0; i < 3; i++)
            {
                int segment = segments[i];
                Vector2 a = m.route[segment], b = m.route[segment + 1];
                Vector2 dir = (b - a).normalized, side = new Vector2(dir.y, -dir.x) * (i == 1 ? -mirror : mirror);
                Vector2 midpoint = (a + b) * .5f;
                Vector2 center = midpoint + side * 13;
                Vector2 entry = midpoint - dir * 13, rejoin = midpoint + dir * 13;
                CarveSegment(m, entry, entry + side * 13, 5);
                CarveSegment(m, rejoin, rejoin + side * 13, 5);
                CarveSegment(m, entry + side * 13, rejoin + side * 13, 7);
                Vector2 start = center - dir * 7 - side * 2;
                Vector2 bend = center + dir * 3 - side * 2;
                Vector2 target = center + dir * 7 + side * 3;
                m.tasks.Add(new TaskSite { index = i, center = center, entrance = entry, rejoin = rejoin, start = start, target = target,
                    checkpoints = new[] { bend }, routeDistance = m.NearestRouteDistance(entry) });
            }
            for(int i=1;i<m.route.Count;i++)m.modules.Add(new ModuleV9 { index=i-1, entrance=m.route[i-1], exit=m.route[i], kind="commute", bounds=new Rect(Mathf.Min(m.route[i-1].x,m.route[i].x)-half,Mathf.Min(m.route[i-1].y,m.route[i].y)-half,Mathf.Abs(m.route[i].x-m.route[i-1].x)+half*2,Mathf.Abs(m.route[i].y-m.route[i-1].y)+half*2) });
            // Street learning tasks earlier; metro machine before platform; office deliveries spread later.
            if(stage==1){var machine=m.tasks[0];m.tasks[0]=m.tasks[1];m.tasks[1]=machine;m.tasks[0].index=0;m.tasks[1].index=1;}
            foreach(var module in m.modules)
            {
                module.floorKey=stage==0?"floors-v3/asphalt":stage==1?"floors-v3/ceramic":"floors-v3/carpet";
                module.kind=module.index%3==0?"crossing":module.index%3==1?"promenade":"junction";
                Vector2 forward=(module.exit-module.entrance).normalized,normal=new Vector2(-forward.y,forward.x);
                for(float distance=12;distance<Vector2.Distance(module.entrance,module.exit)-8;distance+=12)
                    module.decorSlots.Add(module.entrance+forward*distance+normal*(half+2));
                foreach(var site in m.tasks)if(module.bounds.Contains(site.entrance))module.fixtureSlots.Add(site.center);
            }
            string[][] kinds={new[]{"breakfast","repair","flip"},new[]{"platform","capsule","cleaning"},new[]{"coffee","delivery","chairs"}};
            for(int i=0;i<3;i++)m.tasks[i].kind=kinds[stage][i];
            m.signal=m.route[3]-Vector2.up*10;
            m.platform=m.tasks[0].center;m.trainDoor=m.platform+Vector2.up*6;
            return m;

        }

        static void CarveSegment(MapData map, Vector2 a, Vector2 b, int half)
        {
            if (Mathf.Abs(a.x - b.x) > .01f && Mathf.Abs(a.y - b.y) > .01f) throw new ArgumentException("Modules must be axis aligned");
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x)) - half, x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x)) + half;
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y)) - half, y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y)) + half;
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) map.cells.Add(new Vector2Int(x, y));
        }
    }
}
