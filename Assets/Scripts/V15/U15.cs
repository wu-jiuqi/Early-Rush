using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using EarlyRush.UIKit;

namespace EarlyRush.V15
{
    /// <summary>Shared sprite catalogue and top-left, 1280x720 UI construction helpers.</summary>
    public static class U15
    {
        [Serializable] sealed class Catalogue { public Entry[] assets; }
        [Serializable] sealed class Entry { public string id, path; }
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static bool loaded;
        public static Font Font;
        public static RushUIV15 Owner;
        public static readonly Color Ink = new Color32(22,67,74,255), Teal = new Color32(8,127,128,255),
            Cream = new Color32(255,244,220,255), Gold = new Color32(255,211,78,255), Coral = new Color32(232,112,83,255);
        public static int LoadedCount { get { Load(); return sprites.Count; } }
        static void Load()
        {
            if (loaded) return;
            var json = Resources.Load<TextAsset>("UIV15/manifest");
            if (!json) throw new InvalidOperationException("UIV15 sprite catalogue is missing");
            var catalogue = JsonUtility.FromJson<Catalogue>(json.text);
            foreach (var entry in catalogue.assets)
            {
                string path = "UIV15/" + entry.path.Substring("Sprites/".Length);
                path = path.Substring(0,path.Length-4);
                var sprite = Resources.Load<Sprite>(path);
                if (!sprite) throw new InvalidOperationException("UIV15 sprite missing or not imported as Sprite: " + entry.id + " / " + path);
                sprites.Add(entry.id,sprite);
            }
            loaded=true;
        }
        public static Sprite Sprite(string id)
        {
            Load(); if (id=="action_walk") id="stage_street";
            if (!sprites.TryGetValue(id,out var value)) throw new ArgumentException("Unknown UIV15 asset: "+id);
            return value;
        }
        public static string TaskIcon(string id)
        {
            switch(id) {
                case "S1":return "task_s1_breakfast"; case "S2":return "task_s2_wheel_repair"; case "S3":return "task_s3_flip";
                case "M1":return "task_m1_alighting"; case "M2":return "task_m2_capsule";case "M3":return "task_m3_clean";
                case "O1":return "task_o1_coffee"; case "O2":return "task_o2_document";case "O3":return "task_o3_chair";
                default:return "task_m1_neutral";
            }
        }
        public static RectTransform Rect(Transform parent,string name,Rect bounds)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();SetRect(rt,bounds);return rt;
        }
        public static void SetRect(RectTransform rt,Rect bounds)
        {
            rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);
            rt.anchoredPosition=new Vector2(bounds.x,-bounds.y);rt.sizeDelta=bounds.size;
        }
        public static Image Image(Transform parent,string name,string id,Rect bounds,Color? color=null)
        {
            var rt=Rect(parent,name,bounds);var image=rt.gameObject.AddComponent<Image>();
            image.sprite=Sprite(id);image.color=color??Color.white;image.raycastTarget=false;
            image.type=image.sprite.border.sqrMagnitude>0?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect=image.type==UnityEngine.UI.Image.Type.Simple;return image;
        }
        public static Text Text(Transform parent,string name,string value,Rect bounds,int size=20,TextAnchor align=TextAnchor.MiddleCenter,Color? color=null)
        {
            var rt=Rect(parent,name,bounds);var text=rt.gameObject.AddComponent<Text>();
            text.font=Font?Font:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=size;
            text.text=value;text.alignment=align;text.color=color??Ink;text.raycastTarget=false;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
            return text;
        }
        public static Button Button(Transform parent,string name,string icon,Rect bounds,Action action,string tooltip=null,bool primary=false)
        {
            var rt=Rect(parent,name,bounds);
            var hit=rt.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
            var visual=Rect(rt,"Visual",new Rect(0,0,bounds.width,bounds.height));
            // Centered visual pivot allows a press tween without moving the button's hit rectangle.
            visual.pivot=new Vector2(.5f,.5f);visual.anchoredPosition=new Vector2(bounds.width*.5f,-bounds.height*.5f);
            var plate=Image(visual,"Plate",primary?"component.button_teal":"component.button_cream",new Rect(0,0,bounds.width,bounds.height));
            if(primary){if(icon=="play")icon="component.glyph_play_cream";else if(icon=="next")icon="component.glyph_next_cream";else if(icon=="retry")icon="component.glyph_retry_cream";}
            float side=Mathf.Min(bounds.height-12,bounds.width-12);
            if(!string.IsNullOrEmpty(icon))Image(visual,"Icon",icon,new Rect((bounds.width-side)*.5f,(bounds.height-side)*.5f,side,side));
            var b=rt.gameObject.AddComponent<Button>();b.targetGraphic=plate;
            var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.98f,.82f);colors.pressedColor=new Color(.8f,.94f,.9f);colors.selectedColor=new Color(1,.96f,.72f);colors.disabledColor=new Color(.65f,.69f,.67f,.5f);b.colors=colors;
            b.onClick.AddListener(()=>{if(b.interactable)action?.Invoke();});
            var tween=rt.gameObject.AddComponent<EarlyRushUITween>();
            // The helper's serialized visual defaults to itself; binding to the centered child preserves layout.
            tween.SetVisualRoot(visual);tween.enabled=Owner==null||!Owner.ReducedMotion;
            if(!string.IsNullOrEmpty(tooltip))Tooltip(rt.gameObject,tooltip);
            return b;
        }
        public static void Tooltip(GameObject target,string message)
        { var tip=target.AddComponent<RushTooltipV15>();tip.Message=message; }
    }
    public sealed class RushTooltipV15 : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IDeselectHandler
    {
        public string Message;
        public void OnPointerEnter(PointerEventData e){if(U15.Owner)U15.Owner.SetTooltip(Message);}
        public void OnPointerExit(PointerEventData e){if(U15.Owner)U15.Owner.SetTooltip(null);}
        public void OnDeselect(BaseEventData e){if(U15.Owner)U15.Owner.SetTooltip(null);}
        void OnDisable(){if(U15.Owner)U15.Owner.SetTooltip(null);}
    }
}
