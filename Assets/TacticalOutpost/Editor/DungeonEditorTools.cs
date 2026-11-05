using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Outpost.Dungeon;
using Outpost.PCG;

/// <summary>
/// Menu Tactical Outpost → Dungeon:
///   - Select Profile               : pilih Resources/Dungeon/Dungeon_Profile (parameter generator, diedit manual di Inspector)
///   - Preview Dungeon in Scene     : bangun dungeon (seed dari profile) di scene yang terbuka, tanpa Play. Gizmos tampil di Scene View.
///   - Clear Dungeon Preview
///   - Print ASCII Map              : cetak grid ke Console (# wall, . floor, S, G, E, B, i, o)
///   - Capture Mode Preview         : render gambar kartu MODE 03 → Resources/Dungeon/Preview_Dungeon.png
/// </summary>
static class DungeonEditorTools
{
    const string PreviewPrefix = "DUNGEON PREVIEW";
    const string OutDir = "Assets/TacticalOutpost/Gameplay/Resources/Dungeon";
    const int PreviewSeed = 500;

    [MenuItem("Tactical Outpost/Dungeon/Select Profile")]
    static void SelectProfile()
    {
        var p = Resources.Load<DungeonProfile>(DungeonProfile.ResourcePath);
        if (p == null) { Debug.LogWarning("[DUNGEON] Belum ada " + OutDir + "/Dungeon_Profile.asset. Buat lewat Assets > Create > Outpost > Dungeon Profile di folder itu."); return; }
        Selection.activeObject = p; EditorGUIUtility.PingObject(p);
    }

    [MenuItem("Tactical Outpost/Dungeon/Preview Dungeon in Scene")]
    static void PreviewMenu() { Preview(-1); }

    internal static GameObject Preview(int seedOverride)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[DUNGEON] Keluar dari Play Mode dulu."); return null; }
        ClearPreview();
        var profile = DungeonProfile.Load();
        int seed = seedOverride >= 0 ? seedOverride : profile.seed;
        var env = PcgEnvironment.FindIn(SceneManager.GetActiveScene());
        var ctx = new DungeonBuildContext
        {
            preview = true,
            generatorTemplate = env != null ? env.generatorTemplate : null,
            lampTemplate = env != null ? env.lampTemplate : null,
        };
        var helper = DungeonBuilder.CreateHelper(ctx);
        float tile = DungeonBuilder.ResolveTile(profile, helper);
        var layout = DungeonGenerator.Generate(profile, seed, profile.algorithm, DungeonBuilder.PropAllowed(profile, helper, tile));
        var world = DungeonBuilder.Build(layout, profile, ctx, helper, PreviewPrefix + " (seed " + seed + ", " + profile.algorithm + ")");
        foreach (var t in world.root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;   // tidak ikut tersimpan di scene
        Selection.activeGameObject = world.root.gameObject;
        Debug.Log("[DUNGEON] Preview seed " + seed + ": " + layout.rooms.Count + " room, valid " + layout.valid + ", " + world.gameObjectCount + " GameObject, tile " +
                  world.tile.ToString("0.00") + " m. Pilih root-nya untuk melihat Gizmos (leaf, room, corridor, path, spawn).");
        return world.root.gameObject;
    }

    [MenuItem("Tactical Outpost/Dungeon/Clear Dungeon Preview")]
    static void ClearPreview()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                if (root.name.StartsWith(PreviewPrefix)) Object.DestroyImmediate(root);
    }

    [MenuItem("Tactical Outpost/Dungeon/Print ASCII Map (seed dari Profile)")]
    static void PrintAscii()
    {
        var profile = DungeonProfile.Load();
        var layout = DungeonGenerator.Generate(profile, profile.seed);
        Debug.Log("[DUNGEON] Seed " + profile.seed + " " + profile.algorithm + " valid " + layout.valid + " rooms " + layout.rooms.Count + " BFS start-goal " + layout.startGoalPathLength + "\n" + DungeonGenerator.ToAscii(layout));
    }

    [MenuItem("Tactical Outpost/Dungeon/Capture Mode Preview")]
    static void Capture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[DUNGEON] Keluar dari Play Mode dulu."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var previous = SceneManager.GetActiveScene().path;
        Directory.CreateDirectory(OutDir);
        EditorSceneManager.OpenScene(PcgEnvironment.ScenePath, OpenSceneMode.Single);
        var env = PcgEnvironment.FindIn(SceneManager.GetActiveScene());
        if (env != null && env.staticObjects != null) env.staticObjects.gameObject.SetActive(false);
        try
        {
            var root = Preview(PreviewSeed);
            var giz = root != null ? root.GetComponent<DungeonGizmos>() : null;
            if (giz != null && giz.world != null)
            {
                var w = giz.world;
                Vector3 start = w.CellToWorld(w.layout.startPosition);
                Vector3 focus = Vector3.Lerp(start, w.CellToWorld(w.layout.goalPosition), 0.35f);
                Vector3 pos = focus + new Vector3(-0.35f, 1f, -0.75f).normalized * Mathf.Max(w.HalfX, w.HalfZ) * 1.15f;
                Shoot(pos, focus, 48f, OutDir + "/Preview_Dungeon.png");
            }
        }
        finally
        {
            ClearPreview();
            if (env != null && env.staticObjects != null) env.staticObjects.gameObject.SetActive(true);
        }
        AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath(OutDir + "/Preview_Dungeon.png") as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false; importer.maxTextureSize = 2048; importer.SaveAndReimport();
        }
        if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log("[DUNGEON] Preview kartu tersimpan: " + OutDir + "/Preview_Dungeon.png (seed " + PreviewSeed + ").");
    }

    static void Shoot(Vector3 position, Vector3 target, float fov, string path)
    {
        const int Width = 1280, Height = 720;
        var go = new GameObject("Dungeon Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov; cam.nearClipPlane = 0.2f; cam.farClipPlane = 800f;
        cam.clearFlags = CameraClearFlags.Skybox; cam.allowHDR = true; cam.allowMSAA = false;
        cam.cullingMask = ~((1 << 8) | (1 << 14) | (1 << 30));
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
        rt.Create();
        // Sama dengan PcgPreviewCapture: SRP render request + GPU readback (Camera.Render + ReadPixels tidak stabil di D3D12).
        var request = new RenderPipeline.StandardRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
        else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
        var readback = AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32);
        readback.WaitForCompletion();
        if (!readback.hasError)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.SetPixelData(readback.GetData<byte>(), 0); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        else Debug.LogWarning("[DUNGEON] GPU readback gagal untuk " + path);
        rt.Release(); Object.DestroyImmediate(rt);
        Object.DestroyImmediate(go);
    }
}
