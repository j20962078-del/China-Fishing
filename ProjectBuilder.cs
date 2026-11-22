#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChinaFishing.EditorTools
{
    // Подготовка проекта и сборка. Все шаги повторяемы: повторный запуск ничего не ломает и не дублирует.
    // Вызывается из GitHub Actions (batchmode) или из меню Unity: China Fishing → ...
    public static class ProjectBuilder
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string RendererPath = "Assets/Settings/ChinaFishing_Renderer.asset";

        [MenuItem("China Fishing/1. Подготовить проект")]
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/Resources");
            Directory.CreateDirectory("Assets/Settings");
            Directory.CreateDirectory("Assets/Scenes");
            CheckData();
            CreateMaterials();
            SetupUrp();
            IncludeShaders();
            SetupPlayer();
            CreateScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("China Fishing: проект подготовлен.");
        }

        // Проверка целостности данных игры: 11 рыб, 37 предметов, все ссылки на наживки существуют
        static void CheckData()
        {
            if (GameData.Fish.Count != 11) Debug.LogError("China Fishing: рыб должно быть 11, сейчас " + GameData.Fish.Count);
            if (GameData.Items.Count != 37) Debug.LogError("China Fishing: предметов должно быть 37, сейчас " + GameData.Items.Count);
            foreach (var f in GameData.Fish)
                foreach (var b in f.baits)
                    if (GameData.GetItem(b) == null) Debug.LogError("China Fishing: у рыбы «" + f.name + "» неизвестная наживка: " + b);
        }

        static Material MakeMat(string path, string shaderName)
        {
            var sh = Shader.Find(shaderName);
            if (sh == null) { Debug.LogError("China Fishing: шейдер не найден: " + shaderName); return null; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else m.shader = sh;
            return m;
        }

        static void CreateMaterials()
        {
            var lit = MakeMat("Assets/Resources/Mat_Lit.mat", "Universal Render Pipeline/Lit");
            if (lit != null) EditorUtility.SetDirty(lit);

            var water = MakeMat("Assets/Resources/Mat_Water.mat", "Universal Render Pipeline/Lit");
            if (water != null)
            {
                water.SetFloat("_Surface", 1f);
                water.SetFloat("_Blend", 0f);
                water.SetFloat("_SrcBlend", 5f);
                water.SetFloat("_DstBlend", 10f);
                water.SetFloat("_ZWrite", 0f);
                water.SetOverrideTag("RenderType", "Transparent");
                water.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                water.EnableKeyword("_NORMALMAP");
                water.renderQueue = (int)RenderQueue.Transparent;
                const string flat = "Assets/Resources/waterflat.png";
                if (!File.Exists(flat))
                {
                    var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(0.5f, 0.5f, 1f, 1f));
                    tex.Apply();
                    File.WriteAllBytes(flat, tex.EncodeToPNG());
                    AssetDatabase.ImportAsset(flat);
                    var ti = AssetImporter.GetAtPath(flat) as TextureImporter;
                    if (ti != null) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
                }
                water.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(flat));
                EditorUtility.SetDirty(water);
            }

            var terr = MakeMat("Assets/Resources/Mat_Terrain.mat", "Universal Render Pipeline/Terrain/Lit");
            if (terr != null) EditorUtility.SetDirty(terr);
        }

        // ---------- URP: три набора настроек под режимы качества ----------
        static void SetProp(SerializedObject so, string name, object val)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("China Fishing: у URP нет поля " + name + " (пропущено)"); return; }
            switch (p.propertyType)
            {
                case SerializedPropertyType.Boolean: p.boolValue = (bool)val; break;
                case SerializedPropertyType.Float: p.floatValue = Convert.ToSingle(val); break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum: p.intValue = Convert.ToInt32(val); break;
            }
        }

        static UniversalRenderPipelineAsset MakeUrp(string name, UniversalRendererData rd, float renderScale, int msaa,
            bool shadows, int shadowRes, float shadowDist, bool soft)
        {
            string path = "Assets/Resources/" + name + ".asset";
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (rp == null)
            {
                rp = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(rp, path);
            }
            rp.supportsHDR = false;
            rp.msaaSampleCount = msaa;
            rp.renderScale = renderScale;
            rp.shadowDistance = shadowDist;
            var so = new SerializedObject(rp);
            SetProp(so, "m_MainLightShadowsSupported", shadows);
            SetProp(so, "m_MainLightShadowmapResolution", shadowRes);
            SetProp(so, "m_SoftShadowsSupported", soft);
            SetProp(so, "m_AdditionalLightsRenderingMode", 1);      // дополнительные огни — по вершинам (дёшево)
            SetProp(so, "m_RequireDepthTexture", false);
            SetProp(so, "m_RequireOpaqueTexture", false);
            SetProp(so, "m_ShadowCascadeCount", shadows ? 2 : 1);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rp);
            return rp;
        }

        static void SetupUrp()
        {
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rd == null)
            {
                rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rd, RendererPath);
            }
            MakeUrp("URP_Low", rd, 0.8f, 1, false, 512, 0f, false);
            var med = MakeUrp("URP_Medium", rd, 1f, 1, true, 1024, 35f, false);
            MakeUrp("URP_High", rd, 1f, 2, true, 2048, 70f, true);

            GraphicsSettings.defaultRenderPipeline = med;
            int cur = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = med;
            }
            QualitySettings.SetQualityLevel(cur, false);
        }

        static void IncludeShaders()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) { Debug.LogWarning("China Fishing: GraphicsSettings не найден"); return; }
            var so = new SerializedObject(assets[0]);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) { Debug.LogWarning("China Fishing: нет списка Always Included Shaders"); return; }
            string[] names = {
                "Universal Render Pipeline/Lit", "Universal Render Pipeline/Terrain/Lit", "Universal Render Pipeline/Unlit",
                "Skybox/Procedural", "Hidden/TerrainEngine/Details/BillboardWavingDoublePass",
                "Hidden/TerrainEngine/Details/WavingDoublePass", "Hidden/TerrainEngine/Details/Vertexlit",
                "Hidden/TerrainEngine/BillboardTree"
            };
            foreach (var n in names)
            {
                var sh = Shader.Find(n);
                if (sh == null) continue;
                bool exists = false;
                for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) { exists = true; break; }
                if (exists) continue;
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetupPlayer()
        {
            PlayerSettings.companyName = "ChinaFishing";
            PlayerSettings.productName = "China Fishing";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.chinafishing.game");
            PlayerSettings.bundleVersion = "0.2.0";
            if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.Vulkan });

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets != null && assets.Length > 0)
            {
                var so = new SerializedObject(assets[0]);
                var p = so.FindProperty("activeInputHandler");
                if (p != null) { p.intValue = 0; so.ApplyModifiedPropertiesWithoutUndo(); } // старый Input: сенсорный ввод и StandaloneInputModule
            }

            // подпись (для AAB / Google Play) — только из переменных окружения; секреты в проект не пишутся
            string ks = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PATH");
            if (!string.IsNullOrEmpty(ks) && File.Exists(ks))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = ks;
                PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
                PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("ANDROID_KEY_ALIAS");
                PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("ANDROID_KEY_PASS");
            }
        }

        static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(l.gameObject);
            var cam = Camera.main;
            if (cam == null)
            {
                var cg = new GameObject("Main Camera"); cg.tag = "MainCamera";
                cam = cg.AddComponent<Camera>(); cg.AddComponent<AudioListener>();
            }
            cam.transform.position = new Vector3(0, 10, -40);
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            var go = new GameObject("China Fishing");
            go.AddComponent<GameManager>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static BuildPlayerOptions Options(string path, bool aab)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = aab;
            return new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };
        }

        static void Run(string path, bool aab)
        {
            bool ok = false;
            try
            {
                Prepare();
                var report = BuildPipeline.BuildPlayer(Options(path, aab));
                ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
                Debug.Log("China Fishing: сборка " + (ok ? "успешна: " : "НЕ удалась: ") + path + " (" + report.summary.totalSize + " байт)");
            }
            catch (Exception e) { Debug.LogError("China Fishing: сбой сборки: " + e); }
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        [MenuItem("China Fishing/2. Собрать APK")]
        public static void BuildApk() { Run("build/Android/ChinaFishing.apk", false); }

        [MenuItem("China Fishing/3. Собрать AAB (Google Play)")]
        public static void BuildAab() { Run("build/Android/ChinaFishing.aab", true); }
    }
}
#endif
