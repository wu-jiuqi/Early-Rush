using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
namespace EarlyRush.V9
{
    [Serializable]
    public sealed class RouteCrowdSampleV13
    {
        public float routeDistance;
        public Vector2 center;
        public bool excluded;
        public int visible;
    }
    [Serializable]
    public sealed class CrowdRouteCoverageV13
    {
        public int stage,population,eligibleSamples,excludedSamples,emptySamples,sparseSamples;
        public int minimum,p10,median,maximum,bypassActors;
        public double elapsed;
        public float spacing,longestEmptyMetres,longestSparseMetres,junctionMean,connectorMean;
        public List<RouteCrowdSampleV13> samples=new List<RouteCrowdSampleV13>();
        public string Summary(bool includeSamples=false)
        {
            var result=new StringBuilder();
            result.AppendFormat(CultureInfo.InvariantCulture,
                "stage={0} seconds={1:F2} population={2} routeSampleStep={3:F0} viewport=26.6667x15 eligible={4} excluded={5} visibleMin={6} p10={7} median={8} max={9} empty={10} below6={11} longestEmptySampledM={12:F0} longestBelow6SampledM={13:F0} junctionMean={14:F2} connectorMean={15:F2} bypassActors={16}",
                stage,elapsed,population,spacing,eligibleSamples,excludedSamples,minimum,p10,median,maximum,emptySamples,sparseSamples,
                longestEmptyMetres,longestSparseMetres,junctionMean,connectorMean,bypassActors);
            if(includeSamples)
            {
                result.Append(" routeVisible=");
                for(int i=0;i<samples.Count;i++)
                {
                    if(i>0)result.Append('|');
                    result.Append(samples[i].routeDistance.ToString("F0",CultureInfo.InvariantCulture)).Append(':')
                        .Append(samples[i].excluded?"task-workspace":samples[i].visible.ToString(CultureInfo.InvariantCulture));
                }
            }
            return result.ToString();
        }
    }
    public sealed partial class RunV9
    {
        /// <summary>Whole commute, fixed viewport and spacing. Exclusions are explicit in the sample list.</summary>
        public CrowdRouteCoverageV13 MeasureRouteCrowdCoverage(float spacing=12)
        {
            if(float.IsNaN(spacing)||float.IsInfinity(spacing)||spacing<5||spacing>30)throw new ArgumentOutOfRangeException(nameof(spacing));
            var result=new CrowdRouteCoverageV13{stage=stage,elapsed=elapsed,population=people.Count,spacing=spacing};
            var counts=new List<int>();float emptyRun=0,sparseRun=0;int junctionTotal=0,junctionSamples=0,connectorTotal=0,connectorSamples=0;
            // Twelve metres at each end are an intentional starting/finishing buffer.
            for(float distance=12;distance<=map.PathLength-12;distance+=spacing)
            {
                Vector2 center=map.PointAtDistance(distance);
                var sample=new RouteCrowdSampleV13{routeDistance=distance,center=center,excluded=IsPedestrianReserved(center,.48f),
                    visible=VisibleCrowdCount(center,40f/3,7.5f)};
                result.samples.Add(sample);
                if(sample.excluded){result.excludedSamples++;emptyRun=sparseRun=0;continue;}
                result.eligibleSamples++;counts.Add(sample.visible);
                if(sample.visible==0){result.emptySamples++;emptyRun+=spacing;}else emptyRun=0;
                if(sample.visible<6){result.sparseSamples++;sparseRun+=spacing;}else sparseRun=0;
                result.longestEmptyMetres=Mathf.Max(result.longestEmptyMetres,emptyRun);
                result.longestSparseMetres=Mathf.Max(result.longestSparseMetres,sparseRun);
                if(PedestrianJunctionAt(center)>=0){junctionTotal+=sample.visible;junctionSamples++;}
                else{connectorTotal+=sample.visible;connectorSamples++;}
            }
            counts.Sort();
            if(counts.Count>0)
            {
                result.minimum=counts[0];result.p10=counts[Mathf.FloorToInt((counts.Count-1)*.1f)];
                result.median=counts[counts.Count/2];result.maximum=counts[counts.Count-1];
            }
            result.junctionMean=junctionTotal/(float)Mathf.Max(1,junctionSamples);
            result.connectorMean=connectorTotal/(float)Mathf.Max(1,connectorSamples);
            foreach(var person in people)if(!person.exited)
                foreach(var zone in map.zones)if(zone.kind=="loop-lane"&&zone.bounds.Contains(person.position)){result.bypassActors++;break;}
            return result;
        }

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
                " fixedHotspotViewportCounts="+string.Join("/",hotspots)+" wholeRoute={"+MeasureRouteCrowdCoverage().Summary()+"}";
        }
    }
}
