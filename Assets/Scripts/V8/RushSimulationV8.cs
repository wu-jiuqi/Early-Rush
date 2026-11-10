using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush.V8
{
    public enum RunPhase { Ready, Playing, Paused, Results }
    public enum TaskKind { CrowdPurchase, PushRoute, ClearAndPark, Chairs }
    public sealed class PushBody
    {
        public int id,task,checkpoint;
        public Vector2 position,home,safe,velocity,target;
        public Vector2[] checkpoints;
        public float radius=.6f,stable,pushed,stuck;
        public bool done,touched;
        public string art;
    }
    public sealed class Commuter
    {
        public int id,task=-1;
        public Vector2 position,home,velocity;
        public float radius=.48f,pushed,phase,cooldown;
        public bool credited;
        public string art="people/man";
    }
    public sealed class Errand
    {
        public int index;
        public TaskKind kind;
        public string title,icon,help;
        public Vector2 station;
        public int needed=1;
        public float stable;
        public bool done;
        public readonly HashSet<int> people=new HashSet<int>();
        public readonly List<PushBody> bodies=new List<PushBody>();
    }
    public sealed class RunV8
    {
        public const float PlayerRadius=.46f;
        public MapData map;
        public RunPhase phase=RunPhase.Ready;
        public int stage,seed,bumps,stars;
        public double elapsed,limit=420;
        public Vector2 player,velocity,facing=Vector2.up;
        public float energy=1,boost,impact,clock,eventAge;
        public bool success,muted;
        public string eventKind="";
        public readonly List<Errand> errands=new List<Errand>();
        public readonly List<Commuter> crowd=new List<Commuter>();
        public readonly List<PushBody> bodies=new List<PushBody>();
        public readonly List<Rect> solids=new List<Rect>();
        int eventIndex;
        public int Completed {get{int n=0;foreach(var t in errands)if(t.done)n++;return n;}}
        public static int Score(bool onTime,int tasks) {return (onTime?1:0)+(tasks>=1?1:0)+(tasks>=3?1:0);}
        public float Remaining => Mathf.Max(0,(float)(limit-elapsed));
        public bool EventActive => eventKind!=""&&eventAge>=2&&eventAge<25;
        public Action<string> onCue;
        public RunV8(int stage,int seed)
        {
            this.stage=stage;this.seed=seed;map=RushMapV8.Create(stage,seed);player=map.spawn;
            Setup();
        }
        void Setup()
        {
            string[][] names={new[]{"挤进咖啡店","推回卸货车","移开折叠路障"},new[]{"把背包推到出口","归位导流栏","整理车厢行李车"},new[]{"挤进打印区","送回文件车","推齐会议椅"}};
            string[][] icons={new[]{"legacy-extras/coffee","office/parcel-cart","street/barrier"},new[]{"tasks/backpack","street/rail","office/parcel-cart"},new[]{"stalls/printer","office/parcel-cart","tasks/chair"}};
            for(int i=0;i<3;i++)
            {
                var site=map.tasks[i];var t=new Errand{index=i,title=names[stage][i],icon=icons[stage][i],station=site.target};
                t.kind=i==0&&stage!=1?TaskKind.CrowdPurchase:stage==2&&i==2?TaskKind.Chairs:i==2?TaskKind.ClearAndPark:TaskKind.PushRoute;
                t.help=t.kind==TaskKind.CrowdPurchase?"挤开两位不同的人，再进入发光区":t.kind==TaskKind.Chairs?"把三把椅子分别推到同色位置":"持续碰撞推动物品，沿标记送到接收框";
                errands.Add(t);
                if(t.kind==TaskKind.CrowdPurchase)
                {
                    for(int j=0;j<5;j++)AddCrowd(site.target+new Vector2((j%3-1)*1.05f,-2.8f-(j/3)*1.1f),i);
                }
                else
                {
                    int count=t.kind==TaskKind.Chairs?3:1;t.needed=count;
                    for(int j=0;j<count;j++)
                    {
                        Vector2 offset=t.kind==TaskKind.Chairs?new Vector2((j-1)*2.8f,0):Vector2.zero;
                        var b=new PushBody{id=bodies.Count,task=i,position=site.start+offset,home=site.start+offset,safe=site.start+offset,target=site.target+offset,
                            art=icons[stage][i],radius=t.kind==TaskKind.Chairs?.55f:stage==1&&i==0?.45f:.65f,
                            checkpoints=t.kind==TaskKind.Chairs?new Vector2[0]:site.checkpoints};
                        bodies.Add(b);t.bodies.Add(b);
                    }
                }
            }
            var random=new System.Random(seed+stage*359);
            // Deterministic crossing streams away from the first learning stretch.
            for(int i=1;i<map.route.Count;i++)
            {
                Vector2 a=map.route[i-1],b=map.route[i];float len=Vector2.Distance(a,b);Vector2 d=(b-a).normalized,n=new Vector2(-d.y,d.x);
                for(float s=12;s<len-7;s+=15)
                {
                    int count=2+random.Next(3);
                    for(int j=0;j<count;j++)AddCrowd(a+d*s+n*(j-(count-1)*.5f)*1.3f,-1);
                }
            }
        }
        void AddCrowd(Vector2 p,int task)
        {
            if(!map.IsWalkable(p,.55f))return;
            crowd.Add(new Commuter{id=crowd.Count,position=p,home=p,task=task,phase=crowd.Count*.83f,art=crowd.Count%3==0?"people/woman":crowd.Count%3==1?"people/man":"people/elder"});
        }
        public void Begin(){if(phase==RunPhase.Ready)phase=RunPhase.Playing;}
        public void Pause(){if(phase==RunPhase.Playing){phase=RunPhase.Paused;velocity=Vector2.zero;boost=0;}}
        public void Resume(){if(phase==RunPhase.Paused){phase=RunPhase.Playing;velocity=Vector2.zero;}}
        public void Rescue(int task)
        {
            if(phase!=RunPhase.Playing||task<0||task>=3)return;
            foreach(var b in errands[task].bodies)if(!b.done){b.position=b.safe;b.velocity=Vector2.zero;b.stable=0;b.stuck=0;}
            onCue?.Invoke("rescue");
        }
        public Vector2 Resolve(Vector2 from,Vector2 delta,float radius)
        {
            Vector2 p=map.ResolveMove(from,delta,radius);
            foreach(var r in solids)
            {
                Vector2 q=new Vector2(Mathf.Clamp(p.x,r.xMin,r.xMax),Mathf.Clamp(p.y,r.yMin,r.yMax));Vector2 d=p-q;
                if(d.sqrMagnitude>=radius*radius)continue;
                if(d.sqrMagnitude>.000001f)p=q+d.normalized*(radius+.005f);
                else {Vector2 c=r.center;Vector2 v=p-c;if(Mathf.Abs(v.x)/(r.width+.001f)>Mathf.Abs(v.y)/(r.height+.001f))p.x=v.x>0?r.xMax+radius:r.xMin-radius;else p.y=v.y>0?r.yMax+radius:r.yMin-radius;}
            }
            return map.IsWalkable(p,radius)?p:from;
        }
        public void Tick(float dt,Vector2 input,bool dash,bool brace)
        {
            if(phase!=RunPhase.Playing||dt<=0)return;
            // Consume all supplied time in bounded physics steps; never silently truncate a long frame.
            float left=dt;bool pressed=dash;
            while(left>0&&phase==RunPhase.Playing)
            {
                float step=Mathf.Min(left,1f/60f);Step(step,Vector2.ClampMagnitude(input,1),pressed,brace);pressed=false;left-=step;
            }
        }
        void Step(float dt,Vector2 input,bool dash,bool brace)
        {
            // A deadline event is processed after this step's tasks and exit.
            dt=Mathf.Min(dt,Mathf.Max(0,(float)(limit-elapsed)));
            if(dt<=0){Finish(elapsed<=limit&&Vector2.Distance(player,map.exit)<1.3f);return;}
            elapsed+=dt;clock+=dt;impact=Mathf.MoveTowards(impact,0,dt*3);energy=Mathf.Min(1,energy+dt*.24f);
            if(input.sqrMagnitude>.01f)facing=input.normalized;
            if(dash&&energy>=.55f){energy-=.55f;boost=.32f;onCue?.Invoke("dash");}
            boost=Mathf.Max(0,boost-dt);
            float speed=brace?2.2f:stage==0?4.4f:3.6f;if(boost>0)speed*=1.8f;
            velocity=Vector2.MoveTowards(velocity,input*speed,dt*(stage==0?13:23));
            Vector2 before=player;Vector2 delta=velocity*dt;
            int steps=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/.12f));
            for(int k=0;k<steps;k++)
            {
                Vector2 next=Resolve(player,delta/steps,PlayerRadius);
                // Soft people yield. No damage, knockdown, forced pause or score penalty.
                foreach(var c in crowd)
                {
                    Vector2 d=c.position-next;float min=c.radius+PlayerRadius;
                    if(d.sqrMagnitude>=min*min)continue;
                    Vector2 n=d.sqrMagnitude>.00001f?d.normalized:facing;
                    Vector2 old=c.position;c.position=Resolve(c.position,n*(min-d.magnitude+.015f),c.radius);
                    float moved=Vector2.Distance(old,c.position);c.pushed+=moved;c.velocity+=n*(boost>0?1.1f:.3f);
                    if(moved>.003f&&c.cooldown<=0){bumps++;energy=Mathf.Min(1,energy+.035f);c.cooldown=.8f;impact=.4f;onCue?.Invoke("bump");}
                    if(c.task>=0&&c.pushed>.35f&&!c.credited){c.credited=true;errands[c.task].people.Add(c.id);}
                }
                foreach(var b in bodies)
                {
                    if(b.done)continue;Vector2 d=b.position-next;float min=b.radius+PlayerRadius;
                    if(d.sqrMagnitude>=min*min)continue;
                    Vector2 n=d.sqrMagnitude>.00001f?d.normalized:facing;Vector2 old=b.position;
                    b.position=Resolve(b.position,n*(min-d.magnitude+.01f),b.radius);
                    float moved=Vector2.Distance(old,b.position);
                    if(moved>.002f){b.touched=true;b.pushed+=moved;b.velocity+=n*.09f;impact=.25f;}
                    if(Vector2.Distance(b.position,next)<min-.04f)next=player; // A solid cannot be walked through, but tangential correction remains possible.
                }
                player=next;
            }
            foreach(var c in crowd)
            {
                c.cooldown=Mathf.Max(0,c.cooldown-dt);Vector2 roam=new Vector2(Mathf.Sin(clock*.55f+c.phase),Mathf.Cos(clock*.4f+c.phase))*.5f;
                Vector2 desired=c.home+roam;
                Vector2 motion=Vector2.ClampMagnitude(desired-c.position,c.task>=0?.18f:.65f)*dt+c.velocity*dt;
                if(Vector2.Distance(c.position,player)<1.2f)motion=c.velocity*dt;
                c.position=Resolve(c.position,motion,c.radius);c.velocity=Vector2.MoveTowards(c.velocity,Vector2.zero,dt*5);
            }
            foreach(var b in bodies)
            {
                if(b.done)continue;
                float slip=stage==0&&EventActive?1.1f:1;
                b.position=Resolve(b.position,b.velocity*dt*slip,b.radius);b.velocity=Vector2.MoveTowards(b.velocity,Vector2.zero,dt*4);
                if(b.touched&&b.checkpoints!=null&&b.checkpoint<b.checkpoints.Length&&Vector2.Distance(b.position,b.checkpoints[b.checkpoint])<2.3f)
                {b.safe=b.checkpoints[b.checkpoint];b.checkpoint++;onCue?.Invoke("checkpoint");}
                bool eligible=b.touched&&b.pushed>.3f&&(b.checkpoints==null||b.checkpoint>=b.checkpoints.Length);
                b.stable=eligible&&Vector2.Distance(b.position,b.target)<1.05f?b.stable+dt:0;
                if(b.stable>=.4f){b.done=true;b.velocity=Vector2.zero;b.position=b.target;onCue?.Invoke("park");}
            }
            foreach(var t in errands)
            {
                if(t.done)continue;
                if(t.kind==TaskKind.CrowdPurchase)
                {
                    t.stable=t.people.Count>=2&&Vector2.Distance(player,t.station)<1.5f?t.stable+dt:0;
                    if(t.stable>=.4f)t.done=true;
                }
                else {bool all=true;foreach(var b in t.bodies)if(!b.done)all=false;t.done=all;}
                if(t.done)onCue?.Invoke("task");
            }
            UpdateEvents(dt);
            if(Vector2.Distance(player,map.exit)<1.3f)Finish(true);
            else if(elapsed>=limit-0.00001)Finish(false);
        }
        void UpdateEvents(float dt)
        {
            if(eventKind!=""){eventAge+=dt;if(eventAge>=25){eventKind="";eventAge=0;}return;}
            double trigger=eventIndex==0?55:140;
            if(eventIndex<2&&elapsed>=trigger&&Remaining>45)
            {
                bool safe=true;foreach(var t in errands)if(Vector2.Distance(t.station,player)<8)safe=false;
                if(!safe)return;
                eventKind=stage==0?(eventIndex==0?"rain":"delivery"):stage==1?(eventIndex==0?"arrival":"cleaning"):(eventIndex==0?"meeting":"cleaning");
                eventAge=0;eventIndex++;onCue?.Invoke("event");
                if(eventKind!="rain")for(int i=0;i<8;i++)AddCrowd(player+new Vector2((i%4-1.5f)*1.2f,5+i/4),-1);
            }
        }
        public void Finish(bool onTime)
        {
            if(phase!=RunPhase.Playing)return;
            success=onTime&&elapsed<=limit+.00001;stars=Score(success,Completed);phase=RunPhase.Results;velocity=Vector2.zero;boost=0;eventKind="";onCue?.Invoke(success?"win":"late");
        }
    }
}
