using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using EarlyRush.V9;
public static class V4StreetValidation {
 const string Folder="Builds/EarlyRush-V14/QA/v4-street";
 [MenuItem("Early Rush/Art V4/Validate Optional Street Geometry")]
 public static void Validate(){Directory.CreateDirectory(Folder);var map=RushMapV9.Create(0,RushV4StreetMap.Seed);if(!map.Validate(out string reason))throw new Exception(reason);int checkedPoints=0;foreach(var b in map.structures){if(map.IsWalkable(b.bounds.center,.01f))throw new Exception("Thin footprint penetrable at "+b.bounds.center);checkedPoints++;}var text="Optional seed 91027 ONLY. Geometry validation passed.\nrouteUnits="+map.PathLength+" modules="+map.modules.Count+" cells="+map.cells.Count+" blockedFootprints="+checkedPoints+"\n40 px/unit; original 91026 and legacy validations untouched. Conservative circle-in-room/capsule containment and exact footprint collisions. No replay/human timing claim.\n";File.WriteAllText(Folder+"/geometry.txt",text);Debug.Log(text);}
 [MenuItem("Early Rush/Art V4/Replay Optional Street Default Deadline")]
 public static void ReplayDefault(){Replay(420,"default-420");}
 [MenuItem("Early Rush/Art V4/Replay Optional Street Diagnostic 600s")]
 public static void ReplayDiagnostic(){Replay(600,"diagnostic-600");}
 public static void ReplayDiagnostic720(){Replay(720,"diagnostic-720");}
 static void Replay(double limit,string label){Validate();var config=new ConfigV9{stageLimit=limit};var main=RushTestsV9.ReplayMainline(0,RushV4StreetMap.Seed,config);var all=RushTestsV9.ReplayAllTasks(0,RushV4StreetMap.Seed,new ConfigV9{stageLimit=limit});string text="Optional 91027 actual Tick replay. deadline="+limit+"; population and task mechanisms unchanged. Diagnostic 600 is NOT shipped balance or human timing evidence.\nmainline success="+main.success+" seconds="+main.elapsed+" stop="+main.stoppedAt+" reason="+main.reason+"\nallTasks success="+all.success+" seconds="+all.elapsed+" completed="+all.completed+" states="+string.Join("|",all.taskStates??new string[0])+" stop="+all.stoppedAt+" reason="+all.reason+"\n";File.WriteAllText(Folder+"/"+label+".txt",text);Debug.Log(text);if(!main.success||!all.success)Debug.LogWarning("V4 optional replay has failures. Read report; not release-qualified.");}
}
