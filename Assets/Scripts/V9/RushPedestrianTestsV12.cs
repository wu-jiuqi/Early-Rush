using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush.V9
{
    /// <summary>Natural population checks and explicitly arranged pedestrian/decision fixtures.</summary>
    public static class RushPedestrianTestsV12
    {
        static int assertions;
        static void Check(bool value,string description)
        {assertions++;if(!value)throw new InvalidOperationException("V12 pedestrian test failed: "+description);}

        public static int RunAll()
        {
            assertions=0;
            for(int stage=0;stage<3;stage++)Population(stage);
            DestinationLifecycle();SignalCompliance();RouteBlockingActions();OverlapRecovery();ObserveGeneratedBlocking();
            Debug.Log("V12_PEDESTRIAN_ASSERTIONS count="+assertions);return assertions;
        }

        static void Population(int stage)
        {
            var run=new RunV9(stage,91026);var ids=new HashSet<int>();var profiles=new HashSet<PedestrianProfile>();
            var destinations=new HashSet<Vector2Int>();var distance=new Dictionary<int,float>();var previous=new Dictionary<int,Vector2>();
            var taskBodies=new List<Vector2>();foreach(var body in run.bodies)taskBodies.Add(body.position);
            Check(run.people.Count>=70,"stage "+stage+" has a real commuter population");
            foreach(var person in run.people)
            {
                Check(ids.Add(person.id),"unique pedestrian identity");
                Check(person.hasPedestrianAI&&person.routePlans>0,"each spawned pedestrian owns a planned trip");
                Check(run.map.IsWalkable(person.position,person.radius)&&run.map.IsWalkable(person.destination,person.radius),"trip endpoints are walkable");
                Check(!person.canRush||(person.kind==PersonKind.Ordinary&&person.profile==PedestrianProfile.Hurried),"only ordinary hurried commuters may rush");
                profiles.Add(person.profile);destinations.Add(MapData.Cell(person.destination));distance[person.id]=0;previous[person.id]=person.position;
            }
            Check(profiles.Count>=3&&destinations.Count>1,"population has differing profiles and destinations");
            run.Begin();float largestStep=0;
            for(int frame=0;frame<40;frame++)
            {
                run.Tick(.25f,Vector2.zero);bool safe=true;
                foreach(var person in run.people)
                {
                    float step=Vector2.Distance(previous[person.id],person.position);distance[person.id]+=step;largestStep=Mathf.Max(largestStep,step);previous[person.id]=person.position;
                    safe&=run.map.IsWalkable(person.position,person.radius)&&!run.IsPedestrianReserved(person.position,person.radius);
                }
                Check(safe,"population remains on usable ground and outside task workspaces");
            }
            int moving=0,yields=0,plans=0,changed=0;foreach(var person in run.people)
            {if(distance[person.id]>1)moving++;yields+=person.yieldEvents;plans+=person.routePlans;changed+=person.destinationChanges;}
            Check(moving>run.people.Count*.5f,"most commuters make physical progress in ten seconds");
            Check(largestStep<=Mathf.Max(run.config.npcChargeSpeed,3)*.25f+.1f,"AI never teleports to a new goal");
            for(int i=0;i<run.bodies.Count;i++)Check(run.bodies[i].position==taskBodies[i]&&!run.bodies[i].touched,"pedestrians cannot push task objects");
            foreach(var patch in run.dirt)Check(!patch.cleaned,"pedestrians cannot clean optional-task water");
            run.Pause();double time=run.elapsed;var frozen=new List<Vector2>();var clocks=new List<Vector2Int>();
            foreach(var person in run.people){frozen.Add(person.position);clocks.Add(new Vector2Int(person.routePlans,person.destinationChanges));}
            run.Tick(5,Vector2.up);bool unchanged=run.elapsed==time;
            for(int i=0;i<run.people.Count;i++)unchanged&=run.people[i].position==frozen[i]&&run.people[i].routePlans==clocks[i].x&&run.people[i].destinationChanges==clocks[i].y;
            Check(unchanged,"pause freezes physical movement and autonomous destination decisions");
            Debug.Log("V12_PEDESTRIANS stage="+stage+" population="+run.people.Count+" profiles="+profiles.Count+" destinations="+destinations.Count+" moving10s="+moving+" yields="+yields+" plans="+plans+" destinationChanges="+changed+" largestQuarterSecondStep="+largestStep);
        }

        static void Arena(RunV9 run,out Vector2 position,out Vector2 direction,bool protectAttacks=false)
        {
            for(float distance=18;distance<run.map.PathLength-18;distance+=2)
            {
                Vector2 point=run.map.PointAtDistance(distance),forward=(run.map.PointAtDistance(distance+1)-point).normalized;
                bool clear=forward.sqrMagnitude>.9f;
                for(float along=0;along<=10;along+=.5f)
                {
                    Vector2 sample=point+forward*along;
                    if(!run.map.IsWalkable(sample,.7f)||run.IsPedestrianReserved(sample,.7f)||(protectAttacks&&run.IsCrowdProtected(sample,1)))clear=false;
                }
                if(clear){position=point;direction=forward;return;}
            }
            throw new InvalidOperationException("No usable straight corridor for the V12 local fixture");
        }

        static void DestinationLifecycle()
        {
            var run=new RunV9(0,91026);var person=run.people.Find(p=>p.kind==PersonKind.Ordinary);Check(person!=null,"lifecycle fixture uses a generated pedestrian");
            var other=run.people.Find(p=>p.id!=person.id);Vector2 otherGoal=other.destination,otherPosition=other.position;int otherPlans=other.routePlans;
            person.canRush=false;
            Arena(run,out var start,out var direction);person.position=start;Vector2 goal=start+direction*6;int before=person.routePlans;
            Check(run.AssignPedestrianDestination(person,goal),"pedestrian can independently plan a reachable new destination");
            Check(person.position==start&&person.destination==goal&&person.routePlans>before,"destination assignment plans without teleporting");
            Check(other.destination==otherGoal&&other.position==otherPosition&&other.routePlans==otherPlans,"one pedestrian's new trip does not overwrite another's goal or plan");
            run.people.Clear();run.people.Add(person);
            run.Begin();int maxSteps=25*60;bool reached=false;
            for(int step=0;step<maxSteps&&run.phase==RunPhase.Playing;step++)
            {
                run.Tick(1f/60,Vector2.zero);
                if(Vector2.Distance(person.position,goal)<.9f){reached=true;break;}
            }
            Check(reached,"generated pedestrian reaches an assigned goal through physical walking");
            int changes=person.destinationChanges;int plans=person.routePlans;run.Tick(12,Vector2.zero);
            Check(person.destinationChanges>changes&&person.routePlans>plans&&Vector2.Distance(person.destination,goal)>1,
                "arrival and dwell lead to a newly planned trip instead of a fixed loop");
        }

        static void SignalCompliance()
        {
            var run=new RunV9(0,91026);var person=run.people.Find(p=>p.kind==PersonKind.Ordinary);run.people.Clear();run.people.Add(person);person.canRush=false;
            person.position=run.map.signal-Vector2.up*1.2f;person.impatience=0;
            Check(run.AssignPedestrianDestination(person,run.map.signal+Vector2.up*9),"signal fixture has a route across the street");
            run.Begin();run.elapsed=run.config.greenDuration+1;run.Tick(2,Vector2.zero);
            Check(person.position.y<=run.map.signal.y-person.radius&&person.aiState==PedestrianState.WaitingSignal,"pedestrian waits before entering a red crossing");
            Check(person.impatience<.01f&&person.actionPhase==NpcActionPhase.Cruise,"waiting for red does not create aggression");
            run.elapsed=run.config.greenDuration+run.config.redDuration-.1;run.Tick(3,Vector2.zero);
            Check(person.position.y>run.map.signal.y+.2f,"green releases the same waiting pedestrian");
            run.elapsed=run.config.greenDuration*2+run.config.redDuration;run.Tick(5,Vector2.zero);
            Check(person.position.y>run.map.signal.y+5+person.radius,"legal green entrant can clear after the light turns red");
            person.position=run.map.signal+Vector2.up*6.2f;person.impatience=0;
            Check(run.AssignPedestrianDestination(person,run.map.signal-Vector2.up*9),"reverse crossing route is reachable");
            run.elapsed=run.config.greenDuration+1;run.Tick(2,Vector2.zero);
            Check(person.position.y>=run.map.signal.y+5+person.radius&&person.aiState==PedestrianState.WaitingSignal,"red crossing also blocks the opposite direction");
        }

        static RunV9 ActionDecisionFixture(out PersonV9 person)
        {
            // Decision isolation uses one manually arranged actor; Population() retains defaults.
            var run=new RunV9(0,91026,new ConfigV9{pedestrianStreetCount=0,pedestrianMetroCount=0,pedestrianOfficeCount=0});Arena(run,out var start,out var direction,true);
            person=new PersonV9{id=8700,position=start,home=start,destination=start+direction*8,preferredDirection=direction,
                hasPedestrianAI=true,profile=PedestrianProfile.Hurried,aiState=PedestrianState.Yielding,impatience=1,
                kind=PersonKind.Ordinary,canRush=true,actionInitialized=true};
            run.people.Add(person);run.player=start+direction*2.2f;run.Begin();return run;
        }

        static void RouteBlockingActions()
        {
            // Decision fixtures intentionally supply impatience; natural waiting accumulation is
            // covered by population observation and must not be reported as attack frequency.
            foreach(string scenario in new[]{"behind","side","distant","patient","ordinary-profile","red-wait"})
            {
                var run=ActionDecisionFixture(out var person);Vector2 right=new Vector2(person.preferredDirection.y,-person.preferredDirection.x);
                if(scenario=="behind")run.player=person.position-person.preferredDirection*2;
                if(scenario=="side")run.player+=right*1.2f;
                if(scenario=="distant")run.player=person.position+person.preferredDirection*4.2f;
                if(scenario=="patient")person.impatience=.79f;
                if(scenario=="ordinary-profile")person.profile=PedestrianProfile.Commuter;
                if(scenario=="red-wait")person.aiState=PedestrianState.WaitingSignal;
                run.StepNpcAction(person,1f/60);
                Check(person.actionPhase==NpcActionPhase.Cruise,"AI does not pursue player in case: "+scenario);
            }
            var blocked=ActionDecisionFixture(out var hurried);Vector2 lateral=new Vector2(hurried.preferredDirection.y,-hurried.preferredDirection.x);
            blocked.player+=lateral*.4f;blocked.StepNpcAction(hurried,1f/60);
            Check(hurried.actionPhase==NpcActionPhase.Windup&&hurried.chargeDirection==hurried.preferredDirection,
                "blocked hurried commuter commits along their own route rather than aiming at player");
            blocked.player+=lateral*1.2f;blocked.StepNpcAction(hurried,1f/60);
            Check(hurried.actionPhase==NpcActionPhase.Recovery&&hurried.chargeCancelled,"yielding out of their path cancels AI windup");
        }

        /// <summary>Generated actor, controlled initial placement/readiness, then only normal Tick/input.</summary>
        public static string ObserveGeneratedBlocking()
        {
            var run=new RunV9(0,91026);var person=run.people.Find(p=>p.kind==PersonKind.Ordinary&&p.profile==PedestrianProfile.Hurried&&p.canRush);
            Check(person!=null&&person.hasPedestrianAI&&person.impatience==0,"generated hurried actor begins without injected impatience");
            run.people.Clear();run.people.Add(person);Arena(run,out var start,out var direction,true);
            person.position=start;
            Check(run.AssignPedestrianDestination(person,start+direction*8),"generated hurried actor receives an ordinary route past the blocker");
            // The attack is initially available; impatience and AI/action states are never assigned.
            person.actionInitialized=true;person.actionCooldownLeft=0;
            run.player=start+direction*3;run.Begin();float maximumImpatience=0;bool windup=false;
            for(int frame=0;frame<5*60&&run.phase==RunPhase.Playing;frame++)
            {
                run.Tick(1f/60,Vector2.zero);maximumImpatience=Mathf.Max(maximumImpatience,person.impatience);
                if(person.actionPhase==NpcActionPhase.Windup){windup=true;break;}
            }
            Check(maximumImpatience>0,"standing player causes impatience to accumulate through real AI ticks");
            bool cancelled=false;
            if(windup)
            {
                Vector2 lateral=new Vector2(person.chargeDirection.y,-person.chargeDirection.x);
                for(int frame=0;frame<60&&run.phase==RunPhase.Playing;frame++)
                {
                    run.Tick(1f/60,lateral);
                    if(person.actionPhase==NpcActionPhase.Recovery&&person.chargeCancelled){cancelled=true;break;}
                    if(person.actionPhase==NpcActionPhase.Charging)break;
                }
                Check(cancelled&&run.PlayerLaunchCount==0,"player side movement cancels a naturally accumulated AI windup");
            }
            else Check(Vector2.Distance(person.position,start)>1,"without a windup the actor actually bypasses the blocker instead of remaining stuck");
            string report="V12_GENERATED_BLOCKING actor="+person.id+" profile="+person.profile+" maxImpatience="+maximumImpatience+
                " windupObserved="+windup+" cancelledByPlayerInput="+cancelled+" actorPosition="+person.position+" playerPosition="+run.player+
                " seconds="+run.elapsed+" setup=generated-actor-isolated-placement-and-ready-cooldown-only";
            Debug.Log(report);return report;
        }

        static void OverlapRecovery()
        {
            var run=new RunV9(0,91026);var first=run.people[0];var second=run.people[1];run.people.Clear();run.people.Add(first);run.people.Add(second);
            Arena(run,out var start,out var direction);Vector2 overlap=start+direction*4;
            first.position=overlap;second.position=overlap;
            Check(run.AssignPedestrianDestination(first,overlap+direction*4)&&run.AssignPedestrianDestination(second,overlap-direction*4),"overlap fixture starts with two independent valid trips");
            run.Begin();float firstProgress=0,secondProgress=0,largestStep=0;
            for(int frame=0;frame<8*4;frame++)
            {
                Vector2 a=first.position,b=second.position;run.Tick(.25f,Vector2.zero);
                largestStep=Mathf.Max(largestStep,Mathf.Max(Vector2.Distance(a,first.position),Vector2.Distance(b,second.position)));
                firstProgress=Mathf.Max(firstProgress,Vector2.Distance(overlap,first.position));secondProgress=Mathf.Max(secondProgress,Vector2.Distance(overlap,second.position));
            }
            Check(firstProgress>1&&secondProgress>1,"perfectly overlapping pedestrians separate and resume both trips");
            Check(largestStep<1&&run.map.IsWalkable(first.position,first.radius)&&run.map.IsWalkable(second.position,second.radius),"overlap recovery is gradual and does not cross walls");
        }

        sealed class DenseTrack
        {
            internal readonly Vector2[] positions=new Vector2[41];
            internal readonly float[] travelled=new float[41];
            internal Vector2 previous;
            internal float distance,stopped,longestStop;
            internal DenseTrack(Vector2 position)
            {previous=position;for(int i=0;i<positions.Length;i++)positions[i]=position;}
        }

        static float Quantile(List<float> values,float q)
        {
            if(values.Count==0)return 0;
            values.Sort();return values[Mathf.Clamp(Mathf.RoundToInt((values.Count-1)*q),0,values.Count-1)];
        }

        /// <summary>
        /// Read-only density evidence, not an acceptance shortcut. The same 26.67 x 15 world-unit
        /// window is sampled at 0/10/30/60 seconds, with no camera zoom, actor filtering, or input.
        /// A single stage can be selected for a bounded recheck; defaults retain all populations.
        /// </summary>
        public static string ObserveDenseFlow(int stage=-1,int seconds=60)
        {
            if(stage < -1||stage>2)throw new ArgumentOutOfRangeException(nameof(stage));
            if(seconds<10||seconds>120)throw new ArgumentOutOfRangeException(nameof(seconds));
            var report=new System.Text.StringBuilder();
            int first=stage<0?0:stage,last=stage<0?2:stage;
            for(int chapter=first;chapter<=last;chapter++)
            {
                var watch=System.Diagnostics.Stopwatch.StartNew();
                var run=new RunV9(chapter,91026);var tracks=new Dictionary<int,DenseTrack>();
                var crossings=new Dictionary<int,double>[4];
                for(int i=0;i<4;i++)crossings[i]=new Dictionary<int,double>();
                foreach(var person in run.people)tracks[person.id]=new DenseTrack(person.position);
                int invalidGround=0,reservedSamples=0;float maximumStep=0;
                var reservedIds=new HashSet<int>();var invalidIds=new HashSet<int>();
                run.Begin();
                for(int frame=0;frame<=seconds*4&&run.phase==RunPhase.Playing;frame++)
                {
                    if(frame>0)
                    {
                        run.Tick(.25f,Vector2.zero);
                        foreach(var person in run.people)
                        {
                            if(!tracks.TryGetValue(person.id,out var track))tracks[person.id]=track=new DenseTrack(person.position);
                            float step=Vector2.Distance(track.previous,person.position);maximumStep=Mathf.Max(maximumStep,step);
                            track.distance+=step;
                            bool intentional=person.exited||person.aiState==PedestrianState.Waiting||person.aiState==PedestrianState.WaitingSignal||
                                person.aiState==PedestrianState.Arrived||person.actionPhase!=NpcActionPhase.Cruise||person.dashAirborne;
                            track.stopped=!intentional&&step<.015f?track.stopped+.25f:0;
                            track.longestStop=Mathf.Max(track.longestStop,track.stopped);
                            if(!person.exited)for(int hotspot=0;hotspot<4;hotspot++)
                            {
                                var module=run.map.modules[hotspot*2+1];Vector2 center=(module.entrance+module.exit)*.5f;
                                Vector2 along=(module.exit-module.entrance).normalized;
                                float before=Vector2.Dot(track.previous-center,along),after=Vector2.Dot(person.position-center,along);
                                if(before*after<0&&Mathf.Abs(person.position.x-center.x)<40f/3&&Mathf.Abs(person.position.y-center.y)<7.5f)
                                    crossings[hotspot][person.id]=run.elapsed;
                            }
                            track.previous=person.position;track.positions[frame%41]=person.position;track.travelled[frame%41]=track.distance;
                        }
                    }
                    foreach(var person in run.people)if(!person.exited)
                    {
                        if(!run.map.IsWalkable(person.position,person.radius)){invalidGround++;invalidIds.Add(person.id);}
                        if(run.IsPedestrianReserved(person.position,person.radius)){reservedSamples++;reservedIds.Add(person.id);}
                    }
                    if(frame==0||frame==40||frame==120||frame==240||frame==seconds*4)
                    {
                        for(int hotspot=0;hotspot<4;hotspot++)
                        {
                            var module=run.map.modules[hotspot*2+1];Vector2 center=(module.entrance+module.exit)*.5f;
                            int visible=0,ordinary=0,coffee=0,yielding=0,movedOne=0,crossed=0;
                            var nearest=new List<float>();var net=new List<float>();var travel=new List<float>();
                            foreach(var person in run.people)
                            {
                                if(person.exited||Mathf.Abs(person.position.x-center.x)>=40f/3||Mathf.Abs(person.position.y-center.y)>=7.5f)continue;
                                visible++;if(person.kind==PersonKind.Ordinary)ordinary++;if(person.kind==PersonKind.Coffee)coffee++;
                                if(person.aiState==PedestrianState.Yielding)yielding++;
                                float closest=float.MaxValue;foreach(var other in run.people)
                                    if(other!=person&&!other.exited)closest=Mathf.Min(closest,Vector2.Distance(person.position,other.position));
                                if(closest<float.MaxValue)nearest.Add(closest);
                                DenseTrack track=tracks[person.id];int past=Mathf.Max(0,frame-40)%41;
                                float displacement=Vector2.Distance(track.positions[past],person.position);
                                net.Add(displacement);travel.Add(track.distance-track.travelled[past]);if(displacement>=1)movedOne++;
                            }
                            foreach(var time in crossings[hotspot].Values)if(time>run.elapsed-10)crossed++;
                            string line="V12_DENSE_VIEW stage="+chapter+" seconds="+run.elapsed+" module="+module.index+" center="+center+
                                " window=26.6667x15 visible="+visible+" ordinary="+ordinary+" coffee="+coffee+" yielding="+yielding+
                                " nearestP25="+Quantile(nearest,.25f)+" nearestMedian="+Quantile(nearest,.5f)+" nearestP90="+Quantile(nearest,.9f)+
                                " progressWindowSeconds="+Mathf.Min(frame*.25f,10)+" medianNetMetres="+Quantile(net,.5f)+
                                " medianTravelMetres="+Quantile(travel,.5f)+" movedAtLeast1m="+movedOne+" distinctCentreCrossers10s="+crossed;
                            Debug.Log(line);report.AppendLine(line);
                        }
                    }
                    if(watch.Elapsed.TotalSeconds>300)throw new TimeoutException("Dense-flow observation exceeded its explicit 300-second per-stage wall-clock budget");
                }
                int stopped10=0,stopped15=0;float longest=0;var examples=new System.Text.StringBuilder();
                foreach(var person in run.people)
                {
                    DenseTrack track=tracks[person.id];longest=Mathf.Max(longest,track.longestStop);
                    if(track.longestStop>=10)stopped10++;
                    if(track.longestStop>=15)
                    {
                        stopped15++;if(stopped15<=12)examples.Append(" id="+person.id+" stopped="+track.longestStop+" state="+person.aiState+
                            " at="+person.position+" goal="+person.destination+" plans="+person.routePlans);
                    }
                }
                string summary="V12_DENSE_FLOW stage="+chapter+" seed=91026 simulatedSeconds="+run.elapsed+" population="+run.people.Count+
                    " arrivals="+run.PedestrianArrivals+" replans="+run.PedestrianReplans+" yields="+run.PedestrianYieldEvents+
                    " signalWaits="+run.PedestrianSignalWaits+" stoppedAtLeast10s="+stopped10+" stoppedAtLeast15s="+stopped15+
                    " longestUnintentionalStop="+longest+" invalidGroundSamples="+invalidGround+" invalidGroundActors="+invalidIds.Count+
                    " taskReservedSamples="+reservedSamples+" taskReservedActors="+reservedIds.Count+" maxQuarterSecondStep="+maximumStep+
                    " wallSeconds="+watch.Elapsed.TotalSeconds+examples;
                Debug.Log(summary);report.AppendLine(summary);
            }
            return report.ToString();
        }

        /// <summary>Optional bounded diagnostic; uses full default populations and does not alter AI.</summary>
        public static string ObserveLongRun(int seconds=60)
        {
            if(seconds<1||seconds>120)throw new ArgumentOutOfRangeException(nameof(seconds));
            var report=new System.Text.StringBuilder();var watch=System.Diagnostics.Stopwatch.StartNew();
            for(int stage=0;stage<3;stage++)
            {
                var run=new RunV9(stage,91026);var previous=new Dictionary<int,Vector2>();
                var stopped=new Dictionary<int,float>();var longest=new Dictionary<int,float>();float maximumStep=0;int invalidGround=0,reservedVisits=0;
                foreach(var person in run.people){previous[person.id]=person.position;stopped[person.id]=0;longest[person.id]=0;}
                run.Begin();
                for(int frame=0;frame<seconds*4&&run.phase==RunPhase.Playing;frame++)
                {
                    run.Tick(.25f,Vector2.zero);
                    foreach(var person in run.people)
                    {
                        float step=Vector2.Distance(previous[person.id],person.position);maximumStep=Mathf.Max(maximumStep,step);previous[person.id]=person.position;
                        bool intentionallyWaiting=person.aiState==PedestrianState.Waiting||person.aiState==PedestrianState.WaitingSignal||person.aiState==PedestrianState.Arrived||person.actionPhase!=NpcActionPhase.Cruise;
                        stopped[person.id]=!intentionallyWaiting&&step<.015f?stopped[person.id]+.25f:0;
                        longest[person.id]=Mathf.Max(longest[person.id],stopped[person.id]);
                        if(!run.map.IsWalkable(person.position,person.radius))invalidGround++;
                        if(run.IsPedestrianReserved(person.position,person.radius))reservedVisits++;
                    }
                    if(watch.Elapsed.TotalSeconds>180)throw new TimeoutException("V12 long observation exceeded its explicit 180-second wall-clock budget");
                }
                int stalled=0;float longestStop=0;var samples=new System.Text.StringBuilder();
                foreach(var person in run.people)
                {
                    longestStop=Mathf.Max(longestStop,longest[person.id]);
                    if(longest[person.id]>=10)
                    {
                        stalled++;
                        if(stalled<=8)samples.Append(" id="+person.id+" stopped="+longest[person.id]+" state="+person.aiState+" at="+person.position+" goal="+person.destination+" plans="+person.routePlans);
                    }
                }
                string line="V12_LONG_OBSERVATION stage="+stage+" simulatedSeconds="+run.elapsed+" population="+run.people.Count+
                    " arrivals="+run.PedestrianArrivals+" replans="+run.PedestrianReplans+" yields="+run.PedestrianYieldEvents+" signalWaits="+run.PedestrianSignalWaits+
                    " stoppedAtLeast10s="+stalled+" longestUnintentionalStop="+longestStop+" invalidGroundSamples="+invalidGround+" reservedSamples="+reservedVisits+" maxQuarterSecondStep="+maximumStep+samples;
                Debug.Log(line);report.AppendLine(line);
            }
            return report.ToString();
        }
    }
}
