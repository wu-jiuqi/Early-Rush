using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush.V9
{
    public enum CrowdFlow { Forward, Counter, Crossing, Waiting }
    public sealed partial class ConfigV9
    {
        public float crowdPeakSpacing=2.6f,crowdCalmSpacing=4.1f,crowdMetroPeakSpacing=2.25f;
    }
    public sealed partial class PersonV9
    {
        public int appearance;
        public CrowdFlow flow;
        internal Vector2[] crowdRoute;
        internal int crowdWaypoint;
        internal float crowdSpeed;
        internal Vector2 roadDirection;
    }
    public sealed partial class RunV9
    {
        readonly Dictionary<Vector2Int,List<PersonV9>> crowdBuckets=new Dictionary<Vector2Int,List<PersonV9>>();
        readonly List<Rect> crowdReservations=new List<Rect>();
        public int VisibleCrowdCount(Vector2 center,float halfWidth=13.5f,float halfHeight=8)
        {int n=0;foreach(var c in people)if(!c.exited&&Mathf.Abs(c.position.x-center.x)<halfWidth&&Mathf.Abs(c.position.y-center.y)<halfHeight)n++;return n;}

        internal bool IsCrowdProtected(Vector2 p,float margin=0)
        {
            foreach(var r in crowdReservations)if(p.x>r.xMin-margin&&p.x<r.xMax+margin&&p.y>r.yMin-margin&&p.y<r.yMax+margin)return true;
            foreach(var b in bodies)if(Vector2.Distance(p,b.position)<b.radius+1.5f+margin)return true;
            return false;
        }
        void ReserveCrowd(Vector2 p,float radius)=>crowdReservations.Add(new Rect(p.x-radius,p.y-radius,radius*2,radius*2));
        void SetupCrowd()
        {
            foreach(float spacing in new[]{config.crowdPeakSpacing,config.crowdCalmSpacing,config.crowdMetroPeakSpacing})
                if(!Positive(spacing)||spacing<1.5f)throw new ArgumentException("Crowd spacing must be finite and at least 1.5 units");
            ReserveCrowd(map.spawn,7);ReserveCrowd(map.exit,8);
            foreach(var t in map.tasks)
            {
                ReserveCrowd(t.center,8);ReserveCrowd(t.start,4);ReserveCrowd(t.target,4);
                ReserveCrowd(t.entrance,3);ReserveCrowd(t.rejoin,3);
                foreach(var p in t.checkpoints)ReserveCrowd(p,3);
            }
            foreach(var f in fixtures)ReserveCrowd(f.position,f.kind==FixtureKind.FileDesk?3:2);
            if(stage==0)ReserveCrowd(map.signal+Vector2.up*2.5f,8);
            if(stage==1){ReserveCrowd(map.platform,12);ReserveCrowd(map.trainDoor,10);}
            var random=new System.Random(seed^0x51c4);
            // Each straight block has a continuous local circulation loop. No teleport recycling;
            // corners and protected task crossings separate blocks, while bypasses stay open.
            for(int segment=1;segment<map.route.Count;segment++)
            {
                Vector2 a=map.route[segment-1],d=map.route[segment]-a;float length=d.magnitude;
                Vector2 tangent=d.normalized,normal=new Vector2(tangent.y,-tangent.x);
                float stripStart=-1;
                for(float s=4;s<=length-3;s+=1)
                {
                    Vector2 p=a+tangent*s;
                    bool safe=s<length-4&&!IsCrowdProtected(p,1.9f)&&map.IsWalkable(p+normal*1.15f,.6f)&&map.IsWalkable(p-normal*1.15f,.6f);
                    if(safe&&stripStart<0)stripStart=s;
                    if(!safe&&stripStart>=0)
                    {
                        float end=s-1;
                        if(end-stripStart>=9)
                        {
                            Vector2 start=a+tangent*stripStart,finish=a+tangent*end;
                            int module=NearestCrowdModule((start+finish)*.5f);
                            bool peak=module%2==1;
                            float spacing=peak?(stage==1?config.crowdMetroPeakSpacing:config.crowdPeakSpacing):config.crowdCalmSpacing;
                            var loop=new[]{start+normal*1.1f,finish+normal*1.1f,finish-normal*1.1f,start-normal*1.1f};
                            SpawnCrowdLoop(loop,tangent,spacing,random,false);
                            // A few short transverse circuits in wide peak sections, clear of junctions.
                            Vector2 mid=(start+finish)*.5f;
                            if(peak&&end-stripStart>22&&map.IsWalkable(mid+normal*3.7f,1)&&map.IsWalkable(mid-normal*3.7f,1))
                            {
                                var cross=new[]{mid-normal*3.3f-tangent*.6f,mid+normal*3.3f-tangent*.6f,mid+normal*3.3f+tangent*.6f,mid-normal*3.3f+tangent*.6f};
                                SpawnCrowdLoop(cross,tangent,5.3f,random,true);
                            }
                            // Edge waiters add busyness only when an additional usable lane exists.
                            if(peak)for(float offset=4;offset<end-stripStart-3;offset+=8)
                            {
                                Vector2 edge=start+tangent*offset+normal*3.6f;
                                if(!IsCrowdProtected(edge,1)&&map.IsWalkable(edge,1.1f))
                                {var c=SpawnCrowdPerson(edge,random);c.flow=CrowdFlow.Waiting;c.canRush=false;}
                            }
                        }
                        stripStart=-1;
                    }
                }
            }
        }
        int NearestCrowdModule(Vector2 p)
        {int index=0;float best=float.MaxValue;foreach(var m in map.modules){float dist=(p-(m.entrance+m.exit)*.5f).sqrMagnitude;if(dist<best){best=dist;index=m.index;}}return index;}
        PersonV9 SpawnCrowdPerson(Vector2 p,System.Random random)
        {
            int id=3000+people.Count;
            var c=new PersonV9{id=id,position=p,home=p,target=p,radius=.42f,appearance=random.Next(12),
                kind=stage==2&&people.Count%7==4?PersonKind.Coffee:PersonKind.Ordinary,
                crowdSpeed=1.05f+(float)random.NextDouble()*.4f};
            c.art=c.kind==PersonKind.Coffee?"people/woman":new[]{"people/man","people/woman","people/commuter"}[c.appearance%3];
            c.canRush=c.kind==PersonKind.Ordinary&&people.Count%8==2;people.Add(c);return c;
        }
        void SpawnCrowdLoop(Vector2[] loop,Vector2 tangent,float spacing,System.Random random,bool crossing)
        {
            float perimeter=0;for(int i=0;i<loop.Length;i++)perimeter+=Vector2.Distance(loop[i],loop[(i+1)%loop.Length]);
            int count=Mathf.Max(2,Mathf.FloorToInt(perimeter/spacing));
            for(int i=0;i<count;i++)
            {
                float distance=(i+.3f+((float)random.NextDouble()-.5f)*.35f)*perimeter/count;int side=0;
                while(side<loop.Length-1&&distance>Vector2.Distance(loop[side],loop[(side+1)%loop.Length]))
                {distance-=Vector2.Distance(loop[side],loop[(side+1)%loop.Length]);side++;}
                Vector2 direction=(loop[(side+1)%loop.Length]-loop[side]).normalized,p=loop[side]+direction*distance;
                if(IsCrowdProtected(p,.6f)||!map.IsWalkable(p,.6f))continue;
                var c=SpawnCrowdPerson(p,random);c.crowdRoute=loop;c.crowdWaypoint=(side+1)%loop.Length;c.roadDirection=tangent;
                c.flow=crossing?CrowdFlow.Crossing:Vector2.Dot(direction,tangent)>=0?CrowdFlow.Forward:CrowdFlow.Counter;
                if(crossing)c.canRush=false;
            }
        }
        internal Vector2 MoveCrowdPerson(PersonV9 c,Vector2 delta)
        {
            Vector2 next=Resolve(c.position,delta,c.radius);
            // People never transfer impulses to task props. Entering a task's working space is blocked.
            if(IsCrowdProtected(next,c.radius)&&!IsCrowdProtected(c.position,c.radius))return c.position;
            foreach(var b in bodies)if(Vector2.Distance(next,b.position)<c.radius+b.radius+.25f&&Vector2.Distance(next,b.position)<Vector2.Distance(c.position,b.position))return c.position;
            foreach(var f in fixtures)if(Vector2.Distance(next,f.position)<c.radius+f.radius+.2f&&Vector2.Distance(next,f.position)<Vector2.Distance(c.position,f.position))return c.position;
            return next;
        }
        static Vector2Int CrowdCell(Vector2 p)=>new Vector2Int(Mathf.FloorToInt(p.x/2),Mathf.FloorToInt(p.y/2));
        void UpdateCrowdPeople(float dt)
        {
            foreach(var bucket in crowdBuckets.Values)bucket.Clear();
            foreach(var c in people)if(!c.exited)
            {var key=CrowdCell(c.position);if(!crowdBuckets.TryGetValue(key,out var bucket)){bucket=new List<PersonV9>(8);crowdBuckets.Add(key,bucket);}bucket.Add(c);}
            foreach(var c in people)
            {
                if(c.exited)continue;
                if(c.kind==PersonKind.Alighting)
                {
                    Vector2 old=c.position;c.position=Resolve(c.position,Vector2.ClampMagnitude(c.target-c.position,1.6f)*dt,c.radius);c.velocity=(c.position-old)/dt;
                    if(Vector2.Distance(c.position,player)<c.radius+PlayerRadius)Contact(c.id);
                    if(Vector2.Distance(c.position,c.target)<.3f)c.exited=true;
                    continue;
                }
                if(StepNpcAction(c,dt))continue;
                Vector2 target=c.home;
                if(c.crowdRoute!=null)
                {
                    target=c.crowdRoute[c.crowdWaypoint];
                    if(Vector2.Distance(c.position,target)<.25f){c.crowdWaypoint=(c.crowdWaypoint+1)%c.crowdRoute.Length;target=c.crowdRoute[c.crowdWaypoint];}
                    if(c.flow!=CrowdFlow.Crossing)c.flow=Vector2.Dot(target-c.position,c.roadDirection)>=0?CrowdFlow.Forward:CrowdFlow.Counter;
                    // Track a short point ahead with a small personal lateral drift. The physical
                    // footprint follows the same path as the sprite; endpoint taper keeps turns clear.
                    Vector2 legStart=c.crowdRoute[(c.crowdWaypoint+c.crowdRoute.Length-1)%c.crowdRoute.Length];
                    Vector2 leg=target-legStart,heading=leg.normalized;
                    float along=Mathf.Clamp(Vector2.Dot(c.position-legStart,heading)+1.75f,0,leg.magnitude);
                    float taper=Mathf.Clamp01(Mathf.Min(along,leg.magnitude-along)/1.5f);
                    float drift=Mathf.Sin((float)elapsed*.65f+c.id*.63f)*.28f*taper;
                    target=legStart+heading*along+new Vector2(heading.y,-heading.x)*drift;
                }
                Vector2 desired=Vector2.ClampMagnitude(target-c.position,c.crowdSpeed),separation=Vector2.zero;
                var cell=CrowdCell(c.position);
                for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)if(crowdBuckets.TryGetValue(cell+new Vector2Int(x,y),out var neighbors))
                    foreach(var other in neighbors)
                    {
                        if(other==c||other.exited)continue;Vector2 away=c.position-other.position;float distance=away.magnitude;
                        if(distance<1.05f&&distance>.001f)separation+=away/distance*(1.05f-distance)*2.5f;
                    }
                Vector2 fromPlayer=c.position-player;float gap=fromPlayer.magnitude;
                if(gap<1.15f&&gap>.001f)separation+=fromPlayer/gap*(1.15f-gap)*2;
                Vector2 before=c.position;c.position=MoveCrowdPerson(c,Vector2.ClampMagnitude(desired+separation,1.8f)*dt);c.velocity=(c.position-before)/dt;
                if(Vector2.Distance(c.position,player)<c.radius+PlayerRadius){Contact(c.id);HandleNpcActionContact(c);}
            }
        }
    }
}
