using System;
using System.Collections.Generic;
using UnityEngine;
namespace EarlyRush.V9
{
    /// <summary>Input-driven simulation. No Unity objects, frame clock, rewards or renderer writes.</summary>
    public sealed partial class RunV9
    {
        public const float PlayerRadius=.46f;
        public readonly int stage,seed;public readonly ConfigV9 config;public readonly MapData map;
        public RunPhase phase=RunPhase.Ready;public double elapsed;public double limit=>config.stageLimit;
        public Vector2 player,velocity,facing=Vector2.up;public float energy=1,boost;public bool bicycle,success;public int stars,bumps;
        public readonly List<TaskV9> tasks=new List<TaskV9>();
        public readonly List<BodyV9> bodies=new List<BodyV9>();
        public readonly List<PersonV9> people=new List<PersonV9>();
        public readonly List<FixtureV9> fixtures=new List<FixtureV9>();
        public readonly List<DirtV9> dirt=new List<DirtV9>();
        public readonly List<TelemetryV9> telemetry=new List<TelemetryV9>();
        public readonly List<Rect> solids=new List<Rect>();
        public readonly bool[] boards=new bool[5];public readonly bool[] boardGoal=new bool[5];
        public TrainPhase trainPhase=TrainPhase.Waiting;
        public string eventKind="";public float eventAge;public Action<string> onCue;
        public bool SignalGreen=>elapsed%(config.greenDuration+config.redDuration)<config.greenDuration;
        public float BeatPhase=>(float)((elapsed-beatOrigin)%config.beatPeriod/config.beatPeriod);
        public float Remaining=>Mathf.Max(0,(float)(limit-elapsed));
        public int Completed{get{int n=0;foreach(var t in tasks)if(t.done)n++;return n;}}
        public bool IsDelivered(int leaderId)=>delivered.Contains(leaderId);
        public static int Score(bool onTime,int complete)=>(onTime?1:0)+(complete>=1?1:0)+(complete==3?1:0);
        HashSet<int> contacts=new HashSet<int>(),currentContacts=new HashSet<int>();
        readonly HashSet<int> delivered=new HashSet<int>();
        double beatOrigin,trainStarted;float pickupHold,waitHold;bool nearCapsule,waitingRegistered,crossingPermit,trainApproaching,earlyBoarded;
        int eventIndex,lastModule=-1;double nextSample;
        public RunV9(int stage,int seed,ConfigV9 config=null)
        {
            this.stage=stage;this.seed=seed;this.config=config??new ConfigV9();
            if(stage<0||stage>2||this.config.fileCount>5||this.config.chairCount>6||!Positive(this.config.breakfastGameMinutes)||!Positive(this.config.gameMinutesPerRealSecond)||!Positive(this.config.deliveryDuration)||!Positive(this.config.greenDuration)||!Positive(this.config.redDuration)||!Positive(this.config.stageLimit)||!Positive(this.config.walkSpeed)||!Positive(this.config.bikeSpeed)||this.config.bikeSpeed<=this.config.walkSpeed||!Positive(this.config.beatPeriod)||!Positive(this.config.beatWindow)||this.config.beatWindow>this.config.beatPeriod||!Positive(this.config.stableDuration)||!Positive(this.config.stableSpeed)||this.config.capsuleGoal<1||this.config.capsulePenalty<0||this.config.breakfastTolerance<0||this.config.coffeeTolerance<0||this.config.fileCount<1||this.config.chairCount<1)throw new ArgumentException("Invalid V9 configuration");
            ValidateActionConfiguration();map=RushMapV9.Create(stage,seed);player=map.spawn;Setup();
        }
        static bool Positive(double value)=>value>0&&!double.IsNaN(value)&&!double.IsInfinity(value);
        void Setup()
        {
            string[][] titles={new[]{"早餐保卫战","自行车修理店","翻面挑战"},new[]{"先下后上","扭蛋机","拖水渍"},new[]{"小心同事的咖啡","文件精准派送","办公椅归位"}};
            string[][] icons={new[]{"stalls/breakfast-stall","legacy-icons/bike","ui/selection"},new[]{"legacy-icons/train","metro/vending","fx-decal/rain"},new[]{"legacy-extras/coffee","office/filing-cabinet","tasks/chair"}};
            for(int i=0;i<3;i++)tasks.Add(new TaskV9{id=(stage==0?"S":stage==1?"M":"O")+(i+1),title=titles[stage][i],icon=icons[stage][i],position=map.tasks[i].center});
            if(stage==0)
            {
                tasks[0].limit=config.breakfastTolerance;tasks[1].limit=2;tasks[2].limit=boards.Length;
                AddFixture(FixtureKind.Breakfast,map.tasks[0].center,"stalls/breakfast-stall");
                AddFixture(FixtureKind.Repair,map.tasks[1].target,"street/storefront");
                for(int i=0;i<2;i++)AddBody(BodyKind.Wheel,map.tasks[1].start+Vector2.right*(i*3-1.5f),"tasks/wheel");
                for(int i=0;i<boards.Length;i++)AddFixture(FixtureKind.FlipBoard,map.tasks[2].center+new Vector2((i-2)*2,0),"ui/selection",i);
                var random=new System.Random(seed);for(int i=0;i<9;i++)Flip(random.Next(boards.Length));
                if(BoardsSolved())Flip(0);
            }
            else if(stage==1)
            {
                tasks[1].limit=config.capsuleGoal;
                AddFixture(FixtureKind.Capsule,map.tasks[1].center,"metro/vending");
                AddBody(BodyKind.Cloth,map.tasks[2].start,"tasks/cloth");
                for(int patch=0;patch<3;patch++)for(int x=0;x<5;x++)for(int y=0;y<5;y++)
                    dirt.Add(new DirtV9{position=map.tasks[2].center+new Vector2((patch-1)*3+x*.3f- .6f,y*.3f-.6f)});
                tasks[2].limit=dirt.Count;
            }
            else
            {
                tasks[0].state=TaskState.Active;tasks[0].limit=config.coffeeTolerance;
                tasks[1].limit=config.fileCount;tasks[2].limit=config.chairCount;
                AddFixture(FixtureKind.FilePickup,map.tasks[1].start,"office/filing-cabinet");
                for(int i=0;i<config.fileCount;i++)AddFixture(FixtureKind.FileDesk,map.PointAtDistance(map.PathLength*(.54f+.22f*(i+1)/(config.fileCount+1))),"office/desk-chair",i);
                for(int i=0;i<config.chairCount;i++)
                {
                    Vector2 p=map.tasks[2].center+new Vector2((i%3-1)*3,(i/3)*3);
                    AddFixture(FixtureKind.ChairSocket,p+Vector2.up*4,"ui/selection",i);
                    AddBody(BodyKind.Chair,p-Vector2.up*4,"tasks/chair");
                }
            }
            SetupCrowd();
        }
        void AddFixture(FixtureKind kind,Vector2 p,string art,int index=0){fixtures.Add(new FixtureV9{id=1000+fixtures.Count,index=index,kind=kind,position=p,art=art});}
        void AddBody(BodyKind kind,Vector2 p,string art){bodies.Add(new BodyV9{id=2000+bodies.Count,kind=kind,position=p,home=p,art=art,radius=kind==BodyKind.Cloth?.7f:.55f});}
        PersonV9 AddPerson(Vector2 p,PersonKind kind)
        {
            if(!map.IsWalkable(p,.5f))return null;
            var person=new PersonV9{id=3000+people.Count,kind=kind,position=p,home=p,target=p,art=kind==PersonKind.Coffee?"people/woman":"people/man",
                hasPedestrianAI=kind==PersonKind.Alighting,aiState=PedestrianState.Walking,profile=PedestrianProfile.Cautious};
            people.Add(person);return person;
        }
        void Log(string type,int id=-1){telemetry.Add(new TelemetryV9{time=elapsed,type=type,id=id,position=player});}
        void Cue(string kind){Log(kind);onCue?.Invoke(kind);}
        public void Begin(){if(phase==RunPhase.Ready){phase=RunPhase.Playing;Log("begin");}}
        public void Pause(){if(phase==RunPhase.Playing){CancelPlayerActionInput();phase=RunPhase.Paused;velocity=Vector2.zero;boost=0;Log("pause");}}
        public void Resume(){if(phase==RunPhase.Paused){phase=RunPhase.Playing;velocity=Vector2.zero;Log("resume");}}
        void SetState(int index,TaskState state){var t=tasks[index];if(t.done||t.state==TaskState.Failed||t.state==state)return;t.state=state;if(state==TaskState.Active)t.startedAt=elapsed;Log(t.id+":"+state);if(!(stage==1&&index==0&&state!=TaskState.Failed))onCue?.Invoke(state==TaskState.Complete?"task":state==TaskState.Failed?"task-fail":"start");}
        public void Rescue(int bodyId)
        {
            if(phase!=RunPhase.Playing)return;
            foreach(var b in bodies)if(b.id==bodyId&&!b.done)
            {
                // Reset transport evidence; the warp itself never pushes, cleans or parks an object.
                b.position=b.home;b.velocity=Vector2.zero;b.stable=0;b.socket=-1;b.touched=false;b.pushed=0;
                contacts.Remove(b.id);currentContacts.Remove(b.id);Log("rescue",b.id);
            }
        }
        public Vector2 Resolve(Vector2 from,Vector2 delta,float radius)
        {
            int steps=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/.12f));if(steps>4096)return from;
            Vector2 p=from;
            for(int i=0;i<steps;i++)
            {
                Vector2 next=map.ResolveMove(p,delta/steps,radius);bool blocked=false;
                foreach(var r in solids){Vector2 q=new Vector2(Mathf.Clamp(next.x,r.xMin,r.xMax),Mathf.Clamp(next.y,r.yMin,r.yMax));if((next-q).sqrMagnitude<radius*radius){blocked=true;break;}}
                if(!blocked)p=next;
            }
            return p;
        }
        public void Tick(float dt,Vector2 input)
        {
            if(phase!=RunPhase.Playing||dt<=0||float.IsNaN(dt)||float.IsInfinity(dt)||float.IsNaN(input.x)||float.IsNaN(input.y)||float.IsInfinity(input.x)||float.IsInfinity(input.y))return;
            // Long frames are consumed, not silently discarded. The host must pause on OS suspension.
            double left=dt;while(left>1e-9&&phase==RunPhase.Playing)
            {
                double step=Math.Min(left,1.0/60);
                // Split at task deadlines so a later contact cannot invalidate an already eaten
                // breakfast, and a delivery exactly at its deadline is processed before timeout.
                if(stage==0&&tasks[0].state==TaskState.Active)step=BeforeDeadline(step,tasks[0].startedAt+config.breakfastDuration);
                if(stage==2&&tasks[1].state==TaskState.Active)step=BeforeDeadline(step,tasks[1].startedAt+config.deliveryDuration);
                float actual=(float)step;Step(actual,Vector2.ClampMagnitude(input,1));left-=actual;
            }
        }
        double BeforeDeadline(double step,double deadline){double remaining=deadline-elapsed;return remaining>1e-8?Math.Min(step,remaining):step;}
        void Step(float duration,Vector2 input)
        {
            float dt=(float)Math.Min(duration,Math.Max(0,limit-elapsed));
            if(dt<=0){EvaluateTasks(0);Finish(AtExit()&&elapsed<=limit);return;}
            elapsed=Math.Min(limit,elapsed+dt);currentContacts.Clear();boost=0;energy=1;Vector2 playerBeforeStep=player;
            UpdateCapsuleProximity();
            if(input.sqrMagnitude>.001f)facing=input.normalized;
            StepPlayerActions(dt,input);
            Vector2 desired=player+velocity*dt,next=Resolve(player,velocity*dt,PlayerRadius);
            if(Vector2.Distance(desired,next)>.0001f)Contact(-1);
            if(stage==0)
            {
                float line=map.signal.y,south=line-PlayerRadius-.01f,north=line+5+PlayerRadius+.01f;
                bool atCrossing=Mathf.Abs(next.x-map.signal.x)<map.corridorWidth*.5f+1;
                if(player.y>=north||player.y<=south)crossingPermit=false;
                if(atCrossing)
                {
                    bool enteringSouth=player.y<=south+.0001f&&next.y>south;
                    bool enteringNorth=player.y>=north-.0001f&&next.y<north;
                    if(enteringSouth||enteringNorth)
                    {
                        if(!SignalGreen&&!crossingPermit)next.y=enteringSouth?south:north;
                        else crossingPermit=true;
                    }
                }
                // A green-light entrant may clear the whole lane, in either direction, after red.
            }
            foreach(var f in fixtures)
            {
                bool solid=f.kind==FixtureKind.FlipBoard||f.kind==FixtureKind.Capsule;
                if(!solid)continue;
                Vector2 d=next-f.position;float radius=f.radius+PlayerRadius;
                if(d.sqrMagnitude<=radius*radius){Contact(f.id);next=f.position+(d.sqrMagnitude>.00001f?d.normalized:-facing)*(radius+.001f);}
            }
            foreach(var c in people)
            {
                if(c.exited)continue;Vector2 d=c.position-next;float radius=c.radius+PlayerRadius;
                if(d.sqrMagnitude<radius*radius)
                {
                    Contact(c.id);if(!HandlePlayerDashContact(c))HandleNpcActionContact(c);
                    // A launched actor has one ground-motion owner. Additional ordinary soft
                    // pushes would bypass its travel cap and task-area safety checks.
                    if(c.dashAirborne)continue;
                    Vector2 n=d.sqrMagnitude>.00001f?d.normalized:facing;
                    c.position=c.kind==PersonKind.Alighting?Resolve(c.position,n*(radius-d.magnitude+.01f),c.radius):MoveCrowdPerson(c,n*(radius-d.magnitude+.01f));
                    if(Vector2.Distance(c.position,next)<radius-.02f)next=player;
                }
            }
            foreach(var b in bodies)
            {
                b.velocity=Vector2.zero;
                if(b.done)continue;Vector2 d=b.position-next;float radius=b.radius+PlayerRadius;
                if(d.sqrMagnitude<radius*radius)
                {
                    Contact(b.id);Vector2 n=d.sqrMagnitude>.00001f?d.normalized:facing,old=b.position;
                    Vector2 movedTo=Resolve(b.position,n*Mathf.Min(.15f,radius-d.magnitude+.005f),b.radius);
                    bool obstructed=false;foreach(var other in bodies)if(other.id!=b.id&&!other.done&&Vector2.Distance(movedTo,other.position)<b.radius+other.radius)obstructed=true;
                    if(!obstructed)b.position=movedTo;
                    float moved=Vector2.Distance(old,b.position);b.velocity=(b.position-old)/dt;if(moved>.0001f){b.touched=true;b.pushed+=moved;Clean(old,b.position,b);}
                    if(Vector2.Distance(b.position,next)<radius-.02f)next=player;
                }
            }
            if(map.IsWalkable(next,PlayerRadius))player=next;
            observedPlayerVelocity=(player-playerBeforeStep)/dt;
            UpdatePeople(dt);UpdateTrain(dt);EvaluateTasks(dt);UpdateEvents(dt);
            // Hysteresis: separation of at least .08 is needed to re-arm a fixed object.
            foreach(int id in contacts)if(!currentContacts.Contains(id)&&StillNear(id))currentContacts.Add(id);
            var swap=contacts;contacts=currentContacts;currentContacts=swap;
            if(elapsed>=nextSample){Log("sample");nextSample=elapsed+1;}
            int module=0;float nearest=float.MaxValue;foreach(var m in map.modules){float d=Vector2.Distance(player,(m.entrance+m.exit)*.5f);if(d<nearest){nearest=d;module=m.index;}}
            if(module!=lastModule){lastModule=module;Log("module",module);}
            if(AtExit())Finish(true);else if(elapsed>=limit-1e-8)Finish(false);
        }
        bool StillNear(int id)
        {
            if(id==-1)
            {
                foreach(var direction in new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right})
                    if(!map.IsWalkable(player+direction*.08f,PlayerRadius))return true;
                foreach(var r in solids)
                {
                    var nearest=new Vector2(Mathf.Clamp(player.x,r.xMin,r.xMax),Mathf.Clamp(player.y,r.yMin,r.yMax));
                    if(Vector2.Distance(player,nearest)<PlayerRadius+.08f)return true;
                }
            }
            foreach(var f in fixtures)if(f.id==id)return Vector2.Distance(player,f.position)<f.radius+PlayerRadius+.08f;
            foreach(var b in bodies)if(b.id==id)return Vector2.Distance(player,b.position)<b.radius+PlayerRadius+.08f;
            foreach(var c in people)if(c.id==id&&!c.exited)return Vector2.Distance(player,c.position)<c.radius+PlayerRadius+.08f;
            return false;
        }
        void Contact(int id)
        {
            if(!currentContacts.Add(id)||contacts.Contains(id))return;
            bumps++;Log("contact",id);
            if(stage==0&&tasks[0].state==TaskState.Active){tasks[0].count++;if(tasks[0].count>config.breakfastTolerance)SetState(0,TaskState.Failed);}
            foreach(var c in people)if(c.id==id)
            {
                if(stage==1&&c.kind==PersonKind.Alighting&&trainPhase==TrainPhase.Alighting&&tasks[0].state==TaskState.Active)SetState(0,TaskState.Failed);
                if(stage==2&&c.kind==PersonKind.Coffee&&!c.spilled&&tasks[0].state==TaskState.Active){c.spilled=true;tasks[0].count++;if(tasks[0].count>config.coffeeTolerance)SetState(0,TaskState.Failed);}
            }
            foreach(var f in fixtures)if(f.id==id)
            {
                if(f.kind==FixtureKind.FlipBoard&&!tasks[2].done){SetState(2,TaskState.Active);Flip(f.index);}
                if(f.kind==FixtureKind.Capsule&&!tasks[1].done)
                {
                    SetState(1,TaskState.Active);
                    double beat=(elapsed-beatOrigin)%config.beatPeriod;
                    bool hit=beat>=config.beatPeriod*.5-config.beatWindow*.5&&beat<=config.beatPeriod*.5+config.beatWindow*.5;
                    tasks[1].count=Mathf.Clamp(tasks[1].count+(hit?1:-config.capsulePenalty),0,config.capsuleGoal);
                    Log(hit?"beat-hit":"beat-miss");
                }
            }
        }
        void Flip(int index){for(int j=Mathf.Max(0,index-1);j<=Mathf.Min(boards.Length-1,index+1);j++)boards[j]=!boards[j];}
        public bool BoardsSolved(){for(int i=0;i<boards.Length;i++)if(boards[i]!=boardGoal[i])return false;return true;}
        void Clean(Vector2 a,Vector2 b,BodyV9 body)
        {
            if(body.kind!=BodyKind.Cloth||!body.touched)return;
            SetState(2,TaskState.Active);Vector2 segment=b-a;
            foreach(var patch in dirt)if(!patch.cleaned)
            {
                float t=Mathf.Clamp01(Vector2.Dot(patch.position-a,segment)/Mathf.Max(.000001f,segment.sqrMagnitude));
                // Discrete effective-area cells (0.3 units). Full cell must fit inside the swept cloth.
                if(Vector2.Distance(patch.position,a+segment*t)<=body.radius-.213f)patch.cleaned=true;
            }
        }
        void EvaluateTasks(float dt)
        {
            if(stage==0)
            {
                if(tasks[0].state==TaskState.NotStarted)
                {pickupHold=Vector2.Distance(player,map.tasks[0].center)<1.3f?pickupHold+dt:0;if(pickupHold>=config.stableDuration)SetState(0,TaskState.Active);}
                if(tasks[0].state==TaskState.Active){tasks[0].progress=Mathf.Clamp01((float)((elapsed-tasks[0].startedAt)/config.breakfastDuration));if(elapsed-tasks[0].startedAt>=config.breakfastDuration-1e-8)SetState(0,TaskState.Complete);}
                int wheels=0;foreach(var b in bodies)if(b.kind==BodyKind.Wheel)
                {
                    if(b.touched)SetState(1,TaskState.Active);
                    b.stable=b.touched&&b.pushed>.2f&&b.velocity.magnitude<=config.stableSpeed&&Vector2.Distance(b.position,map.tasks[1].target)<2.2f?b.stable+dt:0;
                    if(!b.done&&b.stable>=config.stableDuration){b.done=true;Log("wheel-stored",b.id);}if(b.done)wheels++;
                }
                tasks[1].count=wheels;if(wheels==2&&!tasks[1].done){SetState(1,TaskState.Complete);bicycle=true;Cue("bicycle");}
                if(BoardsSolved()&&tasks[2].state==TaskState.Active)SetState(2,TaskState.Complete);
            }
            else if(stage==1)
            {
                UpdateCapsuleProximity();
                if(tasks[1].count>=config.capsuleGoal)SetState(1,TaskState.Complete);
                int clean=0;foreach(var p in dirt)if(p.cleaned)clean++;tasks[2].count=clean;tasks[2].progress=clean/(float)dirt.Count;
                if(clean==dirt.Count)SetState(2,TaskState.Complete);
            }
            else
            {
                if(tasks[1].state==TaskState.NotStarted){pickupHold=Vector2.Distance(player,map.tasks[1].start)<1.3f?pickupHold+dt:0;if(pickupHold>=config.stableDuration)SetState(1,TaskState.Active);}
                if(tasks[1].state==TaskState.Active)
                {
                    foreach(var f in fixtures)if(f.kind==FixtureKind.FileDesk&&f.index>=0&&f.index<config.fileCount&&!delivered.Contains(f.index))
                    {
                        // Each held document is keyed by leader ID; a desk never consumes another leader's file.
                        if(Vector2.Distance(player,f.position)<1.2f)deskHold[f.index]=GetHold(f.index)+dt;else deskHold[f.index]=0;
                        if(GetHold(f.index)+1e-6f>=config.stableDuration&&elapsed-tasks[1].startedAt<=config.deliveryDuration+1e-8){delivered.Add(f.index);Log("delivered",f.index);}
                    }
                    tasks[1].count=delivered.Count;
                    if(delivered.Count==config.fileCount)SetState(1,TaskState.Complete);else if(elapsed-tasks[1].startedAt>config.deliveryDuration)SetState(1,TaskState.Failed);
                }
                int parked=0;foreach(var b in bodies)if(b.kind==BodyKind.Chair)
                {
                    if(b.done){parked++;continue;}if(b.touched)SetState(2,TaskState.Active);
                    FixtureV9 socket=null;foreach(var f in fixtures)if(f.kind==FixtureKind.ChairSocket&&!f.occupied&&Vector2.Distance(b.position,f.position)<.9f){socket=f;break;}
                    if(socket==null||!b.touched||b.pushed<.2f||b.velocity.magnitude>config.stableSpeed){b.stable=0;b.socket=-1;continue;}
                    if(b.socket!=socket.id){b.socket=socket.id;b.stable=0;}b.stable+=dt;
                    if(b.stable>=config.stableDuration){socket.occupied=true;socket.occupant=b.id;b.done=true;b.position=socket.position;parked++;Log("chair-parked",b.id);}
                }
                tasks[2].count=parked;if(parked==config.chairCount)SetState(2,TaskState.Complete);
            }
        }
        readonly Dictionary<int,float> deskHold=new Dictionary<int,float>();
        void UpdateCapsuleProximity()
        {
            if(stage!=1)return;bool near=Vector2.Distance(player,map.tasks[1].center)<4;
            if(near&&!nearCapsule){beatOrigin=elapsed;Log("beat-restart");}nearCapsule=near;
        }
        float GetHold(int id)=>deskHold.TryGetValue(id,out float value)?value:0;
        void UpdatePeople(float dt)
        {
            UpdateCrowdPeople(dt);
        }
        void UpdateTrain(float dt)
        {
            if(stage!=1)return;
            if(trainPhase==TrainPhase.Waiting)
            {
                bool wait=Vector2.Distance(player,map.platform)<4;waitHold=wait?waitHold+dt:0;
                if(waitHold>.4f)waitingRegistered=true;
                if(Vector2.Distance(player,map.platform)<10&&!trainApproaching){trainApproaching=true;trainStarted=elapsed;Log("train-approach");}
                if(trainApproaching&&elapsed-trainStarted>=3)
                {
                    trainPhase=TrainPhase.Alighting;trainStarted=elapsed;if(waitingRegistered)SetState(0,TaskState.Active);
                    for(int i=0;i<6;i++)
                    {
                        var c=AddPerson(map.trainDoor+new Vector2((i%2-.5f)*1.1f,-i/2f),PersonKind.Alighting);
                        if(c!=null)
                        {
                            c.target=map.platform+new Vector2((i%2==0?-1:1)*4,-4);c.destination=c.target;
                            c.pedestrianPath=new List<Vector2>{c.position,c.target};c.routePlans=1;c.destinationChanges=1;
                        }
                        else Log("invalid-alighting-spawn",i);
                    }
                    Log("train-open");
                }
            }
            else if(trainPhase==TrainPhase.Alighting)
            {
                if(Vector2.Distance(player,map.trainDoor)<1.5f)earlyBoarded=true;
                bool remaining=false;foreach(var c in people)if(c.kind==PersonKind.Alighting&&!c.exited)remaining=true;
                if(!remaining)
                {
                    trainPhase=earlyBoarded?TrainPhase.Riding:TrainPhase.Boarding;trainStarted=elapsed;
                    Log("alighting-ended");if(earlyBoarded)Log("early-boarded-uncredited");
                }
            }
            else if(trainPhase==TrainPhase.Boarding&&Vector2.Distance(player,map.trainDoor)<1.5f)
            {
                if(waitingRegistered&&tasks[0].state==TaskState.Active)SetState(0,TaskState.Complete);
                trainPhase=TrainPhase.Riding;trainStarted=elapsed;Log("boarded");
            }
            else if(trainPhase==TrainPhase.Riding&&elapsed-trainStarted>=1){trainPhase=TrainPhase.Arrived;Log("arrived");}
        }
        void UpdateEvents(float dt)
        {
            if(eventKind!=""){eventAge+=dt;if(eventAge>25){Log("event-end");eventKind="";eventAge=0;}return;}
            if(eventIndex>=2||Remaining<45||elapsed<(eventIndex==0?100:260))return;
            foreach(var t in map.tasks)if(Vector2.Distance(player,t.center)<20)return;
            if(stage==1&&trainPhase!=TrainPhase.Arrived)return;
            eventKind=stage==0?(eventIndex==0?"rain":"delivery"):stage==1?"cleaning":eventIndex==0?"meeting":"cleaning";
            eventIndex++;eventAge=0;Log("event:"+eventKind);
            // Event visuals and routes can subscribe. No hidden task changes or NPC task completion.
        }
        bool AtExit()=>Vector2.Distance(player,map.exit)<1.3f&&(stage!=1||trainPhase==TrainPhase.Arrived);
        void Finish(bool onTime)
        {
            if(phase!=RunPhase.Playing)return;success=onTime&&elapsed<=limit+1e-8;
            if(stage==2&&success&&tasks[0].state==TaskState.Active)SetState(0,TaskState.Complete);
            stars=Score(success,Completed);CancelPlayerActionInput();phase=RunPhase.Results;velocity=Vector2.zero;eventKind="";Cue(success?"win":"late");
        }
    }
}
