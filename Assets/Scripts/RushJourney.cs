using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush
{
    public partial class RushGame
    {
        sealed class JourneyEvent
        {
            public int id,phase,activations,hits,blocks,createdFrame;
            public string kind,title,hint;
            public Rect area;
            public Vector2 direction;
            public float duration,left,cooldown,scatterAge;
            public bool broken;
            public Transform root,arrows;
        }
        public struct JourneyProbe
        {
            public int id,phase,activations,hits,blocks;
            public string kind;
            public Rect area;
            public Vector2 direction;
            public float warningRemaining,warningDuration,cooldownRemaining;
            public bool broken;
        }
        readonly List<JourneyEvent> journeyEvents=new List<JourneyEvent>();
        readonly Queue<int> journeyBursts=new Queue<int>();
        readonly List<int> journeyAlighting=new List<int>();
        public int JourneyAlightingCount=>journeyAlighting.Count;
        public bool JourneyAlightingReleased {get;private set;}
        public bool IsJourneyAlighting(int slot)=>journeyAlighting.Contains(slot);
        int journeySerial;
        float clockHold;
        bool journeyClocked;
        public int JourneyWarnings {get;private set;}
        public int JourneyHits {get;private set;}
        public int JourneyBlocks {get;private set;}
        public int JourneyBreaks {get;private set;}
        public int JourneyCompleted {get;private set;}
        public int JourneyClockIns {get;private set;}
        public int JourneyCrowdLaunches {get;private set;}
        public float ClockHold01=>Mathf.Clamp01(clockHold/.8f);
        public bool JourneyWarning {get {foreach(var e in journeyEvents)if(e.phase==1)return true;return false;}}
        public float JourneyWarningRemaining {get {foreach(var e in journeyEvents)if(e.phase==1)return e.left;return 0;}}
        JourneyEvent NextJourney
        {
            get
            {
                foreach(var e in journeyEvents)if(e.phase==1)return e;
                JourneyEvent next=null;
                foreach(var e in journeyEvents)if(!e.broken&&e.area.yMax>=position.y-1&&(next==null||e.area.yMin<next.area.yMin))next=e;
                return next;
            }
        }
        public string JourneyTitle=>JourneyWarning?NextJourney.title:Stage==2&&GoalDistance<10?"站稳打卡":Stage==1&&StageProgress01<.57f?"先下后上":NextJourney!=null?NextJourney.title:"继续赶路";
        public string JourneyHint=>JourneyWarning?NextJourney.hint:Stage==2&&GoalDistance<10?"进入打卡垫，按住 E 0.8 秒（自动刹停）":Stage==1&&StageProgress01<.57f?"下车乘客逆向走来；从空档穿插，别堵在车门正中":NextJourney!=null?NextJourney.hint:"沿路线前往青色目的地";
        public Vector2 JourneyTarget=>Stage==2&&GoalDistance<10?new Vector2(world.goal.x,world.goal.z):NextJourney!=null?NextJourney.area.center:new Vector2(world.goal.x,world.goal.z);
        public JourneyProbe[] JourneyProbeSnapshot()
        {
            var result=new JourneyProbe[journeyEvents.Count];
            for(int i=0;i<result.Length;i++){var e=journeyEvents[i];result[i]=new JourneyProbe{id=e.id,phase=e.phase,activations=e.activations,hits=e.hits,blocks=e.blocks,
                kind=e.kind,area=e.area,direction=e.direction,warningRemaining=e.left,warningDuration=e.duration,cooldownRemaining=e.cooldown,broken=e.broken};}
            return result;
        }
        void ResetJourneyRun()
        {
            JourneyWarnings=JourneyHits=JourneyBlocks=JourneyBreaks=JourneyCompleted=JourneyClockIns=JourneyCrowdLaunches=0;
            ErrandCompleted=0;ErrandRewardSeconds=0;
            journeySerial=0;clockHold=0;journeyClocked=false;journeyAlighting.Clear();JourneyAlightingReleased=false;
        }
        float JourneyZ(float fraction)=>world.spawn.z+(world.goal.z-world.spawn.z)*fraction;
        void BuildJourney()
        {
            journeyEvents.Clear();journeyBursts.Clear();clockHold=0;journeyClocked=false;journeyAlighting.Clear();JourneyAlightingReleased=false;
            BuildDailyCommute();
            if(Stage==0)
            {
                AddJourney("road-crossing","横穿路口","看箭头：绕开橙色区域，或按 Shift 稳住",new Rect(-9,JourneyZ(.20f)-2.5f,18,5),Vector2.right,1.2f);
                AddJourney("road-crossing","横穿路口","横向人流将通过；两侧可绕，Shift 可抗冲击",new Rect(-9,166f-2.5f,18,5),Vector2.left,1.2f);
                BuildParcelShortcut();
            }
            else if(Stage==1)
            {
                PrepareJourneyAlighting();
                AddJourney("emergency-brake","列车急刹","后段橙区即将急刹：按 Shift，或退到两侧安全边缘",new Rect(-8,JourneyZ(.70f),16,JourneyZ(.96f)-JourneyZ(.70f)),Vector2.down,1.3f);
            }
            else
            {
                AddJourney("cart-wave","侧门同事涌出","侧门有人冲出；看横向箭头，避开橙区或按 Shift",new Rect(-9,JourneyZ(.20f)-2.5f,18,5),Vector2.right,1.2f);
                AddJourney("cart-wave","侧门同事涌出","侧门人流将横穿：看箭头绕行，或按 Shift 稳住",new Rect(-9,138f-2.5f,18,5),Vector2.left,1.2f);
            }
        }
        void PrepareJourneyAlighting()
        {
            if(impactQaMode||combatQaMode)return;
            for(int i=0;i<world.crowd.Count;i++)
            {
                var spawn=world.crowd[i];var path=spawn.route;
                if(path==null||path.Length<2||Mathf.Abs(path[0].y-94f)>.01f||Mathf.Abs(path[1].y-82f)>.01f||Mathf.Abs(path[0].x)>.7f)continue;
                journeyAlighting.Add(i);spawn.delay=60f;world.crowd[i]=spawn;
            }
        }
        void TickJourneyAlighting()
        {
            if(Stage!=1||JourneyAlightingReleased||journeyAlighting.Count==0)return;
            bool release=position.y>=78f||StageTime>=60f;
            for(int rank=0;rank<journeyAlighting.Count;rank++)
            {
                int slot=journeyAlighting[rank];var c=crowds[slot];
                // Delay navigation only: ordinary separation, impacts and recovery stay live.
                c.startAt=release?StageTime+(rank%6)*.12f:Mathf.Max(c.startAt,StageTime+.5f);
                if(slot<combat.Count)
                {
                    if(!release)CancelNpcCombat(slot,.5f);
                    combat[slot].cooldownLeft=Mathf.Max(combat[slot].cooldownLeft,release?.9f:.5f);
                }
            }
            if(release)JourneyAlightingReleased=true;
        }
        JourneyEvent AddJourney(string kind,string title,string hint,Rect area,Vector2 direction,float duration)
        {
            var e=new JourneyEvent{id=Stage*10+journeyEvents.Count,kind=kind,title=title,hint=hint,area=area,direction=direction,duration=duration};
            e.root=new GameObject("Journey "+kind).transform;e.root.SetParent(world.root,false);
            e.root.localPosition=new Vector3(area.center.x,.20f,area.center.y);
            RushVisuals.Box(e.root,"Exact player-centre danger area",Vector3.zero,new Vector3(area.width,.025f,area.height),new Color(.98f,.57f,.16f),.005f);
            for(int s=-1;s<=1;s+=2)
            {
                RushVisuals.Box(e.root,"Area edge",new Vector3(s*area.width*.5f,.03f,0),new Vector3(.06f,.025f,area.height),RushVisuals.Cream,.005f);
                RushVisuals.Box(e.root,"Area edge",new Vector3(0,.03f,s*area.height*.5f),new Vector3(area.width,.025f,.06f),RushVisuals.Cream,.005f);
            }
            e.arrows=new GameObject("Locked impulse arrows").transform;e.arrows.SetParent(e.root,false);
            for(float z=-area.height*.5f+1;z<area.height*.5f;z+=3)
                for(float x=-area.width*.5f+1.5f;x<area.width*.5f;x+=3)
                {
                    var a=new GameObject("Direction").transform;a.SetParent(e.arrows,false);a.localPosition=new Vector3(x,.06f,z);
                    a.localRotation=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y));
                    RushVisuals.Box(a,"Shaft",new Vector3(0,0,-.15f),new Vector3(.14f,.03f,.8f),RushVisuals.Ink,.01f);
                    for(int s=-1;s<=1;s+=2){var h=RushVisuals.Box(a,"Head",new Vector3(s*.18f,0,.18f),new Vector3(.14f,.03f,.55f),RushVisuals.Ink,.01f);h.localRotation=Quaternion.Euler(0,s*-45,0);}
                }
            e.root.gameObject.SetActive(false);journeyEvents.Add(e);return e;
        }
        static bool JourneyInside(Rect r,Vector2 p)=>p.x>=r.xMin&&p.x<=r.xMax&&p.y>=r.yMin&&p.y<=r.yMax;
        static Rect JourneyGrow(Rect r,float margin)=>new Rect(r.xMin-margin,r.yMin-margin,r.width+margin*2,r.height+margin*2);
        Vector2 JourneyRoutePoint(float z)
        {
            var route=world.riskRoute;
            for(int i=1;i<route.Count;i++)
                if(z>=Mathf.Min(route[i-1].y,route[i].y)&&z<=Mathf.Max(route[i-1].y,route[i].y)&&Mathf.Abs(route[i].y-route[i-1].y)>.01f)
                    return Vector2.Lerp(route[i-1],route[i],Mathf.InverseLerp(route[i-1].y,route[i].y,z));
            return new Vector2(0,z);
        }
        void BuildParcelShortcut()
        {
            Rect area=default;bool found=false;CrowdSpawn[] detours=null;
            for(int k=0;k<25&&!found;k++)
            {
                Vector2 p=JourneyRoutePoint(JourneyZ(.45f)+(k%2==0?1:-1)*(k/2)*.5f);
                area=new Rect(p.x-1.7f,p.y-.4f,3.4f,.8f);Rect gap=JourneyGrow(area,.5f);
                found=true;foreach(Rect r in world.obstacles)if(gap.Overlaps(r)){found=false;break;}
                if(found)foreach(var c in world.crowd)if(JourneyInside(gap,c.position)){found=false;break;}
                if(found){world.obstacles.Add(area);found=JourneyParcelDetours(area,out detours);if(!found)world.obstacles.Remove(area);}
            }
            if(!found){Debug.LogWarning("Journey parcel placement has no clear gap");return;}
            var e=new JourneyEvent{id=Stage*10+journeyEvents.Count,kind="parcel-shortcut",title="纸箱近道",hint="Space 冲刺撞碎纸箱；也可从两边绕过",area=area};
            e.root=new GameObject("Breakable cardboard shortcut").transform;e.root.SetParent(world.root,false);e.root.localPosition=new Vector3(area.center.x,0,area.center.y);
            for(int i=0;i<5;i++)
            {
                var box=RushVisuals.Box(e.root,"Cardboard parcel",new Vector3((i-2)*.68f,.55f,0),new Vector3(.67f,1.1f,.8f),new Color(.74f,.49f,.24f),.06f);
                RushVisuals.Box(box,"Paper tape",new Vector3(0,.56f,0),new Vector3(.12f,.025f,.79f),RushVisuals.Cream,.01f);
            }
            for(int i=0;i<detours.Length;i++)world.crowd[i]=detours[i];journeyEvents.Add(e);
        }
        bool JourneyParcelDetours(Rect area,out CrowdSpawn[] updated)
        {
            var clearanceCache=new Dictionary<Vector4,bool>();
            bool Clear(Vector2 from,Vector2 to)
            {
                var key=new Vector4(from.x,from.y,to.x,to.y);
                if(!clearanceCache.TryGetValue(key,out bool clear))
                {clear=StaticCircleRouteClear(from,to,.4f);clearanceCache.Add(key,clear);}
                return clear;
            }
            // Authored commuters retain their spawn and destination but can pass an
            // unopened box. Only future intersecting segments gain local clear corners.
            updated=world.crowd.ToArray();Rect r=JourneyGrow(area,.65f);
            Vector2[] corners={new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),new Vector2(r.xMin,r.yMax),new Vector2(r.xMax,r.yMax)};
            for(int i=0;i<updated.Length;i++)
            {
                var spawn=updated[i];if(spawn.route==null)continue;var route=new List<Vector2>(spawn.route);
                for(int j=spawn.routeIndex;j<route.Count;j++)
                {
                    Vector2 from=j==spawn.routeIndex?spawn.position:route[j-1],to=route[j];
                    if(!JourneySegmentRect(from,to,JourneyGrow(area,.41f))||Clear(from,to))continue;
                    int first=-1,second=-1;float best=float.MaxValue;
                    for(int a=0;a<4;a++)for(int b=0;b<4;b++)
                    {
                        if(!Clear(from,corners[a])||!Clear(corners[a],corners[b])||!Clear(corners[b],to))continue;
                        float cost=Vector2.Distance(from,corners[a])+Vector2.Distance(corners[a],corners[b])+Vector2.Distance(corners[b],to);
                        if(cost<best){best=cost;first=a;second=b;}
                    }
                    if(first<0)return false;
                    route.Insert(j++,corners[first]);if(second!=first)route.Insert(j++,corners[second]);
                }
                spawn.route=route.ToArray();updated[i]=spawn;
            }
            return true;
        }
        static bool JourneySegmentRect(Vector2 from,Vector2 to,Rect r)
        {
            float enter=0,exit=1;Vector2 step=to-from;
            for(int axis=0;axis<2;axis++)
            {
                float p=axis==0?from.x:from.y,d=axis==0?step.x:step.y,min=axis==0?r.xMin:r.yMin,max=axis==0?r.xMax:r.yMax;
                if(Mathf.Abs(d)<.000001f){if(p<min||p>max)return false;continue;}
                float a=(min-p)/d,b=(max-p)/d;if(a>b){float t=a;a=b;b=t;}enter=Mathf.Max(enter,a);exit=Mathf.Min(exit,b);
                if(enter>exit)return false;
            }
            return true;
        }
        void TickParcel(JourneyEvent e,float dt)
        {
            if(!e.broken&&IsDashing&&!PlayerAirborne&&!PlayerRecovering&&JourneySegmentRect(position,position+facing*(Stage==0?13f:11.3f)*dt,JourneyGrow(e.area,Radius)))
            {
                e.broken=true;e.phase=2;world.obstacles.Remove(e.area);JourneyBreaks++;e.scatterAge=0;
                if(impactFx!=null)impactFx.Emit(new Vector3(e.area.center.x,.2f,e.area.center.y),facing,true);
                playerHitAge=0;playerJellyStrength=.3f;Sound(launchClip,.65f);shake=Mathf.Max(shake,.5f);
            }
            if(!e.broken||!e.root.gameObject.activeSelf)return;
            e.scatterAge+=dt;float t=e.scatterAge/.5f;
            for(int i=0;i<e.root.childCount;i++){var part=e.root.GetChild(i);part.localPosition=new Vector3((i-2)*.68f*(1+t),.55f+Mathf.Sin(t*Mathf.PI)*1.4f,(i%2==0?1:-1)*t);part.localRotation=Quaternion.Euler(t*150,(i-2)*t*60,t*70);}
            if(t>=1)e.root.gameObject.SetActive(false);
        }
        void FireJourney(JourneyEvent e)
        {
            e.phase=2;e.left=0;e.cooldown=7f;JourneyCompleted++;
            int attackId=-1000000-(++journeySerial);
            impactBursts[attackId]=new ImpactBurst();journeyBursts.Enqueue(attackId);
            while(journeyBursts.Count>8)impactBursts.Remove(journeyBursts.Dequeue());
            if(JourneyInside(e.area,position)&&!PlayerAirborne&&!PlayerRecovering&&playerProtectionLeft<=0)
            {
                playerLastContactAttack=attackId;
                if(IsBracing)
                {
                    JourneyBlocks++;e.blocks++;BraceBlocks++;braceSuccessLeft=.8f;playerProtectionLeft=.25f;
                    playerHitAge=0;playerJellyStrength=.18f;Sound(tickClip,.8f);ApplyPlayerCombatPresentation();
                }
                else {LaunchPlayer(e.direction,attackId);NpcToPlayerLaunches--;JourneyHits++;e.hits++;clockHold=0;}
            }
            int launched=0;
            for(int i=0;i<crowds.Count&&launched<6;i++)
                if(JourneyInside(e.area,crowds[i].position)&&LaunchCrowd(i,e.direction,attackId,2))
                {launched++;JourneyCrowdLaunches++;NpcToNpcLaunches--;ChainLaunches--;}
            Sound(bumpClip,.7f);
        }
        void TickJourney(float dt)
        {
            if(impactQaMode||combatQaMode||State!=RushState.Playing)return;
            TickJourneyAlighting();
            bool warning=JourneyWarning;
            foreach(var e in journeyEvents)
            {
                if(e.kind=="parcel-shortcut"){TickParcel(e,dt);continue;}
                e.cooldown=Mathf.Max(0,e.cooldown-dt);
                if(e.phase==2)
                {
                    if(e.cooldown<6.65f)e.root.gameObject.SetActive(false);
                    if(e.cooldown<=0)e.phase=0;
                }
                if(e.phase==1)
                {
                    if(e.createdFrame!=Time.frameCount)e.left=Mathf.Max(0,e.left-dt);
                    e.arrows.localPosition=Vector3.up*(.025f+.025f*Mathf.Sin((e.duration-e.left)*18));
                    if(e.left<=0){FireJourney(e);warning=false;}
                }
                else if(e.phase==0&&!warning&&e.cooldown<=0&&position.y>=e.area.yMin-6&&position.y<=e.area.yMax+2)
                {
                    Vector2 nearby=new Vector2(Mathf.Clamp(position.x,e.area.xMin,e.area.xMax),Mathf.Clamp(position.y,e.area.yMin,e.area.yMax));
                    if(!CrowdInView(nearby,0))continue;
                    e.phase=1;e.left=e.duration;e.createdFrame=Time.frameCount;e.activations++;JourneyWarnings++;warning=true;
                    e.root.gameObject.SetActive(true);Sound(tickClip,.6f);
                }
            }
            if(Stage==2&&!journeyClocked)
            {
                Vector2 goal=new Vector2(world.goal.x,world.goal.z);
                bool onPad=Mathf.Abs(position.x-goal.x)<world.goalHalfSize.x&&Mathf.Abs(position.y-goal.y)<world.goalHalfSize.y;
                clockHold=onPad&&(interactInput||(IsBracing&&moveInput.sqrMagnitude<.01f))&&!PlayerAirborne&&!PlayerRecovering&&velocity.magnitude<1.4f?Mathf.Min(.8f,clockHold+dt):0;
                if(clockHold>=.8f){journeyClocked=true;JourneyClockIns++;JourneyCompleted++;Sound(goalClip,.7f);}
            }
        }
        void PauseJourney()
        {
            errandHold=0;interactInput=false;QAInteract=false;
            foreach(var e in journeyEvents)
            {
                if(e.kind=="parcel-shortcut"){if(e.broken&&e.root)e.root.gameObject.SetActive(false);continue;}
                if(e.phase==1||e.phase==2){e.phase=0;e.left=0;e.cooldown=0;if(e.root)e.root.gameObject.SetActive(false);}
            }
            if(!journeyClocked)clockHold=0;
        }
        bool JourneyCanFinish()=>Stage!=2||journeyClocked;
    }
}

