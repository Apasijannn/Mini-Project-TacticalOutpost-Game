using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Outpost.PCG;

/// <summary>
/// Membuat gambar preview untuk kartu mode di layar CHOOSE YOUR OPERATION, dari map asli dengan pencahayaan asli
/// (tanpa lampu tambahan) dan post-processing yang sama dengan game:
///   - Mini Project : scene TacticalOutpost_Solo, sudut kamera seperti background main menu (MenuBackdrop) tapi lebih tinggi.
///   - PCG          : scene TacticalOutpost_PCG + Preview Arena dengan seed tetap (PreviewSeed), di-zoom ke area generator.
/// Hasil: Assets/TacticalOutpost/Gameplay/Resources/PCG/Preview_MiniProject.png dan Preview_PCG.png (Sprite).
/// </summary>
static class PcgPreviewCapture
{
    const string SoloPath = "Assets/Scenes/TacticalOutpost_Solo.unity";
    const string OutDir = "Assets/TacticalOutpost/Gameplay/Resources/PCG";
    const int Width = 1280, Height = 720;
    const int PreviewSeed = 500;

    [MenuItem("Tactical Outpost/PCG/Capture Mode Previews")]
    static void CaptureAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[PCG] Keluar dari Play Mode dulu."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var previous = SceneManager.GetActiveScene().path;
        Directory.CreateDirectory(OutDir);

        // 1) Mini Project: map industrial asli, sudut main menu dinaikkan agar layout terlihat dari atas.
        EditorSceneManager.OpenScene(SoloPath, OpenSceneMode.Single);
        Shoot(new Vector3(-58f, 52f, -62f), new Vector3(-6f, 2f, 2f), 50f, OutDir + "/Preview_MiniProject.png");

        // 2) PCG: arena seed tetap, kamera dari arah yang sama, di-zoom ke generator dan sekitarnya.
        EditorSceneManager.OpenScene(PcgEnvironment.ScenePath, OpenSceneMode.Single);
        var profile = Resources.Load<PcgProfile>(PcgProfile.ResourcePath);
        bool oldRandom = profile != null && profile.useRandomSeed; int oldSeed = profile != null ? profile.seed : 0;
        if (profile != null) { profile.useRandomSeed = false; profile.seed = PreviewSeed; }
        try
        {
            PcgSceneSetup.Preview();
            GameObject arena = null;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name.StartsWith("PCG_Arena PREVIEW")) arena = root;
            if (arena != null)
            {
                var giz = arena.GetComponent<PcgGizmos>(); var a = giz != null ? giz.arena : null;
                Vector3 focus = a != null ? a.generatorPos : Vector3.zero;
                Vector3 dir = new Vector3(-58f, 52f, -62f) - new Vector3(-6f, 2f, 2f);
                Shoot(focus + dir.normalized * 62f, focus, 50f, OutDir + "/Preview_PCG.png");
            }
            else Debug.LogWarning("[PCG] Preview arena gagal dibuat; Preview_PCG.png tidak diperbarui.");
        }
        finally
        {
            if (profile != null) { profile.useRandomSeed = oldRandom; profile.seed = oldSeed; }
            PcgSceneSetup.ClearPreview();
        }

        AssetDatabase.Refresh();
        foreach (var file in new[] { "Preview_MiniProject.png", "Preview_PCG.png" })
        {
            var importer = AssetImporter.GetAtPath(OutDir + "/" + file) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log("[PCG] Preview mode tersimpan di " + OutDir + " (Preview_MiniProject.png, Preview_PCG.png; seed PCG " + PreviewSeed + ").");
    }

    static void Shoot(Vector3 position, Vector3 target, float fov, string path)
    {
        var go = new GameObject("PCG Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov; cam.nearClipPlane = 0.2f; cam.farClipPlane = 500f;
        cam.clearFlags = CameraClearFlags.Skybox; cam.allowHDR = true; cam.allowMSAA = false;
        cam.cullingMask = ~((1 << 8) | (1 << 14) | (1 << 30));        // sama dengan kamera main menu
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));

        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
        rt.Create();
        // Render lewat SRP render request lalu baca sinkron dari GPU (Camera.Render + ReadPixels tidak stabil di D3D12).
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
            Debug.Log("[PCG] Render " + path);
        }
        else Debug.LogWarning("[PCG] GPU readback gagal untuk " + path);

        rt.Release(); Object.DestroyImmediate(rt);
        Object.DestroyImmediate(go);
    }
}
