using System;
using System.Collections.Generic;
using UnityEngine;
using EarlyRush.V8;
namespace EarlyRush.V9
{
    // Authored street layout tied to the accepted Art_v003 composite, not arbitrary random tiles.
    // Coordinates below are measured in the 887 x 1774 master; exported tiles are exactly 3x.
    public static class RushStreetArtV14
    {
        public const float PixelsPerUnit=5f;
        public static bool Applies(int stage,int seed)=>stage==0&&seed==91026;
        public static Vector2 World(float x,float y)=>new Vector2((x-375)/PixelsPerUnit,(1745-y)/PixelsPerUnit);
        public static Vector2 Pixel(Vector2 p)=>new Vector2(p.x*PixelsPerUnit+375,1745-p.y*PixelsPerUnit);
        static readonly Vector2[] Road={new Vector2(375,1745),new Vector2(375,1290),new Vector2(726,1290),new Vector2(726,1080),new Vector2(464,1080),new Vector2(464,770),new Vector2(120,770),new Vector2(120,435),new Vector2(480,435),new Vector2(480,130)};
        static readonly string[] Kinds={"residential-court","shop-avenue","signal-junction","market-square","repair-block","cycle-garden","puzzle-court","station-square","station-approach"};
        public static MapData Create()
        {
            var m=new MapData{stage=0,seed=91026,corridorWidth=9};
            foreach(var p in Road)m.route.Add(World(p.x,p.y));
            m.spawn=m.route[0];m.exit=m.route[9];m.signal=World(726,1235);
            for(int i=0;i<9;i++)
            {
                Vector2 a=m.route[i],b=m.route[i+1];var rect=Bounds(a,b,4.2f);
                var module=new ModuleV9{index=i,kind=Kinds[i],floorKey="floors-v3/asphalt",entrance=a,exit=b,bounds=rect};m.modules.Add(module);
                Carve(m,a,b,4.2f);Zone(m,i,"through-lane",rect);
                // Material/traffic zones share the physical street, never invent non-walkable floor.
                Vector2 mid=(a+b)*.5f;Zone(m,i,"arrival",Bounds(a,mid,4.2f));Zone(m,i,"departure",Bounds(mid,b,4.2f));
            }
            // The three real courtyard bypasses have two connections to the main street.
            Bay(m,0,0,new[]{new Vector2(375,1518),new Vector2(525,1518),new Vector2(525,1495),new Vector2(610,1495),new Vector2(610,1380),new Vector2(415,1380),new Vector2(415,1405),new Vector2(375,1405)},new Rect(475,1470,120,55));
            Bay(m,1,4,new[]{new Vector2(464,1020),new Vector2(325,1020),new Vector2(325,980),new Vector2(230,980),new Vector2(230,845),new Vector2(464,845)},new Rect(245,920,145,66));
            Bay(m,2,6,new[]{new Vector2(120,710),new Vector2(260,710),new Vector2(260,595),new Vector2(260,510),new Vector2(120,510)},new Rect(190,555,150,65));
            m.tasks.Add(Site(m,0,"breakfast",540,1500,520,1510,570,1500,375,1518,375,1405));
            m.tasks.Add(Site(m,1,"repair",325,965,330,968,325,945,464,1020,464,845));
            m.tasks.Add(Site(m,2,"flip",265,595,265,628,265,560,120,710,120,510));
            // Static facade collision is at its ground footprint, not its full overhanging sprite.
            Block(m,"breakfast-facade",490,1410,105,55);Block(m,"repair-facade",270,865,110,55);
            m.platform=m.tasks[0].center;m.trainDoor=m.platform+Vector2.up*6;
            return m;
        }
        static TaskSite Site(MapData m,int i,string kind,float x,float y,float sx,float sy,float tx,float ty,float ex,float ey,float rx,float ry)
        {var t=new TaskSite{index=i,kind=kind,center=World(x,y),start=World(sx,sy),target=World(tx,ty),entrance=World(ex,ey),rejoin=World(rx,ry)};t.routeDistance=m.NearestRouteDistance(t.entrance);return t;}
        static Rect Bounds(Vector2 a,Vector2 b,float half)=>Rect.MinMaxRect(Mathf.Min(a.x,b.x)-half,Mathf.Min(a.y,b.y)-half,Mathf.Max(a.x,b.x)+half,Mathf.Max(a.y,b.y)+half);
        static void Zone(MapData m,int i,string kind,Rect r){m.zones.Add(new ZoneV9{moduleIndex=i,kind=kind,floorKey="floors-v3/asphalt",bounds=r});}
        static void Carve(MapData m,Vector2 a,Vector2 b,float half)
        {
            var r=Bounds(a,b,half);Vector2 d=b-a;
            for(int y=Mathf.CeilToInt(r.yMin);y<=Mathf.FloorToInt(r.yMax);y++)for(int x=Mathf.CeilToInt(r.xMin);x<=Mathf.FloorToInt(r.xMax);x++)
            {var p=new Vector2(x,y);float t=Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.001f,d.sqrMagnitude));if((p-a-d*t).sqrMagnitude<=half*half)m.cells.Add(new Vector2Int(x,y));}
        }
        static void Bay(MapData m,int task,int module,Vector2[] points,Rect roomPx)
        {
            for(int i=1;i<points.Length;i++){var a=World(points[i-1].x,points[i-1].y);var b=World(points[i].x,points[i].y);Carve(m,a,b,2.2f);Zone(m,module,i==1?"loop-lane":"task-access",Bounds(a,b,2.2f));}
            var lo=World(roomPx.xMin,roomPx.yMax);var hi=World(roomPx.xMax,roomPx.yMin);var r=Rect.MinMaxRect(lo.x,lo.y,hi.x,hi.y);
            for(int y=Mathf.CeilToInt(r.yMin);y<=Mathf.FloorToInt(r.yMax);y++)for(int x=Mathf.CeilToInt(r.xMin);x<=Mathf.FloorToInt(r.xMax);x++)m.cells.Add(new Vector2Int(x,y));
            Zone(m,module,"task-court-"+task,r);
        }
        static void Block(MapData m,string kind,float x,float y,float w,float h)
        {
            Vector2 lo=World(x,y+h),hi=World(x+w,y);var r=Rect.MinMaxRect(lo.x,lo.y,hi.x,hi.y);
            m.structures.Add(new StructureV9{kind=kind,artKey="street/storefront",bounds=r,position=new Vector2(r.center.x,r.yMin),width=r.width});
            m.cells.RemoveWhere(c=>r.Contains((Vector2)c));
        }
        public static void Build(Transform parent,List<UnityEngine.Object> owned,List<SpriteRenderer> decor,RunV9 run)
        {
            // Tile edge locations are integer export pixels: no overlaps, independent tile UVs or resampling.
            int[] xs={0,1230,2661},ys={0,1320,2430,3855,5322};
            for(int row=0;row<4;row++)for(int col=0;col<2;col++)
            {
                string key="tiles/tile-"+(row*2+col+1).ToString("00");
                var tex=Load(key);var sprite=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(0,1),15f,0,SpriteMeshType.FullRect);owned.Add(sprite);
                var go=new GameObject("Street V14 "+key);go.transform.SetParent(parent,false);go.transform.localPosition=World(xs[col]/3f,ys[row]/3f);
                var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=ArtV8.FloorOrder;
                go.transform.localScale=new Vector3((xs[col+1]-xs[col])/(float)tex.width,(ys[row+1]-ys[row])/(float)tex.height,1);
            }
            var lights=parent.gameObject.AddComponent<RushStreetLightsV14>();lights.run=run;
            lights.left=Place(parent,owned,null,"interactions/pedestrian-signal-green",652,1195,50,50,1242);
            lights.right=Place(parent,owned,null,"interactions/pedestrian-signal-green",750,1195,50,50,1242);
            Place(parent,owned,decor,"facades/breakfast-stall",476.9066f,1365.8901f,132.2198f,132.2198f,1465);
            Place(parent,owned,decor,"facades/repair-shop",256.4925f,827.2388f,137.5522f,137.5522f,920);
            Place(parent,owned,decor,"facades/metro-entry",398.2113f,7.1155f,158.5061f,158.5061f,130);
            Place(parent,owned,null,"interactions/flip-target",231.684f,509.168f,69.632f,69.632f,565);
            PlaceTargetFrame(parent,owned,run.map.tasks[0].center,1.3f);
            PlaceTargetFrame(parent,owned,run.map.tasks[1].target,2.2f);
        }
        // Frame PNG has large transparent margins: alpha128 bounds (48,74)-(464,182)
        // in a 512x256 canvas. Fit its visible outline, NOT its canvas, to the real trigger diameter.
        // These radii match the existing simulation; this is a cue, not a new square trigger.
        static void PlaceTargetFrame(Transform parent,List<UnityEngine.Object> owned,Vector2 target,float radius)
        {
            Vector2 pixel=Pixel(target);float diameter=radius*2*PixelsPerUnit;
            float width=diameter*512f/416f,height=diameter*256f/108f;
            Place(parent,owned,null,"interactions/ground-zone-frame",pixel.x-width*.5f,pixel.y-height*.5f,width,height,pixel.y,ArtV8.GroundMarkOrder);
        }
        static readonly Dictionary<string,Sprite> interactiveSprites=new Dictionary<string,Sprite>();
        public static Sprite InteractiveSprite(string key)
        {
            if(interactiveSprites.TryGetValue(key,out var found)&&found)return found;
            var t=Load("interactions/"+key);var result=Sprite.Create(t,new Rect(0,0,t.width,t.height),new Vector2(.5f,.5f),100);interactiveSprites[key]=result;return result;
        }
        public static void RefreshFixtures(RunV9 run,List<SpriteRenderer> fixtures,List<SpriteRenderer> bodies)
        {
            if(!Applies(run.stage,run.seed))return;
            for(int i=0;i<fixtures.Count;i++)if(run.fixtures[i].kind==FixtureKind.FlipBoard)
            {fixtures[i].sprite=InteractiveSprite(run.boards[run.fixtures[i].index]?"flip-face-sun":"flip-face-moon");fixtures[i].color=Color.white;ArtV8.SetWidth(fixtures[i],1.7f);}
            for(int i=0;i<bodies.Count;i++)if(run.bodies[i].kind==BodyKind.Wheel)
            {bodies[i].sprite=InteractiveSprite("wheel");ArtV8.SetWidth(bodies[i],run.bodies[i].radius*2.2f);}
        }
        static Texture2D Load(string key){var t=Resources.Load<Texture2D>("StreetV14/"+key);if(!t)throw new InvalidOperationException("Missing authored street asset: "+key);return t;}
        static SpriteRenderer Place(Transform parent,List<UnityEngine.Object> owned,List<SpriteRenderer> decor,string key,float x,float y,float w,float h,float groundY,int? layer=null)
        {
            var tex=Load(key);var sprite=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(0,1),PixelsPerUnit,0,SpriteMeshType.FullRect);owned.Add(sprite);
            var go=new GameObject("Street V14 "+key);go.transform.SetParent(parent,false);go.transform.localPosition=World(x,y);go.transform.localScale=new Vector3(w/tex.width,h/tex.height,1);
            var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=layer??ArtV8.SortOrder(World(x,groundY).y);decor?.Add(sr);return sr;
        }
    }
    public sealed class RushStreetLightsV14:MonoBehaviour
    {
        public RunV9 run;public SpriteRenderer left,right;bool previous;bool initialized;
        void LateUpdate()
        {
            if(run==null||initialized&&previous==run.SignalGreen)return;initialized=true;previous=run.SignalGreen;
            var sprite=RushStreetArtV14.InteractiveSprite(previous?"pedestrian-signal-green":"pedestrian-signal-red");
            // Retain the authored bounds when changing pivot/texture dimensions.
            foreach(var sr in new[]{left,right})if(sr){var bounds=sr.bounds;sr.sprite=sprite;sr.transform.position=bounds.center;sr.transform.localScale=new Vector3(bounds.size.x/sprite.bounds.size.x,bounds.size.y/sprite.bounds.size.y,1);}
        }
    }
}
