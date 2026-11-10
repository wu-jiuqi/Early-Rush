using UnityEngine;
namespace EarlyRush
{
    /// <summary>World-space errand bubbles and lightweight rain. Input stays in RushGame.</summary>
    public class RushRouteHUD:MonoBehaviour
    {
        RushGame game;WorldData current;Transform pickup,dropoff;float rainClock;
        GUIStyle text;
        void Awake(){game=GetComponent<RushGame>();}
        void Update()
        {
            if(game==null||game.World==null)return;
            if(current!=game.World)
            {
                current=game.World;
                Vector2 p=game.ErrandPickup,d=game.ErrandDropoff;
                pickup=RushArt.Sprite(current.root,game.ErrandKind,new Vector3(p.x,2.4f,p.y),2.2f,2.2f);
                dropoff=RushArt.Sprite(current.root,"check",new Vector3(d.x,2.4f,d.y),2.2f,2.2f);
                // Stand graphics are outside the clear interaction radius; no hidden collision is added.
                string stand=game.Stage==0?"breakfast-stall":game.Stage==1?"lost-item":"coffee-stall";
                RushArt.Sprite(current.root,stand,new Vector3(p.x+2.8f,.8f,p.y),3.0f,3.2f);
                if(game.Stage==1)
                {
                    RushArt.Sprite(current.root,"stairs-prop",new Vector3(-12.5f,.13f,87),5,7);
                    RushArt.Sprite(current.root,"escalator-prop",new Vector3(12.5f,.13f,87),5,7);
                }
            }
            if(pickup)pickup.gameObject.SetActive(game.ErrandPhase==0);
            if(dropoff)dropoff.gameObject.SetActive(game.ErrandPhase==1);
            if(game.State==RushState.Playing)rainClock+=Time.deltaTime;
        }
        void OnGUI()
        {
            if(game==null||game.State!=RushState.Playing||game.World==null)return;
            float scale=Mathf.Max(.1f,Mathf.Min(Screen.width/1280f,Screen.height/720f));Matrix4x4 old=GUI.matrix;Color oldColor=GUI.color;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));float width=Screen.width/scale,height=Screen.height/scale;
            // Procedural streaks are weather VFX, not substitute SVG or placeholder illustrations.
            float rain=game.Rain01;
            if(rain>.01f)
            {
                GUI.color=new Color(.52f,.78f,.85f,.38f*rain);
                for(int i=0;i<36;i++)
                {float x=(i*173.37f+rainClock*40)%width,y=(i*89.71f+rainClock*530)%height;GUI.DrawTexture(new Rect(x,y,1.5f,12),Texture2D.whiteTexture);}
                GUI.color=Color.white;
            }
            if(text==null){text=new GUIStyle(GUI.skin.label){fontSize=18,alignment=TextAnchor.MiddleCenter};text.normal.textColor=new Color(.08f,.29f,.3f);}
            if(game.ErrandPhase<2)
            {
                Vector2 dest=game.ErrandPosition;Vector3 screen=game.GameCamera.WorldToViewportPoint(new Vector3(dest.x,2.5f,dest.y));
                if(screen.z>0)
                {
                    float x=Mathf.Clamp(screen.x*width,178,width-110),y=Mathf.Clamp((1-screen.y)*height,150,height-155);
                    Rect bubble=new Rect(x-30,y-30,60,60);RushArt.DrawIcon(game.ErrandKind,bubble);
                    string hint=game.NearErrand?"E":Vector2.Distance(game.Position,dest).ToString("F0")+"m";
                    GUI.Label(new Rect(x-36,y+28,72,26),hint,text);
                    if(game.ErrandPhase==1)RushArt.DrawIcon("check",new Rect(x+17,y-42,28,28));
                }
            }
            Vector3 player=game.GameCamera.WorldToViewportPoint(new Vector3(game.Position.x,.12f,game.Position.y));
            if(player.z>0)RushArt.DrawIcon("arrow",new Rect(player.x*width-13,(1-player.y)*height+16,26,26));
            GUI.matrix=old;GUI.color=oldColor;
        }
    }
}
