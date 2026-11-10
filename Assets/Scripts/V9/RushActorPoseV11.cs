using UnityEngine;
using EarlyRush.V8;

namespace EarlyRush.V9
{
    public enum ActorPoseV11 { Walk, Brace, Dash, Windup, Recovery, Cancelled, Airborne }

    /// <summary>Inexpensive sprite-only motion. Simulation owns position, contacts and action timing.</summary>
    public static class RushActorPoseV11
    {
        public static void Apply(SpriteRenderer body,SpriteRenderer shadow,Vector2 ground,Vector2 velocity,Vector2 facing,
            float width,double clock,ActorPoseV11 pose,float progress,float height,int appearance,bool bicycle,float flightProgress=0,float landingFeedback=0)
        {
            if(!body)return;
            float moving=Mathf.Clamp01(velocity.magnitude/3.6f),phase=(float)clock*(bicycle?14:10)+appearance*1.71f;
            float stride=Mathf.Sin(phase),bob=bicycle?0:Mathf.Abs(stride)*.045f*moving;
            float sx=1,sy=1,angle=bicycle?0:-stride*2.8f*moving;
            Vector2 offset=Vector2.up*bob;float centreWeight=0;
            switch(pose)
            {
                case ActorPoseV11.Brace:
                    sx=1.08f;sy=.88f;angle=-facing.x*3;offset.y=bicycle?0:bob*.35f;break;
                case ActorPoseV11.Dash:
                    sx=.91f;sy=1.08f;angle=-facing.x*13;offset+=facing*.07f;break;
                case ActorPoseV11.Windup:
                    sx=1+.14f*progress;sy=1-.22f*progress;angle=-facing.x*11*progress;
                    offset=-facing*.11f*progress;break;
                case ActorPoseV11.Recovery:
                    sx=1+.1f*(1-progress);sy=1-.13f*(1-progress);angle=-facing.x*7*(1-progress);break;
                case ActorPoseV11.Cancelled:
                    sx=1+.05f*(1-progress);sy=1-.07f*(1-progress);angle=Mathf.Sin(progress*Mathf.PI*2)*5*(1-progress);break;
                case ActorPoseV11.Airborne:
                    float arc=Mathf.Sin(Mathf.Clamp01(flightProgress)*Mathf.PI);
                    sx=.8f+.28f*arc;sy=1.28f-.32f*arc;
                    angle=(facing.x<-.1f?-1:1)*(20+280*Mathf.SmoothStep(0,1,Mathf.Clamp01(flightProgress)));offset.y=0;centreWeight=1;break;
            }
            if(landingFeedback>0&&pose!=ActorPoseV11.Airborne)
            {
                float remaining=Mathf.Clamp01(landingFeedback);
                float blend=Mathf.SmoothStep(0,1,1-remaining),pulse=Mathf.Sin(remaining*Mathf.PI);
                // Flight ends at signed 300 degrees (= -60), .8 x 1.28 scale and a centre pivot.
                // Start at that exact pose, then smoothly return to the live locomotion pose/pivot.
                sx=Mathf.Lerp(.8f,sx,blend)+.26f*pulse;
                sy=Mathf.Lerp(1.28f,sy,blend)-.28f*pulse;
                angle=Mathf.LerpAngle((facing.x<-.1f?-1:1)*-60,angle,blend);
                offset=Vector2.Lerp(Vector2.zero,offset,blend)+Vector2.up*(.06f*pulse);
                centreWeight=1-blend;
            }
            // Feet and shadow keep the stable physical ground position. Only the rendered body rises.
            ArtV8.SetPosition(body,ground);
            body.transform.localPosition+=new Vector3(offset.x,offset.y+Mathf.Max(0,height),0);
            ArtV8.SetWidth(body,width);
            body.transform.localScale=Vector3.Scale(body.transform.localScale,new Vector3(sx,sy,1));
            body.transform.localRotation=Quaternion.Euler(0,0,angle);
            if(centreWeight>0)
            {
                // Retain the centre correction through landing and fade it out continuously.
                // Its weight is exactly zero when normal foot-pivot locomotion resumes.
                Vector3 centre=Vector3.up*(body.sprite.bounds.center.y*body.transform.localScale.y);
                body.transform.localPosition+=(centre-body.transform.localRotation*centre)*centreWeight;
            }
            if(shadow)
            {
                ArtV8.SetPosition(shadow,ground,ArtV8.GroundMarkOrder+3);
                ArtV8.SetWidth(shadow,width*.85f);
                shadow.color=new Color(1,1,1,Mathf.Lerp(.75f,.45f,Mathf.Clamp01(height)));
                shadow.transform.localRotation=Quaternion.identity;
            }
        }
        public static Color Tint(int appearance)
        {
            switch((appearance%6+6)%6)
            {
                case 0:return Color.white;
                case 1:return new Color(.94f,.83f,.9f);
                case 2:return new Color(.86f,.94f,1f);
                case 3:return new Color(1f,.94f,.78f);
                case 4:return new Color(.82f,1f,.88f);
                default:return new Color(.91f,.89f,1f);
            }
        }
    }
}
