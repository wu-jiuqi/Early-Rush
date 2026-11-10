using System;
using UnityEngine;

namespace EarlyRush.V9
{
    /// <summary>One north-up transform for the map image and every marker. GUI coordinates use a top-left origin.</summary>
    public readonly struct RushMinimapProjectionV13
    {
        public Rect WorldBounds {get;}
        public Rect ContentRect {get;}
        public Rect SourceRect {get;}
        public Rect TextureSourceRect {get;}
        public Rect DrawRect {get;}
        public Rect UvRect {get;}
        public float Scale {get;}

        public RushMinimapProjectionV13(Rect worldBounds,Rect panel,Vector2 playerGround,bool expanded)
        {
            if(worldBounds.width<=0||worldBounds.height<=0||panel.width<=20||panel.height<=32)
                throw new ArgumentException("Minimap bounds and panel must have positive content area");
            WorldBounds=worldBounds;
            ContentRect=new Rect(panel.x+10,panel.y+10,panel.width-20,panel.height-32);
            Scale=expanded?Mathf.Min(ContentRect.width/worldBounds.width,ContentRect.height/worldBounds.height):ContentRect.width/42f;
            Vector2 center=expanded?worldBounds.center:playerGround;
            Vector2 size=ContentRect.size/Scale;
            SourceRect=new Rect(center-size*.5f,size);
            float x0=Mathf.Max(SourceRect.xMin,worldBounds.xMin),y0=Mathf.Max(SourceRect.yMin,worldBounds.yMin);
            float x1=Mathf.Max(x0,Mathf.Min(SourceRect.xMax,worldBounds.xMax)),y1=Mathf.Max(y0,Mathf.Min(SourceRect.yMax,worldBounds.yMax));
            TextureSourceRect=Rect.MinMaxRect(x0,y0,x1,y1);
            UvRect=new Rect((x0-worldBounds.xMin)/worldBounds.width,(y0-worldBounds.yMin)/worldBounds.height,
                (x1-x0)/worldBounds.width,(y1-y0)/worldBounds.height);
            DrawRect=new Rect(ContentRect.x+(x0-SourceRect.xMin)*Scale,ContentRect.y+(SourceRect.yMax-y1)*Scale,
                (x1-x0)*Scale,(y1-y0)*Scale);
        }

        public Vector2 WorldToGui(Vector2 world)=>new Vector2(ContentRect.x+(world.x-SourceRect.xMin)*Scale,
            ContentRect.y+(SourceRect.yMax-world.y)*Scale);
        public Vector2 GuiToWorld(Vector2 gui)=>new Vector2(SourceRect.xMin+(gui.x-ContentRect.x)/Scale,
            SourceRect.yMax-(gui.y-ContentRect.y)/Scale);
        public Vector2 WorldToUv(Vector2 world)=>new Vector2((world.x-WorldBounds.xMin)/WorldBounds.width,
            (world.y-WorldBounds.yMin)/WorldBounds.height);
        public Vector2 TextureUvToGui(Vector2 uv)=>new Vector2(DrawRect.x+(uv.x-UvRect.x)/UvRect.width*DrawRect.width,
            DrawRect.y+(UvRect.yMax-uv.y)/UvRect.height*DrawRect.height);

        public Vector2 ProjectMarker(Vector2 world,float padding,out bool offscreen)
        {
            Vector2 raw=WorldToGui(world);
            offscreen=raw.x<ContentRect.xMin||raw.x>ContentRect.xMax||raw.y<ContentRect.yMin||raw.y>ContentRect.yMax;
            if(!offscreen)return raw;
            // Intersect a ray from the view center with the inset rectangle. Independent X/Y
            // clamping puts diagonal destinations on the wrong bearing along the border.
            Vector2 delta=raw-ContentRect.center;
            float halfWidth=Mathf.Max(1,ContentRect.width*.5f-padding),halfHeight=Mathf.Max(1,ContentRect.height*.5f-padding);
            float tx=Mathf.Abs(delta.x)>.00001f?halfWidth/Mathf.Abs(delta.x):float.PositiveInfinity;
            float ty=Mathf.Abs(delta.y)>.00001f?halfHeight/Mathf.Abs(delta.y):float.PositiveInfinity;
            return ContentRect.center+delta*Mathf.Min(tx,ty);
        }

        public static float GuiScale(int screenWidth,int screenHeight)=>Mathf.Min(screenWidth/1280f,screenHeight/720f);
        public static Vector2 GuiOffset(int screenWidth,int screenHeight)
        {float scale=GuiScale(screenWidth,screenHeight);return new Vector2((screenWidth-1280*scale)*.5f,(screenHeight-720*scale)*.5f);}
        public static Vector2 GuiToScreenPixel(Vector2 point,int screenWidth,int screenHeight)=>
            GuiOffset(screenWidth,screenHeight)+point*GuiScale(screenWidth,screenHeight);
        public static Vector2 ScreenPixelToGui(Vector2 point,int screenWidth,int screenHeight)=>
            (point-GuiOffset(screenWidth,screenHeight))/GuiScale(screenWidth,screenHeight);
        public static Matrix4x4 GuiMatrix(int screenWidth,int screenHeight)
        {
            float scale=GuiScale(screenWidth,screenHeight);Vector2 offset=GuiOffset(screenWidth,screenHeight);
            var matrix=Matrix4x4.identity;matrix.m00=matrix.m11=scale;matrix.m03=offset.x;matrix.m13=offset.y;return matrix;
        }
        public static Matrix4x4 RotationAbout(Vector2 center,Vector2 worldDirection)
        {
            float angle=worldDirection.sqrMagnitude>.00001f?Mathf.Atan2(worldDirection.x,worldDirection.y):0;
            float c=Mathf.Cos(angle),s=Mathf.Sin(angle);var matrix=Matrix4x4.identity;
            matrix.m00=c;matrix.m01=-s;matrix.m10=s;matrix.m11=c;
            matrix.m03=center.x-c*center.x+s*center.y;matrix.m13=center.y-s*center.x-c*center.y;
            return matrix;
        }
    }
}
