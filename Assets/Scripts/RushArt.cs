using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush
{
    /// <summary>Generated raster art is presentation only; authored collisions/routes remain authoritative.</summary>
    public static class RushArt
    {
        sealed class Entry { public Texture2D texture;public Rect pixels,uv;public Sprite sprite; }
        static readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
        static bool loaded;
        static readonly Dictionary<string,Material> floorMaterials=new Dictionary<string,Material>();
        static readonly Quaternion viewRotation=Quaternion.Euler(58.736f,0,0);
        static void Atlas(string file,int cols,int rows,string[] names)
        {
            var tex=Resources.Load<Texture2D>("RushArt/"+file);
            if(!tex){Debug.LogError("Missing generated art atlas: "+file);return;}
            for(int i=0;i<names.Length;i++)
            {
                if(string.IsNullOrEmpty(names[i]))continue;
                // Integer boundaries support non-power-of-two imagegen output without leaking adjacent cells.
                int left=Mathf.RoundToInt((i%cols)*tex.width/(float)cols), right=Mathf.RoundToInt((i%cols+1)*tex.width/(float)cols);
                int top=Mathf.RoundToInt((i/cols)*tex.height/(float)rows),bottom=Mathf.RoundToInt((i/cols+1)*tex.height/(float)rows);
                Rect pixels=new Rect(left,tex.height-bottom,right-left,bottom-top);
                entries[names[i]]=new Entry{texture=tex,pixels=pixels,uv=new Rect(pixels.x/tex.width,pixels.y/tex.height,pixels.width/tex.width,pixels.height/tex.height)};
            }
        }
        static void Load()
        {
            if(loaded)return;loaded=true;
            Atlas("props",4,4,new[]{"tree","planter","bench","lamp","cone","barrier","column","rail","vending","gate","kiosk","train-prop","desk","sofa","printer","clockin"});
            // Explicit grid contracts are checked by Tools/validate_v7.py and documented in the art manifest.
            LoadAdditionalAtlases();
        }
        static void LoadAdditionalAtlases()
        {
            Atlas("people",4,4,new[]{"player-walk-back","player-walk-front","player-bike-back","player-bike-front","blue-walk","purple-walk","green-walk","gold-walk","blue-bike","purple-bike","green-bike","gold-bike","player-walk-left","player-walk-right","player-bike-left","player-bike-right"});
            Atlas("icons",4,4,new[]{"bike","train","clock","play","pause","retry","home","sound","shake","exit","move","dash","shield","rain","stairs","escalator"});
            Atlas("extras",4,4,new[]{"coffee","breakfast","lost-item","coffee-stall","breakfast-stall","stairs-prop","escalator-prop","parcel","floor-tile","floor-road","floor-office","office-door","puddle","arrow","check","star"});
            Atlas("floors",3,1,new[]{"floor-tile","floor-road","floor-office"});
        }
        public static bool Has(string key){Load();return entries.ContainsKey(key);}
        public static void DrawIcon(string key,Rect r,float opacity=1)
        {
            Load();if(!entries.TryGetValue(key,out var e))return;
            Color old=GUI.color;GUI.color=new Color(1,1,1,opacity);GUI.DrawTextureWithTexCoords(r,e.texture,e.uv,true);GUI.color=old;
        }
        public static Transform Sprite(Transform parent,string key,Vector3 position,float width,float height)
        {
            Load();if(!entries.TryGetValue(key,out var e))return null;
            if(!e.sprite)e.sprite=UnityEngine.Sprite.Create(e.texture,e.pixels,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
            var t=new GameObject("Art • "+key).transform;t.SetParent(parent,false);t.localPosition=position;t.rotation=viewRotation;
            var sr=t.gameObject.AddComponent<SpriteRenderer>();sr.sprite=e.sprite;sr.sortingOrder=0;
            t.localScale=new Vector3(width/(e.pixels.width/100),height/(e.pixels.height/100),1);
            return t;
        }
        public static void SetSprite(SpriteRenderer renderer,string key)
        {
            Load();if(!entries.TryGetValue(key,out var e))return;
            if(!e.sprite)e.sprite=UnityEngine.Sprite.Create(e.texture,e.pixels,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
            renderer.sprite=e.sprite;
        }
        static void Replace(Transform source,WorldData w,string key,float width,float height)
        {
            var renderer=source.GetComponent<Renderer>();if(!renderer||!Has(key))return;
            renderer.enabled=false;
            Vector3 world=source.position;world.y=.12f;
            // Ground-centred art extends back from its collision footprint, rather than covering the path ahead.
            Sprite(w.root,key,w.root.InverseTransformPoint(world)+Vector3.up*(height*.26f),width,height);
        }
        public static void ApplyWorld(WorldData w,int stage)
        {
            Load();var renderers=w.root.GetComponentsInChildren<Renderer>(true);
            foreach(var r in renderers)
            {
                string n=r.gameObject.name;Transform t=r.transform;
                if(n.StartsWith("Art •"))continue;
                var mf=t.GetComponent<MeshFilter>();Vector3 size=mf&&mf.sharedMesh?mf.sharedMesh.bounds.size:Vector3.one;
                size=Vector3.Scale(size,t.lossyScale);
                if(n=="Tree trunk")Replace(t,w,"tree",3.2f,4.4f);
                else if(n=="Rounded planter")Replace(t,w,"planter",Mathf.Max(size.x,size.z)*1.3f,Mathf.Max(size.x,size.z)*1.3f);
                else if(n=="Seat")Replace(t,w,"bench",3.1f,2.7f);
                else if(n=="Street lamp pole")Replace(t,w,"lamp",1.8f,3.8f);
                else if(n=="Cone foot")Replace(t,w,"cone",.85f,1.1f);
                else if(n=="Road barrier")Replace(t,w,"barrier",size.x+0.3f,2.1f);
                else if(n=="Station column")Replace(t,w,"column",1.8f,3.7f);
                else if(n=="Ticket machine")Replace(t,w,"vending",2.3f,3.0f);
                else if(n=="Ticket gate")Replace(t,w,"gate",2.0f,4.5f);
                else if(n=="Coffee kiosk")Replace(t,w,"kiosk",5.6f,5.6f);
                else if(n=="Desk top"||n=="Shared team workbench"||n=="Lobby reception counter")Replace(t,w,"desk",Mathf.Max(4.4f,size.x),4.1f);
                else if(n=="Breakroom sofa")Replace(t,w,"sofa",6f,4.3f);
                else if(n=="Printer delivery island")Replace(t,w,"printer",6.5f,7.5f);
                else if(n=="Platform seating island"||n=="Transfer column island")Replace(t,w,"kiosk",6.5f,7f);
                else if(n=="Delivery crates")Replace(t,w,"parcel",7,9);
                else if(n=="Train side shell")Replace(t,w,"train-prop",size.x+1,6);
                else if(n=="Clock in pedestal")Replace(t,w,"clockin",2.8f,3.7f);
                else if(n=="Low route divider")Replace(t,w,"rail",size.x,1.2f);
                else if(n=="Meeting room low partition")Replace(t,w,"desk",7.2f,9f);
                else if(IsSuperseded(n))r.enabled=false;
                else if(n=="Station tiled floor"||n=="Continuous district ground")TileFloor(t,stage==0?"floor-road":"floor-tile",size);
                else if(n=="Long morning boulevard")TileFloor(t,"floor-road",size);
                else if(n=="Office wood floor")TileFloor(t,"floor-office",size);
            }
        }
        static bool IsSuperseded(string n)
        {
            switch(n)
            {
                case "Escalator tread":case "Stair tread":case "Tree crown":case "Tree sunlit crown":case "Planter soil":case "Broad plant leaf":
                case "Back":case "Leg":case "Lamp globe":case "Lamp foot":case "Traffic cone":case "Cone reflective band":
                case "Barrier stripe":case "Barrier foot":case "Column band":case "Ticket screen":case "Gate scanner":
                case "Kiosk awning":case "Desk leg":case "Monitor":case "Monitor screen":case "Monitor foot":case "Keyboard":case "Desk coffee cup":case "Notebook":
                case "Reception accent":case "Meeting table":case "Printer paper stack":case "Parcel straps":
                case "Clock in tablet":case "Tablet display":case "Train teal stripe":case "Train window":return true;
                default:return false;
            }
        }
        static void TileFloor(Transform original,string key,Vector3 size)
        {
            if(!entries.TryGetValue(key,out var e))return;
            var filter=original.GetComponent<MeshFilter>();if(!filter)return;
            int nx=Mathf.Max(1,Mathf.CeilToInt(size.x/4)),nz=Mathf.Max(1,Mathf.CeilToInt(size.z/4));
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
            {
                float x0=-size.x/2+size.x*x/nx,x1=-size.x/2+size.x*(x+1)/nx,z0=-size.z/2+size.z*z/nz,z1=-size.z/2+size.z*(z+1)/nz;int k=vertices.Count;
                vertices.Add(new Vector3(x0,size.y*.5f,z0));vertices.Add(new Vector3(x0,size.y*.5f,z1));vertices.Add(new Vector3(x1,size.y*.5f,z1));vertices.Add(new Vector3(x1,size.y*.5f,z0));
                uv.Add(new Vector2(e.uv.xMin,e.uv.yMin));uv.Add(new Vector2(e.uv.xMin,e.uv.yMax));uv.Add(new Vector2(e.uv.xMax,e.uv.yMax));uv.Add(new Vector2(e.uv.xMax,e.uv.yMin));
                tris.Add(k);tris.Add(k+1);tris.Add(k+2);tris.Add(k);tris.Add(k+2);tris.Add(k+3);
            }
            var mesh=new Mesh{name="Generated floor tile grid"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();filter.sharedMesh=mesh;
            if(!floorMaterials.TryGetValue(key,out var mat)){mat=new Material(Shader.Find("Unlit/Texture")){mainTexture=e.texture};floorMaterials[key]=mat;}
            original.GetComponent<Renderer>().sharedMaterial=mat;
            original.gameObject.AddComponent<RushArtMeshCleanup>().ownedMesh=mesh;
        }
        public static void WorldSign(WorldData w,string text,Vector3 p,float size,Color color,bool floor)
        {
            string key=text.Contains("HOLD")?"shield":text.Contains("CLOCK")||text.Contains("08:")||text.Contains("MORNING")?"clock":text.Contains("METRO")||text.Contains("LINE")||text.Contains("BOARD")?"train":"move";
            Sprite(w.root,key,p+Vector3.up*.2f,Mathf.Max(.7f,size*2),Mathf.Max(.7f,size*2));
        }
    }
    public sealed class RushArtMeshCleanup:MonoBehaviour
    {
        public Mesh ownedMesh;
        void OnDestroy(){if(ownedMesh)UnityEngine.Object.Destroy(ownedMesh);}
    }
}
