using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EarlyRush.V9
{
    /// <summary>Player smoke and visual capture. Static task views are not playthrough evidence.</summary>
    public sealed class RushRuntimeCheckV10 : MonoBehaviour
    {
        readonly List<string> errors=new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Attach()
        {
            if(Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--v10-runtime-qa"))
                new GameObject("V10 Runtime Validation").AddComponent<RushRuntimeCheckV10>();
        }
        void Awake(){Application.runInBackground=true;Application.logMessageReceived+=OnLog;}
        void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        IEnumerator Start()
        {
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../QA/runtime"));Directory.CreateDirectory(output);
            yield return null;
            var game=FindFirstObjectByType<RushPlayV9>();
            if(!game){errors.Add("RushPlayV9 not loaded");Finish(output);yield break;}
            for(int stage=0;stage<3;stage++)
            {
                game.Load(stage,91026);yield return null;yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output,"stage-"+stage+"-ready.png"));
                game.run.Begin();yield return new WaitForSecondsRealtime(.2f);
                double before=game.run.elapsed;game.run.Pause();yield return new WaitForSecondsRealtime(.2f);
                if(game.run.elapsed!=before)errors.Add("Pause consumed simulation time stage="+stage);
                game.run.Resume();
                for(int site=0;site<3;site++)
                {
                    // Diagnostic framing only. Input replays in BuildEarlyRushV10 validate gameplay.
                    game.run.player=game.run.map.tasks[site].center+Vector2.down*3;
                    game.run.velocity=Vector2.zero;
                    game.FrameForReview();
                    yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(output,"stage-"+stage+"-site-"+site+"-static.png"));
                }
            }
            yield return new WaitForSecondsRealtime(.5f);Finish(output);
        }
        void Finish(string output)
        {
            File.WriteAllText(Path.Combine(output,"smoke.txt"),"Rendered three stages with HUD, pause clock checks and static task views.\nThis is not a human playthrough.\nErrors: "+errors.Count+"\n"+string.Join("\n",errors));
            Debug.Log("V10_RUNTIME_CHECK errors="+errors.Count);Application.Quit(errors.Count==0?0:1);
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
