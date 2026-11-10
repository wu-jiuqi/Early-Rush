using UnityEngine;

namespace EarlyRush.V9
{
    /// <summary>Small, readable original placeholder sprites. No external assets or disk writes.</summary>
    public static class RushPlaceholderArt
    {
        static readonly Color Ink = new Color32(29,65,69,255), Teal = new Color32(67,165,155,255);
        static readonly Color Cream = new Color32(245,237,211,255), Gold = new Color32(244,187,62,255);
        static readonly Color Coral = new Color32(220,114,80,255), Skin = new Color32(239,191,145,255);
        public static Texture2D Create(string key)
        {
            if(key=="street/curb-horizontal"||key=="metro/tactile-strip")
            {
                // Preserve a strip's aspect ratio; a square placeholder would cover the task room.
                var strip=new Texture2D(128,12,TextureFormat.RGBA32,false){name="Placeholder/"+key,filterMode=FilterMode.Point};
                var colors=new Color[128*12];
                for(int y=0;y<12;y++)for(int x=0;x<128;x++)colors[y*128+x]=y<3?Teal:(key.Contains("tactile")&&x%12<3?Ink:Gold);
                strip.SetPixels(colors);strip.Apply(false,true);return strip;
            }
            const int n=128;
            var tex=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Placeholder/"+key,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[n*n];
            System.Action<int,int,int,int,Color> box=(x,y,w,h,c)=>{for(int py=Mathf.Max(0,y);py<Mathf.Min(n,y+h);py++)for(int px=Mathf.Max(0,x);px<Mathf.Min(n,x+w);px++)pixels[py*n+px]=c;};
            System.Action<int,int,int,Color> disc=(cx,cy,r,c)=>{for(int y=Mathf.Max(0,cy-r);y<Mathf.Min(n,cy+r+1);y++)for(int x=Mathf.Max(0,cx-r);x<Mathf.Min(n,cx+r+1);x++)if((x-cx)*(x-cx)+(y-cy)*(y-cy)<=r*r)pixels[y*n+x]=c;};
            System.Action<int,int,int,int,Color> line=(ax,ay,bx,by,c)=>{int steps=Mathf.Max(Mathf.Abs(bx-ax),Mathf.Abs(by-ay));for(int i=0;i<=steps;i++){float t=steps==0?0:i/(float)steps;disc(Mathf.RoundToInt(Mathf.Lerp(ax,bx,t)),Mathf.RoundToInt(Mathf.Lerp(ay,by,t)),3,c);}};
            if(key.Contains("floor"))
            {
                bool asphalt=key.Contains("asphalt"),carpet=key.Contains("carpet");
                Color baseColor=asphalt?new Color32(109,128,128,255):carpet?new Color32(189,202,184,255):Cream;
                box(0,0,n,n,baseColor);Color seam=Color.Lerp(baseColor,Ink,.09f);
                for(int i=0;i<n;i+=32){box(i,0,1,n,seam);box(0,i,n,1,seam);}
                tex.wrapMode=TextureWrapMode.Repeat;
            }
            else if(key.StartsWith("people/"))
            {
                box(34,9,22,24,Ink);box(73,9,22,24,Ink);disc(64,48,33,key.Contains("boy")?Coral:Teal);
                disc(64,88,24,Skin);box(42,100,45,10,Ink);
                if(!key.EndsWith("up")){disc(55,90,3,Ink);disc(73,90,3,Ink);}
                int dx=key.EndsWith("left")?-1:key.EndsWith("right")?1:0;disc(64+dx*30,61,7,Gold);
            }
            else if(key.Contains("wheel")||key.Contains("bike"))
            {
                if(key.Contains("wheel")){disc(64,52,42,Ink);disc(64,52,31,Cream);line(32,52,96,52,Teal);line(64,20,64,84,Teal);}
                else {disc(28,34,24,Ink);disc(28,34,16,Cream);disc(100,34,24,Ink);disc(100,34,16,Cream);line(28,34,52,70,Teal);line(52,70,70,34,Teal);line(70,34,28,34,Teal);line(52,70,90,72,Teal);line(90,72,100,34,Teal);line(70,34,90,72,Teal);disc(62,103,14,Skin);box(49,73,27,20,Coral);}
            }
            else if(key.Contains("desk")||key.Contains("meeting-table"))
            {box(15,13,9,32,Ink);box(104,13,9,32,Ink);box(7,35,114,62,Cream);box(7,35,114,8,Teal);box(31,58,37,28,Ink);box(35,62,29,20,Teal);box(81,58,23,31,Gold);}
            else if(key.Contains("filing-cabinet"))
            {box(25,13,78,101,Teal);for(int y=20;y<110;y+=29){box(32,y,64,24,Cream);box(54,y+10,20,4,Ink);}}
            else if(key.Contains("chair"))
            {box(30,18,8,40,Ink);box(90,18,8,40,Ink);box(23,52,82,16,Teal);box(28,70,72,40,Teal);box(34,76,60,27,Cream);}
            else if(key.Contains("cloth"))
            {box(17,18,94,59,Teal);box(17,77,94,15,Cream);for(int i=25;i<108;i+=13)box(i,24,3,51,Cream);}
            else if(key.Contains("selection"))
            {box(12,12,104,5,Gold);box(12,111,104,5,Gold);box(12,12,5,104,Gold);box(111,12,5,104,Gold);}
            else if(key.Contains("teal-normal")||key.Contains("teal-hover"))
            {box(0,0,128,128,key.Contains("hover")?new Color32(107,200,184,255):new Color32(161,215,197,255));}
            else if(key.Contains("star"))
            {for(int i=0;i<5;i++){float a=(90+i*144)*Mathf.Deg2Rad,b=(90+(i+1)*144)*Mathf.Deg2Rad;line(64+(int)(50*Mathf.Cos(a)),64+(int)(50*Mathf.Sin(a)),64+(int)(50*Mathf.Cos(b)),64+(int)(50*Mathf.Sin(b)),Gold);}disc(64,64,18,Gold);}
            else if(key.Contains("check")&&!key.Contains("checkin"))
            {line(25,61,52,33,Teal);line(52,33,106,96,Teal);}
            else if(key.Contains("arrows")||key.Contains("exit")||key.Contains("play"))
            {line(64,20,64,103,Gold);line(64,103,33,71,Gold);line(64,103,95,71,Gold);}
            else if(key.Contains("pause"))
            {box(30,24,22,80,Ink);box(76,24,22,80,Ink);}
            else if(key.Contains("retry"))
            {disc(64,64,45,Teal);disc(64,64,32,Cream);box(60,70,68,58,Cream);line(64,106,91,84,Teal);line(91,84,96,111,Teal);}
            else if(key.Contains("clock"))
            {disc(64,64,48,Ink);disc(64,64,41,Cream);line(64,64,64,93,Ink);line(64,64,89,49,Ink);}
            else if(key.Contains("coffee"))
            {disc(95,53,20,Ink);disc(95,53,12,Cream);box(24,22,66,67,Cream);box(27,82,60,12,Ink);box(33,93,4,16,Gold);box(65,98,4,16,Gold);}
            else if(key.Contains("breakfast"))
            {box(14,18,100,15,Teal);disc(64,61,37,Gold);box(31,56,66,6,Cream);box(46,41,5,39,Cream);box(76,41,5,39,Cream);}
            else if(key.Contains("ripple")||key=="fx-decal/rain"||key=="legacy-icons/rain")
            {disc(64,51,39,new Color(.4f,.67f,.8f,.7f));disc(64,59,22,new Color(.65f,.85f,.9f,.8f));}
            else if(key.Contains("crosswalk")||key.Contains("strip")||key.Contains("curb"))
            {for(int x=6;x<128;x+=24)box(x,12,12,104,key.Contains("crosswalk")?Cream:Gold);}
            else if(key.Contains("plant")||key.Contains("bush"))
            {box(38,12,52,34,Coral);disc(64,72,39,Teal);disc(43,87,23,new Color32(119,177,126,255));}
            else if(key.Contains("train"))
            {box(5,16,118,90,Teal);box(13,65,30,29,Cream);box(85,65,30,29,Cream);box(50,17,28,77,key.Contains("open")?Ink:Cream);box(8,16,112,7,Gold);}
            else if(key.Contains("vending"))
            {box(22,12,84,105,Teal);box(32,52,64,52,Cream);disc(64,78,20,Gold);box(42,23,44,17,Ink);}
            else if(key.Contains("file")||key.Contains("speech")||key.Contains("thought"))
            {box(21,16,86,96,Cream);for(int y=40;y<100;y+=18)box(35,y,58,4,Teal);}
            else
            {
                // Neutral furniture/building footprint, with front edge at the common ground pivot.
                box(7,13,114,80,Teal);box(13,35,102,58,Cream);box(4,93,120,19,Teal);
                box(21,49,28,28,Ink);box(77,49,28,28,Ink);box(54,15,20,40,Gold);
            }
            tex.SetPixels(pixels);tex.Apply(false,true);return tex;
        }
    }
}
