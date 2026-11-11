using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EarlyRush.V9;

namespace EarlyRush.V15
{
    /// <summary>World anchors use ground positions; hidden M1 passengers never receive task cues.</summary>
    public sealed class RushWorldHintsV15
    {
        sealed class Hint
        {
            public FixtureV9 fixture;public RectTransform root;public Image icon,goal,status;
            public Image beatTrack,beatWindow,beatPointer;public bool priorBoard;public float flipAge=1;
        }
        readonly RushUIV15 owner;readonly RectTransform root;
        readonly List<Hint> hints=new List<Hint>();readonly List<RectTransform> coffee=new List<RectTransform>();
        RectTransform signal,tell;Image signalGlyph,tellArrow;Text signalStop;
        RunV9 bound;
        public RushWorldHintsV15(RushUIV15 owner,RectTransform parent)
        {
            this.owner=owner;root=U15.Rect(parent,"World task hints",new Rect(0,0,1280,720));
            root.SetAsFirstSibling();
            signal=U15.Rect(root,"Crosswalk signal",new Rect(0,0,44,56));
            U15.Image(signal,"Plate","component.badge_cream",new Rect(0,0,44,44));
            signalGlyph=U15.Image(signal,"Go","stage_street",new Rect(5,5,34,34));
            signalStop=U15.Text(signal,"Stop","━",new Rect(0,0,44,44),28,color:U15.Coral);
            tell=U15.Rect(root,"Commuter charge direction",new Rect(0,0,40,40));tellArrow=U15.Image(tell,"Arrow","offscreen_arrow",new Rect(0,0,40,40),U15.Coral);
            for(int i=0;i<8;i++)
            {
                var rt=U15.Rect(root,"Coffee caution "+i,new Rect(0,0,32,32));U15.Image(rt,"Cup","task_o1_coffee",new Rect(0,0,32,32));coffee.Add(rt);
            }
        }
        void Bind(RunV9 run)
        {
            foreach(var hint in hints){hint.root.gameObject.SetActive(false);Object.Destroy(hint.root.gameObject);}hints.Clear();bound=run;
            foreach(var fixture in run.fixtures)
            {
                if(fixture.kind==FixtureKind.ChairSocket)continue;
                var rt=U15.Rect(root,"Fixture hint "+fixture.id,new Rect(0,0,52,68));
                U15.Image(rt,"Pin","component.pin_cream",new Rect(0,0,52,65));
                string icon=fixture.kind==FixtureKind.Breakfast?"task_s1_breakfast":fixture.kind==FixtureKind.Repair?"task_s2_wheel_repair":fixture.kind==FixtureKind.Capsule?"task_m2_capsule":fixture.kind==FixtureKind.FileDesk?"portrait_"+fixture.index.ToString("00"):fixture.kind==FixtureKind.FilePickup?"task_o2_document":"sun";
                var h=new Hint{fixture=fixture,root=rt,icon=U15.Image(rt,"Object",icon,new Rect(9,7,34,34)),status=U15.Image(rt,"Done","check",new Rect(34,27,23,23))};
                h.status.gameObject.SetActive(false);
                if(fixture.kind==FixtureKind.FlipBoard)
                {
                    h.priorBoard=run.boards[fixture.index];
                    h.icon.rectTransform.pivot=new Vector2(.5f,.5f);h.icon.rectTransform.anchoredPosition=new Vector2(26,-24);
                    h.goal=U15.Image(rt,"Target",run.boardGoal[fixture.index]?"sun":"moon",new Rect(17,-23,23,23));
                }
                if(fixture.kind==FixtureKind.Capsule)
                {
                    h.beatTrack=U15.Image(rt,"Beat track","component.progress_track",new Rect(-31,-20,114,8),U15.Ink);
                    h.beatWindow=U15.Image(rt,"Beat window","component.white_pixel",new Rect(0,0,1,8),U15.Gold);h.beatWindow.preserveAspect=false;
                    h.beatPointer=U15.Image(rt,"Beat pointer","component.white_pixel",new Rect(0,0,3,14),U15.Teal);h.beatPointer.preserveAspect=false;
                }
                hints.Add(h);
            }
        }
        static bool Visible(Vector2 p)=>p.x>12&&p.x<1268&&p.y>80&&p.y<710;
        public void Refresh(RunV9 run,float dt)
        {
            if(!ReferenceEquals(bound,run))Bind(run);
            root.gameObject.SetActive(run.phase==RunPhase.Playing||run.phase==RunPhase.Paused);
            if(!root.gameObject.activeSelf)return;
            foreach(var h in hints)
            {
                Vector2 p=owner.WorldToUI(h.fixture.position);bool visible=Visible(p);
                h.root.gameObject.SetActive(visible);if(!visible)continue;
                U15.SetRect(h.root,new Rect(p.x-26,p.y-70,52,68));
                if(h.fixture.kind==FixtureKind.FileDesk)h.status.gameObject.SetActive(run.IsDelivered(h.fixture.index));
                if(h.fixture.kind==FixtureKind.FlipBoard)
                {
                    bool state=run.boards[h.fixture.index];if(state!=h.priorBoard){h.priorBoard=state;h.flipAge=0;}
                    h.flipAge+=dt;
                    float scale=owner.ReducedMotion?1:Mathf.Max(.08f,Mathf.Abs(1-Mathf.Clamp01(h.flipAge/.18f)*2));
                    h.icon.rectTransform.localScale=new Vector3(scale,1,1);
                    if(owner.ReducedMotion||h.flipAge>=.09f)h.icon.sprite=U15.Sprite(state?"sun":"moon");
                    h.goal.sprite=U15.Sprite(run.boardGoal[h.fixture.index]?"sun":"moon");
                }
                if(h.beatTrack)
                {
                    bool near=Vector2.Distance(run.player,h.fixture.position)<5;
                    h.beatTrack.gameObject.SetActive(near);h.beatWindow.gameObject.SetActive(near);h.beatPointer.gameObject.SetActive(near);
                    float width=114*run.config.beatWindow/run.config.beatPeriod;
                    U15.SetRect(h.beatWindow.rectTransform,new Rect(-31+(114-width)*.5f,-20,width,8));
                    U15.SetRect(h.beatPointer.rectTransform,new Rect(-31+114*run.BeatPhase-1.5f,-23,3,14));
                }
            }
            signal.gameObject.SetActive(run.stage==0);
            if(run.stage==0)
            {
                Vector2 p=owner.WorldToUI(run.map.signal);signal.gameObject.SetActive(Visible(p));U15.SetRect(signal,new Rect(p.x-22,p.y-66,44,56));
                signalGlyph.gameObject.SetActive(run.SignalGreen);signalStop.gameObject.SetActive(!run.SignalGreen);
            }
            tell.gameObject.SetActive(false);int cups=0;
            foreach(var person in run.people)
            {
                if(person.exited)continue;
                bool showCoffee=person.kind==PersonKind.Coffee&&!person.spilled&&cups<coffee.Count;
                bool showCharge=person.kind==PersonKind.Ordinary&&!person.dashAirborne&&(person.actionPhase==NpcActionPhase.Windup||person.actionPhase==NpcActionPhase.Charging)&&!tell.gameObject.activeSelf;
                if(!showCoffee&&!showCharge)continue;
                Vector2 p=owner.WorldToUI(person.position);if(!Visible(p))continue;
                if(showCoffee){var rt=coffee[cups++];rt.gameObject.SetActive(true);U15.SetRect(rt,new Rect(p.x-16,p.y-47,32,32));}
                if(showCharge)
                {
                    Vector2 aim=owner.WorldToUI(person.position+person.chargeDirection*1.6f);tell.gameObject.SetActive(true);U15.SetRect(tell,new Rect(aim.x-20,aim.y-20,40,40));
                    tellArrow.rectTransform.pivot=new Vector2(.5f,.5f);tellArrow.rectTransform.anchoredPosition=new Vector2(20,-20);
                    tellArrow.rectTransform.localEulerAngles=new Vector3(0,0,-Mathf.Atan2(person.chargeDirection.x,person.chargeDirection.y)*Mathf.Rad2Deg);
                }
            }
            for(int i=cups;i<coffee.Count;i++)coffee[i].gameObject.SetActive(false);
        }
    }
}
