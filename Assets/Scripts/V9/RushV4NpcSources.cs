using System;
using System.Collections.Generic;
using UnityEngine;
namespace EarlyRush.V9
{
    /// <summary>Exact human-source ownership of ArtV4 blockers. Does not modify source data.</summary>
    public static class RushV4NpcSources
    {
        const float MatchTolerancePixels=.011f;
        public const float SpawnRadius=.48f,MaxSpawnShift=1.6f;
        public static bool IsNpcObject(string src)
        {
            return src=="sprites/npcs/commuter-female.png"||src=="sprites/npcs/commuter-male.png"
                ||src=="sprites/npcs/commuter-back.png"||src=="sprites/npcs/commuter-side.png";
        }
        public sealed class NpcSource
        {
            public string id,src;public int objectIndex,blockerIndex,corridorIndex,segmentIndex;
            public Vector2 sourceFoot,position,size;
            public float Shift=>Vector2.Distance(sourceFoot,position);
        }
        public sealed class Exclusions
        {
            readonly RushV4Layout layout;
            readonly HashSet<int> indices=new HashSet<int>();
            readonly Dictionary<string,NpcSource> byId=new Dictionary<string,NpcSource>();
            readonly List<NpcSource> sources=new List<NpcSource>();
            public IReadOnlyList<NpcSource> Sources=>sources;
            public int Count=>indices.Count;
            public IReadOnlyCollection<int> BlockerIndices=>indices;
            public int RetainedBlockerCount=>(layout.blockers?.Length??0)-Count;
            internal Exclusions(RushV4Layout layout){this.layout=layout;}
            public bool IsExcluded(int blockerIndex){return indices.Contains(blockerIndex);}
            public bool IsReplacedObject(string id){return id!=null&&byId.ContainsKey(id);}
            public NpcSource Find(string id){return byId.TryGetValue(id,out var source)?source:null;}
            internal void Add(NpcSource source)
            {
                if(!indices.Add(source.blockerIndex)||byId.ContainsKey(source.id))throw new InvalidOperationException("Duplicate NPC/blocker ownership: "+source.id);
                sources.Add(source);byId.Add(source.id,source);
            }
            // Required physical source geometry with only proven human footprints excluded.
            // Does not remove nearby furniture or manual wall/track rectangles.
            public bool IsWalkable(Vector2 world,float radius)
            {
                Vector2 p=layout.Pixel(world);float r=radius*RushV4Layout.PixelsPerUnit;bool inside=false;
                foreach(var b in layout.rooms??new RushV4Layout.Box[0])
                    if(p.x-r>=b.x&&p.x+r<=b.x+b.w&&p.y-r>=b.y&&p.y+r<=b.y+b.h){inside=true;break;}
                if(!inside)foreach(var c in layout.corridors)
                    for(int i=1;i<c.points.Length;i++)if(InCapsule(p,c.points[i-1],c.points[i],c.width/2-r)){inside=true;break;}
                if(!inside)return false;
                for(int i=0;i<layout.blockers.Length;i++)if(!indices.Contains(i))
                {
                    var b=layout.blockers[i];float dx=Mathf.Max(Mathf.Max(b.x-p.x,0),p.x-b.x-b.w),dy=Mathf.Max(Mathf.Max(b.y-p.y,0),p.y-b.y-b.h);
                    if(dx*dx+dy*dy<=r*r+.00016f)return false;
                }
                return true;
            }
            public bool IsInOriginalCorridor(NpcSource source,Vector2 world,float radius=SpawnRadius)
            {
                var c=layout.corridors[source.corridorIndex];
                return InCapsule(layout.Pixel(world),c.points[source.segmentIndex-1],c.points[source.segmentIndex],c.width/2-radius*RushV4Layout.PixelsPerUnit);
            }
            internal void PlaceAll()
            {
                // Nearest source segment fixes corridor identity before any relocation.
                foreach(var source in sources)
                {
                    Vector2 pixel=layout.Pixel(source.sourceFoot);float best=float.PositiveInfinity;
                    for(int c=0;c<layout.corridors.Length;c++)for(int i=1;i<layout.corridors[c].points.Length;i++)
                    {
                        var corridor=layout.corridors[c];float distance=DistanceToSegment(pixel,corridor.points[i-1],corridor.points[i]);
                        if(distance<=corridor.width/2&&distance<best){best=distance;source.corridorIndex=c;source.segmentIndex=i;}
                    }
                    if(float.IsPositiveInfinity(best))throw new InvalidOperationException("NPC source has no original corridor: "+source.id);
                    bool found=false;
                    foreach(var offset in offsets)
                    {
                        var candidate=layout.World(pixel+offset);
                        if(!IsInOriginalCorridor(source,candidate)||!IsWalkable(candidate,SpawnRadius))continue;
                        source.position=candidate;found=true;break;
                    }
                    if(!found)throw new InvalidOperationException("No local legal NPC spawn within "+MaxSpawnShift+" units: "+source.id);
                }
            }
        }
        static readonly Vector2[] offsets=BuildOffsets();
        static Vector2[] BuildOffsets()
        {
            var result=new List<Vector2>();const int max=64,step=8;
            for(int x=-max;x<=max;x+=step)for(int y=-max;y<=max;y+=step)
                if(x*x+y*y<=max*max)result.Add(new Vector2(x,y));
            result.Sort((a,b)=>{int d=a.sqrMagnitude.CompareTo(b.sqrMagnitude);if(d!=0)return d;d=a.x.CompareTo(b.x);return d!=0?d:a.y.CompareTo(b.y);});
            return result.ToArray();
        }
        static float DistanceToSegment(Vector2 p,Vector2 a,Vector2 b)
        {var d=b-a;return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.00001f,d.sqrMagnitude)));}
        static bool InCapsule(Vector2 p,Vector2 a,Vector2 b,float half)
        {return half>=0&&DistanceToSegment(p,a,b)<=half;}
        public static Rect ExpectedBlockerPixels(RushV4Layout.Item item)
        {return new Rect(item.x+item.w*.18f,item.y+item.h-30,item.w*.64f,30);}
        static bool Matches(Rect expected,RushV4Layout.Box actual)
        {return Mathf.Abs(expected.x-actual.x)<=MatchTolerancePixels&&Mathf.Abs(expected.y-actual.y)<=MatchTolerancePixels&&Mathf.Abs(expected.width-actual.w)<=MatchTolerancePixels&&Mathf.Abs(expected.height-actual.h)<=MatchTolerancePixels;}
        /// <summary>Build once per loaded layout, retain the result for geometry, spawn and rendering.</summary>
        public static Exclusions BuildExclusions(RushV4Layout layout)
        {
            if(layout==null||layout.objects==null||layout.blockers==null||layout.corridors==null)throw new ArgumentException("Incomplete NPC source layout");
            var result=new Exclusions(layout);
            for(int objectIndex=0;objectIndex<layout.objects.Length;objectIndex++)
            {
                var item=layout.objects[objectIndex];if(!IsNpcObject(item.src))continue;
                var expected=ExpectedBlockerPixels(item);int found=-1;
                for(int i=0;i<layout.blockers.Length;i++)if(Matches(expected,layout.blockers[i]))
                {if(found>=0)throw new InvalidOperationException("Ambiguous source NPC blocker: "+item.id);found=i;}
                if(found<0)throw new InvalidOperationException("Missing source NPC blocker: "+item.id);
                result.Add(new NpcSource{id=item.id,src=item.src,objectIndex=objectIndex,blockerIndex=found,
                    sourceFoot=layout.World(new Vector2(item.x+item.w/2,item.y+item.h)),size=new Vector2(item.w,item.h)/RushV4Layout.PixelsPerUnit});
            }
            result.PlaceAll();return result;
        }
    }
}
