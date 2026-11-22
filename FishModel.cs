using System.Collections.Generic;
using UnityEngine;

namespace ChinaFishing
{
    // Процедурные модели рыб: у каждого вида своя форма, окраска и особенности
    public static class FishModel
    {
        public static GameObject Build(FishDef f, float lengthM)
        {
            var root = new GameObject("Рыба_" + f.name);
            float L = lengthM;
            float Y = L * f.bodyScale.y / f.bodyScale.x, X = L * f.bodyScale.z / f.bodyScale.x;
            var top = Mats.Lit(f.color, 0.55f);
            var belly = Mats.Lit(f.belly, 0.55f);
            var dark = Mats.Lit(f.color * 0.55f, 0.4f);
            var black = Mats.Lit(15, 15, 15, 0.8f);
            var P = PrimitiveType.Sphere;

            WorldBuilder.Prim(P, root.transform, Vector3.zero, new Vector3(X, Y, L * 0.78f), top);
            WorldBuilder.Prim(P, root.transform, new Vector3(0, -Y * 0.2f, 0.02f * L), new Vector3(X * 0.92f, Y * 0.62f, L * 0.74f), belly);
            WorldBuilder.Prim(P, root.transform, new Vector3(0, 0, L * 0.33f), new Vector3(X * 0.9f, Y * 0.82f, L * 0.26f), top);
            // хвост и плавники
            WorldBuilder.Prim(PrimitiveType.Cube, root.transform, new Vector3(0, 0, -L * 0.47f), new Vector3(X * 0.1f, Y * 1.05f, L * 0.16f), dark, false, new Vector3(0, 0, 0));
            WorldBuilder.Prim(PrimitiveType.Cube, root.transform, new Vector3(0, Y * 0.52f, -L * 0.02f), new Vector3(X * 0.08f, Y * 0.42f, L * 0.24f), dark, false, new Vector3(-15, 0, 0));
            WorldBuilder.Prim(PrimitiveType.Cube, root.transform, new Vector3(X * 0.42f, -Y * 0.3f, L * 0.18f), new Vector3(X * 0.5f, Y * 0.05f, L * 0.1f), dark, false, new Vector3(0, 25, 0));
            WorldBuilder.Prim(PrimitiveType.Cube, root.transform, new Vector3(-X * 0.42f, -Y * 0.3f, L * 0.18f), new Vector3(X * 0.5f, Y * 0.05f, L * 0.1f), dark, false, new Vector3(0, -25, 0));
            // глаза
            float ey = Y * 0.12f, ez = L * 0.38f, es = Mathf.Max(0.012f, L * 0.03f);
            WorldBuilder.Prim(P, root.transform, new Vector3(X * 0.4f, ey, ez), Vector3.one * es, black);
            WorldBuilder.Prim(P, root.transform, new Vector3(-X * 0.4f, ey, ez), Vector3.one * es, black);

            switch (f.id)
            {
                case "catfish":
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        WorldBuilder.Prim(PrimitiveType.Cylinder, root.transform, new Vector3(sx * X * 0.4f, -Y * 0.12f, L * 0.52f), new Vector3(L * 0.012f, L * 0.1f, L * 0.012f), dark, false, new Vector3(75, sx * 12, 0));
                        WorldBuilder.Prim(PrimitiveType.Cylinder, root.transform, new Vector3(sx * X * 0.2f, -Y * 0.28f, L * 0.5f), new Vector3(L * 0.008f, L * 0.05f, L * 0.008f), dark, false, new Vector3(75, 0, 0));
                    }
                    break;
                case "sturgeon":
                case "beluga":
                    WorldBuilder.Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0, -Y * 0.05f, L * 0.5f), new Vector3(X * 0.28f, L * 0.08f, X * 0.28f), top, false, new Vector3(90, 0, 0));
                    for (int i = 0; i < 7; i++)
                        WorldBuilder.Prim(PrimitiveType.Cube, root.transform, new Vector3(0, Y * 0.46f, L * (0.3f - i * 0.085f)), new Vector3(X * 0.22f, Y * 0.14f, L * 0.05f), belly);
                    break;
                case "pike":
                    WorldBuilder.Prim(PrimitiveType.Cube, root.transform, new Vector3(0, -Y * 0.05f, L * 0.5f), new Vector3(X * 0.75f, Y * 0.25f, L * 0.12f), top);
                    for (int i = 0; i < 6; i++)
                        WorldBuilder.Prim(P, root.transform, new Vector3(X * 0.3f * (i % 2 == 0 ? 1 : -1), Y * 0.15f, L * (0.25f - i * 0.1f)), new Vector3(X * 0.1f, Y * 0.1f, L * 0.05f), belly);
                    break;
                case "perch":
                case "zander":
                    for (int i = 0; i < 5; i++)
                        WorldBuilder.Prim(P, root.transform, new Vector3(0, Y * 0.1f, L * (0.22f - i * 0.1f)), new Vector3(X * 1.02f, Y * 0.82f, L * 0.035f), dark);
                    break;
                case "ruffe":
                    for (int i = 0; i < 5; i++)
                        WorldBuilder.Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0, Y * 0.6f, L * (0.2f - i * 0.07f)), new Vector3(X * 0.05f, Y * 0.18f, X * 0.05f), dark);
                    break;
                case "black_amur":
                case "white_amur":
                case "carp":
                case "crucian":
                    WorldBuilder.Prim(P, root.transform, new Vector3(0, Y * 0.05f, L * 0.1f), new Vector3(X * 1.02f, Y * 0.9f, L * 0.2f), belly);
                    if (f.id == "carp")
                        foreach (float sx in new[] { -1f, 1f })
                            WorldBuilder.Prim(PrimitiveType.Cylinder, root.transform, new Vector3(sx * X * 0.3f, -Y * 0.15f, L * 0.48f), new Vector3(L * 0.008f, L * 0.03f, L * 0.008f), dark, false, new Vector3(90, 0, 0));
                    break;
            }
            return root;
        }
    }

    // Мини-просмотр 3D-рыбы в интерфейсе (рендер в текстуру)
    public class FishPreview : MonoBehaviour
    {
        static readonly Dictionary<string, FishPreview> map = new Dictionary<string, FishPreview>();
        static int counter;
        Camera cam; RenderTexture rt; Transform model; float spin;
        public RenderTexture Texture { get { return rt; } }

        public static FishPreview Get(FishDef f)
        {
            FishPreview p;
            if (map.TryGetValue(f.id, out p) && p != null) return p;
            var go = new GameObject("Просмотр_" + f.id);
            go.transform.position = new Vector3(3000f + counter * 20f, -600f, 0f);
            counter++;
            p = go.AddComponent<FishPreview>();
            p.rt = new RenderTexture(256, 160, 16, RenderTextureFormat.ARGB32);
            var m = FishModel.Build(f, 1f);
            m.transform.SetParent(go.transform, false);
            p.model = m.transform;
            var cg = new GameObject("Камера"); cg.transform.SetParent(go.transform, false);
            cg.transform.localPosition = new Vector3(0, 0.05f, -2.4f);
            p.cam = cg.AddComponent<Camera>();
            p.cam.fieldOfView = 28f; p.cam.targetTexture = p.rt;
            p.cam.clearFlags = CameraClearFlags.SolidColor;
            p.cam.backgroundColor = new Color(0.10f, 0.22f, 0.28f, 1f);
            p.cam.nearClipPlane = 0.1f; p.cam.farClipPlane = 10f; p.cam.depth = -20;
            p.cam.enabled = false;
            map[f.id] = p;
            return p;
        }

        void OnDestroy()
        {
            if (rt != null) { rt.Release(); Destroy(rt); }
            var dead = new List<string>();
            foreach (var kv in map) if (kv.Value == this) dead.Add(kv.Key);
            foreach (var k in dead) map.Remove(k);
        }

        public static void SetAllActive(bool on) { foreach (var p in map.Values) if (p != null) p.cam.enabled = on; }
        public void SetActive(bool on) { cam.enabled = on; }

        void Update()
        {
            if (cam != null && cam.enabled) { spin += Time.deltaTime * 35f; model.localRotation = Quaternion.Euler(8f, 90f + Mathf.Sin(spin * 0.03f) * 30f, 0); }
        }
    }

    // Иконки снастей, наживок и приманок, нарисованные кодом
    public static class IconFactory
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        const int S = 64;

        public static Texture2D Get(ItemDef d)
        {
            Texture2D t;
            if (cache.TryGetValue(d.id, out t)) return t;
            var px = new Color[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);
            Draw(px, d);
            t = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            t.SetPixels(px); t.Apply();
            cache[d.id] = t;
            return t;
        }

        static void Dot(Color[] p, float cx, float cy, float rx, float ry, Color c)
        {
            for (int y = Mathf.Max(0, (int)(cy - ry - 1)); y < Mathf.Min(S, (int)(cy + ry + 2)); y++)
                for (int x = Mathf.Max(0, (int)(cx - rx - 1)); x < Mathf.Min(S, (int)(cx + rx + 2)); x++)
                {
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) p[y * S + x] = c;
                }
        }

        static void Line(Color[] p, float x0, float y0, float x1, float y1, float w, Color c)
        {
            int n = (int)(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 2f) + 1;
            for (int i = 0; i <= n; i++) { float t = i / (float)n; Dot(p, Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), w, w, c); }
        }

        static void Fish(Color[] p, float cx, float cy, float len, float h, Color body, Color acc)
        {
            Dot(p, cx, cy, len, h, body);
            Dot(p, cx - len * 0.95f, cy, len * 0.35f, h * 0.9f, acc);
            Dot(p, cx + len * 0.55f, cy + h * 0.2f, 2f, 2f, Color.black);
        }

        static void Draw(Color[] p, ItemDef d)
        {
            var c = d.color; var dk = c * 0.6f; dk.a = 1f; var lt = Color.Lerp(c, Color.white, 0.5f);
            switch (d.cat)
            {
                case ItemCategory.Удочки:
                    Line(p, 8, 8, 56, 52, 2.2f, c); Line(p, 8, 8, 20, 18, 3.5f, dk); Dot(p, 22, 12, 6, 6, lt); Dot(p, 22, 12, 3, 3, dk);
                    Line(p, 56, 52, 56, 30, 0.8f, Color.white); break;
                case ItemCategory.Катушки:
                    Dot(p, 32, 32, 20, 20, c); Dot(p, 32, 32, 13, 13, dk); Dot(p, 32, 32, 5, 5, lt); Line(p, 32, 32, 54, 50, 2.5f, lt); Dot(p, 55, 51, 4, 4, dk); break;
                case ItemCategory.Лески:
                    for (int i = 0; i < 40; i++) { float t = i / 40f; Dot(p, 8 + t * 48, 32 + Mathf.Sin(t * 12f) * 14f, 2.2f, 2.2f, c); }
                    break;
                case ItemCategory.Крючки:
                    Line(p, 36, 56, 36, 24, 2f, c); for (int i = 0; i <= 14; i++) { float a = Mathf.PI * i / 14f; Dot(p, 26 + Mathf.Cos(a) * 10f, 24 - Mathf.Sin(a) * 10f + 0f, 2f, 2f, c); }
                    Dot(p, 36, 58, 4, 3, c); Line(p, 16, 24, 14, 32, 2f, c); Dot(p, 36, 58, 3, 3, c); break;
                case ItemCategory.Поплавки:
                    Line(p, 32, 6, 32, 20, 1.5f, dk); Dot(p, 32, 30, 8, 12, c); Dot(p, 32, 40, 7, 6, Color.white); Line(p, 32, 48, 32, 60, 1.2f, dk); break;
                case ItemCategory.Грузила:
                    Dot(p, 32, 28, 14, 14, c); Dot(p, 32, 28, 5, 5, lt * 0.5f); Line(p, 32, 6, 32, 16, 1.5f, Color.white); break;
                case ItemCategory.Оснастки:
                    Line(p, 32, 4, 32, 58, 1f, Color.white); Dot(p, 32, 16, 5, 8, c); Dot(p, 32, 38, 4, 4, Color.gray); Line(p, 32, 50, 32, 58, 2f, lt); Dot(p, 27, 58, 4, 3, lt); break;
                case ItemCategory.Наживки:
                    switch (d.family ?? d.id)
                    {
                        case "worm": for (int i = 0; i < 30; i++) { float t = i / 30f; Dot(p, 10 + t * 44, 32 + Mathf.Sin(t * 9f) * 10f, 3.5f, 3.5f, c); } break;
                        case "maggot": for (int i = 0; i < 4; i++) Dot(p, 18 + i * 8, 28 + (i % 2) * 8, 6, 3.5f, c); break;
                        case "bread": Dot(p, 32, 32, 20, 18, new Color(0.7f, 0.5f, 0.25f)); Dot(p, 32, 32, 16, 14, c); break;
                        case "corn": for (int i = 0; i < 6; i++) for (int j = 0; j < 3; j++) Dot(p, 22 + i * 4.5f, 22 + j * 10, 3.3f, 3.8f, c); break;
                        case "dough": Dot(p, 32, 30, 16, 12, c); Dot(p, 40, 36, 10, 9, c * 0.9f); break;
                        case "boilie": Dot(p, 22, 32, 9, 9, c); Dot(p, 40, 26, 9, 9, c * 0.9f); Dot(p, 38, 42, 9, 9, c * 1.1f); break;
                        case "fish_piece": Dot(p, 32, 32, 18, 12, c); Dot(p, 32, 32, 12, 7, lt); break;
                        default: Fish(p, 36, 32, 16, 8, c, dk); break;
                    }
                    break;
                case ItemCategory.Приманки:
                    if (d.id.StartsWith("lure_spinner")) { Line(p, 10, 32, 54, 32, 1.5f, Color.gray); Dot(p, 26, 32, 9, 5, c); Dot(p, 46, 32, 4, 4, dk); Dot(p, 54, 36, 3, 3, Color.gray); }
                    else if (d.id.StartsWith("lure_jig") || d.family == "lure_jig") { Dot(p, 20, 32, 7, 7, c); for (int i = 0; i < 4; i++) Dot(p, 32 + i * 6, 32 + Mathf.Sin(i) * 2, 5, 4, lt); }
                    else Fish(p, 34, 32, 18, 9, c, dk);
                    break;
                default:
                    Dot(p, 32, 30, 20, 18, c); for (int i = 0; i < 5; i++) Line(p, 14 + i * 9, 14, 14 + i * 9, 46, 1f, dk); for (int i = 0; i < 4; i++) Line(p, 14, 18 + i * 9, 50, 18 + i * 9, 1f, dk);
                    break;
            }
        }

        public static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Обычная: return new Color(0.8f, 0.85f, 0.85f);
                case Rarity.Необычная: return new Color(0.45f, 0.85f, 0.45f);
                case Rarity.Редкая: return new Color(0.4f, 0.65f, 1f);
                default: return new Color(1f, 0.7f, 0.2f);
            }
        }
    }
}
