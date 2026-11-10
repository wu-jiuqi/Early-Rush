using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace EarlyRush.V10
{
    /// <summary>Walkable component assembly review, deliberately not a nine-task game certification.</summary>
    public sealed class RushAssemblyV10:MonoBehaviour
    {
        Camera cam;RushWorldV10 world;MapDataV10 map;SpriteRenderer actor;Vector2 position;bool capture;
        void Awake()
        {
            cam=new GameObject("V10 Fixed Camera").AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=7.5f;cam.backgroundColor=new Color(.20f,.27f,.26f);cam.transform.position=new Vector3(0,0,-30);cam.farClipPlane=60;
            capture=Array.Exists(Environment.GetCommandLineArgs(),x=>x=="--v10-panorama");Load(0);
            if(capture){Application.runInBackground=true;StartCoroutine(Panoramas());}
        }
        public void Load(int stage)
        {
            if(world)Destroy(world.gameObject);map=RushMapV10.Create(stage,91026);
            world=new GameObject("V10 Components Chapter "+stage).AddComponent<RushWorldV10>();world.Build(map.geometry,stage);
            position=map.spawn;actor=world.Place("people/boy-up",position,1.05f);Follow();
        }
        void Update()
        {
            if(capture)return;
            if(Input.GetKeyDown(KeyCode.Alpha1))Load(0);if(Input.GetKeyDown(KeyCode.Alpha2))Load(1);if(Input.GetKeyDown(KeyCode.Alpha3))Load(2);
            var input=new Vector2((Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0));
            if(input.sqrMagnitude>1)input.Normalize();float remaining=Mathf.Min(Time.unscaledDeltaTime,.25f);
            while(remaining>0){float step=Mathf.Min(remaining,1f/60);position=map.ResolveMove(position,input*3.6f*step,.46f);remaining-=step;}
            world.Position(actor,position);world.Occlusion(position);Follow();
        }
        void Follow(){cam.transform.position=new Vector3(position.x,position.y,-30);}
        void OnGUI(){if(!capture)GUI.Label(new Rect(15,15,650,30),"V10 COMPONENT ASSEMBLY ONLY · WASD walk · 1/2/3 chapter · tasks not connected");}
        IEnumerator Panoramas()
        {
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../QA/V10-assembly"));Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"README.txt"),"Actual Unity component assembly, not concept-image background. Legacy art reuse pending new generated replacements. No nine-task simulation integration or gameplay completion claim.");
            for(int stage=0;stage<3;stage++)
            {
                Load(stage);yield return null;yield return null;Bounds b=world.FullBounds();int max=Mathf.Min(12000,SystemInfo.maxTextureSize);
                float scale=Mathf.Min(Mathf.Min(4000,max)/b.size.x,max/b.size.y);int w=Mathf.Max(1,(int)(b.size.x*scale)),h=Mathf.Max(1,(int)(b.size.y*scale));
                cam.aspect=w/(float)h;cam.orthographicSize=Mathf.Max(b.extents.y,b.extents.x/cam.aspect);cam.transform.position=new Vector3(b.center.x,b.center.y,-30);
                var rt=new RenderTexture(w,h,24);var old=RenderTexture.active;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);
                try{if(!rt.Create())throw new Exception("V10 panorama allocation failed");cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(output,"chapter-"+stage+".png"),tex.EncodeToPNG());File.WriteAllText(Path.Combine(output,"chapter-"+stage+".json"),map.ToPreviewJson());}
                finally{cam.targetTexture=null;RenderTexture.active=old;rt.Release();Destroy(rt);Destroy(tex);}
            }
            Debug.Log("V10_ASSEMBLY_CAPTURE_COMPLETE");Application.Quit();
        }
    }
}
