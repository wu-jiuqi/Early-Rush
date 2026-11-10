using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace EarlyRush.V8
{
    // Explicit command-line opt-in. Never enabled during normal play.
    public sealed class RushV8RuntimeQA : MonoBehaviour
    {
        IEnumerator Start()
        {
            string root=Path.Combine(Application.dataPath,"..","QA");Directory.CreateDirectory(root);
            string error=null;int tests=0;
            try{tests=RushV8Tests.RunAll();}catch(Exception e){error=e.ToString();}
            if(error!=null){File.WriteAllText(Path.Combine(root,"runtime-fail.txt"),error);Debug.LogError(error);Application.Quit(2);yield break;}
            var game=GetComponent<RushPlayV8>();
            for(int stage=0;stage<3;stage++)
            {
                game.Load(stage,81026);game.run.Begin();
                game.run.player=game.run.map.tasks[0].center;game.run.velocity=Vector2.zero;
                for(int i=0;i<60;i++){game.RenderFrame();yield return null;}
                yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(root,"stage-"+stage+".png"));yield return null;
            }
            File.WriteAllText(Path.Combine(root,"runtime-pass.txt"),"Logic tests: "+tests+". Three stage presentation screenshots. Fixture-based task tests, NOT human playability or full-route traversal.");
            yield return new WaitForSecondsRealtime(1);Application.Quit(0);
        }
    }
}
