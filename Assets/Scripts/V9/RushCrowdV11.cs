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
        readonly List<int> destinationComponents=new List<int>();
        readonly List<float> destinationOffsets=new List<float>();
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
            // Give every stretch of the commute its own spawn quota. Junction crowds are
            // additional traffic, rather than four isolated populations walking in place.
            const float routeBinLength=12;
            int routeBinCount=Mathf.Max(1,Mathf.CeilToInt((map.PathLength-17)/routeBinLength));
            var roadBins=new List<Vector2>[routeBinCount];
            for(int i=0;i<roadBins.Length;i++)roadBins[i]=new List<Vector2>();
            var junctionPoints=new List<Vector2>[map.modules.Count];
            for(int i=0;i<junctionPoints.Length;i++)junctionPoints[i]=new List<Vector2>();
            foreach(var p in PedestrianPoints)
            {
                if(!PublicPedestrianArea(p))continue;
                float along=map.NearestRouteDistance(p);
                float offRoad=Vector2.Distance(p,map.PointAtDistance(along));
                // Attack-safe road approaches may carry ordinary pedestrians. The distinct
                // walking reservations still keep every task's working space clear.
                if(!IsPedestrianReserved(p,.6f))
                {
                    if(offRoad<=3.1f&&along>=8&&along<=map.PathLength-9)
                        roadBins[Mathf.Clamp(Mathf.FloorToInt((along-8)/routeBinLength),0,roadBins.Length-1)].Add(p);
                    int junction=PedestrianJunctionAt(p);
                    if(junction>=0&&!IsCrowdProtected(p,.55f))junctionPoints[junction].Add(p);
                }
                var cell=MapData.Cell(p);
                // End trips beside the stream; each destination is still a distinct nav point.
                bool terminal=along>=10&&along<=map.PathLength-10&&(along<=22||along>=map.PathLength-22)&&offRoad<=3.1f;
                if(offRoad>=1.4f&&(terminal||Math.Abs(cell.x*17+cell.y*29)%11==0))
                {
                    pedestrianDestinations.Add(p);destinationAlong.Add(along);destinationModules.Add(NearestCrowdModule(p));
                    destinationComponents.Add(PedestrianComponent(p));destinationOffsets.Add(offRoad);
                }
            }
            var usableRoadBins=new List<List<Vector2>>();
            foreach(var bin in roadBins)if(bin.Count>0)usableRoadBins.Add(bin);
            var weightedJunctions=new List<int>();
            for(int i=0;i<junctionPoints.Length;i++)if(junctionPoints[i].Count>0)
            {weightedJunctions.Add(i);if(i%2==1)weightedJunctions.Add(i);}
            if(usableRoadBins.Count==0||pedestrianDestinations.Count<2)return;
            var random=new System.Random(seed^0x712a);
            int count=stage==0?config.pedestrianStreetCount:stage==1?config.pedestrianMetroCount:config.pedestrianOfficeCount;
            int coffeeCount=stage==2?Mathf.Min(40,(count+2)/7):0;
            int roadQuota=Mathf.CeilToInt(count*.68f);
            for(int attempt=0;people.Count<count&&attempt<count*180;attempt++)
            {
                bool onRoad=people.Count<roadQuota||weightedJunctions.Count==0;
                var choices=onRoad?usableRoadBins[people.Count%usableRoadBins.Count]:
                    junctionPoints[weightedJunctions[(people.Count-roadQuota)%weightedJunctions.Count]];
                Vector2 center=choices[random.Next(choices.Count)];
                int travelSign=random.NextDouble()<.6?1:-1;
                float routeAlong=map.NearestRouteDistance(center);
                Vector2 roadPoint=map.PointAtDistance(routeAlong);
                Vector2 axis=(map.PointAtDistance(Mathf.Min(map.PathLength,routeAlong+.25f))-map.PointAtDistance(Mathf.Max(0,routeAlong-.25f))).normalized;
                Vector2 right=new Vector2(axis.y,-axis.x);
                if(onRoad&&Vector2.Dot(center-roadPoint,right)*travelSign<.65f)continue;
                Vector2 p=center+new Vector2((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*.5f;
                if(!map.IsWalkable(p,.52f)||IsPedestrianReserved(p,.6f))continue;
                float spacing=onRoad?1.16f:1.05f;
                bool overlap=false;foreach(var other in people)if((other.position-p).sqrMagnitude<spacing*spacing){overlap=true;break;}if(overlap)continue;
                var c=new PersonV9{id=3000+people.Count,position=p,home=p,target=p,radius=.42f,appearance=random.Next(12),hasPedestrianAI=true,
                    rushHourLocal=random.NextDouble()<.25,kind=coffeeCount>0&&(people.Count+1)*coffeeCount/count>people.Count*coffeeCount/count?PersonKind.Coffee:PersonKind.Ordinary};
                int roll=random.Next(100);c.profile=roll<55?PedestrianProfile.Commuter:roll<75?PedestrianProfile.Hurried:roll<88?PedestrianProfile.Leisure:PedestrianProfile.Cautious;
                if(c.kind==PersonKind.Coffee)c.profile=PedestrianProfile.Cautious;
                c.walkingSpeed=(c.profile==PedestrianProfile.Hurried?1.75f:c.profile==PedestrianProfile.Leisure?.95f:c.profile==PedestrianProfile.Cautious?1.15f:1.4f)*(.9f+(float)random.NextDouble()*.2f);
                c.travelSign=travelSign;c.pedestrianRandom=unchecked((uint)(seed*397+c.id*7919))|1u;
                c.canRush=c.kind==PersonKind.Ordinary&&c.profile==PedestrianProfile.Hurried;
                c.art=c.kind==PersonKind.Coffee?"people/woman":new[]{"people/man","people/woman","people/commuter"}[c.appearance%3];
                if(!ChoosePedestrianDestination(c))continue;
                InitializeCoffeeAI(c);
                c.decisionLeft=NextPedestrianRandom(c)*config.pedestrianDecisionInterval;
                if(random.NextDouble()<.06){c.aiState=PedestrianState.Waiting;c.waitLeft=.5f+NextPedestrianRandom(c)*2.5f;}
                people.Add(c);
            }
        }
        int PedestrianJunctionAt(Vector2 p)
        {
            for(int index=0;index<map.modules.Count;index++)
            {
                var module=map.modules[index];Vector2 axis=(module.exit-module.entrance).normalized,offset=p-(module.entrance+module.exit)*.5f;
                float along=Mathf.Abs(Vector2.Dot(offset,axis)),across=Mathf.Abs(offset.x*axis.y-offset.y*axis.x);
                // Public workroom/plaza shoulders meet the through road here. Parallel
                // bypasses remain outside this extra-density area and are usable detours.
                if(along<=11&&across<=6.5f)return index;
            }
            return -1;
        }
        bool ChooseCommuteDestination(PersonV9 c)
        {
            float fromAlong=map.NearestRouteDistance(c.position);
            int fromModule=NearestCrowdModule(c.position),component=PedestrianComponent(c.position);
            float minimum=c.rushHourLocal?24:58,maximum=c.rushHourLocal?100:155;
            float preferred=c.rushHourLocal?35+NextPedestrianRandom(c)*35:65+NextPedestrianRandom(c)*65;
            bool sideErrand=c.rushHourLocal&&NextPedestrianRandom(c)<.35f;
            // Try the intended stream first. A disconnected side room may need the other
            // direction, but never falls back to repeatedly choosing the same node's bank.
            for(int pass=0;pass<2;pass++)
            {
                int sign=pass==0?c.travelSign:-c.travelSign,best=-1;float bestScore=float.MinValue;
                float available=sign>0?map.PathLength-10-fromAlong:fromAlong-10;
                if(available<10)continue;
                bool terminalTrip=available<minimum+24;
                float minAdvance=terminalTrip?10:minimum;
                float ideal=terminalTrip?Mathf.Max(10,available-2):preferred;
                int offset=Mathf.Min(pedestrianDestinations.Count-1,(int)(NextPedestrianRandom(c)*pedestrianDestinations.Count));
                for(int attempt=0;attempt<pedestrianDestinations.Count;attempt++)
                {
                    int index=(offset+attempt)%pedestrianDestinations.Count;
                    float advance=(destinationAlong[index]-fromAlong)*sign;
                    Vector2 point=pedestrianDestinations[index];
                    bool endpoint=sign>0?destinationAlong[index]>=map.PathLength-18:destinationAlong[index]<=18;
                    if(advance<minAdvance||advance>Mathf.Min(maximum,available)||destinationComponents[index]!=component||
                        (!c.rushHourLocal&&destinationOffsets[index]>3.1f)||
                        (terminalTrip&&!endpoint)||
                        (destinationModules[index]==fromModule&&!(terminalTrip&&endpoint)))continue;
                    // Through commuters use the full main road. Local errands still visit
                    // public side areas/bypasses, without locking any actor to a fixed loop.
                    float score=NextPedestrianRandom(c)*14-Mathf.Abs(advance-ideal)*.3f+
                        (terminalTrip&&endpoint?12:PedestrianJunctionAt(point)>=0?5:0)+
                        (sideErrand&&destinationOffsets[index]>6.5f?10:0);
                    if(score>bestScore){bestScore=score;best=index;}
                }
                if(best>=0&&AssignPedestrianDestination(c,pedestrianDestinations[best])){c.travelSign=sign;return true;}
            }
            return false;
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
            var path=FindPedestrianCommutePath(c,goal);if(path==null||path.Count<2)return false;
            if((goal-c.destination).sqrMagnitude>.1f)c.destinationChanges++;
            c.destination=goal;c.target=goal;c.pedestrianPath=path;c.pathIndex=1;c.routePlans++;
            c.aiState=PedestrianState.Walking;c.waitLeft=0;c.blockedTime=0;c.progressDistance=float.MaxValue;
            c.preferredDirection=(path[1]-c.position).normalized;c.decisionLeft=0;return true;
        }
        List<Vector2> FindPedestrianCommutePath(PersonV9 c,Vector2 goal,Vector2? avoidPoint=null)
        {
            // Joined side rooms can form shortcuts around main-road bends. Through trips
            // retain directional corner waypoints; local errands can still take those shortcuts.
            if(c.kind!=PersonKind.Ordinary||c.rushHourLocal||!c.hasPedestrianAI)return FindPedestrianPath(c.position,goal,avoidPoint);
            float fromAlong=map.NearestRouteDistance(c.position),goalAlong=map.NearestRouteDistance(goal);
            int sign=goalAlong>=fromAlong?1:-1;
            var turns=new List<Vector2>();float along=0;
            for(int i=1;i<map.route.Count-1;i++)
            {
                along+=Vector2.Distance(map.route[i-1],map.route[i]);
                if(along<=Mathf.Min(fromAlong,goalAlong)+3||along>=Mathf.Max(fromAlong,goalAlong)-3)continue;
                Vector2 incoming=(map.route[i]-map.route[i-1]).normalized,outgoing=(map.route[i+1]-map.route[i]).normalized;
                Vector2 rightIn=new Vector2(incoming.y,-incoming.x),rightOut=new Vector2(outgoing.y,-outgoing.x);
                Vector2 turn=map.route[i]+(rightIn+rightOut)*(1.4f*sign);
                if(!map.IsWalkable(turn,.5f)||IsPedestrianReserved(turn,.5f))turn=map.route[i];
                turns.Add(turn);
            }
            if(turns.Count==0)return FindPedestrianPath(c.position,goal,avoidPoint);
            if(sign<0)turns.Reverse();
            turns.Add(goal);
            var complete=new List<Vector2>{c.position};Vector2 from=c.position;
            foreach(Vector2 waypoint in turns)
            {
                var segment=FindPedestrianPath(from,waypoint,avoidPoint);
                if(segment==null)return null;
                for(int i=1;i<segment.Count;i++)complete.Add(segment[i]);
                from=waypoint;
            }
            return complete;
        }
        bool ChoosePedestrianDestination(PersonV9 c)
        {
            if(c.kind==PersonKind.Coffee&&c.coffeeAIInitialized)return TryChooseCoffeeDestination(c);
            if(ChooseCommuteDestination(c))return true;
            int component=PedestrianComponent(c.position);float fromAlong=map.NearestRouteDistance(c.position);
            int best=-1;float bestScore=float.MinValue;
            for(int tries=0;tries<100;tries++)
            {
                int index=Mathf.Min(pedestrianDestinations.Count-1,(int)(NextPedestrianRandom(c)*pedestrianDestinations.Count));
                Vector2 point=pedestrianDestinations[index];float distance=Vector2.Distance(c.position,point),advance=(destinationAlong[index]-fromAlong)*c.travelSign;
                if(distance<20||distance>145||destinationModules[index]==NearestCrowdModule(c.position)||PedestrianComponent(point)!=component)continue;
                float score=NextPedestrianRandom(c)*25-Mathf.Abs(distance-(c.profile==PedestrianProfile.Leisure?40:80))*.4f+(advance>20?18:0);
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
                    var path=FindPedestrianCommutePath(c,c.destination,c.position+c.preferredDirection*2);
                    if(path!=null&&path.Count>1){c.pedestrianPath=path;c.pathIndex=1;c.routePlans++;PedestrianReplans++;c.aiState=PedestrianState.Replanning;}
                    c.blockedTime=0;c.replanCooldown=2.5f+NextPedestrianRandom(c);c.progressDistance=float.MaxValue;c.decisionLeft=0;
                }
            }
        }
    }
}
