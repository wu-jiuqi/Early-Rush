using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

namespace EarlyRush
{
    public enum RushState { Menu, Tutorial, Playing, Paused, Transition, Results }
    public partial class RushGame : MonoBehaviour
    {
        public RushState State = RushState.Menu;
        public int Stage;
        public float TotalTime, TimeLimit = 110f, StageTime, TransitionLeft;
        public int Collisions;
        public float[] SplitTimes = new float[3];
        public int[] SplitCollisions = new int[3];
        public bool Muted, IsBracing, IsDashing;
        public bool ScreenShakeEnabled = true;
        public void ToggleScreenShake(){ScreenShakeEnabled=!ScreenShakeEnabled;if(!ScreenShakeEnabled)cameraRig.transform.position=cameraFocus+cameraHome;}
        public bool IsStumbling => stumbleLeft > 0;
        public float TotalStumbleTime { get; private set; }
        public float LongestStumbleTime { get; private set; }
        public int StumbleTriggers { get; private set; }
        public float StumbleProtectionRemaining => stumbleProtectionLeft;
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
        float cooldown, dashLeft, stumbleLeft, stumbleProtectionLeft, continuousStumbleTime, impact, shake, collisionSoundCooldown;
        float transitionBeginTime;
        int stageStartCollisions;
        List<Crowd> crowds = new List<Crowd>();
        Dictionary<int,float> collisionTimes = new Dictionary<int,float>();
        AudioSource audioSource;
        AudioClip dashClip, bumpClip, goalClip, tickClip, launchClip;
        LineRenderer trail;
        Material locatorMaterial,trailMaterial;
        Vector3[] trailPoints = new Vector3[10];
        RushQA qa;
        public bool QAInput;
        public Vector2 QAMove;
        public bool QABrace, QADash;
        const float Radius = .42f;
        class Crowd
        {
            public Transform root, visual;
            public RushPersonParts parts;
            public Vector2 origin, position, previous, offset, pushVelocity, heading;
            public Vector2[] route;
            public int waypoint, originWaypoint, generation;
            public float speed, startAt, recycleAt, progress, stuckTime, correctionDistanceLeft;
            public bool active = true, crossedGoal, touchingPlayer, fixtureDisabled, airborne;
            public Vector2 flightVelocity, launchDirection, lastVelocity;
            public float height, verticalVelocity, recoveryLeft, spinDegrees, airTime, landedAt=-1, hitAge=-1, landingAge=-1, jellyStrength, landingSpin;
            public int hitDashId=-1, chainDepth, launchCount, landCount;
            public float windup01;
            public Vector2 windupDirection;
        }
        // Physical roots remain unit scale; all squash, tumble and bounce lives below them.
        public struct ImpactProbe
        {
            public Vector2 position, velocity, launchDirection, separationOffset;
            public bool active, airborne, recovering;
            public float height, recoveryLeft, spinDegrees, landedAt, correctionBudgetLeft;
            public int dashId, chainDepth, launchCount, landCount, generation;
            public Vector3 visualScale, rootScale;
            public Quaternion visualRotation;
        }
        class ImpactBurst
        {
            public HashSet<int> targets=new HashSet<int>();
            public bool stopped;
            public int owner=-1;
        }
        readonly Dictionary<int,ImpactBurst> impactBursts=new Dictionary<int,ImpactBurst>();
        RushImpactFx impactFx;
        bool impactQaMode;
        float impactFreezeLeft, nextImpactStopAt, playerHitAge=-1, playerJellyStrength;
        Vector2 playerTickStart;
        float playerSeparationLeft;
        public int CurrentDashId { get; private set; }
        public int DashImpactCount => impactBursts.TryGetValue(CurrentDashId,out var burst)?burst.targets.Count:0;
        public int TotalLaunches { get; private set; }
        public int ChainLaunches { get; private set; }
        public int MaxObservedChainDepth { get; private set; }
        public int MaxObservedDashTargets { get; private set; }
        public int NormalContactCount { get; private set; }
        public float ImpactFreezeRemaining => impactFreezeLeft;
        public float ImpactFxClock => impactFx==null?0:impactFx.Clock;
        public int ActiveImpactFxCount => impactFx==null?0:impactFx.ActiveCount;
        public Vector3 PlayerVisualScale => visual?visual.localScale:Vector3.one;
        public Vector3 PlayerRootScale => player?player.localScale:Vector3.one;
        public float PlayerCollisionRadius => Radius;
        public bool ImpactFixtureReady { get; private set; }
        public string ImpactFixtureKind { get; private set; } = "";
        public ImpactProbe[] ImpactProbeSnapshot()
        {
            var result=new ImpactProbe[crowds.Count];
            for(int i=0;i<crowds.Count;i++)
            {
                var c=crowds[i];
                result[i]=new ImpactProbe{position=c.position,velocity=c.airborne?c.flightVelocity:c.lastVelocity,launchDirection=c.launchDirection,
                    active=c.active,airborne=c.airborne,recovering=c.recoveryLeft>0,height=c.height,recoveryLeft=c.recoveryLeft,
                    spinDegrees=c.spinDegrees,landedAt=c.landedAt,dashId=c.hitDashId,chainDepth=c.chainDepth,
                    launchCount=c.launchCount,landCount=c.landCount,generation=c.generation,
                    visualScale=c.visual.localScale,rootScale=c.root.localScale,visualRotation=c.visual.rotation,
                    separationOffset=c.offset,correctionBudgetLeft=c.correctionDistanceLeft};
            }
            return result;
        }
        public struct CrowdProbe
        {
            public Vector2 position;
            public float progress;
            public int generation;
            public bool active, visible;
        }
        public int CrowdArrivals { get; private set; }
        public int CrowdGoalCrossings { get; private set; }
        public int CrowdRecycles { get; private set; }
        public int CrowdVisibleRecycles { get; private set; }
        public float CrowdForwardDistance { get; private set; }
        public float CrowdBackwardDistance { get; private set; }
        public int CrowdPopulation => crowds.Count;
        public int CrowdActiveCount
        {
            get { int count=0; foreach(var c in crowds)if(c.active)count++; return count; }
        }
        public int CrowdVisibleCount
        {
            get { int count=0; foreach(var c in crowds)if(c.active&&CrowdInView(c.position,0))count++; return count; }
        }
        public CrowdProbe[] CrowdProbeSnapshot()
        {
            var result=new CrowdProbe[crowds.Count];
            for(int i=0;i<crowds.Count;i++)
            {
                var c=crowds[i];
                result[i]=new CrowdProbe{position=c.position,progress=c.progress,generation=c.generation,active=c.active,visible=c.active&&CrowdInView(c.position,0)};
            }
            return result;
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
            launchClip = MakeSound("rubbery thwump", 185, .22f, 3);
            gameObject.AddComponent<RushRouteHUD>();
            BuildStage(0);
            impactQaMode=Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--impact-qa");
            combatQaMode=Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--combat-qa");
            if(combatQaMode)gameObject.AddComponent<RushCombatQA>();
            else if(impactQaMode)gameObject.AddComponent<RushImpactQA>();
            else if(Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--qa"))qa=gameObject.AddComponent<RushQA>();
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
                float tone=Mathf.Sin(i/22050f*frequency*Mathf.PI*2)*.7f;
                if(kind==3)tone=Mathf.Sin(i/22050f*(260f-185f*t)*Mathf.PI*2)*.7f+(float)(random.NextDouble()*2-1)*.23f*Mathf.Exp(-t*9);
                data[i]=(tone+(kind==0?(float)(random.NextDouble()*2-1)*.3f:0))*envelope*(kind==3?.36f:.22f);
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
            if(State==RushState.Playing){ClearImpactTransients();State=RushState.Paused;velocity=Vector2.zero;dashLeft=0;IsDashing=false;IsBracing=false;}
            else if(State==RushState.Paused)State=RushState.Playing;
            LockActions();Log("pause_toggle");
        }
        void ResetRun()
        {
            TotalTime=0; StageTime=0; Collisions=0; stageStartCollisions=0;TransitionCount=0;
            TotalStumbleTime=0;LongestStumbleTime=0;StumbleTriggers=0;
            ResetCombatRunStats();
            CurrentDashId=0;TotalLaunches=0;ChainLaunches=0;MaxObservedChainDepth=0;MaxObservedDashTargets=0;NormalContactCount=0;
            SplitTimes=new float[3];SplitCollisions=new int[3]; BuildStage(0);
        }
        void LockActions(){actionLocked=true;dashPressed=false;braceInput=false;QADash=false;QABrace=false;moveInput=Vector2.zero;}
        void BuildStage(int index)
        {
            var buildWatch=System.Diagnostics.Stopwatch.StartNew();
            ClearImpactScene();ImpactFixtureReady=false;ImpactFixtureKind="";impactBursts.Clear();
            if(world!=null && world.root!=null)Destroy(world.root.gameObject);
            if(player!=null)Destroy(player.gameObject);
            Stage=index;StageTime=0;cooldown=0;dashLeft=0;stumbleLeft=0;stumbleProtectionLeft=0;continuousStumbleTime=0;IsDashing=false;IsBracing=false;
            CrowdArrivals=0;CrowdGoalCrossings=0;CrowdRecycles=0;CrowdVisibleRecycles=0;CrowdForwardDistance=0;CrowdBackwardDistance=0;
            velocity=Vector2.zero;facing=Vector2.up;impact=0;shake=0;collisionTimes.Clear();crowds.Clear();
            world=RushWorld.Build(index);position=new Vector2(world.spawn.x,world.spawn.z);
            impactFx=new RushImpactFx(world.root);
            player=new GameObject("Player collision root").transform;
            visual=RushVisuals.CreatePerson(player,new Color(1f,.38f,.1f),true,index==0);
            player.position=new Vector3(position.x,0,position.y);
            var r=GameObject.CreatePrimitive(PrimitiveType.Cylinder);Destroy(r.GetComponent<Collider>());
            r.name="Player orange locator";ring=r.transform;ring.SetParent(player,false);ring.localPosition=new Vector3(0,.14f,0);ring.localScale=new Vector3(1.2f,.018f,1.2f);
            if(!locatorMaterial){locatorMaterial=new Material(Shader.Find("Standard"));locatorMaterial.color=new Color(1f,.61f,.18f);}
            r.GetComponent<Renderer>().sharedMaterial=locatorMaterial;
            trail=new GameObject("Dash ribbon").AddComponent<LineRenderer>();trail.transform.SetParent(player,false);
            if(!trailMaterial)trailMaterial=new Material(Shader.Find("Sprites/Default"));
            trail.sharedMaterial=trailMaterial;trail.startColor=new Color(1f,.76f,.3f,.8f);trail.endColor=new Color(1f,.85f,.5f,0);trail.startWidth=.34f;trail.endWidth=.04f;trail.positionCount=10;trail.enabled=false;
            for(int i=0;i<10;i++)trailPoints[i]=player.position+Vector3.up*.3f;
            foreach(var spawn in world.crowd)
            {
                var c=new Crowd();
                c.route=spawn.route!=null&&spawn.route.Length>1?spawn.route:new[]{spawn.position,world.crowdExit};
                c.originWaypoint=Mathf.Clamp(spawn.routeIndex,1,c.route.Length-1);c.waypoint=c.originWaypoint;
                c.origin=Resolve(spawn.position,.40f,false);c.position=c.origin;c.previous=c.position;
                c.speed=spawn.desiredSpeed>0?spawn.desiredSpeed:3.1f;c.startAt=spawn.delay;
                c.heading=(c.route[c.waypoint]-c.position).normalized;
                c.root=new GameObject("Commuter "+crowds.Count).transform;c.root.SetParent(world.root,false);
                c.visual=RushVisuals.CreatePerson(c.root,spawn.color,false,spawn.cyclist);
                c.parts=c.visual.GetComponent<RushPersonParts>();
                c.root.position=new Vector3(c.position.x,0,c.position.y);
                if(c.heading.sqrMagnitude>.01f)c.visual.rotation=Quaternion.LookRotation(new Vector3(c.heading.x,0,c.heading.y));
                crowds.Add(c);
            }
            InitializeCombat();ResetCamera();LockActions();Log("stage_loaded");
            buildWatch.Stop();Debug.Log("RUSH_BUILD_STAGE_MS stage="+Stage+" ms="+buildWatch.Elapsed.TotalMilliseconds.ToString("F2"));
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
                float frozen=impactFreezeLeft;impactFreezeLeft=Mathf.Max(0,impactFreezeLeft-Time.deltaTime);
                TickImpactPresentation(Mathf.Max(0,Time.deltaTime-frozen));
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
            if(State==RushState.Playing)
            {
                impact=Mathf.MoveTowards(impact,0,Time.deltaTime*4);
                if(impactFreezeLeft<=0)shake=Mathf.MoveTowards(shake,0,Time.deltaTime*2.6f);
                collisionSoundCooldown-=Time.deltaTime;
            }
            // Camera follows in LateUpdate. Pause freezes the visual player as well.
            if(player!=null&&State==RushState.Playing)
            {
                if(impactFreezeLeft<=0)
                {
                    player.position=Vector3.Lerp(player.position,new Vector3(position.x,0,position.y),1-Mathf.Exp(-Time.deltaTime*40));
                    if(!PlayerAirborne&&!PlayerRecovering&&facing.sqrMagnitude>.01f)visual.rotation=Quaternion.Slerp(visual.rotation,Quaternion.LookRotation(new Vector3(facing.x,0,facing.y)),1-Mathf.Exp(-Time.deltaTime*18));
                    if(!PlayerAirborne&&!PlayerRecovering)RushVisuals.AnimatePerson(visual,velocity,ImpactFxClock,IsDashing,IsBracing);
                    for(int i=9;i>0;i--)trailPoints[i]=trailPoints[i-1];
                    trailPoints[0]=player.position+Vector3.up*.3f;trail.SetPositions(trailPoints);trail.enabled=IsDashing;
                }
                ApplyPlayerCombatPresentation();
                ring.localScale=Vector3.one*(IsBracing?1.45f:1.2f);ring.localScale=new Vector3(ring.localScale.x,.018f,ring.localScale.z);
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
            if(impactFreezeLeft<=0)
            {
                Vector2 ahead=Vector2.ClampMagnitude(velocity*.38f,3.2f);
                cameraLook=Vector2.Lerp(cameraLook,ahead,1-Mathf.Exp(-Time.deltaTime*5));
                cameraFocus=Vector3.SmoothDamp(cameraFocus,CameraTarget(),ref cameraVelocity,.14f,80f,Time.deltaTime);
            }
            Vector3 kick=ScreenShakeEnabled?new Vector3(Mathf.Sin(ImpactFxClock*93),Mathf.Sin(ImpactFxClock*77)*.35f,0)*shake*.065f:Vector3.zero;
            cameraRig.transform.position=cameraFocus+cameraHome+kick;
        }
        void FixedUpdate()
        {
            if(State!=RushState.Playing||impactFreezeLeft>0)return;
            float dt=Time.fixedDeltaTime;
            cooldown=Mathf.Max(0,cooldown-dt);dashLeft=Mathf.Max(0,dashLeft-dt);
            playerTickStart=position;
            TickCombat(dt);
            IsBracing=braceInput&&!PlayerAirborne&&!PlayerRecovering;
            // A global recovery window prevents a stream of new bodies from extending
            // one stumble indefinitely. Bracing cancels the stumble but keeps protection.
            stumbleProtectionLeft=Mathf.Max(0,stumbleProtectionLeft-dt);
            float impaired=IsBracing?0:Mathf.Min(stumbleLeft,dt);
            if(impaired>0)
            {
                TotalStumbleTime+=impaired;continuousStumbleTime+=impaired;
                LongestStumbleTime=Mathf.Max(LongestStumbleTime,continuousStumbleTime);
            }
            stumbleLeft=IsBracing?0:Mathf.Max(0,stumbleLeft-dt);
            if(stumbleLeft<=0)continuousStumbleTime=0;
            if(IsBracing)dashLeft=0;
            if(dashPressed && !IsBracing && !IsStumbling && !PlayerAirborne&&!PlayerRecovering&&cooldown<=0)
            {dashLeft=.20f;cooldown=1.15f;if(moveInput.sqrMagnitude>.04f)facing=moveInput.normalized;
                CurrentDashId++;impactBursts[CurrentDashId]=new ImpactBurst();
                if(CurrentDashId>4)impactBursts.Remove(CurrentDashId-4);
                Sound(dashClip);Log("dash");}
            dashPressed=false;IsDashing=dashLeft>0;
            if(PlayerAirborne||PlayerRecovering){dashLeft=0;IsDashing=false;SimulatePlayerImpact(dt);}
            else
            {
            float speed=(Stage==0?6.1f:4.9f)*(IsBracing?.48f:IsStumbling?.60f:1f);
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
            }
            playerSeparationLeft=dt*3.6f;
            UpdateCrowds(dt);
            position=Resolve(position,Radius,false);
            Vector2 goal=new Vector2(world.goal.x,world.goal.z);
            if(!PlayerAirborne&&!PlayerRecovering&&Mathf.Abs(position.x-goal.x)<world.goalHalfSize.x && Mathf.Abs(position.y-goal.y)<world.goalHalfSize.y)CompleteStage();
        }
        bool CrowdInView(Vector2 point,float margin)
        {
            Vector3 v=cameraRig.WorldToViewportPoint(new Vector3(point.x,1f,point.y));
            return v.z>0&&v.x>=-margin&&v.x<=1+margin&&v.y>=-margin&&v.y<=1+margin;
        }
        Vector2 MoveCrowd(Vector2 from,Vector2 displacement)
        {
            // Swept circle movement preserves thin barriers even during a dash shove.
            int steps=Mathf.Max(1,Mathf.CeilToInt(displacement.magnitude/.14f));
            for(int s=0;s<steps;s++)from=Resolve(from+displacement/steps,.40f,false);
            return from;
        }
        void PushCrowd(Crowd c,Vector2 displacement)
        {
            // This budget bounds accumulated local displacement; it never snaps a commuter
            // back to a scripted origin, so yielding and going round another person persists.
            // Share one real-distance budget across every correction in this physics tick.
            // Deep overlap after recovery must be resolved gradually, not as a one-frame jump.
            if(c.correctionDistanceLeft<=.00001f)return;
            Vector2 next=Vector2.ClampMagnitude(c.offset+displacement,2.2f);
            Vector2 before=c.position;
            c.position=MoveCrowd(c.position,Vector2.ClampMagnitude(next-c.offset,c.correctionDistanceLeft));
            c.correctionDistanceLeft=Mathf.Max(0,c.correctionDistanceLeft-Vector2.Distance(c.position,before));
            c.offset=Vector2.ClampMagnitude(c.offset+c.position-before,2.2f);
        }
        bool TryRecycle(Crowd c,int id)
        {
            if(c.fixtureDisabled||StageTime<c.recycleAt)return false;
            c.recycleAt=StageTime+.3f;
            Vector2 entry=Resolve(c.origin,.40f,false);
            // Test the actual resolved position, not an unchecked authored spawn point.
            if(CrowdInView(entry,.18f)||Vector2.Distance(entry,position)<12f)return false;
            for(int i=0;i<crowds.Count;i++)
                if(crowds[i].active&&(crowds[i].position-entry).sqrMagnitude<1.05f*1.05f)return false;
            c.position=entry;c.previous=entry;c.waypoint=c.originWaypoint;
            ResetCrowdImpact(c);
            c.pushVelocity=Vector2.zero;c.offset=Vector2.zero;c.progress=0;c.stuckTime=0;
            c.startAt=StageTime+.1f+(id%5)*.07f;c.crossedGoal=false;c.touchingPlayer=false;
            c.heading=(c.route[c.waypoint]-entry).normalized;
            c.generation++;c.active=true;CrowdRecycles++;
            if(CrowdInView(entry,.18f))CrowdVisibleRecycles++;
            c.root.position=new Vector3(entry.x,0,entry.y);c.root.gameObject.SetActive(true);
            return true;
        }
        const float OrdinaryBodyFloor=.60f;
        Vector2 ConstrainOrdinaryStep(Crowd actor,Vector2 start,Vector2 step)
        {
            // The .78m soft separator still does the spreading. This smaller floor only
            // stops intentional walking from continuously deepening a crowded queue.
            float reach=OrdinaryBodyFloor+step.magnitude;
            for(int pass=0;pass<2;pass++)
                for(int j=0;j<crowds.Count;j++)
                {
                    var other=crowds[j];if(other==actor||!other.active||other.airborne)continue;
                    Vector2 delta=start-other.position;float sq=delta.sqrMagnitude;
                    if(sq<.00000001f||sq>reach*reach)continue;
                    float distance=Mathf.Sqrt(sq);Vector2 normal=delta/distance;
                    float allowed=Mathf.Max(0,distance-OrdinaryBodyFloor);
                    float inward=-Vector2.Dot(step,normal);
                    if(inward>allowed)step+=normal*(inward-allowed);
                }
            // A later projection can slightly violate an earlier plane. Scale the
            // remaining step to their tightest common limit without allocating lists.
            float fraction=1f;
            for(int j=0;j<crowds.Count;j++)
            {
                var other=crowds[j];if(other==actor||!other.active||other.airborne)continue;
                Vector2 delta=start-other.position;float sq=delta.sqrMagnitude;
                if(sq<.00000001f||sq>reach*reach)continue;
                float distance=Mathf.Sqrt(sq),inward=-Vector2.Dot(step,delta/distance);
                float allowed=Mathf.Max(0,distance-OrdinaryBodyFloor);
                if(inward>allowed+.000001f)fraction=Mathf.Min(fraction,allowed/inward);
            }
            return step*fraction;
        }
        bool OrdinaryStepRespectsBodies(Crowd actor,Vector2 start,Vector2 actualStep)
        {
            float reach=OrdinaryBodyFloor+actualStep.magnitude;
            for(int j=0;j<crowds.Count;j++)
            {
                var other=crowds[j];if(other==actor||!other.active||other.airborne)continue;
                Vector2 delta=start-other.position;float sq=delta.sqrMagnitude;
                if(sq<.00000001f||sq>reach*reach)continue;
                float distance=Mathf.Sqrt(sq);
                if(-Vector2.Dot(actualStep,delta/distance)>Mathf.Max(0,distance-OrdinaryBodyFloor)+.00001f)return false;
            }
            return true;
        }
        Vector2 MoveOrdinaryCrowd(Crowd actor,Vector2 desiredStep)
        {
            Vector2 start=actor.position,attempt=desiredStep;
            for(int retry=0;retry<4;retry++)
            {
                Vector2 step=ConstrainOrdinaryStep(actor,start,attempt);
                if(step.sqrMagnitude<.0000000001f)return start;
                Vector2 resolved=MoveCrowd(start,step);
                Vector2 actual=resolved-start;
                if(OrdinaryStepRespectsBodies(actor,start,actual))return resolved;
                // Static corner resolution can redirect a step. Re-project the real
                // displacement and shorten it; if no safe step exists, wait this tick.
                attempt=actual*.5f;
            }
            return start;
        }
        void SeparateCommittedCrowds()
        {
            // Keep the telegraphed origin fixed while yielding real space around it.
            // This pass precedes ordinary pair separation so those pairs cannot consume
            // the entire remaining correction budget before a fixed body is considered.
            for(int i=0;i<crowds.Count;i++)
            {
                var anchor=crowds[i];
                if(!anchor.active||anchor.airborne||!CombatCommitted(i))continue;
                for(int j=0;j<crowds.Count;j++)
                {
                    if(i==j)continue;
                    var other=crowds[j];
                    if(!other.active||other.airborne||CombatCommitted(j))continue;
                    Vector2 delta=other.position-anchor.position;float sq=delta.sqrMagnitude;
                    if(sq>=.78f*.78f)continue;
                    float distance=Mathf.Sqrt(sq);
                    Vector2 normal=distance>.001f?delta/distance:(j%2==0?Vector2.right:Vector2.left);
                    PushCrowd(other,normal*Mathf.Min(.11f,.78f-distance));
                }
            }
        }
        void UpdateCrowds(float dt)
        {
            // All agents simulate outside the camera; only their limb animation is culled.
            for(int i=0;i<crowds.Count;i++)
            {
                Crowd c=crowds[i];
                if(!c.active&&!TryRecycle(c,i))continue;
                c.previous=c.position;
                c.correctionDistanceLeft=dt*3f;
                if(c.airborne){SimulateFlight(c,dt);continue;}
                if(c.recoveryLeft>0)
                {
                    c.recoveryLeft=Mathf.Max(0,c.recoveryLeft-dt);
                    if(c.recoveryLeft<=0)RejoinForwardRoute(c);
                    continue;
                }
                if(AdvanceNpcCombat(c,i,dt))continue;
                c.offset=Vector2.MoveTowards(c.offset,Vector2.zero,dt*1.2f);
                c.pushVelocity=Vector2.MoveTowards(c.pushVelocity,Vector2.zero,dt*15f);
                while(c.waypoint<c.route.Length-1&&Vector2.Distance(c.position,c.route[c.waypoint])<.35f)c.waypoint++;
                Vector2 to=c.route[c.waypoint]-c.position;
                c.heading=to.sqrMagnitude>.0001f?to.normalized:Vector2.up;
                if(StageTime<c.startAt)continue;
                Vector2 side=new Vector2(c.heading.y,-c.heading.x);
                Vector2 avoidance=Vector2.zero;float pace=1;
                for(int j=0;j<crowds.Count;j++)
                {
                    if(i==j||!crowds[j].active||crowds[j].airborne)continue;
                    Vector2 away=c.position-crowds[j].position;
                    float sq=away.sqrMagnitude;
                    if(sq>3.24f||sq<.00001f)continue;
                    float distance=Mathf.Sqrt(sq);
                    float ahead=-Vector2.Dot(away,c.heading);
                    float lateral=Vector2.Dot(away,side);
                    if(ahead>0&&Mathf.Abs(lateral)<.72f)
                    {
                        pace=Mathf.Min(pace,Mathf.Clamp((ahead-.55f)/1.0f,.32f,1));
                        float laneSide=Mathf.Abs(lateral)>.08f?Mathf.Sign(lateral):(i%2==0?1:-1);
                        avoidance+=side*laneSide*(1-distance/1.8f)*.85f;
                    }
                    if(distance<.95f)avoidance+=away/distance*(.95f-distance)*1.5f;
                }
                // Keep urgency and forward intent even inside a queue. Local body separation
                // below prevents overlap, and different speeds leave opportunities to pass.
                Vector2 desired=c.heading*(c.speed*pace)+Vector2.ClampMagnitude(avoidance,1.35f);
                float forward=Vector2.Dot(desired,c.heading);
                if(forward<c.speed*.2f)desired+=c.heading*(c.speed*.2f-forward);
                if(c.stuckTime>.45f)
                {
                    float choice=((i+(int)(c.stuckTime/1.1f))%2==0)?1:-1;
                    desired+=side*choice*1.8f;
                }
                Vector2 travel=desired*dt;
                if(to.magnitude<.5f)travel=Vector2.ClampMagnitude(travel,to.magnitude);
                c.position=MoveOrdinaryCrowd(c,travel);
                PushCrowd(c,c.pushVelocity*dt);
            }
            ResolveNpcRushHits();
            PropagateImpacts();
            SeparateCommittedCrowds();
            // Gentle symmetric separation uses real static collision for both bodies.
            // No elastic velocity is fed back, so dense groups cannot gain unlimited momentum.
            for(int i=0;i<crowds.Count;i++)
            {
                var a=crowds[i];if(!a.active||a.airborne||CombatCommitted(i))continue;
                for(int j=i+1;j<crowds.Count;j++)
                {
                    var b=crowds[j];if(!b.active||b.airborne||CombatCommitted(j))continue;
                    Vector2 delta=a.position-b.position;float sq=delta.sqrMagnitude;
                    if(sq>=.78f*.78f)continue;
                    float distance=Mathf.Sqrt(sq);
                    Vector2 normal=distance>.001f?delta/distance:(i%2==0?Vector2.right:Vector2.left);
                    Vector2 correction=normal*Mathf.Min(.055f,(.78f-distance)*.5f);
                    PushCrowd(a,correction);PushCrowd(b,-correction);
                }
            }
            for(int i=0;i<crowds.Count;i++)
            {
                var c=crowds[i];if(!c.active)continue;
                Vector2 delta=position-c.position;float distance=delta.magnitude;
                if(!c.airborne&&c.recoveryLeft<=0&&!PlayerAirborne&&!PlayerRecovering)
                {
                    bool newContact=distance<Radius+.43f&&!c.touchingPlayer;
                    if(distance<Radius+.43f)c.touchingPlayer=true;
                    else if(distance>Radius+.52f)c.touchingPlayer=false;
                    bool dashTouch=IsDashing&&SegmentDistance(c.position,playerTickStart,position)<Radius+.43f;
                    bool launched=false;
                    if(dashTouch)
                    {
                        Vector2 forward=velocity.sqrMagnitude>.1f?velocity.normalized:facing;
                        Vector2 outward=c.position-position;
                        if(outward.sqrMagnitude<.001f)outward=forward;else outward.Normalize();
                        Vector2 direction=(forward*.85f+outward*.65f).normalized;
                        launched=LaunchCrowd(i,direction,CurrentDashId,0);
                    }
                    if(!launched&&distance<Radius+.43f)
                    {
                        Vector2 normal=distance>.001f?delta/distance:Vector2.right;
                        float overlap=Radius+.43f-distance;
                        float crowdShare=IsDashing?.94f:IsBracing?.88f:.70f;
                        Vector2 before=c.position;if(!CombatCommitted(i))PushCrowd(c,-normal*overlap*crowdShare);
                        float displaced=Mathf.Max(0,Vector2.Dot(before-c.position,normal));
                        Vector2 playerBefore=position;
                        MovePlayer(normal*Mathf.Min(Mathf.Max(0,overlap-displaced),playerSeparationLeft));
                        playerSeparationLeft=Mathf.Max(0,playerSeparationLeft-Vector2.Distance(position,playerBefore));
                        c.pushVelocity=Vector2.ClampMagnitude(c.pushVelocity-normal*(IsBracing?.35f:.5f),2.2f);
                        float into=Vector2.Dot(velocity,normal);
                        if(into<0&&!IsDashing)velocity-=normal*into*(IsBracing?.06f:.12f);
                        bool fresh=RegisterHit(1000+i,IsDashing?.45f:.2f);
                        if(fresh&&newContact&&!IsDashing)
                        {
                            NormalContactCount++;c.hitAge=0;c.jellyStrength=.18f;
                            playerHitAge=0;playerJellyStrength=.14f;
                        }
                        // Ordinary contact is a light shove. Strong hits preserve the
                        // player's burst; neither path applies the old self-stumble.
                    }
                }
                c.lastVelocity=(c.position-c.previous)/dt;
                float signed=Vector2.Dot(c.position-c.previous,c.heading);
                if(!c.airborne&&c.recoveryLeft<=0&&StageTime>=c.startAt)c.stuckTime=signed<dt*.18f?c.stuckTime+dt:Mathf.Max(0,c.stuckTime-dt*2);
                c.progress+=signed;
                if(signed>=0)CrowdForwardDistance+=signed;else CrowdBackwardDistance-=signed;
                if(!c.crossedGoal&&Mathf.Abs(c.position.x-world.goal.x)<world.goalHalfSize.x+1f&&c.position.y>=world.goal.z-.5f)
                {c.crossedGoal=true;CrowdGoalCrossings++;}
                // A covered exit line accepts side-by-side arrivals. A tiny shared point
                // would let two separated bodies hold each other outside it indefinitely.
                Rect cover=world.crowdExitCover;
                bool covered=c.position.x>cover.xMin+.5f&&c.position.x<cover.xMax-.5f&&c.position.y>cover.yMin+.5f&&c.position.y<cover.yMax-.5f;
                if(!c.airborne&&c.recoveryLeft<=0&&c.waypoint==c.route.Length-1&&c.position.y>=world.crowdExit.y-.18f&&Mathf.Abs(c.position.x-world.crowdExit.x)<=1.6f&&covered)
                {
                    c.active=false;c.recycleAt=StageTime+.6f+(i%7)*.08f;
                    c.root.position=new Vector3(c.position.x,0,c.position.y);
                    c.root.gameObject.SetActive(false);CrowdArrivals++;continue;
                }
                c.root.position=new Vector3(c.position.x,0,c.position.y);
                if(!c.airborne&&c.recoveryLeft<=0&&CrowdInView(c.position,.18f))
                {
                    Vector2 cv=(c.position-c.previous)/dt;
                    if(cv.sqrMagnitude>.02f)c.visual.rotation=Quaternion.Slerp(c.visual.rotation,Quaternion.LookRotation(new Vector3(cv.x,0,cv.y)),dt*7);
                    RushVisuals.AnimatePerson(c.parts,cv,ImpactFxClock+i,false,false);
                }
                ApplyCrowdPresentation(c);
            }
        }
        static float SegmentDistance(Vector2 point,Vector2 a,Vector2 b)
        {
            Vector2 ab=b-a;float t=ab.sqrMagnitude>.00001f?Mathf.Clamp01(Vector2.Dot(point-a,ab)/ab.sqrMagnitude):0;
            return Vector2.Distance(point,a+ab*t);
        }
        void ResetCrowdImpact(Crowd c)
        {
            c.windup01=0;
            c.airborne=false;c.height=0;c.verticalVelocity=0;c.flightVelocity=Vector2.zero;
            c.recoveryLeft=0;c.airTime=0;c.hitAge=-1;c.landingAge=-1;c.jellyStrength=0;
            c.lastVelocity=Vector2.zero;c.spinDegrees=0;c.landingSpin=0;c.touchingPlayer=false;
            c.visual.localPosition=Vector3.zero;c.visual.localScale=Vector3.one;
            if(c.heading.sqrMagnitude>.01f)c.visual.rotation=Quaternion.LookRotation(new Vector3(c.heading.x,0,c.heading.y));
        }
        void ClearImpactTransients()
        {
            impactFreezeLeft=0;shake=0;impact=0;playerHitAge=-1;playerJellyStrength=0;
            ClearCombatTransients();
            if(impactFx!=null)impactFx.Clear();
            if(trail)trail.enabled=false;
            if(visual)visual.localScale=Vector3.one;
            foreach(var c in crowds){c.hitAge=-1;c.landingAge=-1;c.jellyStrength=0;ApplyCrowdPresentation(c);}
            if(cameraRig)cameraRig.transform.position=cameraFocus+cameraHome;
        }
        void ClearImpactScene()
        {
            ClearImpactTransients();nextImpactStopAt=0;ResetCombatScene();
            foreach(var c in crowds)ResetCrowdImpact(c);
        }
        void TickImpactPresentation(float dt)
        {
            if(dt<=0)return;
            if(impactFx!=null)impactFx.Tick(dt);
            if(playerHitAge>=0)playerHitAge+=dt;
            if(playerLandingAge>=0)playerLandingAge+=dt;
            foreach(var c in crowds)
            {
                if(!c.active)continue;
                if(c.hitAge>=0)c.hitAge+=dt;
                if(c.landingAge>=0)c.landingAge+=dt;
                ApplyCrowdPresentation(c);
            }
        }
        void ApplyCrowdPresentation(Crowd c)
        {
            if(!c.visual)return;
            // Telegraph pose is applied last, outside the walking rig. Navigation roots
            // and collision radii remain unchanged throughout the visible wind-up.
            if(!c.airborne&&c.recoveryLeft<=0&&c.windup01>0)
            {
                float charge=Mathf.Clamp01(c.windup01);
                Vector2 aim=c.windupDirection.sqrMagnitude>.01f?c.windupDirection:Vector2.up;
                float wobble=Mathf.Sin(charge*24f)*3.5f*charge;
                c.visual.rotation=Quaternion.LookRotation(new Vector3(aim.x,0,aim.y))*Quaternion.Euler(10f+charge*10f,0,wobble);
                c.visual.localScale=new Vector3(1f+charge*.12f,1f-charge*.22f,1f+charge*.10f);
                c.visual.localPosition=Vector3.zero;
                return;
            }
            Vector3 scale=RushVisuals.ImpactScale(c.hitAge,c.landingAge,c.jellyStrength);
            c.visual.localScale=scale;
            if(c.airborne||c.recoveryLeft>0)
            {
                float angle=c.airborne?c.spinDegrees:Mathf.Lerp(c.landingSpin,0,Mathf.SmoothStep(0,1,1-c.recoveryLeft/.55f));
                Vector2 direction=c.launchDirection.sqrMagnitude>.01f?c.launchDirection:Vector2.up;
                Quaternion yaw=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y));
                float wobble=c.airborne?Mathf.Sin(c.airTime*16)*10:c.landingAge>=0?Mathf.Sin((.55f-c.recoveryLeft)*25)*c.recoveryLeft*24:0;
                c.visual.rotation=yaw*Quaternion.Euler(-angle,0,wobble);
                float pivot=.85f*scale.y;
                float settle=c.airborne?0:.35f*Mathf.Abs(Mathf.Sin(angle*Mathf.Deg2Rad));
                float bounce=c.landingAge>=0?Mathf.Abs(Mathf.Sin(c.landingAge*20))*Mathf.Exp(-c.landingAge*8)*.18f:0;
                c.visual.localPosition=Vector3.up*(c.height+pivot-settle+bounce)-c.visual.localRotation*(Vector3.up*pivot);
            }
            else c.visual.localPosition=Vector3.zero;
        }
        bool LaunchCrowd(int index,Vector2 direction,int dashId,int depth)
        {
            var c=crowds[index];
            if(!c.active||c.airborne||c.recoveryLeft>0||depth>2||!impactBursts.TryGetValue(dashId,out var burst))return false;
            if(burst.owner==index||burst.targets.Count>=6||!burst.targets.Add(index))return false;
            CancelNpcCombat(index,3f);
            direction=direction.sqrMagnitude>.01f?direction.normalized:Vector2.up;
            c.airborne=true;c.airTime=0;c.height=0;c.verticalVelocity=8.2f*Mathf.Pow(.88f,depth);
            c.flightVelocity=direction*(13.8f*Mathf.Pow(.80f,depth));c.launchDirection=direction;
            c.hitDashId=dashId;c.chainDepth=depth;c.launchCount++;c.spinDegrees=0;c.recoveryLeft=0;
            c.hitAge=0;c.landingAge=-1;c.jellyStrength=depth==0?1f:.82f;c.touchingPlayer=false;
            c.pushVelocity=Vector2.zero;c.offset=Vector2.zero;
            TotalLaunches++;if(depth>0)ChainLaunches++;
            MaxObservedChainDepth=Mathf.Max(MaxObservedChainDepth,depth);
            MaxObservedDashTargets=Mathf.Max(MaxObservedDashTargets,burst.targets.Count);
            if(dashId<0)NpcToNpcLaunches++;
            if(depth==0&&dashId>=0)
            {
                RegisterHit(1000+index,.85f);playerHitAge=0;playerJellyStrength=.46f;
                // Commit the compressed model BEFORE starting hit-stop.
                if(visual)visual.localScale=RushVisuals.ImpactScale(0,-1,playerJellyStrength);
            }
            ApplyCrowdPresentation(c);
            if(impactFx!=null)impactFx.Emit(new Vector3(c.position.x,.12f,c.position.y),direction,depth==0);
            if(!burst.stopped)
            {
                burst.stopped=true;
                if(StageTime>=nextImpactStopAt){impactFreezeLeft=.06f;nextImpactStopAt=StageTime+.18f;}
                shake=Mathf.Max(shake,.9f);Sound(launchClip,.8f);
            }
            return true;
        }
        void SimulateFlight(Crowd c,float dt)
        {
            c.airTime+=dt;
            Vector2 desired=c.flightVelocity*dt;
            Vector2 before=c.position;c.position=MoveCrowd(c.position,desired);
            Vector2 blocked=desired-(c.position-before);
            if(blocked.sqrMagnitude>.00001f)
            {
                Vector2 normal=blocked.normalized;float into=Vector2.Dot(c.flightVelocity,normal);
                if(into>0)c.flightVelocity-=normal*into*1.15f;
            }
            c.flightVelocity=Vector2.ClampMagnitude(Vector2.MoveTowards(c.flightVelocity,Vector2.zero,dt*4.4f),13.8f);
            c.height+=c.verticalVelocity*dt;c.verticalVelocity-=19.5f*dt;
            c.spinDegrees+=420f*dt;
            if(c.height<=0&&c.verticalVelocity<0 || c.airTime>1.1f)
            {
                c.height=0;c.verticalVelocity=0;c.flightVelocity=Vector2.zero;c.airborne=false;
                c.position=Resolve(c.position,.40f,false);c.recoveryLeft=.55f;
                c.landingSpin=Mathf.DeltaAngle(0,c.spinDegrees);c.landingAge=0;c.landedAt=StageTime;c.landCount++;
                RejoinForwardRoute(c);
                if(impactFx!=null)impactFx.Emit(new Vector3(c.position.x,.1f,c.position.y),c.launchDirection,false);
            }
        }
        void PropagateImpacts()
        {
            for(int i=0;i<crowds.Count;i++)
            {
                var source=crowds[i];
                if(!source.active||!source.airborne||source.airTime<.025f||source.chainDepth>=2||source.flightVelocity.sqrMagnitude<9f)continue;
                if(!impactBursts.TryGetValue(source.hitDashId,out var burst)||burst.targets.Count>=6)continue;
                for(int j=0;j<crowds.Count&&burst.targets.Count<6;j++)
                {
                    var target=crowds[j];
                    if(i==j||!target.active||target.airborne||target.recoveryLeft>0||burst.targets.Contains(j))continue;
                    if(SegmentDistance(target.position,source.previous,source.position)>.83f)continue;
                    Vector2 forward=source.flightVelocity.normalized;
                    Vector2 outward=target.position-source.position;
                    if(outward.sqrMagnitude>.001f)outward.Normalize();else outward=forward;
                    if(LaunchCrowd(j,(forward*.85f+outward*.35f).normalized,source.hitDashId,source.chainDepth+1))
                        source.flightVelocity*=.88f;
                }
            }
        }
        void RejoinForwardRoute(Crowd c)
        {
            float best=float.MaxValue;int next=c.waypoint;
            for(int i=Mathf.Max(1,c.waypoint);i<c.route.Length;i++)
            {
                Vector2 a=c.route[i-1],ab=c.route[i]-a;
                float t=ab.sqrMagnitude>.001f?Mathf.Clamp01(Vector2.Dot(c.position-a,ab)/ab.sqrMagnitude):1;
                float distance=(c.position-a-ab*t).sqrMagnitude;
                if(distance<best){best=distance;next=t>.96f&&i<c.route.Length-1?i+1:i;}
            }
            c.waypoint=Mathf.Max(c.waypoint,next);c.stuckTime=0;c.offset=Vector2.zero;c.pushVelocity=Vector2.zero;
            c.heading=(c.route[c.waypoint]-c.position).normalized;
        }
        // Explicit fixture initialization is unavailable in ordinary builds/runs. It does
        // not apply impacts: the dedicated harness still has to drive movement and Space.
        public bool SetupImpactFixture(string kind)
        {
            if(!impactQaMode||(kind!="front"&&kind!="side"&&kind!="chain"&&kind!="boundary"&&kind!="wall-chain"))return false;
            ResetRun();State=RushState.Playing;QAInput=true;QAMove=Vector2.zero;QABrace=false;QADash=false;actionLocked=false;
            position=kind=="wall-chain"?new Vector2(14.2f,6):kind=="boundary"?new Vector2(17.8f,6):new Vector2(0,4);
            facing=kind=="boundary"||kind=="wall-chain"?Vector2.right:Vector2.up;velocity=Vector2.zero;
            player.position=new Vector3(position.x,0,position.y);visual.rotation=Quaternion.LookRotation(new Vector3(facing.x,0,facing.y));
            Vector2[] points=kind=="chain"?new[]{new Vector2(0,6),new Vector2(0,7.4f),new Vector2(.60f,8.05f),new Vector2(-.60f,8.05f),new Vector2(0,9.0f),new Vector2(.70f,9.7f),new Vector2(-.70f,9.7f),new Vector2(0,10.7f),new Vector2(0,12.0f)}
                :kind=="wall-chain"?new[]{new Vector2(15.8f,6),new Vector2(17,6),new Vector2(18.2f,6),new Vector2(19.4f,6)}
                :new[]{kind=="boundary"?new Vector2(19,6):new Vector2(kind=="side"?.58f:0,6)};
            for(int i=0;i<crowds.Count;i++)
            {
                var c=crowds[i];ResetCrowdImpact(c);c.fixtureDisabled=true;c.active=i<points.Length;c.root.gameObject.SetActive(c.active);
                if(!c.active)continue;
                c.position=Resolve(points[i],.40f,false);c.previous=c.position;c.origin=c.position;
                c.route=new[]{c.position,new Vector2(c.position.x,22),new Vector2(0,24),world.crowdExit};
                c.waypoint=1;c.originWaypoint=1;c.speed=3.1f;c.startAt=.85f;c.generation=0;c.progress=0;
                c.hitDashId=-1;c.launchCount=0;c.landCount=0;c.heading=Vector2.up;
                c.root.position=new Vector3(c.position.x,0,c.position.y);c.visual.rotation=Quaternion.identity;
            }
            ResetCamera();ImpactFixtureKind=kind;ImpactFixtureReady=true;return true;
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
        bool RegisterHit(int id,float strength)
        {
            if(collisionTimes.TryGetValue(id,out float time)&&StageTime-time<.65f)return false;
            collisionTimes[id]=StageTime;Collisions++;impact=Mathf.Max(impact,strength);shake=Mathf.Max(shake,strength);
            if(collisionSoundCooldown<=0){Sound(bumpClip,.45f+strength*.4f);collisionSoundCooldown=.1f;}
            return true;
        }
        void CompleteStage()
        {
            if(State!=RushState.Playing)return;
            ClearImpactScene();
            SplitTimes[Stage]=StageTime;SplitCollisions[Stage]=Collisions-stageStartCollisions;stageStartCollisions=Collisions;
            velocity=Vector2.zero;dashLeft=0;stumbleLeft=0;stumbleProtectionLeft=0;continuousStumbleTime=0;IsDashing=false;IsBracing=false;LockActions();TransitionCount++;
            Sound(goalClip);Log("stage_complete");
            if(Stage==2){State=RushState.Results;Log(TotalTime<=TimeLimit?"result_on_time":"result_late");}
            else{State=RushState.Transition;transitionBeginTime=Time.unscaledTime;TransitionLeft=1.55f;}
        }
        void OnApplicationFocus(bool focus){if(!focus&&qa==null&&!impactQaMode&&!combatQaMode&&State==RushState.Playing)TogglePause();}
        void OnGUI(){RushUI.Draw(this);}
        public void Log(string evt){Debug.Log("RUSH_EVENT "+evt+" stage="+Stage+" time="+TotalTime.ToString("F3")+" pos="+position+" collisions="+Collisions+" transitions="+TransitionCount);}
    }
}
