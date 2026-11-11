using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using EarlyRush.V9;
using EarlyRush.UIKit;

namespace EarlyRush.V15
{
    [DefaultExecutionOrder(100)]
    public sealed class RushUIV15 : MonoBehaviour
    {
        public RushPlayV9 Host {get;private set;}
        public RushMenusV15 Menus {get;private set;}
        public RushHudV15 Hud {get;private set;}
        public RectTransform Root {get;private set;}
        public bool HelpVisible=>Menus!=null&&Menus.HasOverlay;
        public int UILoadedAssetCount=>U15.LoadedCount;
        Canvas canvas;GameObject ownedEventSystem;RushWorldHintsV15 hints;CanvasGroup hudInput,menuFxVisibility;
        RectTransform fxRoot,menuFxRoot,tooltip;Text tooltipText;
        RunV9 lastRun;double lastElapsed;bool muted,reducedMotion,focused=true;
        float volume;
        readonly List<EarlyRushUISequence> fx=new List<EarlyRushUISequence>();
        public bool Muted=>muted;
        public float Volume {get=>volume;set{volume=Mathf.Clamp01(value);PlayerPrefs.SetFloat("V15_UiVolume",volume);ApplyVolume();}}
        public bool ReducedMotion {get=>reducedMotion;set{reducedMotion=value;PlayerPrefs.SetInt("V15_ReducedMotion",value?1:0);if(canvas)foreach(var t in canvas.GetComponentsInChildren<EarlyRushUITween>(true))t.enabled=!value;}}
        public void ToggleSound(){muted=!muted;PlayerPrefs.SetInt("V15_UiMuted",muted?1:0);ApplyVolume();}
        void ApplyVolume(){AudioListener.volume=muted?0:volume;}
        public void Initialize(RushPlayV9 host)
        {
            Host=host;U15.Owner=this;U15.Font=host.UIFont;
            muted=PlayerPrefs.GetInt("V15_UiMuted",0)!=0;volume=PlayerPrefs.GetFloat("V15_UiVolume",.8f);reducedMotion=PlayerPrefs.GetInt("V15_ReducedMotion",0)!=0;ApplyVolume();
            var go=new GameObject("Early Rush V15 Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var rootGo=new GameObject("Safe UI 1280x720",typeof(RectTransform));Root=rootGo.GetComponent<RectTransform>();Root.SetParent(go.transform,false);Root.anchorMin=Root.anchorMax=Root.pivot=new Vector2(.5f,.5f);Root.sizeDelta=new Vector2(1280,720);Root.anchoredPosition=Vector2.zero;
            if(!FindFirstObjectByType<EventSystem>()){ownedEventSystem=new GameObject("V15 Event System",typeof(EventSystem),typeof(StandaloneInputModule));ownedEventSystem.transform.SetParent(transform,false);}
            Hud=new RushHudV15(this,Root);hints=new RushWorldHintsV15(this,Root);
            fxRoot=U15.Rect(Root,"UI Effects",new Rect(0,0,1280,720));
            Menus=new RushMenusV15(this,Root);
            hudInput=Hud.Root.gameObject.AddComponent<CanvasGroup>();
            menuFxRoot=U15.Rect(Root,"Menu Effects",new Rect(0,0,1280,720));
            menuFxVisibility=menuFxRoot.gameObject.AddComponent<CanvasGroup>();menuFxVisibility.blocksRaycasts=false;
            tooltip=U15.Rect(Root,"Tooltip",new Rect(350,667,580,42));U15.Image(tooltip,"Plate","component.tooltip_cream",new Rect(0,0,580,42));tooltipText=U15.Text(tooltip,"Hint","",new Rect(12,4,556,34),17);tooltip.gameObject.SetActive(false);
            Debug.Log("V15_UI_READY assets="+UILoadedAssetCount);RefreshNow();
        }
        void LateUpdate(){if(Host)RefreshNow();}
        public void RefreshNow()
        {
            if(!Host||Host.run==null||Menus==null)return;
            var run=Host.run;float gameDt=0;
            if(!ReferenceEquals(run,lastRun))
            {
                lastRun=run;lastElapsed=run.elapsed;foreach(var sequence in fx)if(sequence){sequence.Cancel();sequence.gameObject.SetActive(false);}SetTooltip(null);
            }
            else if(run.phase==RunPhase.Playing&&focused)gameDt=(float)Math.Max(0,run.elapsed-lastElapsed);
            lastElapsed=run.elapsed;
            Hud.Root.gameObject.SetActive(run.phase!=RunPhase.Ready);Hud.Refresh(run,gameDt);
            hints.Refresh(run,gameDt);Menus.Refresh(run,focused?Time.unscaledDeltaTime:0);
            hudInput.interactable=hudInput.blocksRaycasts=run.phase==RunPhase.Playing&&!Menus.HasOverlay;
            if(EventSystem.current)EventSystem.current.sendNavigationEvents=run.phase!=RunPhase.Playing||Menus.HasOverlay;
            // Gameplay effects stay below modal pages. Only menu effects and hints sit above menus.
            if(tooltip.GetSiblingIndex()!=Root.childCount-1)tooltip.SetAsLastSibling();
            if(menuFxRoot.GetSiblingIndex()!=Root.childCount-2)menuFxRoot.SetSiblingIndex(Root.childCount-2);
            menuFxVisibility.alpha=Menus.HasOverlay?0:1;
            foreach(var sequence in fx)if(sequence&&sequence.gameObject.activeSelf)
            {
                if(sequence.Clock==EarlyRushUISequence.ClockMode.Manual&&run.phase==RunPhase.Results)
                {sequence.Cancel();sequence.gameObject.SetActive(false);continue;}
                sequence.SetPaused(!focused||(sequence.Clock==EarlyRushUISequence.ClockMode.Manual&&run.phase!=RunPhase.Playing));
                if(sequence.Clock==EarlyRushUISequence.ClockMode.Manual)sequence.Advance(gameDt);
            }
        }
        public void ToggleHelp()=>Menus.ToggleHelp();
        public void ShowHelp(){if(!Menus.HasOverlay)Menus.ToggleHelp();}
        public void CloseHelp()=>Menus.CloseOverlay();
        public void ShowTaskHelp(string taskId)=>Menus.ShowTaskHelp(taskId);
        public void ShowTutorial(string kind)=>Menus.ShowTutorial(kind);
        public bool HandleEscape(){return Menus!=null&&Menus.CloseOverlayIfOpen();}
        public void SetTooltip(string value)
        {if(!tooltip)return;tooltip.gameObject.SetActive(!string.IsNullOrEmpty(value));tooltipText.text=value??"";}
        public Button FindButton(string name)
        {if(!canvas)return null;Button found=null;foreach(var b in canvas.GetComponentsInChildren<Button>(true))if(b.name==name&&b.gameObject.activeInHierarchy)found=b;return found;}
        public Vector2 WorldToUI(Vector2 world)
        {
            Vector3 pixel=Host.GameCamera.WorldToScreenPoint(new Vector3(world.x,world.y,0));
            return RushMinimapProjectionV13.ScreenPixelToGui(new Vector2(pixel.x,Screen.height-pixel.y),Screen.width,Screen.height);
        }
        public void PlayFx(string key,Vector2 position,bool menu=false)
        {
            if(reducedMotion||!fxRoot)return;
            EarlyRushUISequence sequence=null;foreach(var item in fx)if(item&&!item.gameObject.activeSelf){sequence=item;break;}
            if(!sequence)
            {
                if(fx.Count>=12)return;
                var image=U15.Image(fxRoot,"FX "+key,key+".frame_00",new Rect(position.x-64,position.y-64,128,128));
                sequence=image.gameObject.AddComponent<EarlyRushUISequence>();fx.Add(sequence);
                var captured=sequence;sequence.Completed+=()=>{if(captured)captured.gameObject.SetActive(false);};
            }
            sequence.transform.SetParent(menu?menuFxRoot:fxRoot,false);
            sequence.gameObject.SetActive(true);U15.SetRect(sequence.GetComponent<RectTransform>(),new Rect(position.x-64,position.y-64,128,128));
            sequence.Clock=menu?EarlyRushUISequence.ClockMode.Unscaled:EarlyRushUISequence.ClockMode.Manual;
            var frames=new Sprite[6];for(int i=0;i<6;i++)frames[i]=U15.Sprite(key+".frame_"+i.ToString("00"));
            sequence.Play(frames,key=="impact_puff"?18:12,false);
        }
        void OnApplicationFocus(bool value){focused=value;}
        void OnDestroy(){Menus?.Dispose();if(U15.Owner==this)U15.Owner=null;if(canvas)Destroy(canvas.gameObject);if(ownedEventSystem)Destroy(ownedEventSystem);PlayerPrefs.Save();}
    }
}
