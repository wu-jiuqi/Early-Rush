using System;
using System.Collections.Generic;
using UnityEngine;
namespace EarlyRush.V9 {
public sealed partial class RunV9 {
 readonly Dictionary<int,Rect> authoredChairTargets=new Dictionary<int,Rect>();
 readonly Dictionary<int,int> authoredChairSockets=new Dictionary<int,int>();
 int RequiredChairs=>map.authoredLayout!=null&&stage==2?map.authoredLayout.TaskById("O3").pickups.Length:config.chairCount;
 int RequiredFiles=>map.authoredLayout!=null&&stage==2?map.authoredLayout.TaskById("O2").deliveries.Length:config.fileCount;
 bool SetupAuthoredInterior(){
  var layout=map.authoredLayout;if(layout==null||stage==0)return false;
  if(stage==1){
   tasks[1].limit=config.capsuleGoal;AddFixture(FixtureKind.Capsule,map.tasks[1].center,"metro/vending");
   var clean=layout.TaskWorld("M3");AddBody(BodyKind.Cloth,map.tasks[2].start,"tasks/cloth");
   foreach(var patch in clean.patches)for(float x=patch.bounds.xMin+.2f;x<patch.bounds.xMax;x+=.4f)for(float y=patch.bounds.yMin+.2f;y<patch.bounds.yMax;y+=.4f){var p=new Vector2(x,y);if(map.IsWalkable(p,.05f))dirt.Add(new DirtV9{position=p});}
   if(dirt.Count==0)throw new Exception("Authored metro has no accessible dirt samples");tasks[2].limit=dirt.Count;
  }else{
   tasks[0].state=TaskState.Active;tasks[0].limit=config.coffeeTolerance;
   var files=layout.TaskWorld("O2");tasks[1].limit=RequiredFiles;AddFixture(FixtureKind.FilePickup,files.position,"office/filing-cabinet");
   for(int i=0;i<files.deliveries.Length;i++)AddFixture(FixtureKind.FileDesk,files.deliveries[i].position,"office/desk-chair",i);
   var chairs=layout.TaskWorld("O3");tasks[2].limit=RequiredChairs;
   for(int i=0;i<chairs.pickups.Length;i++){
    var p=chairs.pickups[i];if(!p.target.HasValue)throw new Exception("Chair missing authored target");
    AddFixture(FixtureKind.ChairSocket,p.target.Value.center,"ui/selection",i);var socket=fixtures[fixtures.Count-1];
    AddBody(BodyKind.Chair,p.position,"tasks/chair");var body=bodies[bodies.Count-1];
    authoredChairTargets[socket.id]=p.target.Value;authoredChairSockets[body.id]=socket.id;
   }
  }
  SetupCrowd();SetupAuthoredCoffee();return true;
 }
 bool ChairFits(BodyV9 body,FixtureV9 socket){
  if(!authoredChairSockets.TryGetValue(body.id,out var expected))return Vector2.Distance(body.position,socket.position)<.9f;
  if(expected!=socket.id||!authoredChairTargets.TryGetValue(socket.id,out var r))return false;
  return body.position.x-body.radius>=r.xMin&&body.position.x+body.radius<=r.xMax&&body.position.y-body.radius>=r.yMin&&body.position.y+body.radius<=r.yMax;
 }
 void ApplyAuthoredTrainArrival(){
  if(map.authoredLayout==null||stage!=1)return;var portal=map.authoredLayout.PortalWorld("M1");if(portal==null)return;
  if(!map.IsWalkable(portal.to,PlayerRadius))throw new Exception("Train arrival outside valid geometry");
  CancelPlayerActionInput();player=portal.to;velocity=Vector2.zero;boost=0;Log("train-portal-arrival");
 }
}
}
