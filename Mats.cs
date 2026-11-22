using System.Collections.Generic;
using UnityEngine;

namespace ChinaFishing
{
    // Материалы и процедурные текстуры. Шаблоны материалов создаёт редактор (Resources/Mat_*),
    // чтобы нужные варианты шейдеров попали в сборку.
    public static class Mats
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Material litTemplate;

        static Material Template()
        {
            if (litTemplate == null) litTemplate = Resources.Load<Material>("Mat_Lit");
            return litTemplate;
        }

        public static Material Lit(Color c, float smooth = 0.15f, float metal = 0f)
        {
            string key = ((Color32)c).r + "_" + ((Color32)c).g + "_" + ((Color32)c).b + "_" + smooth + "_" + metal;
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            var t = Template();
            if (t != null) m = new Material(t);
            else
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                m = new Material(sh);
            }
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            cache[key] = m;
            return m;
        }

        public static Material Lit(int r, int g, int b, float smooth = 0.15f)
        {
            return Lit(new Color(r / 255f, g / 255f, b / 255f), smooth);
        }

        // Зелёный/коричневый цветной шум 64x64
        public static Texture2D NoiseTex(Color a, Color b, float scale, int size = 64, int seed = 0)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(seed + x / (float)size * scale, seed + y / (float)size * scale) * 0.6f
                            + Mathf.PerlinNoise(seed + x / (float)size * scale * 3f, y / (float)size * scale * 3f) * 0.4f;
                    tex.SetPixel(x, y, Color.Lerp(a, b, n));
                }
            tex.Apply(true);
            return tex;
        }

        // Бесшовная карта нормалей для воды
        public static Texture2D WaterNormal(int size = 128)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            var h = new float[size, size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size * Mathf.PI * 2f, v = y / (float)size * Mathf.PI * 2f;
                    h[x, y] = Mathf.Sin(u * 3f + Mathf.Sin(v * 2f)) * 0.5f + Mathf.Sin(v * 4f + Mathf.Cos(u * 3f)) * 0.35f + Mathf.Sin((u + v) * 6f) * 0.15f;
                }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = h[(x + 1) % size, y] - h[(x - 1 + size) % size, y];
                    float dy = h[x, (y + 1) % size] - h[x, (y - 1 + size) % size];
                    var n = new Vector3(-dx * 0.9f, -dy * 0.9f, 1f).normalized;
                    tex.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
                }
            tex.Apply(true);
            return tex;
        }

        public static Texture2D GrassBlade(int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) tex.SetPixel(x, y, clear);
            var rnd = new System.Random(5);
            for (int i = 0; i < 9; i++)
            {
                float bx = (float)rnd.NextDouble() * (size - 8) + 4, h = size * (0.45f + (float)rnd.NextDouble() * 0.5f), lean = ((float)rnd.NextDouble() - 0.5f) * 10f;
                for (int y = 0; y < h; y++)
                {
                    float t = y / h; int cx = Mathf.RoundToInt(bx + lean * t * t); int w = Mathf.Max(1, Mathf.RoundToInt(3f * (1f - t)));
                    var col = Color.Lerp(new Color(0.25f, 0.45f, 0.15f, 1f), new Color(0.6f, 0.8f, 0.3f, 1f), t);
                    for (int dx = -w; dx <= w; dx++) { int px = cx + dx; if (px >= 0 && px < size) tex.SetPixel(px, y, col); }
                }
            }
            tex.Apply(true);
            return tex;
        }
    }
}
