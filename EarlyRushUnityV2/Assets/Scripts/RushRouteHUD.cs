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
            string hint=game.IsStumbling?"被挤住了：按 SHIFT 稳住，恢复步伐。\n松开稳住后，再冲刺穿过空档。":game.World.routeHint;
            Panel(new Rect(width*.5f-265,105,530,54),hint);
            Vector2 v=game.GoalViewport;
            if(v.x<.06f||v.x>.94f||v.y<.16f||v.y>.76f)
            {
                Vector2 direction=v-new Vector2(.5f,.5f);
                string arrow=Mathf.Abs(direction.x)>Mathf.Abs(direction.y)?(direction.x>0?"→":"←"):(direction.y>0?"↑":"↓");
                float x=Mathf.Clamp(v.x*width,120,width-120);
                float y=Mathf.Clamp((1-v.y)*height,188,height-140);
                Panel(new Rect(x-100,y-22,200,44),arrow+" 目标 "+game.GoalDistance.ToString("F0")+" 米");
            }
            GUI.matrix=before;
        }
        void Panel(Rect bounds,string text)
        {
            Color old=GUI.color;
            GUI.color=new Color(.07f,.19f,.24f,.94f);
            GUI.DrawTexture(bounds,Texture2D.whiteTexture);
            GUI.color=Color.white;
            GUI.Label(new Rect(bounds.x+8,bounds.y+3,bounds.width-16,bounds.height-6),text,label);
            GUI.color=old;
        }
    }
}
