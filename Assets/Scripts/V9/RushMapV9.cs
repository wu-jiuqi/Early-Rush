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
    [Serializable]
    public sealed class ModuleV9
    {
        public int index;public string kind,floorKey;public Vector2 entrance,exit;public Rect bounds;
        public List<Vector2> fixtureSlots=new List<Vector2>();
        public List<Vector2> decorSlots=new List<Vector2>();
    }

    [Serializable] public sealed class ZoneV9 { public int moduleIndex;public string kind,floorKey;public Rect bounds; }
    [Serializable] public sealed class StructureV9 { public string kind,artKey;public Rect bounds;public Vector2 position;public float width; }
    [Serializable] public sealed class PreviewV9
    {
        public string schema="early-rush-v9-map-2";public int stage,seed;public bool timingVerified=false;
        public Vector2 spawn,exit,signal,platform,trainDoor;public Vector2[] route;public Vector2Int[] cells;
        public ModuleV9[] modules;public ZoneV9[] zones;public StructureV9[] structures;public TaskSite[] tasks;
    }
    public sealed class MapData
    {
        // Optional authored geometry. Old seeds retain their original cell collision.
        public Func<Vector2,float,bool> geometryWalkable;
        public RushV4Layout authoredLayout;
        public RushV4NpcSources.Exclusions authoredNpcSources;
        public HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        public List<Vector2> route = new List<Vector2>();
        // Indices starting a disconnected section. No synthetic walkable edge across a train journey.
        public readonly HashSet<int> routeBreaks = new HashSet<int>();
        public Vector2 spawn, exit, signal, platform, trainDoor;
        public List<ModuleV9> modules=new List<ModuleV9>();
        public List<ZoneV9> zones=new List<ZoneV9>();
        public List<StructureV9> structures=new List<StructureV9>();
        public string ToPreviewJson()
        {
            var ordered=new List<Vector2Int>(cells);ordered.Sort((a,b)=>a.y==b.y?a.x.CompareTo(b.x):a.y.CompareTo(b.y));
            return JsonUtility.ToJson(new PreviewV9 { stage=stage,seed=seed,spawn=spawn,exit=exit,signal=signal,platform=platform,trainDoor=trainDoor,
                route=route.ToArray(),cells=ordered.ToArray(),modules=modules.ToArray(),zones=zones.ToArray(),structures=structures.ToArray(),tasks=tasks.ToArray() },true);
        }
        public List<TaskSite> tasks = new List<TaskSite>();
        public int seed, stage;
        public float corridorWidth;
        public float PathLength
        {
            get { float result = 0; for (int i = 1; i < route.Count; i++) if(!routeBreaks.Contains(i)) result += Vector2.Distance(route[i - 1], route[i]); return result; }
        }

        public bool IsWalkable(Vector2 p, float radius)
        {
            if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || radius < 0 || float.IsNaN(radius) || float.IsInfinity(radius)) return false;
            if (geometryWalkable != null) return geometryWalkable(p,radius);
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
                if(routeBreaks.Contains(i))continue;
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
                if(routeBreaks.Contains(i))continue;
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
            if(stage==0&&seed==RushV4StreetMap.Seed)return RushV4StreetMap.Validate(this,out reason);
            if(stage>0&&seed==RushV4StreetMap.Seed)return RushV4InteriorMap.Validate(this,out reason);
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
            if(modules.Count!=9||zones.Count<30||structures.Count<2){reason="insufficient module diversity";return false;}
            var moduleKinds=new HashSet<string>();foreach(var module in modules)moduleKinds.Add(module.kind);
            if(moduleKinds.Count<7){reason="repeated module semantics";return false;}
            int loops=0;foreach(var zone in zones)if(zone.kind=="loop-lane")loops++;
            if(loops<3){reason="missing bypass loops";return false;}
            // Protect the full current task interaction envelope, not just centre dots.
            var operational=new List<Vector2>();
            if(stage==0)
            {
                operational.Add(tasks[1].start+Vector2.right*1.5f);operational.Add(tasks[1].start-Vector2.right*1.5f);
                for(int i=0;i<5;i++){operational.Add(tasks[2].center+new Vector2((i-2)*2,-2));operational.Add(tasks[2].center+new Vector2((i-2)*2,2));}
            }
            if(stage==1)
            {
                operational.Add(platform+new Vector2(-4,-4));operational.Add(platform+new Vector2(4,-4));operational.Add(trainDoor+Vector2.up*2);
                operational.Add(tasks[1].center-Vector2.up*2);
                for(int x=-4;x<=4;x+=2)operational.Add(tasks[2].center+new Vector2(x,0));
            }
            if(stage==2)for(int i=0;i<6;i++)
            {operational.Add(tasks[2].center+new Vector2((i%3-1)*3,(i/3)*3-4));operational.Add(tasks[2].center+new Vector2((i%3-1)*3,(i/3)*3+4));}
            foreach(var point in operational)if(!IsWalkable(point,1.05f)||!reachable.Contains(Cell(point))){reason="task operational envelope blocked";return false;}
            foreach(var structure in structures)
                if(IsWalkable(structure.bounds.center,.1f)){reason="structure footprint not removed";return false;}
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
            MapData result = seed==RushV4StreetMap.Seed ? (stage==0?RushV4StreetMap.Create():RushV4InteriorMap.Create(stage,seed)) : RushStreetArtV14.Applies(stage,seed) ? RushStreetArtV14.Create() : Generate(stage, seed);
            string reason;
            if (!result.Validate(out reason)) throw new InvalidOperationException("Map validation failed: " + reason);
            return result;
        }

        static MapData Generate(int stage, int seed)
        {
            var rng=new System.Random(unchecked(seed*397^(stage+1)*7919));
            var m=new MapData { seed=seed,stage=stage,corridorWidth=9 };
            int mirror=(seed&1)==0?1:-1;
            string baseFloor=stage==0?"floors-v3/asphalt":stage==1?"floors-v3/ceramic":"floors-v3/carpet";
            // Street courtyards stay asphalt; office workrooms stay carpet. Only dedicated
            // edge/threshold patches may use a different material, never the full task room.
            string secondary=stage==0?"floors-v3/asphalt":stage==1?"legacy-floors/floor-tile":"floors-v3/carpet";
            string[][] semantic={
                new[]{"residential-court","shop-avenue","signal-junction","market-square","repair-block","cycle-garden","puzzle-court","station-square","station-approach"},
                new[]{"ticket-hall","concourse-loop","gate-approach","transfer-hall","platform-hall","carriage-link","arrival-cleaning-hall","exit-concourse","station-exit"},
                new[]{"reception","coffee-corridors","document-hub","open-office","team-workrooms","atrium-loop","chair-workshop","meeting-foyer","meeting-room"}};
            Vector2 p=Vector2.zero;m.route.Add(p);
            Vector2[] directions={Vector2.up,Vector2.right,Vector2.up,Vector2.left,Vector2.up,Vector2.left,Vector2.up,Vector2.right,Vector2.up};
            for(int i=0;i<directions.Length;i++)
            {
                Vector2 d=new Vector2(directions[i].x*mirror,directions[i].y);
                // Variable module lengths and widths are spatial variety, not an elapsed-time guarantee.
                float length=rng.Next(56,69);Vector2 end=p+d*length;int half=stage==1?(i%3==1?4:5):stage==2?(i%3==1?3:4):(i%3==1?3:4);
                var module=new ModuleV9{index=i,kind=semantic[stage][i],floorKey=baseFloor,entrance=p,exit=end,
                    bounds=SegmentBounds(p,end,half)};m.modules.Add(module);
                CarveSegment(m,p,end,half);Zone(m,module,"through-lane",SegmentBounds(p,end,half),baseFloor);
                Vector2 n=new Vector2(-d.y,d.x),middle=(p+end)*.5f;
                if(i%2==0)
                {
                    // A meaningful arrival/interaction room wider than its entrance; two side alcoves.
                    Rect room=OrientedBounds(middle,d,stage==1?11:stage==2?8:9,stage==1?10:stage==2?8:9);CarveRoom(m,module,stage==0?"street-court":stage==1?"station-hall":"office-workroom",room,secondary);
                    for(int side=-1;side<=1;side+=2)
                        CarveRoom(m,module,"alcove",OrientedBounds(middle+d*7+n*side*12,d,5,5),secondary);
                }
                else
                {
                    // Two entrances to a parallel passage form a genuine traversable loop.
                    Vector2 first=p+d*14,last=end-d*14,offset=n*(i==3?-1:1)*(stage==1?17:stage==2?13:14);
                    CarveSegment(m,first,first+offset,3);CarveSegment(m,first+offset,last+offset,3);CarveSegment(m,last+offset,last,3);
                    Zone(m,module,"loop-entry",SegmentBounds(first,first+offset,3),secondary);
                    Zone(m,module,"loop-lane",SegmentBounds(first+offset,last+offset,3),secondary);
                    Zone(m,module,"loop-rejoin",SegmentBounds(last+offset,last,3),secondary);
                    CarveRoom(m,module,"decision-plaza",OrientedBounds(middle,d,7,8),secondary);
                }
                for(float distance=10;distance<length-8;distance+=11)
                {
                    module.decorSlots.Add(p+d*distance+n*(half+2));
                    module.decorSlots.Add(p+d*distance-n*(half+2));
                }
                m.route.Add(end);p=end;
            }
            m.spawn=m.route[0];m.exit=m.route[m.route.Count-1];
            CarveRoom(m,m.modules[0],"spawn-buffer",OrientedBounds(m.spawn+Vector2.up*4,Vector2.up,8,8),secondary);
            CarveRoom(m,m.modules[8],stage==2?"meeting-attendance":"exit-buffer",OrientedBounds(m.exit-Vector2.up*3,Vector2.up,8,9),secondary);
            int[][] stageSegments={new[]{0,4,6},new[]{4,0,6},new[]{0,2,6}};
            string[][] kinds={new[]{"breakfast","repair","flip"},new[]{"platform","capsule","cleaning"},new[]{"coffee","delivery","chairs"}};
            for(int i=0;i<3;i++)
            {
                int segment=stageSegments[stage][i];var module=m.modules[segment];
                Vector2 a=module.entrance,b=module.exit,dir=(b-a).normalized,side=new Vector2(dir.y,-dir.x)*(i==1?-mirror:mirror);
                Vector2 midpoint=(a+b)*.5f,center=midpoint+side*23,entry=midpoint-dir*15,rejoin=midpoint+dir*15;
                CarveSegment(m,entry,entry+side*23,3);CarveSegment(m,rejoin,rejoin+side*23,3);
                Zone(m,module,"task-entry",SegmentBounds(entry,entry+side*23,3),secondary);
                Zone(m,module,"task-rejoin",SegmentBounds(rejoin,rejoin+side*23,3),secondary);
                // Compact task bay plus a narrow entry-to-rejoin spine: no 36x20 empty hall.
                CarveSegment(m,entry+side*23,rejoin+side*23,3);
                Zone(m,module,"task-spine",SegmentBounds(entry+side*23,rejoin+side*23,3),secondary);
                CarveRoom(m,module,kinds[stage][i]+"-room",OrientedBounds(center,dir,11,7),secondary);
                var site=new TaskSite{index=i,kind=kinds[stage][i],center=center,entrance=entry,rejoin=rejoin,
                    start=center-dir*7-side*2,target=center+dir*7+side*3,checkpoints=new Vector2[0],routeDistance=m.NearestRouteDistance(entry)};
                m.tasks.Add(site);module.fixtureSlots.Add(center);
            }
            m.signal=m.route[3]-Vector2.up*10;m.platform=m.tasks[0].center;m.trainDoor=m.platform+Vector2.up*6;
            if(stage==1)
            {
                CarveRoom(m,m.modules[4],"waiting-sides",new Rect(m.platform.x-7,m.platform.y-5,14,8),secondary);
                CarveRoom(m,m.modules[4],"carriage",new Rect(m.trainDoor.x-7,m.trainDoor.y,14,9),baseFloor);
            }
            // Functional backdrops belong to map occupancy, not arbitrary renderer decoration.
            // Never cover the centre interaction, wheel starts, flip touch row or chair sockets.
            for(int i=0;i<3;i++)
            {
                var t=m.tasks[i];Vector2 c=t.center;
                string main=stage==0?(i==0?"street/storefront":i==1?"street/boxes":"street/bench"):
                    stage==1?(i==0?"metro/platform-bench":i==1?"metro/kiosk":"metro/pillar"):
                    i==1?"office/filing-cabinet":"office/desk-chair";
                Vector2 back=c+Vector2.up*(i==2?10:4);
                if(stage==0&&i==1)back=t.target+Vector2.up*3;
                if(stage==2&&i==1)back=t.start+Vector2.up*3;
                if(stage==1&&i==0)back=c+new Vector2(6,1);
                AddStructure(m,"task-backdrop-"+i,main,back,3,2,3.5f);
                string small=stage==0?"street/bush-planter":stage==1?"metro/pillar":"office/plant";
                // Two corners give each bay a composed foreground/background without a prop wall.
                AddStructure(m,"task-corner-"+i,small,c+new Vector2(-6,8),1,2,1.7f);
                AddStructure(m,"task-corner-"+i,small,c+new Vector2(6,-8),1,2,1.7f);
                string sideArt=stage==0?(i==1?"street/boxes":"street/bench"):stage==1?"metro/platform-bench":i==1?"office/filing-cabinet":"office/sofa";
                AddStructure(m,"task-side-"+i,sideArt,c+new Vector2(-6,-2),2,2,2.4f);
            }
            // Visible furniture/planter islands create choices within the wider halls, never an
            // invisible collider. Their exact footprints are removed from the walkable cell mask.
            foreach(var module in m.modules)
            {
                Vector2 d=(module.exit-module.entrance).normalized,n=new Vector2(-d.y,d.x),middle=(module.exit+module.entrance)*.5f;
                if(module.index%2==0)continue;
                Vector2 at=MapData.Cell(middle-n*6);Rect rect=new Rect(at.x-1.5f,at.y-1.5f,3,3);
                bool safe=true;foreach(var task in m.tasks)if(Vector2.Distance(at,task.center)<18)safe=false;
                if(!safe)continue;
                string art=stage==0?"street/bush-planter":stage==1?"metro/pillar":"office/desk-chair";
                AddStructure(m,stage==0?"planter-island":stage==1?"hall-pillar":"workstation-island",art,at,3,3,3.4f);
            }
            return m;
        }
        static void AddStructure(MapData map,string kind,string art,Vector2 centre,int widthCells,int heightCells,float artWidth)
        {
            Vector2Int cell=MapData.Cell(centre);int x0=cell.x-widthCells/2,y0=cell.y-heightCells/2;
            Rect footprint=new Rect(x0-.5f,y0-.5f,widthCells,heightCells);
            map.structures.Add(new StructureV9{kind=kind,artKey=art,bounds=footprint,
                position=new Vector2(footprint.center.x,footprint.yMin+.2f),width=artWidth});
            for(int y=y0;y<y0+heightCells;y++)for(int x=x0;x<x0+widthCells;x++)map.cells.Remove(new Vector2Int(x,y));
        }
        static Rect SegmentBounds(Vector2 a,Vector2 b,int half)
        {return Rect.MinMaxRect(Mathf.Min(a.x,b.x)-half-.5f,Mathf.Min(a.y,b.y)-half-.5f,Mathf.Max(a.x,b.x)+half+.5f,Mathf.Max(a.y,b.y)+half+.5f);}
        static Rect OrientedBounds(Vector2 center,Vector2 direction,float longitudinal,float lateral)
        {return Mathf.Abs(direction.y)>.5f?new Rect(center.x-lateral,center.y-longitudinal,lateral*2,longitudinal*2):new Rect(center.x-longitudinal,center.y-lateral,longitudinal*2,lateral*2);}
        static void Zone(MapData map,ModuleV9 module,string kind,Rect bounds,string floor)
        {
            map.zones.Add(new ZoneV9{moduleIndex=module.index,kind=kind,bounds=bounds,floorKey=floor});
            module.bounds=Rect.MinMaxRect(Mathf.Min(module.bounds.xMin,bounds.xMin),Mathf.Min(module.bounds.yMin,bounds.yMin),Mathf.Max(module.bounds.xMax,bounds.xMax),Mathf.Max(module.bounds.yMax,bounds.yMax));
        }
        static void CarveRoom(MapData map,ModuleV9 module,string kind,Rect bounds,string floor)
        {
            for(int y=Mathf.CeilToInt(bounds.yMin);y<=Mathf.FloorToInt(bounds.yMax);y++)
                for(int x=Mathf.CeilToInt(bounds.xMin);x<=Mathf.FloorToInt(bounds.xMax);x++)map.cells.Add(new Vector2Int(x,y));
            Zone(map,module,kind,bounds,floor);
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
