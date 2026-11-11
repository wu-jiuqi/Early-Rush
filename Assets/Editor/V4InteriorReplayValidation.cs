using System;
using System.IO;
using UnityEngine;
using EarlyRush.V9;
public static class V4InteriorReplayValidation {
 public static void MetroMainline(){Replay(1,false);}
 public static void OfficeMainline(){Replay(2,false);}
 public static void MetroTasks(){Replay(1,true);}
 public static void OfficeTasks(){Replay(2,true);}
 static void Replay(int stage,bool tasks){
  var config=new ConfigV9{stageLimit=600};
  var r=tasks?RushTestsV9.ReplayAllTasks(stage,91027,config):RushTestsV9.ReplayMainline(stage,91027,config);
  string report=$"Actual Tick ArtV4 stage={stage} tasks={tasks} success={r.success} elapsed={r.elapsed} completed={r.completed} stopped={r.stoppedAt} reason={r.reason}\nReal configured crowd and input; not human timing or performance evidence.";
  Directory.CreateDirectory("Builds/EarlyRush-V14/QA/v4-interior");File.WriteAllText($"Builds/EarlyRush-V14/QA/v4-interior/stage-{stage}-{(tasks?"tasks":"mainline")}.txt",report);Debug.Log(report);
  if(!r.success||(tasks&&r.completed!=3))throw new Exception("ArtV4 replay did not pass; see report");
 }
}
