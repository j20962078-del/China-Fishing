using System.Collections.Generic;
using UnityEngine;

namespace ChinaFishing
{
    public class FishingSpot
    {
        public string name;
        public Vector3 pos;
        public float yaw;
        public bool fishing = true;
    }

    // Строит стартовую локацию целиком из кода: озеро, берег, растительность, деревню, мосты.
    // ВАЖНО: это «белая коробка»-основа. Модели из примитивов потом заменяются на настоящие по референсам.
    public class WorldBuilder : MonoBehaviour
    {
        public const float Size = 320f, MinY = -14f, HeightRange = 24f;
        public const float WaterLevel = 0f;
        public static WorldBuilder I;
        void OnDestroy() { if (I == this) I = null; }
        public Terrain terrain;
        public Transform water;
        public Transform root;
        public readonly List<FishingSpot> spots = new List<FishingSpot>();
        readonly List<Vector2> reedCenters = new List<Vector2>();
        public Vector3 spawn;
        Material waterMat;
        Texture2D waterNormal;
        Mesh coneMesh;

        // ---------------- ВЫСОТА МЕСТНОСТИ ----------------
        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        public static float H(float x, float z)
        {
            float d = Mathf.Sqrt((x / 95f) * (x / 95f) + ((z - 45f) / 62f) * ((z - 45f) / 62f));
            d += (Mathf.PerlinNoise(x * 0.03f + 100f, z * 0.03f) - 0.5f) * 0.22f;
            float land = Mathf.Lerp(0.55f, 1.7f, Smooth(1.0f, 1.35f, d))
                       + (Mathf.PerlinNoise(x * 0.05f, z * 0.05f + 30f) - 0.5f) * 0.7f * Smooth(1.05f, 1.4f, d);
            // ровные берега в месте моста (мост на высоте 1,95 м)
            land = Mathf.Lerp(land, 1.7f, Smooth(8f, 4f, Mathf.Abs(z + 40f)) * Smooth(27f, 21f, Mathf.Abs(x + 70f)));
            float r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            land += 6f * Smooth(115f, 158f, r) * (0.5f + Mathf.PerlinNoise(x * 0.02f + 9f, z * 0.02f + 4f) * 0.7f);

            float t = Smooth(1.0f, 0.62f, d);
            float lakeBed = -2f - 9f * Mathf.PerlinNoise(x * 0.035f + 7f, z * 0.035f + 3f);
            float h = Mathf.Lerp(land, lakeBed, t);

            // протока к западу от деревни (под мостом)
            float ch = Smooth(6.5f, 3f, Mathf.Abs(x + 70f)) * Smooth(-90f, -78f, z) * Smooth(14f, 4f, z);
            h = Mathf.Lerp(h, -2.5f, ch);

            // ровная площадка деревни
            float v = Smooth(0f, 18f, x) * Smooth(125f, 107f, x) * Smooth(-135f, -117f, z) * Smooth(-45f, -58f, z);
            h = Mathf.Lerp(h, 1.7f, v);
            return h;
        }

        public float Ground(float x, float z)
        {
            return terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        static bool OnPath(float x, float z)
        {
            var p = new Vector2(x, z);
            return DistToSegment(p, new Vector2(0, pierZ0 - 1f), new Vector2(28, -56)) < 1.6f ||
                   DistToSegment(p, new Vector2(28, -56), new Vector2(106, -56)) < 1.4f ||
                   DistToSegment(p, new Vector2(28, -56), new Vector2(-56, -40)) < 1.4f ||
                   DistToSegment(p, new Vector2(-84, -40), new Vector2(-110, -30)) < 1.4f;
        }

        bool Blocked(float x, float z)
        {
            if (Mathf.Abs(x) < 6f && z > pierZ0 - 8f && z < pierZ1 + 3f) return true;   // мостки
            if (Mathf.Abs(x + 70f) < 13f && z > -92f && z < 4f) return true;            // протока и мост
            if (x > 6f && x < 125f && z > -140f && z < -50f) return true;              // деревня
            if (OnPath(x, z)) return true;
            return false;
        }

        // ---------------- ПОСТРОЕНИЕ ----------------
        public void Build()
        {
            I = this;
            Random.InitState(20260921);
            root = new GameObject("Мир").transform;
            var env = new GameObject("Окружение").transform; env.SetParent(root, false);

            ComputePier();
            BuildSky();
            BuildTerrain();
            BuildWater();
            BuildPier(env);
            BuildBridge(env);
            BuildVillage(env);
            BuildVegetation(env);
            BuildShoreProps(env);
            DefineSpots();

            try { StaticBatchingUtility.Combine(env.gameObject); } catch { }
            float sz = pierZ0 - 4f;
            spawn = new Vector3(0f, Ground(0f, sz) + 0.3f, sz);
        }

        void BuildSky()
        {
            var sun = new GameObject("Солнце").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.84f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.7f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.52f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.17f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0032f;
            RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.9f);
            var sh = Shader.Find("Skybox/Procedural");
            if (sh != null)
            {
                var sky = new Material(sh);
                sky.SetFloat("_SunSize", 0.05f);
                sky.SetFloat("_AtmosphereThickness", 0.9f);
                sky.SetColor("_SkyTint", new Color(0.5f, 0.62f, 0.82f));
                RenderSettings.skybox = sky;
            }
        }

        void BuildTerrain()
        {
            const int res = 257;
            var td = new TerrainData();
            td.heightmapResolution = res;
            td.size = new Vector3(Size, HeightRange, Size);
            var hts = new float[res, res];
            float step = Size / (res - 1);
            for (int zi = 0; zi < res; zi++)
                for (int xi = 0; xi < res; xi++)
                    hts[zi, xi] = Mathf.Clamp01((H(-Size / 2 + xi * step, -Size / 2 + zi * step) - MinY) / HeightRange);
            td.SetHeights(0, 0, hts);

            var grass = new TerrainLayer { diffuseTexture = Mats.NoiseTex(new Color(0.22f, 0.4f, 0.12f), new Color(0.38f, 0.55f, 0.2f), 6f, 64, 1), tileSize = new Vector2(9, 9) };
            var sand = new TerrainLayer { diffuseTexture = Mats.NoiseTex(new Color(0.72f, 0.65f, 0.45f), new Color(0.85f, 0.78f, 0.58f), 9f, 64, 2), tileSize = new Vector2(7, 7) };
            var mud = new TerrainLayer { diffuseTexture = Mats.NoiseTex(new Color(0.28f, 0.21f, 0.14f), new Color(0.42f, 0.32f, 0.2f), 5f, 64, 3), tileSize = new Vector2(6, 6) };
            var dirt = new TerrainLayer { diffuseTexture = Mats.NoiseTex(new Color(0.45f, 0.36f, 0.24f), new Color(0.6f, 0.5f, 0.35f), 7f, 64, 4), tileSize = new Vector2(5, 5) };
            td.terrainLayers = new[] { grass, sand, mud, dirt };

            const int ar = 128;
            td.alphamapResolution = ar;
            var a = new float[ar, ar, 4];
            float astep = Size / ar;
            for (int zi = 0; zi < ar; zi++)
                for (int xi = 0; xi < ar; xi++)
                {
                    float wx = -Size / 2 + (xi + 0.5f) * astep, wz = -Size / 2 + (zi + 0.5f) * astep;
                    float h = H(wx, wz);
                    float n = Mathf.PerlinNoise(wx * 0.15f, wz * 0.15f);
                    float wG = 1f, wS = 0f, wM = 0f, wD = 0f;
                    if (h < 1.05f + n * 0.25f) { wS = Smooth(1.05f, 0.5f, h); wG = 1f - wS; }
                    if (h < -0.6f) { wM = Smooth(-0.6f, -1.8f, h); wS *= 1f - wM; wG *= 1f - wM; }
                    if (OnPath(wx, wz) && h > 0.3f) { wD = 0.9f; wG *= 0.1f; wS *= 0.1f; }
                    float sum = wG + wS + wM + wD; if (sum < 0.001f) { wG = 1; sum = 1; }
                    a[zi, xi, 0] = wG / sum; a[zi, xi, 1] = wS / sum; a[zi, xi, 2] = wM / sum; a[zi, xi, 3] = wD / sum;
                }
            td.SetAlphamaps(0, 0, a);

            // трава
            td.SetDetailResolution(256, 16);
            td.detailPrototypes = new[]
            {
                new DetailPrototype
                {
                    prototypeTexture = Mats.GrassBlade(), renderMode = DetailRenderMode.GrassBillboard, usePrototypeMesh = false,
                    minWidth = 0.9f, maxWidth = 1.5f, minHeight = 0.5f, maxHeight = 1.0f, noiseSpread = 0.4f,
                    healthyColor = new Color(0.7f, 0.9f, 0.55f), dryColor = new Color(0.75f, 0.75f, 0.4f)
                }
            };
            var det = new int[256, 256];
            float dstep = Size / 256f;
            for (int zi = 0; zi < 256; zi++)
                for (int xi = 0; xi < 256; xi++)
                {
                    float wx = -Size / 2 + (xi + 0.5f) * dstep, wz = -Size / 2 + (zi + 0.5f) * dstep;
                    float h = H(wx, wz);
                    if (h > 1.0f && !OnPath(wx, wz) && !(wx > 6f && wx < 125f && wz > -140f && wz < -50f && Mathf.PerlinNoise(wx * 0.2f, wz * 0.2f) < 0.5f))
                        det[zi, xi] = Mathf.PerlinNoise(wx * 0.07f + 3f, wz * 0.07f) > 0.35f ? Random.Range(1, 4) : 0;
                }
            td.SetDetailLayer(0, 0, 0, det);

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Ландшафт";
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(-Size / 2, MinY, -Size / 2);
            terrain = go.GetComponent<Terrain>();
            var tmat = Resources.Load<Material>("Mat_Terrain");
            if (tmat != null) terrain.materialTemplate = tmat;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 8;
            terrain.detailObjectDistance = Game.S.quality == 0 ? 35f : Game.S.quality == 1 ? 60f : 90f;
            terrain.detailObjectDensity = Game.S.quality == 0 ? 0.4f : Game.S.quality == 1 ? 0.7f : 1f;
            terrain.basemapDistance = 120f;
        }

        void BuildWater()
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            g.name = "Вода";
            g.transform.SetParent(root, false);
            g.transform.position = new Vector3(0, WaterLevel, 0);
            g.transform.localScale = new Vector3(Size / 10f, 1f, Size / 10f);
            Object.Destroy(g.GetComponent<Collider>());
            waterNormal = Mats.WaterNormal();
            waterMat = Resources.Load<Material>("Mat_Water");
            if (waterMat != null) waterMat = new Material(waterMat);
            else
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                waterMat = new Material(sh);
                waterMat.SetFloat("_Surface", 1f);
                waterMat.SetOverrideTag("RenderType", "Transparent");
                waterMat.SetInt("_SrcBlend", 5); waterMat.SetInt("_DstBlend", 10); waterMat.SetInt("_ZWrite", 0);
                waterMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                waterMat.EnableKeyword("_NORMALMAP");
                waterMat.renderQueue = 3000;
            }
            var col = new Color(0.10f, 0.32f, 0.36f, 0.78f);
            waterMat.SetColor("_BaseColor", col); waterMat.color = col;
            waterMat.SetFloat("_Smoothness", 0.95f);
            waterMat.SetFloat("_Metallic", 0f);
            waterMat.SetTexture("_BumpMap", waterNormal);
            waterMat.SetFloat("_BumpScale", 0.6f);
            waterMat.SetTextureScale("_BumpMap", new Vector2(60, 60));
            waterMat.SetTextureScale("_BaseMap", new Vector2(60, 60));
            g.GetComponent<Renderer>().sharedMaterial = waterMat;
            water = g.transform;
            g.AddComponent<WaterAnimator>().Init(waterMat);
        }

        // ---------------- ПРИМИТИВЫ ----------------
        public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 lp, Vector3 scale, Material m, bool collider = false, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(t);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp;
            g.transform.localScale = scale;
            if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
            g.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) { var c = g.GetComponent<Collider>(); if (c != null) Object.Destroy(c); }
            return g;
        }

        static GameObject Box(Transform parent, Vector3 lp, Vector3 scale, Material m, bool collider = false, Vector3? euler = null)
        {
            return Prim(PrimitiveType.Cube, parent, lp, scale, m, collider, euler);
        }

        static void Quad(List<Vector3> v, List<Vector3> nrm, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 center)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            var cen = (a + b + c + d) / 4f;
            if (Vector3.Dot(n, cen - center) < 0f) { var tmp = b; b = d; d = tmp; n = -n; }
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int k = 0; k < 4; k++) nrm.Add(n);
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        static Mesh RoofMesh(float w, float d, float h, float over)
        {
            float hx = w / 2f + over, hz = d / 2f + over;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            var cen = new Vector3(0, h * 0.4f, 0);
            Quad(v, n, t, new Vector3(-hx, 0, -hz), new Vector3(-hx, 0, hz), new Vector3(0, h, hz), new Vector3(0, h, -hz), cen);
            Quad(v, n, t, new Vector3(hx, 0, -hz), new Vector3(hx, 0, hz), new Vector3(0, h, hz), new Vector3(0, h, -hz), cen);
            float gz = d / 2f, gx = w / 2f;
            Quad(v, n, t, new Vector3(-gx, 0, -gz), new Vector3(gx, 0, -gz), new Vector3(0, h, -gz), new Vector3(0, h, -gz), cen);
            Quad(v, n, t, new Vector3(-gx, 0, gz), new Vector3(gx, 0, gz), new Vector3(0, h, gz), new Vector3(0, h, gz), cen);
            Quad(v, n, t, new Vector3(-hx, 0, -hz), new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), new Vector3(hx, 0, -hz), new Vector3(0, 5f, 0)); // низ
            var m = new Mesh { name = "Крыша" };
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0);
            return m;
        }

        Mesh ConeMesh()
        {
            if (coneMesh != null) return coneMesh;
            const int seg = 9;
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < seg; i++) { float a = i * Mathf.PI * 2f / seg; v.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0, Mathf.Sin(a) * 0.5f)); }
            int apexStart = v.Count;
            for (int i = 0; i < seg; i++) v.Add(new Vector3(0, 1, 0));
            for (int i = 0; i < seg; i++) { t.Add(i); t.Add(apexStart + i); t.Add((i + 1) % seg); }
            coneMesh = new Mesh { name = "Конус" };
            coneMesh.SetVertices(v); coneMesh.SetTriangles(t, 0); coneMesh.RecalculateNormals();
            return coneMesh;
        }

        GameObject Cone(Transform parent, Vector3 lp, Vector3 scale, Material m)
        {
            var g = new GameObject("Крона");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp; g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
            return g;
        }

        // ---------------- МОСТКИ ----------------
        static float pierZ0 = -24f, pierZ1 = 12f, pierY = 0.75f;

        // Мостки начинаются на суше (за 4 м до воды) и уходят на глубину
        void ComputePier()
        {
            float shore = -17f;
            for (float z = -45f; z < 30f; z += 0.5f) if (H(0f, z) < 0.25f) { shore = z; break; }
            pierZ0 = shore - 4f;
            pierZ1 = shore + 32f;
            float g = Mathf.Max(H(-1.3f, pierZ0), Mathf.Max(H(1.3f, pierZ0), H(0f, pierZ0)));
            pierY = Mathf.Clamp(g + 0.1f, 0.75f, 1.3f);
        }

        void BuildPier(Transform env)
        {
            var p = new GameObject("Мостки").transform; p.SetParent(env, false);
            var wood = Mats.Lit(150, 112, 72, 0.1f);
            var dark = Mats.Lit(95, 68, 42, 0.1f);
            float z0 = pierZ0, z1 = pierZ1, y = pierY, wdt = 2.4f, mid = (z0 + z1) / 2f, len = z1 - z0;
            var deck = new GameObject("Настил"); deck.transform.SetParent(p, false);
            var bc = deck.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, y - 0.06f, mid); bc.size = new Vector3(wdt, 0.12f, len);
            for (float z = z0; z < z1; z += 0.5f)
                Box(p, new Vector3(0, y, z + 0.25f), new Vector3(wdt, 0.1f, 0.46f), wood);
            Box(p, new Vector3(-wdt / 2 + 0.15f, y - 0.2f, mid), new Vector3(0.18f, 0.25f, len), dark);
            Box(p, new Vector3(wdt / 2 - 0.15f, y - 0.2f, mid), new Vector3(0.18f, 0.25f, len), dark);
            for (float z = z0 + 1f; z < z1; z += 3.5f)
                foreach (float sx in new[] { -1f, 1f })
                {
                    float px = sx * (wdt / 2 - 0.1f);
                    float bottom = Mathf.Min(-3f, H(px, z) - 0.5f);   // сваи уходят в дно
                    Prim(PrimitiveType.Cylinder, p, new Vector3(px, (y + 0.6f + bottom) / 2f, z), new Vector3(0.2f, (y + 0.6f - bottom) / 2f, 0.2f), dark);
                    Box(p, new Vector3(px, y + 0.6f, z), new Vector3(0.14f, 0.1f, 0.14f), dark);
                }
            foreach (float sx in new[] { -1f, 1f })
            {
                float px = sx * (wdt / 2 - 0.1f);
                Box(p, new Vector3(px, y + 0.95f, mid), new Vector3(0.07f, 0.07f, len), wood);
                Box(p, new Vector3(px, y + 0.5f, mid), new Vector3(0.05f, 0.05f, len), wood);
                var rail = new GameObject("Перила"); rail.transform.SetParent(p, false);
                var rc = rail.AddComponent<BoxCollider>();
                rc.center = new Vector3(px, y + 0.8f, mid + 1f); rc.size = new Vector3(0.1f, 1.2f, len - 2.4f); // вход свободен
            }
            // концевая площадка с ограждением
            Box(p, new Vector3(0, y + 0.01f, z1 + 0.6f), new Vector3(wdt + 1.2f, 0.1f, 1.2f), wood);
            var endCol = new GameObject("Площадка").AddComponent<BoxCollider>(); endCol.transform.SetParent(p, false);
            endCol.center = new Vector3(0, y - 0.05f, z1 + 0.6f); endCol.size = new Vector3(wdt + 1.2f, 0.1f, 1.2f);
            var endRail = new GameObject("Ограждение конца").AddComponent<BoxCollider>(); endRail.transform.SetParent(p, false);
            endRail.center = new Vector3(0, y + 0.8f, z1 + 1.25f); endRail.size = new Vector3(wdt + 1.2f, 1.2f, 0.1f);
            Prim(PrimitiveType.Cylinder, p, new Vector3(-0.9f, y + 0.55f, z0 + 3f), new Vector3(0.55f, 0.5f, 0.55f), Mats.Lit(70, 90, 120, 0.2f));
        }

        // ---------------- МОСТ ----------------
        const float BridgeY = 1.95f, BridgeZ = -40f, BridgeXa = -83f, BridgeXb = -57f;

        void BuildBridge(Transform env)
        {
            var b = new GameObject("Мост").transform; b.SetParent(env, false);
            var wood = Mats.Lit(140, 100, 62, 0.1f);
            var dark = Mats.Lit(88, 62, 38, 0.1f);
            float zc = BridgeZ, xa = BridgeXa, xb = BridgeXb, y = BridgeY;
            float len = xb - xa, cx = (xa + xb) / 2f, wd = 2.8f;
            var deck = new GameObject("Настил моста"); deck.transform.SetParent(b, false);
            var bc = deck.AddComponent<BoxCollider>();
            bc.center = new Vector3(cx, y - 0.08f, zc); bc.size = new Vector3(len, 0.16f, wd);
            for (float x = xa; x < xb; x += 0.5f) Box(b, new Vector3(x + 0.25f, y - 0.02f, zc), new Vector3(0.46f, 0.1f, wd), wood);
            foreach (float sz in new[] { -1f, 1f })
            {
                float pz = zc + sz * (wd / 2 - 0.08f);
                Box(b, new Vector3(cx, y - 0.2f, pz), new Vector3(len, 0.3f, 0.16f), dark);
                Box(b, new Vector3(cx, y + 0.95f, pz), new Vector3(len, 0.08f, 0.1f), wood);
                Box(b, new Vector3(cx, y + 0.5f, pz), new Vector3(len, 0.06f, 0.06f), wood);
                for (float x = xa + 0.5f; x < xb; x += 2.2f) Box(b, new Vector3(x, y + 0.45f, pz), new Vector3(0.12f, 1f, 0.12f), dark);
                var rail = new GameObject("Перила моста"); rail.transform.SetParent(b, false);
                var rc = rail.AddComponent<BoxCollider>();
                rc.center = new Vector3(cx, y + 0.8f, pz); rc.size = new Vector3(len - 0.6f, 1.3f, 0.12f);
            }
            for (float x = xa + 5f; x < xb - 4f; x += 5.5f)
                foreach (float sz in new[] { -1f, 1f })
                {
                    float bed = H(x, zc + sz * 1.1f);
                    float bottom = Mathf.Min(bed, -0.5f) - 0.3f;
                    Prim(PrimitiveType.Cylinder, b, new Vector3(x, (y + bottom) / 2f, zc + sz * 1.1f), new Vector3(0.28f, (y - bottom) / 2f, 0.28f), dark);
                }
        }

        // ---------------- ДЕРЕВНЯ ----------------
        void House(Transform parent, Vector3 pos, float yaw, float w, float d, float hgt, Color wall, Color roofC, int chimney)
        {
            var h = new GameObject("Дом").transform;
            h.SetParent(parent, false);
            h.position = pos; h.rotation = Quaternion.Euler(0, yaw, 0);
            var wm = Mats.Lit(wall, 0.1f);
            Box(h, new Vector3(0, 0.15f, 0), new Vector3(w + 0.25f, 0.4f, d + 0.25f), Mats.Lit(110, 108, 100, 0.05f));
            Box(h, new Vector3(0, hgt / 2f + 0.2f, 0), new Vector3(w, hgt, d), wm, true);
            var roof = new GameObject("Крыша"); roof.transform.SetParent(h, false);
            roof.transform.localPosition = new Vector3(0, hgt + 0.2f, 0);
            roof.AddComponent<MeshFilter>().sharedMesh = RoofMesh(w, d, w * 0.38f, 0.5f);
            roof.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(roofC, 0.15f);
            Box(h, new Vector3(0, 1.15f, d / 2f + 0.03f), new Vector3(0.95f, 1.9f, 0.1f), Mats.Lit(80, 52, 30, 0.1f));
            var glass = Mats.Lit(150, 190, 210, 0.85f);
            var frame = Mats.Lit(235, 235, 230, 0.2f);
            foreach (float sx in new[] { -1f, 1f })
            {
                Box(h, new Vector3(sx * w * 0.3f, 1.6f, d / 2f + 0.02f), new Vector3(0.95f, 0.95f, 0.08f), frame);
                Box(h, new Vector3(sx * w * 0.3f, 1.6f, d / 2f + 0.06f), new Vector3(0.75f, 0.75f, 0.05f), glass);
            }
            if (chimney > 0) Box(h, new Vector3(w * 0.25f, hgt + w * 0.3f, -d * 0.15f), new Vector3(0.55f, w * 0.5f, 0.55f), Mats.Lit(130, 70, 55, 0.05f));
            Box(h, new Vector3(0, 0.12f, d / 2f + 0.8f), new Vector3(1.6f, 0.2f, 1.2f), Mats.Lit(120, 118, 110, 0.05f));
        }

        void Fence(Transform parent, Vector3 a, Vector3 b)
        {
            var wood = Mats.Lit(150, 120, 80, 0.1f);
            var dir = b - a; float len = dir.magnitude; dir.y = 0; dir.Normalize();
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 1.6f));
            var seg = new GameObject("Забор"); seg.transform.SetParent(parent, false);
            var mid = (a + b) / 2f; mid.y = Ground(mid.x, mid.z);
            var bc = seg.AddComponent<BoxCollider>();
            seg.transform.position = mid; seg.transform.rotation = Quaternion.LookRotation(dir);
            bc.center = new Vector3(0, 0.55f, 0); bc.size = new Vector3(0.12f, 1.1f, len);
            for (int i = 0; i <= n; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)n);
                float gy = Ground(p.x, p.z);
                Box(parent, new Vector3(p.x, gy + 0.55f, p.z), new Vector3(0.12f, 1.1f, 0.12f), wood);
                if (i < n)
                {
                    var q = Vector3.Lerp(a, b, (i + 0.5f) / n);
                    float qy = Ground(q.x, q.z);
                    var rot = Quaternion.LookRotation(dir).eulerAngles;
                    Box(parent, new Vector3(q.x, qy + 0.85f, q.z), new Vector3(0.05f, 0.1f, len / n), wood, false, rot);
                    Box(parent, new Vector3(q.x, qy + 0.45f, q.z), new Vector3(0.05f, 0.1f, len / n), wood, false, rot);
                }
            }
        }

        void Garden(Transform parent, Vector3 c)
        {
            float gy = Ground(c.x, c.z);
            var soil = Mats.Lit(70, 48, 32, 0.05f);
            var green = Mats.Lit(70, 130, 50, 0.1f);
            var green2 = Mats.Lit(110, 160, 60, 0.1f);
            Box(parent, new Vector3(c.x, gy + 0.08f, c.z), new Vector3(5f, 0.2f, 3f), soil);
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 4; j++)
                    Prim(PrimitiveType.Sphere, parent, new Vector3(c.x - 2f + i, gy + 0.32f, c.z - 1.1f + j * 0.7f), new Vector3(0.45f, 0.32f, 0.45f), (i + j) % 2 == 0 ? green : green2);
        }

        void BuildVillage(Transform env)
        {
            var v = new GameObject("Деревня").transform; v.SetParent(env, false);
            var walls = new[] { new Color(0.62f, 0.45f, 0.28f), new Color(0.82f, 0.74f, 0.5f), new Color(0.45f, 0.6f, 0.7f), new Color(0.75f, 0.52f, 0.4f), new Color(0.85f, 0.85f, 0.78f) };
            var roofs = new[] { new Color(0.5f, 0.2f, 0.15f), new Color(0.32f, 0.34f, 0.38f), new Color(0.2f, 0.3f, 0.45f), new Color(0.42f, 0.25f, 0.15f) };
            var plots = new[]
            {
                new Vector3(26, 0, -72), new Vector3(52, 0, -70), new Vector3(78, 0, -74), new Vector3(102, 0, -72),
                new Vector3(24, 0, -102), new Vector3(52, 0, -104), new Vector3(80, 0, -100), new Vector3(104, 0, -104)
            };
            for (int i = 0; i < plots.Length; i++)
            {
                var c = plots[i]; c.y = Ground(c.x, c.z);
                float w = Random.Range(6f, 8f), d = Random.Range(5f, 7f);
                House(v, c, Random.Range(-4f, 4f), w, d, Random.Range(2.8f, 3.4f), walls[i % walls.Length], roofs[(i * 3) % roofs.Length], i % 2);
                float hw = 11f, hd = 10f;
                var tl = new Vector3(c.x - hw, 0, c.z - hd); var tr = new Vector3(c.x + hw, 0, c.z - hd);
                var bl = new Vector3(c.x - hw, 0, c.z + hd); var br = new Vector3(c.x + hw, 0, c.z + hd);
                Fence(v, tl, tr); Fence(v, tl, bl); Fence(v, tr, br);
                Fence(v, bl, new Vector3(c.x - 2.5f, 0, c.z + hd)); Fence(v, br, new Vector3(c.x + 2.5f, 0, c.z + hd));
                Garden(v, new Vector3(c.x + 3f, 0, c.z - 6.5f));
                if (i % 2 == 0) Shed(v, new Vector3(c.x - 7.5f, 0, c.z - 6f));
                if (i % 3 == 0) Woodpile(v, new Vector3(c.x - 5.5f, 0, c.z + 4.5f));
            }
            // колодец
            var wc = new Vector3(40, 0, -60); wc.y = Ground(wc.x, wc.z);
            var well = new GameObject("Колодец").transform; well.SetParent(v, false); well.position = wc;
            var stone = Mats.Lit(120, 118, 112, 0.05f); var wood = Mats.Lit(110, 78, 48, 0.1f);
            Prim(PrimitiveType.Cylinder, well, new Vector3(0, 0.5f, 0), new Vector3(1.6f, 0.5f, 1.6f), stone, true);
            foreach (float sx in new[] { -0.7f, 0.7f }) Box(well, new Vector3(sx, 1.5f, 0), new Vector3(0.12f, 2f, 0.12f), wood);
            var wr = new GameObject("Крыша колодца"); wr.transform.SetParent(well, false);
            wr.transform.localPosition = new Vector3(0, 2.4f, 0); wr.transform.localRotation = Quaternion.Euler(0, 90, 0);
            wr.AddComponent<MeshFilter>().sharedMesh = RoofMesh(1.8f, 1.6f, 0.7f, 0.2f);
            wr.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(100, 50, 35, 0.1f);
        }

        void Shed(Transform parent, Vector3 p)
        {
            p.y = Ground(p.x, p.z);
            var s = new GameObject("Сарай").transform; s.SetParent(parent, false); s.position = p; s.rotation = Quaternion.Euler(0, Random.Range(-10, 10), 0);
            Box(s, new Vector3(0, 1.15f, 0), new Vector3(3.2f, 2.3f, 2.8f), Mats.Lit(110, 90, 65, 0.05f), true);
            var r = new GameObject("Крыша"); r.transform.SetParent(s, false); r.transform.localPosition = new Vector3(0, 2.3f, 0);
            r.AddComponent<MeshFilter>().sharedMesh = RoofMesh(3.2f, 2.8f, 0.9f, 0.3f);
            r.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(70, 70, 75, 0.2f);
            Box(s, new Vector3(0, 0.9f, 1.42f), new Vector3(1.2f, 1.7f, 0.08f), Mats.Lit(70, 50, 32, 0.1f));
        }

        void Woodpile(Transform parent, Vector3 p)
        {
            p.y = Ground(p.x, p.z);
            var wp = new GameObject("Поленница").transform; wp.SetParent(parent, false); wp.position = p;
            var m = Mats.Lit(150, 110, 70, 0.05f);
            var col = new GameObject("Коллайдер").AddComponent<BoxCollider>(); col.transform.SetParent(wp, false);
            col.center = new Vector3(0, 0.5f, 0); col.size = new Vector3(2f, 1f, 0.8f);
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 5 - row; i++)
                    Prim(PrimitiveType.Cylinder, wp, new Vector3(-0.8f + i * 0.4f + row * 0.2f, 0.18f + row * 0.32f, 0), new Vector3(0.3f, 0.4f, 0.3f), m, false, new Vector3(0, 0, 90)).transform.localEulerAngles = new Vector3(90, 0, 0);
        }

        // ---------------- РАСТИТЕЛЬНОСТЬ ----------------
        void Tree(Transform parent, Vector3 p, float s, bool pine)
        {
            p.y = Ground(p.x, p.z) - 0.1f;
            var t = new GameObject(pine ? "Ель" : "Дерево").transform;
            t.SetParent(parent, false); t.position = p; t.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            var bark = Mats.Lit(pine ? new Color(0.30f, 0.20f, 0.12f) : new Color(0.38f, 0.28f, 0.2f), 0.05f);
            float g = Random.Range(0.85f, 1.15f);
            if (pine)
            {
                Prim(PrimitiveType.Cylinder, t, new Vector3(0, 1.5f * s, 0), new Vector3(0.45f * s, 1.5f * s, 0.45f * s), bark, true);
                var gm = Mats.Lit(new Color(0.10f * g, 0.30f * g, 0.14f * g), 0.1f);
                Cone(t, new Vector3(0, 1.8f * s, 0), new Vector3(4.2f * s, 3.2f * s, 4.2f * s), gm);
                Cone(t, new Vector3(0, 3.4f * s, 0), new Vector3(3.2f * s, 3f * s, 3.2f * s), gm);
                Cone(t, new Vector3(0, 5.0f * s, 0), new Vector3(2.2f * s, 2.8f * s, 2.2f * s), gm);
            }
            else
            {
                Prim(PrimitiveType.Cylinder, t, new Vector3(0, 1.7f * s, 0), new Vector3(0.5f * s, 1.7f * s, 0.5f * s), bark, true);
                var gm = Mats.Lit(new Color(0.20f * g, 0.42f * g, 0.14f * g), 0.1f);
                var gm2 = Mats.Lit(new Color(0.28f * g, 0.5f * g, 0.18f * g), 0.1f);
                Prim(PrimitiveType.Sphere, t, new Vector3(0, 4.4f * s, 0), new Vector3(4.2f * s, 3.4f * s, 4.2f * s), gm);
                Prim(PrimitiveType.Sphere, t, new Vector3(1.4f * s, 3.7f * s, 0.6f * s), new Vector3(2.8f * s, 2.4f * s, 2.8f * s), gm2);
                Prim(PrimitiveType.Sphere, t, new Vector3(-1.3f * s, 3.9f * s, -0.7f * s), new Vector3(3f * s, 2.6f * s, 3f * s), gm2);
            }
        }

        void Bush(Transform parent, Vector3 p, float s)
        {
            p.y = Ground(p.x, p.z);
            float g = Random.Range(0.8f, 1.2f);
            var m = Mats.Lit(new Color(0.18f * g, 0.38f * g, 0.13f * g), 0.1f);
            var b = new GameObject("Куст").transform; b.SetParent(parent, false); b.position = p;
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Sphere, b, new Vector3(Random.Range(-0.5f, 0.5f) * s, 0.45f * s, Random.Range(-0.5f, 0.5f) * s), new Vector3(1.2f, 0.9f, 1.2f) * s * Random.Range(0.8f, 1.1f), m);
        }

        void BuildVegetation(Transform env)
        {
            var veg = new GameObject("Растительность").transform; veg.SetParent(env, false);
            int trees = Game.S.quality == 0 ? 70 : Game.S.quality == 1 ? 110 : 160, placed = 0, guard = 0;
            var tpos = new List<Vector2>();
            while (placed < trees && guard++ < 6000)
            {
                float x = Random.Range(-150f, 150f), z = Random.Range(-150f, 150f);
                float h = H(x, z);
                if (h < 1.0f || Blocked(x, z)) continue;
                bool tooClose = false;
                foreach (var q in tpos) if ((q - new Vector2(x, z)).sqrMagnitude < 25f) { tooClose = true; break; }
                if (tooClose) continue;
                float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                if (edge < 100f && Random.value < 0.55f) continue;
                tpos.Add(new Vector2(x, z));
                Tree(veg, new Vector3(x, 0, z), Random.Range(0.9f, 1.5f), h > 4f || Random.value < 0.3f);
                placed++;
            }
            int bushes = 0; guard = 0;
            while (bushes < (Game.S.quality == 0 ? 50 : Game.S.quality == 1 ? 80 : 110) && guard++ < 3000)
            {
                float x = Random.Range(-150f, 150f), z = Random.Range(-150f, 150f);
                float h = H(x, z);
                if (h < 1.0f || Blocked(x, z)) continue;
                Bush(veg, new Vector3(x, 0, z), Random.Range(0.8f, 1.5f));
                bushes++;
            }
            BuildReeds(veg);
            BuildLilies(veg);
        }

        void BuildReeds(Transform parent)
        {
            var stalk = Mats.Lit(110, 130, 60, 0.1f);
            var head = Mats.Lit(90, 55, 35, 0.1f);
            int clusters = 0, guard = 0, maxC = Game.S.quality == 0 ? 10 : 16;
            while (clusters < maxC && guard++ < 5000)
            {
                float x = Random.Range(-100f, 100f), z = Random.Range(-30f, 112f);
                float h = H(x, z);
                if (h > -0.25f || h < -0.9f || Blocked(x, z)) continue;
                if (Mathf.PerlinNoise(x * 0.08f + 40f, z * 0.08f + 40f) < 0.55f) continue;
                reedCenters.Add(new Vector2(x, z));
                int count = Random.Range(7, 13);
                for (int i = 0; i < count; i++)
                {
                    float rx = x + Random.Range(-0.9f, 0.9f), rz = z + Random.Range(-0.9f, 0.9f);
                    if (H(rx, rz) > -0.1f || H(rx, rz) < -1.3f) continue;
                    float hh = Random.Range(0.8f, 1.5f);
                    var r = new GameObject("Камыш").transform; r.SetParent(parent, false);
                    float bed = H(rx, rz);
                    r.position = new Vector3(rx, bed, rz);
                    float len = (WaterLevel - bed) + hh;
                    r.rotation = Quaternion.Euler(Random.Range(-6f, 6f), 0, Random.Range(-6f, 6f));
                    Prim(PrimitiveType.Cylinder, r, new Vector3(0, len * 0.5f, 0), new Vector3(0.035f, len * 0.5f, 0.035f), stalk);
                    if (Random.value < 0.6f) Prim(PrimitiveType.Capsule, r, new Vector3(0, len + 0.1f, 0), new Vector3(0.075f, 0.16f, 0.075f), head);
                }
                clusters++;
            }
        }

        void BuildLilies(Transform parent)
        {
            var pad = Mats.Lit(50, 110, 45, 0.5f);
            var flower = Mats.Lit(245, 225, 235, 0.3f);
            int clusters = 0, guard = 0, maxC = Game.S.quality == 0 ? 6 : 11;
            while (clusters < maxC && guard++ < 5000)
            {
                float x = Random.Range(-90f, 90f), z = Random.Range(-10f, 105f);
                float h = H(x, z);
                if (h > -0.5f || h < -1.9f || Blocked(x, z)) continue;
                if (Mathf.PerlinNoise(x * 0.07f + 10f, z * 0.07f + 90f) < 0.58f) continue;
                int count = Random.Range(5, 10);
                for (int i = 0; i < count; i++)
                {
                    float rx = x + Random.Range(-2f, 2f), rz = z + Random.Range(-2f, 2f);
                    float hr = H(rx, rz);
                    if (hr > -0.4f || hr < -2.2f) continue;
                    float sz = Random.Range(0.28f, 0.5f);
                    Prim(PrimitiveType.Cylinder, parent, new Vector3(rx, 0.03f, rz), new Vector3(sz, 0.008f, sz), pad);
                    if (Random.value < 0.35f) Prim(PrimitiveType.Sphere, parent, new Vector3(rx, 0.08f, rz), new Vector3(0.13f, 0.09f, 0.13f), flower);
                }
                clusters++;
            }
        }

        void BuildShoreProps(Transform env)
        {
            var p = new GameObject("Камни").transform; p.SetParent(env, false);
            int rocks = 0, guard = 0;
            while (rocks < 60 && guard++ < 4000)
            {
                float x = Random.Range(-110f, 110f), z = Random.Range(-40f, 120f);
                float h = H(x, z);
                if (h < -0.4f || h > 0.9f || Blocked(x, z)) continue;
                float s = Random.Range(0.5f, 1.6f); float g = Random.Range(0.45f, 0.7f);
                var r = Prim(PrimitiveType.Sphere, p, new Vector3(x, Mathf.Max(h, -0.2f) + 0.15f * s, z),
                    new Vector3(s * Random.Range(1f, 1.5f), s * 0.7f, s * Random.Range(0.9f, 1.3f)), Mats.Lit(new Color(g, g, g * 0.95f), 0.08f), s > 1f);
                r.transform.rotation = Quaternion.Euler(Random.Range(-10, 10), Random.Range(0, 360), Random.Range(-10, 10));
                rocks++;
            }
        }

        // Идём от суши к воде и берём последнюю точку, где берег ещё выше воды
        bool ShoreAlong(Vector2 from, Vector2 toWater, out Vector3 pos, out float yaw)
        {
            pos = Vector3.zero; yaw = 0f;
            var dir = (toWater - from).normalized;
            float total = (toWater - from).magnitude;
            if (H(from.x, from.y) < 0.4f) return false;               // старт должен быть на суше
            Vector2 last = from;
            for (float d = 0f; d < total; d += 0.5f)
            {
                var q = from + dir * d;
                if (H(q.x, q.y) < 0.3f)
                {
                    // убедиться, что дальше действительно вода, а не ямка
                    var ahead = q + dir * 6f;
                    if (H(ahead.x, ahead.y) > -0.3f) { last = q; continue; }
                    pos = new Vector3(last.x, Ground(last.x, last.y) + 0.2f, last.y);
                    yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                    return true;
                }
                last = q;
            }
            return false;
        }

        void DefineSpots()
        {
            spots.Add(new FishingSpot { name = "Мостки", pos = new Vector3(0, pierY + 0.2f, pierZ1 - 2f), yaw = 0f });
            Vector3 p; float yaw;
            if (ShoreAlong(new Vector2(-30, -35), new Vector2(-30, 45), out p, out yaw)) spots.Add(new FishingSpot { name = "Южный берег", pos = p, yaw = yaw });
            if (ShoreAlong(new Vector2(130, 45), new Vector2(60, 45), out p, out yaw)) spots.Add(new FishingSpot { name = "Восточный пляж", pos = p, yaw = yaw });
            if (ShoreAlong(new Vector2(0, 150), new Vector2(0, 60), out p, out yaw)) spots.Add(new FishingSpot { name = "Северный берег", pos = p, yaw = yaw });
            if (reedCenters.Count > 0)
            {
                var best = reedCenters[0]; float bd = 1e9f;
                foreach (var rc in reedCenters) { float dd = (rc - new Vector2(-60, 45)).sqrMagnitude; if (dd < bd) { bd = dd; best = rc; } }
                var center = new Vector2(0, 45);
                var outward = (best - center).normalized;
                if (ShoreAlong(best + outward * 28f, best, out p, out yaw)) spots.Add(new FishingSpot { name = "Камышовая заводь", pos = p, yaw = yaw });
            }
            spots.Add(new FishingSpot { name = "Старый мост", pos = new Vector3(-70f, BridgeY + 0.2f, BridgeZ), yaw = 0f });
            var vp = new Vector3(40f, 0f, -52f); vp.y = Ground(vp.x, vp.z) + 0.2f;
            spots.Add(new FishingSpot { name = "Деревня (площадь)", pos = vp, yaw = 0f, fishing = false });
        }

        public void ApplyTerrainQuality()
        {
            if (terrain == null || Game.S == null) return;
            int q = Game.S.quality;
            terrain.detailObjectDistance = q == 0 ? 35f : q == 1 ? 60f : 90f;
            terrain.detailObjectDensity = q == 0 ? 0.4f : q == 1 ? 0.7f : 1f;
            terrain.heightmapPixelError = q == 0 ? 14f : q == 1 ? 8f : 5f;
            terrain.basemapDistance = q == 0 ? 60f : 120f;
        }

        // ---------------- КАРТА ----------------
        public Texture2D MakeMap(int res)
        {
            var tex = new Texture2D(res, res, TextureFormat.RGB24, false);
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float wx = (x / (float)res - 0.5f) * Size, wz = (y / (float)res - 0.5f) * Size;
                    float h = H(wx, wz);
                    Color c;
                    if (h < 0f) c = Color.Lerp(new Color(0.35f, 0.65f, 0.75f), new Color(0.05f, 0.2f, 0.4f), Mathf.Clamp01(-h / 10f));
                    else if (h < 0.9f) c = new Color(0.82f, 0.76f, 0.55f);
                    else c = Color.Lerp(new Color(0.35f, 0.58f, 0.25f), new Color(0.2f, 0.38f, 0.17f), Mathf.Clamp01((h - 1f) / 6f));
                    if (h > 0.9f && OnPath(wx, wz)) c = new Color(0.6f, 0.5f, 0.35f);
                    if (wx > 8f && wx < 120f && wz > -140f && wz < -55f && h > 0.9f && Mathf.PerlinNoise(wx * 0.3f, wz * 0.3f) > 0.78f) c = new Color(0.65f, 0.25f, 0.2f);
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return tex;
        }
    }

    public class WaterAnimator : MonoBehaviour
    {
        Material m; Vector2 off;
        public void Init(Material mat) { m = mat; }
        void Update()
        {
            if (m == null) return;
            off += new Vector2(0.004f, 0.0025f) * Time.deltaTime * 6f;
            m.SetTextureOffset("_BumpMap", off);
            m.SetTextureOffset("_BaseMap", off * 0.5f);
        }
    }
}
