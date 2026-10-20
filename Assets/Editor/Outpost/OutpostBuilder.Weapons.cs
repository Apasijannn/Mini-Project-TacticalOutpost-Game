#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Outpost.Player;

// Aset senjata/drop/radar tambahan: inventarisasi, konversi material, ikon.
public static partial class OutpostBuilder
{
    const string PistolPath = "Assets/Nokobot/Modern Guns - Handgun/_Prefabs/Handgun Black/M1911 Handgun_Black.prefab";
    const string KnifePath = "Assets/TrackerKnife/Prefabs/TrackerKnife.prefab";
    const string AmmoBoxPath = "Assets/AmmoBox/AmmoBox.FBX";
    const string MedkitPath = "Assets/GeeKay3D/First-Aid-Set/Assets/Prefabs/FirstAidKit_Red.prefab";

    static string ConvertNewAssets()
    {
        string r = "";
        foreach (var f in new[] { "Assets/Nokobot", "Assets/TrackerKnife", "Assets/AmmoBox", "Assets/GeeKay3D" })
            r += f + ": " + ConvertMaterialsIn(f) + "; ";
        AssetDatabase.SaveAssets();
        return r;
    }

    static void DumpObj(System.Text.StringBuilder sb, GameObject go, string title)
    {
        sb.AppendLine("== " + title + " scale " + go.transform.localScale);
        Bounds b = new Bounds(); bool first = true;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            sb.AppendLine("  renderer " + r.GetType().Name + " " + r.name + " c " + r.bounds.center.ToString("F3") + " s " + r.bounds.size.ToString("F3") + " mat " + (r.sharedMaterial != null ? r.sharedMaterial.name + "/" + r.sharedMaterial.shader.name : "null"));
        }
        sb.AppendLine("  TOTAL c " + b.center.ToString("F3") + " s " + b.size.ToString("F3"));
        foreach (var tr in go.GetComponentsInChildren<Transform>(true))
        {
            int depth = 0; var q = tr; while (q != go.transform) { q = q.parent; depth++; }
            sb.AppendLine("  " + new string(' ', depth) + tr.name + " pos " + tr.localPosition.ToString("F3") + " rot " + tr.localEulerAngles.ToString("F0") + " scl " + tr.localScale.ToString("F2"));
        }
        foreach (var c in go.GetComponentsInChildren<Component>(true)) if (c != null && !(c is Transform) && !(c is Renderer) && !(c is MeshFilter)) sb.AppendLine("  comp " + c.GetType().Name + " on " + c.name);
    }

    static string WeaponInfo()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(ConvertNewAssets());
        foreach (var kv in new[] { new[] { PistolPath, "pistol" }, new[] { KnifePath, "knife" }, new[] { AmmoBoxPath, "ammobox" }, new[] { MedkitPath, "medkit" }, new[] { "Assets/Nokobot/Modern Guns - Handgun/_Prefabs/Base Prefabs/M1911 Handgun Base.prefab", "pistolbase" }, new[] { "Assets/ArtStore3D/Insurgent (Urban)/Prefab/Ak47.prefab", "ak47" } })
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(kv[0]);
            if (go == null) { sb.AppendLine("TIDAK ADA " + kv[0]); continue; }
            DumpObj(sb, go, kv[1] + " " + kv[0]);
        }
        File.WriteAllText(Path.Combine(StatusDir, "weapons.txt"), sb.ToString());
        return "weaponinfo ok";
    }


    // ---------- ikon HUD dari model 3D ----------
    const string UiDir = "Assets/Outpost/UI";

    static Bounds WorldBounds(GameObject go)
    {
        Bounds b = new Bounds(); bool first = true;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // Render model dari samping (moncong ke kiri, seperti ikon loadout game FPS) -> PNG transparan putih-abu.
    static string RenderModelIcon(string modelPath, string outName, Vector3 euler, float scaleHint)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (prefab == null) return "TIDAK ADA " + modelPath;
        EnsureFolder(UiDir);
        var holder = new GameObject("TMP_ICON"); holder.transform.position = new Vector3(0, 3000, 0);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
        inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.Euler(euler);
        foreach (var a in inst.GetComponentsInChildren<Animator>(true)) a.enabled = false;
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
        var b = WorldBounds(inst);
        int w = 512, h = 256;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
        var camGo = new GameObject("TMP_ICONCAM");
        var cam = camGo.AddComponent<Camera>();
        var cd = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); cd.renderPostProcessing = false;
        cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.targetTexture = rt; cam.nearClipPlane = 0.01f; cam.farClipPlane = 100f;
        float aspect = (float)w / h;
        float halfZ = b.extents.z, halfY = b.extents.y;
        cam.orthographicSize = Mathf.Max(halfY, halfZ / aspect) * 1.12f;
        camGo.transform.position = b.center - Vector3.right * 20f;
        camGo.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
        // lampu khusus ikon (arah tetap) supaya hasil konsisten
        var lgo = new GameObject("TMP_ICONLIGHT"); var lt = lgo.AddComponent<Light>(); lt.type = LightType.Directional; lt.intensity = 1.0f; lt.color = Color.white; lt.shadows = LightShadows.None;
        lgo.transform.rotation = Quaternion.Euler(35f, 70f, 0f);
        cam.Render(); cam.Render();
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = null;
        var px = tex.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            float a = px[i].a / 255f;
            if (a < 0.02f) { px[i] = new Color32(255, 255, 255, 0); continue; }
            // siluet putih polos, gaya ikon di aset "2D GUI Shooter"
            px[i] = new Color32(255, 255, 255, px[i].a);
        }
        tex.SetPixels32(px); tex.Apply();
        string path = UiDir + "/" + outName + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex); Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo); Object.DestroyImmediate(lgo); Object.DestroyImmediate(holder);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null) { ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.mipmapEnabled = false; ti.alphaIsTransparency = true; ti.filterMode = FilterMode.Bilinear; ti.SaveAndReimport(); }
        return outName + " ok bounds " + b.size.ToString("F3");
    }

    static string WeaponIcons(string args)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(RenderModelIcon("Assets/ArtStore3D/Insurgent (Urban)/Prefab/Ak47.prefab", "icon_ak47", Vector3.zero, 1f));
        sb.AppendLine(RenderModelIcon(PistolPath, "icon_pistol", Vector3.zero, 1f));
        sb.AppendLine(RenderModelIcon(KnifePath, "icon_knife", Vector3.zero, 1f));
        return sb.ToString();
    }


    // tampilkan hanya satu senjata di scene (untuk preview di edit mode): "showweapon <slot 0-2> [ads]"
    static string ShowWeapon(string arg)
    {
        if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path != ScenePath) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
        var pl = GameObject.Find("Player_FPS"); if (pl == null) return "player tidak ada";
        var mgr = pl.GetComponentInChildren<WeaponManager>(true); if (mgr == null) return "WeaponManager tidak ada";
        var t = arg.Split(' ');
        int idx = int.Parse(t[0]); bool ads = t.Length > 1 && t[1] == "ads";
        for (int i = 0; i < mgr.weapons.Length; i++)
        {
            var w = mgr.weapons[i]; w.gameObject.SetActive(i == idx);
            if (i == idx) { w.transform.localPosition = ads ? w.adsPosition : w.hipPosition; w.transform.localRotation = Quaternion.Euler(ads ? Vector3.zero : w.hipEuler); }
        }
        return "showweapon " + idx + (ads ? " ads" : "") + " player " + pl.transform.position + " yaw " + pl.transform.eulerAngles.y;
    }

    static string ReloadScene()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
        return "scene dimuat ulang";
    }


    // render kamera radar ke PNG (uji layer, transparansi, depth): "run minimapshot"
    static string MinimapShot()
    {
        if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path != ScenePath) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
        var camGo = GameObject.Find("Cam_Map"); var pl = GameObject.Find("Player_FPS");
        if (camGo == null || pl == null) return "Cam_Map/player tidak ada";
        var cam = camGo.GetComponent<Camera>(); var rt = cam.targetTexture;
        if (rt == null) return "Cam_Map tanpa RenderTexture";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("cullingMask " + cam.cullingMask + " rt " + rt.width + "x" + rt.height + " depth " + rt.depth + " clear " + cam.clearFlags + " far " + cam.farClipPlane + " ortho " + cam.orthographic + " size " + cam.orthographicSize + " hdr " + cam.allowHDR);
        var ctl = Object.FindFirstObjectByType<Outpost.UI.MinimapController>(); Vector3 ctlC = ctl != null ? ctl.mapCenter : Vector3.zero; float ctlS = ctl != null ? ctl.largeCamSize : 82f; float ctlBoost = ctl != null ? ctl.sunBoost : 1f;
        var sunL = RenderSettings.sun; if (sunL == null) foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sunL = l; break; }
        float sunI = sunL != null ? sunL.intensity : 0f; if (sunL != null) sunL.intensity = sunI * (ctlBoost);
        var modes = new[] { new object[] { "small", pl.transform.position + Vector3.up * 24.5f, 27f }, new object[] { "large", new Vector3(ctlC.x, 24.5f, ctlC.z), ctlS } };
        foreach (var m in modes)
        {
            cam.transform.position = (Vector3)m[1]; cam.orthographicSize = (float)m[2];
            cam.Render(); cam.Render();
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = null;
            // komposit di atas latar abu agar transparansi terlihat; ukur persen piksel tak-transparan
            var px = tex.GetPixels32(); int opaque = 0;
            for (int i = 0; i < px.Length; i++) { if (px[i].a > 200) opaque++; }
            sb.AppendLine((string)m[0] + ": piksel solid " + (100f * opaque / px.Length).ToString("F1") + "%");
            File.WriteAllBytes(Path.Combine(StatusDir, "minimap_" + (string)m[0] + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        if (sunL != null) sunL.intensity = sunI;
        return sb.ToString();
    }


    // uji tata letak HUD + minimap: render scene + kedua canvas ke PNG. "run hudshot <slot> [large]"
    static string HudShot(string arg)
    {
        var t = arg.Split(' ');
        int idx = int.Parse(t[0]); bool large = arg.Contains("large");
        var info = ShowWeapon(idx.ToString());
        var pl = GameObject.Find("Player_FPS"); if (pl == null) return "player tidak ada";
        var mgr = pl.GetComponentInChildren<WeaponManager>(true);
        var hud = pl.GetComponent<FpsHud>();
        var fl = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        typeof(WeaponManager).GetProperty("CurrentIndex").GetSetMethod(true).Invoke(mgr, new object[] { idx });
        var oldHud = pl.transform.Find("PlayerHUD"); if (oldHud != null) Object.DestroyImmediate(oldHud.gameObject);
        typeof(FpsHud).GetMethod("Awake", fl).Invoke(hud, null);
        var slotsF = typeof(FpsHud).GetField("slots", fl);
        var slots = (System.Array)slotsF.GetValue(hud);
        for (int i = 0; i < slots.Length; i++) { var s = slots.GetValue(i); s.GetType().GetField("t").SetValue(s, i == idx ? 1f : 0f); }
        typeof(FpsHud).GetMethod("Update", fl).Invoke(hud, null);

        int W = 1920, H = 1080;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
        var pc = pl.GetComponentInChildren<Camera>(true);
        var go = new GameObject("TMP_HUDCAM"); var cam = go.AddComponent<Camera>();
        var cd = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); cd.renderPostProcessing = true;
        cam.transform.SetPositionAndRotation(pc.transform.position, pc.transform.rotation);
        cam.fieldOfView = 70f; cam.nearClipPlane = 0.03f; cam.farClipPlane = 400f; cam.targetTexture = rt; cam.clearFlags = CameraClearFlags.Skybox;

        var ctl = Object.FindFirstObjectByType<Outpost.UI.MinimapController>();
        var canv = new List<Canvas>();
        var hc = pl.transform.Find("PlayerHUD"); if (hc != null) canv.Add(hc.GetComponent<Canvas>());
        if (ctl != null) canv.Add(ctl.GetComponent<Canvas>());
        foreach (var c in canv) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases();

        string log = info + "\n";
        if (ctl != null)
        {
            var sunL = RenderSettings.sun; if (sunL == null) foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sunL = l; break; }
            float sunI = sunL != null ? sunL.intensity : 0f; if (sunL != null) sunL.intensity = sunI * ctl.sunBoost;
            typeof(Outpost.UI.MinimapController).GetMethod("Apply", fl).Invoke(ctl, new object[] { large ? 1f : 0f, true });
            ctl.mapCam.transform.position = large ? new Vector3(ctl.mapCenter.x, 24.5f, ctl.mapCenter.z) : pl.transform.position + Vector3.up * 24.5f;
            ctl.mapCam.Render(); ctl.mapCam.Render();
            if (sunL != null) sunL.intensity = sunI;
            // posisi ikon player di peta (seperti LateUpdate controller)
            typeof(Outpost.UI.MinimapController).GetMethod("LateUpdate", fl).Invoke(ctl, null);
            var rr = ctl.holder.rect; log += "holder " + rr.size + " map anchored " + ctl.map.anchoredPosition + " scale " + ctl.map.localScale + "\n";
        }
        cam.Render();
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(StatusDir, "hud_shot.png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
        return log + "hud_shot.png";
    }

    static string RunWeaponCmd(string a0)
    {
        if (a0.StartsWith("hudshot ")) return HudShot(a0.Substring(8));
        if (a0.StartsWith("showweapon ")) return ShowWeapon(a0.Substring(11));
        switch (a0)
        {
            case "weaponinfo": return WeaponInfo();
            case "weaponicons": return WeaponIcons("");
            case "reloadscene": return ReloadScene();
            case "minimapshot": return MinimapShot();
        }
        return RunFixCmd(a0);
    }
}
#endif
