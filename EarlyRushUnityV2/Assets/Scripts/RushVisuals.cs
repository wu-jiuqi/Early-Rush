using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush
{
    // All meshes and materials are authored here; no external game assets are used.
    public static class RushVisuals
    {
        static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        static readonly Dictionary<string, Mesh> boxes = new Dictionary<string, Mesh>();
        static Mesh ringMesh;
        public static readonly Color Ink = new Color(.13f,.22f,.28f);
        public static readonly Color Orange = new Color(1f,.39f,.15f);
        public static readonly Color Teal = new Color(.18f,.76f,.73f);
        public static readonly Color Cream = new Color(.98f,.92f,.77f);

        public static Material Mat(Color color)
        {
            if (materials.TryGetValue(color, out var m)) return m;
            m = new Material(Shader.Find("Standard")); m.color = color;
            m.SetFloat("_Glossiness", .2f); m.SetFloat("_Metallic", 0);
            materials[color] = m; return m;
        }

        public static Transform Primitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            var o = GameObject.CreatePrimitive(type); o.name = name; o.transform.SetParent(parent,false);
            o.transform.localPosition = position; o.transform.localScale = scale;
            Object.Destroy(o.GetComponent<Collider>()); o.GetComponent<Renderer>().sharedMaterial = Mat(color);
            return o.transform;
        }

        public static Transform Box(Transform parent, string name, Vector3 position, Vector3 size, Color color, float radius=.12f)
        {
            radius = Mathf.Min(radius, Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.48f);
            var key = size.ToString("F3") + radius.ToString("F3");
            if (!boxes.TryGetValue(key, out var mesh)) { mesh=RoundedBox(size,radius); boxes[key]=mesh; }
            var g=new GameObject(name); g.transform.SetParent(parent,false);g.transform.localPosition=position;
            g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=Mat(color);
            return g.transform;
        }

        static Mesh RoundedBox(Vector3 size,float r)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
            var half=size*.5f;var inner=half-Vector3.one*r;
            Vector3[] directions={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            foreach(var n in directions)
            {
                Vector3 u=Vector3.Cross(n, Mathf.Abs(n.y)>.5f?Vector3.forward:Vector3.up).normalized;
                Vector3 v=Vector3.Cross(n,u);
                float hu=Vector3.Dot(new Vector3(Mathf.Abs(u.x),Mathf.Abs(u.y),Mathf.Abs(u.z)),half);
                float hv=Vector3.Dot(new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z)),half);
                float hn=Vector3.Dot(new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z)),half);
                float[] xs={-hu,-hu+r,0,hu-r,hu};float[] ys={-hv,-hv+r,0,hv-r,hv};
                int start=vertices.Count;
                for(int y=0;y<5;y++)for(int x=0;x<5;x++)
                {
                    Vector3 p=n*hn+u*xs[x]+v*ys[y];
                    var c=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));
                    Vector3 normal=(p-c).normalized;vertices.Add(c+normal*r);normals.Add(normal);
                }
                for(int y=0;y<4;y++)for(int x=0;x<4;x++)
                { int a=start+y*5+x;triangles.Add(a);triangles.Add(a+1);triangles.Add(a+6);triangles.Add(a);triangles.Add(a+6);triangles.Add(a+5); }
            }
            var mesh=new Mesh();mesh.name="Handcrafted rounded box";mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }

        public static Transform Rod(Transform parent,string name,Vector3 a,Vector3 b,float radius,Color color)
        {
            var t=Primitive(parent,name,PrimitiveType.Cylinder,(a+b)*.5f,new Vector3(radius*2,Vector3.Distance(a,b)*.5f,radius*2),color);
            t.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);return t;
        }

        public static Transform Ring(Transform parent,string name,Vector3 position,float radius,float thickness,Color color)
        {
            var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=position;
            if(ringMesh==null)
            {
                var vs=new List<Vector3>();var ns=new List<Vector3>();var ts=new List<int>();
                for(int i=0;i<=40;i++){float a=i*Mathf.PI*2/40;for(int j=0;j<=8;j++){float b=j*Mathf.PI*2/8;var n=new Vector3(Mathf.Cos(a)*Mathf.Cos(b),Mathf.Sin(b),Mathf.Sin(a)*Mathf.Cos(b));vs.Add(new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))+n*.1f);ns.Add(n);}}
                for(int i=0;i<40;i++)for(int j=0;j<8;j++){int a=i*9+j;ts.Add(a);ts.Add(a+1);ts.Add(a+9);ts.Add(a+1);ts.Add(a+10);ts.Add(a+9);}
                ringMesh=new Mesh();ringMesh.SetVertices(vs);ringMesh.SetNormals(ns);ringMesh.SetTriangles(ts,0);ringMesh.RecalculateBounds();
            }
            g.AddComponent<MeshFilter>().sharedMesh=ringMesh;g.AddComponent<MeshRenderer>().sharedMaterial=Mat(color);g.transform.localScale=new Vector3(radius,thickness*10,radius);return g.transform;
        }

        public static Transform CreatePerson(Transform parent, Color color, bool player, bool bike)
        {
            var root=new GameObject(player?"Player • orange commuter":"Commuter").transform;root.SetParent(parent,false);
            var parts=root.gameObject.AddComponent<RushPersonParts>();parts.bike=bike;
            var rig=new GameObject("Soft body rig").transform;rig.SetParent(root,false);parts.rig=rig;
            float h=bike?.18f:0;
            var skin=new Color(.99f,.71f,.49f);
            Box(rig,"Chunky jacket",new Vector3(0,.86f+h,0),new Vector3(.75f,.77f,.5f),color,.22f);
            Primitive(rig,"Neck",PrimitiveType.Sphere,new Vector3(0,1.22f+h,0),new Vector3(.24f,.24f,.25f),skin);
            Primitive(rig,"Round face",PrimitiveType.Sphere,new Vector3(0,1.52f+h,.035f),new Vector3(.76f,.73f,.67f),skin);
            Primitive(rig,"Hair cap",PrimitiveType.Sphere,new Vector3(0,1.76f+h,-.04f),new Vector3(.79f,.42f,.67f),Ink);
            Primitive(rig,"Hair fringe",PrimitiveType.Sphere,new Vector3(-.15f,1.69f+h,.27f),new Vector3(.37f,.20f,.15f),Ink);
            for(int side=-1;side<=1;side+=2)
            {
                Primitive(rig,"Ear",PrimitiveType.Sphere,new Vector3(side*.37f,1.5f+h,.025f),new Vector3(.16f,.23f,.16f),skin);
                Primitive(rig,"Bright eye",PrimitiveType.Sphere,new Vector3(side*.14f,1.54f+h,.341f),new Vector3(.065f,.085f,.042f),Ink);
                Primitive(rig,"Blush",PrimitiveType.Sphere,new Vector3(side*.23f,1.42f+h,.326f),new Vector3(.1f,.05f,.025f),new Color(.94f,.4f,.29f));
            }
            Primitive(rig,"Button nose",PrimitiveType.Sphere,new Vector3(0,1.43f+h,.37f),new Vector3(.11f,.1f,.1f),skin);
            parts.legs=new Transform[2];parts.arms=new Transform[2];
            for(int i=0;i<2;i++)
            {
                float side=i==0?-1:1;
                var leg=new GameObject("Leg").transform;leg.SetParent(rig,false);leg.localPosition=new Vector3(side*.22f,.54f+h,0);parts.legs[i]=leg;
                Box(leg,"Trouser",new Vector3(0,-.17f,0),new Vector3(.26f,.42f,.29f),Ink,.1f);
                Box(leg,"Sneaker",new Vector3(0,-.39f,.08f),new Vector3(.3f,.19f,.43f),Cream,.08f);
                var arm=new GameObject("Arm").transform;arm.SetParent(rig,false);arm.localPosition=new Vector3(side*.37f,1.09f+h,0);parts.arms[i]=arm;
                Box(arm,"Sleeve",new Vector3(side*.055f,-.18f,0),new Vector3(.25f,.42f,.29f),color,.11f);
                Primitive(arm,"Mitten",PrimitiveType.Sphere,new Vector3(side*.06f,-.40f,.01f),new Vector3(.24f,.25f,.26f),skin);
            }
            if(player)
            {
                Box(rig,"Backpack",new Vector3(0,.93f,-.36f),new Vector3(.52f,.57f,.31f),new Color(.96f,.69f,.21f),.13f);
                Box(rig,"Backpack pocket",new Vector3(0,.79f,-.54f),new Vector3(.34f,.22f,.08f),Orange,.04f);
                Box(rig,"Backpack patch",new Vector3(0,1.06f,-.53f),new Vector3(.17f,.12f,.025f),Cream,.02f);
                Ring(root,"Player ground marker",new Vector3(0,.16f,0),.58f,.035f,Orange);
            }
            else
            {
                Box(rig,"Commuter bag",new Vector3(.38f,.68f,-.14f),new Vector3(.3f,.4f,.27f),new Color(.33f,.25f,.23f),.08f);
            }
            if(bike)CreateBike(root,player?Teal:color);
            return root;
        }

        static void CreateBike(Transform parent,Color color)
        {
            var root=new GameObject("City bicycle").transform;root.SetParent(parent,false);
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 c=new Vector3(0,.37f,side*.66f);
                var wheel=Ring(root,"Rubber tyre",c,.34f,.055f,Ink);wheel.localRotation=Quaternion.Euler(0,0,90);
                var rim=Ring(root,"Metal rim",c,.27f,.022f,Cream);rim.localRotation=Quaternion.Euler(0,0,90);
                Rod(root,"Axle",c+Vector3.left*.13f,c+Vector3.right*.13f,.045f,Ink);
                for(int i=0;i<4;i++){float a=i*Mathf.PI/4;Vector3 v=new Vector3(0,Mathf.Cos(a),Mathf.Sin(a))*.27f;Rod(root,"Spoke",c-v,c+v,.011f,Cream);}
            }
            Vector3 rear=new Vector3(0,.37f,-.66f),front=new Vector3(0,.37f,.66f),seat=new Vector3(0,.85f,-.25f),crank=new Vector3(0,.35f,-.05f),fork=new Vector3(0,.88f,.46f);
            Rod(root,"Frame",rear,seat,.055f,color);Rod(root,"Frame",seat,crank,.055f,color);Rod(root,"Frame",crank,rear,.055f,color);Rod(root,"Frame",seat,fork,.055f,color);Rod(root,"Frame",crank,fork,.055f,color);Rod(root,"Front fork",fork,front,.045f,color);
            Rod(root,"Stem",fork,new Vector3(0,1.12f,.52f),.04f,Ink);Rod(root,"Handlebar",new Vector3(-.43f,1.12f,.52f),new Vector3(.43f,1.12f,.52f),.045f,Ink);
            Box(root,"Saddle",new Vector3(0,.93f,-.25f),new Vector3(.28f,.12f,.4f),Ink,.06f);
            Primitive(root,"Headlight",PrimitiveType.Sphere,new Vector3(0,.93f,.66f),new Vector3(.16f,.15f,.13f),Cream);
        }

        public static void AnimatePerson(Transform visual, Vector2 velocity,float time,bool dash,bool brace)
        {
            if(!visual)return;var p=visual.GetComponent<RushPersonParts>();if(!p)return;
            float speed=Mathf.Clamp01(velocity.magnitude/5f);float stride=Mathf.Sin(time*(p.bike?15:12));
            float bob=p.bike?0:Mathf.Abs(stride)*.055f*speed;
            p.rig.localPosition=new Vector3(0,bob,0);
            p.rig.localScale=Vector3.Lerp(p.rig.localScale,brace?new Vector3(1.09f,.89f,1.09f):dash?new Vector3(.93f,1.04f,.94f):Vector3.one,Time.unscaledDeltaTime*16);
            p.rig.localRotation=Quaternion.Euler(dash?13:brace?-5:p.bike?12:4*speed,0,p.bike?-velocity.x*1.5f:-stride*2*speed);
            for(int i=0;i<2;i++)
            {
                float s=i==0?1:-1;
                p.legs[i].localRotation=Quaternion.Euler(p.bike?(-25+stride*s*20):stride*s*29*speed,0,0);
                p.arms[i].localRotation=Quaternion.Euler(p.bike?-60:brace?-25:-stride*s*24*speed,0,brace?s*18:0);
            }
        }
    }

    public class RushPersonParts:MonoBehaviour
    {
        public Transform rig;public Transform[] legs,arms;public bool bike;
    }
}
