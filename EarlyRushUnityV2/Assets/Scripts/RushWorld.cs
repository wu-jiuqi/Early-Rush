using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush
{
    public class WorldData
    {
        public Transform root;
        public Vector3 spawn, goal;
        public Vector2 goalHalfSize;
        public string title, routeHint;
        public Vector2 crowdExit;
        public Rect crowdExitCover;
        public List<Vector2> riskRoute = new List<Vector2>(), safeRoute = new List<Vector2>();
        public List<CrowdSpawn> crowd = new List<CrowdSpawn>();
        public List<Rect> obstacles = new List<Rect>();
        public Vector2 boundsMin = new Vector2(-20, 0), boundsMax = new Vector2(20, 100);
    }
    public struct CrowdSpawn
    {
        public Vector2 position, axis;
        public float range, speed, phase;
        public Vector2[] route;
        public int routeIndex;
        public float desiredSpeed, delay;
        public Color color;
        public bool cyclist;
        public CrowdSpawn(float x, float z, float ax, float az, float range, float speed, Color color, bool cyclist = false, float phase = -1)
        {
            position = new Vector2(x,z); axis = new Vector2(ax,az);
            this.range=range; this.speed=speed; this.color=color; this.cyclist=cyclist; this.phase=phase;
            route=null; routeIndex=0; desiredSpeed=0; delay=0;
        }
        public CrowdSpawn(Vector2 position,Vector2[] route,int routeIndex,float desiredSpeed,float delay,Color color,bool cyclist)
        {
            this.position=position; this.route=route; this.routeIndex=routeIndex;
            this.desiredSpeed=desiredSpeed; this.delay=delay; this.color=color; this.cyclist=cyclist;
            axis=Vector2.zero; range=0; speed=0; phase=-1;
        }
    }
    public static class RushWorld
    {
        static readonly Color Teal=RushVisuals.Teal, Ink=RushVisuals.Ink, Cream=RushVisuals.Cream;
        static readonly Color Coral=new Color(.96f,.42f,.29f), Blue=new Color(.32f,.59f,.77f), Green=new Color(.40f,.64f,.39f), Gold=new Color(.98f,.73f,.28f);
        static readonly Color[] coats={new Color(.39f,.57f,.79f),new Color(.74f,.52f,.73f),new Color(.42f,.69f,.62f),new Color(.95f,.68f,.32f),new Color(.70f,.41f,.40f)};
        static Transform P(WorldData w,string n,Vector3 p,Vector3 s,Color c,float r=.12f)=>RushVisuals.Box(w.root,n,p,s,c,r);
        static Transform Sphere(WorldData w,string n,Vector3 p,Vector3 s,Color c)=>RushVisuals.Primitive(w.root,n,PrimitiveType.Sphere,p,s,c);
        static void Obstacle(WorldData w,float x,float z,float width,float depth){w.obstacles.Add(new Rect(x-width/2,z-depth/2,width,depth));}
        static void Person(WorldData w,float x,float z,float ax,float az,float range,float speed,int color,bool bike=false)
        {w.crowd.Add(new CrowdSpawn(x,z,ax,az,range,speed,coats[color%coats.Length],bike));}
        static void Solid(WorldData w,string name,float x,float z,float sx,float sz,float height,Color color)
        { P(w,name,new Vector3(x,height*.5f,z),new Vector3(sx,height,sz),color,.13f);Obstacle(w,x,z,sx,sz); }
        // A low, visible divider has a short crowded central gap and a longer side bypass.
        // All solid intervals are explicit; the clearance matches rendered geometry.
        static void Divider(WorldData w,float z,int side,Color color)
        {
            float sideX=side*13;
            float[] cuts=side>0 ? new[]{-20f,-2.2f,2.2f,11.2f,14.8f,20f} : new[]{-20f,-14.8f,-11.2f,-2.2f,2.2f,20f};
            for(int i=0;i<cuts.Length;i+=2)
                Solid(w,"Low route divider",(cuts[i]+cuts[i+1])*.5f,z,cuts[i+1]-cuts[i],.6f,.8f,color);
            Arrow(w,new Vector3(0,.13f,z-3),1f,Gold);
            Arrow(w,new Vector3(sideX,.13f,z-3),1f,Teal);
            Label(w,"QUICK",new Vector3(0,.16f,z-4.5f),.7f,Gold,true);
            Label(w,"BYPASS",new Vector3(sideX,.16f,z-4.5f),.7f,Teal,true);
        }
        static void Junction(WorldData w,float z,float depth)
        {
            // Paint marks a shared junction; all commuters now travel toward the destination.
            P(w,"Busy junction warning band",new Vector3(0,.09f,z),new Vector3(20.8f,.025f,depth),Gold,.01f);
            for(int i=-4;i<=4;i++)P(w,"Crosswalk paint",new Vector3(i*2.2f,.115f,z),new Vector3(.9f,.02f,depth-.16f),Cream,.01f);
        }
        static void Route(WorldData w,bool safe,params Vector2[] points)
        {
            (safe?w.safeRoute:w.riskRoute).AddRange(points);
            for(int i=1;i<points.Length;i++)
            {
                Vector2 delta=points[i]-points[i-1]; float length=delta.magnitude;
                for(float distance=2;distance<length;distance+=4)
                {
                    Vector2 point=points[i-1]+delta*(distance/length);
                    var dash=P(w,safe?"Bypass trail":"Quick route trail",new Vector3(point.x,.17f,point.y),new Vector3(.12f,.02f,1.1f),safe?Teal:Gold,.01f);
                    dash.localRotation=Quaternion.Euler(0,Mathf.Atan2(delta.x,delta.y)*Mathf.Rad2Deg,0);
                }
            }
        }
        static float RouteLength(Vector2[] route)
        {
            float length=0;
            for(int i=1;i<route.Length;i++)length+=Vector2.Distance(route[i-1],route[i]);
            return length;
        }
        static Vector2[] CommuterRoute(List<Vector2> route,float lane,WorldData w,int stage)
        {
            var points=new List<Vector2>();
            foreach(Vector2 point in route)points.Add(point+Vector2.right*lane);
            if(stage==2)points.Add(new Vector2(2,95.5f));
            points.Add(w.crowdExit);
            return points.ToArray();
        }
        static void SpawnAlong(WorldData w,Vector2[] route,float distance,float speed,float delay,int color,bool cyclist)
        {
            int next=1;
            while(next<route.Length-1)
            {
                float segment=Vector2.Distance(route[next-1],route[next]);
                if(distance<segment)break;
                distance-=segment;
                next++;
            }
            Vector2 from=route[next-1],to=route[next];
            float length=Vector2.Distance(from,to);
            Vector2 position=Vector2.Lerp(from,to,length>.001f?Mathf.Clamp01(distance/length):0);
            w.crowd.Add(new CrowdSpawn(position,route,next,speed,delay,coats[color%coats.Length],cyclist));
        }
        static void ExitCanopy(WorldData w,int stage)
        {
            float x=stage==2?2:0;
            w.crowdExit=new Vector2(x,98.6f);
            w.crowdExitCover=new Rect(x-3,95.5f,6,4);
            // Any player entering this five-metre gap crosses the wide goal pad first.
            // NPCs can continue beneath the canopy after the player has finished.
            Solid(w,"Arrival safety rail left",-11.25f,95.5f,17.5f,.4f,.8f,Teal);
            Solid(w,"Arrival safety rail right",11.25f,95.5f,17.5f,.4f,.8f,Teal);
            // The roof bottom is 3.05m. Its front projects over an arrival's feet at z98.6,
            // while the player's goal pad at z94 remains unobstructed.
            P(w,"Commuter exit canopy",new Vector3(x,3.225f,97.5f),new Vector3(6,.35f,4),Ink,.12f);
            for(int side=-1;side<=1;side+=2)
                Solid(w,"Exit canopy support",x+side*2.9f,96.7f,.3f,.35f,3.05f,Teal);
            if(stage!=1)Solid(w,"Destination rear screen",x,99.55f,6,.3f,3,Teal);
            Label(w,stage==0?"METRO ENTRY":stage==1?"ON BOARD":"CLOCKED IN",new Vector3(x,3.32f,96.46f),.3f,Cream,false);
        }
        static void PopulateCommuters(WorldData w,int stage)
        {
            // Commuters share the player's destination, with three main lanes and two
            // quieter bypass lanes. Initial positions sample route distance, never random walls.
            for(int i=0;i<84;i++)
            {
                float lane=(i%3-1)*.9f;
                Vector2[] route=CommuterRoute(w.riskRoute,lane,w,stage);
                float distance=(i/3+.35f)/28f*(RouteLength(route)-5);
                bool cyclist=stage==0&&i%3!=1;
                float speed=stage==0?(cyclist?4.4f+(i%7)*.19f:3.1f+(i%5)*.15f):2.6f+(i%7)*.2f;
                SpawnAlong(w,route,distance,speed,(i%5)*.08f,i,cyclist);
            }
            for(int i=0;i<24;i++)
            {
                Vector2[] route=CommuterRoute(w.safeRoute,i%2==0?-.35f:.35f,w,stage);
                float distance=(i/2+.4f)/12f*(RouteLength(route)-6);
                bool cyclist=stage==0&&i%2==0;
                SpawnAlong(w,route,distance,stage==0?(cyclist?4.5f:3.4f):2.9f+(i%4)*.18f,(i%4)*.12f,i+2,cyclist);
            }
            int merging=stage==0?12:stage==1?20:16;
            for(int i=0;i<merging;i++)
            {
                int side=i%2==0?-1:1;
                var route=new List<Vector2>();
                if(stage==0)
                {
                    if(side<0)
                    {
                        route.AddRange(new[]{new Vector2(-13,34),new Vector2(-8,42),new Vector2(0,44)});
                        for(int n=4;n<w.riskRoute.Count;n++)route.Add(w.riskRoute[n]);
                    }
                    else
                    {
                        route.AddRange(new[]{new Vector2(13,60),new Vector2(8,70),new Vector2(0,71)});
                        for(int n=9;n<w.riskRoute.Count;n++)route.Add(w.riskRoute[n]);
                    }
                }
                else if(stage==1)
                {
                    route.AddRange(new[]{new Vector2(side*13,80),new Vector2(side*5,83),new Vector2(side*2,86),new Vector2(side*.7f,88),new Vector2(side*.7f,94)});
                }
                else
                {
                    route.AddRange(new[]{new Vector2(side*13,7),new Vector2(side*3,10),new Vector2(side,18),new Vector2(side,24)});
                    for(int n=1;n<w.riskRoute.Count;n++)route.Add(w.riskRoute[n]);
                }
                if(stage==2)route.Add(new Vector2(2,95.5f));
                route.Add(w.crowdExit);
                float distance=(i/2+.4f)/(merging/2f)*18f;
                bool cyclist=stage==0;
                SpawnAlong(w,route.ToArray(),distance,stage==0?4.8f:2.8f+(i%5)*.18f,(i%6)*.1f,i+1,cyclist);
            }
        }

        public static WorldData Build(int stage)
        {
            var w=new WorldData{root=new GameObject("Early Rush v2 district "+(stage+1)).transform,spawn=new Vector3(0,0,4),goal=new Vector3(0,0,94),goalHalfSize=new Vector2(2.6f,1.1f)};
            P(w,"District base",new Vector3(0,-.5f,50),new Vector3(41,.9f,101),Ink,.3f);
            P(w,"District ground",new Vector3(0,-.06f,50),new Vector3(40,.2f,100),stage==0?new Color(.80f,.84f,.70f):Cream,.03f);
            for(int side=-1;side<=1;side+=2)Solid(w,"Map edge curb",side*19.9f,50,.2f,100,.4f,Teal);
            if(stage==0)Street(w);else if(stage==1)Station(w);else Office(w);
            Goal(w,stage);
            ExitCanopy(w,stage);
            PopulateCommuters(w,stage);
            return w;
        }
        static void Street(WorldData w)
        {
            w.title="骑车赶地铁 · 街区";
            w.routeHint="大家都在赶地铁！黄线主路人多，青线绕行更稳。\n看准并道空隙；按 SHIFT 稳住后再冲刺。";
            P(w,"Long morning boulevard",new Vector3(0,.025f,50),new Vector3(21,.045f,99),new Color(.37f,.48f,.51f),.02f);
            for(int s=-1;s<=1;s+=2)
            {
                P(w,"Continuous cycle strip",new Vector3(s*8.7f,.055f,50),new Vector3(2.2f,.025f,99),new Color(.31f,.62f,.61f),.01f);
                P(w,"Wide bypass sidewalk",new Vector3(s*13,.035f,50),new Vector3(5.2f,.05f,99),new Color(.88f,.85f,.72f),.01f);
                for(int z=8;z<90;z+=16)
                {
                    Planter(w,s*17.3f,z,2.3f,2.3f);Tree(w,s*17.3f,z);
                    StreetLamp(w,s*10.6f,z+5);
                }
                for(int z=4;z<91;z+=6)P(w,"Cycle lane stripe",new Vector3(s*7.4f,.09f,z),new Vector3(.12f,.02f,2.8f),Cream,.01f);
                for(int z=12;z<88;z+=24)Arrow(w,new Vector3(s*13,.12f,z),1.2f,Teal);
            }
            for(int z=3;z<90;z+=6)P(w,"Boulevard center paint",new Vector3(0,.08f,z),new Vector3(.16f,.025f,2),Gold,.01f);
            Divider(w,24,1,Gold); Divider(w,52,-1,Gold); Divider(w,78,1,Gold);
            Solid(w,"First roadworks bed",0,35,5.5f,9, .3f,new Color(.50f,.39f,.29f));
            Barrier(w,0,30.5f,5.5f);Barrier(w,0,39.5f,5.5f);Cone(w,3.2f,32);Cone(w,-3.2f,38);
            Solid(w,"Delivery crates",.7f,64,6,9,1.1f,Gold);
            P(w,"Parcel straps",new Vector3(.7f,1.12f,64),new Vector3(.18f,.04f,9),Coral,.01f);
            Junction(w,18,3.2f);Junction(w,47,3.2f);Junction(w,73,3.2f);Junction(w,85.5f,3.2f);
            
            P(w,"Metro entry court",new Vector3(0,.055f,94),new Vector3(12,.06f,9),new Color(.22f,.43f,.47f),.03f);
            for(int s=-1;s<=1;s+=2)
            {
                Solid(w,"Metro entrance post",s*3.8f,94,.35f,.35f,3,Ink);
                Solid(w,"Coffee kiosk",s*16,94,4,4,2.1f,s<0?Coral:Blue);
                P(w,"Kiosk awning",new Vector3(s*16,2.2f,93.6f),new Vector3(4.3f,.25f,4.5f),Cream,.1f);
            }
            P(w,"Metro destination sign",new Vector3(0,3.05f,95),new Vector3(8,.65f,.3f),Teal,.12f);
            Label(w,"M  CITY METRO",new Vector3(0,3.07f,94.8f),.45f,Cream,false);
            Route(w,false,new Vector2(0,4),new Vector2(0,28),new Vector2(5,30),new Vector2(5,43),new Vector2(0,45),new Vector2(0,55),new Vector2(-5,58),new Vector2(-5,70),new Vector2(0,71),new Vector2(0,94));
            Route(w,true,new Vector2(0,4),new Vector2(13,10),new Vector2(13,43),new Vector2(-13,43),new Vector2(-13,70),new Vector2(13,70),new Vector2(13,84),new Vector2(13,90),new Vector2(0,90),new Vector2(0,94));
        }
        static void Station(WorldData w)
        {
            w.title="地铁早高峰 · 长站台";
            w.routeHint="通勤者正汇入车门，前方随时有人抢位。\n黄线穿换乘口，青线走侧道；挤住时稳住后退。";
            P(w,"Station tiled floor",new Vector3(0,.025f,50),new Vector3(39,.045f,99),new Color(.79f,.84f,.81f),.02f);
            for(int z=0;z<100;z+=4)P(w,"Tile seam",new Vector3(0,.055f,z),new Vector3(39,.015f,.035f),new Color(.64f,.72f,.71f),.005f);
            for(int x=-16;x<=16;x+=8)P(w,"Long tile seam",new Vector3(x,.055f,50),new Vector3(.035f,.015f,99),new Color(.64f,.72f,.71f),.005f);
            for(int s=-1;s<=1;s+=2)
            {
                for(int z=12;z<=80;z+=22)
                {
                    Pillar(w,s*17,z);Obstacle(w,s*17,z,1.1f,1.1f);
                    Bench(w,s*17,z+5,90);Obstacle(w,s*17,z+5,1.2f,2.5f);
                }
                P(w,"Tactile guidance strip",new Vector3(s*13,.09f,44),new Vector3(.42f,.08f,85),Gold,.02f);
                Solid(w,"Ticket machine",s*17,5,1.5f,1,2,Blue);
                P(w,"Ticket screen",new Vector3(s*17,1.4f,4.47f),new Vector3(1,.65f,.07f),Ink,.05f);
            }
            Divider(w,22,-1,Blue);Divider(w,49,1,Blue);Divider(w,74,-1,Blue);
            for(int s=-1;s<=1;s+=2)
            {
                Solid(w,"Ticket gate",s*3.7f,12,1.2f,4,1.3f,Ink);
                P(w,"Gate scanner",new Vector3(s*3.7f,1.36f,11.2f),new Vector3(.8f,.08f,.7f),Teal,.07f);
                Solid(w,"Platform seating island",s*6.5f,37,5.5f,6,.85f,Coral);
                Solid(w,"Transfer column island",s*7.0f,63,6,5,1.1f,Cream);
            }
            Junction(w,18,4.3f);Junction(w,46,4.3f);Junction(w,71,4.3f);
            P(w,"Boarding warning stripe",new Vector3(0,.1f,86.2f),new Vector3(39,.08f,.55f),Gold,.02f);
            for(int s=-1;s<=1;s+=2)
            {
                Solid(w,"Train side shell",s*10.8f,89,18.4f,.45f,1.85f,Cream);
                P(w,"Train teal stripe",new Vector3(s*10.8f,.6f,88.74f),new Vector3(18.2f,.30f,.035f),Teal,.02f);
                for(int x=4;x<19;x+=4)P(w,"Train window",new Vector3(s*x,1.35f,88.74f),new Vector3(2.1f,.65f,.04f),Blue,.08f);
                Solid(w,"Train rear seats",s*8,97.7f,9,1.3f,.9f,Coral);
            }
            P(w,"Roofless train floor",new Vector3(0,.045f,94.5f),new Vector3(39,.08f,10),new Color(.54f,.75f,.73f),.02f);
            Solid(w,"Train rear wall",0,99.7f,40,.3f,1.6f,Cream);
            P(w,"Open door header",new Vector3(0,2.4f,89),new Vector3(3.2f,.2f,.45f),Teal,.05f);
            Label(w,"08  CITY LINE",new Vector3(0,2.9f,96),.5f,Ink,false);
            
            Route(w,false,new Vector2(0,4),new Vector2(0,28),new Vector2(0,54),new Vector2(0,80),new Vector2(0,85),new Vector2(0,94));
            Route(w,true,new Vector2(0,4),new Vector2(-13,8),new Vector2(-13,28),new Vector2(-13,42),new Vector2(13,42),new Vector2(13,54),new Vector2(13,67.5f),new Vector2(-13,67.5f),new Vector2(-13,82),new Vector2(0,85),new Vector2(0,94));
        }
        static void Office(WorldData w)
        {
            w.title="公司抢打卡 · 大厅与走廊";
            w.routeHint="同事们正抢着进门打卡，注意入口和走廊的汇流。\n黄线穿大厅，青线绕办公区；SHIFT 稳住再穿插。";
            P(w,"Office wood floor",new Vector3(0,.025f,50),new Vector3(39,.05f,99),new Color(.86f,.75f,.56f),.03f);
            for(int x=-18;x<=18;x+=3)P(w,"Wood plank seam",new Vector3(x,.06f,50),new Vector3(.025f,.015f,99),new Color(.73f,.62f,.45f),.005f);
            P(w,"Entrance lobby rug",new Vector3(0,.085f,9),new Vector3(24,.035f,16),new Color(.34f,.59f,.62f),.02f);
            P(w,"Central runner",new Vector3(0,.085f,55),new Vector3(4,.035f,74),new Color(.58f,.76f,.69f),.01f);
            Divider(w,24,1,Cream);Divider(w,51,-1,Cream);Divider(w,77,1,Cream);
            for(int s=-1;s<=1;s+=2)
            {
                Planter(w,s*17,7,2,2);Plant(w,s*17,7);
                Solid(w,"Lobby reception counter",s*8,15,7,3,1.1f,Cream);
                P(w,"Reception accent",new Vector3(s*8,.65f,13.45f),new Vector3(5.8f,.4f,.04f),Teal,.02f);
                for(int z=32;z<=64;z+=32)
                {
                    Desk(w,s*17,z,s);Obstacle(w,s*17,z,3.4f,1.6f);
                    Desk(w,s*17,z+6,s);Obstacle(w,s*17,z+6,3.4f,1.6f);
                }
                P(w,"Bypass carpet",new Vector3(s*13,.08f,51),new Vector3(3,.03f,69),new Color(.69f,.78f,.68f),.01f);
                Solid(w,"Breakroom sofa",s*16,87,5,2,1,Coral);
            }
            // Offset islands force two purposeful lane changes on the short route.
            Solid(w,"Meeting room low partition",1,35,6,9,1.0f,Cream);
            P(w,"Meeting table",new Vector3(1,1.1f,35),new Vector3(4,.15f,6),Blue,.1f);
            Solid(w,"Printer delivery island",-1,63,6,10,1.0f,Blue);
            P(w,"Printer paper stack",new Vector3(-1,1.08f,63),new Vector3(4,.15f,6),Cream,.04f);
            Junction(w,19,3.2f);Junction(w,46,4.3f);Junction(w,72,3.2f);Junction(w,83,3.2f);
            // The final turnstile has a readable 3.6m central opening.
            Solid(w,"Clock hall divider left",-10.9f,90,18.2f,.6f,.9f,Ink);
            Solid(w,"Clock hall divider right",10.9f,90,18.2f,.6f,.9f,Ink);
            P(w,"Clock-in foyer",new Vector3(0,.11f,94),new Vector3(12,.04f,7),Teal,.02f);
            Solid(w,"Clock in pedestal",0,97,1.3f,.9f,1.4f,Ink);
            P(w,"Clock in tablet",new Vector3(0,1.7f,96.9f),new Vector3(1.8f,1.1f,.2f),Teal,.13f);
            P(w,"Tablet display",new Vector3(0,1.7f,96.78f),new Vector3(1.5f,.8f,.04f),Cream,.03f);
            Label(w,"08:00",new Vector3(0,1.7f,96.73f),.28f,Ink,false);
            Label(w,"MORNING CLUB",new Vector3(0,2.8f,98.5f),.6f,Ink,false);
            
            Route(w,false,new Vector2(0,4),new Vector2(0,27),new Vector2(-5,29),new Vector2(-5,42),new Vector2(0,43),new Vector2(0,54),new Vector2(5,56),new Vector2(5,70),new Vector2(0,70),new Vector2(0,94));
            Route(w,true,new Vector2(0,4),new Vector2(13,9),new Vector2(13,42),new Vector2(-13,42),new Vector2(-13,69.5f),new Vector2(13,69.5f),new Vector2(13,82),new Vector2(12,88),new Vector2(0,88),new Vector2(0,94));
        }


        static void Goal(WorldData w,int stage)
        {
            var pos=w.goal;pos.y=.14f;
            P(w,"Cyan destination pad",pos,new Vector3(w.goalHalfSize.x*2,.045f,w.goalHalfSize.y*2),Teal,.02f);
            for(int s=-1;s<=1;s+=2)P(w,"Destination edge",pos+new Vector3(s*w.goalHalfSize.x,.03f,0),new Vector3(.09f,.05f,w.goalHalfSize.y*2+.14f),Cream,.02f);
            Arrow(w,pos+new Vector3(0,.05f,0),.5f,Cream);
            var marker=new GameObject("Goal beacon").transform;marker.SetParent(w.root,false);marker.position=w.goal+Vector3.up*2.4f;
            var ring=RushVisuals.Ring(marker,"Floating beacon",Vector3.zero,.35f,.05f,Teal);ring.localRotation=Quaternion.Euler(65,0,0);
            marker.gameObject.AddComponent<RushGoalBob>();
        }
        static void Tree(WorldData w,float x,float z)
        {
            RushVisuals.Rod(w.root,"Tree trunk",new Vector3(x,.2f,z),new Vector3(x,1.9f,z),.16f,new Color(.55f,.36f,.24f));
            Sphere(w,"Tree crown",new Vector3(x,2.25f,z),new Vector3(1.9f,1.8f,1.8f),Green);
            Sphere(w,"Tree sunlit crown",new Vector3(x-.4f,2.75f,z-.12f),new Vector3(1.2f,1.1f,1.2f),new Color(.57f,.75f,.39f));
        }
        static void Planter(WorldData w,float x,float z,float width,float depth)
        { P(w,"Rounded planter",new Vector3(x,.3f,z),new Vector3(width,.6f,depth),Cream,.14f);P(w,"Planter soil",new Vector3(x,.61f,z),new Vector3(width-.23f,.05f,depth-.23f),new Color(.43f,.34f,.26f),.02f);Obstacle(w,x,z,width,depth); }
        static void Plant(WorldData w,float x,float z)
        {
            for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;var t=Sphere(w,"Broad plant leaf",new Vector3(x+Mathf.Cos(a)*.22f,1.0f,z+Mathf.Sin(a)*.22f),new Vector3(.33f,.9f,.23f),i%2==0?Green:Teal);t.localRotation=Quaternion.Euler(Mathf.Sin(a)*28,0,-Mathf.Cos(a)*28);}
        }
        static void Bench(WorldData w,float x,float z,float yaw)
        {
            var root=new GameObject("Park bench").transform;root.SetParent(w.root,false);root.localPosition=new Vector3(x,0,z);root.localRotation=Quaternion.Euler(0,yaw,0);
            RushVisuals.Box(root,"Seat",new Vector3(0,.5f,0),new Vector3(2.3f,.17f,.75f),Gold,.07f);RushVisuals.Box(root,"Back",new Vector3(0,1.0f,.32f),new Vector3(2.3f,.7f,.15f),Gold,.07f);
            for(int s=-1;s<=1;s+=2)RushVisuals.Box(root,"Leg",new Vector3(s*.85f,.25f,0),new Vector3(.16f,.5f,.62f),Ink,.06f);
        }
        static void StreetLamp(WorldData w,float x,float z)
        {
            RushVisuals.Rod(w.root,"Street lamp pole",new Vector3(x,0,z),new Vector3(x,2.8f,z),.065f,Ink);Sphere(w,"Lamp globe",new Vector3(x,2.87f,z),new Vector3(.45f,.40f,.45f),Cream);
            P(w,"Lamp foot",new Vector3(x,.12f,z),new Vector3(.32f,.24f,.32f),Ink,.08f);Obstacle(w,x,z,.35f,.35f);
        }
        static void Cone(WorldData w,float x,float z)
        {
            P(w,"Cone foot",new Vector3(x,.10f,z),new Vector3(.52f,.14f,.52f),Ink,.07f);Obstacle(w,x,z,.52f,.52f);
            RushVisuals.Primitive(w.root,"Traffic cone",PrimitiveType.Capsule,new Vector3(x,.36f,z),new Vector3(.28f,.28f,.28f),Coral);
            RushVisuals.Primitive(w.root,"Cone reflective band",PrimitiveType.Cylinder,new Vector3(x,.39f,z),new Vector3(.30f,.055f,.30f),Cream);
        }
        static void Barrier(WorldData w,float x,float z,float width)
        {
            P(w,"Road barrier",new Vector3(x,.65f,z),new Vector3(width,.46f,.16f),Gold,.07f);
            for(int i=0;i<5;i++){var t=P(w,"Barrier stripe",new Vector3(x-width*.4f+i*width*.2f,.65f,z-.09f),new Vector3(.16f,.46f,.025f),Ink,.01f);t.localRotation=Quaternion.Euler(0,0,-22);}
            for(int s=-1;s<=1;s+=2)P(w,"Barrier foot",new Vector3(x+s*width*.38f,.32f,z),new Vector3(.15f,.65f,.55f),Ink,.05f);
            Obstacle(w,x,z,width,.55f);
        }
        static void Pillar(WorldData w,float x,float z)
        {P(w,"Station column",new Vector3(x,1.45f,z),new Vector3(1.0f,2.9f,1.0f),Cream,.17f);P(w,"Column band",new Vector3(x,1.7f,z),new Vector3(1.04f,.34f,1.04f),Teal,.15f);}
        static void Desk(WorldData w,float x,float z,int facing)
        {
            P(w,"Desk top",new Vector3(x,.95f,z),new Vector3(3.4f,.22f,1.6f),Cream,.13f);
            for(int s=-1;s<=1;s+=2)P(w,"Desk leg",new Vector3(x+s*1.3f,.45f,z),new Vector3(.17f,.9f,1.1f),Ink,.06f);
            P(w,"Monitor",new Vector3(x,1.52f,z+.32f),new Vector3(1.15f,.8f,.15f),Ink,.10f);P(w,"Monitor screen",new Vector3(x,1.53f,z+.225f),new Vector3(.93f,.59f,.035f),Blue,.025f);
            P(w,"Monitor foot",new Vector3(x,1.14f,z+.32f),new Vector3(.12f,.3f,.12f),Ink,.025f);P(w,"Keyboard",new Vector3(x,1.09f,z-.35f),new Vector3(.85f,.05f,.3f),Ink,.02f);
            RushVisuals.Primitive(w.root,"Desk coffee cup",PrimitiveType.Cylinder,new Vector3(x+.9f,1.2f,z-.25f),new Vector3(.22f,.13f,.22f),Coral);
            P(w,"Notebook",new Vector3(x-1.05f,1.1f,z-.2f),new Vector3(.45f,.06f,.6f),Teal,.03f);
        }
        static void Arrow(WorldData w,Vector3 p,float scale,Color c)
        {
            P(w,"Wayfinding arrow stem",p+new Vector3(0,0,-.25f*scale),new Vector3(.18f*scale,.025f,.66f*scale),c,.01f);
            var g=new GameObject("Wayfinding arrow head");g.transform.SetParent(w.root,false);g.transform.position=p;
            var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-.4f*scale,0,0),new Vector3(0,0,.5f*scale),new Vector3(.4f*scale,0,0)};mesh.triangles=new[]{0,1,2};mesh.RecalculateNormals();g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=RushVisuals.Mat(c);
        }
        static void RingUpright(WorldData w,Vector3 p,float r,Color c){var t=RushVisuals.Ring(w.root,"Clock rim",p,r,.045f,c);t.localRotation=Quaternion.Euler(90,0,0);}
        static void Label(WorldData w,string text,Vector3 p,float size,Color color,bool floor)
        {
            var g=new GameObject("Sign "+text);g.transform.SetParent(w.root,false);g.transform.position=p;if(floor)g.transform.rotation=Quaternion.Euler(90,0,0);
            var tm=g.AddComponent<TextMesh>();tm.text=text;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.fontSize=64;tm.characterSize=size*.20f;tm.color=color;
            tm.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");g.GetComponent<MeshRenderer>().sharedMaterial=tm.font.material;
        }
    }
    public class RushGoalBob : MonoBehaviour
    {
        Vector3 origin;
        RushGame game;
        float elapsed;

        void Awake()
        {
            origin=transform.localPosition;
        }

        void Update()
        {
            if(game==null)game=Object.FindFirstObjectByType<RushGame>();
            if(game==null || game.State!=RushState.Playing)return;
            elapsed+=Time.deltaTime;
            transform.localPosition=origin+Vector3.up*Mathf.Sin(elapsed*3)*.12f;
        }
    }

}
