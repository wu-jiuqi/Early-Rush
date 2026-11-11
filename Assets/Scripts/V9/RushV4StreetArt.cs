using System;
using System.Collections.Generic;
using UnityEngine;
using EarlyRush.V8;
using Item=EarlyRush.ArtV4.RushArtV4Review.Item;
using Box=EarlyRush.ArtV4.RushArtV4Review.Box;
using World=EarlyRush.ArtV4.RushArtV4Review.World;
namespace EarlyRush.V9 {
/// <summary>Optional 91027 rendering only. No camera, input, task outcome or physics writes.</summary>
public static class RushV4StreetArt {
 public static bool Applies(int stage,int seed)=>stage>=0&&stage<=2&&seed==RushV4StreetMap.Seed;
 public static void Build(Transform parent,List<UnityEngine.Object> owned,List<SpriteRenderer> decor,RunV9 run){
  if(!Applies(run.stage,run.seed))throw new ArgumentException("V4 street renderer requires stage0 seed91027");
  new Context(parent,owned,decor).Build(run.stage);
 }
 sealed class Context {
 const float Ppu=40;readonly Transform root;readonly List<UnityEngine.Object> owned;readonly List<SpriteRenderer> decor;
 readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();World data;
 public Context(Transform root,List<UnityEngine.Object> owned,List<SpriteRenderer> decor){this.root=root;this.owned=owned;this.decor=decor;}
 Vector3 Point(Vector2 p)=>new Vector3(p.x/Ppu,(data.worldHeight-p.y)/Ppu,0);
 Texture2D Tex(string path){if(!textures.TryGetValue(path,out var t)){t=Resources.Load<Texture2D>("ArtV4/"+path.Substring(0,path.LastIndexOf('.')));if(!t)throw new Exception("Missing art-v4 texture: "+path);textures[path]=t;}return t;}
 public void Build(int stage){data=JsonUtility.FromJson<World>(Resources.Load<TextAsset>("ArtV4/"+new[]{"street","metro","office"}[stage]).text);
  ColorUtility.TryParseHtmlString(data.outsideColor,out var outside);
  Quad(new Box{x=-512,y=-512,w=data.worldWidth+1024,h=data.worldHeight+1024},outside,-30001);
  Ground(42,new Color(.97f,.94f,.85f),null,-30000);Ground(15,new Color(.45f,.66f,.60f),null,-29999);Ground(0,Color.white,Tex(data.groundSrc),-29998);if(stage==0)Road();
  foreach(var b in data.zones)if(b.kind=="work-island"||b.kind=="track-bed")Quad(b,b.kind=="track-bed"?new Color(.4f,.43f,.43f):new Color(.82f,.91f,.83f),-29996,stage==2&&b.kind=="work-island"?Tex(data.groundSrc):null);
  foreach(var item in data.objects){
   // Runtime bodies are authoritative: never draw a second stationary copy of their source PNG.
   if(RushV4NpcSources.IsNpcObject(item.src))continue;
   if(item.role=="pushable"||item.role=="puddle"||item.id=="M3-cloth")continue;
   // The rear flip-panels facade has a source physical footprint and stays as a target sign.
   // Five live contact boards are separately rendered in front by RunV9's presentation.
   AddSprite(item);
  }
 }
 void Ground(float border,Color color,Texture2D texture,int order){var v=new List<Vector3>();var tri=new List<int>();foreach(var c in data.corridors)for(int i=1;i<c.points.Length;i++)Capsule(v,tri,c.points[i-1],c.points[i],(c.width+border)/2);foreach(var b in data.rooms)Rect(v,tri,b.x-border/2,b.y-border/2,b.w+border,b.h+border);Mesh(v,tri,texture,color,order);}
 void Road(){var v=new List<Vector3>();var t=new List<int>();foreach(var c in data.corridors)if(c.kind=="main")for(int i=1;i<c.points.Length;i++)Capsule(v,t,c.points[i-1],c.points[i],128);Mesh(v,t,Tex("sprites/materials/asphalt.png"),Color.white,-29997);}
 void Rect(List<Vector3>v,List<int>t,float x,float y,float w,float h){int n=v.Count;v.Add(Point(new Vector2(x,y)));v.Add(Point(new Vector2(x+w,y)));v.Add(Point(new Vector2(x+w,y+h)));v.Add(Point(new Vector2(x,y+h)));t.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});}
 void Capsule(List<Vector3>v,List<int>t,Vector2 a,Vector2 b,float r){Vector2 side=new Vector2(-(b-a).y,(b-a).x).normalized*r;int n=v.Count;v.Add(Point(a+side));v.Add(Point(a-side));v.Add(Point(b-side));v.Add(Point(b+side));t.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});Disc(v,t,a,r);Disc(v,t,b,r);}
 void Disc(List<Vector3>v,List<int>t,Vector2 p,float r){int n=v.Count;v.Add(Point(p));const int count=32;for(int i=0;i<=count;i++){float a=i*Mathf.PI*2/count;v.Add(Point(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r));if(i>0)t.AddRange(new[]{n,n+i+1,n+i});}}
 void Mesh(List<Vector3>v,List<int>tri,Texture2D texture,Color color,int order){var go=new GameObject("Ground geometry");go.transform.parent=root;var m=new Mesh();m.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;m.SetVertices(v);m.SetTriangles(tri,0);var uv=new List<Vector2>();foreach(var p in v)uv.Add(new Vector2(p.x*Ppu/512,p.y*Ppu/512));m.SetUVs(0,uv);m.RecalculateBounds();go.AddComponent<MeshFilter>().sharedMesh=m;var renderer=go.AddComponent<MeshRenderer>();var mat=new Material(Shader.Find("Sprites/Default"));mat.mainTexture=texture?texture:Texture2D.whiteTexture;mat.color=color;renderer.sharedMaterial=mat;renderer.sortingOrder=order;owned.Add(m);owned.Add(mat);}
 void Quad(Box b,Color color,int order,Texture2D texture=null){var v=new List<Vector3>();var t=new List<int>();Rect(v,t,b.x,b.y,b.w,b.h);Mesh(v,t,texture,color,order);}
 void AddSprite(Item item){var texture=Tex(item.src);var go=new GameObject(item.id);go.transform.parent=root;var sr=go.AddComponent<SpriteRenderer>();var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,0),Ppu);owned.Add(sprite);sr.sprite=sprite;float scale=Mathf.Min(item.w/texture.width,item.h/texture.height);go.transform.localScale=Vector3.one*scale;go.transform.position=Point(new Vector2(item.x+item.w/2,item.y+item.h));sr.sortingOrder=item.role=="puddle"?-29995:ArtV8.SortOrder(Point(new Vector2(item.x,item.y+item.h)).y);decor.Add(sr);}

}
}
}
