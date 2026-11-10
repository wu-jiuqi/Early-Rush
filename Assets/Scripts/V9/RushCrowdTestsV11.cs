using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush.V9
{
    public static class RushCrowdTestsV11
    {
        static int assertions;
        static void Check(bool condition,string description){assertions++;if(!condition)throw new Exception("V11 crowd: "+description);}
        public static int RunAll()
        {
            assertions=0;
            foreach(int seed in new[]{91026,91027,91028})for(int stage=0;stage<3;stage++)
            {
                var run=new RunV9(stage,seed);var positions=new Dictionary<int,Vector2>();int forward=0,counter=0,rushers=0,peak=0;
                foreach(var c in run.people)
                {
                    positions[c.id]=c.position;
                    Check(run.map.IsWalkable(c.position,c.radius)&&!run.IsCrowdProtected(c.position,c.radius),"spawn outside reserved space");
                    if(c.flow==CrowdFlow.Forward)forward++;if(c.flow==CrowdFlow.Counter)counter++;if(c.canRush)rushers++;
                    Check(!c.canRush||c.kind==PersonKind.Ordinary,"special roles excluded from attackers");
                }
                for(float along=10;along<run.map.PathLength-10;along+=5)peak=Mathf.Max(peak,run.VisibleCrowdCount(run.map.PointAtDistance(along)));
                Check(run.people.Count>=70&&forward>15&&counter>15&&rushers>3,"two-way population and attackers exist");
                Check(peak>=10&&peak<=45,"local peak density has useful lower and upper bounds");
                var bodyPositions=new List<Vector2>();foreach(var b in run.bodies)bodyPositions.Add(b.position);
                run.Begin();run.Tick(10,Vector2.zero);
                int moving=0;foreach(var c in run.people)
                {
                    Check(run.map.IsWalkable(c.position,c.radius),"moving NPC stays on walkable ground");
                    Check(!run.IsCrowdProtected(c.position,c.radius),"moving NPC respects task working space");
                    if(Vector2.Distance(positions[c.id],c.position)>1)moving++;
                }
                Check(moving>run.people.Count*.65f,"population truly walks instead of hovering at home");
                for(int i=0;i<run.bodies.Count;i++)Check(run.bodies[i].position==bodyPositions[i]&&!run.bodies[i].touched&&run.bodies[i].pushed==0,"NPCs cannot manipulate task bodies");
                foreach(var patch in run.dirt)Check(!patch.cleaned,"crowds cannot clean dirt");
                run.Pause();double time=run.elapsed;var paused=new List<Vector2>();foreach(var c in run.people)paused.Add(c.position);run.Tick(5,Vector2.up);
                Check(run.elapsed==time,"pause freezes simulation time");for(int i=0;i<paused.Count;i++)Check(paused[i]==run.people[i].position,"pause freezes crowd");
                Debug.Log("V11_CROWD seed="+seed+" stage="+stage+" count="+run.people.Count+" forward="+forward+" counter="+counter+" attackers="+rushers+" peakVisible="+peak+" moved10s="+moving);
            }
            return assertions;
        }
    }
}
