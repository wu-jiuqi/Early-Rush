using UnityEngine;
using EarlyRush.V8;

namespace EarlyRush.V9
{
    public enum ActorPoseV11 { Walk, Brace, Dash, Windup, Recovery, Cancelled, Airborne }

    /// <summary>Inexpensive sprite-only motion. Simulation owns position, contacts and action timing.</summary>
    public static class RushActorPoseV11
    {
        public static void Apply(SpriteRenderer body,SpriteRenderer shadow,Vector2 ground,Vector2 velocity,Vector2 facing,
            float width,double clock,ActorPoseV11 pose,float progress,float height,int appearance,bool bicycle)
        {
            if(!body)return;
            float moving=Mathf.Clamp01(velocity.magnitude/3.6f),phase=(float)clock*(bicycle?14:10)+appearance*1.71f;
            float stride=Mathf.Sin(phase),bob=bicycle?0:Mathf.Abs(stride)*.045f*moving;
            float sx=1,sy=1,angle=bicycle?0:-stride*2.8f*moving;
            Vector2 offset=Vector2.up*bob;
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
                    sx=1.12f;sy=.85f;angle=Mathf.Sin((float)clock*15)*16;offset.y=0;break;
            }
            // Feet and shadow keep the stable physical ground position. Only the rendered body rises.
            ArtV8.SetPosition(body,ground);
            body.transform.localPosition+=new Vector3(offset.x,offset.y+Mathf.Max(0,height),0);
            ArtV8.SetWidth(body,width);
            body.transform.localScale=Vector3.Scale(body.transform.localScale,new Vector3(sx,sy,1));
            body.transform.localRotation=Quaternion.Euler(0,0,angle);
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
