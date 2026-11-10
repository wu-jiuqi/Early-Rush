using System;
using System.Collections.Generic;
using UnityEngine;
using EarlyRush.V9;

namespace EarlyRush.V10
{
    [Serializable] public sealed class RegionV10
    {
        public string id,kind,floorKey; public Rect bounds; public Vector2 center;
        public int chapterBeat; public bool optionalTask; public string encounter;
    }
    [Serializable] public sealed class PortalV10
    {
        public string id,kind; public Rect bounds; public Vector2 entry,exit;
        public bool requiredTrainPassage;
    }
    [Serializable] public sealed class PreviewV10
    {
        public string schema="early-rush-v10-layout-1";public bool timingVerified=false;
        public int stage,seed;public Vector2 spawn,exit;public Vector2Int[] cells;
        public Vector2[] route;public RegionV10[] regions;public PortalV10[] portals;
        public ZoneV9[] zones;public StructureV9[] structures;public TaskSite[] tasks;
    }
    // Explicit logical geometry, never inferred from opaque concept-image pixels.
    // V9 geometry is reused as a collision library; its generator/length validator are NOT used.
    public sealed class MapDataV10
    {
        public readonly MapData geometry=new MapData();
        public readonly List<RegionV10> regions=new List<RegionV10>();
        public readonly List<PortalV10> portals=new List<PortalV10>();
        public HashSet<Vector2Int> cells {get{return geometry.cells;}}
        public List<Vector2> route {get{return geometry.route;}}
        public List<ZoneV9> zones {get{return geometry.zones;}}
        public List<StructureV9> structures {get{return geometry.structures;}}
        public List<TaskSite> tasks {get{return geometry.tasks;}}
        public Vector2 spawn {get{return geometry.spawn;}} public Vector2 exit {get{return geometry.exit;}}
        public float PathLength {get{return geometry.PathLength;}}
        public bool IsWalkable(Vector2 p,float radius){return geometry.IsWalkable(p,radius);}
        public Vector2 ResolveMove(Vector2 p,Vector2 delta,float radius){return geometry.ResolveMove(p,delta,radius);}
        public string ToPreviewJson()
        {
            var ordered=new List<Vector2Int>(cells);ordered.Sort((a,b)=>a.y==b.y?a.x.CompareTo(b.x):a.y.CompareTo(b.y));
            return JsonUtility.ToJson(new PreviewV10{stage=geometry.stage,seed=geometry.seed,spawn=spawn,exit=exit,cells=ordered.ToArray(),route=route.ToArray(),regions=regions.ToArray(),portals=portals.ToArray(),zones=zones.ToArray(),structures=structures.ToArray(),tasks=tasks.ToArray()},true);
        }
        public bool Validate(out string reason)
        {
            reason=null;var seen=Reachable(null);
            if(!seen.Contains(MapData.Cell(exit))){reason="Exit disconnected";return false;}
            foreach(var task in tasks)foreach(var p in new[]{task.center,task.start,task.target})
                if(!IsWalkable(p,1.05f)||!seen.Contains(MapData.Cell(p))){reason="Task clearance "+task.index+" at "+p;return false;}
            foreach(var portal in portals)if(portal.requiredTrainPassage&&Reachable(portal.bounds).Contains(MapData.Cell(exit)))
                {reason="Train passage can be bypassed";return false;}
            if(regions.Count!=15||PathLength<350){reason="Incomplete chapter regions";return false;}
            return true;
        }
        HashSet<Vector2Int> Reachable(Rect? excluded)
        {
            var visited=new HashSet<Vector2Int>();var queue=new Queue<Vector2Int>();var begin=MapData.Cell(spawn);queue.Enqueue(begin);visited.Add(begin);
            var dirs=new[]{Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
            while(queue.Count>0){var p=queue.Dequeue();foreach(var dir in dirs){var q=p+dir;if(visited.Contains(q)||!IsWalkable(q,.47f)||(excluded.HasValue&&excluded.Value.Contains(q)))continue;visited.Add(q);queue.Enqueue(q);}}
            return visited;
        }
    }
    public static class RushMapV10
    {
        static readonly string[][] Names={
            new[]{"residential-entry","breakfast-court","market-crossing","shop-street","bus-stop","traffic-junction","repair-court","cycle-lane","garden-turn","flip-board-court","delivery-alley","neighbourhood-square","park-edge","station-plaza","metro-entry"},
            new[]{"station-entry","ticket-hall","gate-lanes","concourse","capsule-arcade","transfer-turn","platform-approach","waiting-platform","carriage-interior","arrival-platform","cleaning-bay","exit-concourse","retail-corner","escalator-hall","street-exit"},
            new[]{"reception","coffee-lounge","workstation-a","document-hub","workstation-b","leader-office-a","leader-office-b","leader-office-c","team-corridor","chair-workshop","project-room","quiet-corner","meeting-foyer","meeting-corridor","meeting-room"}};
        public static MapDataV10 Create(int stage,int seed)
        {
            if(stage<0||stage>2)throw new ArgumentOutOfRangeException("stage");
            var m=new MapDataV10();m.geometry.stage=stage;m.geometry.seed=seed;m.geometry.corridorWidth=9;
            string floor=stage==0?"asphalt":stage==1?"tile":"carpet";
            // Fifteen human-scale districts, not an enlarged single screenshot. The ordered
            // district graph is bent both ways; each district contains functioning side space.
            int[][] coordinates=stage==0 ? new[]{new[]{0,0},new[]{0,64},new[]{0,128},new[]{64,128},new[]{64,192},new[]{128,192},new[]{128,128},new[]{192,128},new[]{192,64},new[]{128,64},new[]{128,0},new[]{256,0},new[]{256,64},new[]{256,192},new[]{320,192}}
                : stage==1 ? new[]{new[]{0,0},new[]{64,0},new[]{64,64},new[]{0,64},new[]{0,128},new[]{64,128},new[]{128,128},new[]{128,192},new[]{192,192},new[]{256,192},new[]{256,256},new[]{192,256},new[]{192,320},new[]{256,320},new[]{320,320}}
                : new[]{new[]{0,0},new[]{64,0},new[]{64,64},new[]{128,64},new[]{128,128},new[]{64,128},new[]{0,128},new[]{0,192},new[]{64,192},new[]{128,192},new[]{192,192},new[]{192,128},new[]{256,128},new[]{256,192},new[]{320,192}};
            foreach(var coordinate in coordinates)
            {
                Vector2 c=new Vector2(coordinate[0]*.5f,coordinate[1]*.5f);
                m.route.Add(c);
                var region=new RegionV10{id="r"+m.regions.Count,kind=Names[stage][m.regions.Count],floorKey=floor,center=c,bounds=new Rect(c.x-13,c.y-11,26,22),chapterBeat=m.regions.Count,encounter="cross-flow-and-furniture"};
                m.regions.Add(region);Paint(m,region.bounds,floor,region.chapterBeat,"district");
            }
            m.geometry.spawn=m.route[0];m.geometry.exit=m.route[14];
            for(int i=1;i<m.route.Count;i++)
            {
                Vector2 a=m.route[i-1],b=m.route[i],d=(b-a).normalized,n=new Vector2(-d.y,d.x);
                var r=Rect.MinMaxRect(Mathf.Min(a.x,b.x)-4,Mathf.Min(a.y,b.y)-4,Mathf.Max(a.x,b.x)+4,Mathf.Max(a.y,b.y)+4);
                Paint(m,r,floor,i-1,"link");
                // Alternating real lay-bys break the connector into short navigational beats.
                for(int k=1;k<=2;k++)
                {
                    Vector2 c=Vector2.Lerp(a,b,k/3f),side=n*(k%2==0?-1:1);
                    Paint(m,new Rect(c.x-7,c.y-7,14,14),floor,i-1,"passing-bay");
                    Place(m,c+side*6,stage==0?"street/planter":stage==1?"metro/bench":"office/filing-cabinet",new Vector2(2,2),2.5f,"lane-edge");
                }
                m.geometry.modules.Add(new ModuleV9{index=i-1,kind=Names[stage][i-1],floorKey=floor,entrance=a,exit=b,bounds=r});
            }
            int[] taskRegions=stage==0?new[]{1,6,9}:stage==1?new[]{7,4,10}:new[]{1,3,9};
            for(int t=0;t<3;t++)
            {
                var region=m.regions[taskRegions[t]];region.optionalTask=true;Vector2 c=region.center;
                m.tasks.Add(new TaskSite{index=t,kind=region.kind,center=c,start=c+new Vector2(-2,-7),target=c+new Vector2(3,7),entrance=c-Vector2.up*9,rejoin=c+Vector2.up*9,routeDistance=DistanceTo(m.route,taskRegions[t])});
                // Functional framing with reserved central 12-unit pushing/interaction strip.
                Place(m,c+new Vector2(-10,5),stage==0?"street/storefront":stage==1?"metro/vending":"office/desk-chair",new Vector2(3,4),4,"service-wing");
                Place(m,c+new Vector2(10,-4),stage==0?"street/planter":stage==1?"metro/bench":"office/filing-cabinet",new Vector2(3,3),3,"service-wing");
            }
            // District furniture retains scale and genuine footprints, never invisible obstacles.
            for(int i=0;i<15;i++)foreach(int side in new[]{-1,1})
                Place(m,m.route[i]+new Vector2(side*10,side*8),stage==0?"street/planter":stage==1?"metro/pillar":"office/desk-chair",new Vector2(2,2),2.8f,"district-corner");
            foreach(var region in m.regions)
            {
                string[] group=Furniture(region.kind,stage);
                // Keep the central X +/- 5 traffic/transport strip clear; furnishings form
                // recognisable functional wings rather than random objects along map edges.
                Vector2[] slots={new Vector2(-8,-3),new Vector2(8,3),new Vector2(-8,3),new Vector2(8,-3)};
                for(int i=0;i<group.Length;i++)Place(m,region.center+slots[i],group[i],new Vector2(2.5f,2.5f),3.5f,region.kind+"-furniture");
            }
            m.geometry.signal=(m.route[4]+m.route[5])*.5f;m.geometry.platform=m.tasks[0].center;m.geometry.trainDoor=m.geometry.platform+Vector2.right*8;
            if(stage==1)
            {
                // The sole connection between waiting and arrival graphs passes through the
                // carriage district. Closing this portal must disconnect the chapter exit.
                Vector2 a=m.route[7],b=m.route[8],mid=(a+b)*.5f;
                var portal=new PortalV10{id="train-required",kind="train-carriage-bridge",entry=a,exit=b,requiredTrainPassage=true,bounds=new Rect(mid.x-2,mid.y-12,4,24)};
                m.portals.Add(portal);m.regions[8].encounter="traverse-carriage-then-alight";
                Paint(m,new Rect(mid.x-22,mid.y-4,44,8),"carriage",7,"carriage-floor");
                // No bypass along the external platform lip: the only cross-cut opening is
                // inside the carriage. Render these same boundaries, not a picture-only wall.
                for(int y=Mathf.CeilToInt(mid.y-12);y<mid.y+12;y++)
                    for(int x=Mathf.CeilToInt(mid.x-2);x<mid.x+2;x++)
                        if(y<mid.y-4||y>=mid.y+4)m.cells.Remove(new Vector2Int(x,y));
            }
            // Later floor zones may overlap earlier furniture; collision occupancy wins last.
            foreach(var structure in m.structures)
                for(int y=Mathf.CeilToInt(structure.bounds.yMin);y<structure.bounds.yMax;y++)
                    for(int x=Mathf.CeilToInt(structure.bounds.xMin);x<structure.bounds.xMax;x++)m.cells.Remove(new Vector2Int(x,y));
            return m;
        }
        static string[] Furniture(string kind,int stage)
        {
            if(kind=="breakfast-court")return new[]{"stalls/breakfast-stall","street/bench","street/bush-planter"};
            if(kind=="repair-court"||kind=="cycle-lane")return new[]{"street/bicycle-rack","street/storefront","street/bicycle-rack"};
            if(kind=="flip-board-court")return new[]{"street/bench","street/bush-planter"};
            if(kind=="capsule-arcade")return new[]{"metro/vending","metro/platform-bench","metro/pillar"};
            if(kind=="waiting-platform"||kind=="arrival-platform")return new[]{"metro/platform-bench","metro/platform-bench","metro/pillar"};
            if(kind=="carriage-interior")return new[]{"metro/platform-bench","metro/platform-bench"};
            if(kind=="coffee-lounge")return new[]{"office/coffee-counter","office/sofa","office/plant"};
            if(kind=="reception")return new[]{"office/reception","office/sofa","office/plant"};
            if(kind=="document-hub")return new[]{"office/printer","office/filing-cabinet","office/parcel-cart"};
            if(kind.StartsWith("leader-office"))return new[]{"office/desk-chair","office/filing-cabinet","office/partition"};
            if(kind=="meeting-room")return new[]{"office/meeting-table","office/meeting-table","office/plant"};
            if(kind=="chair-workshop")return new[]{"office/desk-chair","office/desk-chair"};
            return stage==0?new[]{"street/storefront","street/bench"}:stage==1?new[]{"metro/vending","metro/platform-bench"}:new[]{"office/desk-chair","office/partition","office/plant"};
        }
        static float DistanceTo(List<Vector2> route,int index){float n=0;for(int i=1;i<=index;i++)n+=Vector2.Distance(route[i-1],route[i]);return n;}
        static void Paint(MapDataV10 m,Rect r,string floor,int index,string kind)
        {
            m.zones.Add(new ZoneV9{bounds=r,floorKey=floor,moduleIndex=index,kind=kind});
            for(int y=Mathf.CeilToInt(r.yMin);y<r.yMax;y++)for(int x=Mathf.CeilToInt(r.xMin);x<r.xMax;x++)m.cells.Add(new Vector2Int(x,y));
        }
        static void Place(MapDataV10 m,Vector2 center,string art,Vector2 size,float width,string kind)
        {
            // Keep true occupied cells aligned with the supplied collision footprint.
            var r=new Rect(center-size*.5f,size);m.structures.Add(new StructureV9{kind=kind,artKey=art,bounds=r,position=new Vector2(center.x,r.yMin),width=width});
            for(int y=Mathf.CeilToInt(r.yMin);y<r.yMax;y++)for(int x=Mathf.CeilToInt(r.xMin);x<r.xMax;x++)m.cells.Remove(new Vector2Int(x,y));
        }
    }
}
