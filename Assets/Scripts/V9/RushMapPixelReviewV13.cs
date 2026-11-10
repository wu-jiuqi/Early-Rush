using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush.V9
{
    /// <summary>Screenshot-only QA. No drawing, simulation changes, or production projection calls.</summary>
    public static class RushMapPixelReviewV13
    {
        [Serializable] public sealed class Result
        {
            public bool arrowPassed,terrainChecked,terrainPassed;
            public int goldPixels,roadSamples,roadMatches,emptySamples,emptyMatches;
            public float centerError,directionDot,centroidBias;
            public Vector2 expectedCenter,observedCenter,goldCentroid;
            public string ArrowSummary()=>"pixels="+goldPixels+" centerError="+centerError.ToString("F2")+
                " directionDot="+directionDot.ToString("F2")+" centroidBias="+centroidBias.ToString("F2");
            public string TerrainSummary()=>"road="+roadMatches+"/"+roadSamples+" empty="+emptyMatches+"/"+emptySamples+
                " (cell interiors, icon masks, same-scale independent layout)";
        }

        struct Layout
        {
            public Rect content;public Vector2 worldCenter,screenOffset;public float worldScale,guiScale;
            public Vector2 ToGui(Vector2 world)=>content.center+new Vector2(world.x-worldCenter.x,worldCenter.y-world.y)*worldScale;
            public Vector2 ToWorld(Vector2 gui)=>worldCenter+new Vector2(gui.x-content.center.x,content.center.y-gui.y)/worldScale;
            public Vector2 ToScreen(Vector2 gui)=>screenOffset+gui*guiScale;
        }

        // This intentionally derives bounds from occupied cells and the documented 42-unit
        // local view instead of reading SourceRect/WorldToGui from the production helper.
        static Layout IndependentLayout(Texture2D image,RunV9 run,Rect panel,bool expanded)
        {
            int x0=int.MaxValue,y0=int.MaxValue,x1=int.MinValue,y1=int.MinValue;
            foreach(var cell in run.map.cells){x0=Math.Min(x0,cell.x);y0=Math.Min(y0,cell.y);x1=Math.Max(x1,cell.x);y1=Math.Max(y1,cell.y);}
            if(x0>x1)throw new ArgumentException("Map has no occupied cells");
            Rect content=new Rect(panel.x+10,panel.y+10,panel.width-20,panel.height-32);
            float guiScale=Mathf.Min(image.width/1280f,image.height/720f);
            return new Layout{content=content,worldCenter=expanded?new Vector2((x0+x1)*.5f,(y0+y1)*.5f):run.player,
                worldScale=expanded?Mathf.Min(content.width/(x1-x0+1),content.height/(y1-y0+1)):content.width/42f,
                guiScale=guiScale,screenOffset=new Vector2((image.width-1280*guiScale)*.5f,(image.height-720*guiScale)*.5f)};
        }

        /// <param name="includeTerrain">Road landmarks need checking once per layout, not eight times for eight facings.</param>
        public static Result Review(Texture2D image,RunV9 run,Rect panel,bool expanded,bool includeTerrain=true)
        {
            if(!image||run==null)throw new ArgumentNullException("Screenshot and simulation are required");
            Layout layout=IndependentLayout(image,run,panel,expanded);
            var result=new Result{expectedCenter=layout.ToScreen(layout.ToGui(run.player))};
            ReviewArrow(image,run.facing,layout,result);
            if(includeTerrain)ReviewTerrain(image,run,layout,result);
            return result;
        }

        static bool Gold(Color color)=>color.r>.88f&&color.g>.63f&&color.g<.93f&&color.b<.34f;
        static Color TopPixel(Texture2D image,int x,int y)=>image.GetPixel(x,image.height-1-y);
        static void ReviewArrow(Texture2D image,Vector2 facing,Layout layout,Result result)
        {
            int radius=Mathf.CeilToInt(13*layout.guiScale)+2;
            int left=Mathf.RoundToInt(result.expectedCenter.x)-radius,top=Mathf.RoundToInt(result.expectedCenter.y)-radius,width=radius*2+1;
            var mask=new bool[width*width];int seed=-1;float nearest=float.MaxValue;
            for(int y=0;y<width;y++)for(int x=0;x<width;x++)
            {
                int px=left+x,py=top+y;if(px<0||py<0||px>=image.width||py>=image.height)continue;
                if(!Gold(TopPixel(image,px,py)))continue;
                int index=y*width+x;mask[index]=true;
                float distance=(new Vector2(px+.5f,py+.5f)-result.expectedCenter).sqrMagnitude;
                if(distance<nearest){nearest=distance;seed=index;}
            }
            if(seed<0||nearest>Mathf.Pow(4*layout.guiScale+1,2))return;
            // Keep only the connected player-arrow component; nearby gold task decorations
            // are not allowed to count as part of the arrow's center or facing.
            var queue=new Queue<int>();queue.Enqueue(seed);mask[seed]=false;
            Vector2 direction=new Vector2(facing.x,-facing.y).normalized,normal=new Vector2(-direction.y,direction.x),sum=Vector2.zero;
            float amin=float.MaxValue,amax=float.MinValue,bmin=float.MaxValue,bmax=float.MinValue;
            while(queue.Count>0)
            {
                int index=queue.Dequeue(),x=index%width,y=index/width;
                Vector2 p=new Vector2(left+x+.5f,top+y+.5f);sum+=p;result.goldPixels++;
                Vector2 delta=p-result.expectedCenter;float a=Vector2.Dot(delta,direction),b=Vector2.Dot(delta,normal);
                amin=Mathf.Min(amin,a);amax=Mathf.Max(amax,a);bmin=Mathf.Min(bmin,b);bmax=Mathf.Max(bmax,b);
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=width||ny>=width)continue;
                    int next=ny*width+nx;if(!mask[next])continue;mask[next]=false;queue.Enqueue(next);
                }
            }
            result.goldCentroid=sum/result.goldPixels;
            result.observedCenter=result.expectedCenter+direction*((amin+amax)*.5f)+normal*((bmin+bmax)*.5f);
            result.centerError=Vector2.Distance(result.observedCenter,result.expectedCenter);
            // A filled triangle's centroid sits towards its wide base. The vector back from
            // that centroid to the oriented bounding center therefore points towards its tip.
            Vector2 tip=result.observedCenter-result.goldCentroid;result.centroidBias=tip.magnitude;
            result.directionDot=Vector2.Dot(tip.normalized,direction);
            result.arrowPassed=result.goldPixels>=12*layout.guiScale*layout.guiScale&&result.goldPixels<=320*layout.guiScale*layout.guiScale&&
                result.centerError<=2*layout.guiScale+1&&result.centroidBias>=.55f*layout.guiScale&&result.directionDot>=.8f;
        }

        static void ReviewTerrain(Texture2D image,RunV9 run,Layout layout,Result result)
        {
            result.terrainChecked=true;var excluded=new List<Rect>();
            AddMask(excluded,layout.ToGui(run.player),15);
            foreach(var task in run.tasks)
                if(!(run.stage==1&&task.id=="M1"))MaskMarker(excluded,layout,task.position,10,16,12);
            MaskMarker(excluded,layout,run.map.exit,10,16,12);
            // Document portraits identify approximate regions rather than exact desk cells.
            if(run.stage==2&&run.tasks[1].state==TaskState.Active)
                foreach(var fixture in run.fixtures)if(fixture.kind==FixtureKind.FileDesk&&!run.IsDelivered(fixture.index))
                    MaskMarker(excluded,layout,new Vector2(Mathf.Round(fixture.position.x/8)*8,Mathf.Round(fixture.position.y/8)*8),14,20,16);
            float edgeWorld=1.75f/(layout.worldScale*layout.guiScale);
            // A coprime order spreads the bounded sample budget across the entire view.
            for(int i=0;i<713&&(result.roadSamples<32||result.emptySamples<32);i++)
            {
                int index=i*137%713,col=index%31,row=index/31;
                Vector2 gui=new Vector2(layout.content.xMin+6+(layout.content.width-12)*(col+.5f)/31,
                    layout.content.yMin+6+(layout.content.height-12)*(row+.5f)/23);
                bool masked=false;foreach(var rect in excluded)if(rect.Contains(gui)){masked=true;break;}if(masked)continue;
                Vector2 world=layout.ToWorld(gui);bool road=run.map.cells.Contains(MapData.Cell(world));
                if(road?result.roadSamples>=32:result.emptySamples>=32)continue;
                // Reject antialiased/sampled terrain boundaries. All cells touched by a
                // ~two-pixel margin must have the same occupancy as the selected landmark.
                bool interior=true;
                int x0=Mathf.FloorToInt(world.x-edgeWorld+.5f),x1=Mathf.FloorToInt(world.x+edgeWorld+.5f);
                int y0=Mathf.FloorToInt(world.y-edgeWorld+.5f),y1=Mathf.FloorToInt(world.y+edgeWorld+.5f);
                for(int y=y0;y<=y1&&interior;y++)for(int x=x0;x<=x1;x++)if(run.map.cells.Contains(new Vector2Int(x,y))!=road){interior=false;break;}
                if(!interior)continue;
                Vector2 screen=layout.ToScreen(gui);int px=Mathf.RoundToInt(screen.x),py=Mathf.RoundToInt(screen.y);
                if(px<0||py<0||px>=image.width||py>=image.height)continue;
                Color color=TopPixel(image,px,py);bool teal=color.g-color.r>.12f&&color.b-color.r>.09f&&color.g>.35f;
                if(road){result.roadSamples++;if(teal)result.roadMatches++;}
                else{result.emptySamples++;if(!teal)result.emptyMatches++;}
            }
            result.terrainPassed=result.roadSamples>=8&&result.emptySamples>=8&&
                result.roadMatches>=result.roadSamples*.9f&&result.emptyMatches>=result.emptySamples*.9f;
        }

        static void AddMask(List<Rect> masks,Vector2 p,float radius)=>masks.Add(new Rect(p-Vector2.one*radius,Vector2.one*radius*2));
        static void MaskMarker(List<Rect> masks,Layout layout,Vector2 world,float padding,float inward,float iconRadius)
        {
            Vector2 raw=layout.ToGui(world);
            if(layout.content.Contains(raw)){AddMask(masks,raw,iconRadius);return;}
            Vector2 delta=raw-layout.content.center;
            float tx=Mathf.Abs(delta.x)>.00001f?(layout.content.width*.5f-padding)/Mathf.Abs(delta.x):float.PositiveInfinity;
            float ty=Mathf.Abs(delta.y)>.00001f?(layout.content.height*.5f-padding)/Mathf.Abs(delta.y):float.PositiveInfinity;
            Vector2 edge=layout.content.center+delta*Mathf.Min(tx,ty);
            AddMask(masks,edge,11);AddMask(masks,edge+(layout.content.center-edge).normalized*inward,iconRadius);
        }
    }
}
