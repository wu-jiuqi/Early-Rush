using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush
{
    // Tiny authored meshes share the existing palette/materials. The owner advances this
    // clock explicitly; neither pause nor hit-stop can leave autonomous particle playback.
    public sealed class RushImpactFx
    {
        sealed class Burst
        {
            public Transform root, ring;
            public Transform[] stars;
            public float age, duration, strength;
        }
        readonly Transform owner;
        readonly List<Burst> bursts=new List<Burst>();
        public float Clock { get; private set; }
        public int ActiveCount => bursts.Count;
        public RushImpactFx(Transform owner){this.owner=owner;}
        public void Emit(Vector3 position,Vector2 direction,bool strong)
        {
            if(!owner)return;
            if(bursts.Count>=14){Object.Destroy(bursts[0].root.gameObject);bursts.RemoveAt(0);}
            var b=new Burst{duration=strong?.36f:.26f,strength=strong?1f:.65f,stars=new Transform[strong?5:3]};
            b.root=new GameObject(strong?"Impact flash and stars":"Landing puff").transform;
            b.root.SetParent(owner,false);b.root.position=position;
            b.ring=RushVisuals.Ring(b.root,"Shock ring",Vector3.zero,.3f,.025f,new Color(1f,.83f,.32f));
            for(int i=0;i<b.stars.Length;i++)
            {
                var star=new GameObject("Impact star").transform;star.SetParent(b.root,false);
                RushVisuals.Box(star,"Star ray",Vector3.zero,new Vector3(.11f,.40f,.09f),RushVisuals.Cream,.035f);
                RushVisuals.Box(star,"Star ray",Vector3.zero,new Vector3(.36f,.11f,.09f),RushVisuals.Cream,.035f);
                star.localRotation=Quaternion.Euler(0,Mathf.Atan2(direction.x,direction.y)*Mathf.Rad2Deg,25+i*31);
                b.stars[i]=star;
            }
            bursts.Add(b);Pose(b);
        }
        static void Pose(Burst b)
        {
            float t=b.age/b.duration;
            float radius=Mathf.Lerp(.3f,1.6f,t)*b.strength;
            b.ring.localScale=new Vector3(radius,.20f*(1-t),radius);
            for(int i=0;i<b.stars.Length;i++)
            {
                float angle=(i/(float)b.stars.Length)*Mathf.PI*2;
                float spread=(.18f+t*1.35f)*b.strength;
                b.stars[i].localPosition=new Vector3(Mathf.Cos(angle)*spread,.85f+Mathf.Sin(t*Mathf.PI)*.6f,Mathf.Sin(angle)*spread);
                b.stars[i].localScale=Vector3.one*(1-t)*b.strength;
                b.stars[i].localRotation=Quaternion.Euler(12,0,25+i*31+t*140);
            }
        }
        public void Tick(float dt)
        {
            if(dt<=0)return;Clock+=dt;
            for(int i=bursts.Count-1;i>=0;i--)
            {
                var b=bursts[i];b.age+=dt;
                if(b.age>=b.duration){if(b.root)Object.Destroy(b.root.gameObject);bursts.RemoveAt(i);}
                else Pose(b);
            }
        }
        public void Clear()
        {
            foreach(var b in bursts)if(b.root)Object.Destroy(b.root.gameObject);
            bursts.Clear();Clock=0;
        }
    }
}
