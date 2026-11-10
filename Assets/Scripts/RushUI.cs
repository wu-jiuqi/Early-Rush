using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush
{
    // Icon-first presentation. Short labels are confined to tooltips and the first-run key guide.
    // Timing, collision information and controls remain numeric/textual for legibility.
    public static class RushUI
    {
        static readonly Color Ink=new Color32(25,76,77,255), Cream=new Color32(255,250,231,255),
            Teal=new Color32(47,190,173,255), Gold=new Color32(255,205,57,255), Red=new Color32(234,100,83,255);
        static readonly Dictionary<string,GUIStyle> styles=new Dictionary<string,GUIStyle>();
        static Font font;
        static RushGame game;
        static float width,height;
        static string tooltip;
        static Rect R(float x,float y,float w,float h)=>new Rect((width-1280)*.5f+x,(height-720)*.5f+y,w,h);
        public static void Draw(RushGame value)
        {
            game=value;
            if(!font)font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Noto Sans CJK SC","Arial"},24);
            if(!font)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Matrix4x4 old=GUI.matrix;Color oldColor=GUI.color;int oldDepth=GUI.depth;
            float scale=Mathf.Max(.1f,Mathf.Min(Screen.width/1280f,Screen.height/720f));
            width=Screen.width/scale;height=Screen.height/scale;GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));GUI.depth=-20;tooltip=null;
            switch(game.State)
            {
                case RushState.Menu:Menu();break;
                case RushState.Tutorial:Tutorial();break;
                case RushState.Playing:Hud();break;
                case RushState.Paused:Hud();Pause();break;
                case RushState.Transition:Hud();Transition();break;
                case RushState.Results:Results();break;
            }
            if(!string.IsNullOrEmpty(tooltip))
            {Card(new Rect(width*.5f-245,height-42,490,30),Ink);Text(new Rect(width*.5f-238,height-42,476,30),tooltip,14,Cream);}
            GUI.matrix=old;GUI.color=oldColor;GUI.depth=oldDepth;
        }
        static string StageIcon(int stage)=>stage==0?"bike":stage==1?"train":"clock";
        static void Menu()
        {
            Scrim(.2f);Card(R(90,60,500,590),Cream);
            Text(R(125,93,430,62),"早八冲刺",52,Ink,true);Text(R(125,158,430,30),"EARLY RUSH",23,Teal,true);
            for(int i=0;i<3;i++){Icon(StageIcon(i),R(136+i*140,220,92,92));if(i<2)Text(R(230+i*140,242,40,40),"→",30,Teal);}
            if(IconButton(R(148,355,384,108),"play","开始三段通勤 · ENTER",Gold))game.StartRun();
            if(IconButton(R(148,482,80,72),"train","地铁练习 · C",Teal))game.StartCoreTutorial();
            if(IconButton(R(246,482,80,72),"sound",game.Muted?"打开声音 · M":"关闭声音 · M",game.Muted?Color.gray:Teal))game.ToggleSound();
            if(IconButton(R(344,482,80,72),"shake",game.ScreenShakeEnabled?"关闭震屏":"打开震屏",game.ScreenShakeEnabled?Teal:Color.gray))game.ToggleScreenShake();
            if(IconButton(R(442,482,80,72),"exit","退出",Cream))game.Quit();
            Text(R(136,583,408,30),"WASD  ·  SPACE  ·  SHIFT  ·  E",17,Ink);
            Card(R(870,132,260,425),Cream);
            for(int i=0;i<3;i++){Icon(StageIcon(i),R(945,155+i*128,110,100));if(i<2)Text(R(978,255+i*128,40,25),"↓",24,Teal);}
        }
        static void Tutorial()
        {
            Scrim(.7f);
            string[] icons={"move","dash","shield","clock"};string[] keys={"W A S D","SPACE","SHIFT","E"};
            string[] tips={"移动 / 转向","冲刺撞飞","稳住防撞","按住交互 / 打卡"};
            for(int i=0;i<4;i++)
            {
                float x=110+i*275;Card(R(x,170,245,340),Cream);Icon(icons[i],R(x+48,200,150,150));
                Text(R(x+12,363,221,55),keys[i],30,Ink,true);Text(R(x+12,431,221,38),tips[i],19,Ink);
            }
            if(IconButton(R(510,556,260,85),"play","出发 · ENTER",Gold))game.BeginPlay();
        }
        static void Hud()
        {
            bool late=game.TotalTime>game.TimeLimit;
            Card(new Rect(22,20,222,74),Cream);Icon(StageIcon(game.Stage),new Rect(32,26,62,62));
            Text(new Rect(103,29,122,27),(game.Stage+1)+" / 3",23,Ink,true);
            Progress(new Rect(105,67,114,8),game.StageProgress01,Teal);
            Card(new Rect(width*.5f-156,20,312,66),Cream);
            for(int i=0;i<3;i++)
            {Icon(StageIcon(i),new Rect(width*.5f-140+i*103,25,54,54),i>game.Stage?.35f:1f);if(i<2)Text(new Rect(width*.5f-82+i*103,34,36,34),"→",25,Teal);}
            Card(new Rect(width-278,20,176,74),late?Red:Cream);Icon("clock",new Rect(width-268,32,45,45));
            Text(new Rect(width-222,29,112,49),(late?"+":"")+TimeText(Mathf.Abs(game.TimeLimit-game.TotalTime)),27,late?Cream:Ink,true);
            if(IconButton(new Rect(width-84,20,62,62),"pause","暂停 · ESC",Teal))game.TogglePause();
            Card(new Rect(22,height-106,215,84),Cream);Icon("move",new Rect(32,height-101,64,64));Text(new Rect(101,height-94,122,44),"WASD",23,Ink,true);
            Card(new Rect(width-266,height-106,244,84),Cream);
            Icon("dash",new Rect(width-258,height-100,61,61),game.PlayerAirborne||game.IsBracing?.4f:1);
            Text(new Rect(width-195,height-99,91,32),"SPACE",17,Ink,true);Progress(new Rect(width-192,height-56,83,7),game.DashCooldown01,Gold);
            Icon("shield",new Rect(width-95,height-98,55,55),game.IsBracing?1:.55f);Text(new Rect(width-96,height-47,56,16),"SHIFT",10,Ink,true);
            if(game.World!=null)
            {
                Vector2 d=game.GoalViewport-new Vector2(.5f,.5f);string arrow=Mathf.Abs(d.x)>Mathf.Abs(d.y)?(d.x>0?"→":"←"):(d.y>0?"↑":"↓");
                Text(new Rect(width*.5f-100,90,200,30),arrow+"  "+game.GoalDistance.ToString("F0")+" m",18,Ink,true);
            }
            if(game.Rain01>0){Icon("rain",new Rect(27,106,45,45));}
            if(game.TerrainKind=="stairs"||game.TerrainKind=="escalator")Icon(game.TerrainKind,new Rect(78,106,45,45));
            JourneyHud();
        }
        static void JourneyHud()
        {
            if(game.World==null)return;
            if(game.JourneyWarning)
            {
                Rect r=new Rect(width*.5f-100,height-120,200,80);Card(r,Gold);Icon("shield",new Rect(r.x+10,r.y+8,60,60));
                Text(new Rect(r.x+77,r.y+9,112,30),game.JourneyWarningRemaining.ToString("F1"),24,Ink,true);Text(new Rect(r.x+77,r.y+41,112,24),"SHIFT",17,Ink,true);
            }
            else if(game.Stage==2&&game.GoalDistance<7)
            {
                Rect r=new Rect(width*.5f-94,height-120,188,80);Card(r,Cream);Icon("clock",new Rect(r.x+9,r.y+9,60,60));Text(new Rect(r.x+80,r.y+8,90,38),"E",30,Ink,true);
                Progress(new Rect(r.x+78,r.y+58,95,8),game.ClockHold01,Teal);
            }
            if(!string.IsNullOrEmpty(game.ErrandKind))
            {
                Rect r=new Rect(22,167,123,114);Card(r,Cream);Icon(game.ErrandKind,new Rect(r.x+26,r.y+4,70,70));
                if(game.ErrandPhase==2)Text(new Rect(r.x+10,r.y+77,103,27),"✓",25,Teal,true);
                else{Text(new Rect(r.x+8,r.y+77,25,25),"E",19,Ink,true);Progress(new Rect(r.x+40,r.y+86,70,8),game.ErrandProgress01,Teal);}
            }
        }
        static void Pause()
        {
            Scrim(.65f);Card(R(380,150,520,420),Cream);Icon("pause",R(591,172,98,98));
            if(IconButton(R(435,304,410,94),"play","继续 · ESC",Gold))game.TogglePause();
            if(IconButton(R(435,425,86,78),"retry","重新开始 · R",Teal))game.Restart();
            if(IconButton(R(543,425,86,78),"sound","声音 · M",game.Muted?Color.gray:Teal))game.ToggleSound();
            if(IconButton(R(651,425,86,78),"shake","震屏",game.ScreenShakeEnabled?Teal:Color.gray))game.ToggleScreenShake();
            if(IconButton(R(759,425,86,78),"home","返回首页",Teal))game.ToMenu();
        }
        static void Transition()
        {
            Scrim(.35f);Card(R(370,248,540,206),Cream);Icon(StageIcon(game.Stage),R(440,270,120,115));Text(R(590,299,90,60),"→",48,Teal);
            Icon(StageIcon(Mathf.Min(game.Stage+1,2)),R(720,270,120,115));Progress(R(420,422,440,8),1-Mathf.Clamp01(game.TransitionLeft/1.55f),Gold);
        }
        static void Results()
        {
            Scrim(.6f);Card(R(310,70,660,580),Cream);Icon("clock",R(570,91,140,126));
            Text(R(335,215,610,50),game.TotalTime<=game.TimeLimit?"✓  "+TimeText(game.TotalTime):"+  "+TimeText(game.TotalTime-game.TimeLimit),37,game.TotalTime<=game.TimeLimit?Teal:Red,true);
            for(int i=0;i<3;i++)
            {if(game.CoreMode&&i!=1)continue;float y=294+i*62;Icon(StageIcon(i),R(420,y,48,48));Text(R(490,y,210,48),TimeText(game.SplitTimes[i]),24,Ink,true);Icon("dash",R(720,y,42,42));Text(R(774,y,80,48),game.SplitCollisions[i].ToString(),22,Ink);}
            if(IconButton(R(410,521,214,83),"retry","再赶一次 · R",Gold))game.Restart();
            if(IconButton(R(656,521,214,83),"home","返回首页",Teal))game.ToMenu();
        }
        static string TimeText(float t){t=Mathf.Max(0,t);return ((int)t/60).ToString("00")+":"+((int)t%60).ToString("00");}
        static void Scrim(float alpha)=>Fill(new Rect(0,0,width,height),new Color(Ink.r,Ink.g,Ink.b,alpha));
        static void Fill(Rect r,Color c){Color old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        static void Card(Rect r,Color c)
        {
            Fill(new Rect(r.x+2,r.y+5,r.width,r.height),new Color(.08f,.2f,.2f,.15f));
            // Panel geometry is functional UI, not placeholder illustration.
            Fill(r,c);
        }
        static void Progress(Rect r,float value,Color c){Fill(r,new Color(.15f,.35f,.35f,.18f));if(value>0)Fill(new Rect(r.x,r.y,r.width*Mathf.Clamp01(value),r.height),c);}
        static void Icon(string name,Rect r,float opacity=1)=>RushArt.DrawIcon(name,r,opacity);
        static bool IconButton(Rect r,string name,string tip,Color color)
        {
            bool hover=r.Contains(Event.current.mousePosition);Card(r,hover?Color.Lerp(color,Color.white,.18f):color);
            float s=Mathf.Min(r.width-16,r.height-16);Icon(name,new Rect(r.center.x-s*.5f,r.center.y-s*.5f,s,s));
            if(hover)tooltip=tip;
            return GUI.Button(r,GUIContent.none,GUIStyle.none);
        }
        static void Text(Rect r,string text,int size,Color c,bool bold=false)
        {
            string key=size+"/"+bold;if(!styles.TryGetValue(key,out var style)){style=new GUIStyle{font=font,fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,alignment=TextAnchor.MiddleCenter,wordWrap=false};style.normal.textColor=Color.white;styles[key]=style;}
            Color old=GUI.color;GUI.color=c;GUI.Label(r,text,style);GUI.color=old;
        }
    }
}
