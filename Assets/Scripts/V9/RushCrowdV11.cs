using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush.V9
{
    public enum CrowdFlow { Forward, Counter, Crossing, Waiting }
    public enum PedestrianState { Walking, Yielding, WaitingSignal, Waiting, Arrived, Replanning }
    public enum PedestrianProfile { Commuter, Hurried, Leisure, Cautious }
    public sealed partial class ConfigV9
    {
        public int pedestrianStreetCount=760,pedestrianMetroCount=900,pedestrianOfficeCount=700;
        public float pedestrianDecisionInterval=.22f,pedestrianReplanDelay=1.8f;
    }
    public sealed partial class PersonV9
    {
        public int appearance;
        public CrowdFlow flow;
        public bool hasPedestrianAI;
        public PedestrianState aiState;
        public PedestrianProfile profile;
        public Vector2 destination,preferredDirection;
        public float impatience;
        public int destinationChanges,routePlans,yieldEvents;
        public IReadOnlyList<Vector2> PlannedPath=>pedestrianPath;
        internal List<Vector2> pedestrianPath;
        internal int pathIndex,travelSign=1;
        internal float walkingSpeed,decisionLeft,waitLeft,blockedTime,replanCooldown;
        internal float progressDistance=float.MaxValue;
        internal Vector2 steeringVelocity;
        internal bool pedestrianCrossingPermit;
        internal bool rushHourLocal;
        internal float simulationDebt;
        internal uint pedestrianRandom;
    }
    public sealed partial class RunV9
    {
        readonly Dictionary<Vector2Int,List<PersonV9>> crowdBuckets=new Dictionary<Vector2Int,List<PersonV9>>();
        readonly List<PersonV9> decisionNeighbors=new List<PersonV9>(32);
        readonly List<Rect> crowdReservations=new List<Rect>(),pedestrianReservations=new List<Rect>();
        readonly List<Vector2> pedestrianDestinations=new List<Vector2>();
        readonly List<float> destinationAlong=new List<float>();
        readonly List<int> destinationModules=new List<int>();
        readonly List<Vector2>[] rushHourGoals={new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>()};
        Vector2 observedPlayerVelocity;
        int crowdSimulationStep;
        public int PedestrianArrivals {get;private set;}
        public int PedestrianReplans {get;private set;}
        public int PedestrianSignalWaits {get;private set;}
        public int PedestrianYieldEvents {get;private set;}
        public int VisibleCrowdCount(Vector2 center,float halfWidth=13.5f,float halfHeight=8)
        {int n=0;foreach(var c in people)if(!c.exited&&Mathf.Abs(c.position.x-center.x)<halfWidth&&Mathf.Abs(c.position.y-center.y)<halfHeight)n++;return n;}

        internal bool IsCrowdProtected(Vector2 p,float margin=0)
        {
            foreach(var r in crowdReservations)if(InsideExpanded(r,p,margin))return true;
            foreach(var b in bodies)if(Vector2.Distance(p,b.position)<b.radius+1.5f+margin)return true;
            return false;
        }
        internal bool IsPedestrianReserved(Vector2 p,float margin=0)
        {return IsPedestrianReservedFast(p,margin);}
        static bool InsideExpanded(Rect r,Vector2 p,float margin)=>p.x>r.xMin-margin&&p.x<r.xMax+margin&&p.y>r.yMin-margin&&p.y<r.yMax+margin;
        void ReserveCrowd(Vector2 p,float radius,bool walking=false)
        {var r=new Rect(p.x-radius,p.y-radius,radius*2,radius*2);crowdReservations.Add(r);if(walking)pedestrianReservations.Add(r);}
        void SetupCrowd()
        {
            if(config.pedestrianStreetCount<0||config.pedestrianMetroCount<0||config.pedestrianOfficeCount<0||config.pedestrianStreetCount>1200||config.pedestrianMetroCount>1200||config.pedestrianOfficeCount>1200||!Positive(config.pedestrianDecisionInterval)||!Positive(config.pedestrianReplanDelay))throw new ArgumentException("Invalid pedestrian configuration");
            ReserveCrowd(map.spawn,7);ReserveCrowd(map.exit,8);
            // Attack protection differs from walking clearance: ordinary junctions stay connected.
            foreach(var t in map.tasks)
            {
                ReserveCrowd(t.center,8,true);ReserveCrowd(t.start,4,true);ReserveCrowd(t.target,4,true);
                ReserveCrowd(t.entrance,3);ReserveCrowd(t.rejoin,3);
                // Keep the central task approach clear without disconnecting ordinary road traffic.
                pedestrianReservations.Add(new Rect(t.entrance-Vector2.one*1.1f,Vector2.one*2.2f));
                pedestrianReservations.Add(new Rect(t.rejoin-Vector2.one*1.1f,Vector2.one*2.2f));
                foreach(var p in t.checkpoints)ReserveCrowd(p,3,true);
            }
            foreach(var f in fixtures)
            {
                if(f.kind==FixtureKind.FileDesk)
                {
                    ReserveCrowd(f.position,2);
                    pedestrianReservations.Add(new Rect(f.position-Vector2.one*1.3f,Vector2.one*2.6f));
                }
                else ReserveCrowd(f.position,1.5f,true);
            }
            foreach(var b in bodies)ReserveCrowd(b.home,b.radius+1.5f,true);
            if(stage==0)ReserveCrowd(map.signal+Vector2.up*2.5f,8);
            if(stage==1){ReserveCrowd(map.platform,12,true);ReserveCrowd(map.trainDoor,10,true);}
            BuildPedestrianReservationIndex();
            InitializePedestrianNavigation();
            var spawnPoints=new List<Vector2>();
            var peakPoints=new[]{new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>()};
            foreach(var p in PedestrianPoints)
            {
                if(!PublicPedestrianArea(p))continue;
                int peak=RushHourPeakAt(p);
                if(!IsCrowdProtected(p,.7f))
                {
                    if(peak>=0)peakPoints[peak].Add(p);
                    else spawnPoints.Add(p);
                }
                var cell=MapData.Cell(p);
                if(Math.Abs(cell.x*17+cell.y*29)%11==0)
                {pedestrianDestinations.Add(p);destinationAlong.Add(map.NearestRouteDistance(p));destinationModules.Add(NearestCrowdModule(p));}
                if(peak>=0)
                {
                    var module=map.modules[peak*2+1];Vector2 axis=(module.exit-module.entrance).normalized;
                    float along=Mathf.Abs(Vector2.Dot(p-(module.entrance+module.exit)*.5f,axis));
                    // Independent destinations on both banks: nobody is sent to a shared centre.
                    if(along>=12&&along<=18&&Math.Abs(cell.x*11+cell.y*7)%3==0)rushHourGoals[peak].Add(p);
                }
            }
            if(spawnPoints.Count==0||pedestrianDestinations.Count<2)return;
            var random=new System.Random(seed^0x712a);
            int count=stage==0?config.pedestrianStreetCount:stage==1?config.pedestrianMetroCount:config.pedestrianOfficeCount;
            int coffeeCount=stage==2?Mathf.Min(40,(count+2)/7):0;
            int peakQuota=Mathf.FloorToInt(count*.8f/4)*4;
            for(int attempt=0;people.Count<count&&attempt<count*180;attempt++)
            {
                bool atPeak=people.Count<peakQuota;
                var choices=atPeak?peakPoints[people.Count%4]:spawnPoints;
                if(choices.Count==0)choices=spawnPoints;
                Vector2 center=choices[random.Next(choices.Count)];
                int module=NearestCrowdModule(center);
                bool bypass=false;foreach(var z in map.zones)if(z.kind=="loop-lane"&&z.bounds.Contains(center)){bypass=true;break;}
                if(!atPeak&&random.NextDouble()>(bypass?.16:module%2==1?.8:.65))continue;
                Vector2 p=center+new Vector2((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*.5f;
                if(!map.IsWalkable(p,.52f)||IsPedestrianReserved(p,.6f)||IsCrowdProtected(p,.55f))continue;
                float spacing=atPeak?1.04f:1.5f;
                bool overlap=false;foreach(var other in people)if((other.position-p).sqrMagnitude<spacing*spacing){overlap=true;break;}if(overlap)continue;
                var c=new PersonV9{id=3000+people.Count,position=p,home=p,target=p,radius=.42f,appearance=random.Next(12),hasPedestrianAI=true,
                    rushHourLocal=atPeak,kind=coffeeCount>0&&(people.Count+1)*coffeeCount/count>people.Count*coffeeCount/count?PersonKind.Coffee:PersonKind.Ordinary};
                int roll=random.Next(100);c.profile=roll<55?PedestrianProfile.Commuter:roll<75?PedestrianProfile.Hurried:roll<88?PedestrianProfile.Leisure:PedestrianProfile.Cautious;
                if(c.kind==PersonKind.Coffee)c.profile=PedestrianProfile.Cautious;
                c.walkingSpeed=(c.profile==PedestrianProfile.Hurried?1.75f:c.profile==PedestrianProfile.Leisure?.95f:c.profile==PedestrianProfile.Cautious?1.15f:1.4f)*(.9f+(float)random.NextDouble()*.2f);
                c.travelSign=random.NextDouble()<.72?1:-1;c.pedestrianRandom=unchecked((uint)(seed*397+c.id*7919))|1u;
                c.canRush=c.kind==PersonKind.Ordinary&&c.profile==PedestrianProfile.Hurried;
                c.art=c.kind==PersonKind.Coffee?"people/woman":new[]{"people/man","people/woman","people/commuter"}[c.appearance%3];
                if(!ChoosePedestrianDestination(c))continue;
                InitializeCoffeeAI(c);
                c.decisionLeft=NextPedestrianRandom(c)*config.pedestrianDecisionInterval;
                if(random.NextDouble()<.06){c.aiState=PedestrianState.Waiting;c.waitLeft=.5f+NextPedestrianRandom(c)*2.5f;}
                people.Add(c);
            }
        }
        int RushHourPeakAt(Vector2 p)
        {
            for(int peak=0;peak<4;peak++)
            {
                var module=map.modules[peak*2+1];Vector2 axis=(module.exit-module.entrance).normalized,offset=p-(module.entrance+module.exit)*.5f;
                float along=Mathf.Abs(Vector2.Dot(offset,axis)),across=Mathf.Abs(offset.x*axis.y-offset.y*axis.x);
                // Main-road streams plus plaza shoulders, with the parallel bypass outside.
                if(along<=18&&across<=(along<7?6.5f:3.1f))return peak;
            }
            return -1;
        }
        bool ChooseRushHourDestination(PersonV9 c)
        {
            int peak=0;float nearest=float.MaxValue;
            for(int n=0;n<4;n++)
            {var m=map.modules[n*2+1];float d=(c.position-(m.entrance+m.exit)*.5f).sqrMagnitude;if(d<nearest){nearest=d;peak=n;}}
            float trip=NextPedestrianRandom(c);
            if(trip>.97f)return false;
            if(trip>.82f)peak=(peak+(c.travelSign>0?1:3))%4;
            var module=map.modules[peak*2+1];Vector2 center=(module.entrance+module.exit)*.5f,axis=(module.exit-module.entrance).normalized;
            float side=Vector2.Dot(c.position-center,axis);int component=PedestrianComponent(c.position);
            var goals=rushHourGoals[peak];int best=-1;float bestScore=float.MinValue;
            for(int n=0;n<40&&goals.Count>0;n++)
            {
                int index=Mathf.Min(goals.Count-1,(int)(NextPedestrianRandom(c)*goals.Count));Vector2 point=goals[index];
                float distance=Vector2.Distance(c.position,point);if(distance<12||PedestrianComponent(point)!=component)continue;
                float opposite=side*Vector2.Dot(point-center,axis)<0?18:0;
                float score=opposite+NextPedestrianRandom(c)*15-Mathf.Abs(distance-29)*.15f;
                if(score>bestScore){best=index;bestScore=score;}
            }
            return best>=0&&AssignPedestrianDestination(c,goals[best]);
        }
        bool PublicPedestrianArea(Vector2 p)
        {
            if(Vector2.Distance(p,map.spawn)<8||Vector2.Distance(p,map.exit)<9)return false;
            foreach(var zone in map.zones)if(zone.bounds.Contains(p)&&!zone.kind.StartsWith("task-",StringComparison.Ordinal)&&
                (zone.kind=="through-lane"||zone.kind.StartsWith("loop-",StringComparison.Ordinal)||zone.kind=="decision-plaza"||zone.kind=="alcove"||zone.kind=="street-court"||zone.kind=="station-hall"||zone.kind=="office-workroom"))return true;
            return false;
        }
        int NearestCrowdModule(Vector2 p)
        {int index=0;float best=float.MaxValue;foreach(var m in map.modules){float dist=(p-(m.entrance+m.exit)*.5f).sqrMagnitude;if(dist<best){best=dist;index=m.index;}}return index;}
        static float NextPedestrianRandom(PersonV9 c)
        {uint x=c.pedestrianRandom;x^=x<<13;x^=x>>17;x^=x<<5;c.pedestrianRandom=x;return (x&0xffffff)/16777216f;}

        internal bool AssignPedestrianDestination(PersonV9 c,Vector2 goal)
        {
            var path=FindPedestrianPath(c.position,goal);if(path==null||path.Count<2)return false;
            if((goal-c.destination).sqrMagnitude>.1f)c.destinationChanges++;
            c.destination=goal;c.target=goal;c.pedestrianPath=path;c.pathIndex=1;c.routePlans++;
            c.aiState=PedestrianState.Walking;c.waitLeft=0;c.blockedTime=0;c.progressDistance=float.MaxValue;
            c.preferredDirection=(path[1]-c.position).normalized;c.decisionLeft=0;return true;
        }
        bool ChoosePedestrianDestination(PersonV9 c)
        {
            if(c.kind==PersonKind.Coffee&&c.coffeeAIInitialized)return TryChooseCoffeeDestination(c);
            if(c.rushHourLocal&&ChooseRushHourDestination(c))return true;
            int component=PedestrianComponent(c.position);float fromAlong=map.NearestRouteDistance(c.position);
            int best=-1;float bestScore=float.MinValue;
            for(int tries=0;tries<100;tries++)
            {
                int index=Mathf.Min(pedestrianDestinations.Count-1,(int)(NextPedestrianRandom(c)*pedestrianDestinations.Count));
                Vector2 point=pedestrianDestinations[index];float distance=Vector2.Distance(c.position,point),advance=(destinationAlong[index]-fromAlong)*c.travelSign;
                if(distance<14||distance>145||PedestrianComponent(point)!=component)continue;
                float score=NextPedestrianRandom(c)*25-Mathf.Abs(distance-(c.profile==PedestrianProfile.Leisure?30:65))*.4f+(advance>15?18:0)+(destinationModules[index]%2==1?6:0);
                if(score>bestScore){bestScore=score;best=index;}
            }
            if(best>=0&&AssignPedestrianDestination(c,pedestrianDestinations[best]))return true;
            c.aiState=PedestrianState.Waiting;c.waitLeft=1;c.steeringVelocity=Vector2.zero;return false;
        }
        internal Vector2 MoveCrowdPerson(PersonV9 c,Vector2 delta)
        {
            float radius=c.hasPedestrianAI?Mathf.Max(.48f,c.radius):c.radius;
            Vector2 next=Resolve(c.position,delta,radius);
            if(IsPedestrianReserved(next,radius)&&!IsPedestrianReserved(c.position,radius))return c.position;
            foreach(var b in bodies)if(Vector2.Distance(next,b.position)<c.radius+b.radius+.3f&&Vector2.Distance(next,b.position)<Vector2.Distance(c.position,b.position))return c.position;
            foreach(var f in fixtures)if(Vector2.Distance(next,f.position)<c.radius+f.radius+.2f&&Vector2.Distance(next,f.position)<Vector2.Distance(c.position,f.position))return c.position;
            if(stage==0&&c.hasPedestrianAI)
            {
                float south=map.signal.y-radius-.01f,north=map.signal.y+5+radius+.01f;
                if(c.position.y<=south||c.position.y>=north)c.pedestrianCrossingPermit=false;
                if(Mathf.Abs(next.x-map.signal.x)<map.corridorWidth*.5f+1)
                {
                    bool up=c.position.y<=south+.0001f&&next.y>south,down=c.position.y>=north-.0001f&&next.y<north;
                    if(up||down)
                    {
                        if(!SignalGreen&&!c.pedestrianCrossingPermit)
                        {
                            next.y=up?south:north;
                            if(c.aiState!=PedestrianState.WaitingSignal)PedestrianSignalWaits++;
                            c.aiState=PedestrianState.WaitingSignal;c.impatience=0;
                        }
                        else c.pedestrianCrossingPermit=true;
                    }
                }
            }
            return next;
        }
        static Vector2Int CrowdCell(Vector2 p)=>new Vector2Int(Mathf.FloorToInt(p.x/3),Mathf.FloorToInt(p.y/3));
        void SeparatePedestrianOverlap(PersonV9 c,float dt)
        {
            Vector2 correction=Vector2.zero;var cell=CrowdCell(c.position);
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)if(crowdBuckets.TryGetValue(cell+new Vector2Int(x,y),out var neighbors))
                foreach(var other in neighbors)
                {
                    if(other==c||other.exited)continue;
                    Vector2 away=c.position-other.position;float distance=away.magnitude;
                    float overlap=c.radius+other.radius+.03f-distance;if(overlap<=.02f)continue;
                    // Exact coincidence needs an antisymmetric direction; a zero displacement
                    // otherwise makes every prediction reject escape forever, even after replanning.
                    Vector2 direction=distance>.001f?away/distance:(c.id<other.id?Vector2.right:Vector2.left);
                    correction+=direction*Mathf.Min(overlap*.5f,dt);
                }
            if(correction.sqrMagnitude>.000001f)c.position=MoveCrowdPerson(c,Vector2.ClampMagnitude(correction,dt*1.2f));
        }
        void SetPedestrianYield(PersonV9 c)
        {if(c.aiState!=PedestrianState.Yielding){c.yieldEvents++;PedestrianYieldEvents++;}c.aiState=PedestrianState.Yielding;}
        void DecidePedestrianVelocity(PersonV9 c)
        {
            if(c.pedestrianPath==null||c.pathIndex>=c.pedestrianPath.Count){ChoosePedestrianDestination(c);return;}
            while(c.pathIndex<c.pedestrianPath.Count-1&&Vector2.Distance(c.position,c.pedestrianPath[c.pathIndex])<.4f)
            {c.pathIndex++;c.progressDistance=float.MaxValue;}
            Vector2 goal=c.pedestrianPath[c.pathIndex];c.preferredDirection=(goal-c.position).normalized;
            c.flow=Vector2.Dot(c.preferredDirection,Vector2.up)>.2f?CrowdFlow.Forward:CrowdFlow.Counter;
            float speed=Mathf.Min(c.walkingSpeed,Vector2.Distance(c.position,goal)*2.5f);
            Vector2 best=Vector2.zero;float bestScore=float.NegativeInfinity;var cell=CrowdCell(c.position);
            decisionNeighbors.Clear();
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)if(crowdBuckets.TryGetValue(cell+new Vector2Int(x,y),out var neighbors))
                foreach(var other in neighbors)if(other!=c&&!other.exited&&(other.position-c.position).sqrMagnitude<=12)decisionNeighbors.Add(other);
            float personalRadius=c.radius+CoffeePersonalSpace(c);
            for(int option=0;option<15;option++)
            {
                float degrees=option==0?0:option==1?28:option==2?-28:option==3?55:option==4?-55:option==5?85:option==6?-85:option==8?110:option==9?-110:option==10?145:option==11?-145:option==12?180:0;
                if(option>=8&&option<=12&&c.blockedTime<.85f)continue;
                float angle=degrees*Mathf.Deg2Rad;Vector2 forward=c.preferredDirection,right=new Vector2(forward.y,-forward.x);
                Vector2 candidate=option==7?Vector2.zero:(forward*Mathf.Cos(angle)+right*Mathf.Sin(angle))*speed;
                // Dense queues can creep forward; full speed versus stop alone produces jams.
                if(option==13)candidate*=.35f;else if(option==14)candidate*=.65f;
                if(candidate.sqrMagnitude>.001f&&!PedestrianLineClear(c.position,c.position+candidate*.65f,.43f))continue;
                // A blocked commuter may briefly give up forward progress to let others pass.
                float score=Vector2.Dot(candidate,forward)*(c.blockedTime>.85f?1.2f:3)-(candidate-c.velocity).magnitude*.3f+(degrees>0?.12f:0)+(option>=8&&option<=12?1.2f:0);
                foreach(var other in decisionNeighbors)
                    score-=PedestrianCollisionCost(c.position,candidate,personalRadius,other.position,other.velocity,other.radius+CoffeePersonalSpace(other));
                score-=PedestrianCollisionCost(c.position,candidate,personalRadius,player,observedPlayerVelocity,PlayerRadius)*1.25f;
                if(score>bestScore){bestScore=score;best=candidate;}
            }
            c.steeringVelocity=best;
            if(Vector2.Dot(best,c.preferredDirection)<c.walkingSpeed*.45f)SetPedestrianYield(c);else c.aiState=PedestrianState.Walking;
        }
        static float PedestrianCollisionCost(Vector2 self,Vector2 candidate,float radius,Vector2 other,Vector2 otherVelocity,float otherRadius)
        {
            Vector2 gap=other-self;if(gap.sqrMagnitude>12)return 0;
            Vector2 relative=otherVelocity-candidate;float time=relative.sqrMagnitude>.001f?Mathf.Clamp(-Vector2.Dot(gap,relative)/relative.sqrMagnitude,0,.8f):0;
            float clearance=(gap+relative*time).magnitude-(radius+otherRadius);
            if(gap.magnitude<radius+otherRadius+.05f&&Vector2.Dot(gap,relative)>0)return 0;
            return clearance<.05f?30+(.05f-clearance)*20:clearance<.4f?(.4f-clearance)*5:0;
        }
        void UpdateCrowdPeople(float dt)
        {
            crowdSimulationStep++;
            foreach(var bucket in crowdBuckets.Values)bucket.Clear();
            foreach(var c in people)if(!c.exited)
            {var key=CrowdCell(c.position);if(!crowdBuckets.TryGetValue(key,out var bucket)){bucket=new List<PersonV9>(8);crowdBuckets.Add(key,bucket);}bucket.Add(c);}
            foreach(var c in people)
            {
                if(c.exited)continue;
                if(c.kind==PersonKind.Alighting)
                {
                    // Train passengers own individual exit goals, with a dedicated door-clearing
                    // policy instead of ordinary public-space goals inside the reserved platform.
                    c.preferredDirection=(c.target-c.position).normalized;c.aiState=PedestrianState.Walking;
                    Vector2 old=c.position;c.position=Resolve(c.position,Vector2.ClampMagnitude(c.target-c.position,1.6f)*dt,c.radius);c.velocity=(c.position-old)/dt;
                    if(Vector2.Distance(c.position,player)<c.radius+PlayerRadius)Contact(c.id);
                    if(Vector2.Distance(c.position,c.target)<.3f){c.exited=true;c.aiState=PedestrianState.Arrived;}continue;
                }
                float actorDt=dt;
                if(c.hasPedestrianAI)
                {
                    // Every actor keeps simulating. Only off-camera motion is amortized to
                    // 15 Hz; near-player interactions and airborne bodies stay at full rate.
                    c.simulationDebt+=dt;
                    bool distant=(c.position-player).sqrMagnitude>28*28&&!c.dashAirborne&&c.actionPhase==NpcActionPhase.Cruise;
                    if(distant&&((c.id+crowdSimulationStep)&3)!=0)continue;
                    actorDt=c.simulationDebt;c.simulationDebt=0;
                }
                if(StepDashImpact(c,actorDt))continue;
                if(!c.hasPedestrianAI){if(!StepNpcAction(c,actorDt))c.velocity=Vector2.zero;continue;}
                SeparatePedestrianOverlap(c,actorDt);
                if(StepCoffeeAI(c,actorDt))continue;
                c.replanCooldown=Mathf.Max(0,c.replanCooldown-actorDt);
                if(c.aiState==PedestrianState.WaitingSignal)
                {
                    c.velocity=Vector2.zero;c.steeringVelocity=Vector2.zero;c.impatience=0;
                    if(!SignalGreen)continue;c.aiState=PedestrianState.Walking;c.decisionLeft=0;c.blockedTime=0;
                }
                if(c.aiState==PedestrianState.Waiting||c.aiState==PedestrianState.Arrived)
                {
                    c.velocity=Vector2.zero;c.steeringVelocity=Vector2.zero;c.waitLeft-=actorDt;
                    if(c.waitLeft<=0)ChoosePedestrianDestination(c);continue;
                }
                if(Vector2.Distance(c.position,c.destination)<.65f)
                {
                    if(CoffeeArrival(c))continue;
                    c.aiState=PedestrianState.Arrived;c.waitLeft=c.profile==PedestrianProfile.Hurried?.35f:c.profile==PedestrianProfile.Leisure?2+NextPedestrianRandom(c)*3:.5f+NextPedestrianRandom(c)*1.5f;
                    c.velocity=Vector2.zero;c.steeringVelocity=Vector2.zero;c.impatience=0;PedestrianArrivals++;continue;
                }
                c.decisionLeft-=actorDt;
                if(c.decisionLeft<=0){DecidePedestrianVelocity(c);c.decisionLeft=config.pedestrianDecisionInterval*(.7f+NextPedestrianRandom(c)*.6f);}
                Vector2 toPlayer=player-c.position;
                bool playerBlocking=c.profile==PedestrianProfile.Hurried&&Vector2.Dot(toPlayer,c.preferredDirection)>0&&toPlayer.magnitude<3.8f&&Mathf.Abs(toPlayer.x*c.preferredDirection.y-toPlayer.y*c.preferredDirection.x)<.9f;
                c.impatience=playerBlocking?Mathf.Min(3,c.impatience+actorDt):Mathf.Max(0,c.impatience-actorDt*2);
                if(StepNpcAction(c,actorDt))continue;
                Vector2 before=c.position;
                c.velocity=Vector2.MoveTowards(c.velocity,c.steeringVelocity,actorDt*5);
                c.position=MoveCrowdPerson(c,c.velocity*actorDt);c.velocity=(c.position-before)/actorDt;
                if(c.aiState==PedestrianState.WaitingSignal){c.velocity=Vector2.zero;c.steeringVelocity=Vector2.zero;continue;}
                if(Vector2.Distance(c.position,player)<c.radius+PlayerRadius){Contact(c.id);if(!HandlePlayerDashContact(c))HandleNpcActionContact(c);}
                float progress=c.pedestrianPath!=null&&c.pathIndex<c.pedestrianPath.Count?Vector2.Distance(c.position,c.pedestrianPath[c.pathIndex]):0;
                if(progress<c.progressDistance-.2f){c.progressDistance=progress;c.blockedTime=0;}else c.blockedTime+=actorDt;
                if(c.blockedTime>config.pedestrianReplanDelay&&c.replanCooldown<=0)
                {
                    var path=FindPedestrianPath(c.position,c.destination,c.position+c.preferredDirection*2);
                    if(path!=null&&path.Count>1){c.pedestrianPath=path;c.pathIndex=1;c.routePlans++;PedestrianReplans++;c.aiState=PedestrianState.Replanning;}
                    c.blockedTime=0;c.replanCooldown=2.5f+NextPedestrianRandom(c);c.progressDistance=float.MaxValue;c.decisionLeft=0;
                }
            }
        }
    }
}
