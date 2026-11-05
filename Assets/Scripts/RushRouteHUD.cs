using UnityEngine;
namespace EarlyRush
{
    public class RushRouteHUD : MonoBehaviour
    {
        RushGame game; GUIStyle label; Font font;
        void Awake(){game=GetComponent<RushGame>();}
        void OnGUI()
        {
            if(game==null||game.State!=RushState.Playing||game.World==null)return;
            if(label==null)
            {
                font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},18);
                label=new GUIStyle(GUI.skin.label){font=font,fontSize=14,alignment=TextAnchor.MiddleCenter,wordWrap=true};
                label.normal.textColor=Color.white;
            }
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            Matrix4x4 before=GUI.matrix;
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,Vector3.one*scale);
            float width=Screen.width/scale,height=Screen.height/scale;
            // Keep the badge below the feet so it cannot cover a launched body ahead.
            Vector3 player=game.GameCamera.WorldToViewportPoint(new Vector3(game.Position.x,.12f,game.Position.y));
            if(player.z>0&&player.x>0&&player.x<1&&player.y>0&&player.y<1)
            {
                Rect badge=new Rect(player.x*width-12,Mathf.Min((1-player.y)*height+18,height-120),24,24);
                Color old=GUI.color;
                GUI.color=new Color(.07f,.19f,.24f,1);
                GUI.DrawTexture(new Rect(badge.x-2,badge.y-2,28,28),Texture2D.whiteTexture);
                GUI.color=new Color(1,.55f,.18f,1);
                GUI.DrawTexture(badge,Texture2D.whiteTexture);
                GUI.color=new Color(.07f,.19f,.24f,1);
                GUI.Label(badge,"你",label);
                GUI.color=old;
            }
            GUI.matrix=before;
        }
    }
}
