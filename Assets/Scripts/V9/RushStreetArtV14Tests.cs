using System;
using UnityEngine;
namespace EarlyRush.V9
{
    public static class RushStreetArtV14Tests
    {
        public static int Run()
        {
            int count=0;var m=RushStreetArtV14.Create();
            if(!m.Validate(out string reason))throw new Exception("Authored street geometry: "+reason);count++;
            foreach(var p in m.route){if(!m.IsWalkable(p,1.05f))throw new Exception("Street route clearance "+p);count++;}
            foreach(var site in m.tasks){if(!m.IsWalkable(site.center,1.05f))throw new Exception("Street task center clearance "+site.index);count++;}
            Vector2 source=new Vector2(431.7f,1200.2f);var round=RushStreetArtV14.Pixel(RushStreetArtV14.World(source.x,source.y));
            if(Vector2.Distance(source,round)>.001f)throw new Exception("Pixel/world mapping not invertible");count++;
            if(m.IsWalkable(RushStreetArtV14.World(540,1440),.1f)||m.IsWalkable(RushStreetArtV14.World(325,890),.1f))throw new Exception("Store facade physics absent");count++;
            if(Mathf.Abs(m.PathLength-586.4f)>.1f)throw new Exception("Authored street route changed without review");count++;
            var repair=m.tasks[1];
            for(int i=0;i<2;i++)
            {
                Vector2 wheel=repair.start+Vector2.right*(i*3-1.5f);
                Vector2 direction=(repair.target-wheel).normalized;
                Vector2 stand=wheel-direction*2;
                if(!m.IsWalkable(stand,RunV9.PlayerRadius))throw new Exception("Wheel rear approach lacks clearance: "+i+" "+stand);
                if(!m.IsWalkable(wheel,1.05f))throw new Exception("Wheel spawn lacks body clearance: "+i);
                count+=2;
            }
            return count;
        }
    }
}
