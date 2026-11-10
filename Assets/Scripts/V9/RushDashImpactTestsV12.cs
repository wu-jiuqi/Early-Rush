using System;
using UnityEngine;

namespace EarlyRush.V9
{
    /// <summary>Real dash input/contact fixtures; placements are setup, never launch outcomes.</summary>
    public static class RushDashImpactTestsV12
    {
        static int assertions;
        static void Check(bool value,string message){assertions++;if(!value)throw new InvalidOperationException("V12 dash impact: "+message);}
        static RunV9 Empty(int stage=0)
        {var run=new RunV9(stage,91026,new ConfigV9{pedestrianStreetCount=0,pedestrianMetroCount=0,pedestrianOfficeCount=0});run.Begin();return run;}
        static void Arena(RunV9 run,out Vector2 start,out Vector2 direction)
        {
            for(float along=18;along<run.map.PathLength-18;along+=2)
            {
                Vector2 p=run.map.PointAtDistance(along),d=(run.map.PointAtDistance(along+1)-p).normalized;bool good=d.sqrMagnitude>.9f;
                for(float step=0;step<=10;step+=.5f)
                    if(run.IsCrowdProtected(p+d*step,1)||!run.map.IsWalkable(p+d*step,.7f))good=false;
                if(good){start=p;direction=d;return;}
            }
            throw new InvalidOperationException("No safe corridor for dash impact fixture");
        }
        static PersonV9 Person(RunV9 run,Vector2 at,PersonKind kind=PersonKind.Ordinary)
        {
            var person=new PersonV9{id=8800+run.people.Count,position=at,home=at,target=at,kind=kind,radius=.42f};run.people.Add(person);return person;
        }
        static void Until(RunV9 run,Func<bool> condition,Vector2 input,int frames=90)
        {for(int frame=0;frame<frames&&!condition()&&run.phase==RunPhase.Playing;frame++)run.Tick(1f/60,input);}
        public static int RunAll()
        {
            assertions=0;MultipleAndPause();SpecialRolesAndBrace();Boundaries();OwnTripAfterLanding();DashWinsCounterCharge();
            Debug.Log("V12_DASH_IMPACT_ASSERTIONS count="+assertions);return assertions;
        }

        static void MultipleAndPause()
        {
            var run=Empty();Arena(run,out var start,out var direction);Vector2 side=new Vector2(direction.y,-direction.x);run.player=start;
            var first=Person(run,start+direction*1.2f+side*.55f);var second=Person(run,start+direction*1.2f-side*.55f);
            Check(run.TryDash(direction),"real dash begins");Until(run,()=>run.DashLaunchCount==2,direction);
            Check(run.DashLaunchCount==2&&first.dashAirborne&&second.dashAirborne,"one dash can launch two ordinary commuters through contact");
            run.Tick(.12f,Vector2.zero);
            Check(first.dashHeight>.5f&&first.dashHeight<=1.3001f&&second.dashHeight>.5f,"exaggerated visual hop stays bounded");
            Check(run.DashImpactFeedback01>0&&run.DashLaunchCount==2,"impact feedback exists without repeated launch from sustained contact");
            run.Pause();Vector2 position=first.position;float height=first.dashHeight;double time=run.elapsed;run.Tick(5,direction);
            Check(first.position==position&&first.dashHeight==height&&run.elapsed==time,"pause freezes NPC flight and height");run.Resume();
            Until(run,()=>!first.dashAirborne,Vector2.zero);
            Check(first.dashHeight==0&&first.dashLandingFeedback01>0&&first.dashFlightDistance<=1.9001f,"landing is timely, grounded and displacement-capped");
            Check(run.DashLaunchCount==2,"same dash never launches the same NPC twice");
            var retry=Empty();Check(retry.DashLaunchCount==0&&retry.DashImpactFeedback01==0,"new stage resets dash impact feedback and counts");
        }

        static void SpecialRolesAndBrace()
        {
            var brace=Empty();Arena(brace,out var start,out var direction);brace.player=start;var ordinary=Person(brace,start+direction*1.2f);
            brace.SetBracing(true);Check(!brace.TryDash(direction),"Shift takes priority over player dash impact");brace.Tick(.8f,direction);
            Check(brace.DashLaunchCount==0&&!ordinary.dashAirborne,"steady pushing never launches ordinary NPCs");
            var coffee=Empty(2);Arena(coffee,out start,out direction);coffee.player=start;var cup=Person(coffee,start+direction*1.2f,PersonKind.Coffee);
            coffee.TryDash(direction);coffee.Tick(.3f,direction);
            Check(coffee.DashLaunchCount==0&&!cup.dashAirborne&&cup.spilled&&coffee.tasks[0].count==1,"dash contact spills coffee normally without launching its holder");
            var metro=Empty(1);Arena(metro,out start,out direction);metro.player=start;var passenger=Person(metro,start+direction*1.2f,PersonKind.Alighting);passenger.target=passenger.position+direction*8;
            metro.TryDash(direction);metro.Tick(.3f,direction);
            Check(metro.DashLaunchCount==0&&!passenger.dashAirborne,"alighting passenger cannot be launched by player dash");
        }

        static void Boundaries()
        {
            var wall=Empty();Arena(wall,out var start,out var direction);Vector2 side=new Vector2(direction.y,-direction.x);
            Vector2 edge=wall.map.ResolveMove(start,side*20,.48f);var person=Person(wall,edge-side*.15f);wall.player=person.position-side*1.2f;
            wall.TryDash(side);wall.Tick(.8f,Vector2.zero);
            Check(wall.DashLaunchCount==1&&wall.map.IsWalkable(person.position,person.radius)&&Vector2.Dot(person.position-edge,side)<.21f,
                "flight and dash pushing cannot cross a map wall; launches="+wall.DashLaunchCount+" body="+person.position+" radius="+person.radius+" coarseEdge="+edge);

            var protection=Empty();PersonV9 protectedPerson=null;
            for(float along=18;along<protection.map.PathLength-18;along+=.5f)
            {
                Vector2 p=protection.map.PointAtDistance(along),d=(protection.map.PointAtDistance(along+1)-p).normalized;
                if(protection.IsCrowdProtected(p,.65f)||protection.IsCrowdProtected(p-d*1.3f,.7f)||!protection.IsCrowdProtected(p+d*1.4f,.65f)||!protection.map.IsWalkable(p-d*1.3f,.6f))continue;
                protectedPerson=Person(protection,p);protection.player=p-d*1.3f;protection.TryDash(d);break;
            }
            Check(protectedPerson!=null,"protected-boundary fixture has a reachable approach");bool enteredWhileFlying=false;Vector2 flightStart=protectedPerson.position;
            for(int frame=0;frame<48;frame++)
            {protection.Tick(1f/60,Vector2.zero);if(protectedPerson.dashAirborne&&protection.IsCrowdProtected(protectedPerson.position,protectedPerson.radius+.2f))enteredWhileFlying=true;}
            Check(protection.DashLaunchCount==1&&!enteredWhileFlying,"NPC flight stops before a task or traffic protected area; launches="+protection.DashLaunchCount+" start="+flightStart+" current="+protectedPerson.position+" enteredAirborne="+enteredWhileFlying);

            var inside=Empty();inside.player=inside.map.tasks[0].center;var safe=Person(inside,inside.player+Vector2.up*1.2f);
            inside.TryDash(Vector2.up);inside.Tick(.3f,Vector2.up);
            Check(inside.bumps>0&&inside.DashLaunchCount==0&&!safe.dashAirborne,"real contact inside task workspace remains an ordinary contact");

            var red=new RunV9(0,91026);var waiting=red.people.Find(p=>p.kind==PersonKind.Ordinary&&!p.canRush);red.people.Clear();red.people.Add(waiting);
            waiting.position=red.map.signal-Vector2.up*.55f;red.AssignPedestrianDestination(waiting,red.map.signal+Vector2.up*8);
            red.player=waiting.position-Vector2.up*1.2f;red.Begin();red.elapsed=red.config.greenDuration+1;red.TryDash(Vector2.up);red.Tick(.3f,Vector2.up);
            Check(waiting.position.y<=red.map.signal.y-.48f&&!waiting.dashAirborne&&red.DashLaunchCount==0,"dash cannot push a waiting commuter across a red stop line");
        }

        static void OwnTripAfterLanding()
        {
            var run=new RunV9(0,91026);var person=run.people.Find(p=>p.kind==PersonKind.Ordinary&&!p.canRush);run.people.Clear();run.people.Add(person);
            Arena(run,out var start,out var direction);person.position=start+direction*1.2f;Vector2 goal=start+direction*9;
            Check(run.AssignPedestrianDestination(person,goal),"generated pedestrian has a reachable original destination");int plans=person.routePlans;
            run.player=start;run.Begin();run.TryDash(direction);Until(run,()=>person.dashAirborne,direction);
            Check(person.dashAirborne&&run.DashLaunchCount==1,"real dash launches a generated AI pedestrian");run.SetBracing(true);
            Vector2 previous=person.position;float largestStep=0;
            for(int frame=0;frame<60&&person.dashAirborne;frame++)
            {run.Tick(1f/60,Vector2.zero);largestStep=Mathf.Max(largestStep,Vector2.Distance(previous,person.position));previous=person.position;}
            Check(!person.dashAirborne&&person.destination==goal&&person.routePlans>plans,"landing replans from actual position toward the same personal goal");
            Check(largestStep<.3f&&person.dashFlightDistance<=1.9001f,"generated NPC flight is continuous and bounded instead of teleporting");
            float remaining=Vector2.Distance(person.position,goal);run.Tick(1,Vector2.zero);
            Check(Vector2.Distance(person.position,goal)<remaining-.2f,"pedestrian resumes their own trip after landing");
        }

        static void DashWinsCounterCharge()
        {
            var run=Empty();Arena(run,out var start,out var direction);run.player=start;var opponent=Person(run,start+direction*2.2f);
            opponent.canRush=true;opponent.actionInitialized=true;
            Until(run,()=>opponent.actionPhase==NpcActionPhase.Charging,Vector2.zero);
            Check(opponent.actionPhase==NpcActionPhase.Charging,"opponent enters charging through real windup ticks");
            Check(run.TryDash(direction),"player counters using actual dash input");Until(run,()=>run.DashLaunchCount>0,direction);
            Check(run.DashLaunchCount==1&&run.PlayerLaunchCount==0&&opponent.dashAirborne&&!run.IsAirborne,"player dash priority is independent of which actor moved into contact");
        }
    }
}
