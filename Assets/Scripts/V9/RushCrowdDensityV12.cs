using UnityEngine;
namespace EarlyRush.V9
{
    public sealed partial class RunV9
    {
        // Equal-radius samples avoid confusing horizontal viewport width with actual density.
        public string CrowdDensitySummary()
        {
            int denseSum=0,quietSum=0,denseSamples=0,quietSamples=0,peak=0,protectedCount=0;
            foreach(var module in map.modules)for(int n=1;n<=4;n++)
            {
                Vector2 center=Vector2.Lerp(module.entrance,module.exit,n/5f);int nearby=0;
                foreach(var person in people)if(!person.exited&&(person.position-center).sqrMagnitude<64)nearby++;
                peak=Mathf.Max(peak,nearby);
                if(module.index%2==1){denseSum+=nearby;denseSamples++;}else{quietSum+=nearby;quietSamples++;}
            }
            foreach(var person in people)if(person.kind!=PersonKind.Alighting&&IsPedestrianReserved(person.position,person.radius))protectedCount++;
            var hotspots=new string[4];
            for(int n=0;n<4;n++)
            {
                var module=map.modules[n*2+1];Vector2 center=(module.entrance+module.exit)*.5f;
                hotspots[n]=VisibleCrowdCount(center,13.333333f,7.5f).ToString();
            }
            return "population="+people.Count+" radius=8 denseMean="+(denseSum/(float)Mathf.Max(1,denseSamples)).ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+
                " calmMean="+(quietSum/(float)Mathf.Max(1,quietSamples)).ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" samplePeak="+peak+" taskWorkspaceActors="+protectedCount+
                " fixedHotspotViewportCounts="+string.Join("/",hotspots);
        }
    }
}
