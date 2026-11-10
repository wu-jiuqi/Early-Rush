using UnityEngine;

namespace EarlyRush.V9
{
    public sealed partial class PersonV9
    {
        public bool dashAirborne;
        public float dashHeight,dashFlightProgress01,dashLandingFeedback01;
        internal int lastPlayerDashSerial=-1;
        internal float dashFlightLeft,dashProtectionLeft,dashLandingLeft,dashFlightDistance;
        internal Vector2 dashFlightDirection,dashSavedDestination;
    }

    /// <summary>Player dash impact has visual height; all displacement remains on safe ground.</summary>
    public sealed partial class RunV9
    {
        const float DashNpcFlightDuration=.5f,DashNpcLaunchSpeed=7.4f,DashNpcPeakHeight=1.3f,DashNpcProtection=.8f,DashNpcMaxDisplacement=1.9f;
        int playerDashSerial;
        double lastPlayerDashImpact=-1000;
        public int DashLaunchCount {get;private set;}
        public float DashImpactFeedback01=>Mathf.Clamp01(1-(float)(elapsed-lastPlayerDashImpact)/.2f);

        void BeginDashImpact(){playerDashSerial++;}

        public bool HandlePlayerDashContact(PersonV9 person)
        {
            if(phase!=RunPhase.Playing||!IsDashing||IsBracing||person.exited||person.kind!=PersonKind.Ordinary||person.dashAirborne||
                person.dashProtectionLeft>0||person.lastPlayerDashSerial==playerDashSerial)return false;
            // Player-contact resolution calls this before committing this step's ground position.
            // The extra skin is bounded by one 60 Hz bicycle-dash step; the caller owns contact.
            if(Vector2.Distance(person.position,player)>person.radius+PlayerRadius+.19f)return false;
            if(IsCrowdProtected(player,PlayerRadius+.2f)||IsCrowdProtected(person.position,person.radius+.2f))return false;
            person.lastPlayerDashSerial=playerDashSerial;person.dashAirborne=true;person.dashHeight=0;
            person.dashFlightLeft=DashNpcFlightDuration;person.dashProtectionLeft=DashNpcProtection;
            person.dashFlightProgress01=0;person.dashLandingFeedback01=0;person.dashLandingLeft=0;person.dashFlightDistance=0;
            person.dashFlightDirection=dashDirection.sqrMagnitude>.001f?dashDirection.normalized:facing.normalized;
            person.dashSavedDestination=person.destination;
            // Stop this NPC's counter-charge immediately without transferring an attack to others.
            person.actionPhase=NpcActionPhase.Cruise;person.actionTime=0;person.chargeHitConsumed=true;person.chargeCancelled=true;
            person.actionCooldownLeft=Mathf.Max(person.actionCooldownLeft,config.npcChargeCooldown);
            person.velocity=Vector2.zero;person.impatience=0;
            DashLaunchCount++;lastPlayerDashImpact=elapsed;Cue("dash-impact");return true;
        }

        internal bool StepDashImpact(PersonV9 person,float dt)
        {
            if(phase!=RunPhase.Playing||dt<=0)return person.dashAirborne;
            person.dashProtectionLeft=Mathf.Max(0,person.dashProtectionLeft-dt);
            person.dashLandingLeft=Mathf.Max(0,person.dashLandingLeft-dt);person.dashLandingFeedback01=Mathf.Clamp01(person.dashLandingLeft/.18f);
            if(!person.dashAirborne)return false;
            person.dashFlightLeft=Mathf.Max(0,person.dashFlightLeft-dt);
            float progress=Mathf.Clamp01(1-person.dashFlightLeft/DashNpcFlightDuration);
            person.dashFlightProgress01=progress;
            person.dashHeight=DashNpcPeakHeight*4*progress*(1-progress);
            Vector2 before=person.position,delta=person.dashFlightDirection*DashNpcLaunchSpeed*(1-progress)*dt;
            delta=Vector2.ClampMagnitude(delta,Mathf.Max(0,DashNpcMaxDisplacement-person.dashFlightDistance));
            Vector2 candidate=MoveCrowdPerson(person,delta);
            // Task/exit/station/traffic protection also applies to inertial movement.
            if(!IsCrowdProtected(candidate,person.radius+.2f))person.position=candidate;
            person.dashFlightDistance+=Vector2.Distance(before,person.position);
            person.velocity=(person.position-before)/dt;
            if(person.dashFlightLeft<=0)
            {
                person.dashAirborne=false;person.dashHeight=0;person.velocity=Vector2.zero;
                person.dashLandingLeft=.18f;person.dashLandingFeedback01=1;
                if(person.hasPedestrianAI)
                {
                    // Replan from where the body actually landed, keeping the actor's own goal.
                    if(!AssignPedestrianDestination(person,person.dashSavedDestination))
                    {person.aiState=PedestrianState.Replanning;person.decisionLeft=0;}
                }
                Log("dash-npc-landed",person.id);
            }
            return true;
        }
    }
}
