using System;
using System.Collections.Generic;
using UnityEngine;
namespace EarlyRush.V8
{
    public static class RushV8Tests
    {
        static int count;
        static void Check(bool pass,string text){count++;if(!pass)throw new Exception("V8 test failed: "+text);}
        public static int RunAll()
        {
            count=0;
            for(int n=0;n<=3;n++) {Check(RunV8.Score(true,n)==(n==0?1:n==3?3:2),"on-time stars "+n);Check(RunV8.Score(false,n)==(n==0?0:n==3?2:1),"late record "+n);}
            for(int stage=0;stage<3;stage++)
            {
                var r=new RunV8(stage,81026);Check(r.errands.Count==3,"three tasks");r.Begin();Vector2 p=r.player;
                r.Tick(1,Vector2.up,false,false);Check(r.player.y>p.y,"W up");p=r.player;r.Tick(.5f,Vector2.right,false,false);Check(r.player.x>p.x,"D right");
                double time=r.elapsed;r.Pause();r.Tick(5,Vector2.up,true,false);Check(Math.Abs(r.elapsed-time)<.0001,"pause timer");r.Resume();
                r.Tick(.8f,Vector2.zero,false,false);Check(r.elapsed-time>.79,"long frame consumed");
                r.elapsed=r.limit-.1;r.player=r.map.exit;r.Tick(.1f,Vector2.zero,false,false);Check(r.success&&r.phase==RunPhase.Results,"deadline exit valid");
                var exact=new RunV8(stage,81026);exact.Begin();exact.elapsed=exact.limit;exact.player=exact.map.exit;exact.Tick(.02f,Vector2.zero,false,false);Check(exact.success,"exact deadline at exit succeeds");
                var after=new RunV8(stage,81026);after.Begin();after.elapsed=after.limit+.001;after.player=after.map.exit;after.Tick(.02f,Vector2.zero,false,false);Check(!after.success,"after deadline exit fails");
                var late=new RunV8(stage,81026);late.Begin();late.elapsed=late.limit-.02;late.Tick(.04f,Vector2.zero,false,false);Check(!late.success&&late.phase==RunPhase.Results,"timeout");
                late.player=late.map.exit;late.Tick(1,Vector2.zero,false,false);Check(!late.success,"late exit cannot reverse result");
                var taskRun=new RunV8(stage,81026);taskRun.Begin();
                foreach(var t in taskRun.errands)
                {
                    if(t.kind==TaskKind.CrowdPurchase)
                    {
                        // Entering the purchase zone without collision must not complete.
                        taskRun.player=t.station;taskRun.Tick(.5f,Vector2.zero,false,false);Check(!t.done,"purchase requires push");
                        foreach(var c in taskRun.crowd)if(c.task==t.index&&!c.credited)
                        {
                            taskRun.player=c.position-Vector2.up*.8f;taskRun.velocity=Vector2.zero;taskRun.Tick(.25f,Vector2.up,false,false);
                            if(t.people.Count>=2)break;
                        }
                        Check(t.people.Count>=2,"two distinct people credited");taskRun.player=t.station;taskRun.velocity=Vector2.zero;taskRun.Tick(.6f,Vector2.zero,false,false);Check(t.done,"purchase completes");
                    }
                    else
                    {
                        foreach(var b in t.bodies)
                        {
                            taskRun.player=taskRun.map.spawn;taskRun.velocity=Vector2.zero;b.velocity=Vector2.zero;b.position=b.target;taskRun.Tick(.5f,Vector2.zero,false,false);Check(!b.done,"uncontacted body cannot complete");b.position=b.home;
                            taskRun.player=b.position-Vector2.up*1.6f;taskRun.velocity=Vector2.zero;taskRun.Tick(.65f,Vector2.up,false,false);Check(b.touched&&b.pushed>.05,"player contact pushes body");
                            // Fixture placement isolates ordered checkpoint and stable target predicates; not an end-to-end route drive.
                            for(int i=0;i<b.checkpoints.Length;i++){b.position=b.checkpoints[i];b.velocity=Vector2.zero;taskRun.player=taskRun.map.spawn;taskRun.Tick(.02f,Vector2.zero,false,false);Check(b.checkpoint==i+1,"ordered checkpoint");}
                            b.position=b.target;b.velocity=Vector2.zero;taskRun.player=taskRun.map.spawn;taskRun.velocity=Vector2.zero;taskRun.Tick(.5f,Vector2.zero,false,false);Check(b.done,"body parked");
                        }
                        taskRun.Tick(.02f,Vector2.zero,false,false);Check(t.done,"all bodies task complete");
                    }
                }
                Check(taskRun.Completed==3,"all stage tasks");taskRun.player=taskRun.map.exit;taskRun.Tick(.02f,Vector2.zero,false,false);Check(taskRun.success&&taskRun.stars==3,"three-star result");
                taskRun.Rescue(0);Check(taskRun.Completed==3,"rescue cannot erase completed");
            }
            Debug.Log("EARLY_RUSH_V8_LOGIC_TESTS_PASS count="+count);return count;
        }
    }
}
