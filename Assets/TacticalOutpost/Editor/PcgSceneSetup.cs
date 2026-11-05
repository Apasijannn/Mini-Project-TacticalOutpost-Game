using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Outpost.PCG;

/// <summary>
/// Membuat scene khusus mode PCG: Assets/Scenes/PCG/TacticalOutpost_PCG.unity.
/// Isinya disalin SEKALI dari map industrial (TacticalOutpost_Solo) lalu bebas diedit tanpa memengaruhi Mini Project:
///   PCG Environment
///   ├── Atmosphere        (directional light bulan + Global Volume; skybox/fog/ambient ada di Lighting settings scene)
///   ├── Templates         (generator + lampu merah, menara floodlight, lampu jalan) → disalin ke arena saat Play
///   └── Static Objects    (kosong; objek manual yang selalu ada di arena)
/// </summary>
[InitializeOnLoad]
static class PcgSceneSetup
{
    static PcgSceneSetup()
    {
        // Preview arena tidak boleh ikut ke Play Mode.
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingEditMode) ClearPreview(); };
    }

    const string SoloPath = "Assets/Scenes/TacticalOutpost_Solo.unity";
    const string LightingPath = "Assets/Scenes/PCG/TacticalOutpost_PCG_Lighting.lighting";

    [MenuItem("Tactical Outpost/PCG/Open PCG Environment Scene")]
    static void Open()
    {
        if (!File.Exists(PcgEnvironment.ScenePath)) { Create(); return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(PcgEnvironment.ScenePath, OpenSceneMode.Single);
    }

    [MenuItem("Tactical Outpost/PCG/Create PCG Environment Scene")]
    static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[PCG] Hentikan Play Mode dulu."); return; }
        if (File.Exists(PcgEnvironment.ScenePath))
        {
            Debug.LogWarning("[PCG] Scene sudah ada: " + PcgEnvironment.ScenePath + ". Hapus dulu bila ingin membuat ulang dari map industrial.");
            AddToBuildSettings();
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();

        var solo = SceneManager.GetSceneByPath(SoloPath);
        if (!solo.isLoaded) solo = EditorSceneManager.OpenScene(SoloPath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(solo);
        var rs = RenderSnapshot.Capture();
        var all = solo.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();

        var pcg = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(pcg);
        rs.Apply();

        var env = new GameObject("PCG Environment").AddComponent<PcgEnvironment>();
        env.profile = Resources.Load<PcgProfile>(PcgProfile.ResourcePath);

        // Atmosphere: hanya komponen Light (directional) dan Volume yang disalin, tanpa geometri map.
        env.atmosphere = Child(env.transform, "Atmosphere (cahaya bulan + post-processing)", Vector3.zero);
        var lightMap = new Dictionary<Light, Light>();
        foreach (var t in all)
        {
            if (!t.gameObject.activeInHierarchy) continue;
            var light = t.GetComponent<Light>();
            var volume = t.GetComponent<Volume>();
            bool isSun = light != null && light.enabled && light.type == LightType.Directional;
            bool isVolume = volume != null && volume.enabled;
            if (!isSun && !isVolume) continue;
            var copy = new GameObject(t.name);
            copy.layer = t.gameObject.layer;
            copy.transform.SetParent(env.atmosphere, false);
            copy.transform.SetPositionAndRotation(t.position, t.rotation);
            copy.transform.localScale = t.lossyScale;
            foreach (var c in t.GetComponents<Component>())
            {
                bool keep = (isSun && (c is Light || c.GetType().Name == "UniversalAdditionalLightData")) ||
                            (isVolume && (c is Volume || (c is Collider && !volume.isGlobal)));
                if (!keep) continue;
                var d = copy.GetComponent(c.GetType());
                if (d == null) d = copy.AddComponent(c.GetType());
                EditorUtility.CopySerialized(c, d);
            }
            if (isSun) lightMap[light] = copy.GetComponent<Light>();
        }
        if (rs.sun != null && lightMap.TryGetValue(rs.sun, out var sunCopy)) RenderSettings.sun = sunCopy;

        // Templates: objek map industrial yang sudah di-setup lampunya, diletakkan di luar arena supaya mudah diedit.
        env.templatesRoot = Child(env.transform, "Templates (diedit di sini, disalin ke arena saat Play)", new Vector3(0f, 0f, -160f));
        Transform redLights = all.FirstOrDefault(t => t.name == "Generator red security floodlights");
        Transform towerLights = all.FirstOrDefault(t => t.name == "Corner floodlights - four single heads");
        Transform lamp = all.FirstOrDefault(t => t.name.StartsWith("Roadside lamp") && t.GetComponentInChildren<Light>(true) != null)
                      ?? all.FirstOrDefault(t => t.name.Contains("StreetLamp2") && t.GetComponentInChildren<Light>(true) != null);
        env.generatorTemplate = Template(redLights != null ? redLights.parent : null, env.templatesRoot, new Vector3(0f, 0f, 0f), "Generator (objective + lampu merah)");
        env.towerTemplate = Template(towerLights != null ? towerLights.parent : null, env.templatesRoot, new Vector3(35f, 0f, 0f), "Corner Tower (floodlight)");
        env.lampTemplate = Template(lamp, env.templatesRoot, new Vector3(55f, 0f, 0f), "Wall Lamp");
        if (env.generatorTemplate != null)
            foreach (var l in env.generatorTemplate.GetComponentsInChildren<Light>(true))
            {
                // Di map industrial jangkauan 45 m; arena PCG lebih kecil → merah hanya di sekitar generator.
                l.range = Mathf.Min(l.range, 16f);
                l.intensity *= 0.3f;
            }

        env.staticObjects = Child(env.transform, "Static Objects (objek manual, selalu ada di arena)", Vector3.zero);

        string summary = "generator " + Name(env.generatorTemplate) + ", tower " + Name(env.towerTemplate) + ", lamp " + Name(env.lampTemplate) +
                         ", atmosphere " + env.atmosphere.childCount + " objek";
        Directory.CreateDirectory(Path.GetDirectoryName(PcgEnvironment.ScenePath));
        EditorSceneManager.SaveScene(pcg, PcgEnvironment.ScenePath);

        BakeEnvironmentLighting();
        AddToBuildSettings();
        EditorSceneManager.RestoreSceneManagerSetup(setup);
        AssetDatabase.SaveAssets();
        Debug.Log("[PCG] Scene PCG dibuat: " + PcgEnvironment.ScenePath + " (" + summary + ")");
    }

    // ------------------------------------------------------------------ preview arena di editor

    [MenuItem("Tactical Outpost/PCG/Preview Arena (seed dari Profile)")]
    internal static void Preview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(PcgEnvironment.ScenePath);
        if (!scene.isLoaded)
        {
            if (!File.Exists(PcgEnvironment.ScenePath)) { Create(); }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(PcgEnvironment.ScenePath, OpenSceneMode.Single);
        }
        ClearPreview();
        var env = PcgEnvironment.FindIn(scene);
        var profile = env != null && env.profile != null ? env.profile : Resources.Load<PcgProfile>(PcgProfile.ResourcePath);
        if (env == null || profile == null) { Debug.LogWarning("[PCG] PCG Environment / profile tidak ditemukan."); return; }
        SceneManager.SetActiveScene(scene);

        int block = ~0, probe = ~0;
        foreach (var n in new[] { "TransparentFX", "Ignore Raycast", "Water", "UI", "Player", "Enemy", "Marker" })
        {
            int l = LayerMask.NameToLayer(n);
            if (l >= 0) { block &= ~(1 << l); probe &= ~(1 << l); }
        }
        int ground = LayerMask.NameToLayer("Ground");
        if (ground >= 0) block &= ~(1 << ground);

        int seed = profile.useRandomSeed ? (System.Environment.TickCount & 0x7fffffff) : profile.seed;
        var template = new PcgArena
        {
            generatorTemplate = env.generatorTemplate, towerTemplate = env.towerTemplate, lampTemplate = env.lampTemplate,
            staticRoot = env.staticObjects, blockMask = block, probeMask = probe, preview = true,
        };
        var arena = PcgArenaBuilder.Build(profile, seed, template);
        arena.root.name = "PCG_Arena PREVIEW (seed " + seed + ", tidak disimpan)";
        foreach (var t in arena.root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
        Selection.activeGameObject = arena.root.gameObject;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log("[PCG] Preview arena seed " + seed + " (tanpa NavMesh/zombie). Tidak ikut tersimpan; hilang otomatis saat Play. " +
                  "Edit Templates / Static Objects lalu jalankan Preview lagi untuk melihat hasilnya.");
    }

    [MenuItem("Tactical Outpost/PCG/Clear Arena Preview")]
    internal static void ClearPreview()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name.StartsWith("PCG_Arena PREVIEW")) Object.DestroyImmediate(root);
        }
    }

    static string Name(GameObject go) { return go != null ? go.name : "-"; }

    static Transform Child(Transform parent, string name, Vector3 position)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = position;
        return t;
    }

    static GameObject Template(Transform source, Transform parent, Vector3 localPos, string name)
    {
        if (source == null) return null;
        var copy = Object.Instantiate(source.gameObject);
        copy.name = name;
        copy.transform.SetParent(parent, false);
        copy.transform.localPosition = localPos;
        copy.transform.localRotation = Quaternion.identity;
        copy.transform.localScale = source.lossyScale;
        copy.SetActive(true);
        foreach (var l in copy.GetComponentsInChildren<Light>(true)) { l.enabled = true; l.gameObject.SetActive(true); }
        return copy;
    }

    /// <summary>Ambient light + reflection dari skybox untuk scene PCG (tanpa lightmap, cepat).</summary>
    static void BakeEnvironmentLighting()
    {
        var scene = EditorSceneManager.OpenScene(PcgEnvironment.ScenePath, OpenSceneMode.Single);
        var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
        if (settings == null)
        {
            settings = new LightingSettings { name = "TacticalOutpost_PCG_Lighting", bakedGI = false, realtimeGI = false };
            AssetDatabase.CreateAsset(settings, LightingPath);
        }
        Lightmapping.lightingSettings = settings;
        Lightmapping.Bake();
        EditorSceneManager.SaveScene(scene);
    }

    static void AddToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == PcgEnvironment.ScenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(PcgEnvironment.ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("[PCG] " + PcgEnvironment.ScenePath + " ditambahkan ke Build Settings.");
    }

    /// <summary>Langit, fog, ambient, dan reflection dari map industrial.</summary>
    sealed class RenderSnapshot
    {
        Material skybox; Light sunLight;
        bool fog; Color fogColor; FogMode fogMode; float fogDensity, fogStart, fogEnd;
        AmbientMode ambientMode; Color ambientSky, ambientEquator, ambientGround, ambientLight; float ambientIntensity;
        DefaultReflectionMode reflectionMode; Texture customReflection; float reflectionIntensity; int reflectionBounces;
        public Light sun { get { return sunLight; } }

        public static RenderSnapshot Capture()
        {
            return new RenderSnapshot
            {
                skybox = RenderSettings.skybox, sunLight = RenderSettings.sun,
                fog = RenderSettings.fog, fogColor = RenderSettings.fogColor, fogMode = RenderSettings.fogMode,
                fogDensity = RenderSettings.fogDensity, fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance,
                ambientMode = RenderSettings.ambientMode, ambientSky = RenderSettings.ambientSkyColor, ambientEquator = RenderSettings.ambientEquatorColor,
                ambientGround = RenderSettings.ambientGroundColor, ambientLight = RenderSettings.ambientLight, ambientIntensity = RenderSettings.ambientIntensity,
                reflectionMode = RenderSettings.defaultReflectionMode, customReflection = RenderSettings.customReflectionTexture,
                reflectionIntensity = RenderSettings.reflectionIntensity, reflectionBounces = RenderSettings.reflectionBounces,
            };
        }

        public void Apply()
        {
            RenderSettings.skybox = skybox;
            RenderSettings.fog = fog; RenderSettings.fogColor = fogColor; RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity; RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.ambientMode = ambientMode; RenderSettings.ambientSkyColor = ambientSky; RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround; RenderSettings.ambientLight = ambientLight; RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.defaultReflectionMode = reflectionMode; RenderSettings.customReflectionTexture = customReflection;
            RenderSettings.reflectionIntensity = reflectionIntensity; RenderSettings.reflectionBounces = reflectionBounces;
        }
    }
}
