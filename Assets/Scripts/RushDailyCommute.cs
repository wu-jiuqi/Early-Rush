using UnityEngine;

namespace EarlyRush
{
    // Optional, bounded errands. Never change the destination or block the main route.
    public partial class RushGame
    {
        Vector2 errandPickup, errandDropoff;
        int errandPhase;
        float errandHold;
        public int ErrandCompleted { get; private set; }
        public float ErrandRewardSeconds { get; private set; }
        public string ErrandKind => Stage==0?"breakfast":Stage==1?"lost-item":"coffee";
        public int ErrandPhase => errandPhase; // 0 pickup, 1 delivery, 2 completed
        public Vector2 ErrandPosition => errandPhase==0?errandPickup:errandDropoff;
        public Vector2 ErrandPickup => errandPickup;
        public Vector2 ErrandDropoff => errandDropoff;
        public float ErrandProgress01 => Mathf.Clamp01(errandHold/.65f);
        public bool NearErrand => errandPhase<2&&Vector2.Distance(position,ErrandPosition)<1.65f;
        public bool OnClockPad => Stage==2&&world!=null&&Mathf.Abs(position.x-world.goal.x)<world.goalHalfSize.x&&Mathf.Abs(position.y-world.goal.z)<world.goalHalfSize.y;
        public bool CanInteractHere => OnClockPad||NearErrand;
        // Rain begins in the second outdoor district; easing avoids an abrupt friction switch.
        public float Rain01 => Stage==0?Mathf.SmoothStep(0,1,Mathf.InverseLerp(100,112,position.y))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(172,184,position.y))):0;
        public string TerrainKind => Stage==1&&position.y>=84&&position.y<=90?(position.x>10&&position.x<15?"escalator":position.x> -15&&position.x< -10?"stairs":"floor"):Rain01>.05f?"rain":"floor";
        float TerrainSpeedMultiplier => TerrainKind=="stairs"?.72f:1f;

        void BuildDailyCommute()
        {
            errandPhase=0;errandHold=0;
            // Find a clear point connected to the known main route rather than placing inside furniture.
            errandPickup=ClearErrandSpot(12,Stage==0?16:Stage==1?30:26);
            errandDropoff=ClearErrandSpot(-12,Stage==0?44:Stage==1?43:72);
        }
        Vector2 ClearErrandSpot(float preferredX,float z)
        {
            Vector2 route=JourneyRoutePoint(z);
            for(int i=0;i<=12;i++)
            {
                Vector2 p=new Vector2(Mathf.Lerp(preferredX,route.x,i/12f),z);
                if(StaticCircleRouteClear(route,p,.65f)&&StaticCircleRouteClear(p,p,.65f))return p;
            }
            return route;
        }
        void TickDailyCommute(float dt)
        {
            if(impactQaMode||combatQaMode||State!=RushState.Playing)return;
            if(errandPhase>=2)return;
            bool eligible=interactInput&&NearErrand&&!PlayerAirborne&&!PlayerRecovering&&velocity.sqrMagnitude<1.96f;
            errandHold=eligible?Mathf.Min(.65f,errandHold+dt):0;
            if(errandHold<.65f)return;
            errandHold=0;errandPhase++;Sound(tickClip,.6f);
            if(errandPhase==2)
            {
                // Reward grace time, never subtract measured elapsed time or falsify split records.
                ErrandCompleted++;ErrandRewardSeconds+=8;TimeLimit+=8;
                Sound(goalClip,.6f);Log("errand_complete_"+ErrandKind);
            }
            else Log("errand_pickup_"+ErrandKind);
        }
    }
}
