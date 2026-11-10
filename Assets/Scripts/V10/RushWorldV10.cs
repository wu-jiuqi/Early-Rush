using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using EarlyRush.V9;
namespace EarlyRush.V10
{
    [Serializable] public sealed class ArtEntryV10
    {
        public string id,resourcePath,status;public float pivotX=.5f,pivotY=.1f;
        // Optional Unity bottom-left pixel crop. Pivot is normalized within this crop; collision remains map-owned.
        public float rectX,rectY,rectWidth,rectHeight;
    }
    [Serializable] public sealed class ArtManifestV10 {public ArtEntryV10[] entries;}
    /// <summary>Component composition only. Never uses a chapter concept image as gameplay ground.</summary>
    public sealed class RushWorldV10 : MonoBehaviour
    {
        readonly Dictionary<string,ArtEntryV10> entries=new Dictionary<string,ArtEntryV10>();
        readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        readonly Dictionary<string,Tile[]> tiles=new Dictionary<string,Tile[]>();
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly List<SpriteRenderer> structures=new List<SpriteRenderer>();
        public MapData geometry {get;private set;}
        public const int FloorOrder=-30000,GroundOrder=-29000;
        public static int Sort(float y)=>Mathf.Clamp(-Mathf.RoundToInt(y*20),-28000,28000);
        void ReadManifest()
        {
            var json=Resources.Load<TextAsset>("V10Art/manifest");if(!json)throw new InvalidOperationException("V10 art manifest missing");
            foreach(var item in JsonUtility.FromJson<ArtManifestV10>(json.text).entries)entries.Add(item.id,item);
        }
        public Sprite Resolve(string key)
        {
            if(sprites.TryGetValue(key,out var found))return found;
            if(!entries.TryGetValue(key,out var entry))throw new InvalidOperationException("Unregistered V10 art: "+key);
            if(entry.status=="pending"||entry.status=="needs-rework")throw new InvalidOperationException("Pending generated art: "+key);
            var texture=Resources.Load<Texture2D>(entry.resourcePath);if(!texture)throw new InvalidOperationException("Missing bitmap "+entry.resourcePath);
            var rect=entry.rectWidth>0&&entry.rectHeight>0?new Rect(entry.rectX,entry.rectY,entry.rectWidth,entry.rectHeight):new Rect(0,0,texture.width,texture.height);
            if(rect.xMin<0||rect.yMin<0||rect.xMax>texture.width||rect.yMax>texture.height)throw new InvalidOperationException("Invalid sprite crop: "+key);
            var sprite=Sprite.Create(texture,rect,new Vector2(entry.pivotX,entry.pivotY),100,0,SpriteMeshType.FullRect);
            sprite.name=key;sprites.Add(key,sprite);owned.Add(sprite);return sprite;
        }
        public SpriteRenderer Place(string key,Vector2 contact,float width,int? layer=null)
        {
            var sprite=Resolve(key);var go=new GameObject(key);go.transform.SetParent(transform,false);
            var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.transform.localScale=Vector3.one*(width/sprite.bounds.size.x);Position(sr,contact,layer);return sr;
        }
        public void Position(SpriteRenderer sr,Vector2 contact,int? layer=null)
        {sr.transform.localPosition=new Vector3(contact.x,contact.y,0);sr.sortingOrder=layer??Sort(contact.y);}
        Tile[] Material(string key)
        {
            if(tiles.TryGetValue(key,out var found))return found;
            var texture=Resolve(key).texture;float w=texture.width/4f,h=texture.height/4f;var result=new Tile[16];
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)
            {
                var sprite=Sprite.Create(texture,new Rect(x*w,y*h,w,h),Vector2.one*.5f,w,0,SpriteMeshType.FullRect);owned.Add(sprite);
                var tile=ScriptableObject.CreateInstance<Tile>();tile.sprite=sprite;tile.colliderType=Tile.ColliderType.None;tile.transform=Matrix4x4.Scale(new Vector3(1,w/(float)h,1));owned.Add(tile);result[y*4+x]=tile;
            }
            tiles[key]=result;return result;
        }
        void Floor(Grid grid,string key,List<Vector2Int> cells)
        {
            var go=new GameObject("Material "+key);go.transform.SetParent(grid.transform,false);go.transform.localPosition=new Vector3(-.5f,-.5f,0);
            var tm=go.AddComponent<Tilemap>();var renderer=go.AddComponent<TilemapRenderer>();renderer.sortingOrder=FloorOrder;
            var positions=new Vector3Int[cells.Count];var paint=new TileBase[cells.Count];var set=Material(key);
            for(int i=0;i<cells.Count;i++){var c=cells[i];positions[i]=new Vector3Int(c.x,c.y,0);paint[i]=set[((c.y%4+4)%4)*4+(c.x%4+4)%4];}
            tm.SetTiles(positions,paint);tm.CompressBounds();
        }
        public void Build(MapData data,int stage)
        {
            if(geometry!=null)throw new InvalidOperationException("Create a new world per chapter");
            ReadManifest();geometry=data;var grid=new GameObject("V10 Tile Grid").AddComponent<Grid>();grid.transform.SetParent(transform,false);
            string baseKey=stage==0?"floors-v3/asphalt":stage==1?"floors-v3/ceramic":"floors-v3/carpet";
            var materials=new Dictionary<string,List<Vector2Int>>();
            foreach(var c in data.cells)
            {
                string key=baseKey;foreach(var z in data.zones)if(z.bounds.Contains((Vector2)c))key=z.floorKey;
                if(!materials.TryGetValue(key,out var list)){list=new List<Vector2Int>();materials.Add(key,list);}list.Add(c);
            }
            foreach(var pair in materials)Floor(grid,pair.Key,pair.Value);
            Boundaries(grid,data);
            foreach(var item in data.structures)structures.Add(Place(item.artKey,item.position,item.width));
            Debug.Log("V10_COMPONENT_WORLD cells="+data.cells.Count+" structures="+data.structures.Count+" materials="+materials.Count);
        }
        void Boundaries(Grid grid,MapData data)
        {
            // Interim bitmap edge strips, derived from the collision cells, not independent walls.
            // Generated chapter-specific walls will replace these manifest entries after approval.
            var directions=new[]{Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
            for(int i=0;i<directions.Length;i++)
            {
                bool horizontal=i%2==0;var source=Resolve(horizontal?"fx-decal/curb-h":"fx-decal/curb-v");
                var sprite=Sprite.Create(source.texture,source.rect,Vector2.one*.5f,source.texture.width,0,SpriteMeshType.FullRect);owned.Add(sprite);
                var tile=ScriptableObject.CreateInstance<Tile>();tile.sprite=sprite;tile.colliderType=Tile.ColliderType.None;
                tile.transform=Matrix4x4.Scale(new Vector3(horizontal?1:.18f,(horizontal?.18f:1)*source.texture.width/source.texture.height,1));owned.Add(tile);
                var go=new GameObject("Collision edge "+i);go.transform.SetParent(grid.transform,false);go.transform.localPosition=new Vector3(-.5f+directions[i].x*.5f,-.5f+directions[i].y*.5f,0);
                var tm=go.AddComponent<Tilemap>();go.AddComponent<TilemapRenderer>().sortingOrder=GroundOrder;
                foreach(var c in data.cells)
                {
                    if(data.cells.Contains(c+directions[i]))continue;bool structure=false;
                    foreach(var item in data.structures)if(item.bounds.Contains((Vector2)(c+directions[i]))){structure=true;break;}
                    if(!structure)tm.SetTile(new Vector3Int(c.x,c.y,0),tile);
                }
            }
        }
        public void Occlusion(Vector2 actor)
        {
            int order=Sort(actor.y);foreach(var sr in structures)
            {bool covers=sr.sortingOrder>order&&sr.bounds.Contains(new Vector3(actor.x,actor.y+.55f,0));sr.color=new Color(1,1,1,covers?.45f:1);}
        }
        public Bounds FullBounds()
        {
            var all=GetComponentsInChildren<Renderer>();if(all.Length==0)return new Bounds(Vector3.zero,Vector3.one);
            var bounds=all[0].bounds;foreach(var r in all)bounds.Encapsulate(r.bounds);bounds.Expand(new Vector3(3,3,0));return bounds;
        }
        void OnDestroy(){foreach(var item in owned)if(item)Destroy(item);}
    }
}
