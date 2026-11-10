using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush.V9
{
    /// <summary>Real key-down timing and Tick motion; isolated actors do not replace route QA.</summary>
    public static class RushDashFeelTestsV13
    {
        static int assertions;
        static void Check(bool value,string message)
        {assertions++;if(!value)throw new InvalidOperationException("V13 dash feel test failed: "+message);}

        static RunV9 Empty(ConfigV9 config=null)
        {
            if(config==null)config=new ConfigV9();
            config.pedestrianStreetCount=config.pedestrianMetroCount=config.pedestrianOfficeCount=0;
            var run=new RunV9(0,91026,config);run.Begin();return run;
        }

        public static int RunAll()
        {
            assertions=0;
            BufferedTiming();BurstAndRelease();Cancellation();RealImpactCancelsBuffer();BufferedCrowdImpacts();RepeatedBoundaryContacts();
            Debug.Log("V13_DASH_FEEL_ASSERTIONS count="+assertions);return assertions;
        }

        static void BufferedTiming()
        {
            foreach(int fps in new[]{30,60,120})
            {
                var run=Empty();float dt=1f/fps;var starts=new List<double>();int previous=0,nextPress=0;
                double[] presses={0,.55,1.20,1.85};
                for(int frame=0;frame<fps*3;frame++)
                {
                    if(nextPress<presses.Length&&run.elapsed+1e-5>=presses[nextPress])
                    {Check(run.PressDash(Vector2.up),"late key-down is accepted at "+fps+" FPS, press "+nextPress);nextPress++;}
                    run.Tick(dt,Vector2.zero);
                    if(run.DashStartCount!=previous){starts.Add(run.elapsed);previous=run.DashStartCount;}
                }
                Check(starts.Count==4&&run.DashStartCount==4,"four presses create exactly four bursts, with no held-key repetition");
                for(int i=1;i<starts.Count;i++)
                    Check(Math.Abs((starts[i]-starts[i-1])-run.config.dashCooldown)<dt+.02,"buffered burst respects actual cooldown at "+fps+" FPS");
                Check(!run.DashBuffered&&!run.IsDashing&&run.DashReady01==1,"unused time never generates another burst");
            }

            var early=Empty();early.PressDash(Vector2.up);early.Tick(.3f,Vector2.zero);
            Check(!early.PressDash(Vector2.right)&&!early.DashBuffered,"a press well before the buffer window is not stored indefinitely");
            early.Tick(1,Vector2.zero);Check(early.DashStartCount==1,"rejected early input cannot fire later");

            var direction=Empty();direction.PressDash(Vector2.up);direction.Tick(.54f,Vector2.zero);
            Check(direction.PressDash(Vector2.right)&&direction.DashBuffered&&direction.DashBuffer01>0,"cooldown tail exposes one buffered input");
            Check(direction.PressDash(Vector2.left),"a newer key-down replaces the pending direction");
            direction.Tick(.13f,Vector2.up);
            Check(direction.DashStartCount==2&&direction.velocity.x<0&&Mathf.Abs(direction.velocity.y)<.01f,
                "buffer captures the latest key-down direction, rather than subsequent movement input");
            direction.Tick(1,Vector2.zero);Check(direction.DashStartCount==2,"multiple pending key-downs do not stack a queue");
        }

        static void BurstAndRelease()
        {
            foreach(bool bike in new[]{false,true})
            {
                var released=Empty();released.bicycle=bike;Vector2 start=released.player;
                Check(released.PressDash(Vector2.up),"walking and bicycle burst starts");
                float peak=bike?released.config.bikeDashSpeed:released.config.dashSpeed;
                Check(Mathf.Abs(released.velocity.magnitude-peak)<.001f,"burst responds immediately at its configured initial speed");
                released.Tick(released.config.dashDuration+.02f,Vector2.zero);
                float distance=Vector2.Distance(start,released.player);
                Check(!released.IsDashing&&released.velocity.magnitude<.001f,"released direction reaches rest at the end of the burst");
                Check(distance>1.6f&&distance<peak*released.config.dashDuration+.02f,"braking keeps a useful, bounded burst distance");
                Vector2 stopped=released.player;released.Tick(.25f,Vector2.zero);
                Check(Vector2.Distance(stopped,released.player)<.001f,"there is no uncontrolled post-dash drift");

                var held=Empty();held.bicycle=bike;held.PressDash(Vector2.up);
                held.Tick(held.config.dashDuration+.02f,Vector2.up);
                float normal=bike?held.config.bikeSpeed:held.config.walkSpeed;
                Check(!held.IsDashing&&Mathf.Abs(held.velocity.magnitude-normal)<.01f,"held direction flows into normal walking or riding speed");
                Check(held.bicycle==bike,"dash never changes bicycle ownership");
            }
        }

        static RunV9 Pending()
        {
            var run=Empty();run.PressDash(Vector2.up);run.Tick(.54f,Vector2.zero);
            Check(run.PressDash(Vector2.right)&&run.DashBuffered,"cancellation fixture has a real queued key-down");return run;
        }

        static void Cancellation()
        {
            var brace=Pending();brace.SetBracing(true);
            Check(!brace.DashBuffered&&!brace.IsDashing&&!brace.PressDash(Vector2.up),"Shift cancels the queue and rejects simultaneous Space");
            brace.Tick(.3f,Vector2.up);brace.SetBracing(false);brace.Tick(.3f,Vector2.zero);
            Check(brace.DashStartCount==1,"releasing Shift does not resurrect its cancelled input");
            Check(brace.PressDash(Vector2.up),"a fresh key-down works after releasing Shift");brace.SetBracing(true);
            Check(!brace.IsDashing&&brace.velocity.magnitude<=brace.config.walkSpeed*brace.config.braceSpeedMultiplier+.001f,
                "Shift immediately limits the stronger active burst");

            var pause=Pending();pause.Pause();float cooldown=pause.DashReady01;pause.Tick(5,Vector2.right);
            Check(!pause.DashBuffered&&!pause.IsDashing&&!pause.IsBracing&&pause.DashReady01==cooldown,"pause discards input intent while freezing cooldown");
            Check(!pause.PressDash(Vector2.right),"paused key-down cannot enter the buffer");pause.Resume();pause.Tick(.3f,Vector2.zero);
            Check(pause.DashStartCount==1&&!pause.IsDashing,"resume cannot fire a key pressed before pause");
            Check(pause.PressDash(Vector2.right),"new key-down works after resume");pause.Pause();pause.Resume();pause.Tick(.1f,Vector2.zero);
            Check(!pause.IsDashing&&pause.velocity==Vector2.zero,"pausing an active burst does not resume its old impulse");

            var finished=Empty(new ConfigV9{stageLimit=.60});finished.PressDash(Vector2.up);finished.Tick(.54f,Vector2.zero);
            Check(finished.PressDash(Vector2.right)&&finished.DashBuffered,"result cleanup fixture queues before the real deadline");
            finished.Tick(.2f,Vector2.zero);
            Check(finished.phase==RunPhase.Results&&!finished.DashBuffered&&!finished.IsDashing&&!finished.IsBracing&&finished.velocity==Vector2.zero,
                "actual stage settlement clears queued and held action intent");
            Check(!finished.PressDash(Vector2.right)&&finished.DashStartCount==1,"results reject new and previously buffered presses");
            var retry=Empty();Check(retry.DashStartCount==0&&!retry.DashBuffered&&retry.DashReady01==1,"retry owns a fresh queue, counter and cooldown");
        }

        static void ActionLane(RunV9 run,out Vector2 start,out Vector2 direction)
        {
            start=Vector2.zero;direction=Vector2.zero;
            for(float at=18;at<run.map.PathLength-18;at+=2)
            {
                Vector2 p=run.map.PointAtDistance(at),d=(run.map.PointAtDistance(at+1)-p).normalized;bool clear=d.sqrMagnitude>.9f;
                for(float n=-3;n<10;n+=.25f)if(run.IsCrowdProtected(p+d*n,1)||!run.map.IsWalkable(p+d*n,.7f))clear=false;
                if(clear){start=p;direction=d;return;}
            }
            throw new InvalidOperationException("No unprotected corridor for dash feel fixture");
        }

        static void RealImpactCancelsBuffer()
        {
            // A shorter fixture windup places a real charge hit inside the 140 ms input window.
            // The live game's charge settings, collision rules and recovery remain unchanged.
            var run=Empty(new ConfigV9{npcWindupDuration=.15f});ActionLane(run,out var start,out var direction);
            Check(direction.sqrMagnitude>.9f,"impact fixture finds a real unprotected corridor");
            run.player=start;run.PressDash(direction);run.Tick(.24f,Vector2.zero);
            var attacker=new PersonV9{id=8910,kind=PersonKind.Ordinary,canRush=true,actionInitialized=true,
                position=run.player-direction*2.2f,home=run.player-direction*2.2f};run.people.Add(attacker);
            run.Tick(.30f,Vector2.zero);
            Check(run.PressDash(direction)&&run.DashBuffered,"key is queued while the NPC is actually preparing or charging");
            for(int frame=0;frame<30&&!run.IsAirborne;frame++)run.Tick(1f/60,Vector2.zero);
            Check(run.PlayerLaunchCount==1&&run.IsAirborne&&!run.DashBuffered,"real NPC impact cancels a queued dash before readiness");
            Check(!run.PressDash(direction)&&!run.DashBuffered,"airborne presses cannot queue an involuntary landing dash");
            run.Tick(run.config.playerKnockbackDuration+.2f,Vector2.zero);
            Check(!run.IsAirborne&&run.DashStartCount==1,"landing restores control without firing cancelled input");
        }

        static void BufferedCrowdImpacts()
        {
            var run=Empty();ActionLane(run,out var start,out var direction);run.player=start;
            var first=new PersonV9{id=8920,kind=PersonKind.Ordinary,position=start+direction*1.3f};
            var second=new PersonV9{id=8921,kind=PersonKind.Ordinary,position=start+direction*5.2f};
            run.people.Add(first);run.people.Add(second);
            run.PressDash(direction);run.Tick(.54f,direction);
            Check(run.DashLaunchCount==1&&run.DashStartCount==1,"first burst physically launches only the nearer commuter");
            Check(run.PressDash(direction)&&run.DashBuffered,"second burst uses the real cooldown-tail key-down");
            run.Tick(.35f,direction);
            Check(run.DashStartCount==2&&run.DashLaunchCount==2&&run.PlayerLaunchCount==0,
                "buffered burst enters the same collision pipeline and launches the next commuter");
            bool firstContact=false,secondContact=false;
            foreach(var entry in run.telemetry)if(entry.type=="contact")
            {firstContact|=entry.id==first.id;secondContact|=entry.id==second.id;}
            Check(firstContact&&secondContact,"both real dash impacts retain normal contact accounting");
        }

        static void RepeatedBoundaryContacts()
        {
            var red=Empty();red.elapsed=red.config.greenDuration+1;red.player=red.map.signal-Vector2.up;
            for(int burst=0;burst<4;burst++)
            {
                Check(red.PressDash(Vector2.up),"repeated ready press at red signal is accepted");
                red.Tick(red.config.dashCooldown+.02f,Vector2.up);
                Check(red.player.y<=red.map.signal.y-RunV9.PlayerRadius,"higher dash frequency never passes a red stopping line");
            }
            var board=Empty();var fixture=board.fixtures.Find(f=>f.kind==FixtureKind.FlipBoard);
            board.player=fixture.position-Vector2.up*2.4f;
            for(int burst=0;burst<3;burst++){board.PressDash(Vector2.up);board.Tick(board.config.dashCooldown+.02f,Vector2.up);}
            int contacts=0;foreach(var entry in board.telemetry)if(entry.type=="contact"&&entry.id==fixture.id)contacts++;
            Check(contacts==1,"repeated bursts into a held board contact do not bypass fixture debounce");
        }
    }
}
