using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EarlyRush.V8
{
    public sealed class RushPlayV8 : MonoBehaviour
    {
        public RunV8 run;
        Camera cam;Transform world;
        SpriteRenderer hero,exitSign;
        readonly List<SpriteRenderer> crowdArt=new List<SpriteRenderer>();
        readonly List<SpriteRenderer> bodyArt=new List<SpriteRenderer>();
        readonly List<SpriteRenderer> taskArt=new List<SpriteRenderer>();
        readonly List<SpriteRenderer> props=new List<SpriteRenderer>();
        readonly List<SpriteRenderer> fx=new List<SpriteRenderer>();
        readonly List<float> fxLives=new List<float>();
        Vector3 cameraVel;Vector2 lastHero;
        Font font;AudioSource sound;AudioClip pop,ding,whoosh;
        string[] names={"骑车赶地铁","地铁大通勤","公司打卡"};
        string[] stageIcons={"legacy-icons/bike","legacy-icons/train","legacy-icons/clock"};
        int seed=81026;bool showHelp,showMap,muted,qa;int selectedTask=-1;float cueCooldown;
        GUIStyle label,big,small,button;Color ink=new Color(.1f,.24f,.25f),gold=new Color(1,.78f,.18f);
        readonly int[] best=new int[3];
        bool recorded;
        void Awake()
        {
            Application.targetFrameRate=60;QualitySettings.vSyncCount=1;
            cam=new GameObject("V8 Fixed North Camera").AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=10;
            cam.transform.position=new Vector3(0,3,-30);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.38f,.63f,.57f);cam.nearClipPlane=.1f;cam.farClipPlane=100;
            cam.gameObject.AddComponent<AudioListener>();sound=gameObject.AddComponent<AudioSource>();
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},22);
            pop=Tone(220,.06f);ding=Tone(780,.18f);whoosh=Tone(420,.10f);
            for(int i=0;i<3;i++)best[i]=PlayerPrefs.GetInt("V8Best"+i,0);
            qa=Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--v8-qa");
            Load(0,seed);if(qa)gameObject.AddComponent<RushV8RuntimeQA>();
        }
        AudioClip Tone(float hz,float duration)
        {
            int n=(int)(22050*duration);float[] samples=new float[n];for(int i=0;i<n;i++)samples[i]=Mathf.Sin(i*2*Mathf.PI*hz/22050)*.16f*(1-i/(float)n);
            var clip=AudioClip.Create("Rush cue",n,1,22050,false);clip.SetData(samples,0);return clip;
        }
        public void Load(int stage,int newSeed)
        {
            if(world)Destroy(world.gameObject);world=new GameObject("V8 Scene "+stage).transform;
            crowdArt.Clear();bodyArt.Clear();taskArt.Clear();props.Clear();fx.Clear();fxLives.Clear();seed=newSeed;
            run=new RunV8(stage,seed);run.onCue=Cue;selectedTask=-1;recorded=false;showHelp=false;
            BuildWorld();lastHero=run.player;cam.transform.position=new Vector3(run.player.x,run.player.y+2,-30);cameraVel=Vector3.zero;
            Debug.Log("V8_STAGE_READY stage="+stage+" seed="+seed+" length="+run.map.PathLength);
        }
        void Cue(string what)
        {
            if(what=="task"||what=="win"||what=="park")Emit("fx-decal/impact",run.player,2f,.7f);
            if(what=="bump"&&cueCooldown<=0)Emit("fx-decal/dust",run.player,1.4f,.3f);
            if(!muted&&cueCooldown<=0){sound.PlayOneShot(what=="task"||what=="win"?ding:what=="dash"?whoosh:pop);cueCooldown=.07f;}
        }
        void Emit(string key,Vector2 p,float width,float life){var sr=ArtV8.Create(world,key,p,width,29000);fx.Add(sr);fxLives.Add(life);}
        void BuildWorld()
        {
            string floor=run.stage==0?"floors-v3/asphalt":run.stage==1?"floors-v3/ceramic":"floors-v3/carpet";
            ArtV8.CreateFloor(world,run.map.cells,floor,run.stage==0?5:4);
            // Background boundary tiles and material changes are presentation, not invisible colliders.
            var edge=new HashSet<Vector2Int>();Vector2Int[] dirs={Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
            foreach(var c in run.map.cells)foreach(var d in dirs)if(!run.map.cells.Contains(c+d))edge.Add(c+d);
            ArtV8.CreateFloor(world,edge,run.stage==0?"floors-v3/ceramic":"legacy-floors/floor-tile",4,ArtV8.FloorOrder-1);
            // End and ground-guidance marks are always below people and furniture.
            for(float s=8;s<run.map.PathLength-5;s+=11)
            {
                Vector2 p=run.map.PointAtDistance(s),d=run.map.PointAtDistance(s+1)-p;
                var arrow=ArtV8.Create(world,"fx-decal/arrows",p,.7f,ArtV8.GroundMarkOrder);
                arrow.transform.rotation=Quaternion.Euler(0,0,-Mathf.Atan2(d.x,d.y)*Mathf.Rad2Deg);
                arrow.color=new Color(1,1,1,.45f);
            }
            var random=new System.Random(seed);
            string[] decor=run.stage==0?new[]{"street/bush-planter","street/bench","street/storefront","street/long-planter"}:run.stage==1?new[]{"metro/vending","metro/platform-bench","metro/kiosk","metro/pillar"}:new[]{"office/desk-chair","office/plant","office/sofa","office/filing-cabinet"};
            for(int i=1;i<run.map.route.Count;i++)
            {
                Vector2 a=run.map.route[i-1],b=run.map.route[i],d=(b-a).normalized,n=new Vector2(-d.y,d.x);
                for(float s=9;s<Vector2.Distance(a,b)-8;s+=8)
                {
                    for(int side=-1;side<=1;side+=2)
                    {
                        Vector2 p=a+d*s+n*side*(run.map.corridorWidth*.5f+1.1f);
                        bool nearTask=false;foreach(var t in run.map.tasks)if(Vector2.Distance(t.center,p)<16)nearTask=true;
                        if(nearTask)continue;
                        var prop=ArtV8.Create(world,decor[random.Next(decor.Length)],p,run.stage==0?3.4f:3f);props.Add(prop);
                    }
                }
            }
            for(int i=0;i<3;i++)
            {
                var site=run.map.tasks[i];var t=run.errands[i];
                var ring=ArtV8.Create(world,"ui/selection",site.target,3.5f,ArtV8.GroundMarkOrder+2);taskArt.Add(ring);
                string station=t.kind==TaskKind.CrowdPurchase?(run.stage==0?"stalls/coffee-stall":"stalls/printer"):run.stage==0?"street/boxes":run.stage==1?"metro/kiosk":"office/filing-cabinet";
                Vector2 at=site.target+Vector2.up*2.3f;
                props.Add(ArtV8.Create(world,station,at,3.8f));
                // Match ground footprint, not whole transparent bitmap bounds.
                run.solids.Add(new Rect(at.x-1.2f,at.y-.15f,2.4f,1.25f));
                ArtV8.Create(world,t.icon,at+new Vector2(0,3.2f),1.4f,ArtV8.SortOrder(at.y)+2);
                ArtV8.Create(world,"fx-decal/task-marker",site.entrance+Vector2.down*2,1.2f,ArtV8.GroundMarkOrder+3);
            }
            if(run.stage==1)
            {
                // Carriage is a distinct middle route module, not a decorative poster.
                var start=run.map.route[5];var end=run.map.route[6];Vector2 d=(end-start).normalized,n=new Vector2(-d.y,d.x);
                props.Add(ArtV8.Create(world,"metro/train-open",start,7f));
                for(float s=8;s<Vector2.Distance(start,end)-7;s+=8)
                {
                    ArtV8.Create(world,"metro/tactile-strip",start+d*s,2.2f,ArtV8.GroundMarkOrder+1);
                    props.Add(ArtV8.Create(world,"metro/carriage-bench",start+d*s+n*4.8f,2.7f));
                    props.Add(ArtV8.Create(world,"metro/window-wall",start+d*s-n*5.2f,2.7f));
                }
            }
            exitSign=ArtV8.Create(world,run.stage==0?"street/station-doorway":run.stage==1?"metro/exit-marker":"office/checkin",run.map.exit+Vector2.up*2,5f);
            ArtV8.Create(world,"ui/selection",run.map.exit,3.4f,ArtV8.GroundMarkOrder+3);
            ArtV8.Create(world,"legacy-icons/exit",run.map.exit+Vector2.up*5,1.5f,ArtV8.SortOrder(run.map.exit.y)+2);
            foreach(var b in run.bodies)bodyArt.Add(ArtV8.Create(world,b.art,b.position,b.radius*2.3f));
            hero=ArtV8.Create(world,run.stage==0?"bike-feedback/bike-up":"people/boy-up",run.player,run.stage==0?1.3f:1.1f);
            SyncCrowd();
        }
        void SyncCrowd(){while(crowdArt.Count<run.crowd.Count){var c=run.crowd[crowdArt.Count];crowdArt.Add(ArtV8.Create(world,c.art+"-down",c.position,1.05f));}}
        void Update()
        {
            if(run==null)return;cueCooldown=Mathf.Max(0,cueCooldown-Time.unscaledDeltaTime);
            if(Input.GetKeyDown(KeyCode.Escape)){if(run.phase==RunPhase.Playing)run.Pause();else if(run.phase==RunPhase.Paused)run.Resume();}
            if(Input.GetKeyDown(KeyCode.Tab))showMap=!showMap;
            if(Input.GetKeyDown(KeyCode.F1))showHelp=!showHelp;
            if(Input.GetKeyDown(KeyCode.R)&&run.phase==RunPhase.Playing){int nearest=0;float distance=float.MaxValue;foreach(var b in run.bodies)if(!b.done&&Vector2.Distance(b.position,run.player)<distance){nearest=b.task;distance=Vector2.Distance(b.position,run.player);}run.Rescue(nearest);}
            if(!qa)
            {
                var input=new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));
                // OS focus loss pauses; an unexpected >1s stall also pauses rather than dropping simulation time.
                if(Time.unscaledDeltaTime>1&&run.phase==RunPhase.Playing)run.Pause();
                else run.Tick(Time.unscaledDeltaTime,input,Input.GetKeyDown(KeyCode.Space),Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift));
            }
            RenderFrame();
            if(run.phase==RunPhase.Results&&!recorded)
            {
                recorded=true;if(run.success&&!qa){best[run.stage]=Mathf.Max(best[run.stage],run.stars);PlayerPrefs.SetInt("V8Best"+run.stage,best[run.stage]);PlayerPrefs.Save();}
                Debug.Log("V8_RESULT stage="+run.stage+" success="+run.success+" stars="+run.stars+" tasks="+run.Completed+" elapsed="+run.elapsed);
            }
        }
        public void RenderFrame()
        {
            SyncCrowd();ArtV8.SetPosition(hero,run.player);ArtV8.SetFacing(hero,run.stage==0?"bike-feedback/bike-":"people/boy-",run.facing,run.stage==0?1.3f:1.1f);
            float pulse=1+Mathf.Sin(run.clock*17)*Mathf.Clamp01(run.velocity.magnitude/4)*.025f;
            ArtV8.SetWidth(hero,run.stage==0?1.3f:1.1f);hero.transform.localScale*=pulse;
            for(int i=0;i<run.crowd.Count;i++)
            {
                var c=run.crowd[i];ArtV8.SetPosition(crowdArt[i],c.position);ArtV8.SetFacing(crowdArt[i],c.art+"-",c.velocity.sqrMagnitude>.01f?c.velocity:c.home-c.position,1.05f);
            }
            for(int i=0;i<run.bodies.Count;i++)ArtV8.SetPosition(bodyArt[i],run.bodies[i].position);
            for(int i=0;i<3;i++)taskArt[i].color=run.errands[i].done?new Color(.4f,1,.65f,.5f):new Color(1,.85f,.25f,.8f);
            // Tall art fades only when actually covering the player; ground collision remains unchanged.
            foreach(var p in props)if(p){bool covering=p.sortingOrder>hero.sortingOrder&&p.bounds.Contains(new Vector3(run.player.x,run.player.y+.7f,0));p.color=new Color(1,1,1,covering?.45f:1);}
            float dt=Time.unscaledDeltaTime;cam.transform.position=Vector3.SmoothDamp(cam.transform.position,new Vector3(run.player.x+run.facing.x*1.2f,run.player.y+2+run.facing.y,-30),ref cameraVel,.15f,200,dt);
            for(int i=fx.Count-1;i>=0;i--){fxLives[i]-=dt;if(fxLives[i]<=0){if(fx[i])Destroy(fx[i].gameObject);fx.RemoveAt(i);fxLives.RemoveAt(i);}else if(fx[i])fx[i].color=new Color(1,1,1,Mathf.Min(1,fxLives[i]*3));}
            lastHero=run.player;
        }
        void OnApplicationFocus(bool focus){if(!focus&&!qa&&run!=null)run.Pause();}
        void OnApplicationPause(bool pause){if(pause&&run!=null)run.Pause();}
        void InitGui()
        {
            if(label!=null)return;
            label=new GUIStyle(GUI.skin.label){font=font,fontSize=23,alignment=TextAnchor.MiddleCenter};label.normal.textColor=ink;
            small=new GUIStyle(label){fontSize=17,wordWrap=true};big=new GUIStyle(label){fontSize=38,fontStyle=FontStyle.Bold};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=22};button.normal.textColor=ink;
        }
        void Icon(string key,Rect r,float alpha=1){var t=ArtV8.Texture(key);if(!t)return;Color old=GUI.color;GUI.color=new Color(1,1,1,alpha);GUI.DrawTexture(r,t,ScaleMode.ScaleToFit,true);GUI.color=old;}
        void Fill(Rect r,Color c){Color old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        bool Btn(Rect r,string icon,string text="")
        {
            bool hit=GUI.Button(r,GUIContent.none,GUIStyle.none);Icon(r.Contains(Event.current.mousePosition)?"ui/teal-hover":"ui/teal-normal",r);
            if(icon!="")Icon(icon,new Rect(r.x+8,r.y+8,r.height-16,r.height-16));
            if(text!="")GUI.Label(new Rect(r.x+(icon==""?0:r.height),r.y,r.width-(icon==""?0:r.height),r.height),text,label);return hit;
        }
        void OnGUI()
        {
            if(run==null)return;InitGui();GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1280f,Screen.height/720f,1));
            Fill(new Rect(18,14,1244,78),new Color(.98f,.97f,.89f,.95f));
            Icon(stageIcons[run.stage],new Rect(28,22,55,55));GUI.Label(new Rect(80,24,150,48),(run.stage+1)+" / 3",big);
            Icon("legacy-icons/clock",new Rect(250,22,50,50));GUI.Label(new Rect(300,22,150,50),TimeSpan.FromSeconds(run.Remaining).ToString(@"mm\:ss"),big);
            for(int i=0;i<3;i++)
            {
                Rect r=new Rect(490+i*100,20,76,62);if(GUI.Button(r,GUIContent.none,GUIStyle.none))selectedTask=selectedTask==i?-1:i;
                Icon(run.errands[i].icon,r,run.errands[i].done?1:.7f);if(run.errands[i].done)Icon("legacy-extras/check",new Rect(r.x+44,r.y+30,30,30));
            }
            if(Btn(new Rect(1100,19,66,62),"legacy-icons/pause")){if(run.phase==RunPhase.Playing)run.Pause();else if(run.phase==RunPhase.Paused)run.Resume();}
            if(Btn(new Rect(1177,19,66,62),"legacy-icons/sound"))muted=!muted;
            Fill(new Rect(30,665,200,12),new Color(.1f,.25f,.25f,.7f));Fill(new Rect(30,665,200*run.energy,12),gold);Icon("legacy-icons/dash",new Rect(235,644,50,48));
            GUI.Label(new Rect(28,683,370,30),"W A S D  ·  SPACE  ·  SHIFT  ·  TAB",small);
            if(selectedTask>=0)
            {
                var t=run.errands[selectedTask];Fill(new Rect(800,107,440,135),new Color(1,.98f,.88f,.95f));GUI.Label(new Rect(812,110,416,35),t.title,label);GUI.Label(new Rect(817,148,406,65),t.help,small);
            }
            if(run.phase==RunPhase.Playing)
            {
                if(showMap)DrawMap(new Rect(28,110,430,500));else DrawMap(new Rect(1070,470,180,165));
                DrawWorldHints();
                if(run.eventKind!="")
                {
                    Icon(run.eventKind=="rain"?"legacy-icons/rain":"fx-decal/caution",new Rect(600,102,70,70));
                    if(run.eventKind=="rain"&&run.EventActive)
                        for(int i=0;i<22;i++)Icon("fx-decal/rain",new Rect((i*197)%1280,((run.clock*130+i*83)%800)-60,38,85),.25f);
                }
            }
            if(run.phase!=RunPhase.Playing)
            {
                Fill(new Rect(0,95,1280,625),new Color(.04f,.13f,.17f,.65f));
                Fill(new Rect(340,125,600,520),new Color(.99f,.97f,.88f,.98f));
                if(run.phase==RunPhase.Ready)
                {
                    Icon(stageIcons[run.stage],new Rect(585,145,110,100));GUI.Label(new Rect(380,250,520,55),names[run.stage],big);
                    GUI.Label(new Rect(375,312,530,50),"碰撞开路 · 三项任务 · 独立计时",label);
                    GUI.Label(new Rect(385,365,510,70),"WASD 固定方向移动  /  空格冲刺\nShift 稳住  /  R 回位任务物  /  Tab 地图",small);
                    for(int i=0;i<3;i++)Icon(run.errands[i].icon,new Rect(460+i*135,452,85,75));
                    if(Btn(new Rect(485,550,310,68),"legacy-icons/play","出发"))run.Begin();
                }
                else if(run.phase==RunPhase.Paused)
                {
                    GUI.Label(new Rect(380,170,520,60),"暂停",big);GUI.Label(new Rect(390,260,500,60),"计时已停止，继续后保持当前进度",small);
                    if(Btn(new Rect(470,355,340,65),"legacy-icons/play","继续"))run.Resume();
                    if(Btn(new Rect(470,445,340,65),"legacy-icons/retry","重试当前场景"))Load(run.stage,seed);
                }
                else
                {
                    Icon(run.success?"result/on-time":"result/late",new Rect(545,140,190,170));
                    GUI.Label(new Rect(390,312,500,48),run.success?"准时抵达":"这次迟到了",big);
                    for(int i=0;i<3;i++)Icon("legacy-extras/star",new Rect(470+i*120,375,100,88),i<run.stars?1:.15f);
                    GUI.Label(new Rect(390,462,500,45),run.success?"任务 "+run.Completed+" / 3   ·   "+TimeSpan.FromSeconds(run.elapsed).ToString(@"mm\:ss"):"任务记录 "+run.Completed+" / 3 · 未解锁下一场景",small);
                    if(Btn(new Rect(390,545,220,65),"legacy-icons/retry","重试"))Load(run.stage,seed);
                    if(run.success&&Btn(new Rect(650,545,240,65),"legacy-icons/play",run.stage<2?"下一场景":"再来一轮"))Load(run.stage<2?run.stage+1:0,seed);
                }
            }
            if(showHelp){Fill(new Rect(230,180,820,380),new Color(.99f,.98f,.91f,.98f));GUI.Label(new Rect(260,200,760,300),"碰撞不会掉血、扣分或眩晕。\n挤开人群购买物品；把任务物推进匹配接收框。\n按时到达：1 星；完成 1—2 项：再 1 星；全 3 项：再 1 星。\n碰撞可以充能，空格消耗能量冲刺。\n背包必须沿检查点一路推到出口，不会自动携带。\nR 将未完成任务物回到最近检查点。\nF1 关闭此说明。",label);}
        }
        void DrawWorldHints()
        {
            foreach(var t in run.errands)
            {
                if(t.done)continue;Vector2 p=t.station;
                if(t.bodies.Count>0){PushBody b=t.bodies.Find(x=>!x.done);if(b!=null)p=b.position;}
                Vector3 screen=cam.WorldToViewportPoint(new Vector3(p.x,p.y+1.8f,0));
                if(screen.x>.05f&&screen.x<.95f&&screen.y>.12f&&screen.y<.85f)
                {
                    Rect bubble=new Rect(screen.x*1280-34,(1-screen.y)*720-45,68,65);Icon("ui/speech",bubble);Icon(t.icon,new Rect(bubble.x+14,bubble.y+7,40,38));
                }
                foreach(var b in t.bodies)if(!b.done&&b.touched)
                {
                    Vector2 goal=b.checkpoint<b.checkpoints.Length?b.checkpoints[b.checkpoint]:b.target;
                    var target=cam.WorldToViewportPoint(new Vector3(goal.x,goal.y,0));
                    if(target.x>0&&target.x<1&&target.y>.05f&&target.y<.85f)Icon("fx-decal/task-marker",new Rect(target.x*1280-22,(1-target.y)*720-22,44,44));
                }
            }
        }
        void DrawMap(Rect rect)
        {
            Fill(rect,new Color(.97f,.96f,.88f,.92f));float minX=float.MaxValue,maxX=float.MinValue,minY=float.MaxValue,maxY=float.MinValue;
            foreach(var p in run.map.route){minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);}
            minX-=25;maxX+=25;minY-=10;maxY+=10;float scale=Mathf.Min((rect.width-20)/(maxX-minX),(rect.height-20)/(maxY-minY));
            Vector2 origin=new Vector2(rect.center.x-(maxX-minX)*scale/2,rect.y+rect.height-10);
            Func<Vector2,Vector2> point=p=>origin+new Vector2((p.x-minX)*scale,-(p.y-minY)*scale);
            for(int i=1;i<run.map.route.Count;i++){Vector2 a=point(run.map.route[i-1]),b=point(run.map.route[i]);Fill(new Rect(Mathf.Min(a.x,b.x)-2,Mathf.Min(a.y,b.y)-2,Mathf.Abs(a.x-b.x)+4,Mathf.Abs(a.y-b.y)+4),new Color(.28f,.61f,.57f));}
            for(int i=0;i<3;i++){Vector2 p=point(run.map.tasks[i].center);Icon(run.errands[i].icon,new Rect(p.x-9,p.y-9,18,18),run.errands[i].done?.3f:1);}
            Vector2 player=point(run.player);Fill(new Rect(player.x-4,player.y-4,8,8),gold);Vector2 e=point(run.map.exit);Icon("legacy-icons/exit",new Rect(e.x-10,e.y-12,20,20));
        }
    }
}
