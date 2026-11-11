using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace EarlyRush.ArtV4 {
public sealed class RushArtV4Review:MonoBehaviour {
 [Serializable] public class Corridor {public Vector2[] points;public float width;public string kind;}
 [Serializable] public class Box {public float x,y,w,h;public string kind;}
 [Serializable] public class Item:Box {public string src,id,role;public int z;}
 [Serializable] public class Task {public string id;public Vector2 position;}
 [Serializable] public class World {public string id,groundSrc,outsideColor;public float worldWidth,worldHeight;public Vector2 start,end;public Corridor[] corridors;public Box[] rooms,zones,blockers;public Item[] objects;public Task[] tasks;}
 const float Ppu=40; Camera cam;World data;Transform root;int stage,taskIndex;bool capturing; SpriteRenderer referencePerson;
 readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();readonly List<SpriteRenderer> sprites=new List<SpriteRenderer>();
 readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
 string[] names={"street","metro","office"};
 Vector3 Point(Vector2 p)=>new Vector3(p.x/Ppu,(data.worldHeight-p.y)/Ppu,0);
 Texture2D Tex(string path){if(!textures.TryGetValue(path,out var t)){t=Resources.Load<Texture2D>("ArtV4/"+path.Substring(0,path.LastIndexOf('.')));if(!t)throw new Exception("Missing art-v4 texture: "+path);textures[path]=t;}return t;}
 void Awake(){Application.runInBackground=true;cam=new GameObject("Art V4 Review Camera").AddComponent<Camera>();cam.orthographic=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.nearClipPlane=.1f;cam.farClipPlane=100;cam.transform.position=new Vector3(0,0,-30);Load(0);capturing=Array.Exists(Environment.GetCommandLineArgs(),s=>s=="--art-v4-capture");if(capturing)StartCoroutine(Capture());}
 public void Load(int index){stage=index;taskIndex=0;if(root)Destroy(root.gameObject);foreach(var o in owned)Destroy(o);owned.Clear();sprites.Clear();root=new GameObject("V4 "+names[index]+" REVIEW ONLY").transform;data=JsonUtility.FromJson<World>(Resources.Load<TextAsset>("ArtV4/"+names[index]).text);ColorUtility.TryParseHtmlString(data.outsideColor,out var outside);cam.backgroundColor=outside;
  // Separate geometry layers share world-space UVs; overlapping capsule joins have no texture phase discontinuity.
  Ground(42,new Color(.97f,.94f,.85f),null,-30000);Ground(15,new Color(.45f,.66f,.60f),null,-29999);Ground(0,Color.white,Tex(data.groundSrc),-29998);
  if(data.id=="street")Road();
  foreach(var b in data.zones){if(b.kind=="work-island"||b.kind=="track-bed"||b.kind=="chair"||b.kind=="meeting")Quad(b,b.kind=="track-bed"?new Color(.4f,.43f,.43f):b.kind=="work-island"?new Color(.82f,.91f,.83f):new Color(.95f,.82f,.36f),-29996);}
  foreach(var item in data.objects)AddSprite(item);
  AddSprite(new Item{id="STATIC SCALE REFERENCE - NOT PLAYER",src="sprites/characters/player-front.png",x=data.start.x-42.5f,y=data.start.y-135,w=85,h=135});
  referencePerson=sprites[sprites.Count-1];
  cam.orthographicSize=12.5f;Focus(data.start);
 }
 void Focus(Vector2 p){if(referencePerson){referencePerson.gameObject.SetActive(true);referencePerson.transform.position=Point(p);referencePerson.sortingOrder=Mathf.RoundToInt(p.y);}var a=Point(p);cam.transform.position=new Vector3(a.x,a.y,-30);Cull();}
 void Ground(float border,Color color,Texture2D texture,int order){var v=new List<Vector3>();var tri=new List<int>();foreach(var c in data.corridors)for(int i=1;i<c.points.Length;i++)Capsule(v,tri,c.points[i-1],c.points[i],(c.width+border)/2);foreach(var b in data.rooms)Rect(v,tri,b.x-border/2,b.y-border/2,b.w+border,b.h+border);Mesh(v,tri,texture,color,order);}
 void Road(){var v=new List<Vector3>();var t=new List<int>();foreach(var c in data.corridors)if(c.kind=="main")for(int i=1;i<c.points.Length;i++)Capsule(v,t,c.points[i-1],c.points[i],128);Mesh(v,t,Tex("sprites/materials/asphalt.png"),Color.white,-29997);}
 void Rect(List<Vector3>v,List<int>t,float x,float y,float w,float h){int n=v.Count;v.Add(Point(new Vector2(x,y)));v.Add(Point(new Vector2(x+w,y)));v.Add(Point(new Vector2(x+w,y+h)));v.Add(Point(new Vector2(x,y+h)));t.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});}
 void Capsule(List<Vector3>v,List<int>t,Vector2 a,Vector2 b,float r){Vector2 side=new Vector2(-(b-a).y,(b-a).x).normalized*r;int n=v.Count;v.Add(Point(a+side));v.Add(Point(a-side));v.Add(Point(b-side));v.Add(Point(b+side));t.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});Disc(v,t,a,r);Disc(v,t,b,r);}
 void Disc(List<Vector3>v,List<int>t,Vector2 p,float r){int n=v.Count;v.Add(Point(p));const int count=32;for(int i=0;i<=count;i++){float a=i*Mathf.PI*2/count;v.Add(Point(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r));if(i>0)t.AddRange(new[]{n,n+i+1,n+i});}}
 void Mesh(List<Vector3>v,List<int>tri,Texture2D texture,Color color,int order){var go=new GameObject("Ground geometry");go.transform.parent=root;var m=new Mesh();m.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;m.SetVertices(v);m.SetTriangles(tri,0);var uv=new List<Vector2>();foreach(var p in v)uv.Add(new Vector2(p.x*Ppu/512,p.y*Ppu/512));m.SetUVs(0,uv);m.RecalculateBounds();go.AddComponent<MeshFilter>().sharedMesh=m;var renderer=go.AddComponent<MeshRenderer>();var mat=new Material(Shader.Find("Sprites/Default"));mat.mainTexture=texture?texture:Texture2D.whiteTexture;mat.color=color;renderer.sharedMaterial=mat;renderer.sortingOrder=order;owned.Add(m);owned.Add(mat);}
 void Quad(Box b,Color color,int order){var v=new List<Vector3>();var t=new List<int>();Rect(v,t,b.x,b.y,b.w,b.h);Mesh(v,t,null,color,order);}
 void AddSprite(Item item){var texture=Tex(item.src);var go=new GameObject(item.id);go.transform.parent=root;var sr=go.AddComponent<SpriteRenderer>();var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,0),Ppu);owned.Add(sprite);sr.sprite=sprite;float scale=Mathf.Min(item.w/texture.width,item.h/texture.height);go.transform.localScale=Vector3.one*scale;go.transform.position=Point(new Vector2(item.x+item.w/2,item.y+item.h));sr.sortingOrder=item.role=="puddle"?-29995:Mathf.RoundToInt(item.y+item.h);sprites.Add(sr);}
 void Cull(){float hh=cam.orthographicSize+8,hw=hh*cam.aspect;var p=cam.transform.position;var view=new Bounds(new Vector3(p.x,p.y,0),new Vector3(hw*2,hh*2,20));foreach(var s in sprites)if(s)s.enabled=s.bounds.Intersects(view);}
 void Fit(){cam.orthographicSize=Mathf.Max(data.worldHeight/Ppu/2,data.worldWidth/Ppu/(2*cam.aspect))*1.05f;Focus(new Vector2(data.worldWidth/2,data.worldHeight/2));if(referencePerson)referencePerson.gameObject.SetActive(false);}
 void Update(){if(capturing)return;for(int i=0;i<3;i++)if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i)))Load(i);if(Input.GetKeyDown(KeyCode.F))Fit();if(Input.GetKeyDown(KeyCode.T)){cam.orthographicSize=12.5f;Focus(data.tasks[taskIndex++%data.tasks.Length].position);}float scroll=Input.mouseScrollDelta.y;cam.orthographicSize=Mathf.Clamp(cam.orthographicSize*Mathf.Pow(.85f,scroll),5,300);var move=new Vector3(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"),0);cam.transform.position+=move*Time.unscaledDeltaTime*cam.orthographicSize;Cull();}
 void OnGUI(){GUI.color=Color.white;GUI.Box(new Rect(10,10,670,62),"ART V4 REVIEW ONLY - NOT PLAYABLE / NO TIMING CLAIM\n1 Street | 2 Metro | 3 Office | WASD pan | Wheel zoom | F full map | T tasks");GUI.Label(new Rect(20,78,500,25),names[stage]+" / 40 source pixels per Unity unit / independent PNG objects / static 85x135px person for scale");}
 IEnumerator Capture(){string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../QA/art-v4"));Directory.CreateDirectory(output);yield return null;for(int i=0;i<3;i++){Load(i);yield return null;yield return new WaitForEndOfFrame();Save(output,names[i]+"-start");for(int j=0;j<data.tasks.Length;j++){Focus(data.tasks[j].position);yield return new WaitForEndOfFrame();Save(output,names[i]+"-"+data.tasks[j].id);}Fit();yield return new WaitForEndOfFrame();Save(output,names[i]+"-overview");}File.WriteAllText(Path.Combine(output,"README.txt"),"Unity-rendered art-v4 review, 40 px/unit. Independent sprites and geometry. No simulation/physics/task timing claim. Overview is camera render, not source overview JPEG.");Application.Quit();}
 void Save(string output,string name){var rt=new RenderTexture(1280,720,24);var old=RenderTexture.active;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),tex.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=old;rt.Release();Destroy(rt);Destroy(tex);}
}}
