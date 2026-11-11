using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush.V9
{
    /// <summary>Read-only ArtV4 source contract. No simulation or renderer mutation.</summary>
    [Serializable]
    public sealed class RushV4Layout
    {
        public const float PixelsPerUnit = 40f;
        [Serializable] public class Box { public float x,y,w,h; public string kind; }
        [Serializable] public sealed class Item : Box { public string id,src,role; public int z; }
        [Serializable] public sealed class Patch : Box { public string id; }
        [Serializable] public sealed class Corridor { public Vector2[] points; public float width; public string kind; }
        // Nested source coordinates are [x,y], unlike top-level task.position {x,y}.
        [Serializable] public sealed class Pickup { public string id,sprite; public float[] position; public Box target; }
        [Serializable] public sealed class Delivery { public string id,portrait; public float[] position; }
        [Serializable] public sealed class Actor { public string id,sprite; public float[] position,a,b; public float w,h,speed; }
        [Serializable] public sealed class Task
        {
            public string id,kind,title,icon; public Vector2 position; public float radius,routeDistance;
            public Box zone; public Pickup[] pickups; public Delivery[] deliveries; public Actor[] actors;
            public Patch[] patches; public Box[] targetZones; public float[] exit;
        }
        [Serializable] public sealed class Portal { public string id,taskId; public float[] from,to; public float radius; }
        [Serializable] public sealed class Speeds { public float walk,bike,push; }
        public string id,title,groundSrc,outsideColor,overviewSrc;
        public float worldWidth,worldHeight,tileSize,routeWidth,endRadius;
        public Vector2 start,end;
        public Corridor[] corridors; public Box[] rooms,blockers,zones; public Item[] objects;
        public Task[] tasks; public Portal[] portals; public Speeds speeds;

        public sealed class PickupPlacement { public string id,sprite; public Vector2 position; public Rect? target; }
        public sealed class DeliveryPlacement { public string id,portrait; public Vector2 position; }
        public sealed class ActorPlacement { public string id,sprite; public Vector2 position,a,b,size; public float speed; }
        public sealed class PatchPlacement { public string id; public Rect bounds; }
        public sealed class PortalPlacement { public string id,taskId; public Vector2 from,to; public float radius; }
        public sealed class TaskPlacement
        {
            public string id,kind,title,icon; public Vector2 position; public float radius,routeDistance;
            public Rect? zone; public Vector2? exit;
            public PickupPlacement[] pickups; public DeliveryPlacement[] deliveries; public ActorPlacement[] actors;
            public PatchPlacement[] patches; public Rect[] targetZones;
        }

        public static RushV4Layout Load(int stage)
        {
            if(stage<0||stage>2) throw new ArgumentOutOfRangeException(nameof(stage));
            return Load(new[]{"street","metro","office"}[stage]);
        }
        public static RushV4Layout Load(string name)
        {
            if(name!="street"&&name!="metro"&&name!="office") throw new ArgumentException("Unknown ArtV4 stage",nameof(name));
            var asset=Resources.Load<TextAsset>("ArtV4/"+name);
            if(!asset) throw new InvalidOperationException("Missing ArtV4/"+name);
            var result=JsonUtility.FromJson<RushV4Layout>(asset.text);
            if(result==null||result.id!=name||result.worldHeight<=0||result.worldWidth<=0||result.tasks==null||result.corridors==null)
                throw new InvalidOperationException("Invalid ArtV4 layout: "+name);
            var ids=new HashSet<string>();
            foreach(var t in result.tasks) if(t==null||string.IsNullOrEmpty(t.id)||!ids.Add(t.id))
                throw new InvalidOperationException("Missing/duplicate ArtV4 task ID: "+name);
            return result;
        }
        public Task TaskById(string taskId)
        {
            foreach(var t in tasks) if(t.id==taskId) return t;
            throw new InvalidOperationException("Missing task "+taskId+" in "+id);
        }
        public Portal PortalByTaskId(string taskId)
        {
            Portal found=null;
            foreach(var p in portals??new Portal[0]) if(p.taskId==taskId)
            { if(found!=null) throw new InvalidOperationException("Multiple portals for "+taskId); found=p; }
            return found;
        }
        public static Vector2 Pair(float[] pair)
        {
            if(pair==null||pair.Length!=2||float.IsNaN(pair[0])||float.IsNaN(pair[1])||float.IsInfinity(pair[0])||float.IsInfinity(pair[1]))
                throw new InvalidOperationException("ArtV4 coordinate must be finite [x,y]");
            return new Vector2(pair[0],pair[1]);
        }
        public Vector2 World(Vector2 pixels) { return new Vector2(pixels.x/PixelsPerUnit,(worldHeight-pixels.y)/PixelsPerUnit); }
        public Vector2 World(float[] pair) { return World(Pair(pair)); }
        public Vector2 Pixel(Vector2 world) { return new Vector2(world.x*PixelsPerUnit,worldHeight-world.y*PixelsPerUnit); }
        public Rect WorldRect(Box b)
        {
            if(b==null||b.w<=0||b.h<=0) throw new InvalidOperationException("Invalid ArtV4 rectangle");
            return new Rect(b.x/PixelsPerUnit,(worldHeight-b.y-b.h)/PixelsPerUnit,b.w/PixelsPerUnit,b.h/PixelsPerUnit);
        }
        /// <summary>Ordered separate main corridors; NEVER join across the metro portal.
        /// Source routeSegments duplicates these points. JsonUtility cannot read jagged arrays,
        /// so use the equivalent typed corridor records, not the flattened source route.</summary>
        public Vector2[][] MainRouteSections()
        {
            var sections=new List<Vector2[]>();
            foreach(var c in corridors) if(c.kind=="main")
            {
                if(c.points==null||c.points.Length<2) throw new InvalidOperationException("Invalid main corridor");
                var section=new Vector2[c.points.Length];
                for(int i=0;i<section.Length;i++) section[i]=World(c.points[i]);
                sections.Add(section);
            }
            return sections.ToArray();
        }
        public PortalPlacement PortalWorld(string taskId)
        {
            var p=PortalByTaskId(taskId); if(p==null)return null;
            return new PortalPlacement{id=p.id,taskId=p.taskId,from=World(p.from),to=World(p.to),radius=p.radius/PixelsPerUnit};
        }
        // JsonUtility may materialize an omitted inline serializable class as an all-zero
        // object (and an omitted array as []). Those optional sentinels mean absent, not
        // an invalid required world rectangle. Nonempty malformed values still throw.
        public Rect? OptionalWorldRect(Box b)
        {
            if(b==null||(b.x==0&&b.y==0&&b.w==0&&b.h==0&&string.IsNullOrEmpty(b.kind)))return null;
            return WorldRect(b);
        }
        public Vector2? OptionalWorldPoint(float[] pair)
        {
            if(pair==null||pair.Length==0)return null;
            return World(pair);
        }
        public TaskPlacement TaskWorld(string taskId)
        {
            var t=TaskById(taskId);
            var result=new TaskPlacement{id=t.id,kind=t.kind,title=t.title,icon=t.icon,position=World(t.position),radius=t.radius/PixelsPerUnit,routeDistance=t.routeDistance/PixelsPerUnit,
                zone=OptionalWorldRect(t.zone),exit=OptionalWorldPoint(t.exit)};
            var pickups=new List<PickupPlacement>();
            foreach(var p in t.pickups??new Pickup[0]) pickups.Add(new PickupPlacement{id=p.id,sprite=p.sprite,position=World(p.position),target=OptionalWorldRect(p.target)});
            var deliveries=new List<DeliveryPlacement>();
            foreach(var d in t.deliveries??new Delivery[0]) deliveries.Add(new DeliveryPlacement{id=d.id,portrait=d.portrait,position=World(d.position)});
            var actors=new List<ActorPlacement>();
            foreach(var a in t.actors??new Actor[0]) actors.Add(new ActorPlacement{id=a.id,sprite=a.sprite,position=World(a.position),a=World(a.a),b=World(a.b),size=new Vector2(a.w,a.h)/PixelsPerUnit,speed=a.speed/PixelsPerUnit});
            var patches=new List<PatchPlacement>();
            foreach(var p in t.patches??new Patch[0]) patches.Add(new PatchPlacement{id=p.id,bounds=WorldRect(p)});
            var targets=new List<Rect>(); foreach(var b in t.targetZones??new Box[0]) targets.Add(WorldRect(b));
            result.pickups=pickups.ToArray();result.deliveries=deliveries.ToArray();result.actors=actors.ToArray();result.patches=patches.ToArray();result.targetZones=targets.ToArray();
            return result;
        }
    }
}
