using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EarlyRush.V8
{
    /// <summary>V3 imagegen bitmap presentation. World positions are fixed XY, anchored at ground contact.</summary>
    public static class ArtV8
    {
        public const int FloorOrder = -30000;
        public const int GroundMarkOrder = -29000;
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, UnityEngine.Sprite> sprites = new Dictionary<string, UnityEngine.Sprite>();
        static readonly Dictionary<string, Material> floors = new Dictionary<string, Material>();
        static readonly HashSet<string> missing = new HashSet<string>();

        public static Texture2D Texture(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Art key cannot be empty", nameof(key));
            if (textures.TryGetValue(key, out var value) && value) return value;
            value = Resources.Load<Texture2D>("V8Art/" + key);
            if (!value)
            {
                // A clean source checkout includes a complete, deterministic greybox fallback.
                // Real Resources/V8Art textures always take precedence when artists add them.
                if (missing.Add(key)) Debug.Log("EARLY_RUSH_PLACEHOLDER V8Art/" + key);
                value = EarlyRush.V9.RushPlaceholderArt.Create(key);
            }
            textures[key] = value;
            return value;
        }

        public static UnityEngine.Sprite Sprite(string key)
        {
            if (sprites.TryGetValue(key, out var sprite) && sprite) return sprite;
            var texture = Texture(key);
            if (!texture) return null;
            // Task sprites have distinct transparent margins; anchor their solid footprint.
            var pivot = key == "tasks/backpack" ? new Vector2(.5f, .04f)
                : key == "tasks/chair" ? new Vector2(.492f, .055f)
                : new Vector2(.5f, .1f);
            sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                pivot, 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "V8Art/" + key;
            sprites[key] = sprite;
            return sprite;
        }

        public static int SortOrder(float contactY)
        {
            // Sorting order is a short in Unity. Leave layers below/above world art for floor and HUD.
            return Mathf.Clamp(-Mathf.RoundToInt(contactY * 20f), -28000, 28000);
        }

        public static SpriteRenderer Create(Transform parent, string key, Vector2 pos, float width, int? layer = null)
        {
            var sprite = Sprite(key);
            if (!sprite) return null;
            var obj = new GameObject("Art " + key);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = new Vector3(pos.x, pos.y, 0);
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = layer ?? SortOrder(pos.y);
            SetWidth(sr, width);
            return sr;
        }

        public static void SetWidth(SpriteRenderer sr, float width)
        {
            if (!sr || !sr.sprite) return;
            float scale = Mathf.Max(.001f, width) / Mathf.Max(.001f, sr.sprite.bounds.size.x);
            sr.transform.localScale = new Vector3(scale, scale, 1);
        }

        public static void SetPosition(SpriteRenderer sr, Vector2 position, int? layer = null)
        {
            if (!sr) return;
            sr.transform.localPosition = new Vector3(position.x, position.y, 0);
            sr.sortingOrder = layer ?? SortOrder(position.y);
        }

        public static void SetFacing(SpriteRenderer sr, string prefix, Vector2 direction, float width)
        {
            if (!sr || direction.sqrMagnitude < .0001f) return;
            string facing = Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                ? (direction.x > 0 ? "right" : "left") : (direction.y > 0 ? "up" : "down");
            var next = Sprite(prefix + facing);
            if (!next) return;
            if (sr.sprite != next) { sr.sprite = next; SetWidth(sr, width); }
        }

        /// <summary>One floor mesh/material for all cells of a material; one unit per grid cell.
        /// UV uses world coordinates to keep adjacent cell and module seams aligned.</summary>
        public static MeshRenderer CreateFloor(Transform parent, IEnumerable<Vector2Int> cells,
            string key, float tileSize = 4f, int layer = FloorOrder)
        {
            var texture = Texture(key);
            if (!texture) return null;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            tileSize = Mathf.Max(.1f, tileSize);
            foreach (var cell in cells)
            {
                int start = vertices.Count;
                float x = cell.x - .5f, y = cell.y - .5f;
                vertices.Add(new Vector3(x, y, 0)); vertices.Add(new Vector3(x + 1, y, 0));
                vertices.Add(new Vector3(x + 1, y + 1, 0)); vertices.Add(new Vector3(x, y + 1, 0));
                uv.Add(new Vector2(x, y) / tileSize); uv.Add(new Vector2(x + 1, y) / tileSize);
                uv.Add(new Vector2(x + 1, y + 1) / tileSize); uv.Add(new Vector2(x, y + 1) / tileSize);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
            }
            var obj = new GameObject("Floor " + key);
            obj.transform.SetParent(parent, false);
            var mesh = new Mesh { name = "V8 combined floor " + key };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<V8OwnedMesh>().Value = mesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            if (!floors.TryGetValue(key, out var mat) || !mat)
            {
                var shader = Shader.Find("Unlit/Texture");
                if (!shader) throw new InvalidOperationException("Required Unlit/Texture shader missing");
                mat = new Material(shader) { name = "V8 floor " + key, mainTexture = texture };
                floors[key] = mat;
            }
            renderer.sharedMaterial = mat;
            renderer.sortingOrder = layer;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }
    }

    // Dynamic scene resets release each combined mesh. Sprites/materials remain shared for the application lifetime.
    public sealed class V8OwnedMesh : MonoBehaviour
    {
        public Mesh Value;
        void OnDestroy() { if (Value) Destroy(Value); }
    }
}
