using System;
using UnityEngine;
namespace EarlyRush.V10
{
    public static class RushMapTestsV10
    {
        // Run in the Unity editor before connecting simulation. Geometry checks are NOT
        // evidence for task completion, human fun, or seven-to-ten-minute play duration.
        public static string ValidateChapters()
        {
            string result="V10 geometry only; gameplay and timing NOT validated\n";
            for(int stage=0;stage<3;stage++)
            {
                var map=RushMapV10.Create(stage,91026);string reason;
                if(!map.Validate(out reason))throw new InvalidOperationException("V10 stage "+stage+": "+reason);
                var again=RushMapV10.Create(stage,91026);
                if(map.ToPreviewJson()!=again.ToPreviewJson())throw new InvalidOperationException("Non deterministic V10 map");
                result+="stage="+stage+" regions="+map.regions.Count+" route="+map.PathLength+" cells="+map.cells.Count+"\n";
            }
            return result;
        }
    }
}
