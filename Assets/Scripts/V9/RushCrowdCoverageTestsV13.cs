using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace EarlyRush.V9
{
    /// <summary>Bounded regressions plus a separate whole-route persistence observation.</summary>
    public static class RushCrowdCoverageTestsV13
    {
        static int assertions;
        static void Check(bool value,string message)
        {assertions++;if(!value)throw new InvalidOperationException("V13 crowd coverage failed: "+message);}

        public static int RunAll()
        {
            assertions=0;
            for(int stage=0;stage<3;stage++)
            {
                var run=new RunV9(stage,91026);
                int expected=stage==0?run.config.pedestrianStreetCount:stage==1?run.config.pedestrianMetroCount:run.config.pedestrianOfficeCount;
                var paths=new HashSet<IReadOnlyList<Vector2>>();int ordinary=0,longTrips=0,forward=0,counter=0,coffee=0;
                foreach(var person in run.people)
                {
                    Check(person.PlannedPath!=null&&paths.Add(person.PlannedPath),"each pedestrian owns an independent planned route");
                    if(person.kind==PersonKind.Coffee){coffee++;continue;}
                    ordinary++;if(person.travelSign>0)forward++;else counter++;
                    if(Mathf.Abs(run.map.NearestRouteDistance(person.destination)-run.map.NearestRouteDistance(person.position))>=45)longTrips++;
                }
                Check(run.people.Count==expected&&coffee<=40,"coverage preserves the configured population and coffee cap");
                Check(longTrips>ordinary*.65f&&forward>ordinary*.2f&&counter>ordinary*.2f,"most ordinary trips traverse road sections in both directions");
                var initial=run.MeasureRouteCrowdCoverage();
                Check(initial.eligibleSamples>=35&&initial.excludedSamples<=8,"coverage samples the entire route with bounded explicit task exclusions");
                Check(initial.emptySamples==0&&initial.sparseSamples<=initial.eligibleSamples*.1f,"initial connecting roads have continuous visible population");
                Check(initial.maximum>=25&&initial.junctionMean>initial.connectorMean,"natural junctions remain busier than connections");
                Check(initial.bypassActors<run.people.Count*.1f,"parallel detours remain sparsely populated initially");
                run.Begin();run.Tick(2,Vector2.zero);
                var shortRun=run.MeasureRouteCrowdCoverage();
                Check(shortRun.emptySamples==0,"coverage persists under initial independent movement");
                foreach(var person in run.people)
                    Check(run.map.IsWalkable(person.position,person.radius)&&!run.IsPedestrianReserved(person.position,person.radius),
                        "distribution does not enter walls or task workspaces");
            }
            UnityEngine.Debug.Log("V13_CROWD_COVERAGE_ASSERTIONS count="+assertions);
            return assertions;
        }

        /// <summary>No actor relocation, path injection or input. All NPCs advance through normal Tick.</summary>
        public static string ObserveAll(int seconds=60)
        {
            if(seconds<30||seconds>120)throw new ArgumentOutOfRangeException(nameof(seconds));
            var report=new StringBuilder();
            for(int stage=0;stage<3;stage++)report.Append(ObserveStage(stage,seconds));
            return report.ToString();
        }

        public static string ObserveStage(int stage,int seconds=60)
        {
            if(stage<0||stage>2||seconds<30||seconds>120)throw new ArgumentOutOfRangeException();
            var watch=Stopwatch.StartNew();var run=new RunV9(stage,91026);var report=new StringBuilder();
            var previous=new Dictionary<int,Vector2>();var travel=new Dictionary<int,float>();var stopped=new Dictionary<int,float>();
            foreach(var person in run.people){previous[person.id]=person.position;travel[person.id]=0;stopped[person.id]=0;}
            int invalid=0,reserved=0;float longestStop=0,maximumStep=0,worstEmptyLength=0,worstSparseLength=0;int worstEmpty=0,worstSparse=0;
            run.Begin();
            for(int frame=0;frame<=seconds*4;frame++)
            {
                if(frame>0)
                {
                    run.Tick(.25f,Vector2.zero);
                    foreach(var person in run.people)
                    {
                        float step=Vector2.Distance(previous[person.id],person.position);previous[person.id]=person.position;
                        travel[person.id]+=step;maximumStep=Mathf.Max(maximumStep,step);
                        bool intentional=person.aiState==PedestrianState.Waiting||person.aiState==PedestrianState.WaitingSignal||
                            person.aiState==PedestrianState.Arrived||person.actionPhase!=NpcActionPhase.Cruise||person.dashAirborne;
                        stopped[person.id]=!intentional&&step<.015f?stopped[person.id]+.25f:0;
                        longestStop=Mathf.Max(longestStop,stopped[person.id]);
                        if(!run.map.IsWalkable(person.position,person.radius))invalid++;
                        if(run.IsPedestrianReserved(person.position,person.radius))reserved++;
                    }
                }
                if(frame%4==0||frame==seconds*4)
                {
                    var coverage=run.MeasureRouteCrowdCoverage();
                    worstEmpty=Mathf.Max(worstEmpty,coverage.emptySamples);worstSparse=Mathf.Max(worstSparse,coverage.sparseSamples);
                    worstEmptyLength=Mathf.Max(worstEmptyLength,coverage.longestEmptyMetres);worstSparseLength=Mathf.Max(worstSparseLength,coverage.longestSparseMetres);
                    if(frame%40==0||frame==seconds*4)
                    {
                        string line="V13_ROUTE_COVERAGE "+coverage.Summary(frame==0||frame==120||frame==240);
                        report.AppendLine(line);UnityEngine.Debug.Log(line);
                    }
                }
                if(watch.Elapsed.TotalSeconds>300)throw new TimeoutException("Whole-route observation exceeded its 300-second per-stage budget");
            }
            int moving=0;foreach(var distance in travel.Values)if(distance>10)moving++;
            string summary="V13_ROUTE_FLOW stage="+stage+" simulatedSeconds="+run.elapsed+" population="+run.people.Count+
                " movedOver10m="+moving+" arrivals="+run.PedestrianArrivals+" replans="+run.PedestrianReplans+
                " coverageObservedEverySeconds=1 worstEmptySamples="+worstEmpty+" worstBelow6Samples="+worstSparse+
                " worstEmptySampledM="+worstEmptyLength+" worstBelow6SampledM="+worstSparseLength+" longestUnintentionalStop="+longestStop+
                " invalidGroundSamples="+invalid+" taskWorkspaceSamples="+reserved+" maxQuarterSecondStep="+maximumStep+
                " wallSeconds="+watch.Elapsed.TotalSeconds;
            report.AppendLine(summary);UnityEngine.Debug.Log(summary);return report.ToString();
        }
    }
}
