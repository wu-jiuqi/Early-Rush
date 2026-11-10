using System;
using UnityEngine;

namespace EarlyRush.V9
{
    public enum NpcActionPhase { Cruise, Windup, Charging, Recovery }

    public sealed partial class ConfigV9
    {
        public float dashDuration=.24f,dashCooldown=.65f,dashSpeed=10f,bikeDashSpeed=11.2f;
        public float dashEaseOutDuration=.10f,dashInputBuffer=.14f;
        public float braceSpeedMultiplier=.44f;
        public float npcWindupDuration=.65f,npcChargeDuration=.42f,npcRecoveryDuration=.85f;
        public float npcChargeSpeed=6.5f,npcChargeCooldown=6f,npcChargeRange=4.8f;
        public float playerKnockbackDuration=.4f,playerKnockbackSpeed=6.7f,playerHopHeight=1f;
        public float hitProtectionDuration=1.1f,npcGlobalGap=1.35f;
    }

    public sealed partial class PersonV9
    {
        public bool canRush,chargeHitConsumed,chargeCancelled;
        public NpcActionPhase actionPhase;
        public Vector2 chargeDirection;
        public float actionTime,actionCooldownLeft;
        internal bool actionInitialized;
    }

    /// <summary>Input actions share the simulation clock and existing ground collision pipeline.</summary>
    public sealed partial class RunV9
    {
        float dashLeft,dashCooldownLeft,dashBufferLeft,knockbackLeft,hitProtectionLeft,braceFlashLeft,npcGlobalGapLeft,playerLandingLeft;
        Vector2 dashDirection,bufferedDashDirection,knockbackDirection;
        bool braceHeld;
        public bool IsDashing=>dashLeft>0;
        public bool IsBracing=>braceHeld;
        public bool IsAirborne=>knockbackLeft>0;
        public float PlayerHeight=>IsAirborne?Mathf.Sin(Mathf.Clamp01(1-knockbackLeft/config.playerKnockbackDuration)*Mathf.PI)*config.playerHopHeight:0;
        public float DashReady01=>Mathf.Clamp01(1-dashCooldownLeft/config.dashCooldown);
        public bool DashBuffered=>phase==RunPhase.Playing&&dashBufferLeft>0;
        public float DashBuffer01=>DashBuffered?Mathf.Clamp01(dashBufferLeft/config.dashInputBuffer):0;
        public int DashStartCount {get;private set;}
        public float BraceFeedback01=>Mathf.Clamp01(braceFlashLeft/.28f);
        public float HitProtection01=>Mathf.Clamp01(hitProtectionLeft/config.hitProtectionDuration);
        public float PlayerFlightProgress01=>IsAirborne?Mathf.Clamp01(1-knockbackLeft/config.playerKnockbackDuration):1;
        public float PlayerLandingFeedback01=>Mathf.Clamp01(playerLandingLeft/.18f);
        public int PlayerLaunchCount { get; private set; }
        public int BraceBlockCount { get; private set; }
        public int NpcChargeCount { get; private set; }

        internal void ValidateActionConfiguration()
        {
            foreach(float value in new[]{config.dashDuration,config.dashCooldown,config.dashSpeed,config.bikeDashSpeed,config.dashEaseOutDuration,config.dashInputBuffer,
                config.braceSpeedMultiplier,config.npcWindupDuration,config.npcChargeDuration,config.npcRecoveryDuration,
                config.npcChargeSpeed,config.npcChargeCooldown,config.npcChargeRange,config.playerKnockbackDuration,
                config.playerKnockbackSpeed,config.playerHopHeight,config.hitProtectionDuration,config.npcGlobalGap})
                if(value<=0||float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentException("Invalid V11 action configuration");
            if(config.braceSpeedMultiplier>=1||config.hitProtectionDuration<config.playerKnockbackDuration||config.dashSpeed<=config.walkSpeed||config.bikeDashSpeed<=config.bikeSpeed||
                config.dashEaseOutDuration>config.dashDuration||config.dashCooldown<config.dashDuration||config.dashInputBuffer>config.dashCooldown)
                throw new ArgumentException("V11 action speed/protection ordering is invalid");
        }

        public bool TryDash()=>TryDash(facing);
        public bool TryDash(Vector2 input)
        {
            if(phase!=RunPhase.Playing||braceHeld||IsAirborne||IsDashing||dashCooldownLeft>0)return false;
            if(float.IsNaN(input.x)||float.IsNaN(input.y)||float.IsInfinity(input.x)||float.IsInfinity(input.y))return false;
            dashDirection=input.sqrMagnitude>.001f?input.normalized:facing.normalized;
            if(dashDirection.sqrMagnitude<.001f)dashDirection=Vector2.up;
            facing=dashDirection;dashLeft=config.dashDuration;dashCooldownLeft=config.dashCooldown;
            dashBufferLeft=0;velocity=dashDirection*(bicycle?config.bikeDashSpeed:config.dashSpeed);
            DashStartCount++;BeginDashImpact();Cue("dash");return true;
        }

        /// <summary>One key-down event. A late press can queue one dash, never an automatic chain.</summary>
        public bool PressDash(Vector2 input)
        {
            if(phase!=RunPhase.Playing||braceHeld||IsAirborne||float.IsNaN(input.x)||float.IsNaN(input.y)||float.IsInfinity(input.x)||float.IsInfinity(input.y))return false;
            if(TryDash(input))return true;
            if(dashCooldownLeft>config.dashInputBuffer||dashCooldownLeft<=0)return false;
            bufferedDashDirection=input.sqrMagnitude>.001f?input.normalized:facing.normalized;
            if(bufferedDashDirection.sqrMagnitude<.001f)bufferedDashDirection=Vector2.up;
            dashBufferLeft=config.dashInputBuffer;return true;
        }

        // Pause/finish hooks clear input intent, while cooldown, airborne and protection clocks
        // retain the simulation's normal pause semantics. Resuming never consumes an old press.
        internal void CancelPlayerActionInput()
        {
            bool wasDashing=IsDashing;dashLeft=0;dashBufferLeft=0;braceHeld=false;
            if(wasDashing)velocity=Vector2.zero;
        }

        public void SetBracing(bool held)
        {
            if(held&&phase!=RunPhase.Playing)return;
            braceHeld=held;
            if(!held)return;
            dashLeft=0;dashBufferLeft=0;
            if(!IsAirborne)velocity=Vector2.ClampMagnitude(velocity,(bicycle?config.bikeSpeed:config.walkSpeed)*config.braceSpeedMultiplier);
        }

        internal void StepPlayerActions(float dt,Vector2 input)
        {
            if(phase!=RunPhase.Playing||dt<=0)return;
            dashCooldownLeft=Mathf.Max(0,dashCooldownLeft-dt);
            if(dashBufferLeft>0)
            {
                // Consume before expiring a frame that crosses the exact readiness boundary.
                if(dashCooldownLeft<=0&&!braceHeld&&!IsAirborne&&!IsDashing)TryDash(bufferedDashDirection);
                else dashBufferLeft=Mathf.Max(0,dashBufferLeft-dt);
            }
            hitProtectionLeft=Mathf.Max(0,hitProtectionLeft-dt);
            braceFlashLeft=Mathf.Max(0,braceFlashLeft-dt);
            npcGlobalGapLeft=Mathf.Max(0,npcGlobalGapLeft-dt);
            playerLandingLeft=Mathf.Max(0,playerLandingLeft-dt);
            if(knockbackLeft>0)
            {
                knockbackLeft=Mathf.Max(0,knockbackLeft-dt);
                float strength=knockbackLeft/config.playerKnockbackDuration;
                // Airborne is visual only. Ground velocity still uses wall and red-light constraints.
                velocity=knockbackDirection*config.playerKnockbackSpeed*strength;
                if(knockbackLeft==0){velocity=Vector2.zero;playerLandingLeft=.18f;}
                return;
            }
            if(dashLeft>0&&!braceHeld)
            {
                dashLeft=Mathf.Max(0,dashLeft-dt);
                float baseSpeed=bicycle?config.bikeSpeed:config.walkSpeed,peak=bicycle?config.bikeDashSpeed:config.dashSpeed;
                float release=Mathf.Clamp01(1-dashLeft/config.dashEaseOutDuration);
                release=release*release*(3-2*release);
                float forwardInput=Mathf.Clamp01(Vector2.Dot(Vector2.ClampMagnitude(input,1),dashDirection));
                // Keep the committed direction; ease down to walking/riding speed when held,
                // or to rest when released, rather than leaving a long uncontrolled speed tail.
                velocity=dashDirection*Mathf.Lerp(peak,baseSpeed*forwardInput,release);
                facing=dashDirection;return;
            }
            float speed=(bicycle?config.bikeSpeed:config.walkSpeed)*(braceHeld?config.braceSpeedMultiplier:1);
            velocity=Vector2.MoveTowards(velocity,Vector2.ClampMagnitude(input,1)*speed,dt*(bicycle?12:20));
            if(braceHeld)velocity=Vector2.ClampMagnitude(velocity,speed);
        }

        public float NpcWindup01(PersonV9 person)=>person.actionPhase==NpcActionPhase.Windup?Mathf.Clamp01(person.actionTime/config.npcWindupDuration):0;
        public float NpcRecovery01(PersonV9 person)=>person.actionPhase==NpcActionPhase.Recovery?Mathf.Clamp01(person.actionTime/config.npcRecoveryDuration):0;

        bool AnyNpcAttackActive()
        {
            foreach(var person in people)
                if(!person.exited&&(person.actionPhase==NpcActionPhase.Windup||person.actionPhase==NpcActionPhase.Charging))return true;
            return false;
        }

        bool ClearChargeLane(PersonV9 person,Vector2 direction)
        {
            float distance=config.npcChargeSpeed*config.npcChargeDuration;
            for(float along=0;along<=distance+.001f;along+=.3f)
            {
                Vector2 point=person.position+direction*along;
                if(IsCrowdProtected(point,person.radius+.35f)||!map.IsWalkable(point,person.radius+.1f))return false;
                foreach(var body in bodies)if(Vector2.Distance(point,body.position)<person.radius+body.radius+.35f)return false;
                foreach(var fixture in fixtures)if(Vector2.Distance(point,fixture.position)<person.radius+fixture.radius+.35f)return false;
            }
            Vector2 end=person.position+direction*distance;
            return Vector2.Distance(Resolve(person.position,direction*distance,person.radius),end)<.05f;
        }

        bool PlayerBlocksPedestrianRoute(PersonV9 person,Vector2 direction)
        {
            if(person.profile!=PedestrianProfile.Hurried||person.impatience<.8f||direction.sqrMagnitude<.5f)return false;
            if(person.aiState!=PedestrianState.Walking&&person.aiState!=PedestrianState.Yielding)return false;
            Vector2 offset=player-person.position;
            float forward=Vector2.Dot(offset,direction),lateral=Mathf.Abs(offset.x*direction.y-offset.y*direction.x);
            return forward>person.radius+PlayerRadius+.05f&&offset.magnitude<=Mathf.Min(3.8f,config.npcChargeRange)&&lateral<.9f;
        }

        void RecoverNpc(PersonV9 person,bool cancelled)
        {
            person.actionPhase=NpcActionPhase.Recovery;person.actionTime=0;person.chargeCancelled=cancelled;person.velocity=Vector2.zero;
            person.actionCooldownLeft=config.npcChargeCooldown;npcGlobalGapLeft=Mathf.Max(npcGlobalGapLeft,config.npcGlobalGap);
            Log(cancelled?"npc-charge-cancelled":"npc-recovery",person.id);
        }

        internal bool StepNpcAction(PersonV9 person,float dt)
        {
            if(phase!=RunPhase.Playing||person.exited||dt<=0)return false;
            if(person.kind!=PersonKind.Ordinary||!person.canRush)
            {
                person.actionPhase=NpcActionPhase.Cruise;person.actionTime=0;return false;
            }
            if(!person.actionInitialized)
            {
                person.actionInitialized=true;
                person.actionCooldownLeft=1.5f+(Math.Abs(person.id)%11)*.17f;
            }
            person.actionCooldownLeft=Mathf.Max(0,person.actionCooldownLeft-dt);
            if(person.actionPhase==NpcActionPhase.Recovery)
            {
                person.actionTime+=dt;person.velocity=Vector2.zero;
                if(person.actionTime>=config.npcRecoveryDuration){person.actionPhase=NpcActionPhase.Cruise;person.actionTime=0;person.chargeCancelled=false;}
                return true;
            }
            if(person.actionPhase==NpcActionPhase.Windup||person.actionPhase==NpcActionPhase.Charging)
            {
                if(IsCrowdProtected(person.position,person.radius+.35f)||IsCrowdProtected(player,PlayerRadius+.35f))
                {RecoverNpc(person,true);return true;}
                // A commuter resumes their own trip when the player yields; no target tracking.
                if(person.hasPedestrianAI&&person.actionPhase==NpcActionPhase.Windup&&!PlayerBlocksPedestrianRoute(person,person.chargeDirection))
                {RecoverNpc(person,true);return true;}
                person.actionTime+=dt;
                if(person.actionPhase==NpcActionPhase.Windup)
                {
                    person.velocity=Vector2.zero;
                    if(person.actionTime>=config.npcWindupDuration)
                    {
                        person.actionPhase=NpcActionPhase.Charging;person.actionTime=0;NpcChargeCount++;Log("npc-charge",person.id);
                    }
                    return true;
                }
                Vector2 before=person.position;
                Vector2 delta=person.chargeDirection*config.npcChargeSpeed*dt;
                if(IsCrowdProtected(before+delta,person.radius+.35f)){RecoverNpc(person,true);return true;}
                person.position=MoveCrowdPerson(person,delta);
                person.velocity=(person.position-before)/dt;
                if(Vector2.Distance(person.position,player)<=person.radius+PlayerRadius+.025f)
                {Contact(person.id);HandleNpcActionContact(person);}
                if(person.actionPhase==NpcActionPhase.Charging&&(person.actionTime>=config.npcChargeDuration||(person.position-before).sqrMagnitude<delta.sqrMagnitude*.1f))RecoverNpc(person,false);
                return true;
            }
            if(person.actionCooldownLeft>0||npcGlobalGapLeft>0||AnyNpcAttackActive()||IsAirborne||hitProtectionLeft>0)return false;
            Vector2 toward=player-person.position;float range=toward.magnitude;
            Vector2 direction=toward.normalized;
            if(person.hasPedestrianAI)
            {
                direction=person.preferredDirection.normalized;
                if(!PlayerBlocksPedestrianRoute(person,direction))return false;
            }
            else if(range<1.5f||range>config.npcChargeRange)return false;
            if(IsCrowdProtected(player,PlayerRadius+.35f)||!ClearChargeLane(person,direction))return false;
            person.chargeDirection=direction;person.actionPhase=NpcActionPhase.Windup;person.actionTime=0;
            person.chargeHitConsumed=false;person.chargeCancelled=false;person.velocity=Vector2.zero;
            Log("npc-windup",person.id);return true;
        }

        public bool HandleNpcActionContact(PersonV9 person)
        {
            if(phase!=RunPhase.Playing||person.exited||!person.canRush||person.kind!=PersonKind.Ordinary||person.actionPhase!=NpcActionPhase.Charging||person.chargeHitConsumed||Vector2.Distance(person.position,player)>person.radius+PlayerRadius+.05f)return false;
            // The same dash-priority rule applies whether the player or NPC moved into contact.
            if(IsDashing&&HandlePlayerDashContact(person))return true;
            person.chargeHitConsumed=true;
            bool protectedArea=IsCrowdProtected(player,PlayerRadius+.35f)||IsCrowdProtected(person.position,person.radius+.35f);
            if(protectedArea){RecoverNpc(person,true);return false;}
            if(braceHeld)
            {
                BraceBlockCount++;braceFlashLeft=.28f;Cue("brace-block");RecoverNpc(person,false);return true;
            }
            if(hitProtectionLeft>0||IsAirborne){RecoverNpc(person,false);return false;}
            knockbackDirection=person.chargeDirection.sqrMagnitude>.001f?person.chargeDirection.normalized:(player-person.position).normalized;
            if(knockbackDirection.sqrMagnitude<.001f)knockbackDirection=facing;
            dashLeft=0;dashBufferLeft=0;knockbackLeft=config.playerKnockbackDuration;hitProtectionLeft=config.hitProtectionDuration;
            PlayerLaunchCount++;Cue("player-bumped");RecoverNpc(person,false);return true;
        }
    }
}
