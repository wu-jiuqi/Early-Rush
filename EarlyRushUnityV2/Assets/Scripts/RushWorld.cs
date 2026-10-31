using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush
{
    public class WorldData
    {
        public Transform root;
        public Vector3 spawn, goal;
        public Vector2 goalHalfSize;
        public string title;
        public List<CrowdSpawn> crowd=new List<CrowdSpawn>();
        public List<Rect> obstacles=new List<Rect>();
        public Vector2 boundsMin=new Vector2(-8.5f,-11.5f),boundsMax=new Vector2(8.5f,11.5f);
    }
    public struct CrowdSpawn
    {
        public Vector2 position,axis;public float range,speed;public Color color;public bool cyclist;
        public CrowdSpawn(float x,float z,float ax,float az,float range,float speed,Color color,bool cyclist=false)
        {position=new Vector2(x,z);axis=new Vector2(ax,az);this.range=range;this.speed=speed;this.color=color;this.cyclist=cyclist;}
    }

    public static class RushWorld
    {
        static readonly Color Teal=RushVisuals.Teal, Ink=RushVisuals.Ink, Cream=RushVisuals.Cream;
        static readonly Color Coral=new Color(.96f,.42f,.29f),Blue=new Color(.32f,.59f,.77f),Green=new Color(.40f,.64f,.39f),Gold=new Color(.98f,.73f,.28f);
        static readonly Color[] coats={new Color(.39f,.57f,.79f),new Color(.74f,.52f,.73f),new Color(.42f,.69f,.62f),new Color(.95f,.68f,.32f),new Color(.70f,.41f,.40f)};
        static Transform P(WorldData w,string n,Vector3 p,Vector3 s,Color c,float r=.12f)=>RushVisuals.Box(w.root,n,p,s,c,r);
        static Transform Sphere(WorldData w,string n,Vector3 p,Vector3 s,Color c)=>RushVisuals.Primitive(w.root,n,PrimitiveType.Sphere,p,s,c);
        static void Obstacle(WorldData w,float x,float z,float width,float depth){w.obstacles.Add(new Rect(x-width/2,z-depth/2,width,depth));}
        static void Person(WorldData w,float x,float z,float ax,float az,float range,float speed,int color,bool bike=false){w.crowd.Add(new CrowdSpawn(x,z,ax,az,range,speed,coats[color%coats.Length],bike));}

        public static WorldData Build(int stage)
        {
            var w=new WorldData {root=new GameObject("Handcrafted scene "+(stage+1)).transform,spawn=new Vector3(0,0,-10),goal=new Vector3(0,0,9.7f),goalHalfSize=new Vector2(1.3f,.8f)};
            P(w,"Diorama plinth",new Vector3(0,-.5f,0),new Vector3(19,.95f,25),Ink,.4f);
            P(w,"Sunlit ground",new Vector3(0,-.08f,0),new Vector3(18.2f,.2f,24.2f),stage==0?new Color(.80f,.84f,.70f):new Color(.89f,.87f,.77f),.08f);
            if(stage==0)Street(w);else if(stage==1)Station(w);else Office(w);
            Goal(w,stage);return w;
        }

        static void Street(WorldData w)
        {
            w.title="骑车赶地铁";
            P(w,"Asphalt",new Vector3(0,.025f,0),new Vector3(11,.045f,24),new Color(.37f,.48f,.51f),.02f);
            P(w,"Bike lane left",new Vector3(-4.25f,.055f,0),new Vector3(2,.025f,24),new Color(.31f,.62f,.61f),.01f);
            P(w,"Bike lane right",new Vector3(4.25f,.055f,0),new Vector3(2,.025f,24),new Color(.31f,.62f,.61f),.01f);
            for(int s=-1;s<=1;s+=2)
            {
                P(w,"Cream curb",new Vector3(s*5.6f,.12f,0),new Vector3(.18f,.22f,24),Cream,.05f);
                for(int z=-11;z<12;z+=2)P(w,"Sidewalk slab",new Vector3(s*7.15f,.055f,z),new Vector3(2.8f,.08f,1.94f),new Color(.86f,.84f,.72f),.03f);
                for(int z=-10;z<11;z+=4)P(w,"Broken lane stripe",new Vector3(s*3.1f,.08f,z),new Vector3(.09f,.025f,1.8f),Cream,.01f);
            }
            for(int z=-10;z<9;z+=3)P(w,"Center paint",new Vector3(0,.065f,z),new Vector3(.12f,.025f,1.1f),Gold,.01f);
            for(int x=-5;x<=5;x++)P(w,"Zebra crossing",new Vector3(x,.083f,6.2f),new Vector3(.62f,.035f,2),Cream,.02f);
            Arrow(w,new Vector3(4.2f,.10f,-7),.7f,Cream);Arrow(w,new Vector3(-4.2f,.10f,2.4f),.7f,Cream);
            Planter(w,-7.1f,-8.1f,1.8f,1.7f);Tree(w,-7.1f,-8.1f);Planter(w,7.1f,1.3f,1.7f,1.7f);Tree(w,7.1f,1.3f);
            Tree(w,-7.2f,7.7f);Obstacle(w,-7.2f,7.7f,1.3f,1.3f);
            Bench(w,7.1f,-5,90);Obstacle(w,7.1f,-5,1.1f,2.4f);
            Bench(w,-7.1f,1.5f,90);Obstacle(w,-7.1f,1.5f,1.1f,2.4f);
            StreetLamp(w,-5.8f,-3);StreetLamp(w,5.8f,5.2f);
            // Roadworks create two readable route choices; the bike lanes remain open.
            P(w,"Roadworks soil",new Vector3(-1.8f,.13f,-5),new Vector3(2.9f,.18f,1.7f),new Color(.50f,.39f,.29f),.12f);Obstacle(w,-1.8f,-5,3.1f,1.9f);
            Barrier(w,-1.8f,-5.9f,3.1f);Cone(w,-3.5f,-5.2f);Cone(w,-.1f,-4.6f);
            P(w,"Delivery stack lower",new Vector3(1.9f,.40f,.9f),new Vector3(1.8f,.8f,1.6f),Gold,.14f);
            P(w,"Delivery stack upper",new Vector3(2.15f,1.0f,1.15f),new Vector3(1.1f,.43f,1.0f),Cream,.10f);Obstacle(w,1.9f,.9f,2.0f,1.8f);
            P(w,"Parcel strap",new Vector3(1.9f,.82f,.9f),new Vector3(.12f,.03f,1.6f),Coral,.01f);
            Cone(w,.65f,-.3f);Obstacle(w,.65f,-.3f,.5f,.5f);
            P(w,"Station entrance surround",new Vector3(0,.035f,10.4f),new Vector3(5.2f,.08f,2.5f),new Color(.22f,.43f,.47f),.03f);
            P(w,"Station entry floor",new Vector3(0,.085f,10.2f),new Vector3(4.5f,.025f,1.9f),Teal,.01f);
            // Decoration sits outside the navigable cyan entry, no invisible ledges.
            for(int s=-1;s<=1;s+=2){P(w,"Metro entry post",new Vector3(s*2.7f,1.4f,10.2f),new Vector3(.2f,2.8f,.2f),Ink,.08f);Obstacle(w,s*2.7f,10.2f,.35f,.35f);}
            P(w,"Metro destination sign",new Vector3(0,2.75f,10.2f),new Vector3(5.6f,.65f,.3f),Teal,.12f);Label(w,"M  •  METRO",new Vector3(0,2.76f,9.99f),.28f,Cream,false);
            P(w,"Coffee kiosk",new Vector3(7.1f,1.15f,8.9f),new Vector3(2.4f,2.2f,2.0f),Coral,.20f);Obstacle(w,7.1f,8.9f,2.4f,2.0f);
            P(w,"Coffee awning",new Vector3(7.1f,2.35f,8.4f),new Vector3(2.7f,.25f,2.5f),Cream,.10f);P(w,"Kiosk window",new Vector3(7.1f,1.45f,7.88f),new Vector3(1.7f,.75f,.06f),Ink,.03f);
            Person(w,-2.5f,-7.2f,1,0,2.0f,1.3f,0);Person(w,2.6f,-3f,1,0,2.2f,.95f,1);Person(w,-1.8f,-.4f,1,0,2.5f,1.25f,2);
            Person(w,-2.5f,4.9f,1,0,2.3f,1.4f,3);Person(w,2.3f,6.8f,1,0,2.1f,1.05f,4);Person(w,-4.2f,2.8f,0,1,3.3f,2.0f,2,true);Person(w,4.2f,-4.5f,0,1,3.4f,2.1f,1,true);
            Person(w,6.6f,-1.8f,0,1,2.0f,.7f,3);Person(w,-6.5f,4.4f,0,1,1.4f,.8f,4);
        }

        static void Station(WorldData w)
        {
            w.title="挤上早高峰";w.goal=new Vector3(0,0,9.6f);w.goalHalfSize=new Vector2(1.0f,.8f);
            P(w,"Station inset floor",new Vector3(0,.02f,-1.9f),new Vector3(17.8f,.08f,19.7f),new Color(.79f,.84f,.81f),.03f);
            for(int x=-8;x<9;x+=2)P(w,"Floor seam",new Vector3(x,.066f,-1.9f),new Vector3(.025f,.014f,19.6f),new Color(.64f,.72f,.71f),.005f);
            for(int z=-10;z<8;z+=2)P(w,"Floor seam",new Vector3(0,.067f,z),new Vector3(17.7f,.014f,.025f),new Color(.64f,.72f,.71f),.005f);
            P(w,"Tactile platform stripe",new Vector3(0,.10f,6.1f),new Vector3(17.5f,.09f,.44f),Gold,.03f);
            for(int x=-8;x<9;x++)for(int k=0;k<2;k++)Sphere(w,"Tactile dot",new Vector3(x,.17f,6.0f+k*.2f),new Vector3(.09f,.04f,.08f),Cream);
            // A roofless toy train keeps the destination visible during the crowded entry.
            P(w,"Train floor",new Vector3(0,.035f,9.6f),new Vector3(17.4f,.08f,3.5f),new Color(.54f,.75f,.73f),.03f);
            P(w,"Train rear wall",new Vector3(0,1.05f,11.4f),new Vector3(17.8f,2.1f,.35f),Cream,.16f);
            for(int s=-1;s<=1;s+=2)
            {
                P(w,"Train side shell",new Vector3(s*4.95f,1.0f,7.6f),new Vector3(6.9f,1.9f,.35f),Cream,.15f);
                P(w,"Train teal stripe",new Vector3(s*4.95f,.55f,7.39f),new Vector3(6.85f,.3f,.055f),Teal,.02f);
                Obstacle(w,s*4.95f,7.6f,6.9f,.45f);
                P(w,"Door frame",new Vector3(s*1.48f,1.25f,7.6f),new Vector3(.14f,2.5f,.34f),Ink,.05f);
                for(int i=0;i<3;i++)P(w,"Train window",new Vector3(s*(2.7f+i*2),1.38f,7.39f),new Vector3(1.4f,.69f,.06f),Blue,.10f);
                P(w,"Rear seating",new Vector3(s*4.8f,.53f,10.5f),new Vector3(4.8f,.8f,.85f),Coral,.2f);Obstacle(w,s*4.8f,10.5f,4.8f,.9f);
                P(w,"Rear seat back",new Vector3(s*4.8f,1.05f,10.95f),new Vector3(4.8f,.8f,.25f),Coral,.1f);
                Pillar(w,s*6.9f,-1.8f);Obstacle(w,s*6.9f,-1.8f,1.1f,1.1f);
            }
            Label(w,"08  CITY LINE",new Vector3(0,1.7f,11.19f),.28f,Ink,false);
            P(w,"Open doorway lintel",new Vector3(0,2.5f,7.6f),new Vector3(3.15f,.22f,.34f),Teal,.06f);
            P(w,"Door light",new Vector3(0,2.67f,7.6f),new Vector3(.66f,.12f,.12f),Gold,.05f);
            for(int x=-5;x<=5;x+=5)
            {
                P(w,"Ticket gate pedestal",new Vector3(x,.65f,-5.3f),new Vector3(.72f,1.25f,2.0f),new Color(.41f,.55f,.59f),.18f);Obstacle(w,x,-5.3f,.82f,2.1f);
                P(w,"Gate scanner",new Vector3(x,1.31f,-5.8f),new Vector3(.48f,.07f,.43f),Ink,.06f);P(w,"Gate green light",new Vector3(x,1.36f,-5.8f),new Vector3(.19f,.03f,.16f),Teal,.02f);
            }
            Arrow(w,new Vector3(-2.5f,.12f,-6.2f),.75f,Teal);Arrow(w,new Vector3(2.5f,.12f,-6.2f),.75f,Teal);Arrow(w,new Vector3(0,.12f,3.1f),.85f,Teal);
            Bench(w,-7.0f,2.9f,90);Obstacle(w,-7.0f,2.9f,1.2f,2.5f);
            P(w,"Ticket machine",new Vector3(7.1f,1.0f,-8.8f),new Vector3(1.35f,2.0f,.85f),Blue,.18f);Obstacle(w,7.1f,-8.8f,1.4f,.9f);
            P(w,"Ticket touchscreen",new Vector3(7.1f,1.3f,-9.26f),new Vector3(.92f,.65f,.08f),Ink,.05f);
            Label(w,"M",new Vector3(-7.9f,.12f,-9.1f),1.0f,Teal,true);
            Person(w,-2.4f,-3.1f,1,0,1.3f,.85f,0);Person(w,2.5f,-2.0f,1,0,1.5f,1.05f,1);Person(w,-1.4f,.8f,1,0,2.0f,.9f,2);Person(w,2.9f,1.9f,1,0,1.4f,.8f,3);
            Person(w,-2.1f,4.4f,1,0,1.5f,.7f,4);Person(w,1.9f,4.9f,1,0,1.4f,.8f,0);Person(w,-.65f,6.0f,1,0,.8f,.65f,1);Person(w,.7f,6.7f,1,0,.65f,.6f,2);Person(w,-.4f,8.3f,1,0,.6f,.65f,3);
        }

        static void Office(WorldData w)
        {
            w.title="最后一秒打卡";w.goal=new Vector3(0,0,9.1f);w.goalHalfSize=new Vector2(1.1f,.9f);
            P(w,"Office wood floor",new Vector3(0,.03f,0),new Vector3(17.7f,.08f,23.8f),new Color(.86f,.75f,.56f),.03f);
            for(int x=-8;x<=8;x++)P(w,"Wood plank seam",new Vector3(x,.079f,0),new Vector3(.018f,.012f,23.6f),new Color(.73f,.62f,.45f),.004f);
            P(w,"Lobby rug",new Vector3(0,.094f,-8.4f),new Vector3(7,.035f,4.2f),new Color(.34f,.59f,.62f),.02f);
            P(w,"Runner",new Vector3(0,.10f,3.3f),new Vector3(3.1f,.035f,13.8f),new Color(.58f,.76f,.69f),.015f);
            for(int s=-1;s<=1;s+=2)
            {
                P(w,"Lobby low wall",new Vector3(s*6.9f,.48f,-5.4f),new Vector3(3.5f,.95f,.4f),Cream,.16f);Obstacle(w,s*6.9f,-5.4f,3.5f,.5f);
                P(w,"Turnstile",new Vector3(s*2.3f,.66f,-5.4f),new Vector3(.85f,1.30f,1.7f),Ink,.19f);Obstacle(w,s*2.3f,-5.4f,.95f,1.8f);
                P(w,"Turnstile top",new Vector3(s*2.3f,1.34f,-5.6f),new Vector3(.65f,.1f,.6f),Teal,.07f);
                Planter(w,s*7.15f,-8.6f,1.6f,1.6f);Plant(w,s*7.15f,-8.6f);
                Desk(w,s*5.8f,-1.5f,s);Obstacle(w,s*5.8f,-1.5f,3.4f,1.6f);
                Desk(w,s*5.8f,4.4f,s);Obstacle(w,s*5.8f,4.4f,3.4f,1.6f);
            }
            P(w,"Reception counter",new Vector3(-5.8f,1.0f,9.0f),new Vector3(4.6f,1.9f,2.6f),Cream,.3f);Obstacle(w,-5.8f,9,4.7f,2.7f);
            P(w,"Reception accent",new Vector3(-5.8f,1.0f,7.65f),new Vector3(3.7f,.8f,.08f),Teal,.05f);
            P(w,"Office rear wall",new Vector3(0,.9f,11.5f),new Vector3(17.8f,1.8f,.3f),new Color(.87f,.89f,.8f),.12f);
            P(w,"Clock in machine pedestal",new Vector3(0,.75f,10.5f),new Vector3(1.25f,1.4f,.8f),Ink,.18f);Obstacle(w,0,10.5f,1.3f,.85f);
            P(w,"Clock in tablet",new Vector3(0,1.63f,10.38f),new Vector3(1.6f,1.0f,.23f),Teal,.13f);
            P(w,"Tablet display",new Vector3(0,1.65f,10.24f),new Vector3(1.27f,.66f,.035f),Cream,.03f);Label(w,"08:00",new Vector3(0,1.66f,10.20f),.22f,Ink,false);
            RingUpright(w,new Vector3(0,2.9f,11.29f),.65f,Ink);Label(w,"8",new Vector3(0,2.91f,11.15f),.52f,Ink,false);
            P(w,"Clock face",new Vector3(0,2.9f,11.40f),new Vector3(1.28f,1.28f,.09f),Cream,.6f);
            P(w,"Breakroom sofa",new Vector3(6.2f,.55f,9.7f),new Vector3(3.7f,.95f,1.3f),Coral,.25f);Obstacle(w,6.2f,9.7f,3.8f,1.4f);
            P(w,"Sofa back",new Vector3(6.2f,1.15f,10.25f),new Vector3(3.8f,1.15f,.4f),Coral,.17f);
            P(w,"Printer trolley",new Vector3(-1.1f,.58f,.5f),new Vector3(1.4f,1.1f,1.15f),Blue,.17f);Obstacle(w,-1.1f,.5f,1.5f,1.25f);
            P(w,"Printer papers",new Vector3(-1.1f,1.18f,.5f),new Vector3(.9f,.09f,.7f),Cream,.02f);
            Planter(w,1.5f,5.4f,.9f,1.0f);Plant(w,1.5f,5.4f);
            Arrow(w,new Vector3(0,.14f,-6.5f),.7f,Cream);Arrow(w,new Vector3(0,.14f,3.4f),.7f,Cream);Arrow(w,new Vector3(0,.14f,7.2f),.7f,Cream);
            Person(w,0,-3.2f,1,0,2.5f,1.15f,0);Person(w,-2.7f,-.1f,0,1,2.0f,.85f,1);Person(w,1.5f,1.8f,1,0,1.8f,1.05f,2);
            Person(w,-.9f,4.2f,1,0,1.5f,1.1f,3);Person(w,1.3f,7.2f,1,0,1.9f,.95f,4);Person(w,-2.6f,8.3f,1,0,1.0f,.8f,0);Person(w,3.3f,-8.2f,1,0,1.1f,.8f,2);
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
            P(w,"Cone foot",new Vector3(x,.10f,z),new Vector3(.52f,.14f,.52f),Ink,.07f);
            RushVisuals.Primitive(w.root,"Traffic cone",PrimitiveType.Capsule,new Vector3(x,.36f,z),new Vector3(.28f,.28f,.28f),Coral);
            RushVisuals.Primitive(w.root,"Cone reflective band",PrimitiveType.Cylinder,new Vector3(x,.39f,z),new Vector3(.30f,.055f,.30f),Cream);
        }
        static void Barrier(WorldData w,float x,float z,float width)
        {
            P(w,"Road barrier",new Vector3(x,.65f,z),new Vector3(width,.46f,.16f),Gold,.07f);
            for(int i=0;i<5;i++){var t=P(w,"Barrier stripe",new Vector3(x-width*.4f+i*width*.2f,.65f,z-.09f),new Vector3(.16f,.46f,.025f),Ink,.01f);t.localRotation=Quaternion.Euler(0,0,-22);}
            for(int s=-1;s<=1;s+=2)P(w,"Barrier foot",new Vector3(x+s*width*.38f,.32f,z),new Vector3(.15f,.65f,.55f),Ink,.05f);
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
    public class RushGoalBob:MonoBehaviour
    {
        Vector3 origin;void Start(){origin=transform.localPosition;}void Update(){transform.localPosition=origin+Vector3.up*Mathf.Sin(Time.time*3)*.12f;}
    }
}
