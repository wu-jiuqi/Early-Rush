using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using ArtV8 = EarlyRush.V8.ArtV8;
using EarlyRush.V9;
namespace EarlyRush.V10
{
    /// <summary>V10 review presentation. Simulation remains sole owner of task outcomes.</summary>
    public sealed class RushPlayV10 : MonoBehaviour
    {
        public RunV10 run;
        RushWorldV10 composed; Camera cam; Transform world; SpriteRenderer hero,train;
        readonly List<SpriteRenderer> people=new List<SpriteRenderer>(), bodies=new List<SpriteRenderer>(), fixtures=new List<SpriteRenderer>(), dirt=new List<SpriteRenderer>(), decor=new List<SpriteRenderer>();
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly Dictionary<string,Tile[]> tileSets=new Dictionary<string,Tile[]>();
        Vector3 cameraVelocity;Vector2 cameraAnchor;int seed=91026;bool fullMap,help,qa,panorama,recorded;int selected=-1;
        Font font;GUIStyle label,small,title;Texture2D mapTexture;
        Rect mapBounds;Color ink=new Color(.08f,.23f,.25f),cream=new Color(.99f,.97f,.9f),gold=new Color(1,.8f,.2f);
        string[] names={"早高峰 · 街道","换乘 · 地铁","上班 · 公司"};
        string[] stageIcons={"legacy-icons/bike","legacy-icons/train","legacy-icons/clock"};
        void Awake()
        {
            Application.targetFrameRate=60;QualitySettings.vSyncCount=1;
            cam=new GameObject("V10 Fixed North Camera").AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=7.5f;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.39f,.6f,.53f);cam.nearClipPlane=.1f;cam.farClipPlane=80;
            cam.transform.rotation=Quaternion.identity;cam.transform.position=new Vector3(0,0,-30);cam.gameObject.AddComponent<AudioListener>();
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},22);
            panorama=Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--v9-panorama");
            qa=panorama||Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--v9-qa");Load(0,seed);
            if(qa){Application.runInBackground=true;StartCoroutine(panorama?CapturePanoramas():CaptureReview());}
        }
        public void Load(int stage,int nextSeed)
        {
            if(world)Destroy(world.gameObject);foreach(var o in owned)if(o)Destroy(o);owned.Clear();tileSets.Clear();
            people.Clear();bodies.Clear();fixtures.Clear();dirt.Clear();decor.Clear();
            if(mapTexture)Destroy(mapTexture);world=new GameObject("V10 Stage "+stage).transform;
            seed=nextSeed;selected=-1;recorded=false;run=new RunV10(stage,seed);BuildWorld();
            cameraAnchor=run.player;cameraVelocity=Vector3.zero;cam.transform.position=new Vector3(run.player.x,run.player.y,-30);
            Debug.Log("V10_PRESENTATION_READY stage="+stage+" cells="+run.map.cells.Count+" seed="+seed);
        }
        System.Collections.IEnumerator CapturePanoramas()
        {
            string output=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../QA/panoramas"));
            System.IO.Directory.CreateDirectory(output);
            System.IO.File.WriteAllText(System.IO.Path.Combine(output,"README.txt"),"Three complete chapter maps from the actual Unity BuildWorld scene and Camera.Render. World-only, no HUD. Static inspection, not gameplay evidence. Seed 91026. Uniform aspect ratio, no stretching; render size capped to device texture limit.");
            for(int stage=0;stage<3;stage++)
            {
                Load(stage,91026);yield return null;yield return null;RenderFrame();
                var bounds=new Bounds(new Vector3(mapBounds.center.x,mapBounds.center.y,0),new Vector3(mapBounds.width,mapBounds.height,1));
                foreach(var renderer in world.GetComponentsInChildren<Renderer>())if(renderer.enabled)bounds.Encapsulate(renderer.bounds);
                bounds.Expand(new Vector3(4,4,0));foreach(var sr in decor)if(sr)sr.color=Color.white;
                int maximum=Mathf.Min(12000,SystemInfo.maxTextureSize);
                float density=Mathf.Min(Mathf.Min(4000,maximum)/bounds.size.x,maximum/bounds.size.y);
                int width=Mathf.Max(1,Mathf.FloorToInt(bounds.size.x*density)),height=Mathf.Max(1,Mathf.FloorToInt(bounds.size.y*density));
                cam.aspect=width/(float)height;cam.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.x/cam.aspect);
                cam.transform.position=new Vector3(bounds.center.x,bounds.center.y,-30);
                var rt=new RenderTexture(width,height,24){antiAliasing=1};var previous=RenderTexture.active;
                var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
                try
                {
                    if(!rt.Create())throw new InvalidOperationException("Panorama RenderTexture creation failed "+width+"x"+height);
                    cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,"stage-"+stage+"-complete-map.png"),tex.EncodeToPNG());
                    System.IO.File.WriteAllText(System.IO.Path.Combine(output,"layout-stage-"+stage+".json"),run.layout.ToPreviewJson());
                    Debug.Log("V10_PANORAMA stage="+stage+" size="+width+"x"+height+" bounds="+bounds);
                }
                finally{cam.targetTexture=null;RenderTexture.active=previous;rt.Release();Destroy(rt);Destroy(tex);}
                yield return null;
            }
            Debug.Log("V10_PANORAMAS_COMPLETE "+output);Application.Quit();
        }
        System.Collections.IEnumerator CaptureReview()
        {
            // Static view sampling only. Camera teleportation is not gameplay or route evidence.
            string output=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../QA/visual-samples"));
            System.IO.Directory.CreateDirectory(output);
            System.IO.File.WriteAllText(System.IO.Path.Combine(output,"README.txt"),"Static camera samples, world only, no HUD. No simulated play or task-completion evidence. Seed 91026.");
            for(int stage=0;stage<3;stage++)
            {
                Load(stage,91026);yield return null;yield return null;RenderFrame();
                System.IO.File.WriteAllText(System.IO.Path.Combine(output,"layout-stage-"+stage+".json"),run.layout.ToPreviewJson());
                var points=new List<Vector2>{run.player};foreach(var site in run.map.tasks)points.Add(site.center);points.Add(run.map.exit);
                for(int i=0;i<points.Count;i++)
                {
                    cam.transform.position=new Vector3(points[i].x,points[i].y,-30);
                    var rt=new RenderTexture(1280,720,24);rt.Create();var previous=RenderTexture.active;
                    var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
                    try
                    {
                        cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,"stage-"+stage+"-sample-"+i+"-world-only.png"),tex.EncodeToPNG());
                    }
                    finally{cam.targetTexture=null;RenderTexture.active=previous;rt.Release();Destroy(rt);Destroy(tex);}
                }
            }
            Debug.Log("V10_VISUAL_SAMPLES_COMPLETE "+output);Application.Quit();
        }
        Tile[] Tiles(string key)
        {
            if(tileSets.TryGetValue(key,out var found))return found;
            var texture=ArtV8.Texture(key);if(!texture)throw new InvalidOperationException("Required floor texture missing "+key);
            var result=new Tile[16];int sx=texture.width/4,sy=texture.height/4;
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)
            {
                // Sixteen genuine cell sprites keep a four-unit material motif rather than shrinking a whole floor into each tile.
                var sprite=Sprite.Create(texture,new Rect(x*sx,y*sy,sx,sy),new Vector2(.5f,.5f),sx,0,SpriteMeshType.FullRect);owned.Add(sprite);
                var tile=ScriptableObject.CreateInstance<Tile>();tile.sprite=sprite;tile.colliderType=Tile.ColliderType.None;tile.transform=Matrix4x4.Scale(new Vector3(1,sx/(float)sy,1));owned.Add(tile);result[y*4+x]=tile;
            }
            tileSets[key]=result;return result;
        }
        void LayTiles(Grid grid,string name,IEnumerable<Vector2Int> cells,string key,int order,Color tint)
        {
            var go=new GameObject(name);go.transform.SetParent(grid.transform,false);go.transform.localPosition=new Vector3(-.5f,-.5f,0);
            var tm=go.AddComponent<Tilemap>();tm.tileAnchor=new Vector3(.5f,.5f,0);tm.color=tint;
            var renderer=go.AddComponent<TilemapRenderer>();renderer.sortingOrder=order;renderer.mode=TilemapRenderer.Mode.Chunk;
            var positions=new List<Vector3Int>();var tiles=new List<TileBase>();var set=Tiles(key);
            foreach(var c in cells){positions.Add(new Vector3Int(c.x,c.y,0));tiles.Add(set[((c.y%4+4)%4)*4+(c.x%4+4)%4]);}
            tm.SetTiles(positions.ToArray(),tiles.ToArray());tm.CompressBounds();
        }
        void BuildWorld()
        {
            composed=world.gameObject.AddComponent<RushWorldV10>();composed.Build(run.map,run.stage);
            if(run.stage==0)
            {
                var crossing=ArtV8.Create(world,"corrections/crosswalk-flat",run.map.signal,run.map.corridorWidth*.85f,ArtV8.GroundMarkOrder+1);
                crossing.transform.rotation=Quaternion.Euler(0,0,-Mathf.Atan2(run.SignalDirection.x,run.SignalDirection.y)*Mathf.Rad2Deg);
            }
            // The carriage is actual traversable geometry. No opaque full-train sprite covers it.
            train=null;hero=ArtV8.Create(world,"people/boy-up",run.player,1.05f);
            ArtV8.Create(world,"ui/selection",run.map.exit,3.2f,ArtV8.GroundMarkOrder+1);
            SyncEntities();CreateMapTexture();
        }
        void GroundDetail(string key,Vector2 position,float width,float rotation=0)
        {
            var sr=ArtV8.Create(world,key,position,width,ArtV8.GroundMarkOrder);
            sr.transform.rotation=Quaternion.Euler(0,0,rotation);
        }
        void DressTaskGround()
        {
            // Flat environmental markings organize the task rooms. Physical furnishings are
            // generated exclusively by map.structures so art and traversability share one source.
            if(run.stage==0)
            {
                var breakfast=run.map.tasks[0].center;
                for(int i=0;i<3;i++)GroundDetail("fx-decal/arrows",breakfast+new Vector2(0,-3-i*2),.65f);
                var repair=run.map.tasks[1];GroundDetail("street/bike-parking-marker",repair.target+Vector2.down*2,3.5f);
                for(int side=-1;side<=1;side+=2)GroundDetail("street/curb-horizontal",run.map.tasks[2].center+Vector2.up*side*3.5f,11);
            }
            else if(run.stage==1)
            {
                // Ordinary station architecture, not a hidden M1 task hint or warning.
                for(int x=-6;x<=6;x+=3)GroundDetail("metro/tactile-strip",run.map.trainDoor+new Vector2(x,-2.3f),3);
                for(int i=0;i<3;i++)GroundDetail("fx-decal/arrows",run.map.tasks[1].center+new Vector2(0,-3-i*2),.6f);
            }
            else
            {
                GroundDetail("office/clock-mat",run.map.tasks[0].center+Vector2.down*5,3);
                GroundDetail("office/clock-mat",run.map.tasks[1].start+Vector2.down*2,2.6f);
            }
        }
        void DressBorders()
        {
            string[] keys=run.stage==0?new[]{"street/storefront","street/bush-planter","street/bench","street/long-planter","street/bicycle-rack"}:run.stage==1?new[]{"metro/vending","metro/platform-bench","metro/kiosk","metro/pillar","metro/escalator"}:new[]{"office/desk-chair","office/plant","office/sofa","office/filing-cabinet","office/reception"};
            var candidates=new HashSet<Vector2Int>();Vector2Int[] dirs={Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
            foreach(var cell in run.map.cells)foreach(var dir in dirs)if(!run.map.cells.Contains(cell+dir))for(int d=2;d<=6;d++)candidates.Add(cell+dir*d);
            var sorted=new List<Vector2Int>(candidates);sorted.Sort((a,b)=>a.y==b.y?a.x.CompareTo(b.x):a.y.CompareTo(b.y));
            var placed=new List<Vector2>();var rng=new System.Random(seed+run.stage*359);
            foreach(var c in sorted)
            {
                Vector2 pos=c;bool near=false;foreach(var p in placed)if((p-pos).sqrMagnitude<25){near=true;break;}if(near)continue;
                string key=keys[rng.Next(keys.Length)];var sprite=ArtV8.Sprite(key);float width=3.2f,scale=width/sprite.bounds.size.x;
                Vector2 lo=pos+(Vector2)sprite.bounds.min*scale-Vector2.one*.5f,hi=pos+(Vector2)sprite.bounds.max*scale+Vector2.one*.5f;
                bool overlaps=false;for(int y=Mathf.FloorToInt(lo.y);y<=Mathf.CeilToInt(hi.y)&&!overlaps;y++)for(int x=Mathf.FloorToInt(lo.x);x<=Mathf.CeilToInt(hi.x);x++)if(run.map.cells.Contains(new Vector2Int(x,y))){overlaps=true;break;}
                if(!overlaps)
                {
                    var visualRect=Rect.MinMaxRect(lo.x,lo.y,hi.x,hi.y);
                    foreach(var structure in run.map.structures)if(visualRect.Overlaps(structure.bounds)){overlaps=true;break;}
                }
                if(overlaps)continue;decor.Add(ArtV8.Create(world,key,pos,width));placed.Add(pos);
            }
        }
        void SyncEntities()
        {
            while(people.Count<run.people.Count){var p=run.people[people.Count];people.Add(ArtV8.Create(world,p.art+"-down",p.position,1.05f));}
            while(bodies.Count<run.bodies.Count){var b=run.bodies[bodies.Count];bodies.Add(ArtV8.Create(world,b.art,b.position,b.radius*2.2f));}
            while(fixtures.Count<run.fixtures.Count)
            {
                var f=run.fixtures[fixtures.Count];bool mark=f.kind==FixtureKind.ChairSocket||f.kind==FixtureKind.FlipBoard;
                var sr=ArtV8.Create(world,f.art,f.position,mark?1.7f:2.6f,mark?(int?)ArtV8.GroundMarkOrder+2:null);fixtures.Add(sr);
            }
            while(dirt.Count<run.dirt.Count){var p=run.dirt[dirt.Count];var sr=ArtV8.Create(world,"fx-decal/ripple",p.position,.4f,ArtV8.GroundMarkOrder+1);sr.color=new Color(.45f,.68f,.75f,.65f);dirt.Add(sr);}
        }
        void Update()
        {
            if(run==null)return;
            if(Input.GetKeyDown(KeyCode.Escape)){if(run.phase==RunPhase.Playing)run.Pause();else if(run.phase==RunPhase.Paused)run.Resume();}
            if(Input.GetKeyDown(KeyCode.Tab))fullMap=!fullMap;if(Input.GetKeyDown(KeyCode.F1))help=!help;
            if(Input.GetKeyDown(KeyCode.R)&&run.phase==RunPhase.Playing)
            {
                BodyV9 closest=null;float distance=8;foreach(var b in run.bodies)if(!b.done&&Vector2.Distance(run.player,b.position)<distance){closest=b;distance=Vector2.Distance(run.player,b.position);}if(closest!=null)run.Rescue(closest.id);
            }
            if(!qa)
            {
                if(Time.unscaledDeltaTime>1&&run.phase==RunPhase.Playing)run.Pause();
                else {if(Input.GetKeyDown(KeyCode.Space))run.TryDash();run.Tick(Time.unscaledDeltaTime,new Vector2((Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0)));}
            }
            RenderFrame();
            if(run.phase==RunPhase.Results&&!recorded)
            {
                recorded=true;if(run.success){int previous=PlayerPrefs.GetInt("V10Best"+run.stage,0);PlayerPrefs.SetInt("V10Best"+run.stage,Mathf.Max(previous,run.stars));PlayerPrefs.Save();}
                Debug.Log("V10_RESULT stage="+run.stage+" success="+run.success+" stars="+run.stars+" tasks="+run.Completed+" elapsed="+run.elapsed);
            }
        }
        public void RenderFrame()
        {
            if(composed)composed.Occlusion(run.player);
            SyncEntities();float width=run.bicycle?1.3f:1.05f;ArtV8.SetFacing(hero,run.bicycle?"bike-feedback/bike-":"people/boy-",run.facing,width);ArtV8.SetPosition(hero,run.player);ArtV8.SetWidth(hero,width);
            for(int i=0;i<run.people.Count;i++){var p=run.people[i];people[i].enabled=!p.exited;ArtV8.SetPosition(people[i],p.position);ArtV8.SetFacing(people[i],p.art+"-",p.velocity,1.05f);}
            for(int i=0;i<run.bodies.Count;i++){var b=run.bodies[i];ArtV8.SetPosition(bodies[i],b.position);bodies[i].color=b.done?new Color(.75f,1,.8f):Color.white;}
            for(int i=0;i<run.fixtures.Count;i++)
            {
                var f=run.fixtures[i];if(f.kind==FixtureKind.FlipBoard)fixtures[i].color=run.boards[f.index]?new Color(1,.72f,.15f):new Color(.22f,.78f,.76f);
                else if(f.kind==FixtureKind.ChairSocket)fixtures[i].color=f.occupied?new Color(.4f,1,.5f,.4f):new Color(1,.8f,.2f,.85f);
            }
            for(int i=0;i<run.dirt.Count;i++)dirt[i].enabled=!run.dirt[i].cleaned;
            if(train){bool open=run.trainPhase==TrainPhase.Alighting||run.trainPhase==TrainPhase.Boarding||run.trainPhase==TrainPhase.Arrived;train.sprite=ArtV8.Sprite(open?"metro/train-open":"metro/train-closed");ArtV8.SetWidth(train,7);}
            foreach(var p in decor)if(p){bool cover=p.sortingOrder>hero.sortingOrder&&p.bounds.Contains(new Vector3(run.player.x,run.player.y+.6f,0));p.color=new Color(1,1,1,cover?.48f:1);}
            // Fixed zoom, fixed north and an axis-aligned dead zone. Never use facing, speed or collision to move/shake the camera.
            Vector2 d=run.player-cameraAnchor;Vector2 half=new Vector2(2.6f,1.8f);
            if(Mathf.Abs(d.x)>half.x)cameraAnchor.x=run.player.x-Mathf.Sign(d.x)*half.x;
            if(Mathf.Abs(d.y)>half.y)cameraAnchor.y=run.player.y-Mathf.Sign(d.y)*half.y;
            cam.transform.position=Vector3.SmoothDamp(cam.transform.position,new Vector3(cameraAnchor.x,cameraAnchor.y,-30),ref cameraVelocity,.22f,80,Mathf.Min(Time.unscaledDeltaTime,.1f));
        }
        void OnApplicationFocus(bool value){if(!value&&run!=null&&!qa)run.Pause();}
        void OnApplicationPause(bool value){if(value&&run!=null)run.Pause();}
        void CreateMapTexture()
        {
            int minX=int.MaxValue,minY=int.MaxValue,maxX=int.MinValue,maxY=int.MinValue;
            foreach(var p in run.map.cells){minX=Mathf.Min(minX,p.x);minY=Mathf.Min(minY,p.y);maxX=Mathf.Max(maxX,p.x);maxY=Mathf.Max(maxY,p.y);}
            mapBounds=new Rect(minX-.5f,minY-.5f,maxX-minX+1,maxY-minY+1);
            mapTexture=new Texture2D(maxX-minX+1,maxY-minY+1,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
            var colors=new Color32[mapTexture.width*mapTexture.height];foreach(var p in run.map.cells)colors[(p.y-minY)*mapTexture.width+p.x-minX]=new Color(.22f,.56f,.53f,.85f);
            mapTexture.SetPixels32(colors);mapTexture.Apply(false,true);
        }
        void GuiStyles()
        {
            if(label!=null)return;label=new GUIStyle(GUI.skin.label){font=font,fontSize=22,alignment=TextAnchor.MiddleCenter};label.normal.textColor=ink;
            small=new GUIStyle(label){fontSize=16,wordWrap=true};title=new GUIStyle(label){fontSize=34,fontStyle=FontStyle.Bold};
        }
        void Fill(Rect r,Color color){Color old=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        void Icon(string key,Rect r,float alpha=1){var tex=ArtV8.Texture(key);if(!tex)return;Color old=GUI.color;GUI.color=new Color(1,1,1,alpha);GUI.DrawTexture(r,tex,ScaleMode.ScaleToFit,true);GUI.color=old;}
        bool Button(Rect r,string icon,string text="")
        {
            bool click=GUI.Button(r,GUIContent.none,GUIStyle.none);Icon(r.Contains(Event.current.mousePosition)?"ui/teal-hover":"ui/teal-normal",r);
            Icon(icon,new Rect(r.x+8,r.y+8,r.height-16,r.height-16));if(text.Length>0)GUI.Label(new Rect(r.x+r.height,r.y,r.width-r.height,r.height),text,label);return click;
        }
        bool HiddenTask(TaskV9 t)=>run.stage==1&&t.id=="M1"&&t.state!=TaskState.Failed;
        void OnGUI()
        {
            if(run==null)return;GuiStyles();GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1280f,Screen.height/720f,1));
            Fill(new Rect(18,16,420,76),cream);Icon(stageIcons[run.stage],new Rect(27,23,60,60));GUI.Label(new Rect(90,24,70,50),(run.stage+1)+"/3",title);
            Icon("legacy-icons/clock",new Rect(170,25,46,46));GUI.Label(new Rect(220,24,200,50),TimeSpan.FromSeconds(run.Remaining).ToString(@"mm\:ss"),title);
            for(int i=0;i<run.tasks.Count;i++)
            {
                var t=run.tasks[i];if(HiddenTask(t))continue;Rect r=new Rect(20+i*132,106,124,92);
                Fill(r,new Color(cream.r,cream.g,cream.b,.94f));if(GUI.Button(r,GUIContent.none,GUIStyle.none))selected=selected==i?-1:i;
                Icon(t.icon,new Rect(r.x+8,r.y+6,64,58));
                if(t.state==TaskState.Complete)Icon("legacy-extras/check",new Rect(r.x+77,r.y+13,36,36));
                else if(t.state==TaskState.Failed){Fill(new Rect(r.x+82,r.y+18,26,26),new Color(.87f,.24f,.17f));GUI.Label(new Rect(r.x+80,r.y+14,30,30),"!",label);}
                else if(t.limit>0)GUI.Label(new Rect(r.x+69,r.y+11,50,50),t.count+"/"+t.limit,small);
                Fill(new Rect(r.x+9,r.y+76,106,6),new Color(.68f,.77f,.7f));Fill(new Rect(r.x+9,r.y+76,106*(t.done?1:Mathf.Clamp01(t.progress)),6),gold);
            }
            if(Button(new Rect(450,20,62,62),"legacy-icons/pause")){if(run.phase==RunPhase.Playing)run.Pause();else if(run.phase==RunPhase.Paused)run.Resume();}
            DrawMap(fullMap?new Rect(660,106,590,585):new Rect(1008,18,254,184));
            if(selected>=0&&!HiddenTask(run.tasks[selected])){var t=run.tasks[selected];Fill(new Rect(20,212,398,104),cream);GUI.Label(new Rect(29,218,380,36),t.title,label);GUI.Label(new Rect(32,257,374,48),TaskHelp(t),small);}
            Fill(new Rect(20,660,180,9),ink);Fill(new Rect(20,660,180*run.energy,9),gold);
            GUI.Label(new Rect(15,680,490,28),"W A S D  ·  SPACE 冲刺  ·  R 救回  ·  TAB 地图  ·  F1",small);
            DrawWorldHints();
            if(run.eventKind.Length>0)
            {
                Icon(run.eventKind=="rain"?"legacy-icons/rain":"fx-decal/caution",new Rect(600,20,60,60));
                if(run.eventKind=="rain"&&run.eventAge>=2)for(int i=0;i<18;i++)Icon("fx-decal/rain",new Rect(i*89%1280,((float)run.elapsed*115+i*101)%780-60,30,70),.22f);
            }
            if(run.phase!=RunPhase.Playing)DrawModal();
            if(help){Fill(new Rect(300,190,680,360),cream);GUI.Label(new Rect(325,210,630,280),"碰撞没有生命值惩罚。\n九项任务有各自的成功与失败条件。\n按时通过 1 星；至少完成 1 项再加 1 星；全 3 项再加 1 星。\n固定 W 上 / D 右；相机不随转弯旋转。\nR 只救回 8 米内尚未完成的可推动物。\nF1 收起",label);}
        }
        string TaskHelp(TaskV9 t)
        {
            if(t.state==TaskState.Failed)return "本项失败，仍可继续通勤。重试当前场景可重新挑战。";
            switch(t.id){case "S1":return "拿到早餐后保持一段时间；碰撞超过容忍次数会失败。";case "S2":return "将两个车轮推到修理店，完成后获得自行车。";case "S3":return "碰撞翻牌，同时改变相邻牌；让五张牌与目标颜色一致。";case "M2":return "退开，再在指针进入金色节拍窗时碰撞机器。";case "M3":return "推动抹布擦遍水渍；以实际覆盖面积结算。";case "O1":return "留意拿咖啡的同事，打翻数量超过容忍次数会失败。";case "O2":return "领取文件，在时限内送至对应编号的办公桌。";case "O3":return "把五把椅子推入空位；每个位置只能停一把。";default:return "";}
        }
        Rect WorldRect(Vector2 p,float w,float h)
        {
            Vector3 v=cam.WorldToViewportPoint(new Vector3(p.x,p.y,0));return new Rect(v.x*1280-w/2,(1-v.y)*720-h,w,h);
        }
        bool Visible(Rect r)=>r.xMax>0&&r.x<1280&&r.yMax>0&&r.y<720;
        void DrawWorldHints()
        {
            if(run.phase!=RunPhase.Playing)return;
            foreach(var f in run.fixtures)
            {
                if(f.kind==FixtureKind.ChairSocket)continue;
                Rect r=WorldRect(f.position+Vector2.up*2.6f,58,58);if(!Visible(r))continue;
                if(f.kind==FixtureKind.FlipBoard)
                {
                    Fill(new Rect(r.x+13,r.y+13,32,32),run.boardGoal[f.index]?new Color(1,.72f,.15f):new Color(.22f,.78f,.76f));
                    GUI.Label(new Rect(r.x,r.y-12,58,24),(f.index+1).ToString(),small);continue;
                }
                Icon("ui/speech",r);Icon(f.kind==FixtureKind.Breakfast?"legacy-extras/breakfast":f.kind==FixtureKind.Repair?"legacy-icons/bike":f.art,new Rect(r.x+12,r.y+6,34,34));
                if(f.kind==FixtureKind.FileDesk)GUI.Label(new Rect(r.x+35,r.y+28,24,25),(f.index+1).ToString(),small);
                if(f.kind==FixtureKind.Capsule&&Vector2.Distance(run.player,f.position)<5)
                {
                    Rect beat=new Rect(r.center.x-70,r.y-28,140,12);Fill(beat,ink);float window=run.config.beatWindow/run.config.beatPeriod;
                    Fill(new Rect(beat.x+beat.width*(.5f-window*.5f),beat.y,beat.width*window,beat.height),gold);Fill(new Rect(beat.x+beat.width*run.BeatPhase-2,beat.y-3,4,18),Color.white);
                }
            }
            foreach(var p in run.people)if(p.kind==PersonKind.Coffee&&!p.exited)
            {
                Rect r=WorldRect(p.position+Vector2.up*1.6f,34,34);if(!Visible(r))continue;Icon("ui/thought",r);Icon("legacy-extras/coffee",new Rect(r.x+5,r.y+3,24,24),p.spilled?.3f:1);
            }
            if(run.stage==0)
            {
                Rect r=WorldRect(run.map.signal+new Vector2(-run.map.corridorWidth*.4f,1),44,72);if(Visible(r)){Fill(r,ink);Fill(new Rect(r.x+10,r.y+8,24,24),run.SignalGreen?new Color(.22f,.32f,.26f):new Color(1,.24f,.13f));Fill(new Rect(r.x+10,r.y+40,24,24),run.SignalGreen?new Color(.24f,1,.48f):new Color(.22f,.32f,.26f));}
            }
            // No M1 arrow, target marker, start/complete callout or special passenger highlight.
        }
        void DrawMap(Rect r)
        {
            Fill(r,new Color(.99f,.98f,.9f,.96f));float scale=Mathf.Min((r.width-22)/mapBounds.width,(r.height-22)/mapBounds.height);
            Rect area=new Rect(r.center.x-mapBounds.width*scale*.5f,r.center.y-mapBounds.height*scale*.5f,mapBounds.width*scale,mapBounds.height*scale);
            GUI.DrawTexture(area,mapTexture,ScaleMode.StretchToFill,true);
            Func<Vector2,Vector2> point=p=>new Vector2(area.x+(p.x-mapBounds.x)*scale,area.yMax-(p.y-mapBounds.y)*scale);
            foreach(var t in run.tasks)
            {
                if(run.stage==1&&t.id=="M1")continue;
                Vector2 p=point(t.position);Icon(t.icon,new Rect(p.x-9,p.y-9,18,18),t.done?.35f:1);
            }
            Vector2 exit=point(run.map.exit);Icon("legacy-icons/exit",new Rect(exit.x-8,exit.y-8,16,16));
            Vector2 heroPoint=point(run.player);Fill(new Rect(heroPoint.x-4,heroPoint.y-4,8,8),gold);
        }
        void DrawModal()
        {
            Fill(new Rect(0,0,1280,720),new Color(.02f,.12f,.14f,.64f));Fill(new Rect(350,125,580,505),cream);
            if(run.phase==RunPhase.Ready)
            {
                Icon(stageIcons[run.stage],new Rect(587,150,106,100));GUI.Label(new Rect(380,259,520,50),names[run.stage],title);
                GUI.Label(new Rect(380,322,520,60),"穿过街区与转角，准时抵达。\n碰撞推动物品，观察任务图标。",label);
                GUI.Label(new Rect(380,400,520,70),"WASD 固定方向 · 空格冲刺\nR 救回附近物品 · Esc 暂停",small);
                if(Button(new Rect(485,525,310,68),"legacy-icons/play","出发"))run.Begin();
            }
            else if(run.phase==RunPhase.Paused)
            {
                GUI.Label(new Rect(400,200,480,55),"暂停",title);GUI.Label(new Rect(400,275,480,45),"计时已停止",label);
                if(Button(new Rect(470,360,340,65),"legacy-icons/play","继续"))run.Resume();if(Button(new Rect(470,450,340,65),"legacy-icons/retry","重试本场景"))Load(run.stage,seed);
            }
            else
            {
                Icon(run.success?"result/on-time":"result/late",new Rect(565,145,150,135));GUI.Label(new Rect(400,285,480,50),run.success?"准时抵达":"迟到了，再试一次",title);
                for(int i=0;i<3;i++)Icon("legacy-extras/star",new Rect(477+i*115,355,96,86),i<run.stars?1:.18f);
                GUI.Label(new Rect(395,452,490,40),"任务 "+run.Completed+" / 3  ·  "+TimeSpan.FromSeconds(run.elapsed).ToString(@"mm\:ss"),label);
                if(Button(new Rect(395,530,225,65),"legacy-icons/retry","重试"))Load(run.stage,seed);
                if(run.success&&Button(new Rect(650,530,230,65),"legacy-icons/play",run.stage<2?"下一场景":"再来一轮"))Load(run.stage<2?run.stage+1:0,seed);
            }
        }
        void OnDestroy(){foreach(var o in owned)if(o)Destroy(o);if(mapTexture)Destroy(mapTexture);if(font)Destroy(font);if(world)Destroy(world.gameObject);if(cam)Destroy(cam.gameObject);}
    }
}
