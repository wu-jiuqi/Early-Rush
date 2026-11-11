using System;
using System.Collections.Generic;
using UnityEngine;
namespace EarlyRush.V9
{
    public sealed partial class PersonV9
    {
        public string sourceArtId;
        public bool authoredCoffee;
        public string authoredCoffeeId,authoredCoffeeSprite;
        public Vector2 authoredCoffeeA,authoredCoffeeB;
    }
    public sealed partial class RunV9
    {
        // Call after SetupCrowd. Old maps and other stages are unchanged.
        void SetupAuthoredCoffee()
        {
            if(stage!=2||map.authoredLayout==null)return;
            foreach(var p in people)if(p.authoredCoffee) return; // idempotent setup
            // Authored O1 has exactly three cups, not the generic crowd's forty.
            foreach(var p in people)if(p.kind==PersonKind.Coffee)
            {p.kind=PersonKind.Ordinary;p.coffeeAIInitialized=false;p.canRush=false;p.art="people/man";}
            var actors=map.authoredLayout.TaskWorld("O1").actors;
            int nextId=3000;foreach(var p in people)nextId=Mathf.Max(nextId,p.id+1);
            for(int i=0;i<actors.Length;i++)
            {
                var a=actors[i];
                if(!map.IsWalkable(a.position,.48f)||!AuthoredCoffeeLineClear(a.a,a.b,.48f))
                    throw new InvalidOperationException("Authored coffee route lacks physical clearance: "+a.id);
                var p=new PersonV9{id=nextId++,kind=PersonKind.Coffee,position=a.position,home=a.position,target=a.b,destination=a.b,
                    radius=.42f,hasPedestrianAI=true,aiState=PedestrianState.Walking,profile=PedestrianProfile.Cautious,canRush=false,
                    authoredCoffee=true,authoredCoffeeId=a.id,authoredCoffeeSprite=a.sprite,authoredCoffeeA=a.a,authoredCoffeeB=a.b,
                    art=a.sprite.Contains("female")?"people/coffee-female":"people/coffee-male",walkingSpeed=a.speed,
                    pedestrianRandom=unchecked((uint)(seed*397+(nextId-1)*7919))|1u};
                // Reuse CoffeeAI's guard/spill/desk lifecycle. Its generic public-space trip is
                // immediately replaced by the verified source endpoints below, never simulated.
                InitializeCoffeeAI(p);
                p.coffeeDesk=a.b;p.coffeeCarrySpeed=a.speed;p.walkingSpeed=a.speed;
                p.coffeeGoingToDesk=true;p.coffeeState=CoffeeState.Carrying;
                if(!TryChooseAuthoredCoffeeDestination(p))throw new InvalidOperationException("Cannot initialize authored coffee route: "+a.id);
                people.Add(p);
            }
        }
        // Hook at the start of TryChooseCoffeeDestination for authoredCoffee only.
        bool TryChooseAuthoredCoffeeDestination(PersonV9 p)
        {
            if(!p.authoredCoffee)return false;
            if(p.spilled)
            {
                if(p.coffeeState==CoffeeState.SpillReaction||p.coffeeState==CoffeeState.EmptyAtDesk)return false;
                p.coffeeState=CoffeeState.ReturningEmpty;p.coffeeGoingToDesk=true;
            }
            var goal=p.coffeeGoingToDesk?p.authoredCoffeeB:p.authoredCoffeeA;
            if(!AuthoredCoffeeLineClear(p.position,goal,.48f))
            {p.aiState=PedestrianState.Waiting;p.waitLeft=.5f;StopCoffeeCoworker(p);return false;}
            if((goal-p.destination).sqrMagnitude>.001f)p.destinationChanges++;
            p.destination=p.target=goal;p.pedestrianPath=new List<Vector2>{p.position,goal};p.pathIndex=1;p.routePlans++;
            p.aiState=PedestrianState.Walking;p.waitLeft=0;p.blockedTime=0;p.progressDistance=float.MaxValue;
            p.preferredDirection=(goal-p.position).normalized;p.decisionLeft=0;return true;
        }
        bool AuthoredCoffeeLineClear(Vector2 from,Vector2 to,float radius)
        {
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(from,to)/.2f));
            for(int i=0;i<=steps;i++)if(!map.IsWalkable(Vector2.Lerp(from,to,i/(float)steps),radius))return false;
            return true;
        }
        // Hook immediately AFTER StepCoffeeAI in UpdateCrowdPeople. Existing dash flight and
        // overlap handling run first; existing CoffeeAI owns guards, spills, pauses and empty cups.
        bool StepAuthoredCoffee(PersonV9 p,float dt)
        {
            if(!p.authoredCoffee)return false;
            if(p.aiState==PedestrianState.Waiting)
            {p.waitLeft-=dt;StopCoffeeCoworker(p);if(p.waitLeft<=0)TryChooseAuthoredCoffeeDestination(p);return true;}
            if(Vector2.Distance(p.position,p.destination)<.2f){CoffeeArrival(p);return true;}
            var before=p.position;var delta=Vector2.ClampMagnitude(p.destination-p.position,p.walkingSpeed*dt);
            var next=Resolve(before,delta,Mathf.Max(.48f,p.radius));
            // These three authored task actors may traverse their own O1 reservation, but never
            // ignore physical walls, runtime solids, pushables, fixtures or other pedestrians.
            foreach(var body in bodies)if(Vector2.Distance(next,body.position)<p.radius+body.radius+.3f&&Vector2.Distance(next,body.position)<Vector2.Distance(before,body.position))next=before;
            foreach(var fixture in fixtures)if(Vector2.Distance(next,fixture.position)<p.radius+fixture.radius+.2f&&Vector2.Distance(next,fixture.position)<Vector2.Distance(before,fixture.position))next=before;
            foreach(var other in people)if(other!=p&&!other.exited&&Vector2.Distance(next,other.position)<p.radius+other.radius+.03f&&Vector2.Distance(next,other.position)<Vector2.Distance(before,other.position))next=before;
            p.position=next;p.velocity=(next-before)/Mathf.Max(.00001f,dt);p.steeringVelocity=p.velocity;p.preferredDirection=(p.destination-p.position).normalized;
            p.aiState=(next-before).sqrMagnitude>.000001f?PedestrianState.Walking:PedestrianState.Yielding;
            if(Vector2.Distance(p.position,player)<p.radius+PlayerRadius)
            {Contact(p.id);if(!HandlePlayerDashContact(p))HandleNpcActionContact(p);}
            return true;
        }
    }
}
