using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush
{
    // Generated raster characters with retained animation/collision contracts. Functional meshes remain for ground, rails and collision telegraphs.
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
            m.SetFloat("_Glossiness", .2f); m.SetFloat("_Metallic", 0); m.enableInstancing=true;
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
            parts.legs=new Transform[2];parts.arms=new Transform[2];
            for(int i=0;i<2;i++)
            {
                parts.legs[i]=new GameObject("Animation leg anchor").transform;parts.legs[i].SetParent(rig,false);
                parts.arms[i]=new GameObject("Animation arm anchor").transform;parts.arms[i].SetParent(rig,false);
            }
            string coat=color.b>color.r&&color.b>color.g?"blue":color.r>color.g&&color.b>color.g?"purple":color.g>color.r?"green":"gold";
            string key=player?(bike?"player-bike-back":"player-walk-back"):coat+(bike?"-bike":"-walk");
            var art=RushArt.Sprite(rig,key,new Vector3(0,bike?.9f:1f,0),bike?2.0f:1.75f,bike?2.4f:2.2f);
            if(art){var face=art.gameObject.AddComponent<RushCharacterArt>();face.owner=root;face.player=player;face.bike=bike;}
            if(player)Ring(root,"Player ground marker",new Vector3(0,.12f,0),.58f,.025f,Teal);
            return root;
        }

        public static Vector3 ImpactScale(float hitAge,float landingAge,float strength)
        {
            Vector3 result=Vector3.one;
            if(hitAge>=0&&hitAge<.65f)
            {
                Vector3 compressed=new Vector3(1.13f,1.28f,.52f);
                Vector3 stretched=new Vector3(.92f,.86f,1.35f);
                Vector3 shape;
                if(hitAge<.065f)shape=compressed;
                else if(hitAge<.145f)shape=Vector3.Lerp(compressed,stretched,Mathf.SmoothStep(0,1,(hitAge-.065f)/.08f));
                else
                {
                    float t=hitAge-.145f;
                    shape=Vector3.one+(stretched-Vector3.one)*(Mathf.Exp(-t*8)*Mathf.Cos(t*26));
                }
                result=Vector3.LerpUnclamped(Vector3.one,shape,strength);
            }
            if(landingAge>=0&&landingAge<.7f)
            {
                float pulse=Mathf.Exp(-landingAge*8)*Mathf.Cos(landingAge*24);
                result=Vector3.Scale(result,new Vector3(1+.20f*pulse,1-.30f*pulse,1+.16f*pulse));
            }
            return new Vector3(Mathf.Clamp(result.x,.45f,1.6f),Mathf.Clamp(result.y,.45f,1.6f),Mathf.Clamp(result.z,.45f,1.6f));
        }
        public static void AnimatePerson(Transform visual, Vector2 velocity,float time,bool dash,bool brace)
        {
            if(visual)AnimatePerson(visual.GetComponent<RushPersonParts>(),velocity,time,dash,brace);
        }
        public static void AnimatePerson(RushPersonParts p,Vector2 velocity,float time,bool dash,bool brace)
        {
            if(!p)return;
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

    // Keep generated front/back/side views readable while preserving bob, scale and launch spin.
    public class RushCharacterArt:MonoBehaviour
    {
        public Transform owner;public bool player,bike;
        SpriteRenderer rendererCache;
        string lastKey;
        void Awake(){rendererCache=GetComponent<SpriteRenderer>();}
        void LateUpdate()
        {
            if(!owner||!rendererCache)return;
            Vector3 direction=owner.forward;
            if(player)
            {
                string view=Mathf.Abs(direction.x)>.65f?(direction.x<0?"left":"right"):direction.z<0?"front":"back";
                string key="player-"+(bike?"bike-":"walk-")+view;
                if(lastKey!=key){RushArt.SetSprite(rendererCache,key);lastKey=key;}
            }
            // Billboard yaw is independent of actor direction; launch pitch remains a screen-plane spin.
            float tumble=Mathf.DeltaAngle(0,owner.eulerAngles.x);
            transform.rotation=Quaternion.Euler(58.736f,0,0)*Quaternion.Euler(0,0,tumble);
        }
    }

    public class RushPersonParts:MonoBehaviour
    {
        public Transform rig;public Transform[] legs,arms;public bool bike;
    }
}
