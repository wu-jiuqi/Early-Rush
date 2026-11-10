using System;
using System.Collections.Generic;
using UnityEngine;
namespace EarlyRush.V9
{
    /// <summary>Model assertions and real-input route replay. No Unity execution is implied by source presence.</summary>
    public static class RushTestsV9
    {
        static int count;
        static void Check(bool yes,string label){count++;if(!yes)throw new InvalidOperationException("V9 test failed: "+label);}
        public static int RunAll(int seeds=10)
        {
            count=0;
            for(int stage=0;stage<3;stage++)for(int seed=0;seed<seeds;seed++)
            {
                var map=RushMapV9.Create(stage,seed);Check(map.Validate(out string reason),reason);
                Check(map.modules.Count==9,"module count");Check(map.tasks.Count==3,"task count");
            }
            for(int n=0;n<4;n++)Check(RunV9.Score(true,n)==(n==0?1:n==3?3:2),"success stars");
            for(int n=0;n<4;n++)Check(RunV9.Score(false,n)==(n==0?0:n==3?2:1),"record stars");
            var run=new RunV9(0,42);run.Begin();Vector2 original=run.player;
            run.Tick(1,Vector2.up);Check(run.player.y>original.y+2,"real movement");Check(!run.bicycle,"starts walking");
            run.Pause();double time=run.elapsed;original=run.player;run.Tick(30,Vector2.right);Check(run.elapsed==time&&run.player==original,"pause freezes");run.Resume();
            var repeat=new RunV9(0,42);Check(!repeat.BoardsSolved(),"flip initial not solved");
            // Brute force all 2^5 legal binary operation vectors: must solve generated board.
            bool solvable=false;for(int mask=0;mask<32;mask++)
            {var state=(bool[])repeat.boards.Clone();for(int i=0;i<5;i++)if((mask&(1<<i))!=0)for(int j=Mathf.Max(0,i-1);j<=Mathf.Min(4,i+1);j++)state[j]=!state[j];bool all=true;for(int i=0;i<5;i++)all&=state[i]==repeat.boardGoal[i];solvable|=all;}
            Check(solvable,"flip has legal solution");
            var office=new RunV9(2,42,new ConfigV9{stageLimit=.1});office.Begin();office.Tick(.2f,Vector2.zero);
            Check(!office.success&&office.tasks[0].state!=TaskState.Complete,"timeout never auto-completes coffee");
            var metro=new RunV9(1,42);metro.Begin();Vector2 cloth=metro.bodies[0].position;metro.Rescue(metro.bodies[0].id);
            Check(metro.bodies[0].position==cloth&&metro.tasks[2].count==0,"rescue cannot clean");
            ContactFixtures();
            Debug.Log("V9_MODEL_ASSERTIONS count="+count);return count;
        }
        // Local mechanism fixtures may arrange the player near an interaction. These are NOT
        // traversal/timing evidence; task state and completion are still driven only by Tick events.
        static void ContactFixtures()
        {
            var breakfast=new RunV9(0,42,new ConfigV9{breakfastTolerance=1});breakfast.Begin();
            breakfast.player=breakfast.map.tasks[0].center;breakfast.Tick(.5f,Vector2.zero);
            Check(breakfast.tasks[0].state==TaskState.Active,"pickup starts breakfast");
            FixtureV9 board=null;foreach(var f in breakfast.fixtures)if(f.kind==FixtureKind.FlipBoard){board=f;break;}
            breakfast.player=board.position-Vector2.up*1.5f;breakfast.velocity=Vector2.zero;
            breakfast.Tick(.5f,Vector2.up);Check(breakfast.tasks[0].state==TaskState.Active&&breakfast.tasks[0].count==1,"equal breakfast tolerance remains active");
            int hits=breakfast.bumps;var state=(bool[])breakfast.boards.Clone();breakfast.Tick(1,Vector2.up);
            bool same=true;for(int i=0;i<5;i++)same&=state[i]==breakfast.boards[i];
            Check(same&&hits==breakfast.bumps,"holding board does not retrigger");
            breakfast.Tick(.5f,Vector2.down);breakfast.Tick(.7f,Vector2.up);
            Check(breakfast.bumps>hits,"separate then touch re-arms");
            Check(breakfast.tasks[0].state==TaskState.Failed,"strictly above breakfast tolerance fails");
            var wheel=new RunV9(0,42);wheel.Begin();var body=wheel.bodies[0];
            wheel.player=body.position-Vector2.up*1.5f;Vector2 old=body.position;wheel.Tick(.7f,Vector2.up);
            Check(body.touched&&Vector2.Distance(old,body.position)>.2f,"wheel requires actual push");
            Check(!wheel.bicycle&&!wheel.tasks[1].done,"single pushed wheel cannot grant bike");
            var coffee=new RunV9(2,42,new ConfigV9{coffeeTolerance=0});coffee.Begin();PersonV9 cup=null;
            foreach(var c in coffee.people)if(c.kind==PersonKind.Coffee){cup=c;break;}
            Check(cup!=null,"coffee people exist");coffee.player=cup.position-Vector2.up*.95f;coffee.Tick(.2f,Vector2.up);
            Check(cup.spilled&&coffee.tasks[0].state==TaskState.Failed,"coffee unique spill above threshold fails");
            int spills=coffee.tasks[0].count;coffee.Tick(.2f,Vector2.up);
            Check(coffee.tasks[0].count==spills,"same cup not counted twice");
            var capsule=new RunV9(1,42);capsule.Begin();FixtureV9 machine=null;
            foreach(var f in capsule.fixtures)if(f.kind==FixtureKind.Capsule)machine=f;
            capsule.player=machine.position-Vector2.up*1.5f;capsule.Tick(.5f,Vector2.up);int countBefore=capsule.tasks[1].count;
            capsule.Tick(3,Vector2.up);Check(capsule.tasks[1].count==countBefore&&countBefore>=0,"capsule holding neither repeats nor falls below zero");
        }
        public sealed class ReplayResult
        {
            public bool success;public int stage,seed;public double elapsed;public int contacts;public Vector2 stoppedAt;public string reason;
            public readonly List<TelemetryV9> trace=new List<TelemetryV9>();
        }
        /// <summary>Follows generated cells via BFS, driving Tick at 60Hz. Never edits player/task/body state.
        /// A machine navigation test, not a human playability/time validation. Mainline tasks are optional.
        /// Metro boards using real approach/wait/input. Any obstruction reports the stop, not a teleport.</summary>
        public static ReplayResult ReplayMainline(int stage,int seed,ConfigV9 config=null)
        {
            var run=new RunV9(stage,seed,config);run.Begin();var result=new ReplayResult{stage=stage,seed=seed};
            var goals=new List<Vector2>();
            if(stage==1){goals.Add(run.map.platform+Vector2.left*3);goals.Add(run.map.trainDoor);}
            goals.Add(run.map.exit);
            foreach(var goal in goals)
            {
                var path=FindPath(run.map,run.player,goal);
                if(path==null){result.reason="No clearance path";break;}
                foreach(var point in path)
                {
                    double start=run.elapsed;Vector2 origin=run.player;
                    while(Vector2.Distance(run.player,point)>.25f&&run.phase==RunPhase.Playing)
                    {
                        run.Tick(1f/60,(point-run.player).normalized);
                        if(run.elapsed-start>20){result.reason="Waypoint blocked: "+point+" origin="+origin;break;}
                    }
                    if(result.reason!=null||run.phase!=RunPhase.Playing)break;
                }
                if(result.reason!=null||run.phase!=RunPhase.Playing)break;
                if(stage==1&&goal==goals[0])
                {
                    double wait=run.elapsed;while(run.trainPhase!=TrainPhase.Boarding&&run.phase==RunPhase.Playing&&run.elapsed-wait<40)run.Tick(1f/60,Vector2.zero);
                    if(run.trainPhase!=TrainPhase.Boarding){result.reason="Train failed to reach boarding";break;}
                }
                if(stage==1&&goal==run.map.trainDoor)for(int i=0;i<90&&run.phase==RunPhase.Playing;i++)run.Tick(1f/60,Vector2.zero);
            }
            result.success=run.success;result.elapsed=run.elapsed;result.contacts=run.bumps;result.stoppedAt=run.player;
            if(result.reason==null)result.reason=run.success?"Input replay finished; not human timing evidence":"Did not finish within timer";
            result.trace.AddRange(run.telemetry);return result;
        }
        /// <summary>Dedicated street mechanics route. Uses ordinary movement and push contact only.
        /// High breakfast tolerance isolates the mechanics from navigation quality; not a balance test.</summary>
        public static ReplayResult ReplayStreetTasks(int seed)
        {
            var run=new RunV9(0,seed,new ConfigV9 { stageLimit=1600,breakfastTolerance=1000 });run.Begin();
            var result=new ReplayResult{stage=0,seed=seed};
            if(!MoveTo(run,run.map.tasks[0].center,30,out string reason))return EndReplay(run,result,reason);
            for(int i=0;i<40;i++)run.Tick(1f/60,Vector2.zero);
            if(run.tasks[0].state!=TaskState.Active)return EndReplay(run,result,"Breakfast did not start from real pickup");
            foreach(var body in run.bodies)
            {
                Vector2 target=run.map.tasks[1].target;
                // Arrive near the wheel, then move behind its push direction rather than teleporting it.
                Vector2 direction=(target-body.position).normalized;
                if(!MoveTo(run,body.position-direction*2,30,out reason))return EndReplay(run,result,reason);
                double began=run.elapsed;
                while(!body.done&&run.phase==RunPhase.Playing&&run.elapsed-began<90)
                {
                    direction=(target-body.position).normalized;
                    Vector2 behind=body.position-direction*(body.radius+RunV9.PlayerRadius+.05f);
                    Vector2 input=Vector2.Distance(run.player,behind)>.6f?(behind-run.player).normalized:direction;
                    run.Tick(1f/60,input);
                    if(Vector2.Distance(body.position,target)<1.2f)run.Tick(.45f,Vector2.zero);
                }
                if(!body.done)return EndReplay(run,result,"Wheel push route stuck id="+body.id);
            }
            if(!run.bicycle)return EndReplay(run,result,"Two wheels did not grant bike");
            int solution=-1;
            for(int mask=0;mask<32;mask++)
            {
                var state=(bool[])run.boards.Clone();for(int i=0;i<5;i++)if((mask&(1<<i))!=0)for(int j=Mathf.Max(0,i-1);j<=Mathf.Min(4,i+1);j++)state[j]=!state[j];
                bool solved=true;for(int i=0;i<5;i++)solved&=state[i]==run.boardGoal[i];if(solved){solution=mask;break;}
            }
            if(solution<0)return EndReplay(run,result,"No flip solution");
            foreach(var f in run.fixtures)if(f.kind==FixtureKind.FlipBoard&&(solution&(1<<f.index))!=0)
            {
                if(!MoveTo(run,f.position-Vector2.up*2,30,out reason))return EndReplay(run,result,reason);
                // A fresh forward touch followed by separation. Holding does not generate repeated flips.
                for(int i=0;i<30;i++)run.Tick(1f/60,Vector2.up);
                for(int i=0;i<30;i++)run.Tick(1f/60,Vector2.down);
            }
            if(!run.tasks[2].done)return EndReplay(run,result,"Flip input route failed to solve");
            if(!MoveTo(run,run.map.exit,30,out reason))return EndReplay(run,result,reason);
            return EndReplay(run,result,"Street input route finished; not timing acceptance");
        }
        static bool MoveTo(RunV9 run,Vector2 goal,double waypointBudget,out string reason)
        {
            reason=null;var path=FindPath(run.map,run.player,goal);if(path==null){reason="No path to "+goal;return false;}
            foreach(var p in path)
            {
                double start=run.elapsed;
                while(Vector2.Distance(run.player,p)>.25f&&run.phase==RunPhase.Playing)
                {run.Tick(1f/60,(p-run.player).normalized);if(run.elapsed-start>waypointBudget){reason="Blocked at "+run.player+" waypoint="+p;return false;}}
                if(run.phase!=RunPhase.Playing)return run.success;
            }
            return true;
        }
        static ReplayResult EndReplay(RunV9 run,ReplayResult result,string reason)
        {
            result.success=run.success&&run.Completed==3;result.elapsed=run.elapsed;result.contacts=run.bumps;result.stoppedAt=run.player;result.reason=reason;result.trace.AddRange(run.telemetry);return result;
        }
        public static List<Vector2> FindPath(MapData map,Vector2 start,Vector2 goal)
        {
            Vector2Int root=MapData.Cell(start),end=MapData.Cell(goal);var queue=new Queue<Vector2Int>();var previous=new Dictionary<Vector2Int,Vector2Int>();
            queue.Enqueue(root);previous[root]=root;Vector2Int[] dirs={Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
            while(queue.Count>0)
            {
                var cell=queue.Dequeue();if(cell==end)break;
                foreach(var d in dirs){var p=cell+d;if(previous.ContainsKey(p)||!map.IsWalkable(p,.55f))continue;previous[p]=cell;queue.Enqueue(p);}
            }
            if(!previous.ContainsKey(end))return null;
            var path=new List<Vector2>();for(var p=end;p!=root;p=previous[p])path.Add(p);path.Reverse();return path;
        }
    }
}
