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
            V3RuleRegressions();
            Debug.Log("V9_MODEL_ASSERTIONS count="+count);return count;
        }
        // Arranged local fixtures exercise event boundaries, not route or human timing acceptance.
        static void V3RuleRegressions()
        {
            var config=new ConfigV9();
            Check(config.stageLimit==420,"v3 seven-minute stage limit");
            Check(Math.Abs(config.breakfastDuration-70)<1e-8,"game minutes convert to real seconds");
            config.breakfastDuration=40;Check(Math.Abs(config.breakfastGameMinutes-2)<1e-8,"legacy duration setter preserves game clock units");
            var walk=new RunV9(0,42);Check(!walk.TryDash(),"ready phase cannot dash");walk.Begin();walk.Tick(.5f,Vector2.up);
            Check(walk.velocity.magnitude<=walk.config.walkSpeed+.001f,"walking speed bounded");
            var deadline=new RunV9(0,42,new ConfigV9{stageLimit=.01});deadline.Begin();
            deadline.player=deadline.map.exit-Vector2.up*1.32f;deadline.velocity=Vector2.up*deadline.config.walkSpeed;deadline.Tick(.1f,Vector2.up);
            Check(deadline.success&&deadline.stars==1&&Math.Abs(deadline.elapsed-.01)<1e-8,"exit exactly at deadline succeeds without optional tasks");
            var late=new RunV9(0,42,new ConfigV9{stageLimit=.01});late.Begin();late.player=late.map.exit-Vector2.up*1.38f;late.Tick(.1f,Vector2.up);
            Check(!late.success&&late.phase==RunPhase.Results&&Math.Abs(late.elapsed-.01)<1e-8,"event after deadline cannot arrive");
            foreach(bool atDeadline in new[]{true,false})
            {
                var breakfast=new RunV9(0,42,new ConfigV9{breakfastDuration=.25,breakfastTolerance=0});breakfast.Begin();
                breakfast.player=breakfast.map.tasks[0].center;breakfast.Tick(.42f,Vector2.zero);
                FixtureV9 board=breakfast.fixtures.Find(f=>f.kind==FixtureKind.FlipBoard);
                breakfast.elapsed=breakfast.tasks[0].startedAt+.24;
                breakfast.player=board.position-Vector2.up*(board.radius+RunV9.PlayerRadius+(atDeadline?.02f:.05f));
                breakfast.velocity=Vector2.up*breakfast.config.walkSpeed;breakfast.Tick(.02f,Vector2.up);
                Check(breakfast.tasks[0].state==(atDeadline?TaskState.Failed:TaskState.Complete),atDeadline?"collision at breakfast deadline wins over completion":"collision after eating deadline is outside breakfast window");
            }
            var together=new RunV9(0,42,new ConfigV9{breakfastDuration=.25});together.Begin();together.player=together.map.tasks[0].center;together.Tick(.42f,Vector2.zero);
            together.elapsed=together.tasks[0].startedAt+.24;together.config.stageLimit=together.elapsed+.01;
            together.player=together.map.exit-Vector2.up*1.32f;together.velocity=Vector2.up*together.config.walkSpeed;together.Tick(.1f,Vector2.up);
            Check(together.success&&together.tasks[0].done&&together.stars==2,"task at deadline enters ledger before stage stars");
            foreach(bool exact in new[]{true,false})
            {
                var delivery=new RunV9(2,42,new ConfigV9{stableDuration=.2f,fileCount=1});delivery.Begin();
                delivery.player=delivery.map.tasks[1].start;delivery.Tick(.25f,Vector2.zero);
                var desk=delivery.fixtures.Find(f=>f.kind==FixtureKind.FileDesk);delivery.player=desk.position;delivery.Tick(.18f,Vector2.zero);
                delivery.config.deliveryDuration=delivery.elapsed-delivery.tasks[1].startedAt+(exact?.02:.015);delivery.Tick(.04f,Vector2.zero);
                Check(delivery.tasks[1].state==(exact?TaskState.Complete:TaskState.Failed),exact?"final file delivered exactly at task deadline":"late file cannot consume held document");
                Check(delivery.IsDelivered(0)==exact,"delivery ledger follows timestamp acceptance");
            }
            var mismatched=new RunV9(2,42,new ConfigV9{fileCount=1});mismatched.Begin();mismatched.player=mismatched.map.tasks[1].start;mismatched.Tick(.5f,Vector2.zero);
            var wrongDesk=mismatched.fixtures.Find(f=>f.kind==FixtureKind.FileDesk);wrongDesk.index=99;mismatched.player=wrongDesk.position;mismatched.Tick(.5f,Vector2.zero);
            Check(mismatched.tasks[1].count==0&&!mismatched.IsDelivered(99)&&mismatched.tasks[1].state==TaskState.Active,"unmatched leader desk cannot consume or complete document");

            var signal=new RunV9(0,42);signal.people.Clear();signal.Begin();signal.elapsed=signal.config.greenDuration+1;
            signal.player=signal.map.signal-Vector2.up;signal.Tick(.5f,Vector2.up);
            Check(signal.player.y<=signal.map.signal.y-RunV9.PlayerRadius,"red light stops full body from south");
            signal.player=signal.map.signal+Vector2.up*6;signal.velocity=Vector2.zero;signal.Tick(.5f,Vector2.down);
            Check(signal.player.y>=signal.map.signal.y+5+RunV9.PlayerRadius,"red light stops full body from north");
            signal.elapsed=signal.config.greenDuration-.15;signal.player=signal.map.signal-Vector2.up*.6f;signal.velocity=Vector2.up*signal.config.walkSpeed;
            signal.Tick(.5f,Vector2.up);Check(!signal.SignalGreen&&signal.player.y>signal.map.signal.y+.5f,"legal green entrant clears on red");

            var wheel=new RunV9(0,42);wheel.Begin();var body=wheel.bodies[0];wheel.player=body.position-Vector2.up*1.1f;wheel.Tick(.3f,Vector2.up);
            Check(body.touched,"stable fixture starts with genuine push");
            body.position=wheel.map.tasks[1].target-Vector2.up*1.2f;wheel.player=body.position-Vector2.up*1.02f;wheel.velocity=Vector2.zero;
            wheel.Tick(.5f,Vector2.up);Check(!body.done&&body.stable<.05f,"moving wheel cannot satisfy stable dwell");
            wheel.Tick(.7f,Vector2.zero);Check(body.done,"wheel completes after physical rest in target");
            Vector2 stored=body.position;wheel.Rescue(body.id);Check(body.position==stored&&body.done,"rescue cannot reset completed wheel");

            var cloth=new RunV9(1,42);cloth.Begin();var rag=cloth.bodies[0];rag.position=cloth.dirt[0].position-Vector2.right*.3f;
            cloth.player=rag.position-Vector2.right*1.17f;cloth.Tick(.3f,Vector2.right);int cleaned=cloth.dirt.FindAll(d=>d.cleaned).Count;
            Check(cleaned>0,"real cloth sweep cleans effective cells");cloth.Rescue(rag.id);
            Check(cloth.dirt.FindAll(d=>d.cleaned).Count==cleaned&&!rag.touched&&rag.pushed==0&&rag.socket==-1,"rescue preserves dirt ledger and resets push evidence");

            var coffee=new RunV9(2,42,new ConfigV9{coffeeTolerance=0});coffee.Begin();var cups=coffee.people.FindAll(p=>p.kind==PersonKind.Coffee);
            coffee.player=cups[0].position-Vector2.up*.95f;coffee.Tick(.2f,Vector2.up);int countAtFailure=coffee.tasks[0].count;
            coffee.player=cups[1].position-Vector2.up*.95f;coffee.velocity=Vector2.zero;coffee.Tick(.2f,Vector2.up);
            Check(coffee.tasks[0].state==TaskState.Failed&&coffee.tasks[0].count==countAtFailure,"failed coffee task freezes count for other cups");

            var capsule=new RunV9(1,42);capsule.Begin();var machine=capsule.fixtures.Find(f=>f.kind==FixtureKind.Capsule);
            capsule.player=machine.position-Vector2.up*3;capsule.Tick(.25f,Vector2.zero);float phase=capsule.BeatPhase;double elapsed=capsule.elapsed;
            capsule.Pause();capsule.Tick(5,Vector2.up);Check(capsule.elapsed==elapsed&&capsule.BeatPhase==phase,"pause freezes capsule rhythm");capsule.Resume();
            capsule.player=machine.position-Vector2.up*6;capsule.Tick(.1f,Vector2.zero);capsule.player=machine.position-Vector2.up*1.1f;capsule.Tick(1f/60,Vector2.up);
            Check(capsule.BeatPhase<.03f&&capsule.tasks[1].count==0,"return resets beat before a fresh contact can score");

            var metro=new RunV9(1,42);metro.Begin();metro.player=metro.map.platform+Vector2.left*3;metro.Tick(3.5f,Vector2.zero);
            Check(metro.trainPhase==TrainPhase.Alighting&&metro.tasks[0].state==TaskState.Active,"waiting starts silent alighting window");
            var passengers=metro.people.FindAll(p=>p.kind==PersonKind.Alighting);Check(passengers.Count>0,"train creates passengers");
            foreach(var passenger in passengers)passenger.exited=true;var last=passengers[passengers.Count-1];last.exited=false;last.position=last.target;
            metro.player=last.target;metro.Tick(1f/60,Vector2.zero);
            Check(metro.tasks[0].state==TaskState.Failed,"last exiting passenger contact precedes window completion");
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
            public bool success;public int stage,seed,completed;public string[] taskStates;public string validationScope="machine-input mechanism replay, not human timing acceptance";public double elapsed;public int contacts;public Vector2 stoppedAt;public string reason;
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
                if(!MoveTo(run,goal,20,out string navigationReason)){result.reason=navigationReason;break;}
                if(run.phase!=RunPhase.Playing)break;
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
        /// Defaults to the shipped deadline. Diagnostic callers must explicitly supply any override.</summary>
        public static ReplayResult ReplayStreetTasks(int seed,ConfigV9 config=null)
        {
            var run=new RunV9(0,seed,config);run.Begin();
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
                // Detect a real new contact rather than assuming a fixed input duration reached it.
                int before=ContactCount(run,f.id);double started=run.elapsed;
                while(ContactCount(run,f.id)==before&&run.phase==RunPhase.Playing&&run.elapsed-started<3)run.Tick(1f/60,Vector2.up);
                if(ContactCount(run,f.id)==before)return EndReplay(run,result,"Flip contact not reached id="+f.id);
                if(!MoveTo(run,f.position-Vector2.up*2.5f,10,out reason))return EndReplay(run,result,reason);
            }
            if(!run.tasks[2].done)return EndReplay(run,result,"Flip input route failed to solve");
            if(!MoveTo(run,run.map.exit,30,out reason))return EndReplay(run,result,reason);
            return EndReplay(run,result,"Street input route finished; not timing acceptance");
        }
        static bool CrowdRisk(RunV9 run,PersonV9 person)
        {
            return !person.exited&&((person.kind==PersonKind.Coffee&&!person.spilled)||person.kind==PersonKind.Alighting||
                (run.stage==0&&run.tasks[0].state==TaskState.Active));
        }
        static void NavigationTick(RunV9 run,Vector2 input)
        {
            // React only to a currently visible tell. This is the same Shift input as human play.
            bool brace=false;
            foreach(var person in run.people)
            {
                if(person.exited||person.kind==PersonKind.Alighting||
                    (person.actionPhase!=NpcActionPhase.Windup&&person.actionPhase!=NpcActionPhase.Charging)||
                    Vector2.Distance(person.position,run.player)>10)continue;
                Vector2 delta=person.position-run.player;bool visible=true;
                for(float distance=.5f;distance<delta.magnitude;distance+=.5f)
                    if(!run.map.IsWalkable(run.player+delta.normalized*distance,.05f)){visible=false;break;}
                if(visible){brace=true;break;}
            }
            run.SetBracing(brace);run.Tick(1f/60,input);
        }
        static bool MoveTo(RunV9 run,Vector2 goal,double waypointBudget,out string reason)
        {
            reason=null;List<Vector2> path=null;int pointIndex=0;
            double replanAt=run.elapsed,progressAt=run.elapsed;Vector2 progressPosition=run.player;
            while(Vector2.Distance(run.player,goal)>.35f&&run.phase==RunPhase.Playing)
            {
                if(path==null||pointIndex>=path.Count||run.elapsed>=replanAt)
                {
                    path=NavigationPath(run,goal);pointIndex=0;replanAt=run.elapsed+1;
                    if(path==null)
                    {
                        // A moving body is not a disconnected map. Wait briefly using real simulation time.
                        for(int wait=0;wait<15&&run.phase==RunPhase.Playing;wait++)NavigationTick(run,Vector2.zero);
                        if(run.elapsed-progressAt>waypointBudget){reason="No dynamic clearance to "+goal+" at "+run.player;return false;}
                        continue;
                    }
                    if(path.Count==0)break; // Same grid cell: finish the fractional approach below.
                }
                while(pointIndex<path.Count&&Vector2.Distance(run.player,path[pointIndex])<=.25f)pointIndex++;
                if(pointIndex>=path.Count){path=null;continue;}
                Vector2 point=path[pointIndex];NavigationTick(run,(point-run.player).normalized);
                if(Vector2.Distance(run.player,progressPosition)>.4f){progressPosition=run.player;progressAt=run.elapsed;}
                if(run.elapsed-progressAt>waypointBudget){reason="Blocked at "+run.player+" waypoint="+point;return false;}
            }
            if(run.phase!=RunPhase.Playing)return run.success;
            // Fractional target and bicycle braking are handled by physical input, never assignment.
            double finalStart=run.elapsed;
            while((Vector2.Distance(run.player,goal)>.06f||run.velocity.magnitude>.12f)&&run.phase==RunPhase.Playing)
            {
                Vector2 delta=goal-run.player;float speed=run.bicycle?run.config.bikeSpeed:run.config.walkSpeed;
                NavigationTick(run,Vector2.ClampMagnitude((delta*3-run.velocity*.8f)/speed,1));
                if(run.elapsed-finalStart>10){reason="Precise approach blocked at "+run.player+" goal="+goal;return false;}
            }
            run.SetBracing(false);run.Tick(.15f,Vector2.zero);return run.phase==RunPhase.Playing||run.success;
        }
        static ReplayResult EndReplay(RunV9 run,ReplayResult result,string reason)
        {
            result.completed=run.Completed;result.taskStates=new[]{run.tasks[0].state.ToString(),run.tasks[1].state.ToString(),run.tasks[2].state.ToString()};
            result.success=run.success&&run.Completed==3;result.elapsed=run.elapsed;result.contacts=run.bumps;result.stoppedAt=run.player;result.reason=reason;result.trace.AddRange(run.telemetry);return result;
        }
        static int ContactCount(RunV9 run,int id){int result=0;foreach(var entry in run.telemetry)if(entry.type=="contact"&&entry.id==id)result++;return result;}
        public static ReplayResult ReplayAllTasks(int stage,int seed,ConfigV9 config=null)
        {
            if(stage==0)return ReplayStreetTasks(seed,config);
            // Actual configured deadlines apply. Neither task state nor positions are assigned.
            var run=new RunV9(stage,seed,config);run.Begin();
            var result=new ReplayResult{stage=stage,seed=seed};string reason;
            if(stage==1)
            {
                FixtureV9 machine=null;foreach(var f in run.fixtures)if(f.kind==FixtureKind.Capsule)machine=f;
                for(int tries=0;tries<30&&!run.tasks[1].done&&run.phase==RunPhase.Playing;tries++)
                {
                    if(!MoveTo(run,machine.position-Vector2.up*1.6f,20,out reason))return EndReplay(run,result,reason);
                    // Read the visible beat phase, wait for a fresh safe approach window, then touch.
                    double waitStart=run.elapsed;
                    while((run.BeatPhase<.28f||run.BeatPhase>.32f)&&run.elapsed-waitStart<3&&run.phase==RunPhase.Playing)run.Tick(1f/60,Vector2.zero);
                    for(int i=0;i<30&&run.phase==RunPhase.Playing;i++)run.Tick(1f/60,Vector2.up);
                    for(int i=0;i<25&&run.phase==RunPhase.Playing;i++)run.Tick(1f/60,Vector2.down);
                }
                if(!run.tasks[1].done)return EndReplay(run,result,"Capsule replay exhausted 30 real contact attempts");
                // Register in the lower-left waiting area before the passenger fan reaches it.
                // The old (-3.3,+1) direct route crossed the left passenger ray after doors opened.
                if(!MoveTo(run,run.map.platform+new Vector2(-3,-2),20,out reason))return EndReplay(run,result,reason);
                run.Tick(.5f,Vector2.zero);
                // Move outside that fan first, then go up its left side. No diagonal crossing.
                if(!MoveTo(run,run.map.platform+new Vector2(-4,-2),20,out reason))return EndReplay(run,result,reason);
                if(!MoveTo(run,run.map.platform+new Vector2(-4,1),20,out reason))return EndReplay(run,result,reason);
                if(!MoveTo(run,run.map.platform+new Vector2(-3.7f,.8f),20,out reason))return EndReplay(run,result,reason);
                double began=run.elapsed;while(run.trainPhase!=TrainPhase.Boarding&&run.phase==RunPhase.Playing&&run.elapsed-began<40)run.Tick(1f/60,Vector2.zero);
                if(run.trainPhase!=TrainPhase.Boarding)return EndReplay(run,result,"Train did not finish alighting");
                if(!MoveTo(run,run.map.trainDoor,20,out reason))return EndReplay(run,result,reason);
                for(int i=0;i<90;i++)run.Tick(1f/60,Vector2.zero);
                if(!run.tasks[0].done)return EndReplay(run,result,"Waiting/boarding task failed on actual passenger contact or registration");
                BodyV9 cloth=run.bodies[0];Vector2 center=run.map.tasks[2].center;
                Vector2[] sweep={center+new Vector2(-4,-.3f),center+new Vector2(4,-.3f),center+new Vector2(4,.3f),center+new Vector2(-4,.3f)};
                foreach(var target in sweep)if(!PushTo(run,cloth,target,.12f,90,out reason))return EndReplay(run,result,reason);
                // Chase any unclean effective-area cells left by imperfect physical steering.
                for(int pass=0;pass<12&&!run.tasks[2].done;pass++)
                {
                    DirtV9 remaining=null;float nearest=float.MaxValue;
                    foreach(var patch in run.dirt)if(!patch.cleaned&&Vector2.Distance(patch.position,cloth.position)<nearest){remaining=patch;nearest=Vector2.Distance(patch.position,cloth.position);}
                    if(remaining!=null&&!PushTo(run,cloth,remaining.position,.12f,45,out reason))return EndReplay(run,result,reason);
                }
                if(!run.tasks[2].done)return EndReplay(run,result,"Cloth sweep still has unclean cells="+(run.tasks[2].limit-run.tasks[2].count));
            }
            else
            {
                if(!MoveTo(run,run.map.tasks[1].start,20,out reason))return EndReplay(run,result,reason);
                run.Tick(.5f,Vector2.zero);
                foreach(var desk in run.fixtures)if(desk.kind==FixtureKind.FileDesk)
                {if(!MoveTo(run,desk.position,20,out reason))return EndReplay(run,result,reason);run.Tick(.5f,Vector2.zero);}
                if(!run.tasks[1].done)return EndReplay(run,result,"File delivery failed/timed out using actual movement");
                // Clear front row first, matching the far sockets so later chairs retain access.
                var order=new List<BodyV9>(run.bodies);order.Sort((a,b)=>b.position.y.CompareTo(a.position.y));
                foreach(var chair in order)
                {
                    FixtureV9 selected=null;float best=float.MaxValue;
                    foreach(var socket in run.fixtures)if(socket.kind==FixtureKind.ChairSocket&&!socket.occupied)
                    {
                        float score=Mathf.Abs(socket.position.x-chair.position.x)*20-socket.position.y;
                        if(score<best){best=score;selected=socket;}
                    }
                    if(selected==null)return EndReplay(run,result,"No free chair socket");
                    if(!PushTo(run,chair,selected.position,.3f,90,out reason))return EndReplay(run,result,reason);
                    run.Tick(.6f,Vector2.zero);
                    if(!chair.done)return EndReplay(run,result,"Chair did not stabilize in free socket id="+chair.id);
                }
                if(!run.tasks[2].done)return EndReplay(run,result,"Chairs incomplete");
            }
            if(!MoveTo(run,run.map.exit,20,out reason))return EndReplay(run,result,reason);
            return EndReplay(run,result,"All-task machine input route ended; inspect task states, not human timing acceptance");
        }
        static bool PushTo(RunV9 run,BodyV9 body,Vector2 target,float tolerance,double seconds,out string reason)
        {
            reason=null;double start=run.elapsed;Vector2 previous=body.position;double progressAt=run.elapsed;
            while(!body.done&&Vector2.Distance(body.position,target)>tolerance&&run.phase==RunPhase.Playing)
            {
                Vector2 direction=(target-body.position).normalized;
                Vector2 behind=body.position-direction*(body.radius+RunV9.PlayerRadius+.45f);
                if(!MoveTo(run,behind,15,out reason))return false;
                for(int i=0;i<120&&!body.done&&Vector2.Distance(body.position,target)>tolerance&&run.phase==RunPhase.Playing;i++)
                {
                    direction=(target-body.position).normalized;Vector2 fromBody=run.player-body.position;
                    if(Vector2.Dot(fromBody,direction)>.05f)break;
                    Vector2 lateral=fromBody-direction*Vector2.Dot(fromBody,direction);
                    if(lateral.magnitude>.65f)break;
                    run.Tick(1f/60,Vector2.ClampMagnitude(direction*Mathf.Clamp(Vector2.Distance(body.position,target)*2,.15f,1)-lateral*1.5f,1));
                }
                if(Vector2.Distance(previous,body.position)>.1f){previous=body.position;progressAt=run.elapsed;}
                if(run.elapsed-start>seconds||run.elapsed-progressAt>15){reason="Push stalled id="+body.id+" at="+body.position+" target="+target;return false;}
            }
            run.Tick(.15f,Vector2.zero);return run.phase==RunPhase.Playing||run.success;
        }
        struct AvoidanceSnapshot
        {
            public Vector2 position,predicted;public float radiusSquared;
        }
        static List<Vector2> NavigationPath(RunV9 run,Vector2 goal)
        {
            var map=run.map;Vector2Int root=MapData.Cell(run.player),end=MapData.Cell(goal);
            var nearbyRisk=new List<AvoidanceSnapshot>();
            foreach(var person in run.people)if(CrowdRisk(run,person)&&(person.position-run.player).sqrMagnitude<81)
            {
                float margin=person.radius+RunV9.PlayerRadius+.3f;
                nearbyRisk.Add(new AvoidanceSnapshot{position=person.position,predicted=person.position+Vector2.ClampMagnitude(person.velocity*.3f,.6f),radiusSquared=margin*margin});
            }
            var queue=new Queue<Vector2Int>();var previous=new Dictionary<Vector2Int,Vector2Int>();queue.Enqueue(root);previous[root]=root;
            Vector2Int[] directions={Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
            while(queue.Count>0)
            {
                var cell=queue.Dequeue();if(cell==end)break;
                foreach(var delta in directions)
                {
                    var next=cell+delta;// Use the actual player footprint. A .55 clearance at integer centres removed
                    // the usable outer row of every 7-wide lane, falsely sealing crowd passages.
                    if(previous.ContainsKey(next)||!map.IsWalkable(next,RunV9.PlayerRadius+.01f))continue;
                    Vector2 p=next;bool blocked=false;
                    foreach(var f in run.fixtures)if((f.kind==FixtureKind.FlipBoard||f.kind==FixtureKind.Capsule)&&Vector2.Distance(p,f.position)<f.radius+RunV9.PlayerRadius+.1f)blocked=true;
                    foreach(var b in run.bodies)if(!b.done&&Vector2.Distance(p,b.position)<b.radius+RunV9.PlayerRadius+.12f)blocked=true;
                    // Only nearby, current occupancy enters this short-lived plan. Home positions
                    // describe spawning, not invisible permanent walls. Replan after one second.
                    foreach(var c in nearbyRisk)
                    {
                        if((p-c.position).sqrMagnitude<c.radiusSquared||(p-c.predicted).sqrMagnitude<c.radiusSquared){blocked=true;break;}
                    }
                    if(blocked&&next!=end)continue;previous[next]=cell;queue.Enqueue(next);
                }
            }
            if(!previous.ContainsKey(end))return null;
            var result=new List<Vector2>();for(var at=end;at!=root;at=previous[at])result.Add(at);result.Reverse();return result;
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
