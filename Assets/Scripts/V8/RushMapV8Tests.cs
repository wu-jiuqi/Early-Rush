using System;
using UnityEngine;

namespace EarlyRush.V8
{
    // No NUnit dependency. Run from editor build preflight or QA executable.
    public static class RushMapV8Tests
    {
        public static int RunAll(int seeds = 100)
        {
            int passed = 0;
            for (int stage = 0; stage < 3; stage++) for (int seed = 0; seed < seeds; seed++)
            {
                MapData map = RushMapV8.Create(stage, seed);
                string reason;
                Require(map.Validate(out reason), "validate: " + reason);
                Require(map.PathLength >= 500 && map.PathLength <= 650, "length budget");
                Require(map.tasks.Count == 3, "three task sites");
                Require(map.IsWalkable(map.spawn, .45f), "spawn clearance");
                Require(map.IsWalkable(map.exit, 1.05f), "cart exit clearance");
                for (int i = 1; i < map.route.Count; i++)
                {
                    Vector2 a = map.route[i - 1], b = map.route[i];
                    int samples = Mathf.CeilToInt(Vector2.Distance(a, b));
                    for (int j = 0; j <= samples; j++) Require(map.IsWalkable(Vector2.Lerp(a, b, (float)j / samples), 1.05f), "main route swept clearance");
                }
                Vector2 up = map.ResolveMove(map.spawn, Vector2.up, .45f);
                Vector2 right = map.ResolveMove(map.spawn, Vector2.right, .45f);
                Require(Vector2.Distance(up, map.spawn + Vector2.up) < .01f, "fixed W/up");
                Require(Vector2.Distance(right, map.spawn + Vector2.right) < .01f, "fixed D/right");
                Vector2 blocked = map.ResolveMove(map.spawn, Vector2.right * 60, .45f);
                Require(map.IsWalkable(blocked, .45f) && blocked.x < map.corridorWidth, "no wall tunnelling");
                Vector2 slid = map.ResolveMove(blocked, new Vector2(10, 10), .45f);
                Require(map.IsWalkable(slid, .45f) && slid.y > 8, "wall sliding");
                Vector2 huge = map.ResolveMove(map.spawn, Vector2.up * 1000000, .45f);
                Require(huge == map.spawn, "corrupt impulse guard");
                Require(!map.IsWalkable(new Vector2(float.NaN, 0), .45f), "invalid coordinate guard");
                if (stage == 1)
                {
                    Require(map.tasks[0].checkpoints.Length == 3, "backpack ordered checkpoints");
                    Require(Vector2.Distance(map.tasks[0].target, map.exit) >= 6, "bag target separate from exit");
                    Require(Vector2.Distance(map.tasks[0].start, map.tasks[0].target) > 100, "backpack long push");
                }
                passed++;
            }
            // Repeat fixed seeds: route, geometry and task locations all deterministic.
            for (int stage = 0; stage < 3; stage++)
            {
                MapData a = RushMapV8.Create(stage, 42), b = RushMapV8.Create(stage, 42);
                Require(a.cells.SetEquals(b.cells), "deterministic cells");
                for (int i = 0; i < a.route.Count; i++) Require(a.route[i] == b.route[i], "deterministic route");
                for (int i = 0; i < 3; i++) Require(a.tasks[i].start == b.tasks[i].start && a.tasks[i].target == b.tasks[i].target, "deterministic tasks");
            }
            Debug.Log("RUSH_V8_MAP_QA passed=" + passed + " seed-stage maps; deterministic, radius clearance, turns and collision sweep checked");
            return passed;
        }
        static void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("RUSH_V8_MAP_QA: " + label);
        }
    }
}
