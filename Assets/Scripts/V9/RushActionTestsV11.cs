using System;
using UnityEngine;

namespace EarlyRush.V9
{
    /// <summary>Local action fixtures. These do not replace full-route input replay or human playtesting.</summary>
    public static class RushActionTestsV11
    {
        static int assertions;
        static void Check(bool value,string message)
        {assertions++;if(!value)throw new InvalidOperationException("V11 action test failed: "+message);}

        public static int RunAll()
        {
            assertions=0;DashAndBrace();NpcCharge();CollisionSafety();RuleInteractions();
            Debug.Log("V11_ACTION_ASSERTIONS count="+assertions);return assertions;
        }

        static RunV9 EmptyRun()
        {
            // These local mechanism fixtures have always removed the natural population.
            // Avoid planning hundreds of trips just to discard them before the first tick.
            var run=new RunV9(0,91026,new ConfigV9{pedestrianStreetCount=0,pedestrianMetroCount=0,pedestrianOfficeCount=0});
            run.Begin();return run;
        }

        static void DashAndBrace()
        {
            var ready=new RunV9(0,91026);Check(!ready.TryDash(Vector2.up),"ready phase rejects dash");
            var run=EmptyRun();Vector2 start=run.player;
            Check(run.TryDash(Vector2.right)&&run.IsDashing,"directional dash starts");
            Check(!run.TryDash(Vector2.up),"dash cannot retrigger while active");run.Tick(.08f,Vector2.up);
            Check(run.player.x>start.x+.4f&&Mathf.Abs(run.player.y-start.y)<.01f,"dash locks initial direction");
            Check(run.facing==Vector2.right,"dash facing follows locked direction");
            run.Pause();double time=run.elapsed;float readyBefore=run.DashReady01;Vector2 paused=run.player;run.Tick(5,Vector2.up);
            Check(run.elapsed==time&&run.player==paused&&run.DashReady01==readyBefore,"pause freezes dash and cooldown");
            Check(!run.TryDash(Vector2.up),"paused phase rejects dash");run.Resume();run.SetBracing(true);
            Check(run.IsBracing&&!run.IsDashing,"brace cancels dash");
            Check(!run.TryDash(Vector2.up),"brace takes priority over dash");run.Tick(.1f,Vector2.up);
            Check(run.velocity.magnitude<=run.config.walkSpeed*run.config.braceSpeedMultiplier+.001f,"brace has bounded low walking speed");
            run.bicycle=true;run.Tick(.1f,Vector2.up);
            Check(run.bicycle&&run.velocity.magnitude<=run.config.bikeSpeed*run.config.braceSpeedMultiplier+.001f,"bracing on bike slows without dismounting");
            run.SetBracing(false);run.Tick(run.config.dashCooldown+.1f,Vector2.zero);
            Check(run.DashReady01==1&&run.TryDash(Vector2.up),"cooldown restores one new dash");
            var retry=new RunV9(0,91026);
            Check(!retry.IsDashing&&!retry.IsBracing&&!retry.IsAirborne&&retry.DashReady01==1&&retry.PlayerLaunchCount==0,"stage retry has fresh action state");
        }

        static RunV9 ChargeFixture(out PersonV9 attacker)
        {
            var run=EmptyRun();attacker=null;
            for(float distance=18;distance<run.map.PathLength-18;distance+=2)
            {
                Vector2 point=run.map.PointAtDistance(distance),direction=(run.map.PointAtDistance(distance+1)-point).normalized;
                Vector2 player=point+direction*2.2f;bool clear=direction.sqrMagnitude>.9f;
                for(float along=0;along<4;along+=.3f)
                {
                    Vector2 sample=point+direction*along;
                    if(run.IsCrowdProtected(sample,1)||!run.map.IsWalkable(sample,.7f))clear=false;
                }
                if(!clear)continue;
                run.player=player;
                attacker=new PersonV9{id=8100,kind=PersonKind.Ordinary,position=point,home=point,target=player,canRush=true,actionInitialized=true};
                run.people.Add(attacker);return run;
            }
            throw new InvalidOperationException("No unprotected straight lane for V11 action fixture");
        }

        static void Until(RunV9 run,Func<bool> condition,float seconds=3)
        {
            int steps=Mathf.CeilToInt(seconds*60);
            for(int i=0;i<steps&&!condition()&&run.phase==RunPhase.Playing;i++)run.Tick(1f/60,Vector2.zero);
        }

        static void NpcCharge()
        {
            var telegraph=ChargeFixture(out var attacker);telegraph.Tick(1f/60,Vector2.zero);
            Check(attacker.actionPhase==NpcActionPhase.Windup,"ordinary capable NPC telegraphs before charging");
            Check(telegraph.PlayerLaunchCount==0&&!telegraph.IsAirborne,"windup cannot launch player");
            Vector2 direction=attacker.chargeDirection;telegraph.Tick(.2f,new Vector2(-direction.y,direction.x));
            Check(attacker.chargeDirection==direction&&telegraph.NpcWindup01(attacker)>0,"windup locks direction and exposes progress");
            telegraph.Pause();float progress=attacker.actionTime;telegraph.Tick(10,Vector2.zero);
            Check(attacker.actionTime==progress&&attacker.actionPhase==NpcActionPhase.Windup,"pause freezes NPC windup");

            var hit=ChargeFixture(out var charger);Until(hit,()=>hit.PlayerLaunchCount>0);
            Check(hit.PlayerLaunchCount==1&&hit.IsAirborne,"unbraced active charge causes one short launch");
            Check(charger.actionPhase==NpcActionPhase.Recovery&&charger.chargeHitConsumed,"charge enters recovery after hit");
            int contacts=hit.bumps;double time=hit.elapsed;hit.Tick(.08f,Vector2.zero);
            Check(hit.PlayerHeight>0&&hit.PlayerHeight<=hit.config.playerHopHeight+.001f,"small visual hop is bounded");
            Check(hit.elapsed-time<.081&&hit.elapsed-time>.079,"impact never directly deducts stage time");
            hit.Pause();float height=hit.PlayerHeight,protection=hit.HitProtection01;hit.Tick(5,Vector2.zero);
            Check(hit.PlayerHeight==height&&hit.HitProtection01==protection,"pause freezes hop and protection");hit.Resume();
            var second=new PersonV9{id=8101,kind=PersonKind.Ordinary,canRush=true,position=hit.player-Vector2.right*.8f,chargeDirection=Vector2.right,actionPhase=NpcActionPhase.Charging};
            hit.people.Add(second);Check(!hit.HandleNpcActionContact(second)&&hit.PlayerLaunchCount==1,"protection rejects another NPC launch");
            Check(!hit.HandleNpcActionContact(charger)&&hit.PlayerLaunchCount==1,"same charge cannot repeatedly launch");
            hit.Tick(.5f,Vector2.zero);
            Check(!hit.IsAirborne&&hit.PlayerHeight==0,"launch always ends without permanent stun");
            Vector2 landed=hit.player;hit.Tick(.2f,Vector2.up);
            Check(Vector2.Distance(landed,hit.player)>.1f,"movement resumes after bounded recovery");

            var brace=ChargeFixture(out var blocked);brace.SetBracing(true);Until(brace,()=>brace.BraceBlockCount>0);
            Check(brace.BraceBlockCount==1&&brace.PlayerLaunchCount==0&&!brace.IsAirborne,"steady stance blocks active launch");
            Check(brace.BraceFeedback01>0&&blocked.actionPhase==NpcActionPhase.Recovery,"blocked charge has short feedback and recovery");
            brace.bicycle=true;Check(brace.bicycle&&brace.IsBracing,"brace block preserves bicycle state");

            var simultaneous=ChargeFixture(out var first);
            var other=new PersonV9{id=8102,kind=PersonKind.Ordinary,canRush=true,position=first.position-Vector2.up,home=first.position,actionInitialized=true};
            simultaneous.people.Add(other);simultaneous.Tick(1f/60,Vector2.zero);
            int active=0;foreach(var person in simultaneous.people)if(person.actionPhase==NpcActionPhase.Windup||person.actionPhase==NpcActionPhase.Charging)active++;
            Check(active==1,"at most one NPC prepares or charges at once");

            foreach(var kind in new[]{PersonKind.Coffee,PersonKind.Alighting})
            {
                var special=ChargeFixture(out var person);person.kind=kind;
                special.Tick(.5f,Vector2.zero);
                Check(person.actionPhase==NpcActionPhase.Cruise&&special.NpcChargeCount==0&&special.PlayerLaunchCount==0,"special NPC never attacks: "+kind);
            }
        }

        static void CollisionSafety()
        {
            var wall=EmptyRun();Vector2 edge=wall.map.ResolveMove(wall.player,Vector2.right*20,RunV9.PlayerRadius);
            wall.player=edge-Vector2.right*.1f;wall.TryDash(Vector2.right);wall.Tick(.4f,Vector2.right);
            // ResolveMove's coarse search ends up to .2 units before the geometric boundary.
            Check(wall.map.IsWalkable(wall.player,RunV9.PlayerRadius)&&wall.player.x<=edge.x+.21f,"dash cannot tunnel through map wall");
            var red=EmptyRun();red.elapsed=red.config.greenDuration+1;red.player=red.map.signal-Vector2.up;
            red.TryDash(Vector2.up);red.Tick(.3f,Vector2.up);
            Check(red.player.y<=red.map.signal.y-RunV9.PlayerRadius,"dash respects red stopping line");
            var knockback=ChargeFixture(out var charger);Until(knockback,()=>knockback.IsAirborne);
            Check(knockback.IsAirborne,"collision-safety fixture receives actual attack");
            // Relocate the existing ground footprint for a local boundary fixture; no task/award is set.
            knockback.elapsed=knockback.config.greenDuration+1;knockback.player=knockback.map.signal-Vector2.up*.6f;
            knockback.Tick(.3f,Vector2.zero);
            Check(knockback.player.y<=knockback.map.signal.y-RunV9.PlayerRadius&&knockback.map.IsWalkable(knockback.player,RunV9.PlayerRadius),"knockback uses same red-light and wall constraints");
            var protect=ChargeFixture(out var cancel);protect.Tick(1f/60,Vector2.zero);protect.player=protect.map.tasks[0].center;protect.Tick(1f/60,Vector2.zero);
            Check(cancel.actionPhase==NpcActionPhase.Recovery&&cancel.chargeCancelled&&protect.PlayerLaunchCount==0,"entering task protection cancels windup");
        }

        static void RuleInteractions()
        {
            var breakfast=ChargeFixture(out var attacker);Vector2 arenaPlayer=breakfast.player;
            breakfast.player=breakfast.map.tasks[0].center;breakfast.Tick(.5f,Vector2.zero);
            Check(breakfast.tasks[0].state==TaskState.Active&&breakfast.config.breakfastTolerance==8,"brace/breakfast fixture uses the shipped eight-contact threshold");
            breakfast.player=arenaPlayer;breakfast.SetBracing(true);Until(breakfast,()=>breakfast.BraceBlockCount>0);
            Check(breakfast.BraceBlockCount==1&&breakfast.PlayerLaunchCount==0&&breakfast.tasks[0].count==1&&breakfast.tasks[0].state==TaskState.Active,
                "steady stance prevents launch but still records the real breakfast collision");

            var coffee=new RunV9(2,91026,new ConfigV9{coffeeTolerance=0});
            var cup=coffee.people.Find(person=>person.kind==PersonKind.Coffee);Check(cup!=null,"coffee interaction fixture finds a real cup holder");
            coffee.people.Clear();coffee.people.Add(cup);coffee.Begin();coffee.SetBracing(true);
            coffee.player=cup.position-Vector2.up*.8f;coffee.Tick(.1f,Vector2.up);
            Check(coffee.IsBracing&&cup.spilled&&coffee.tasks[0].count==1&&coffee.tasks[0].state==TaskState.Failed,
                "steady contact still spills coffee and applies its task threshold");

            var flip=EmptyRun();var board=flip.fixtures.Find(fixture=>fixture.kind==FixtureKind.FlipBoard);
            var previous=(bool[])flip.boards.Clone();flip.player=board.position-Vector2.up*2.4f;
            Check(flip.TryDash(Vector2.up),"fixture approach starts through normal dash input");flip.Tick(.8f,Vector2.up);
            int contacts=0;foreach(var entry in flip.telemetry)if(entry.type=="contact"&&entry.id==board.id)contacts++;
            bool pattern=true;for(int index=0;index<flip.boards.Length;index++)
                pattern&=flip.boards[index]==(Mathf.Abs(index-board.index)<=1?!previous[index]:previous[index]);
            Check(contacts==1&&pattern,"dash followed by sustained fixture contact flips each linked board only once");
        }
    }
}
