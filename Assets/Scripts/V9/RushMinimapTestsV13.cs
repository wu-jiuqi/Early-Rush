using System;
using UnityEngine;

namespace EarlyRush.V9
{
    /// <summary>Pure projection regressions; rendered pixel evidence belongs to RuntimeCheckV13.</summary>
    public static class RushMinimapTestsV13
    {
        public static int RunAll()
        {
            int count=0;
            Action<bool,string> check=(passed,message)=>{count++;if(!passed)throw new Exception("Minimap: "+message);};
            Action<Vector2,Vector2,string> near=(actual,expected,message)=>check((actual-expected).sqrMagnitude<.00001f,message+" actual="+actual+" expected="+expected);
            Rect small=new Rect(1008,18,254,184),large=new Rect(660,106,590,585);
            Rect[] bounds={new Rect(-20.5f,-10.5f,420,21),new Rect(-70.5f,-20.5f,21,420),new Rect(-82.5f,-.5f,170,332)};
            foreach(Rect map in bounds)foreach(bool expanded in new[]{false,true})
            {
                Vector2 ground=map.center;
                var p=new RushMinimapProjectionV13(map,expanded?large:small,ground,expanded);
                near(p.WorldToGui(ground),p.ContentRect.center,"center of horizontal/vertical map");
                near(p.WorldToGui(ground+Vector2.up*3)-p.WorldToGui(ground),Vector2.down*(3*p.Scale),"north points up in GUI");
                near(p.WorldToGui(ground+Vector2.right*3)-p.WorldToGui(ground),Vector2.right*(3*p.Scale),"equal horizontal and vertical scale");
                near(p.GuiToWorld(p.WorldToGui(ground+new Vector2(2,3))),ground+new Vector2(2,3),"world/GUI round trip");
                foreach(Vector2 world in new[]{ground,ground+Vector2.right*2,ground+Vector2.up*3,p.TextureSourceRect.min,p.TextureSourceRect.max})
                    near(p.TextureUvToGui(p.WorldToUv(world)),p.WorldToGui(world),"image UV and marker share exact transform");
                check(p.UvRect.xMin>=-.00001f&&p.UvRect.yMin>=-.00001f&&p.UvRect.xMax<=1.00001f&&p.UvRect.yMax<=1.00001f,"UV stays inside source image");
                if(expanded)
                {
                    near(p.WorldToGui(new Vector2(map.xMin,map.yMax)),p.DrawRect.min,"full-map top-left includes letterboxing");
                    near(p.WorldToGui(new Vector2(map.xMax,map.yMin)),p.DrawRect.max,"full-map bottom-right includes letterboxing");
                }
                else
                {
                    check(Mathf.Abs(p.SourceRect.width-42)<.0001f,"local map retains 42 world-unit width");
                    var moved=new RushMinimapProjectionV13(map,small,ground+new Vector2(3,4),false);
                    near(moved.WorldToGui(ground+new Vector2(3,4)),p.ContentRect.center,"ground movement keeps player centered");
                    near(moved.WorldToGui(ground)-p.WorldToGui(ground),new Vector2(-3,4)*p.Scale,"terrain and all markers move together");
                }
            }
            // A cell at integer world (0,0) is drawn from the center of its texel, not its edge.
            var cells=new RushMinimapProjectionV13(new Rect(-.5f,-.5f,11,51),small,Vector2.zero,false);
            near(cells.WorldToUv(Vector2.zero),new Vector2(.5f/11,.5f/51),"half-cell map bounds align texel centers");
            near(cells.TextureUvToGui(new Vector2(.5f/11,.5f/51)),cells.WorldToGui(Vector2.zero),"spawn marker matches source texel at map edge");
            check(cells.DrawRect.xMin>=cells.ContentRect.xMin&&cells.DrawRect.yMin>=cells.ContentRect.yMin&&
                cells.DrawRect.xMax<=cells.ContentRect.xMax+.001f&&cells.DrawRect.yMax<=cells.ContentRect.yMax+.001f,"cropped edge image stays inside content");

            Vector2[] directions={Vector2.up,Vector2.down,Vector2.left,Vector2.right,new Vector2(1,1),new Vector2(-2,1),new Vector2(1,-3),new Vector2(-1,-1)};
            var local=new RushMinimapProjectionV13(new Rect(-200,-200,400,400),small,new Vector2(20,30),false);
            foreach(Vector2 direction in directions)
            {
                Vector2 world=local.SourceRect.center+direction*100;
                bool outside;Vector2 edge=local.ProjectMarker(world,10,out outside),a=edge-local.ContentRect.center,b=local.WorldToGui(world)-local.ContentRect.center;
                check(outside,"distant marker is offscreen");
                check(Mathf.Abs(a.x*b.y-a.y*b.x)<.02f,"edge arrow preserves destination bearing");
                check(edge.x>=local.ContentRect.xMin+9.999f&&edge.x<=local.ContentRect.xMax-9.999f&&
                    edge.y>=local.ContentRect.yMin+9.999f&&edge.y<=local.ContentRect.yMax-9.999f,"edge arrow remains inside margin");
            }
            bool onEdge;Vector2 justInside=local.SourceRect.center+Vector2.right*(local.SourceRect.width*.5f-.01f);
            near(local.ProjectMarker(justInside,10,out onEdge),local.WorldToGui(justInside),"visible task marker is not clamped away from its true position");
            check(!onEdge,"onscreen marker does not get an offscreen arrow");

            Vector2Int[] windows={new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1920,1200),
                new Vector2Int(1024,768),new Vector2Int(2560,1080),new Vector2Int(720,1280)};
            foreach(Vector2Int window in windows)foreach(Vector2 center in new[]{local.ContentRect.center,new Vector2(740,400)})
            {
                var canvas=RushMinimapProjectionV13.GuiMatrix(window.x,window.y);
                Vector2 expected=RushMinimapProjectionV13.GuiToScreenPixel(center,window.x,window.y);
                near(canvas.MultiplyPoint3x4(center),expected,"GUI matrix agrees with screenshot pixel projection");
                near(RushMinimapProjectionV13.ScreenPixelToGui(expected,window.x,window.y),center,"window/GUI round trip includes letterbox offsets");
                foreach(Vector2 direction in directions)
                {
                    var rotated=canvas*RushMinimapProjectionV13.RotationAbout(center,direction);
                    near(rotated.MultiplyPoint3x4(center),expected,"rotating marker keeps center fixed for every window and facing");
                    Vector2 tip=rotated.MultiplyPoint3x4(center+Vector2.down*8);
                    near((tip-expected).normalized,new Vector2(direction.x,-direction.y).normalized,"marker points in correct screen direction");
                }
            }
            // Detect the former left-multiplied pivot bug, hidden by 1280x720 at 1:1 scale.
            var scaled=RushMinimapProjectionV13.GuiMatrix(1920,1080);Vector2 pivot=local.ContentRect.center;
            Vector2 legacy=(RushMinimapProjectionV13.RotationAbout(pivot,Vector2.right)*scaled).MultiplyPoint3x4(pivot);
            check(Vector2.Distance(legacy,RushMinimapProjectionV13.GuiToScreenPixel(pivot,1920,1080))>100,"non-default resolution reproduces old rotation-center drift");
            return count;
        }
    }
}
