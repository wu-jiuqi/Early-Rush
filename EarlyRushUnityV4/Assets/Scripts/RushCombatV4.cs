using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush
{
    public partial class RushGame
    {
        const float WarningDuration=.90f, NpcRushDuration=.32f, NpcRushSpeed=11.5f, DangerRadius=.82f;
        sealed class NpcCombat
        {
            public int phase, attackId, warningCreatedFrame;
            public float warningLeft, rushLeft, cooldownLeft, length, travelled;
            public Vector2 origin, direction;
            public bool hitPlayer, pendingFinish;
            public Transform warning;
            public LineRenderer outline;
            public Mesh fillMesh;
        }
        readonly List<NpcCombat> combat=new List<NpcCombat>();
        readonly Queue<int> npcBurstHistory=new Queue<int>();
        public int NpcBurstCount=>npcBurstHistory.Count;
        bool combatQaMode, combatFixture;
        string combatFixtureKind="";
        int nextNpcAttack=-1000;
        float combatCadence=2.4f;
        Material dangerFill, dangerLine;
        Transform braceFeedback;
        float playerHeight, playerVerticalSpeed, playerAirTime, playerRecoveryLeft, playerProtectionLeft, braceSuccessLeft;
        float playerSpin, playerLandingSpin, playerLandingAge=-1;
        Vector2 playerFlight, playerLaunchDirection;
        int playerLastAttack, playerLaunchCount, playerLandCount, protectedHitCount;
        bool playerAirborne;
        public bool PlayerAirborne=>playerAirborne;
        public bool PlayerRecovering=>playerRecoveryLeft>0;
        public float PlayerProtectionRemaining=>playerProtectionLeft;
        public float BraceSuccessRemaining=>braceSuccessLeft;
        public int NpcToNpcLaunches {get;private set;}
        public int NpcToPlayerLaunches {get;private set;}
        public int BraceBlocks {get;private set;}
        public int MaxVisibleCombatThreats {get;private set;}
        public bool CombatFixtureReady=>combatFixture;
        public string CombatFixtureKind=>combatFixtureKind;
        public int CombatWarningCount {get {int n=0;foreach(var c in combat)if(c.phase==1)n++;return n;}}
        public int CombatRushCount {get {int n=0;foreach(var c in combat)if(c.phase==2)n++;return n;}}
        public struct CombatProbe
        {
            public Vector2 position,direction,warningOrigin;
            public int phase,attackId,warningCreatedFrame;
            public float warningLeft,rushLeft,cooldownLeft,warningLength,warningRadius,warningDuration;
            public bool active,visible;
        }
        public struct PlayerCombatProbe
        {
            public Vector2 position,velocity,launchDirection;
            public bool airborne,recovering;
            public float height,recoveryLeft,protectionLeft,braceSuccessLeft,spinDegrees;
            public int launchCount,landCount,braceSuccessCount,lastAttackId,protectedHitCount;
            public Vector3 visualScale,rootScale;
        }
        public CombatProbe[] CombatProbeSnapshot()
        {
            var a=new CombatProbe[combat.Count];
            for(int i=0;i<a.Length;i++){var c=combat[i];a[i]=new CombatProbe{position=crowds[i].position,direction=c.direction,warningOrigin=c.origin,
                phase=c.phase,attackId=c.attackId,warningCreatedFrame=c.warningCreatedFrame,warningLeft=c.warningLeft,rushLeft=c.rushLeft,cooldownLeft=c.cooldownLeft,
                warningLength=c.length,warningRadius=DangerRadius,warningDuration=WarningDuration,
                active=crowds[i].active,visible=CrowdInView(crowds[i].position,0)};}
            return a;
        }
        public PlayerCombatProbe PlayerCombatProbeSnapshot()=>new PlayerCombatProbe{position=position,velocity=PlayerAirborne?playerFlight:velocity,
            launchDirection=playerLaunchDirection,airborne=PlayerAirborne,recovering=PlayerRecovering,height=playerHeight,recoveryLeft=playerRecoveryLeft,
            protectionLeft=playerProtectionLeft,braceSuccessLeft=braceSuccessLeft,spinDegrees=playerSpin,launchCount=playerLaunchCount,
            landCount=playerLandCount,braceSuccessCount=BraceBlocks,lastAttackId=playerLastAttack,protectedHitCount=protectedHitCount,
            visualScale=PlayerVisualScale,rootScale=PlayerRootScale};

        void OnDestroy()
        {
            foreach(var a in combat)DestroyWarning(a);
            if(dangerFill)Destroy(dangerFill);if(dangerLine)Destroy(dangerLine);
            if(locatorMaterial)Destroy(locatorMaterial);if(trailMaterial)Destroy(trailMaterial);
        }
        bool WarningOriginVisible(Vector2 p)
        {
            Vector3 v=cameraRig.WorldToViewportPoint(new Vector3(p.x,.3f,p.y));
            return v.z>0&&v.x>.04f&&v.x<.96f&&v.y>.04f&&v.y<.96f;
        }
        void ResetCombatRunStats()
        {
            NpcToNpcLaunches=0;NpcToPlayerLaunches=0;BraceBlocks=0;MaxVisibleCombatThreats=0;
            playerLaunchCount=0;playerLandCount=0;protectedHitCount=0;nextNpcAttack=-1000;
        }
        void InitializeCombat()
        {
            combat.Clear();for(int i=0;i<crowds.Count;i++)combat.Add(new NpcCombat{cooldownLeft=1.5f+(i%8)*.3f});
            combatCadence=2.4f;combatFixture=false;combatFixtureKind="";
            braceFeedback=RushVisuals.Ring(player,"Brace success ring",new Vector3(0,.19f,0),.8f,.035f,RushVisuals.Teal);
            braceFeedback.gameObject.SetActive(false);
        }
        void ResetCombatScene()
        {
            while(npcBurstHistory.Count>0)impactBursts.Remove(npcBurstHistory.Dequeue());
            playerAirborne=false;playerHeight=0;playerVerticalSpeed=0;playerFlight=Vector2.zero;
            playerRecoveryLeft=0;playerProtectionLeft=0;playerAirTime=0;playerSpin=0;playerLandingAge=-1;
            playerLastAttack=0;combatFixture=false;combatFixtureKind="";
            if(visual){visual.localPosition=Vector3.zero;visual.localScale=Vector3.one;}
        }
        void ClearCombatTransients()
        {
            for(int i=0;i<combat.Count;i++)CancelNpcCombat(i,2f);
            combatCadence=1.6f;braceSuccessLeft=0;playerLandingAge=-1;
            if(braceFeedback)braceFeedback.gameObject.SetActive(false);
            if(visual){visual.localScale=Vector3.one;ApplyPlayerCombatPresentation();}
        }
        bool CombatCommitted(int i)=>i<combat.Count&&(combat[i].phase==1||combat[i].phase==2);
        void DestroyWarning(NpcCombat c)
        {
            if(c.warning)Destroy(c.warning.gameObject);if(c.fillMesh)Destroy(c.fillMesh);
            c.warning=null;c.outline=null;c.fillMesh=null;
        }
        void CancelNpcCombat(int i,float cooldown)
        {
            if(i>=combat.Count)return;var c=combat[i];
            if(i<crowds.Count)
            {
                var actor=crowds[i];actor.windup01=0;
                if(actor.visual&&!actor.airborne&&actor.recoveryLeft<=0)
                {
                    Vector2 aim=actor.heading.sqrMagnitude>.01f?actor.heading:Vector2.up;
                    actor.visual.rotation=Quaternion.LookRotation(new Vector3(aim.x,0,aim.y));
                }
            }
            if(c.phase==1||c.phase==2){c.phase=3;c.cooldownLeft=cooldown;c.warningLeft=0;c.rushLeft=0;c.pendingFinish=false;DestroyWarning(c);}
        }
        void TickCombat(float dt)
        {
            playerProtectionLeft=Mathf.Max(0,playerProtectionLeft-dt);braceSuccessLeft=Mathf.Max(0,braceSuccessLeft-dt);
            if(impactQaMode||combat.Count!=crowds.Count)return;
            if(combatFixture)
            {
                for(int i=0;i<combat.Count;i++)
                {
                    bool scheduled=i==0||(combatFixtureKind=="protection"&&i==1);
                    float start=i==0?.25f:1.75f;
                    if(scheduled&&combat[i].attackId==0&&StageTime>=start)
                        StartNpcWarning(i,combatFixtureKind=="boundary"?Vector2.right:Vector2.up);
                }
            }
            else
            {
                combatCadence-=dt;
                if(combatCadence<=0&&CombatWarningCount+CombatRushCount<2)
                {
                    for(int k=0;k<crowds.Count;k++)
                    {
                        int i=(k+(int)(StageTime*7))%crowds.Count;var c=crowds[i];var a=combat[i];
                        if(!c.active||c.airborne||c.recoveryLeft>0||a.phase!=0||a.cooldownLeft>0||!WarningOriginVisible(c.position))continue;
                        float distance=Vector2.Distance(c.position,position);
                        if(distance<2.5f||distance>9f||c.position.y>90)continue;
                        Vector2 direction=(c.route[c.waypoint]-c.position).normalized;
                        if(direction.sqrMagnitude<.5f||Vector2.Distance(c.route[c.waypoint],c.position)<4.1f)continue;
                        bool busy=SegmentDistance(position,c.position,c.position+direction*4)<1.4f;
                        for(int j=0;j<crowds.Count&&!busy;j++)
                            if(j!=i&&crowds[j].active&&Vector2.Distance(crowds[j].position,c.position)>.9f&&SegmentDistance(crowds[j].position,c.position,c.position+direction*4)<1.0f)busy=true;
                        if(busy&&StartNpcWarning(i,direction)){combatCadence=1.5f;break;}
                    }
                    if(combatCadence<=0)combatCadence=.25f;
                }
            }
            int visible=0;
            foreach(var a in combat)if(a.phase==1||a.phase==2)
                if(CrowdInView(a.origin,0)||CrowdInView(a.origin+a.direction*a.length,.05f))visible++;
            MaxVisibleCombatThreats=Mathf.Max(MaxVisibleCombatThreats,visible);
        }
        bool StartNpcWarning(int i,Vector2 direction)
        {
            if(i>=combat.Count||CombatWarningCount+CombatRushCount>=2)return false;
            var c=crowds[i];if(!c.active||c.airborne||c.recoveryLeft>0)return false;
            var a=combat[i];a.origin=c.position;a.direction=direction.normalized;
            float length=0;
            // Truncate at the first solid obstruction. The rush never slides out of its warning.
            for(float d=.1f;d<=NpcRushSpeed*NpcRushDuration+.001f;d+=.1f)
            {
                Vector2 desired=a.origin+a.direction*d;
                if(Vector2.Distance(Resolve(desired,.4f,false),desired)>.002f)break;
                length=d;
            }
            if(length<.7f)return false;
            a.length=length;a.phase=1;a.warningLeft=WarningDuration;a.rushLeft=0;a.travelled=0;a.hitPlayer=false;a.pendingFinish=false;
            a.warningCreatedFrame=Time.frameCount;
            a.attackId=--nextNpcAttack;c.pushVelocity=Vector2.zero;c.offset=Vector2.zero;
            c.windup01=.01f;c.windupDirection=a.direction;
            if(c.visual)c.visual.rotation=Quaternion.LookRotation(new Vector3(a.direction.x,0,a.direction.y));
            CreateWarning(a);return true;
        }
        void CreateWarning(NpcCombat a)
        {
            DestroyWarning(a);
            if(!dangerFill){dangerFill=new Material(Shader.Find("Sprites/Default"));dangerFill.color=new Color(1f,.25f,.05f,.20f);}
            if(!dangerLine){dangerLine=new Material(Shader.Find("Sprites/Default"));}
            a.warning=new GameObject("Locked commuter rush warning").transform;a.warning.SetParent(world.root,false);
            a.warning.position=new Vector3(a.origin.x,.19f,a.origin.y);
            a.warning.rotation=Quaternion.LookRotation(new Vector3(a.direction.x,0,a.direction.y));
            var pts=new List<Vector3>();
            for(int k=0;k<=16;k++){float angle=k*Mathf.PI/16;pts.Add(new Vector3(Mathf.Cos(angle)*DangerRadius,0,a.length+Mathf.Sin(angle)*DangerRadius));}
            for(int k=0;k<=16;k++){float angle=Mathf.PI+k*Mathf.PI/16;pts.Add(new Vector3(Mathf.Cos(angle)*DangerRadius,0,Mathf.Sin(angle)*DangerRadius));}
            a.outline=a.warning.gameObject.AddComponent<LineRenderer>();a.outline.useWorldSpace=false;a.outline.loop=true;a.outline.positionCount=pts.Count;
            a.outline.SetPositions(pts.ToArray());a.outline.sharedMaterial=dangerLine;a.outline.startColor=a.outline.endColor=new Color(1,.28f,.04f);
            a.outline.startWidth=a.outline.endWidth=.065f;
            var vertices=new List<Vector3>{new Vector3(0,0,a.length*.5f)};vertices.AddRange(pts);var tris=new List<int>();
            for(int k=0;k<pts.Count;k++){tris.Add(0);tris.Add(k+1);tris.Add((k+1)%pts.Count+1);}
            a.fillMesh=new Mesh();a.fillMesh.SetVertices(vertices);a.fillMesh.SetTriangles(tris,0);a.fillMesh.RecalculateBounds();
            a.warning.gameObject.AddComponent<MeshFilter>().sharedMesh=a.fillMesh;a.warning.gameObject.AddComponent<MeshRenderer>().sharedMaterial=dangerFill;
            // Repeated arrows stay inside the exact danger capsule. A body standing
            // over its endpoint cannot hide all directional cues along both sides.
            for(float z=.45f;z<a.length-.1f;z+=.85f)
                for(int side=-1;side<=1;side+=2)
                {
                    float x=side*.52f;
                    var arrow=new GameObject("Rush direction chevron").AddComponent<LineRenderer>();
                    arrow.transform.SetParent(a.warning,false);arrow.useWorldSpace=false;arrow.positionCount=3;
                    arrow.SetPositions(new[]{new Vector3(x-.14f,.025f,z-.18f),new Vector3(x,.025f,z+.05f),new Vector3(x+.14f,.025f,z-.18f)});
                    arrow.sharedMaterial=dangerLine;arrow.startColor=arrow.endColor=RushVisuals.Cream;arrow.startWidth=arrow.endWidth=.075f;
                }
        }
        bool AdvanceNpcCombat(Crowd c,int i,float dt)
        {
            if(i>=combat.Count||impactQaMode)return false;
            var a=combat[i];a.cooldownLeft=Mathf.Max(0,a.cooldownLeft-dt);
            if(a.phase==3&&a.cooldownLeft<=0)a.phase=0;
            if(a.phase==1)
            {
                if(!WarningOriginVisible(a.origin)){CancelNpcCombat(i,2f);return true;}
                // A catch-up frame may contain several physics ticks before rendering.
                // Present the complete warning once before consuming any reaction time.
                if(Time.frameCount==a.warningCreatedFrame)return true;
                a.warningLeft=Mathf.Max(0,a.warningLeft-dt);
                c.windup01=Mathf.Max(.01f,1f-a.warningLeft/WarningDuration);
                if(a.outline)a.outline.startWidth=a.outline.endWidth=.06f+.025f*Mathf.Sin((1-a.warningLeft/WarningDuration)*Mathf.PI);
                if(a.warningLeft<=0)
                {
                    a.phase=2;a.rushLeft=NpcRushDuration;impactBursts[a.attackId]=new ImpactBurst{owner=i};
                    c.windup01=0;
                    if(c.visual)c.visual.rotation=Quaternion.LookRotation(new Vector3(a.direction.x,0,a.direction.y));
                    npcBurstHistory.Enqueue(a.attackId);
                    while(npcBurstHistory.Count>8)impactBursts.Remove(npcBurstHistory.Dequeue());
                    if(a.outline)a.outline.startColor=a.outline.endColor=new Color(1,.08f,.03f);
                }
                return true;
            }
            if(a.phase!=2)return false;
            float step=Mathf.Min(a.length-a.travelled,NpcRushSpeed*dt);
            Vector2 desired=c.position+a.direction*step;
            Vector2 resolved=MoveCrowd(c.position,a.direction*step);
            if(Vector2.Distance(resolved,desired)>.002f)a.pendingFinish=true;
            else {c.position=resolved;a.travelled+=step;}
            a.rushLeft=Mathf.Max(0,a.rushLeft-dt);
            if(a.rushLeft<=0||a.travelled>=a.length-.001f)a.pendingFinish=true;
            return true;
        }
        void ResolveNpcRushHits()
        {
            for(int i=0;i<combat.Count;i++)
            {
                var a=combat[i];var source=crowds[i];if(a.phase!=2)continue;
                for(int j=0;j<crowds.Count;j++)
                    if(j!=i&&crowds[j].active&&SegmentDistance(crowds[j].position,source.previous,source.position)<=.8f)
                        LaunchCrowd(j,a.direction,a.attackId,0);
                // Only a warned, self-powered rush can launch the player, never a flying chain body.
                if(!a.hitPlayer&&SegmentDistance(Vector2.zero,source.previous-playerTickStart,source.position-position)<=DangerRadius)
                {
                    a.hitPlayer=true;
                    if(PlayerAirborne||PlayerRecovering||playerProtectionLeft>0)protectedHitCount++;
                    else if(IsBracing)
                    {
                        BraceBlocks++;braceSuccessLeft=.8f;playerProtectionLeft=Mathf.Max(playerProtectionLeft,.25f);
                        MovePlayer(a.direction*.07f);playerHitAge=0;playerJellyStrength=.18f;
                        Sound(tickClip,.8f);ApplyPlayerCombatPresentation();a.pendingFinish=true;
                    }
                    else LaunchPlayer(a.direction,a.attackId);
                }
                if(a.pendingFinish){CancelNpcCombat(i,5.5f+(i%3)*.4f);source.startAt=0;RejoinForwardRoute(source);}
            }
        }
        void LaunchPlayer(Vector2 direction,int attackId)
        {
            playerAirborne=true;playerHeight=0;playerAirTime=0;playerVerticalSpeed=7.2f;playerFlight=direction*9.4f;
            playerLaunchDirection=direction;playerLastAttack=attackId;playerSpin=0;playerRecoveryLeft=0;playerLandingAge=-1;
            playerHitAge=0;playerJellyStrength=1;playerLaunchCount++;NpcToPlayerLaunches++;
            dashLeft=0;IsDashing=false;IsBracing=false;velocity=playerFlight;
            if(StageTime>=nextImpactStopAt){impactFreezeLeft=.06f;nextImpactStopAt=StageTime+.18f;}
            shake=Mathf.Max(shake,.9f);Sound(launchClip,.8f);
            if(impactFx!=null)impactFx.Emit(new Vector3(position.x,.12f,position.y),direction,true);
            ApplyPlayerCombatPresentation();
        }
        void SimulatePlayerImpact(float dt)
        {
            if(!playerAirborne){playerRecoveryLeft=Mathf.Max(0,playerRecoveryLeft-dt);velocity=Vector2.zero;return;}
            playerAirTime+=dt;Vector2 desired=playerFlight*dt,before=position;
            int steps=Mathf.Max(1,Mathf.CeilToInt(desired.magnitude/.14f));
            for(int i=0;i<steps;i++)position=Resolve(position+desired/steps,Radius,false);
            Vector2 blocked=desired-(position-before);
            if(blocked.sqrMagnitude>.00001f){Vector2 normal=blocked.normalized;float into=Vector2.Dot(playerFlight,normal);if(into>0)playerFlight-=normal*into*1.15f;}
            playerFlight=Vector2.ClampMagnitude(Vector2.MoveTowards(playerFlight,Vector2.zero,dt*4.4f),9.4f);
            velocity=playerFlight;playerHeight+=playerVerticalSpeed*dt;playerVerticalSpeed-=19.5f*dt;playerSpin+=420*dt;
            if((playerHeight<=0&&playerVerticalSpeed<0)||playerAirTime>1.05f)
            {
                playerHeight=0;playerAirborne=false;playerFlight=Vector2.zero;velocity=Vector2.zero;
                playerRecoveryLeft=.40f;playerProtectionLeft=1.2f;playerLandingAge=0;playerLandingSpin=Mathf.DeltaAngle(0,playerSpin);playerLandCount++;
                if(impactFx!=null)impactFx.Emit(new Vector3(position.x,.1f,position.y),playerLaunchDirection,false);
            }
        }
        void ApplyPlayerCombatPresentation()
        {
            if(!visual)return;
            Vector3 scale=RushVisuals.ImpactScale(playerHitAge,playerLandingAge,playerJellyStrength);visual.localScale=scale;
            if(playerAirborne||PlayerRecovering)
            {
                float angle=playerAirborne?playerSpin:Mathf.Lerp(playerLandingSpin,0,Mathf.SmoothStep(0,1,1-playerRecoveryLeft/.4f));
                Vector2 d=playerLaunchDirection.sqrMagnitude>.01f?playerLaunchDirection:Vector2.up;
                visual.rotation=Quaternion.LookRotation(new Vector3(d.x,0,d.y))*Quaternion.Euler(-angle,0,0);
                float pivot=.85f*scale.y,settle=playerAirborne?0:.35f*Mathf.Abs(Mathf.Sin(angle*Mathf.Deg2Rad));
                float bounce=playerLandingAge>=0?Mathf.Abs(Mathf.Sin(playerLandingAge*20))*Mathf.Exp(-playerLandingAge*8)*.18f:0;
                visual.localPosition=Vector3.up*(playerHeight+pivot-settle+bounce)-visual.localRotation*(Vector3.up*pivot);
            }
            else visual.localPosition=Vector3.zero;
            if(braceFeedback){braceFeedback.gameObject.SetActive(braceSuccessLeft>0);float r=.8f+(1-braceSuccessLeft/.8f)*.45f;braceFeedback.localScale=new Vector3(r,.25f,r);}
        }
        public bool SetupCombatFixture(string kind)
        {
            if(!combatQaMode||(kind!="front"&&kind!="brace"&&kind!="dodge"&&kind!="npc-chain"&&kind!="boundary"&&kind!="protection"&&kind!="near-outside"&&kind!="near-inside"))return false;
            ResetRun();State=RushState.Playing;QAInput=true;QAMove=Vector2.zero;QABrace=false;QADash=false;actionLocked=false;
            combatFixture=true;combatFixtureKind=kind;
            position=kind=="boundary"?new Vector2(19,14):kind=="npc-chain"?new Vector2(8,8):new Vector2(kind=="near-outside"?.92f:kind=="near-inside"?.72f:0,8);
            facing=Vector2.up;velocity=Vector2.zero;player.position=new Vector3(position.x,0,position.y);
            Vector2[] points=kind=="npc-chain"?new[]{new Vector2(0,5.2f),new Vector2(0,7.1f),new Vector2(.6f,8),new Vector2(-.6f,8),new Vector2(0,9.2f),new Vector2(.7f,10),new Vector2(-.7f,10),new Vector2(0,11.2f)}
                :kind=="protection"?new[]{new Vector2(0,5.2f),new Vector2(0,12)}
                :new[]{kind=="boundary"?new Vector2(16.2f,14):new Vector2(0,5.2f)};
            for(int i=0;i<crowds.Count;i++)
            {
                var c=crowds[i];ResetCrowdImpact(c);c.fixtureDisabled=true;c.active=i<points.Length;c.root.gameObject.SetActive(c.active);
                if(!c.active)continue;
                c.position=Resolve(points[i],.4f,false);c.previous=c.position;c.origin=c.position;c.route=new[]{c.position,new Vector2(c.position.x,24),world.crowdExit};
                c.waypoint=1;c.originWaypoint=1;c.speed=3.1f;c.startAt=10;c.heading=Vector2.up;c.root.position=new Vector3(c.position.x,0,c.position.y);
                combat[i].phase=0;combat[i].attackId=0;combat[i].cooldownLeft=0;
            }
            ResetCamera();return true;
        }
    }
}
