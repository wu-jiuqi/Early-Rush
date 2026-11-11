using System;
using System.Collections.Generic;
using UnityEngine;
namespace EarlyRush.V9 {
// Optional art-v4 street model. Does not change the shipped 91026 map or simulation rules.
public static class RushV4StreetMap {
 public const int Seed=91027;public const float Ppu=40;
 [Serializable] public class Box {public float x,y,w,h;}
 [Serializable] public class Corridor {public Vector2[] points;public float width;public string kind;}
 [Serializable] public class Pickup {public float[] position;}
 [Serializable] public class Site {public string id,kind;public Vector2 position;public Box zone;public Pickup[] pickups;}
 [Serializable] public class Layout {public float worldWidth,worldHeight;public Vector2 start,end;public Corridor[] corridors;public Box[] rooms,blockers;public Site[] tasks;}
 public static Vector2 World(Vector2 px)=>new Vector2(px.x/Ppu,(18432-px.y)/Ppu);
 static Rect Convert(Box b)=>new Rect(b.x/Ppu,(18432-b.y-b.h)/Ppu,b.w/Ppu,b.h/Ppu);
 struct Segment {public Vector2 a,b;public float half;public Rect bounds;}
 sealed class Geometry {
  public readonly List<Segment> segments=new List<Segment>();public readonly List<Rect> rooms=new List<Rect>();public readonly Dictionary<Vector2Int,List<Rect>> blocks=new Dictionary<Vector2Int,List<Rect>>();
  const float Bucket=4;
  public void Block(Rect r){for(int y=Mathf.FloorToInt(r.yMin/Bucket);y<=Mathf.FloorToInt(r.yMax/Bucket);y++)for(int x=Mathf.FloorToInt(r.xMin/Bucket);x<=Mathf.FloorToInt(r.xMax/Bucket);x++){var k=new Vector2Int(x,y);if(!blocks.TryGetValue(k,out var list))blocks[k]=list=new List<Rect>();list.Add(r);}}
  public bool Contains(Vector2 p,float radius){bool inside=false;
   // Conservative exact containment in an individual room/capsule. Never expands
   // the visible union; concave union joins can be narrower by at most body radius.
   foreach(var r in rooms)if(p.x-radius>=r.xMin&&p.x+radius<=r.xMax&&p.y-radius>=r.yMin&&p.y+radius<=r.yMax){inside=true;break;}
   if(!inside)foreach(var s in segments){if(radius>s.half||!s.bounds.Contains(p))continue;Vector2 d=s.b-s.a;float t=Mathf.Clamp01(Vector2.Dot(p-s.a,d)/d.sqrMagnitude);if((p-s.a-d*t).sqrMagnitude<=(s.half-radius)*(s.half-radius)){inside=true;break;}}
   if(!inside)return false;
   // Exact circle versus every nearby footprint, including sub-grid thin obstacles.
   for(int y=Mathf.FloorToInt((p.y-radius)/Bucket);y<=Mathf.FloorToInt((p.y+radius)/Bucket);y++)for(int x=Mathf.FloorToInt((p.x-radius)/Bucket);x<=Mathf.FloorToInt((p.x+radius)/Bucket);x++)if(blocks.TryGetValue(new Vector2Int(x,y),out var list))foreach(var b in list){float dx=Mathf.Max(Mathf.Max(b.xMin-p.x,0),p.x-b.xMax),dy=Mathf.Max(Mathf.Max(b.yMin-p.y,0),p.y-b.yMax);if(dx*dx+dy*dy<=radius*radius+1e-7f)return false;}
   return true;
  }
 }
 static Rect Bounds(Vector2 a,Vector2 b,float h)=>Rect.MinMaxRect(Mathf.Min(a.x,b.x)-h,Mathf.Min(a.y,b.y)-h,Mathf.Max(a.x,b.x)+h,Mathf.Max(a.y,b.y)+h);
 public static MapData Create(){var asset=Resources.Load<TextAsset>("ArtV4/street");if(!asset)throw new Exception("Missing ArtV4/street JSON");var l=JsonUtility.FromJson<Layout>(asset.text);var g=new Geometry();var m=new MapData{stage=0,seed=Seed,corridorWidth=448/Ppu};m.geometryWalkable=g.Contains;m.authoredLayout=RushV4Layout.Load(0);m.authoredNpcSources=RushV4NpcSources.BuildExclusions(m.authoredLayout);
  // JsonUtility cannot deserialize jagged arrays; main corridor supplies the same ordered route.
  foreach(var c in l.corridors){for(int i=1;i<c.points.Length;i++){var a=World(c.points[i-1]);var b=World(c.points[i]);var s=new Segment{a=a,b=b,half=c.width/Ppu/2,bounds=Bounds(a,b,c.width/Ppu/2)};g.segments.Add(s);if(c.kind=="main"){int n=m.modules.Count;m.modules.Add(new ModuleV9{index=n,kind="v4-street-leg-"+n,floorKey="floors-v3/asphalt",entrance=a,exit=b,bounds=s.bounds});m.zones.Add(new ZoneV9{moduleIndex=n,kind="through-lane",floorKey="floors-v3/asphalt",bounds=s.bounds});}else m.zones.Add(new ZoneV9{moduleIndex=0,kind="loop-lane",floorKey="floors-v3/asphalt",bounds=s.bounds});}if(c.kind=="main")foreach(var p in c.points)m.route.Add(World(p));}
  foreach(var r in l.rooms){var rect=Convert(r);g.rooms.Add(rect);m.zones.Add(new ZoneV9{moduleIndex=0,kind="task-court",floorKey="floors-v3/asphalt",bounds=rect});}
  int blockerIndex=0;foreach(var b in l.blockers){if(m.authoredNpcSources.IsExcluded(blockerIndex++))continue;var rect=Convert(b);g.Block(rect);m.structures.Add(new StructureV9{kind="v4-footprint",artKey="",bounds=rect,position=rect.center,width=rect.width});}
  for(int y=0;y<=Mathf.CeilToInt(l.worldHeight/Ppu);y++)for(int x=0;x<=Mathf.CeilToInt(l.worldWidth/Ppu);x++)if(g.Contains(new Vector2(x,y),0))m.cells.Add(new Vector2Int(x,y));
  m.spawn=World(l.start);m.exit=World(l.end);
  for(int i=0;i<3;i++){var s=Array.Find(l.tasks,t=>t.id=="S"+(i+1));Vector2 center=World(s.position),start=center-Vector2.up*3.5f,target=center+Vector2.up*3.5f;if(i==0||i==2)target=Convert(s.zone).center;if(i==1){var p0=s.pickups[0].position;var p1=s.pickups[1].position;start=World(new Vector2((p0[0]+p1[0])/2,(p0[1]+p1[1])/2));target=Convert(s.zone).center;}var entry=Nearest(m,center);m.tasks.Add(new TaskSite{index=i,kind=i==1?"repair":s.kind,center=center,start=start,target=target,entrance=entry,rejoin=entry,routeDistance=m.NearestRouteDistance(entry)});}
  // Existing signal rule is north/south. Place it on a real long vertical leg.
  m.signal=(m.route[1]+m.route[2])*.5f-Vector2.up*2.5f;m.platform=m.tasks[0].center;m.trainDoor=m.platform+Vector2.up*6;
  return m;
 }
 static Vector2 Nearest(MapData m,Vector2 p){Vector2 best=m.spawn;float error=float.PositiveInfinity;for(int i=1;i<m.route.Count;i++){var a=m.route[i-1];var d=m.route[i]-a;var q=a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);if((p-q).sqrMagnitude<error){best=q;error=(p-q).sqrMagnitude;}}return best;}
 public static bool Validate(MapData m,out string reason){reason="";if(m.stage!=0||m.seed!=Seed||m.geometryWalkable==null){reason="not V4 street";return false;}if(m.route.Count<20||m.PathLength<1700||m.PathLength>1850||m.tasks.Count!=3||m.modules.Count<20){reason="v4 layout cardinality/budget";return false;}var found=new HashSet<Vector2Int>();var queue=new Queue<Vector2Int>();var root=MapData.Cell(m.spawn);found.Add(root);queue.Enqueue(root);var ds=new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};while(queue.Count>0){var p=queue.Dequeue();foreach(var d in ds){var n=p+d;if(!found.Contains(n)&&m.cells.Contains(n)&&m.IsWalkable(n,1.05f)){found.Add(n);queue.Enqueue(n);}}}
  var points=new List<Vector2>{m.spawn,m.exit};foreach(var t in m.tasks)points.AddRange(new[]{t.center,t.start,t.target,t.entrance,t.rejoin});for(int i=0;i<2;i++){var p=m.tasks[1].start+Vector2.right*(i*3-1.5f);points.Add(p);points.Add(p-(m.tasks[1].target-p).normalized*2);}for(int i=0;i<5;i++){points.Add(m.tasks[2].center+new Vector2((i-2)*2,-2.5f));points.Add(m.tasks[2].center+new Vector2((i-2)*2,2));}foreach(var p in points)if(!m.IsWalkable(p,1.05f)||!found.Contains(MapData.Cell(p))){reason="v4 operational clearance/connectivity at "+p;return false;}return true;}
}}
