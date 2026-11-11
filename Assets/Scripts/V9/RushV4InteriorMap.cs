using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush.V9
{
    /// <summary>Independent ArtV4 interior geometry factory. Caller owns simulation setup and rendering.</summary>
    public static class RushV4InteriorMap
    {
        public const int Seed=91027;
        public static bool Applies(int stage,int seed) { return (stage==1||stage==2)&&seed==Seed; }
        struct Segment { public Vector2 a,b;public float half;public Rect bounds; }
        sealed class Geometry
        {
            public readonly List<Segment> segments=new List<Segment>();
            public readonly List<Rect> rooms=new List<Rect>();
            readonly Dictionary<Vector2Int,List<Rect>> blocks=new Dictionary<Vector2Int,List<Rect>>();
            const float Bucket=4;
            public void Block(Rect r)
            {
                for(int y=Mathf.FloorToInt(r.yMin/Bucket);y<=Mathf.FloorToInt(r.yMax/Bucket);y++)
                for(int x=Mathf.FloorToInt(r.xMin/Bucket);x<=Mathf.FloorToInt(r.xMax/Bucket);x++)
                {var key=new Vector2Int(x,y);if(!blocks.TryGetValue(key,out var list))blocks[key]=list=new List<Rect>();list.Add(r);}
            }
            public bool Contains(Vector2 p,float radius)
            {
                bool inside=false;
                foreach(var r in rooms) if(p.x-radius>=r.xMin&&p.x+radius<=r.xMax&&p.y-radius>=r.yMin&&p.y+radius<=r.yMax){inside=true;break;}
                if(!inside) foreach(var s in segments)
                {
                    if(radius>s.half||!s.bounds.Contains(p))continue;
                    var d=s.b-s.a;float t=Mathf.Clamp01(Vector2.Dot(p-s.a,d)/Mathf.Max(.00001f,d.sqrMagnitude));
                    if((p-s.a-d*t).sqrMagnitude<=(s.half-radius)*(s.half-radius)){inside=true;break;}
                }
                if(!inside)return false;
                for(int y=Mathf.FloorToInt((p.y-radius)/Bucket);y<=Mathf.FloorToInt((p.y+radius)/Bucket);y++)
                for(int x=Mathf.FloorToInt((p.x-radius)/Bucket);x<=Mathf.FloorToInt((p.x+radius)/Bucket);x++)
                if(blocks.TryGetValue(new Vector2Int(x,y),out var list)) foreach(var b in list)
                {
                    float dx=Mathf.Max(Mathf.Max(b.xMin-p.x,0),p.x-b.xMax),dy=Mathf.Max(Mathf.Max(b.yMin-p.y,0),p.y-b.yMax);
                    if(dx*dx+dy*dy<=radius*radius+1e-7f)return false;
                }
                return true;
            }
        }
        static Rect Bounds(Vector2 a,Vector2 b,float half) { return Rect.MinMaxRect(Mathf.Min(a.x,b.x)-half,Mathf.Min(a.y,b.y)-half,Mathf.Max(a.x,b.x)+half,Mathf.Max(a.y,b.y)+half); }
        public static MapData Create(int stage,int seed=Seed)
        {
            if(stage!=1&&stage!=2)throw new ArgumentOutOfRangeException(nameof(stage));
            var layout=RushV4Layout.Load(stage);var g=new Geometry();
            string floor=stage==1?"floors-v3/ceramic":"floors-v3/carpet";
            var m=new MapData{stage=stage,seed=seed,corridorWidth=layout.routeWidth/RushV4Layout.PixelsPerUnit};
            m.authoredLayout=layout;m.authoredNpcSources=RushV4NpcSources.BuildExclusions(layout);m.geometryWalkable=g.Contains;m.spawn=layout.World(layout.start);m.exit=layout.World(layout.end);
            foreach(var corridor in layout.corridors)
            {
                if(corridor.points==null||corridor.points.Length<2)throw new InvalidOperationException("Invalid authored corridor");
                for(int i=1;i<corridor.points.Length;i++)
                {
                    var a=layout.World(corridor.points[i-1]);var b=layout.World(corridor.points[i]);float half=corridor.width/RushV4Layout.PixelsPerUnit/2;
                    var bounds=Bounds(a,b,half);g.segments.Add(new Segment{a=a,b=b,half=half,bounds=bounds});
                    int module=corridor.kind=="main"?m.modules.Count:0;
                    if(corridor.kind=="main")m.modules.Add(new ModuleV9{index=module,kind="v4-"+layout.id+"-leg-"+module,floorKey=floor,entrance=a,exit=b,bounds=bounds});
                    m.zones.Add(new ZoneV9{moduleIndex=module,kind=corridor.kind=="main"?"through-lane":"loop-lane",floorKey=floor,bounds=bounds});
                }
            }
            foreach(var section in layout.MainRouteSections())
            {
                if(m.route.Count>0)m.routeBreaks.Add(m.route.Count);
                m.route.AddRange(section);
            }
            foreach(var room in layout.rooms??new RushV4Layout.Box[0])
            {var bounds=layout.WorldRect(room);g.rooms.Add(bounds);m.zones.Add(new ZoneV9{moduleIndex=0,kind="task-court",floorKey=floor,bounds=bounds});}
            int blockerIndex=0;foreach(var block in layout.blockers??new RushV4Layout.Box[0])
            {if(m.authoredNpcSources.IsExcluded(blockerIndex++))continue;var bounds=layout.WorldRect(block);g.Block(bounds);m.structures.Add(new StructureV9{kind="v4-footprint",artKey="",bounds=bounds,position=bounds.center,width=bounds.width});}
            for(int y=0;y<=Mathf.CeilToInt(layout.worldHeight/RushV4Layout.PixelsPerUnit);y++)
            for(int x=0;x<=Mathf.CeilToInt(layout.worldWidth/RushV4Layout.PixelsPerUnit);x++)
                if(g.Contains(new Vector2(x,y),0))m.cells.Add(new Vector2Int(x,y));
            for(int i=0;i<3;i++)
            {
                var t=layout.TaskWorld((stage==1?"M":"O")+(i+1));var entrance=Nearest(m,t.position);
                Vector2 start=t.position,target=t.position;
                if(t.pickups.Length>0){start=t.pickups[0].position;if(t.pickups[0].target.HasValue)target=t.pickups[0].target.Value.center;}
                if(t.deliveries.Length>0){start=t.position;target=t.deliveries[0].position;}
                if(t.exit.HasValue)target=t.exit.Value;
                if(t.id=="M3")
                    foreach(var item in layout.objects??new RushV4Layout.Item[0])if(item.id=="M3-cloth")start=layout.World(new Vector2(item.x+item.w/2,item.y+item.h));
                m.tasks.Add(new TaskSite{index=i,kind=t.kind,center=t.position,start=start,target=target,entrance=entrance,rejoin=entrance,routeDistance=m.NearestRouteDistance(entrance)});
            }
            if(stage==1)
            {
                var portal=layout.PortalWorld("M1");if(portal==null)throw new InvalidOperationException("Metro portal missing");
                m.trainDoor=portal.from;m.platform=portal.from+Vector2.down*3;
                // Use the nearest authored side-waiting zone, not the alighting lane.
                float best=float.PositiveInfinity;
                foreach(var zone in layout.zones??new RushV4Layout.Box[0])if(zone.kind=="waiting")
                {var p=layout.WorldRect(zone).center;float d=(p-portal.from).sqrMagnitude;if(d<best&&m.IsWalkable(p,RunV9.PlayerRadius)){best=d;m.platform=p;}}
                m.tasks[0].start=m.platform;
            }
            return m;
        }
        static Vector2 Nearest(MapData m,Vector2 p)
        {
            Vector2 best=m.spawn;float error=float.PositiveInfinity;
            for(int i=1;i<m.route.Count;i++)
            {
                if(m.routeBreaks.Contains(i))continue;
                var a=m.route[i-1];var d=m.route[i]-a;var q=a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.00001f,d.sqrMagnitude));
                if((p-q).sqrMagnitude<error){error=(p-q).sqrMagnitude;best=q;}
            }
            return best;
        }
        /// <summary>Structural checks only. Full portal-aware replay belongs to RunV9 tests.</summary>
        public static bool Validate(MapData m,out string reason)
        {
            reason="";
            if(m==null||(m.stage!=1&&m.stage!=2)||m.geometryWalkable==null){reason="not authored interior geometry";return false;}
            if(m.tasks.Count!=3||m.route.Count<20||m.modules.Count<20||m.PathLength<1500||m.PathLength>1900){reason="interior route/cardinality";return false;}
            if(!m.IsWalkable(m.spawn,RunV9.PlayerRadius)||!m.IsWalkable(m.exit,RunV9.PlayerRadius)){reason="spawn/exit collision";return false;}
            if(m.stage==1)
            {
                if(m.routeBreaks.Count!=1||!m.routeBreaks.Contains(11)){reason="metro disconnected route boundary";return false;}
                var portal=RushV4Layout.Load(1).PortalWorld("M1");
                if(!m.IsWalkable(portal.from,RunV9.PlayerRadius)||!m.IsWalkable(portal.to,RunV9.PlayerRadius)||!m.IsWalkable(m.platform,RunV9.PlayerRadius)){reason="portal/waiting clearance";return false;}
            }
            else if(m.routeBreaks.Count!=0){reason="office unexpected route break";return false;}
            return true;
        }
    }
}
