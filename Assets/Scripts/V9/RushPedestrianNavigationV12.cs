using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace EarlyRush.V9
{
    public sealed partial class RunV9
    {
        // One immutable geometry graph per run, shared by all pedestrians. Search scratch
        // is reused synchronously; returned paths are always separate, caller-owned lists.
        readonly Dictionary<Vector2Int, int> pedestrianNodeIndex = new Dictionary<Vector2Int, int>();
        readonly List<Vector2> pedestrianNodes = new List<Vector2>();
        IReadOnlyList<Vector2> pedestrianPointView = Array.Empty<Vector2>();
        static readonly Vector2Int[] PedestrianDirections =
        {
            Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down
        };
        int[] pedestrianEdges, pedestrianComponents, pedestrianSeen, pedestrianParent;
        int[] pedestrianHeap, pedestrianHeapPosition;
        float[] pedestrianCost, pedestrianPriority;
        int pedestrianSearchStamp, pedestrianHeapCount;
        bool pedestrianNavigationReady;

        public IReadOnlyList<Vector2> PedestrianPoints => pedestrianPointView;
        public int PedestrianNavigationNodeCount => pedestrianNodes.Count;
        public int PedestrianComponentCount { get; private set; }
        public long PedestrianPathQueries { get; private set; }
        public long PedestrianAvoidanceQueries { get; private set; }
        public long PedestrianFailedQueries { get; private set; }
        public long PedestrianExpandedNodes { get; private set; }
        public int PedestrianLastExpandedNodes { get; private set; }
        public int PedestrianPeakExpandedNodes { get; private set; }
        public long PedestrianLineQueries { get; private set; }
        public long PedestrianLineSamples { get; private set; }
        public double PedestrianNavigationBuildMilliseconds { get; private set; }

        /// <summary>Call after static walking reservations are set up, once per map.</summary>
        public void InitializePedestrianNavigation()
        {
            var watch = Stopwatch.StartNew();
            pedestrianNavigationReady = false;
            pedestrianNodeIndex.Clear();
            pedestrianNodes.Clear();
            PedestrianPathQueries = PedestrianAvoidanceQueries = PedestrianFailedQueries = PedestrianExpandedNodes = 0;
            PedestrianLastExpandedNodes = PedestrianPeakExpandedNodes = 0;
            PedestrianLineQueries = PedestrianLineSamples = 0;
            PedestrianComponentCount = 0;
            pedestrianSearchStamp = pedestrianHeapCount = 0;

            // Use actual floor cells, including bypass lanes and intersections. Sorting
            // makes IDs, connected components and A* tie breaks deterministic.
            var cells = new List<Vector2Int>(map.cells);
            cells.Sort((a, b) => a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            foreach (var cell in cells)
            {
                Vector2 point = new Vector2(cell.x, cell.y);
                if (!map.IsWalkable(point, .5f) || IsPedestrianReserved(point, .5f)) continue;
                pedestrianNodeIndex.Add(cell, pedestrianNodes.Count);
                pedestrianNodes.Add(point);
            }
            pedestrianPointView = pedestrianNodes.AsReadOnly();
            int count = pedestrianNodes.Count;
            pedestrianEdges = new int[count * 4];
            pedestrianComponents = new int[count];
            pedestrianSeen = new int[count];
            pedestrianParent = new int[count];
            pedestrianHeap = new int[count];
            pedestrianHeapPosition = new int[count];
            pedestrianCost = new float[count];
            pedestrianPriority = new float[count];
            for (int i = 0; i < count * 4; i++) pedestrianEdges[i] = -1;
            for (int i = 0; i < count; i++) pedestrianComponents[i] = -1;

            // Cardinal edges cannot cut grid corners. Test the continuous connection
            // too, so a reservation between otherwise valid node centres still blocks it.
            for (int i = 0; i < count; i++)
            {
                Vector2Int cell = MapData.Cell(pedestrianNodes[i]);
                for (int direction = 0; direction < 2; direction++)
                {
                    if (!pedestrianNodeIndex.TryGetValue(cell + PedestrianDirections[direction], out int other)) continue;
                    if (!PedestrianLineClear(pedestrianNodes[i], pedestrianNodes[other])) continue;
                    pedestrianEdges[i * 4 + direction] = other;
                    pedestrianEdges[other * 4 + direction + 2] = i;
                }
            }

            var queue = new Queue<int>();
            for (int root = 0; root < count; root++)
            {
                if (pedestrianComponents[root] >= 0) continue;
                int component = PedestrianComponentCount++;
                pedestrianComponents[root] = component;
                queue.Enqueue(root);
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    for (int direction = 0; direction < 4; direction++)
                    {
                        int next = pedestrianEdges[current * 4 + direction];
                        if (next < 0 || pedestrianComponents[next] >= 0) continue;
                        pedestrianComponents[next] = component;
                        queue.Enqueue(next);
                    }
                }
            }
            pedestrianNavigationReady = true;
            PedestrianNavigationBuildMilliseconds = watch.Elapsed.TotalMilliseconds;
        }

        /// <summary>Returns -1 outside a reachable navigation anchor; never crosses walls to snap.</summary>
        public int PedestrianComponent(Vector2 point)
        {
            if (!pedestrianNavigationReady) InitializePedestrianNavigation();
            int node = PedestrianAnchor(point, .42f);
            return node < 0 ? -1 : pedestrianComponents[node];
        }

        /// <summary>Includes the requested start and exact goal. Null means no valid route.</summary>
        public List<Vector2> FindPedestrianPath(Vector2 from, Vector2 goal, Vector2? avoidPoint = null)
        {
            if (!pedestrianNavigationReady) InitializePedestrianNavigation();
            PedestrianPathQueries++;
            PedestrianLastExpandedNodes = 0;
            if (avoidPoint.HasValue)
            {
                PedestrianAvoidanceQueries++;
                if (!PedestrianFinite(avoidPoint.Value)) return PedestrianNoPath();
            }
            int start = PedestrianAnchor(from, .42f), finish = PedestrianAnchor(goal, .48f);
            if (start < 0 || finish < 0 || pedestrianComponents[start] != pedestrianComponents[finish])
                return PedestrianNoPath();
            if (!avoidPoint.HasValue && PedestrianLineClear(from, goal)) return new List<Vector2> { from, goal };

            // Generational marks avoid clearing an entire map for every NPC query.
            if (pedestrianSearchStamp == int.MaxValue)
            {
                Array.Clear(pedestrianSeen, 0, pedestrianSeen.Length);
                pedestrianSearchStamp = 0;
            }
            pedestrianSearchStamp++;
            pedestrianHeapCount = 0;
            pedestrianSeen[start] = pedestrianSearchStamp;
            pedestrianCost[start] = 0;
            pedestrianParent[start] = -1;
            pedestrianPriority[start] = PedestrianHeuristic(start, finish);
            PedestrianHeapPush(start);
            while (pedestrianHeapCount > 0)
            {
                int current = PedestrianHeapPop();
                PedestrianLastExpandedNodes++;
                if (current == finish)
                {
                    PedestrianRecordExpansions();
                    var raw = new List<Vector2>();
                    for (int at = finish; at >= 0; at = pedestrianParent[at]) raw.Add(pedestrianNodes[at]);
                    raw.Reverse();
                    if ((raw[0] - from).sqrMagnitude > .000001f) raw.Insert(0, from);
                    else raw[0] = from;
                    if ((raw[raw.Count - 1] - goal).sqrMagnitude > .000001f) raw.Add(goal);
                    else raw[raw.Count - 1] = goal;
                    // Preserve the chosen cost corridor when avoiding a temporary jam.
                    // Straight collinear runs may collapse, but corners cannot be cut.
                    return avoidPoint.HasValue ? CollapsePedestrianStraightRuns(raw) : SmoothPedestrianPath(raw);
                }
                for (int direction = 0; direction < 4; direction++)
                {
                    int next = pedestrianEdges[current * 4 + direction];
                    if (next < 0) continue;
                    float congestion = 0;
                    if (avoidPoint.HasValue)
                        congestion = 10 * Mathf.Max(0, 1 - Vector2.Distance(pedestrianNodes[next], avoidPoint.Value) / 3);
                    float cost = pedestrianCost[current] + 1 + congestion;
                    if (pedestrianSeen[next] == pedestrianSearchStamp)
                    {
                        // Manhattan distance is consistent for cardinal edges costing >= 1;
                        // closed nodes cannot later need reopening.
                        if (pedestrianHeapPosition[next] < 0 || cost >= pedestrianCost[next]) continue;
                        pedestrianCost[next] = cost;
                        pedestrianParent[next] = current;
                        pedestrianPriority[next] = cost + PedestrianHeuristic(next, finish);
                        PedestrianHeapUp(pedestrianHeapPosition[next]);
                    }
                    else
                    {
                        pedestrianSeen[next] = pedestrianSearchStamp;
                        pedestrianCost[next] = cost;
                        pedestrianParent[next] = current;
                        pedestrianPriority[next] = cost + PedestrianHeuristic(next, finish);
                        PedestrianHeapPush(next);
                    }
                }
            }
            PedestrianRecordExpansions();
            return PedestrianNoPath();
        }

        /// <summary>Conservative swept clearance against floor geometry and walking reservations.</summary>
        public bool PedestrianLineClear(Vector2 from, Vector2 to, float radius = .48f)
        {
            PedestrianLineQueries++;
            if (!PedestrianFinite(from) || !PedestrianFinite(to) || float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0)
                return false;
            if (!PedestrianPointClear(from, radius) || !PedestrianPointClear(to, radius)) return false;
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length < .00001f) return true;
            if (length > 8192) return false; // Corrupt coordinates cannot monopolize the simulation.
            int steps = Mathf.CeilToInt(length / .1f);
            // A midpoint disk enlarged by half a substep contains that whole swept
            // subsegment. This prevents thin corners/reservations slipping between samples.
            float sweptRadius = radius + length / steps * .5f;
            for (int i = 0; i < steps; i++)
                if (!PedestrianPointClear(from + delta * ((i + .5f) / steps), sweptRadius)) return false;
            return true;
        }

        bool PedestrianPointClear(Vector2 point, float radius)
        {
            PedestrianLineSamples++;
            return map.IsWalkable(point, radius) && !IsPedestrianReserved(point, radius);
        }

        static bool PedestrianFinite(Vector2 point) =>
            !float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsInfinity(point.x) && !float.IsInfinity(point.y);

        int PedestrianAnchor(Vector2 point, float radius)
        {
            if (!PedestrianFinite(point) || !PedestrianPointClear(point, radius)) return -1;
            Vector2Int cell = MapData.Cell(point);
            if (pedestrianNodeIndex.TryGetValue(cell, out int exact) && PedestrianLineClear(point, pedestrianNodes[exact], radius)) return exact;
            int best = -1;
            float bestDistance = 2.5f * 2.5f;
            // Only local, collision-checked attachment; never an all-map nearest search.
            for (int y = -2; y <= 2; y++) for (int x = -2; x <= 2; x++)
            {
                if (!pedestrianNodeIndex.TryGetValue(cell + new Vector2Int(x, y), out int candidate)) continue;
                float distance = (pedestrianNodes[candidate] - point).sqrMagnitude;
                if (distance >= bestDistance || !PedestrianLineClear(point, pedestrianNodes[candidate], radius)) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        List<Vector2> PedestrianNoPath() { PedestrianFailedQueries++; return null; }
        void PedestrianRecordExpansions()
        {
            PedestrianExpandedNodes += PedestrianLastExpandedNodes;
            PedestrianPeakExpandedNodes = Mathf.Max(PedestrianPeakExpandedNodes, PedestrianLastExpandedNodes);
        }
        float PedestrianHeuristic(int from, int to)
        {
            Vector2 delta = pedestrianNodes[from] - pedestrianNodes[to];
            return Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        }

        List<Vector2> SmoothPedestrianPath(List<Vector2> raw)
        {
            var result = new List<Vector2> { raw[0] };
            int anchor = 0;
            while (anchor < raw.Count - 1)
            {
                int best = anchor + 1, stride = 2;
                // A pedestrian displaced to a wall may only have its physical .42 m
                // clearance. Let the first segment recover onto the .5 m static grid.
                float radius = anchor == 0 ? .42f : .48f;
                // Exponential probes avoid repeatedly scanning a long straight corridor.
                while (best < raw.Count - 1)
                {
                    int probe = Mathf.Min(raw.Count - 1, anchor + stride);
                    if (probe <= best) break;
                    if (PedestrianLineClear(raw[anchor], raw[probe], radius))
                    {
                        best = probe;
                        stride *= 2;
                        continue;
                    }
                    int low = best + 1, high = probe - 1;
                    while (low <= high)
                    {
                        int middle = low + (high - low) / 2;
                        if (PedestrianLineClear(raw[anchor], raw[middle], radius)) { best = middle; low = middle + 1; }
                        else high = middle - 1;
                    }
                    break;
                }
                result.Add(raw[best]);
                anchor = best;
            }
            return result;
        }

        static List<Vector2> CollapsePedestrianStraightRuns(List<Vector2> raw)
        {
            var result = new List<Vector2> { raw[0] };
            for (int i = 1; i < raw.Count - 1; i++)
            {
                Vector2 before = raw[i] - raw[i - 1], after = raw[i + 1] - raw[i];
                float cross = before.x * after.y - before.y * after.x;
                if (Mathf.Abs(cross) > .00001f || Vector2.Dot(before, after) <= 0) result.Add(raw[i]);
            }
            if (raw.Count > 1) result.Add(raw[raw.Count - 1]);
            return result;
        }

        bool PedestrianHeapBefore(int first, int second)
        {
            if (pedestrianPriority[first] != pedestrianPriority[second]) return pedestrianPriority[first] < pedestrianPriority[second];
            if (pedestrianCost[first] != pedestrianCost[second]) return pedestrianCost[first] > pedestrianCost[second];
            return first < second;
        }
        void PedestrianHeapPush(int node)
        {
            int position = pedestrianHeapCount++;
            pedestrianHeap[position] = node;
            pedestrianHeapPosition[node] = position;
            PedestrianHeapUp(position);
        }
        void PedestrianHeapUp(int position)
        {
            int node = pedestrianHeap[position];
            while (position > 0)
            {
                int parent = (position - 1) / 2, other = pedestrianHeap[parent];
                if (!PedestrianHeapBefore(node, other)) break;
                pedestrianHeap[position] = other;
                pedestrianHeapPosition[other] = position;
                position = parent;
            }
            pedestrianHeap[position] = node;
            pedestrianHeapPosition[node] = position;
        }
        int PedestrianHeapPop()
        {
            int result = pedestrianHeap[0];
            pedestrianHeapPosition[result] = -1;
            int count = --pedestrianHeapCount;
            if (count == 0) return result;
            int node = pedestrianHeap[count], position = 0;
            while (position * 2 + 1 < count)
            {
                int child = position * 2 + 1;
                if (child + 1 < count && PedestrianHeapBefore(pedestrianHeap[child + 1], pedestrianHeap[child])) child++;
                int other = pedestrianHeap[child];
                if (!PedestrianHeapBefore(other, node)) break;
                pedestrianHeap[position] = other;
                pedestrianHeapPosition[other] = position;
                position = child;
            }
            pedestrianHeap[position] = node;
            pedestrianHeapPosition[node] = position;
            return result;
        }
    }
}
