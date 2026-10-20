#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Outpost.Player;

// Perintah diagnosa & perbaikan tambahan (dinding, aset baru, dsb).
public static partial class OutpostBuilder
{
    static string RunFixCmd(string a0)
    {
        if (a0 == "setupwaves") return SetupWavesAudio();
        if (a0 == "gendiag") return GenDiag();
        if (a0 == "genfit") return GenFit();
        if (a0 == "setupgenbar") return SetupGenBar();
        if (a0 == "wavetest") return OutpostWaveTest.Start();
        if (a0 == "diagbuild") return DiagBuildings();
        if (a0 == "bones") return DiagBones();
        if (a0 == "diagtower") return DiagTower();
        if (a0 == "diagfloor") return DiagFloor();
        if (a0.StartsWith("refshot")) return RefShot(a0.Length > 8 ? a0.Substring(8) : "");
        if (a0.StartsWith("fpshot ")) return FpShot(a0.Substring(7));
        if (a0 == "procscan") return ProcScan();
        if (a0.StartsWith("setpose ")) return SetPose(a0.Substring(8));
        if (a0.StartsWith("wbounds ")) return WBounds(int.Parse(a0.Substring(8)));
        if (a0 == "scenestate") { var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); return sc.path + " dirty=" + sc.isDirty + " play=" + EditorApplication.isPlaying; }
        if (a0 == "savescene") { bool ok = UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes(); return "saved=" + ok; }
        if (a0 == "convprops") { var r = ConvertMaterialsIn("Assets/Industrial Props"); AssetDatabase.SaveAssets(); return r; }
        if (a0.StartsWith("showprefabs ")) return ShowPrefabs(a0.Substring(12));
        return null;
    }

    // render sudut pandang orang pertama dengan badan + IK: "run fpshot <slot> [ads] [yawOffset] [camBack]"
    static string FpShot(string arg)
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
        var t = arg.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        int idx = int.Parse(t[0]); bool ads = arg.Contains(" ads"); bool crouch = arg.Contains(" crouch");
        float back = 0f, side = 0f, up = 0f, xp = 0f, xy = 0f, amp = -1f, phase = -1f, air = -1f; string name = null;
        float armS = -1f; Vector3? wd = null; Vector3? gl = null, gr = null, aimL = null, pole = null, bo = null; float curlL = -999f, curlR = -999f;
        foreach (var x in t)
        {
            if (x.StartsWith("back=")) back = float.Parse(x.Substring(5));
            if (x.StartsWith("side=")) side = float.Parse(x.Substring(5));
            if (x.StartsWith("up=")) up = float.Parse(x.Substring(3));
            if (x.StartsWith("pitch=")) xp = float.Parse(x.Substring(6));
            if (x.StartsWith("yaw=")) xy = float.Parse(x.Substring(4));
            if (x.StartsWith("amp=")) amp = float.Parse(x.Substring(4));
            if (x.StartsWith("phase=")) phase = float.Parse(x.Substring(6));
            if (x.StartsWith("air=")) air = float.Parse(x.Substring(4));
            if (x.StartsWith("name=")) name = x.Substring(5);
            if (x.StartsWith("gl=")) gl = ParseV3(x.Substring(3));
            if (x.StartsWith("gr=")) gr = ParseV3(x.Substring(3));
            if (x.StartsWith("bo=")) bo = ParseV3(x.Substring(3));
            if (x.StartsWith("as=")) armS = float.Parse(x.Substring(3));
            if (x.StartsWith("wd=")) wd = ParseV3(x.Substring(3));
            if (x.StartsWith("curlL=")) curlL = float.Parse(x.Substring(6));
            if (x.StartsWith("curlR=")) curlR = float.Parse(x.Substring(6));
            if (x.StartsWith("aimL=")) aimL = ParseV3(x.Substring(5));
            if (x.StartsWith("pole=")) pole = ParseV3(x.Substring(5));
        }
        { var pl0 = GameObject.Find("Player_FPS"); if (pl0 != null) { var m0 = pl0.GetComponentInChildren<WeaponManager>(true); if (m0 != null && idx < m0.weapons.Length) ApplyPoseArgs(m0.weapons[idx], t); } }
        ShowWeapon(idx.ToString() + (ads ? " ads" : ""));
        var pl = GameObject.Find("Player_FPS"); if (pl == null) return "player tidak ada";
        var mgr = pl.GetComponentInChildren<WeaponManager>(true);
        typeof(WeaponManager).GetProperty("CurrentIndex").GetSetMethod(true).Invoke(mgr, new object[] { idx });
        var fb = pl.GetComponentInChildren<Outpost.Player.FpsBody>(true);
        if (fb == null) return "FpsBody tidak ada";
        var fc = pl.GetComponent<Outpost.Player.FpsController>();
        if (crouch && fc != null && fc.cameraRoot != null) { var lp = fc.cameraRoot.localPosition; lp.y = fc.crouchEyeHeight; fc.cameraRoot.localPosition = lp; }
        if (armS > 0f) fb.armScale = armS;
        fb.previewAmp = amp; fb.previewPhase = phase; fb.previewAir = air;
        var cw = mgr.Current;
        if (wd.HasValue && cw != null) cw.transform.localPosition += wd.Value;
        if (cw != null) { if (gl.HasValue && cw.gripLeft != null) cw.gripLeft.localPosition = gl.Value; if (gr.HasValue && cw.gripRight != null) cw.gripRight.localPosition = gr.Value; }
        if (curlL > -900f) fb.curlLeft = curlL; if (curlR > -900f) fb.curlRight = curlR;
        if (aimL.HasValue) fb.handAimLocal = aimL.Value;
        if (pole.HasValue) fb.poleOut = pole.Value;
        if (bo.HasValue) fb.bodyOffset = bo.Value;
        // pitch kamera pemain (mis. melihat ke bawah): putar cameraRoot
        if (fc != null && fc.cameraRoot != null && xp != 0f && name != null && name.StartsWith("look")) { fc.cameraRoot.localRotation = Quaternion.Euler(xp, 0, 0); xp = 0f; }
        fb.Pose(0f);
        string reach = fb.DebugReach();
        var pc = pl.GetComponentInChildren<Camera>(true);
        int W = 1600, H = 900;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var go = new GameObject("TMP_FPCAM"); var cam = go.AddComponent<Camera>();
        var cd = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); cd.renderPostProcessing = true;
        cam.transform.SetPositionAndRotation(pc.transform.position - pc.transform.forward * back + pc.transform.right * side + Vector3.up * up, pc.transform.rotation * Quaternion.Euler(xp, xy, 0f));
        cam.fieldOfView = pc.fieldOfView; cam.nearClipPlane = 0.03f; cam.farClipPlane = 400f; cam.targetTexture = rt; cam.clearFlags = CameraClearFlags.Skybox;
        cam.Render(); cam.Render();
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = null;
        string fn = name != null ? name : "fpshot_" + idx + (ads ? "ads" : "");
        File.WriteAllBytes(StatusDir + "/" + fn + ".png", tex.EncodeToPNG());
        Object.DestroyImmediate(go); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        string info = "fov " + pc.fieldOfView + " cam " + pc.transform.position.ToString("F2") + " body " + fb.body.position.ToString("F2") + " -> " + fn + "\n" + reach;
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);   // buang pose agar scene bersih
        return info;
    }

    // render scene referensi aset (Map_v1) untuk menyalin tampilan cahaya: "run refshot [x z yaw h pitch name]"
    static string RefShot(string arg)
    {
        const string refPath = "Assets/RPG_FPS_game_assets_industrial/Map_v1.unity";
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(refPath);
        var sb = new StringBuilder();
        Bounds all = new Bounds(); bool f = true;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) { if (f) { all = r.bounds; f = false; } else all.Encapsulate(r.bounds); }
        sb.AppendLine("bounds center " + all.center.ToString("F1") + " size " + all.size.ToString("F1"));
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) sb.AppendLine("light " + l.type + " rot " + l.transform.eulerAngles.ToString("F1") + " col " + l.color + " int " + l.intensity + " shadows " + l.shadows + " mode " + l.lightmapBakeType);
        sb.AppendLine("skybox " + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "null") + " ambientMode " + RenderSettings.ambientMode + " fog " + RenderSettings.fog + " " + RenderSettings.fogMode);
        var t = arg.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        float x = t.Length > 0 ? float.Parse(t[0]) : all.center.x, z = t.Length > 1 ? float.Parse(t[1]) : all.center.z;
        float yaw = t.Length > 2 ? float.Parse(t[2]) : 0f, h = t.Length > 3 ? float.Parse(t[3]) : 1.7f, pitch = t.Length > 4 ? float.Parse(t[4]) : 0f;
        string nm = t.Length > 5 ? t[5] : "ref";
        int W = 1600, H = 900;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var go = new GameObject("TMP_REFCAM"); var cam = go.AddComponent<Camera>();
        var cd = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); cd.renderPostProcessing = true;
        cam.transform.SetPositionAndRotation(new Vector3(x, h, z), Quaternion.Euler(pitch, yaw, 0));
        cam.fieldOfView = 70; cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f; cam.targetTexture = rt; cam.clearFlags = CameraClearFlags.Skybox;
        cam.Render(); cam.Render();
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = null;
        File.WriteAllBytes(StatusDir + "/" + nm + ".png", tex.EncodeToPNG());
        Object.DestroyImmediate(go); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
        File.WriteAllText(StatusDir + "/diag.txt", sb.ToString());
        return sb.ToString();
    }

    // cari permukaan datar yang saling bertumpuk di ketinggian hampir sama (penyebab z-fighting)
    static string DiagFloor()
    {
        var flat = new List<Renderer>();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var b = r.bounds;
            if (b.size.y < 0.45f && b.max.y < 0.5f && b.size.x * b.size.z > 1f && r.enabled) flat.Add(r);
        }
        var sb = new StringBuilder();
        sb.AppendLine("permukaan datar: " + flat.Count);
        int hits = 0;
        for (int i = 0; i < flat.Count; i++)
            for (int j = i + 1; j < flat.Count; j++)
            {
                Bounds a = flat[i].bounds, b = flat[j].bounds;
                float ox = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x), oz = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);
                if (ox <= 0.2f || oz <= 0.2f) continue;
                float dy = Mathf.Abs(a.max.y - b.max.y);
                if (dy < 0.025f) { hits++; if (hits < 60) sb.AppendLine(flat[i].name + " (" + flat[i].transform.parent?.name + ") top " + a.max.y.ToString("F3") + "  x  " + flat[j].name + " (" + flat[j].transform.parent?.name + ") top " + b.max.y.ToString("F3") + "  overlap " + ox.ToString("F1") + "x" + oz.ToString("F1") + " @ " + a.center.ToString("F0")); }
            }
        sb.AppendLine("tumpang tindih sama tinggi: " + hits);
        File.WriteAllText(StatusDir + "/diag.txt", sb.ToString());
        return sb.ToString();
    }

    static Vector3 ParseV3(string v) { var p = v.Split(','); return new Vector3(float.Parse(p[0]), float.Parse(p[1]), float.Parse(p[2])); }

    static string DiagTower()
    {
        var sb = new StringBuilder();
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tower/Models/Tower.FBX");
        if (pf == null) return "Tower.FBX null";
        foreach (var t in pf.GetComponentsInChildren<Transform>(true))
        {
            var mf = t.GetComponent<MeshFilter>(); var r = t.GetComponent<Renderer>();
            sb.AppendLine(t.name + " lp=" + t.localPosition.ToString("F2") + " lr=" + t.localEulerAngles.ToString("F0") + " ls=" + t.localScale.ToString("F2")
                + (mf != null && mf.sharedMesh != null ? " mesh=" + mf.sharedMesh.name + " v=" + mf.sharedMesh.vertexCount + " bnd=" + mf.sharedMesh.bounds.size.ToString("F2") : "")
                + (r != null ? " mats=" + r.sharedMaterials.Length : ""));
        }
        foreach (var g in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (g.name.StartsWith("Tower_"))
            {
                Bounds b = RBounds(g.gameObject);
                sb.AppendLine("SCENE " + g.name + " pos=" + g.position.ToString("F1") + " rot=" + g.eulerAngles.ToString("F0") + " scale=" + g.localScale.ToString("F2") + " bounds=" + b.size.ToString("F1") + " center=" + b.center.ToString("F1"));
            }
        File.WriteAllText(StatusDir + "/diag.txt", sb.ToString());
        return sb.ToString();
    }

    static string DiagBuildings()
    {
        var sb = new StringBuilder();
        foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            string n = go.name;
            if (!(n.StartsWith("Hangar") || n.StartsWith("Command_Post"))) continue;
            if (go.parent == null || !(go.parent.name == "Buildings" || go.parent.name.Contains("Build"))) { }
            var rs = go.GetComponentsInChildren<Renderer>(true);
            Bounds b = new Bounds(go.position, Vector3.zero); bool f = true;
            foreach (var r in rs) { if (f) { b = r.bounds; f = false; } else b.Encapsulate(r.bounds); }
            var cols = go.GetComponentsInChildren<Collider>(true);
            sb.AppendLine(n + " parent=" + (go.parent != null ? go.parent.name : "-") + " center=" + b.center.ToString("F1") + " size=" + b.size.ToString("F1") + " colliders=" + cols.Length + " rend=" + rs.Length);
            var mats = new HashSet<string>();
            foreach (var r in rs) foreach (var m in r.sharedMaterials) if (m != null) mats.Add(m.name + "[" + m.shader.name.Replace("Universal Render Pipeline/", "URP/") + (m.HasProperty("_Cull") ? " cull=" + m.GetFloat("_Cull") : "") + "]");
            sb.AppendLine("   " + string.Join(", ", mats));
        }
        File.WriteAllText(StatusDir + "/diag.txt", sb.ToString());
        return "diagbuild ok\n" + sb.ToString();
    }

    // "showprefabs <out> <p1;p2;...>" -> deretan prefab/model di lokasi jauh, dirender ke BuilderStatus/<out>.png
    static string ShowPrefabs(string args)
    {
        int sp = args.IndexOf(' ');
        string outName = args.Substring(0, sp); string list = args.Substring(sp + 1);
        var temps = new List<GameObject>(); var sb = new StringBuilder();
        Vector3 origin = new Vector3(400f, 0f, 400f);
        float x = 0f; float maxH = 0f;
        foreach (var raw in list.Split(';'))
        {
            string path = raw.Trim(); if (path.Length == 0) continue;
            if (!path.StartsWith("Assets/")) path = PackRoot + "/" + path;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) { sb.AppendLine("TIDAK ADA " + path); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.position = origin; go.transform.rotation = Quaternion.identity;
            Bounds b = RBounds(go);
            go.transform.position += new Vector3(origin.x + x - b.min.x, -b.min.y, 0f);
            Bounds nb = RBounds(go);
            sb.AppendLine(System.IO.Path.GetFileName(path) + " size " + nb.size.ToString("F2") + " pivot-to-min " + (go.transform.position - nb.min).ToString("F2") + " at x=" + x.ToString("F1"));
            x += nb.size.x + 1.5f; maxH = Mathf.Max(maxH, nb.size.y);
            temps.Add(go);
        }
        Physics.SyncTransforms();
        int w = 1800, h = 700;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        var cgo = new GameObject("TMP_SHOW"); var cam = cgo.AddComponent<Camera>();
        var cd = cgo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); cd.renderPostProcessing = true;
        cam.targetTexture = rt; cam.clearFlags = CameraClearFlags.Skybox; cam.nearClipPlane = 0.1f; cam.farClipPlane = 600f;
        cam.orthographic = false; cam.fieldOfView = 40f;
        float cx = origin.x + x * 0.5f; float dist = Mathf.Max(x * 0.5f * 1.2f / (Mathf.Tan(20f * Mathf.Deg2Rad) * (w / (float)h)), maxH * 2.2f);
        cgo.transform.position = new Vector3(cx, maxH * 0.5f, origin.z - dist);
        cgo.transform.rotation = Quaternion.LookRotation(new Vector3(cx, maxH * 0.45f, origin.z) - cgo.transform.position, Vector3.up);
        cam.Render();
        RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = null;
        File.WriteAllBytes(StatusDir + "/" + outName + ".png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex); Object.DestroyImmediate(rt); Object.DestroyImmediate(cgo);
        foreach (var t in temps) Object.DestroyImmediate(t);
        return sb.ToString() + outName + ".png";
    }

    static void DumpTree(Transform t, int depth, StringBuilder sb, Transform root)
    {
        sb.AppendLine(new string(' ', depth * 2) + t.name + " lp=" + t.localPosition.ToString("F3") + " wp=" + t.position.ToString("F3"));
        foreach (Transform c in t) DumpTree(c, depth + 1, sb, root);
    }
    static string DiagBones()
    {
        var pl = GameObject.FindGameObjectWithTag("Player"); if (pl == null) return "player tidak ada";
        var body = pl.transform.Find("Body"); var sb = new StringBuilder();
        DumpTree(body, 0, sb, body);
        File.WriteAllText(StatusDir + "/bones.txt", sb.ToString());
        return "bones ok";
    }

    // ---------- bangunan: material dua sisi (dinding tidak "hilang" bila dilihat dari dalam/belakang) ----------
    static string MakeBuildingsDoubleSided()
    {
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { PackRoot + "/Buildings" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null || !m.HasProperty("_Cull")) continue;
            m.SetFloat("_Cull", 0f);
            m.doubleSidedGI = true;
            EditorUtility.SetDirty(m); n++;
        }
        AssetDatabase.SaveAssets();
        return "material bangunan dibuat dua sisi: " + n;
    }

    // ---------- area bermain ----------
    static void BuildPlayArea()
    {
        var go = new GameObject("PlayArea");
        go.transform.SetParent(envRoot, false);
        var pa = go.AddComponent<Outpost.Env.PlayArea>();
        pa.min = PlayMin; pa.max = PlayMax;
    }

    // ---------- GUI: sprite dari aset "2D GUI Shooter + Icons" ----------
    const string GuiRoot = "Assets/2D Gui Shooter + Icons";
    static Sprite GuiSprite(string rel, Vector4? border = null)
    {
        string path = GuiRoot + "/" + rel;
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null && border.HasValue && ti.spriteBorder != border.Value)
        {
            ti.spriteBorder = border.Value; ti.SaveAndReimport();
        }
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sp == null) Log("PERINGATAN: sprite GUI tidak ada: " + path);
        return sp;
    }

    static void WireHudSkin(FpsHud hud)
    {
        hud.sprSlotBg = GuiSprite("Ui/Icon_32.png", new Vector4(70, 70, 70, 70));
        hud.sprSlotFrame = GuiSprite("Ui/Icon_24.png", new Vector4(80, 80, 80, 80));
        hud.sprCross = GuiSprite("Ui/Icon_15.png");
        hud.sprHitCross = GuiSprite("Ui/Icon_14.png");
        hud.sprHeadCross = GuiSprite("Ui/Icon_22.png");
        hud.sprBarBg = GuiSprite("Ui/Icon_34.png");
        hud.sprBarFill = GuiSprite("Ui/Icon_35.png");
        hud.sprAmmo = GuiSprite("Icons/Icon_48.png");
        hud.sprHealth = GuiSprite("Icons/Icon_56.png");
    }

    // ---------- generator ("reaktor") lebih terang & jelas ----------
    static void BrightenGenerator(GameObject body)
    {
        EnsureFolder(MatDir);
        foreach (var r in body.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var om = mats[i]; if (om == null) continue;
                string path = MatDir + "/M_genbright_" + om.name + ".mat";
                var nm = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (nm == null) { nm = new Material(om); AssetDatabase.CreateAsset(nm, path); }
                nm.CopyPropertiesFromMaterial(om);
                nm.SetColor("_BaseColor", new Color(2.3f, 2.3f, 2.3f, 1f));
                nm.SetFloat("_Smoothness", Mathf.Min(0.35f, om.HasProperty("_Smoothness") ? om.GetFloat("_Smoothness") : 0.3f));
                // sedikit emisi hangat dari tekstur itu sendiri supaya sisi yang membelakangi matahari tetap terbaca
                var tex = om.HasProperty("_BaseMap") ? om.GetTexture("_BaseMap") : null;
                nm.EnableKeyword("_EMISSION");
                nm.SetTexture("_EmissionMap", tex);
                nm.SetColor("_EmissionColor", new Color(0.30f, 0.27f, 0.22f));
                nm.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(nm);
                mats[i] = nm;
            }
            r.sharedMaterials = mats;
        }
    }

    // batas renderer sebuah senjata di ruang lokal senjata: "run wbounds <slot>"
    static string WBounds(int idx)
    {
        var pl = GameObject.Find("Player_FPS"); if (pl == null) return "player tidak ada";
        var mgr = pl.GetComponentInChildren<WeaponManager>(true);
        var w = mgr.weapons[idx]; bool was = w.gameObject.activeSelf; w.gameObject.SetActive(true);
        var sb = new StringBuilder();
        sb.AppendLine(w.name + " lp " + w.transform.localPosition.ToString("F3") + " lr " + w.transform.localEulerAngles.ToString("F1"));
        foreach (var r in w.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is LineRenderer) continue;
            var b = r.bounds; Vector3 mn = w.transform.InverseTransformPoint(b.min), mx = w.transform.InverseTransformPoint(b.max);
            sb.AppendLine(r.name + " world-bounds->local min " + Vector3.Min(mn, mx).ToString("F3") + " max " + Vector3.Max(mn, mx).ToString("F3"));
        }
        foreach (var n in new[] { "Muzzle", "GripR", "GripL" }) { var t = w.transform.Find(n); if (t != null) sb.AppendLine(n + " lp " + t.localPosition.ToString("F3")); }
        w.gameObject.SetActive(was);
        return sb.ToString();
    }

    // parameter pose senjata: hip= hipe= ads= adse= gl= gr= camgl= hands=1 ar= al= rr= rl= cr= cl= pr= pl=
    static void ApplyPoseArgs(FpsWeapon w, string[] tokens)
    {
        foreach (var x in tokens)
        {
            int eq = x.IndexOf('='); if (eq <= 0) continue;
            string k = x.Substring(0, eq), v = x.Substring(eq + 1);
            switch (k)
            {
                case "hip": w.hipPosition = ParseV3(v); break;
                case "hipe": w.hipEuler = ParseV3(v); break;
                case "ads": w.adsPosition = ParseV3(v); break;
                case "adse": w.adsEuler = ParseV3(v); break;
                case "gl": if (w.gripLeft != null) w.gripLeft.localPosition = ParseV3(v); break;
                case "gr": if (w.gripRight != null) w.gripRight.localPosition = ParseV3(v); break;
                case "camgl":
                    {
                        var par = w.transform.parent; var g = par.Find("GripL_Cam");
                        if (g == null) { g = new GameObject("GripL_Cam").transform; g.SetParent(par, false); }
                        g.localPosition = ParseV3(v); g.localRotation = Quaternion.identity; w.gripLeft = g;
                        break;
                    }
                case "hands": w.customHands = v == "1"; break;
                case "ar": w.aimR = ParseV3(v); break;
                case "al": w.aimL = ParseV3(v); break;
                case "rr": w.rollR = float.Parse(v); break;
                case "rl": w.rollL = float.Parse(v); break;
                case "cr": w.curlR = float.Parse(v); break;
                case "cl": w.curlL = float.Parse(v); break;
                case "pr": w.poleR = ParseV3(v); break;
                case "pl": w.poleL = ParseV3(v); break;
            }
        }
    }

    // terapkan pose ke scene yang sedang terbuka lalu simpan: "run setpose <slot> k=v ..."
    static string SetPose(string arg)
    {
        var t = arg.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        int idx = int.Parse(t[0]);
        var pl = GameObject.Find("Player_FPS"); if (pl == null) return "player tidak ada";
        var mgr = pl.GetComponentInChildren<WeaponManager>(true); var w = mgr.weapons[idx];
        ApplyPoseArgs(w, t);
        EditorUtility.SetDirty(w);
        if (w.gripLeft != null) EditorUtility.SetDirty(w.gripLeft);
        if (w.gripRight != null) EditorUtility.SetDirty(w.gripRight);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        bool ok = UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        return "setpose " + w.name + " tersimpan=" + ok;
    }
}
#endif