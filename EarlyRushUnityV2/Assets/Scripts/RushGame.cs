using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

namespace EarlyRush
{
    public enum RushState { Menu, Tutorial, Playing, Paused, Transition, Results }
    public class RushGame : MonoBehaviour
    {
        public RushState State = RushState.Menu;
        public int Stage;
        public float TotalTime, TimeLimit = 95f, StageTime, TransitionLeft;
        public int Collisions;
        public float[] SplitTimes = new float[3];
        public int[] SplitCollisions = new int[3];
        public bool Muted, IsBracing, IsDashing;
        public string[] StageNames = { "骑车赶地铁", "挤上早高峰", "抢最后的打卡" };
        public float DashCooldown01 => Mathf.Clamp01(1f - cooldown / 1.15f);
        public float StageProgress01 => world == null ? 0 : Mathf.Clamp01((position.y-world.spawn.z)/(world.goal.z-world.spawn.z));
        public Vector2 Position => position;
        public Vector2 Velocity => velocity;
        public WorldData World => world;
        public bool InputLocked => actionLocked;
        public int TransitionCount { get; private set; }
        WorldData world;
        Transform player, visual, ring;
        Camera cameraRig;
        Vector3 cameraHome, cameraFocus, cameraVelocity;
        Vector2 cameraLook;
        public Camera GameCamera => cameraRig;
        public Vector2[] CrowdSnapshot()
        {
            var result=new Vector2[crowds.Count];
            for(int i=0;i<crowds.Count;i++)result[i]=crowds[i].position;
            return result;
        }
        public Vector2 GoalViewport => cameraRig.WorldToViewportPoint(world.goal+Vector3.up*.2f);
        public float GoalDistance => Vector2.Distance(position,new Vector2(world.goal.x,world.goal.z));
        Vector2 position, velocity, facing = Vector2.up;
        Vector2 moveInput;
        bool braceInput, dashPressed, actionLocked = true;
        float cooldown, dashLeft, impact, shake, collisionSoundCooldown;
        float transitionBeginTime;
        int stageStartCollisions;
        List<Crowd> crowds = new List<Crowd>();
        Dictionary<int,float> collisionTimes = new Dictionary<int,float>();
        AudioSource audioSource;
        AudioClip dashClip, bumpClip, goalClip, tickClip;
        LineRenderer trail;
        Vector3[] trailPoints = new Vector3[10];
        RushQA qa;
        public bool QAInput;
        public Vector2 QAMove;
        public bool QABrace, QADash;
        const float Radius = .42f;
        class Crowd
        {
            public Transform visual;
            public Vector2 origin, position, offset, axis, pushVelocity;
            public float range, speed, phase;
        }
        void Awake()
        {
            Application.targetFrameRate = 60;
            Time.fixedDeltaTime = 1f/60f;
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 2;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.82f,.9f,1f);
            RenderSettings.ambientEquatorColor = new Color(.61f,.68f,.74f);
            RenderSettings.ambientGroundColor = new Color(.43f,.47f,.53f);
            var light = new GameObject("Soft morning sun").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.25f;
            light.color = new Color(1f,.92f,.81f); light.shadows = LightShadows.Soft;
            light.shadowBias = .025f; light.transform.rotation = Quaternion.Euler(50,-35,0);
            cameraRig = new GameObject("Overview camera").AddComponent<Camera>();
            cameraRig.orthographic = true; cameraRig.orthographicSize = 9.5f;
            cameraHome = new Vector3(0,28,-17);
            cameraRig.transform.position = cameraHome;
            cameraRig.transform.LookAt(new Vector3(0,0,0));
            cameraRig.backgroundColor = new Color(.62f,.8f,.83f);
            cameraRig.clearFlags = CameraClearFlags.SolidColor;
            cameraRig.farClipPlane=100; cameraRig.nearClipPlane=.1f;
            cameraRig.gameObject.AddComponent<AudioListener>();
            audioSource = gameObject.AddComponent<AudioSource>();
            dashClip = MakeSound("swoosh", 440, .14f, 0);
            bumpClip = MakeSound("soft bump", 130, .12f, 1);
            goalClip = MakeSound("arrival", 660, .34f, 2);
            tickClip = MakeSound("ready", 880, .07f, 2);
            gameObject.AddComponent<RushRouteHUD>();
            BuildStage(0);
            if (Array.Exists(Environment.GetCommandLineArgs(), a=>a=="--qa"))
                qa = gameObject.AddComponent<RushQA>();
            Debug.Log("EARLY_RUSH_READY " + SystemInfo.graphicsDeviceName + " " + SystemInfo.graphicsDeviceType);
        }
        AudioClip MakeSound(string name, float freq,float duration,int kind)
        {
            int samples=(int)(22050*duration); float[] data=new float[samples];
            var random=new System.Random(12+kind);
            for(int i=0;i<samples;i++)
            {
                float t=(float)i/samples; float envelope=Mathf.Sin(t*Mathf.PI)*Mathf.Pow(1-t,1.6f);
                float frequency=freq*(kind==0? 1.6f-t : kind==2? 1+Mathf.Floor(t*3)*.25f : 1-t*.6f);
                data[i]=(Mathf.Sin(i/22050f*frequency*Mathf.PI*2)*.7f+(kind==0?(float)(random.NextDouble()*2-1)*.3f:0))*envelope*.22f;
            }
            var clip=AudioClip.Create(name,samples,1,22050,false);clip.SetData(data,0);return clip;
        }
        void Sound(AudioClip clip,float volume=1) { if(!Muted) audioSource.PlayOneShot(clip,volume); }
        public void ToggleSound(){Muted=!Muted;audioSource.mute=Muted;}
        public void StartRun(){ResetRun();State=RushState.Tutorial;LockActions();}
        public void BeginPlay(){State=RushState.Playing;LockActions();Sound(tickClip);Log("run_begin");}
        public void Restart(){ResetRun();State=RushState.Playing;LockActions();Log("restart");}
        public void ToMenu(){ResetRun();State=RushState.Menu;LockActions();}
        public void Quit(){Application.Quit();}
        public void TogglePause()
        {
            if(State==RushState.Playing){State=RushState.Paused;velocity=Vector2.zero;dashLeft=0;IsDashing=false;IsBracing=false;}
            else if(State==RushState.Paused)State=RushState.Playing;
            LockActions();Log("pause_toggle");
        }
        void ResetRun()
        {
            TotalTime=0; StageTime=0; Collisions=0; stageStartCollisions=0;TransitionCount=0;
            SplitTimes=new float[3];SplitCollisions=new int[3]; BuildStage(0);
        }
        void LockActions(){actionLocked=true;dashPressed=false;braceInput=false;QADash=false;QABrace=false;moveInput=Vector2.zero;}
        void BuildStage(int index)
        {
            if(world!=null && world.root!=null)Destroy(world.root.gameObject);
            if(player!=null)Destroy(player.gameObject);
            Stage=index;StageTime=0;cooldown=0;dashLeft=0;IsDashing=false;IsBracing=false;
            velocity=Vector2.zero;facing=Vector2.up;impact=0;shake=0;collisionTimes.Clear();crowds.Clear();
            world=RushWorld.Build(index);position=new Vector2(world.spawn.x,world.spawn.z);
            player=new GameObject("Player collision root").transform;
            visual=RushVisuals.CreatePerson(player,new Color(1f,.38f,.1f),true,index==0);
            player.position=new Vector3(position.x,0,position.y);
            var r=GameObject.CreatePrimitive(PrimitiveType.Cylinder);Destroy(r.GetComponent<Collider>());
            r.name="Player orange locator";ring=r.transform;ring.SetParent(player,false);ring.localPosition=new Vector3(0,.14f,0);ring.localScale=new Vector3(1.2f,.018f,1.2f);
            var rm=new Material(Shader.Find("Standard"));rm.color=new Color(1f,.61f,.18f);r.GetComponent<Renderer>().material=rm;
            trail=new GameObject("Dash ribbon").AddComponent<LineRenderer>();trail.transform.SetParent(player,false);
            trail.material=new Material(Shader.Find("Sprites/Default"));trail.startColor=new Color(1f,.76f,.3f,.8f);trail.endColor=new Color(1f,.85f,.5f,0);trail.startWidth=.34f;trail.endWidth=.04f;trail.positionCount=10;trail.enabled=false;
            for(int i=0;i<10;i++)trailPoints[i]=player.position+Vector3.up*.3f;
            int n=0;
            foreach(var spawn in world.crowd)
            {
                var c=new Crowd();c.origin=spawn.position;c.axis=spawn.axis.sqrMagnitude>.01f?spawn.axis.normalized:Vector2.right;c.range=spawn.range;c.speed=spawn.speed;c.phase=spawn.phase>=0?spawn.phase:n*1.71f;n++;
                c.position=Resolve(c.origin+c.axis*(Mathf.Sin(c.phase)*c.range),.40f,false);
                var root=new GameObject("Commuter").transform;root.SetParent(world.root,false);
                c.visual=RushVisuals.CreatePerson(root,spawn.color,false,spawn.cyclist);root.position=new Vector3(c.position.x,0,c.position.y);crowds.Add(c);
            }
            ResetCamera();LockActions();Log("stage_loaded");
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.M))ToggleSound();
            if(Input.GetKeyDown(KeyCode.Escape))
            {if(State==RushState.Playing||State==RushState.Paused)TogglePause();else if(State==RushState.Tutorial)ToMenu();}
            if(Input.GetKeyDown(KeyCode.Return) && !actionLocked)
            {if(State==RushState.Menu)StartRun();else if(State==RushState.Tutorial)BeginPlay();else if(State==RushState.Results)Restart();}
            if(Input.GetKeyDown(KeyCode.R)&&(State==RushState.Paused||State==RushState.Results))Restart();
            bool held=Input.GetKey(KeyCode.Space)||Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)||Input.GetKey(KeyCode.Return);
            if(actionLocked&&!held&&!QADash&&!QABrace)actionLocked=false;
            if(State==RushState.Playing)
            {
                TotalTime+=Time.deltaTime;StageTime+=Time.deltaTime;
                if(QAInput){moveInput=Vector2.ClampMagnitude(QAMove,1);braceInput=!actionLocked&&QABrace;if(!actionLocked&&QADash)dashPressed=true;QADash=false;}
                else
                {
                    float x=(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0);
                    float y=(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?1:0);
                    moveInput=Vector2.ClampMagnitude(new Vector2(x,y),1);
                    braceInput=!actionLocked&&(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift));
                    if(!actionLocked&&Input.GetKeyDown(KeyCode.Space))dashPressed=true;
                }
            }
            else if(State==RushState.Transition)
            {
                TransitionLeft=Mathf.Max(0,1.55f-(Time.unscaledTime-transitionBeginTime));
                if(TransitionLeft<=0){BuildStage(Stage+1);State=RushState.Playing;Sound(tickClip);}
            }
            else{moveInput=Vector2.zero;braceInput=false;dashPressed=false;}
            impact=Mathf.MoveTowards(impact,0,Time.deltaTime*4);
            shake=Mathf.MoveTowards(shake,0,Time.deltaTime*1.5f);
            collisionSoundCooldown-=Time.deltaTime;
            // Camera follows in LateUpdate. Pause freezes the visual player as well.
            if(player!=null&&State==RushState.Playing)
            {
                player.position=Vector3.Lerp(player.position,new Vector3(position.x,0,position.y),1-Mathf.Exp(-Time.deltaTime*40));
                if(facing.sqrMagnitude>.01f)visual.rotation=Quaternion.Slerp(visual.rotation,Quaternion.LookRotation(new Vector3(facing.x,0,facing.y)),1-Mathf.Exp(-Time.deltaTime*18));
                RushVisuals.AnimatePerson(visual,State==RushState.Playing?velocity:Vector2.zero,Time.time,IsDashing,IsBracing);
                visual.localScale=new Vector3(1+impact*.14f,1-impact*.18f,1+impact*.14f);
                ring.localScale=Vector3.one*(IsBracing?1.45f:1.2f);ring.localScale=new Vector3(ring.localScale.x,.018f,ring.localScale.z);
                for(int i=9;i>0;i--)trailPoints[i]=trailPoints[i-1];trailPoints[0]=player.position+Vector3.up*.3f;trail.SetPositions(trailPoints);trail.enabled=IsDashing;
            }
        }
        Vector3 CameraTarget()
        {
            var target=new Vector3(position.x+cameraLook.x,0,position.y+cameraLook.y);
            float hx=Mathf.Min(cameraRig.orthographicSize*cameraRig.aspect,(world.boundsMax.x-world.boundsMin.x)*.5f);
            float hz=Mathf.Min(cameraRig.orthographicSize/(28f/Mathf.Sqrt(28f*28f+17f*17f)),(world.boundsMax.y-world.boundsMin.y)*.5f);
            target.x=Mathf.Clamp(target.x,world.boundsMin.x+hx,world.boundsMax.x-hx);
            target.z=Mathf.Clamp(target.z,world.boundsMin.y+hz,world.boundsMax.y-hz);
            return target;
        }
        void ResetCamera()
        {
            cameraLook=Vector2.zero;cameraVelocity=Vector3.zero;
            cameraRig.orthographicSize=Mathf.Max(9.5f,6f/Mathf.Max(.5f,cameraRig.aspect));
            cameraFocus=CameraTarget();cameraRig.transform.position=cameraFocus+cameraHome;
        }
        void LateUpdate()
        {
            if(world==null||State!=RushState.Playing)return;
            cameraRig.orthographicSize=Mathf.Max(9.5f,6f/Mathf.Max(.5f,cameraRig.aspect));
            Vector2 ahead=Vector2.ClampMagnitude(velocity*.38f,3.2f);
            cameraLook=Vector2.Lerp(cameraLook,ahead,1-Mathf.Exp(-Time.deltaTime*5));
            cameraFocus=Vector3.SmoothDamp(cameraFocus,CameraTarget(),ref cameraVelocity,.14f,80f,Time.deltaTime);
            cameraRig.transform.position=cameraFocus+cameraHome;
        }
        void FixedUpdate()
        {
            if(State!=RushState.Playing)return;
            float dt=Time.fixedDeltaTime;
            cooldown=Mathf.Max(0,cooldown-dt);dashLeft=Mathf.Max(0,dashLeft-dt);
            IsBracing=braceInput;
            if(IsBracing)dashLeft=0;
            if(dashPressed && !IsBracing && cooldown<=0)
            {dashLeft=.20f;cooldown=1.15f;if(moveInput.sqrMagnitude>.04f)facing=moveInput.normalized;Sound(dashClip);Log("dash");}
            dashPressed=false;IsDashing=dashLeft>0;
            float speed=(Stage==0?6.1f:4.9f)*(IsBracing?.48f:1f);
            if(IsDashing)velocity=facing*(Stage==0?13f:11.3f);
            else
            {
                float acceleration=Stage==0?16f:29f;
                if(moveInput.sqrMagnitude<.01f)acceleration=Stage==0?14f:34f;
                if(IsBracing)acceleration=44f;
                velocity=Vector2.MoveTowards(velocity,moveInput*speed,acceleration*dt);
                if(moveInput.sqrMagnitude>.04f)facing=moveInput.normalized;
            }
            MovePlayer(velocity*dt);
            for(int i=0;i<crowds.Count;i++)
            {
                Crowd c=crowds[i];Vector2 old=c.position;
                c.pushVelocity=Vector2.MoveTowards(c.pushVelocity,Vector2.zero,dt*15);c.offset+=c.pushVelocity*dt;c.offset=Vector2.ClampMagnitude(Vector2.MoveTowards(c.offset,Vector2.zero,dt*.65f),2.2f);
                c.position=c.origin+c.axis*(Mathf.Sin(StageTime*c.speed+c.phase)*c.range)+c.offset;
                c.position=Resolve(c.position,.40f,false);
                Vector2 delta=position-c.position;float distance=delta.magnitude;
                if(distance<Radius+.43f)
                {
                    Vector2 normal=distance>.001f?delta/distance:Vector2.right;
                    float overlap=Radius+.43f-distance;
                    float crowdShare=IsDashing?.70f:IsBracing?.86f:.45f;
                    c.offset-=normal*overlap*crowdShare;c.position-=normal*overlap*crowdShare;
                    position+=normal*overlap*(1-crowdShare);
                    c.pushVelocity-=normal*(IsDashing?5f:IsBracing?1.7f:1.2f);
                    c.pushVelocity=Vector2.ClampMagnitude(c.pushVelocity,7);
                    float into=Vector2.Dot(velocity,normal);
                    if(into<0 && !IsDashing)velocity-=normal*into*(IsBracing?.1f:.55f);
                    RegisterHit(1000+i,IsDashing?1f:.55f);
                    // Save bursts for visible gaps; contact costs momentum.
                    if(IsDashing){dashLeft=0;IsDashing=false;velocity*=.5f;}
                }
                c.offset=Vector2.ClampMagnitude(c.offset,2.2f);
                c.position=Resolve(c.origin+c.axis*(Mathf.Sin(StageTime*c.speed+c.phase)*c.range)+c.offset,.40f,false);
                c.visual.parent.position=new Vector3(c.position.x,0,c.position.y);
                Vector2 cv=(c.position-old)/dt;
                if(cv.sqrMagnitude>.02f)c.visual.rotation=Quaternion.Slerp(c.visual.rotation,Quaternion.LookRotation(new Vector3(cv.x,0,cv.y)),dt*7);
                RushVisuals.AnimatePerson(c.visual,cv,Time.time+i,false,false);
            }
            position=Resolve(position,Radius,false);
            Vector2 goal=new Vector2(world.goal.x,world.goal.z);
            if(Mathf.Abs(position.x-goal.x)<world.goalHalfSize.x && Mathf.Abs(position.y-goal.y)<world.goalHalfSize.y)CompleteStage();
        }
        void MovePlayer(Vector2 delta)
        {
            int count=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/.14f));
            for(int i=0;i<count;i++)position=Resolve(position+delta/count,Radius,true);
        }
        Vector2 Resolve(Vector2 p,float radius,bool countHits)
        {
            for(int pass=0;pass<4;pass++)
            {
                for(int i=0;i<world.obstacles.Count;i++)
                {
                    Rect b=world.obstacles[i];Vector2 q=new Vector2(Mathf.Clamp(p.x,b.xMin,b.xMax),Mathf.Clamp(p.y,b.yMin,b.yMax));
                    Vector2 d=p-q;float len=d.magnitude;
                    if(len>=radius)continue;
                    Vector2 normal;
                    if(len>.00001f){normal=d/len;p+=normal*(radius-len+.0005f);}
                    else
                    {
                        float left=p.x-b.xMin,right=b.xMax-p.x,down=p.y-b.yMin,up=b.yMax-p.y;
                        float min=Mathf.Min(left,right,down,up);
                        if(min==left){normal=Vector2.left;p.x=b.xMin-radius;}else if(min==right){normal=Vector2.right;p.x=b.xMax+radius;}else if(min==down){normal=Vector2.down;p.y=b.yMin-radius;}else{normal=Vector2.up;p.y=b.yMax+radius;}
                    }
                    if(countHits)
                    {
                        float into=Vector2.Dot(velocity,normal);
                        if(into<-2)RegisterHit(i,Mathf.Clamp01(-into/10));
                        if(into<0)velocity-=normal*into;
                    }
                }
                float minX=world.boundsMin.x+radius,maxX=world.boundsMax.x-radius;
                float minY=world.boundsMin.y+radius,maxY=world.boundsMax.y-radius;
                // A boundary blocks motion exactly like a solid obstacle: discard the
                // outward component so reversing away from a wall responds immediately.
                if(countHits)
                {
                    if((p.x<minX&&velocity.x<0)||(p.x>maxX&&velocity.x>0))velocity.x=0;
                    if((p.y<minY&&velocity.y<0)||(p.y>maxY&&velocity.y>0))velocity.y=0;
                }
                p.x=Mathf.Clamp(p.x,minX,maxX);
                p.y=Mathf.Clamp(p.y,minY,maxY);
            }
            return p;
        }
        void RegisterHit(int id,float strength)
        {
            if(collisionTimes.TryGetValue(id,out float time)&&StageTime-time<.65f)return;
            collisionTimes[id]=StageTime;Collisions++;impact=Mathf.Max(impact,strength);shake=Mathf.Max(shake,strength);
            if(collisionSoundCooldown<=0){Sound(bumpClip,.45f+strength*.4f);collisionSoundCooldown=.1f;}
        }
        void CompleteStage()
        {
            if(State!=RushState.Playing)return;
            SplitTimes[Stage]=StageTime;SplitCollisions[Stage]=Collisions-stageStartCollisions;stageStartCollisions=Collisions;
            velocity=Vector2.zero;dashLeft=0;IsDashing=false;IsBracing=false;LockActions();TransitionCount++;
            Sound(goalClip);Log("stage_complete");
            if(Stage==2){State=RushState.Results;Log(TotalTime<=TimeLimit?"result_on_time":"result_late");}
            else{State=RushState.Transition;transitionBeginTime=Time.unscaledTime;TransitionLeft=1.55f;}
        }
        void OnApplicationFocus(bool focus){if(!focus&&qa==null&&State==RushState.Playing)TogglePause();}
        void OnGUI(){RushUI.Draw(this);}
        public void Log(string evt){Debug.Log("RUSH_EVENT "+evt+" stage="+Stage+" time="+TotalTime.ToString("F3")+" pos="+position+" collisions="+Collisions+" transitions="+TransitionCount);}
    }
}
