using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using EarlyRush.V8;
namespace EarlyRush.V9
{
    /// <summary>V9 review presentation. Simulation remains sole owner of task outcomes.</summary>
    public sealed class RushPlayV9 : MonoBehaviour
    {
        public RunV9 run;
        Camera cam; Transform world; SpriteRenderer hero,heroShadow,train;
        readonly List<SpriteRenderer> people=new List<SpriteRenderer>(), peopleShadows=new List<SpriteRenderer>(), bodies=new List<SpriteRenderer>(), fixtures=new List<SpriteRenderer>(), dirt=new List<SpriteRenderer>(), decor=new List<SpriteRenderer>();
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly Dictionary<string,Tile[]> tileSets=new Dictionary<string,Tile[]>();
        public RushCameraV10 cameraFollow = new RushCameraV10();
        [Range(5f, 12f)] public float cameraSize = 7.5f;
        bool v4StreetRequested;
        int seed=91026;bool fullMap,help,qa,panorama,recorded,runtimeQa;int selected=-1;
        readonly bool[] failureShown=new bool[3];string notice="";double noticeUntil;
        int seenDashLaunches,seenPlayerLaunches;double impactAt=-10;Vector2 impactPoint,playerFlightFacing=Vector2.up;
        float uiScale=1;Vector2 uiOffset;Texture2D triangle;
        public Camera GameCamera => cam;
        // Read-only production projection for runtime screenshot checks. Pixels have a top-left origin.
        public Rect GetMapPanelForReview(bool expanded)=>expanded?new Rect(660,106,590,585):new Rect(1008,18,254,184);
        public RushMinimapProjectionV13 CreateMinimapProjection(Rect panel,bool expanded)=>new RushMinimapProjectionV13(mapBounds,panel,run.player,expanded);
        public RushMinimapProjectionV13 CreateMinimapProjection(bool expanded)=>CreateMinimapProjection(GetMapPanelForReview(expanded),expanded);
        public Vector2 GuiToScreenPixel(Vector2 point)=>RushMinimapProjectionV13.GuiToScreenPixel(point,Screen.width,Screen.height);
        // Diagnostic display setup only, not evidence that a Tab key was pressed.
        public void SetExpandedMapForReview(bool expanded){fullMap=expanded;}
        Vector2 ViewHalf => new Vector2(cam.orthographicSize*cam.aspect,cam.orthographicSize);
        Font font;GUIStyle label,small,title;Texture2D mapTexture;
        Rect mapBounds;Color ink=new Color(.08f,.23f,.25f),cream=new Color(.99f,.97f,.9f),gold=new Color(1,.8f,.2f);
        string[] names={"早高峰 · 街道","通勤 · 地铁","上班 · 公司"};
        string[] stageIcons={"people/boy-up","legacy-icons/train","legacy-icons/clock"};
        void Awake()
        {
            Application.targetFrameRate=60;QualitySettings.vSyncCount=1;
            cam=new GameObject("V9 Fixed North Camera").AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=cameraSize;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.39f,.6f,.53f);cam.nearClipPlane=.1f;cam.farClipPlane=80;
            cam.transform.rotation=Quaternion.identity;cam.transform.position=new Vector3(0,0,-30);cam.gameObject.AddComponent<AudioListener>();
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},22);
            panorama=Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--v9-panorama");
            v4StreetRequested=Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--v4-street"||a=="--v4-demo");
            if(v4StreetRequested){seed=RushV4StreetMap.Seed;cameraSize=12;cam.orthographicSize=cameraSize;}
            qa=panorama||Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--v9-qa");Load(0,seed);
            bool v13Qa=Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--v13-runtime-qa");
            bool v12Qa=Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--v12-runtime-qa");
            runtimeQa=v13Qa||v12Qa||Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--v11-runtime-qa");
            if(runtimeQa)
            {
                Application.runInBackground=true;
                if(v13Qa)gameObject.AddComponent<RushRuntimeCheckV13>();else if(v12Qa)gameObject.AddComponent<RushRuntimeCheckV12>();else gameObject.AddComponent<RushRuntimeCheckV11>();
            }
            if(qa){Application.runInBackground=true;StartCoroutine(panorama?CapturePanoramas():CaptureReview());}
        }
        public void Load(int stage,int nextSeed){LoadModelForReview(new RunV9(stage,nextSeed));}
        // Allows runtime diagnostics to render an explicitly declared mechanism fixture from a fresh world.
        public void LoadModelForReview(RunV9 simulation)
        {
            if(simulation==null)throw new ArgumentNullException(nameof(simulation));
            int stage=simulation.stage,nextSeed=simulation.seed;
            if(world)Destroy(world.gameObject);foreach(var o in owned)if(o)Destroy(o);owned.Clear();tileSets.Clear();
            people.Clear();peopleShadows.Clear();bodies.Clear();fixtures.Clear();dirt.Clear();decor.Clear();
            if(mapTexture)Destroy(mapTexture);world=new GameObject("V9 Stage "+stage).transform;
            seed=nextSeed;selected=-1;recorded=false;notice="";Array.Clear(failureShown,0,failureShown.Length);
            run=simulation;seenDashLaunches=seenPlayerLaunches=0;playerFlightFacing=run.facing;impactAt=-10;BuildWorld();
            cameraFollow.Reset(run.player,mapBounds,ViewHalf);
            cam.transform.position=new Vector3(cameraFollow.Position.x,cameraFollow.Position.y,-30);
            Debug.Log("V9_PRESENTATION_READY stage="+stage+" cells="+run.map.cells.Count+" seed="+seed);
        }
        // Static review framing only; gameplay never calls this during a running stage.
        public void FrameForReview()
        {
            cameraFollow.Reset(run.player,mapBounds,ViewHalf);
            cam.transform.position=new Vector3(cameraFollow.Position.x,cameraFollow.Position.y,-30);
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
                    System.IO.File.WriteAllText(System.IO.Path.Combine(output,"layout-stage-"+stage+".json"),run.map.ToPreviewJson());
                    Debug.Log("V9_PANORAMA stage="+stage+" size="+width+"x"+height+" bounds="+bounds);
                }
                finally{cam.targetTexture=null;RenderTexture.active=previous;rt.Release();Destroy(rt);Destroy(tex);}
                yield return null;
            }
            Debug.Log("V9_PANORAMAS_COMPLETE "+output);Application.Quit();
        }
        System.Collections.IEnumerator CaptureReview()
        {
            // Static view sampling only. Camera teleportation is not gameplay or route evidence.
            string output=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,v4StreetRequested?"../QA/v4-street-visual":"../QA/visual-samples"));
            System.IO.Directory.CreateDirectory(output);
            System.IO.File.WriteAllText(System.IO.Path.Combine(output,"README.txt"),"Static camera samples, world only, no HUD. No simulated play or task-completion evidence. Seed recorded in layout JSON; v4 street is an optional geometry integration, not timing proof.");
            for(int stage=0;stage<3;stage++)
            {
                Load(stage,v4StreetRequested?RushV4StreetMap.Seed:91026);yield return null;yield return null;RenderFrame();
                System.IO.File.WriteAllText(System.IO.Path.Combine(output,"layout-stage-"+stage+".json"),run.map.ToPreviewJson());
                var points=new List<Vector2>{run.player};foreach(var site in run.map.tasks)points.Add(site.center);points.Add(run.map.exit);
                if(v4StreetRequested||RushStreetArtV14.Applies(run.stage,run.seed))points.AddRange(run.map.route);
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
            Debug.Log("V9_VISUAL_SAMPLES_COMPLETE "+output);Application.Quit();
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
            bool authoredV4=seed==RushV4StreetMap.Seed;
            bool authoredStreet=RushStreetArtV14.Applies(run.stage,seed);
            if(authoredV4)RushV4StreetArt.Build(world,owned,decor,run);
            if(authoredStreet)RushStreetArtV14.Build(world,owned,decor,run);
            if(!authoredStreet&&!authoredV4)
            {
            var grid=new GameObject("V9 World Grid").AddComponent<Grid>();grid.transform.SetParent(world,false);grid.cellSize=Vector3.one;
            string floor=run.stage==0?"floors-v3/asphalt":run.stage==1?"floors-v3/ceramic":"floors-v3/carpet";
            LayTiles(grid,"Walkable Tilemap",run.map.cells,floor,ArtV8.FloorOrder,Color.white);
            // Apply later zones last, only to existing walkable cells: never fill a structure hole.
            var materialCells=new Dictionary<string,List<Vector2Int>>();
            foreach(var cell in run.map.cells)
            {
                string key=floor;foreach(var zone in run.map.zones)if(zone.bounds.Contains((Vector2)cell))key=zone.floorKey;
                if(key==floor)continue;
                if(!materialCells.TryGetValue(key,out var list)){list=new List<Vector2Int>();materialCells.Add(key,list);}list.Add(cell);
            }
            foreach(var pair in materialCells)LayTiles(grid,"Zone Tilemap "+pair.Key,pair.Value,pair.Key,ArtV8.FloorOrder+1,Color.white);
            foreach(var structure in run.map.structures)
            {
                // The map supplies the contact pivot and width. Do not substitute Rect.center,
                // recenter the sprite, or add a second inconsistent collider in the view layer.
                var sr=ArtV8.Create(world,structure.artKey,structure.position,structure.width);
                sr.name="Structure "+structure.kind;decor.Add(sr);
            }
            var border=new HashSet<Vector2Int>();Vector2Int[] dirs={Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
            foreach(var c in run.map.cells)foreach(var d in dirs)if(!run.map.cells.Contains(c+d))for(int n=1;n<=8;n++)if(!run.map.cells.Contains(c+d*n))border.Add(c+d*n);
            LayTiles(grid,"Environment Tilemap",border,"floors-v3/ceramic",ArtV8.FloorOrder-1,run.stage==0?new Color(.92f,1,.93f):run.stage==1?new Color(.8f,.94f,.94f):new Color(.95f,.9f,.8f));
            DressBorders();DressTaskGround();
            if(run.stage==0)ArtV8.Create(world,"corrections/crosswalk-flat",run.map.signal,run.map.corridorWidth*.85f,ArtV8.GroundMarkOrder+1);
            for(float s=8;s<run.map.PathLength-4;s+=14)
            {
                Vector2 p=run.map.PointAtDistance(s),dir=run.map.PointAtDistance(s+1)-p;
                var sr=ArtV8.Create(world,"fx-decal/arrows",p,.55f,ArtV8.GroundMarkOrder);sr.transform.rotation=Quaternion.Euler(0,0,-Mathf.Atan2(dir.x,dir.y)*Mathf.Rad2Deg);sr.color=new Color(1,1,1,.38f);
            }
            }
            if(run.stage==2&&!authoredV4)
            {
                // Office terminus is the meeting room, not a time-clock/check-in machine.
                // Dress beyond the receiving ring, keeping its entire southern approach free.
                Vector2 tableAt=run.map.exit+Vector2.up*3.5f;
                decor.Add(ArtV8.Create(world,"office/meeting-table",tableAt,5));
                decor.Add(ArtV8.Create(world,"tasks/chair",tableAt+new Vector2(-3,1),1.2f));
                decor.Add(ArtV8.Create(world,"tasks/chair",tableAt+new Vector2(3,1),1.2f));
                decor.Add(ArtV8.Create(world,"office/plant",tableAt+new Vector2(-4,3),1.5f));
                decor.Add(ArtV8.Create(world,"office/plant",tableAt+new Vector2(4,3),1.5f));
                run.solids.Add(new Rect(tableAt.x-2.1f,tableAt.y,4.2f,1.5f));
            }
            else if(!authoredStreet&&!authoredV4)decor.Add(ArtV8.Create(world,run.stage==0?"street/station-doorway":"metro/exit-marker",run.map.exit+Vector2.up*2,4));
            ArtV8.Create(world,"ui/selection",run.map.exit,3.2f,ArtV8.GroundMarkOrder+1);
            if(run.stage==1){train=ArtV8.Create(world,authoredV4?"metro/train-open":"metro/train-closed",run.map.trainDoor+Vector2.up*1.5f,7);train.enabled=!authoredV4;decor.Add(train);}
            else train=null;
            heroShadow=ArtV8.Create(world,"fx-decal/actor-shadow",run.player,.9f,ArtV8.GroundMarkOrder+3);
            hero=ArtV8.Create(world,"people/boy-up",run.player,1.05f);
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
            while(people.Count<run.people.Count)
            {
                var p=run.people[people.Count];
                peopleShadows.Add(ArtV8.Create(world,"fx-decal/actor-shadow",p.position,.85f,ArtV8.GroundMarkOrder+3));
                people.Add(ArtV8.Create(world,p.art+"-down",p.position,1.05f));
            }
            while(bodies.Count<run.bodies.Count){var b=run.bodies[bodies.Count];bodies.Add(ArtV8.Create(world,b.art,b.position,b.radius*2.2f));}
            while(fixtures.Count<run.fixtures.Count)
            {
                var f=run.fixtures[fixtures.Count];bool mark=f.kind==FixtureKind.ChairSocket||f.kind==FixtureKind.FlipBoard;
                var sr=ArtV8.Create(world,f.art,f.position,mark?1.7f:2.6f,mark?(int?)ArtV8.GroundMarkOrder+2:null);fixtures.Add(sr);if(seed==RushV4StreetMap.Seed&&(f.kind==FixtureKind.Capsule||f.kind==FixtureKind.FilePickup||f.kind==FixtureKind.FileDesk))sr.enabled=false;if((RushStreetArtV14.Applies(run.stage,seed)||RushV4StreetArt.Applies(run.stage,seed))&&(f.kind==FixtureKind.Breakfast||f.kind==FixtureKind.Repair))sr.enabled=false;
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
            if(!qa&&!runtimeQa)
            {
                if(Time.unscaledDeltaTime>1&&run.phase==RunPhase.Playing)run.Pause();
                else
                {
                    Vector2 movement=new Vector2(
                        (Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0),
                        (Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?1:0));
                    // Bracing wins when Shift and Space arrive in the same frame.
                    run.SetBracing(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift));
                    if(Input.GetKeyDown(KeyCode.Space))run.PressDash(movement);
                    run.Tick(Time.unscaledDeltaTime,movement);
                }
            }
            RenderFrame();
            if(run.phase==RunPhase.Results&&!recorded)
            {
                recorded=true;if(run.success)
                {
                    int previous=PlayerPrefs.GetInt("V9Best"+run.stage,0);
                    PlayerPrefs.SetInt("V9Best"+run.stage,Mathf.Max(previous,run.stars));
                    PlayerPrefs.SetInt("V10Unlocked",Mathf.Max(UnlockedStage(),Mathf.Min(2,run.stage+1)));
                    PlayerPrefs.Save();
                }
                Debug.Log("V9_RESULT stage="+run.stage+" success="+run.success+" stars="+run.stars+" tasks="+run.Completed+" elapsed="+run.elapsed);
            }
        }
        public void RenderFrame()
        {
            SyncEntities();
            if(run.phase!=RunPhase.Paused)
            {
                float actorScale=run.seed==RushV4StreetMap.Seed?2.025f:1f;
                float width=(run.bicycle?1.3f:1.05f)*actorScale;
                // The player's simulation-facing may change with held input while in the air.
                // Capture a view-only direction once and retain it until landing has settled.
                if(run.PlayerLaunchCount!=seenPlayerLaunches)playerFlightFacing=run.velocity.sqrMagnitude>.001f?run.velocity.normalized:run.facing;
                Vector2 heroFacing=run.IsAirborne||run.PlayerLandingFeedback01>0?playerFlightFacing:run.facing;
                ArtV8.SetFacing(hero,run.bicycle?"bike-feedback/bike-":"people/boy-",heroFacing,width);
                ActorPoseV11 pose=run.IsAirborne?ActorPoseV11.Airborne:run.IsBracing?ActorPoseV11.Brace:run.IsDashing?ActorPoseV11.Dash:ActorPoseV11.Walk;
                RushActorPoseV11.Apply(hero,heroShadow,run.player,run.velocity,heroFacing,width,run.elapsed,pose,0,run.PlayerHeight,0,run.bicycle,run.PlayerFlightProgress01,run.PlayerLandingFeedback01);
                hero.color=run.BraceFeedback01>0?Color.Lerp(Color.white,new Color(.54f,1,.8f),run.BraceFeedback01):Color.white;
                for(int i=0;i<run.people.Count;i++)
                {
                    var p=run.people[i];people[i].enabled=!p.exited;peopleShadows[i].enabled=!p.exited;if(p.exited)continue;
                    bool attacking=p.actionPhase==NpcActionPhase.Windup||p.actionPhase==NpcActionPhase.Charging;
                    // The launch direction survives wall contact and the zero velocity at landing.
                    Vector2 direction=p.dashAirborne||p.dashLandingFeedback01>0?p.dashFlightDirection:attacking?p.chargeDirection:p.velocity;
                    float personWidth=1.05f*actorScale*(1+(p.appearance%3-1)*.035f);
                    ArtV8.SetFacing(people[i],p.art+"-",direction,personWidth);
                    ActorPoseV11 personPose=p.actionPhase==NpcActionPhase.Windup?ActorPoseV11.Windup:
                        p.actionPhase==NpcActionPhase.Charging?ActorPoseV11.Dash:p.actionPhase==NpcActionPhase.Recovery?
                        (p.chargeCancelled?ActorPoseV11.Cancelled:ActorPoseV11.Recovery):ActorPoseV11.Walk;
                    float progress=p.actionPhase==NpcActionPhase.Windup?run.NpcWindup01(p):run.NpcRecovery01(p);
                    if(p.kind==PersonKind.Coffee)
                    {
                        if(p.coffeeState==CoffeeState.GuardingCup)personPose=ActorPoseV11.Brace;
                        else if(p.coffeeState==CoffeeState.SpillReaction){personPose=ActorPoseV11.Recovery;progress=.15f;}
                    }
                    if(p.dashAirborne)personPose=ActorPoseV11.Airborne;
                    RushActorPoseV11.Apply(people[i],peopleShadows[i],p.position,p.velocity,direction,personWidth,run.elapsed,personPose,progress,p.dashHeight,p.appearance,false,p.dashFlightProgress01,p.dashLandingFeedback01);
                    people[i].color=RushActorPoseV11.Tint(p.appearance);
                }
            }
            if(run.DashLaunchCount!=seenDashLaunches||run.PlayerLaunchCount!=seenPlayerLaunches)
            {
                impactAt=run.elapsed;impactPoint=run.player;
                if(run.DashLaunchCount!=seenDashLaunches)
                {
                    float nearest=float.MaxValue;foreach(var person in run.people)if(person.dashAirborne)
                    {float distance=(person.position-run.player).sqrMagnitude;if(distance<nearest){nearest=distance;impactPoint=person.position;}}
                }
                seenDashLaunches=run.DashLaunchCount;seenPlayerLaunches=run.PlayerLaunchCount;
            }
            for(int i=0;i<run.bodies.Count;i++){var b=run.bodies[i];ArtV8.SetPosition(bodies[i],b.position);bodies[i].color=b.done?new Color(.75f,1,.8f):Color.white;}
            for(int i=0;i<run.fixtures.Count;i++)
            {
                var f=run.fixtures[i];if(f.kind==FixtureKind.FlipBoard)fixtures[i].color=run.boards[f.index]?new Color(1,.72f,.15f):new Color(.22f,.78f,.76f);
                else if(f.kind==FixtureKind.ChairSocket)fixtures[i].color=f.occupied?new Color(.4f,1,.5f,.4f):new Color(1,.8f,.2f,.85f);
            }
            RushStreetArtV14.RefreshFixtures(run,fixtures,bodies);
            for(int i=0;i<run.dirt.Count;i++)dirt[i].enabled=!run.dirt[i].cleaned;
            if(train){bool open=run.trainPhase==TrainPhase.Alighting||run.trainPhase==TrainPhase.Boarding||run.trainPhase==TrainPhase.Arrived;if(run.map.authoredLayout!=null){train.enabled=open;train.sprite=ArtV8.Sprite("metro/train-open");var portal=run.map.authoredLayout.PortalWorld("M1");ArtV8.SetPosition(train,(run.trainPhase==TrainPhase.Arrived?portal.to:run.map.trainDoor)+Vector2.up*1.5f);}else train.sprite=ArtV8.Sprite(open?"metro/train-open":"metro/train-closed");ArtV8.SetWidth(train,7);}
            foreach(var p in decor)if(p){bool cover=p.sortingOrder>hero.sortingOrder&&p.bounds.Contains(new Vector3(run.player.x,run.player.y+.6f,0));p.color=new Color(1,1,1,cover?.48f:1);}
            Vector2 cameraPoint=cameraFollow.Step(run.player,mapBounds,ViewHalf,Time.unscaledDeltaTime,run.phase==RunPhase.Playing&&!qa);
            if(!qa)cam.transform.position=new Vector3(cameraPoint.x,cameraPoint.y,-30);
            for(int i=0;i<run.tasks.Count;i++)if(run.tasks[i].state==TaskState.Failed&&!failureShown[i])
            {
                failureShown[i]=true;notice=run.tasks[i].title+"失败，仍可继续通勤。";noticeUntil=run.elapsed+4;
            }
        }
        void OnApplicationFocus(bool value){if(!value&&run!=null&&!qa&&!runtimeQa)run.Pause();}
        void OnApplicationPause(bool value){if(value&&run!=null&&!runtimeQa)run.Pause();}
        void CreateMapTexture()
        {
            int minX=int.MaxValue,minY=int.MaxValue,maxX=int.MinValue,maxY=int.MinValue;
            foreach(var p in run.map.cells){minX=Mathf.Min(minX,p.x);minY=Mathf.Min(minY,p.y);maxX=Mathf.Max(maxX,p.x);maxY=Mathf.Max(maxY,p.y);}
            mapBounds=new Rect(minX-.5f,minY-.5f,maxX-minX+1,maxY-minY+1);
            mapTexture=new Texture2D(maxX-minX+1,maxY-minY+1,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
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
            bool click=GUI.Button(r,GUIContent.none,GUIStyle.none);
            Fill(r,!GUI.enabled?new Color(.79f,.84f,.8f):r.Contains(Event.current.mousePosition)?new Color(.42f,.78f,.72f):new Color(.63f,.84f,.77f));
            Icon(icon,new Rect(r.x+8,r.y+8,r.height-16,r.height-16));if(text.Length>0)GUI.Label(new Rect(r.x+r.height,r.y,r.width-r.height,r.height),text,label);return click;
        }
        bool HiddenTask(TaskV9 t)=>run.stage==1&&t.id=="M1"&&t.state!=TaskState.Failed;
        void OnGUI()
        {
            if(run==null)return;GuiStyles();uiScale=RushMinimapProjectionV13.GuiScale(Screen.width,Screen.height);
            uiOffset=RushMinimapProjectionV13.GuiOffset(Screen.width,Screen.height);
            GUI.matrix=RushMinimapProjectionV13.GuiMatrix(Screen.width,Screen.height);
            if(v4StreetRequested){Fill(new Rect(480,650,780,28),cream);GUI.Label(new Rect(490,652,760,25),"V4 integration TEST / 600s prototype budget / NOT acceptance-ready",small);}
            Fill(new Rect(18,16,420,76),cream);Icon(stageIcons[run.stage],new Rect(27,23,60,60));GUI.Label(new Rect(90,24,70,50),(run.stage+1)+"/3",title);
            Icon("legacy-icons/clock",new Rect(170,25,46,46));GUI.Label(new Rect(220,24,200,50),TimeSpan.FromSeconds(run.Remaining).ToString(@"mm\:ss"),title);
            for(int i=0;i<run.tasks.Count;i++)
            {
                var t=run.tasks[i];Rect r=new Rect(20+i*146,106,138,110);
                Fill(r,new Color(cream.r,cream.g,cream.b,.94f));
                if(HiddenTask(t))
                {
                    // M1 keeps a neutral slot: no name, icon, progress or success notification.
                    Fill(new Rect(r.x+45,r.y+26,48,48),new Color(.66f,.75f,.69f));
                    Fill(new Rect(r.x+48,r.y+29,42,42),cream);continue;
                }
                if(GUI.Button(r,GUIContent.none,GUIStyle.none))selected=selected==i?-1:i;
                Icon(t.icon,new Rect(r.x+8,r.y+5,46,46));
                if(t.state==TaskState.Complete)Icon("legacy-extras/check",new Rect(r.x+86,r.y+12,34,34));
                else if(t.state==TaskState.Failed)GUI.Label(new Rect(r.x+83,r.y+10,38,36),"×",title);
                else GUI.Label(new Rect(r.x+60,r.y+9,72,40),TaskProgress(t),small);
                GUI.Label(new Rect(r.x+2,r.y+52,r.width-4,28),t.title,small);
                if(t.id=="S3")for(int n=0;n<run.boardGoal.Length;n++)
                {
                    var cell=new Rect(r.x+16+n*22,r.y+82,16,16);Fill(cell,run.boardGoal[n]?gold:new Color(.22f,.78f,.76f));
                    GUI.Label(cell,run.boardGoal[n]?"●":"○",small);
                }
                else
                {
                    Fill(new Rect(r.x+9,r.y+92,120,6),new Color(.68f,.77f,.7f));
                    Fill(new Rect(r.x+9,r.y+92,120*(t.done?1:Mathf.Clamp01(t.progress)),6),gold);
                }
            }
            if(Button(new Rect(450,20,62,62),"legacy-icons/pause")){if(run.phase==RunPhase.Playing)run.Pause();else if(run.phase==RunPhase.Paused)run.Resume();}
            DrawMap(GetMapPanelForReview(fullMap));
            if(selected>=0&&!HiddenTask(run.tasks[selected])){var t=run.tasks[selected];Fill(new Rect(20,228,426,124),cream);GUI.Label(new Rect(29,234,408,36),t.title,label);GUI.Label(new Rect(32,272,402,72),TaskHelp(t),small);}
            GUI.Label(new Rect(15,680,930,28),"WASD / 方向键 · 空格冲刺 · Shift 稳步 / 稳车 · R 救回 · TAB 地图 · Esc 暂停 · F1",small);
            DrawActionStatus();
            if(run.stage==0&&run.tasks[0].state==TaskState.Active)
            {
                Fill(new Rect(20,608,430,62),cream);
                GUI.Label(new Rect(30,611,410,54),"进食 "+(run.tasks[0].progress*run.config.breakfastGameMinutes).ToString("0.0")+" / "+run.config.breakfastGameMinutes.ToString("0.0")+" 游戏分钟\n1 游戏分钟 = "+(1/run.config.gameMinutesPerRealSecond).ToString("0")+" 现实秒 · 碰撞 "+run.tasks[0].count+" / "+run.config.breakfastTolerance,small);
            }
            DrawDocuments();
            if(notice.Length>0&&run.elapsed<noticeUntil&&run.phase==RunPhase.Playing){Fill(new Rect(470,611,510,54),cream);GUI.Label(new Rect(480,615,490,46),notice,small);}
            DrawWorldHints();
            if(run.eventKind.Length>0)
            {
                Icon(run.eventKind=="rain"?"legacy-icons/rain":"fx-decal/caution",new Rect(1180,212,60,60));
                if(run.eventKind=="rain"&&run.eventAge>=2)for(int i=0;i<18;i++)Icon("fx-decal/rain",new Rect(i*89%1280,((float)run.elapsed*115+i*101)%780-60,30,70),.22f);
            }
            if(run.phase!=RunPhase.Playing)DrawModal();
            if(help){Fill(new Rect(300,170,680,390),cream);GUI.Label(new Rect(325,190,630,350),"点按空格：向当前输入方向冲刺；冷却快结束时可提前按下。\nShift：稳步或稳车，不会下车；松开后可再次冲刺。\n冲刺可撞开普通人，任务碰撞规则仍有效。\n蓄力方向锁定后可侧移躲开，或提前按住 Shift 抵挡。\n碰撞没有血量、扣分或直接扣时惩罚；任务例外仍有效。\n按时抵达 1 星；完成至少 1 项加 1 星；全任务再加 1 星。\nR 救回附近物品；镜头保持固定方向。F1 收起。",small);}
        }
        void DrawActionStatus()
        {
            Fill(new Rect(530,18,455,76),Color.Lerp(cream,gold,Mathf.Clamp01(run.DashImpactFeedback01)*.35f));
            string travel=run.bicycle?"骑行":"步行";
            string state=run.IsAirborne?"被撞 · 腾空":run.IsBracing?(run.bicycle?"稳车":"稳步"):run.IsDashing?"冲刺中":"自由移动";
            GUI.Label(new Rect(540,20,435,30),travel+" · "+state+(run.DashImpactFeedback01>0?" · 累计撞开 "+run.DashLaunchCount:run.BraceFeedback01>0?" · 抵住冲撞":""),label);
            GUI.Label(new Rect(540,50,158,28),run.DashBuffered?"已接收 · 即将冲刺":run.DashReady01>=.999f?"空格 · 冲刺就绪":"冲刺冷却",small);
            Fill(new Rect(710,58,250,9),new Color(.69f,.78f,.72f));Fill(new Rect(710,58,250*run.DashReady01,9),gold);
        }
        string TaskHelp(TaskV9 t)
        {
            if(t.state==TaskState.Failed)return "本项失败，仍可继续通勤。重试当前场景可重新挑战。";
            switch(t.id){case "S1":return "进食需 "+run.config.breakfastGameMinutes.ToString("0.0")+" 游戏分钟（约 "+run.config.breakfastDuration.ToString("0")+" 现实秒）。碰撞 > "+run.config.breakfastTolerance+" 次才失败，阈值内不重置进度。";case "S2":return "将两个车轮推到修理店，完成后获得自行车。";case "S3":return "碰撞翻板与相邻板联动；目标图案见任务槽。离开再接触才再次翻面，错误可纠正。";case "M2":return "退开，再在指针进入金色节拍窗时碰撞机器。";case "M3":return "推动抹布擦遍水渍；以实际覆盖面积结算。";case "O1":return "留意拿咖啡的同事，打翻数量超过容忍次数会失败。";case "O2":return "领取文件，在时限内找到相同头像的领导工位。下方头像和小地图提供大致方向。";case "O3":return "把 "+run.config.chairCount+" 把椅子推入空位；每个位置只能停一把。";default:return "";}
        }
        Rect WorldRect(Vector2 p,float w,float h)
        {
            Vector3 v=cam.WorldToViewportPoint(new Vector3(p.x,p.y,0));return new Rect((v.x*Screen.width-uiOffset.x)/uiScale-w/2,((1-v.y)*Screen.height-uiOffset.y)/uiScale-h,w,h);
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
                    GUI.Label(new Rect(r.x,r.y-12,58,24),"目标 "+(f.index+1),small);GUI.Label(new Rect(r.x+13,r.y+13,32,32),run.boardGoal[f.index]?"●":"○",small);
                    Rect actual=WorldRect(f.position+Vector2.up*.5f,28,28);GUI.Label(actual,run.boards[f.index]?"●":"○",label);continue;
                }
                Icon("ui/speech",r);Icon(f.kind==FixtureKind.Breakfast?"legacy-extras/breakfast":f.kind==FixtureKind.Repair?"legacy-icons/bike":f.art,new Rect(r.x+12,r.y+6,34,34));
                if(f.kind==FixtureKind.FileDesk){LeaderPortrait(new Rect(r.x+5,r.y+1,48,48),f.index);if(run.IsDelivered(f.index))Icon("legacy-extras/check",new Rect(r.x+37,r.y+30,24,24));}
                if(f.kind==FixtureKind.Capsule&&Vector2.Distance(run.player,f.position)<5)
                {
                    Rect beat=new Rect(r.center.x-70,r.y-28,140,12);Fill(beat,ink);float window=run.config.beatWindow/run.config.beatPeriod;
                    Fill(new Rect(beat.x+beat.width*(.5f-window*.5f),beat.y,beat.width*window,beat.height),gold);Fill(new Rect(beat.x+beat.width*run.BeatPhase-2,beat.y-3,4,18),Color.white);
                }
            }
            foreach(var p in run.people)if(p.kind==PersonKind.Coffee&&!p.exited)
            {
                Rect r=WorldRect(p.position+Vector2.up*(1.6f+p.dashHeight),34,34);if(!Visible(r))continue;Icon("ui/thought",r);Icon("legacy-extras/coffee",new Rect(r.x+5,r.y+3,24,24),p.spilled?.3f:1);
            }
            if(run.stage==0)
            {
                Rect r=WorldRect(run.map.signal+new Vector2(-run.map.corridorWidth*.4f,1),44,72);if(Visible(r)){Fill(r,ink);Fill(new Rect(r.x+10,r.y+8,24,24),run.SignalGreen?new Color(.22f,.32f,.26f):new Color(1,.24f,.13f));Fill(new Rect(r.x+10,r.y+40,24,24),run.SignalGreen?new Color(.24f,1,.48f):new Color(.22f,.32f,.26f));GUI.Label(new Rect(r.x-10,r.y+74,64,24),run.SignalGreen?"可通行 ↑":"停止 ━",small);}
            }
            // Only normal commuters can expose a combat tell. M1 passengers have no task warning.
            foreach(var p in run.people)
            {
                if(p.exited||p.dashAirborne||p.kind==PersonKind.Alighting||p.actionPhase==NpcActionPhase.Cruise)continue;
                Rect body=WorldRect(p.position+Vector2.up*1.8f,100,24);if(!Visible(body))continue;
                if(p.actionPhase==NpcActionPhase.Windup)
                {
                    float amount=run.NpcWindup01(p);Fill(new Rect(body.x+10,body.y+22,80,6),cream);
                    Fill(new Rect(body.x+10,body.y+22,80*amount,6),new Color(.95f,.4f,.18f));GUI.Label(body,"蓄力",small);
                    Rect aim=WorldRect(p.position+p.chargeDirection*(1+.5f*amount),24,24);
                    Triangle(aim.center,p.chargeDirection,24,new Color(.95f,.4f,.18f));
                    break;
                }
                if(p.actionPhase==NpcActionPhase.Charging)
                {
                    Rect aim=WorldRect(p.position+p.chargeDirection*.9f,22,22);Triangle(aim.center,p.chargeDirection,22,gold);break;
                }
                if(p.actionPhase==NpcActionPhase.Recovery&&p.chargeCancelled&&run.NpcRecovery01(p)<.5f)GUI.Label(body,"收势",small);
            }
            DrawImpactFeedback();
            // No M1 arrow, target marker, start/complete callout or special passenger highlight.
        }
        void DrawImpactFeedback()
        {
            float age=(float)(run.elapsed-impactAt);if(age<0||age>.36f)return;
            Rect location=WorldRect(impactPoint+Vector2.up*.25f,2,2);if(!Visible(location))return;
            Vector2 center=location.center;float t=age/.36f,radius=18+42*t;
            Color color=new Color(1,.77f,.24f,(1-t)*.85f);
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI*2/16,b=(i+1)*Mathf.PI*2/16;
                GuiLine(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,3*(1-t)+1,color);
            }
            for(int i=0;i<4;i++)
            {
                Vector2 direction=new Vector2(Mathf.Cos(i*Mathf.PI*.5f+.3f),Mathf.Sin(i*Mathf.PI*.5f+.3f));
                GuiLine(center+direction*(radius+7),center+direction*(radius+24),3,color);
            }
        }
        void GuiLine(Vector2 a,Vector2 b,float thickness,Color color)
        {
            Matrix4x4 matrix=GUI.matrix;Vector2 direction=b-a;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg,a);
            Fill(new Rect(a.x,a.y-thickness*.5f,direction.magnitude,thickness),color);GUI.matrix=matrix;
        }
        string TaskProgress(TaskV9 task)
        {
            if(task.id=="M3")return Mathf.RoundToInt(task.progress*100)+"%";
            if(task.id=="S1"&&task.state==TaskState.Active)return Mathf.RoundToInt(task.progress*100)+"%\n碰撞 "+task.count+"/"+task.limit;
            if(task.id=="O2"&&task.state==TaskState.Active)return task.count+"/"+task.limit+"\n"+Mathf.Max(0,(float)(run.config.deliveryDuration-run.elapsed+task.startedAt)).ToString("0")+"秒";
            return task.limit>0?task.count+"/"+task.limit:"—";
        }
        string TaskResult(TaskV9 task)=>task.state==TaskState.Complete?"完成":task.state==TaskState.Failed?"失败":"未完成";
        void LeaderPortrait(Rect r,int id)
        {
            Color[] colors={new Color(.23f,.67f,.67f),new Color(.77f,.43f,.36f),new Color(.44f,.49f,.76f),new Color(.64f,.57f,.3f),new Color(.65f,.4f,.66f)};
            Fill(r,colors[id%colors.Length]);
            // Deliberately simple placeholder portraits, shared by held files and desks.
            Fill(new Rect(r.x+r.width*.23f,r.y+r.height*.2f,r.width*.54f,r.height*.55f),new Color(1,.82f,.63f));
            Fill(new Rect(r.x+r.width*.21f,r.y+r.height*.13f,r.width*.58f,r.height*(id%2==0?.17f:.25f)),ink);
            Fill(new Rect(r.x+r.width*.31f,r.y+r.height*.43f,r.width*.08f,r.height*.07f),ink);
            Fill(new Rect(r.x+r.width*.61f,r.y+r.height*.43f,r.width*.08f,r.height*.07f),ink);
            if(id%3==1)Fill(new Rect(r.x+r.width*.28f,r.y+r.height*.42f,r.width*.44f,r.height*.04f),ink);
            GUI.Label(new Rect(r.x,r.y+r.height*.68f,r.width,r.height*.3f),"甲乙丙丁戊"[id%5].ToString(),small);
        }
        void DrawDocuments()
        {
            if(run.stage!=2||run.tasks[1].state!=TaskState.Active)return;
            Fill(new Rect(20,574,430,96),cream);
            GUI.Label(new Rect(26,576,418,27),"待送文件 · 对照相同头像 · "+Mathf.Max(0,(float)(run.config.deliveryDuration-run.elapsed+run.tasks[1].startedAt)).ToString("0")+" 秒",small);
            int slot=0;foreach(var f in run.fixtures)if(f.kind==FixtureKind.FileDesk&&!run.IsDelivered(f.index))
            {
                LeaderPortrait(new Rect(35+slot*77,610,48,48),f.index);slot++;
            }
        }
        void Triangle(Vector2 center,Vector2 worldDirection,float size,Color color)
        {
            if(!triangle)
            {
                triangle=new Texture2D(24,24,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear};
                var colors=new Color[24*24];for(int y=0;y<24;y++)for(int x=0;x<24;x++)
                    colors[y*24+x]=Mathf.Abs(x-11.5f)<(23-y)*.48f?Color.white:Color.clear;
                triangle.SetPixels(colors);triangle.Apply();
            }
            Matrix4x4 before=GUI.matrix;Color old=GUI.color;
            // Rotate in the same virtual GUI coordinates first; apply scale/letterboxing after.
            // Left-multiplying a window-space rotation moves the marker at non-1:1 resolutions.
            GUI.matrix=before*RushMinimapProjectionV13.RotationAbout(center,worldDirection);
            GUI.color=color;GUI.DrawTexture(new Rect(center.x-size/2,center.y-size/2,size,size),triangle);
            GUI.color=old;GUI.matrix=before;
        }
        void DrawMap(Rect r)
        {
            Fill(r,new Color(.99f,.98f,.9f,.96f));
            var projection=CreateMinimapProjection(r,fullMap);Rect inner=projection.ContentRect;
            // Crop both source UVs and destination together. No nested GUI group or giant
            // off-panel texture rectangle can introduce a second marker coordinate space.
            if(projection.DrawRect.width>0&&projection.DrawRect.height>0)
                GUI.DrawTextureWithTexCoords(projection.DrawRect,mapTexture,projection.UvRect,true);
            Action<Vector2,string,float,bool> marker=(worldPosition,key,alpha,failed)=>
            {
                bool offscreen;Vector2 p=projection.ProjectMarker(worldPosition,10,out offscreen);
                if(offscreen&&!fullMap)
                {
                    if(alpha<.5f)return; // Finished/failed tasks never keep directing the player off-screen.
                    Triangle(p,worldPosition-projection.SourceRect.center,12,ink);
                    p+= (inner.center-p).normalized*16;
                }
                Icon(key,new Rect(p.x-9,p.y-9,18,18),alpha);
                if(failed)GUI.Label(new Rect(p.x-10,p.y-12,20,24),"×",small);
            };
            foreach(var task in run.tasks)
            {
                if(run.stage==1&&task.id=="M1")continue;
                marker(task.position,task.icon,task.done?.28f:task.state==TaskState.Failed?.25f:1,task.state==TaskState.Failed);
            }
            if(run.stage==2&&run.tasks[1].state==TaskState.Active)
            {
                FixtureV9 closest=null;float distance=float.MaxValue;
                foreach(var f in run.fixtures)if(f.kind==FixtureKind.FileDesk&&!run.IsDelivered(f.index))
                {float d=Vector2.Distance(run.player,f.position);if(d<distance){distance=d;closest=f;}}
                if(closest!=null)
                {
                    // Quantise to a region: the desk is identified from its portrait in the world.
                    Vector2 region=new Vector2(Mathf.Round(closest.position.x/8)*8,Mathf.Round(closest.position.y/8)*8);
                    bool offscreen;Vector2 p=projection.ProjectMarker(region,14,out offscreen);
                    if(offscreen){Triangle(p,region-projection.SourceRect.center,14,ink);p+=(inner.center-p).normalized*20;}
                    LeaderPortrait(new Rect(p.x-12,p.y-12,24,24),closest.index);
                }
            }
            marker(run.map.exit,"legacy-icons/exit",1,false);
            Triangle(projection.WorldToGui(run.player),run.facing,16,gold);
            Rect caption=new Rect(r.x,r.yMax-22,r.width,22);
            Fill(caption,cream);GUI.Label(caption,fullMap?"全图 · 北向固定 · TAB 收起":"附近道路 · 北向固定 · TAB 全图",small);
        }
        int UnlockedStage()
        {
            int unlocked=Mathf.Clamp(PlayerPrefs.GetInt("V10Unlocked",0),0,2);
            // Preserve successful progression from earlier builds, never derive unlocks from failure records.
            for(int stage=0;stage<2;stage++)if(PlayerPrefs.GetInt("V9Best"+stage,0)>0)unlocked=Mathf.Max(unlocked,stage+1);
            return unlocked;
        }
        void DrawModal()
        {
            Fill(new Rect(0,0,1280,720),new Color(.02f,.12f,.14f,.64f));Fill(new Rect(350,125,580,505),cream);
            if(run.phase==RunPhase.Ready)
            {
                Icon(stageIcons[run.stage],new Rect(587,150,106,100));GUI.Label(new Rect(380,259,520,50),names[run.stage],title);
                GUI.Label(new Rect(380,322,520,60),(run.stage==0?"步行出发，准时抵达地铁入口。":run.stage==1?"乘车后前往出站点。":"准时进入会议室参会区。")+"\n任务均可跳过，不影响前往终点。",label);
                GUI.Label(new Rect(380,386,520,38),"WASD / 方向键 · 空格冲刺 · Shift 稳步 / 稳车\nR 救回物品 · Esc 暂停",small);
                int unlocked=UnlockedStage();
                for(int i=0;i<3;i++)
                {
                    bool available=i<=unlocked;GUI.enabled=available;
                    if(Button(new Rect(390+i*170,430,160,48),stageIcons[i],available?(i+1)+" · "+new[]{"街道","地铁","早会"}[i]:"未解锁")){Load(i,seed);}
                    GUI.enabled=true;
                }
                GUI.Label(new Rect(390,484,280,30),"地图种子 "+seed+" · 上限 "+(run.limit/60).ToString("0")+" 分钟",small);
                if(Button(new Rect(700,482,190,36),"legacy-icons/retry","换种子"))Load(run.stage,seed+1);
                if(Button(new Rect(485,525,310,68),"legacy-icons/play","出发"))run.Begin();
            }
            else if(run.phase==RunPhase.Paused)
            {
                GUI.Label(new Rect(400,200,480,55),"暂停",title);GUI.Label(new Rect(400,275,480,45),"计时已停止",label);
                if(Button(new Rect(470,350,340,60),"legacy-icons/play","继续"))run.Resume();
                if(Button(new Rect(470,430,340,60),"legacy-icons/retry","同种子重试"))Load(run.stage,seed);
                if(Button(new Rect(470,510,340,60),"legacy-icons/retry","换种子挑战"))Load(run.stage,seed+1);
            }
            else
            {
                GUI.Label(new Rect(380,150,520,50),run.success?"准时抵达":"超时 · 挑战记录",title);
                for(int i=0;i<3;i++)Icon("legacy-extras/star",new Rect(515+i*85,211,74,64),i<run.stars?1:.18f);
                GUI.Label(new Rect(380,283,520,32),"耗时 "+TimeSpan.FromSeconds(run.elapsed).ToString(@"mm\:ss")+" · "+(run.success?"成功最高 "+PlayerPrefs.GetInt("V9Best"+run.stage,0)+" 星":"任务星仅记录，不解锁下一阶段"),small);
                for(int i=0;i<run.tasks.Count;i++)
                {
                    var task=run.tasks[i];Icon(task.icon,new Rect(405,330+i*50,38,38));
                    GUI.Label(new Rect(452,330+i*50,310,38),task.title,label);
                    GUI.Label(new Rect(772,330+i*50,108,38),TaskResult(task),small);
                }
                if(Button(new Rect(395,530,225,65),"legacy-icons/retry","重试"))Load(run.stage,seed);
                if(run.success&&Button(new Rect(650,530,230,65),"legacy-icons/play",run.stage<2?"下一场景":"再来一轮"))Load(run.stage<2?run.stage+1:0,seed);

            }
        }
        void OnDestroy(){if(triangle)Destroy(triangle);foreach(var o in owned)if(o)Destroy(o);if(mapTexture)Destroy(mapTexture);if(font)Destroy(font);if(world)Destroy(world.gameObject);if(cam)Destroy(cam.gameObject);}
    }
}
