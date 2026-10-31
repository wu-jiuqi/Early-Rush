using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace EarlyRush
{
    /// <summary>Opt-in Player verification. Uses the same input channel and simulation as human play.</summary>
    public class RushQA : MonoBehaviour
    {
        [Serializable] public class Check { public string name; public bool passed; public string evidence; }
        [Serializable] public class RunEvidence
        {
            public string name; public float totalSeconds; public float[] splitSeconds; public int collisions;
            public int[] splitCollisions; public int completedTransitions; public bool reachedResults;
        }
        [Serializable] public class Report
        {
            public string utc, engine, device, graphicsApi, driver="Normal input channel; real-time simulation; no position or time overrides";
            public bool passed; public float elapsedRealSeconds, meanFrameMilliseconds, approximateFPS, worstFrameMilliseconds;
            public int sampledFrames, failureCount; public List<Check> checks=new List<Check>();
            public List<RunEvidence> runs=new List<RunEvidence>(); public List<string> screenshots=new List<string>();
        }
        RushGame game;
        Report report=new Report();
        string output;
        float began, sampleTotal, sampleWorst;
        int sampleFrames;
        bool finished;
        const float Grid=.5f, Margin=.54f;

        void Start()
        {
            game=GetComponent<RushGame>();
            output=Path.Combine(Path.GetDirectoryName(Application.dataPath),"QA");
            foreach(string arg in Environment.GetCommandLineArgs())
                if(arg.StartsWith("--qa-output=",StringComparison.Ordinal))output=arg.Substring("--qa-output=".Length);
            Directory.CreateDirectory(output);
            began=Time.realtimeSinceStartup;
            report.utc=DateTime.UtcNow.ToString("o"); report.engine=Application.unityVersion;
            report.device=SystemInfo.graphicsDeviceName;report.graphicsApi=SystemInfo.graphicsDeviceType.ToString();
            game.QAInput=true;
            Application.logMessageReceived+=OnLog;
            StartCoroutine(Suite());
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
        void OnLog(string condition,string trace,LogType type)
        {
            if(type==LogType.Exception || type==LogType.Error)
                CheckThat("No runtime exception: "+condition,false,trace);
        }
        void Update()
        {
            if(finished)return;
            if(game!=null&&game.State==RushState.Playing&&Time.unscaledDeltaTime>0)
            {
                sampleFrames++;sampleTotal+=Time.unscaledDeltaTime;
                sampleWorst=Mathf.Max(sampleWorst,Time.unscaledDeltaTime);
            }
            if(Time.realtimeSinceStartup-began>250)
            {CheckThat("Suite finished within 250 seconds",false,"Timeout at state "+game.State+" stage "+game.Stage+" position "+game.Position);Finish();}
        }
        void CheckThat(string name,bool passed,string evidence)
        {
            report.checks.Add(new Check{name=name,passed=passed,evidence=evidence});
            if(!passed)report.failureCount++;
            Debug.Log("RUSH_QA_CHECK "+(passed?"PASS ":"FAIL ")+name+" | "+evidence);
        }
        void Neutral(){game.QAMove=Vector2.zero;game.QADash=false;game.QABrace=false;}
        IEnumerator Wait(float seconds)
        {
            float end=Time.realtimeSinceStartup+seconds;
            while(Time.realtimeSinceStartup<end&&!finished)yield return null;
        }
        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path=Path.Combine(output,name+".png");
            ScreenCapture.CaptureScreenshot(path);report.screenshots.Add(path);
            yield return null;
        }
        IEnumerator Suite()
        {
            yield return Wait(.6f);
            CheckThat("Player opens to menu",game.State==RushState.Menu,game.State.ToString());
            yield return Shot("01-menu");
            game.StartRun();yield return Wait(.2f);
            CheckThat("Start opens instruction screen",game.State==RushState.Tutorial,game.State.ToString());
            yield return Shot("02-controls");
            game.BeginPlay();Neutral();yield return Wait(.15f);

            game.QAMove=Vector2.right;yield return Wait(.55f);
            float straight=game.Velocity.magnitude;
            game.Restart();Neutral();yield return Wait(.15f);
            game.QAMove=Vector2.one;yield return Wait(.55f);
            float diagonal=game.Velocity.magnitude;
            CheckThat("Diagonal input is normalized",Mathf.Abs(straight-diagonal)<.12f && diagonal<=6.2f,
                "Cardinal="+straight.ToString("F3")+" diagonal="+diagonal.ToString("F3"));
            game.Restart();Neutral();yield return Wait(.15f);
            game.QAMove=Vector2.up;game.QABrace=true;game.QADash=true;yield return Wait(.25f);
            CheckThat("Brace takes priority over simultaneous dash",game.IsBracing&&!game.IsDashing&&game.Velocity.magnitude<=3.1f,
                "bracing="+game.IsBracing+" dashing="+game.IsDashing+" speed="+game.Velocity.magnitude.ToString("F3"));
            Neutral();yield return Wait(.15f);
            game.QADash=true;yield return Wait(.06f);
            CheckThat("Dash accelerates and starts cooldown",game.IsDashing&&game.Velocity.magnitude>=10&&game.DashCooldown01<.2f,
                "dash="+game.IsDashing+" speed="+game.Velocity.magnitude.ToString("F3")+" readiness="+game.DashCooldown01.ToString("F3"));
            game.QABrace=true;yield return Wait(.08f);
            CheckThat("Brace cancels active dash",game.IsBracing&&!game.IsDashing,"dash="+game.IsDashing+" brace="+game.IsBracing);
            Neutral();yield return Wait(.2f);

            game.TogglePause();yield return Wait(.1f);
            float pausedTotal=game.TotalTime,pausedStage=game.StageTime;Vector2 pausedPosition=game.Position;
            yield return Shot("03-pause");yield return Wait(.55f);
            CheckThat("Pause freezes shared timer, split timer and motion",Mathf.Abs(game.TotalTime-pausedTotal)<.001f&&Mathf.Abs(game.StageTime-pausedStage)<.001f&&Vector2.Distance(game.Position,pausedPosition)<.001f,
                "Total delta="+(game.TotalTime-pausedTotal).ToString("F6")+" position delta="+Vector2.Distance(game.Position,pausedPosition).ToString("F6"));
            game.TogglePause();game.QABrace=true;game.QADash=true;yield return Wait(.1f);
            CheckThat("Held action keys do not activate across resume",!game.IsBracing&&!game.IsDashing&&game.InputLocked,
                "locked="+game.InputLocked+" dash="+game.IsDashing+" brace="+game.IsBracing);
            Neutral();yield return Wait(.15f);
            CheckThat("Releasing actions rearms controls",!game.InputLocked,"locked="+game.InputLocked);

            game.Restart();Neutral();yield return Wait(.15f);
            game.QAMove=Vector2.left;yield return Wait(2.3f);
            Vector2 atWall=game.Position;
            CheckThat("Outer wall prevents escape",atWall.x>=game.World.boundsMin.x+.40f&&atWall.x<=game.World.boundsMax.x-.40f,
                "position="+atWall+" bounds="+game.World.boundsMin+" to "+game.World.boundsMax);
            game.QAMove=Vector2.right;yield return Wait(.6f);
            CheckThat("Player can leave outer wall",game.Position.x>atWall.x+.8f,"wall x="+atWall.x.ToString("F3")+" escaped x="+game.Position.x.ToString("F3"));

            game.Restart();Neutral();yield return Wait(.15f);
            yield return Traverse("on-time");
            RecordRun("on-time");
            CheckThat("Normal run reaches on-time result",game.State==RushState.Results&&game.TotalTime<=game.TimeLimit,
                "state="+game.State+" total="+game.TotalTime.ToString("F3")+" limit="+game.TimeLimit);
            CheckRun("on-time");yield return Shot("07-result-on-time");
            for(int i=0;i<3;i++)
            {
                game.Restart();Neutral();yield return Wait(.08f);
                bool clean=game.Stage==0&&game.Collisions==0&&game.TransitionCount==0&&game.Velocity.magnitude<.01f&&game.TotalTime<.4f;
                for(int n=0;n<3;n++)clean &= game.SplitTimes[n]==0&&game.SplitCollisions[n]==0;
                CheckThat("Restart resets all transient state "+(i+1),clean,
                    "stage="+game.Stage+" collisions="+game.Collisions+" transitions="+game.TransitionCount+" velocity="+game.Velocity);
            }
            game.Restart();Neutral();yield return Wait(.15f);
            while(game.TotalTime<=game.TimeLimit+.3f&&!finished)yield return null;
            CheckThat("Deadline does not end gameplay",game.State==RushState.Playing&&game.TotalTime>game.TimeLimit,
                "state="+game.State+" total="+game.TotalTime.ToString("F3"));
            yield return Shot("08-late-still-playing");
            yield return Traverse("late");RecordRun("late");
            CheckThat("Late run can still clock in",game.State==RushState.Results&&game.TotalTime>game.TimeLimit,
                "state="+game.State+" total="+game.TotalTime.ToString("F3"));
            CheckRun("late");yield return Shot("09-result-late");
            game.ToMenu();Neutral();yield return Wait(.15f);
            CheckThat("Return to menu clears run",game.State==RushState.Menu&&game.TotalTime==0&&game.Collisions==0,
                "state="+game.State+" total="+game.TotalTime+" collisions="+game.Collisions);
            game.ToggleSound();CheckThat("Sound toggle mutes",game.Muted,"muted="+game.Muted);
            game.ToggleSound();CheckThat("Sound toggle restores",!game.Muted,"muted="+game.Muted);
            yield return Wait(.3f);Finish();
        }
        void RecordRun(string name)
        {
            report.runs.Add(new RunEvidence{name=name,totalSeconds=game.TotalTime,splitSeconds=(float[])game.SplitTimes.Clone(),collisions=game.Collisions,
                splitCollisions=(int[])game.SplitCollisions.Clone(),completedTransitions=game.TransitionCount,reachedResults=game.State==RushState.Results});
        }
        void CheckRun(string name)
        {
            float total=0;int hits=0;bool positive=true;
            for(int i=0;i<3;i++){total+=game.SplitTimes[i];hits+=game.SplitCollisions[i];positive &= game.SplitTimes[i]>0;}
            CheckThat(name+": exactly three stage completions",game.TransitionCount==3,"transitions="+game.TransitionCount);
            CheckThat(name+": splits sum to shared timer",positive&&Mathf.Abs(total-game.TotalTime)<.08f,
                "splits="+total.ToString("F3")+" shared="+game.TotalTime.ToString("F3"));
            CheckThat(name+": split collision totals match",hits==game.Collisions,"splits="+hits+" total="+game.Collisions);
        }
        IEnumerator Traverse(string runName)
        {
            int previousStage=-1;
            float routeBegan=Time.realtimeSinceStartup,replan=0,lastProgress=Time.realtimeSinceStartup,lastDash=-10;
            Vector2 progressPoint=game.Position;
            List<Vector2> route=null;
            bool boundsOK=true,obstaclesOK=true;
            while(game.State!=RushState.Results&&!finished&&Time.realtimeSinceStartup-routeBegan<70)
            {
                if(game.State!=RushState.Playing){Neutral();yield return null;continue;}
                if(game.Stage!=previousStage)
                {
                    previousStage=game.Stage;Neutral();route=null;replan=0;lastProgress=Time.realtimeSinceStartup;progressPoint=game.Position;
                    if(game.Stage>0)
                    {
                        game.QABrace=true;game.QADash=true;yield return Wait(.10f);
                        CheckThat(runName+": scene "+game.Stage+" suppresses held action input",!game.IsDashing&&!game.IsBracing&&game.InputLocked,
                            "dash="+game.IsDashing+" brace="+game.IsBracing+" locked="+game.InputLocked);
                        Neutral();
                    }
                    yield return Wait(.12f);
                    if(runName=="on-time")yield return Shot("0"+(4+game.Stage)+"-stage-"+(game.Stage+1));
                    CheckThat(runName+": scene "+game.Stage+" clears momentum and cooldown",game.Velocity.sqrMagnitude<.01f&&game.DashCooldown01>.99f,
                        "speed="+game.Velocity.magnitude.ToString("F3")+" readiness="+game.DashCooldown01.ToString("F3"));
                }
                WorldData w=game.World;Vector2 p=game.Position;
                boundsOK &= p.x>=w.boundsMin.x+.40f&&p.x<=w.boundsMax.x-.40f&&p.y>=w.boundsMin.y+.40f&&p.y<=w.boundsMax.y-.40f;
                foreach(Rect b in w.obstacles)
                {
                    Vector2 q=new Vector2(Mathf.Clamp(p.x,b.xMin,b.xMax),Mathf.Clamp(p.y,b.yMin,b.yMax));
                    if(Vector2.Distance(q,p)<.39f)obstaclesOK=false;
                }
                if(Vector2.Distance(p,progressPoint)>.45f){progressPoint=p;lastProgress=Time.realtimeSinceStartup;}
                if(route==null||Time.realtimeSinceStartup>=replan)
                {route=Plan(p,new Vector2(w.goal.x,w.goal.z),w);replan=Time.realtimeSinceStartup+.5f;}
                Vector2 target=new Vector2(w.goal.x,w.goal.z);
                if(route!=null&&route.Count>0)
                {
                    target=route[0];
                    foreach(Vector2 candidate in route)
                    {
                        if(Vector2.Distance(p,candidate)>6f)break;
                        if(LineClear(p,candidate,w))target=candidate;else break;
                    }
                }
                Vector2 direction=target-p;
                game.QAMove=direction.sqrMagnitude>.01f?direction.normalized:Vector2.zero;
                game.QABrace=false;
                if(game.DashCooldown01>.99f&&Time.realtimeSinceStartup-lastDash>1.25f&&direction.magnitude>1.4f)
                {game.QADash=true;lastDash=Time.realtimeSinceStartup;}
                if(Time.realtimeSinceStartup-lastProgress>2f)
                {
                    replan=0;
                    // Brief side-step releases a crowd pinch; movement still goes through the collision solver.
                    Vector2 tangent=new Vector2(-direction.y,direction.x).normalized;
                    if(((int)((Time.realtimeSinceStartup-lastProgress)*2))%2==0)tangent=-tangent;
                    game.QAMove=(direction.normalized+tangent*.9f).normalized;
                }
                if(Time.realtimeSinceStartup-lastProgress>8f)
                {CheckThat(runName+": no permanent crowd/corner pin",false,"stage="+game.Stage+" position="+game.Position);break;}
                yield return null;
            }
            Neutral();
            CheckThat(runName+": traversal stays within world bounds",boundsOK,"All sampled positions respect player radius");
            CheckThat(runName+": traversal does not penetrate static obstacles",obstaclesOK,"All sampled obstacle clearances >= 0.39 m");
            CheckThat(runName+": three-scene traversal completes",game.State==RushState.Results,"state="+game.State+" stage="+game.Stage+" position="+game.Position);
        }
        bool Free(Vector2 p,WorldData world)
        {
            if(p.x<world.boundsMin.x+Margin||p.x>world.boundsMax.x-Margin||p.y<world.boundsMin.y+Margin||p.y>world.boundsMax.y-Margin)return false;
            foreach(Rect b in world.obstacles)
            {
                Vector2 q=new Vector2(Mathf.Clamp(p.x,b.xMin,b.xMax),Mathf.Clamp(p.y,b.yMin,b.yMax));
                if(Vector2.Distance(p,q)<Margin)return false;
            }
            return true;
        }
        bool LineClear(Vector2 a,Vector2 b,WorldData world)
        {
            int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/.16f));
            for(int i=1;i<=n;i++)if(!Free(Vector2.Lerp(a,b,(float)i/n),world))return false;
            return true;
        }
        List<Vector2> Plan(Vector2 start,Vector2 goal,WorldData world)
        {
            int width=Mathf.RoundToInt((world.boundsMax.x-world.boundsMin.x)/Grid)+1;
            int height=Mathf.RoundToInt((world.boundsMax.y-world.boundsMin.y)/Grid)+1,count=width*height;
            Vector2[] points=new Vector2[count];bool[] free=new bool[count],closed=new bool[count],opened=new bool[count];
            float[] cost=new float[count];int[] parent=new int[count];int s=-1,t=-1;float sd=float.MaxValue,td=float.MaxValue;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int id=y*width+x;Vector2 p=world.boundsMin+new Vector2(x*Grid,y*Grid);points[id]=p;free[id]=Free(p,world);cost[id]=float.MaxValue;parent[id]=-1;
                if(!free[id])continue;
                float d=(p-start).sqrMagnitude;if(d<sd){sd=d;s=id;}
                d=(p-goal).sqrMagnitude;if(d<td){td=d;t=id;}
            }
            if(s<0||t<0)return new List<Vector2>();
            var open=new List<int>();open.Add(s);opened[s]=true;cost[s]=0;
            while(open.Count>0)
            {
                int best=0;float bestF=float.MaxValue;
                for(int i=0;i<open.Count;i++){int id=open[i];float f=cost[id]+Vector2.Distance(points[id],points[t]);if(f<bestF){bestF=f;best=i;}}
                int current=open[best];open.RemoveAt(best);opened[current]=false;if(current==t)break;closed[current]=true;
                int cx=current%width,cy=current/width;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    if(dx==0&&dy==0)continue;int nx=cx+dx,ny=cy+dy;if(nx<0||nx>=width||ny<0||ny>=height)continue;
                    int next=ny*width+nx;if(!free[next]||closed[next])continue;
                    if(dx!=0&&dy!=0&&(!free[cy*width+nx]||!free[ny*width+cx]))continue;
                    float nextCost=cost[current]+(dx!=0&&dy!=0?.7071068f:.5f);
                    if(nextCost>=cost[next])continue;cost[next]=nextCost;parent[next]=current;
                    if(!opened[next]){opened[next]=true;open.Add(next);}
                }
            }
            var result=new List<Vector2>();if(s!=t&&parent[t]<0)return result;
            int cursor=t;while(cursor>=0){result.Add(points[cursor]);if(cursor==s)break;cursor=parent[cursor];}
            result.Reverse();if(Free(goal,world))result.Add(goal);return result;
        }
        void Finish()
        {
            if(finished)return;finished=true;Neutral();
            report.passed=report.failureCount==0;
            report.elapsedRealSeconds=Time.realtimeSinceStartup-began;report.sampledFrames=sampleFrames;
            report.meanFrameMilliseconds=sampleFrames>0?sampleTotal/sampleFrames*1000:0;
            report.approximateFPS=sampleTotal>0?sampleFrames/sampleTotal:0;report.worstFrameMilliseconds=sampleWorst*1000;
            string path=Path.Combine(output,"qa-report.json");File.WriteAllText(path,JsonUtility.ToJson(report,true));
            Debug.Log("RUSH_QA_COMPLETE "+(report.passed?"PASS":"FAIL")+" assertions="+report.checks.Count+" failures="+report.failureCount+" report="+path);
            Application.Quit(report.passed?0:2);
        }
    }
}
