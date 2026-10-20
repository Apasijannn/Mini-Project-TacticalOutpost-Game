#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using MiniMapModular;
using Outpost.Pickups;
using Outpost.Player;

// Senjata (3 slot), item drop, dan radar/minimap.
public static partial class OutpostBuilder
{
    const string RadarRoot = "Assets/Mini Mapa(Radar)";
    const string PickupDir = Root + "/Pickups";

    // ---------- ikon minimap buatan sendiri (putih, diwarnai lewat komponen) ----------
    static Sprite SaveIcon(string name, int size, System.Func<float, float, float> shape)
    {
        EnsureFolder(UiDir);
        string path = UiDir + "/" + name + ".png";
        if (!File.Exists(path))
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(shape(u, v));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null) { ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.mipmapEnabled = false; ti.alphaIsTransparency = true; ti.SaveAndReimport(); }
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static float Edge(float d, float soft) { return Mathf.Clamp01(-d / soft + 0.5f); }   // d<0 di dalam

    static Sprite dotSprite, crossSprite, boxSprite, genSprite;
    static void EnsureMinimapIcons()
    {
        const float k = 0.08f;
        dotSprite = SaveIcon("mm_dot", 64, (u, v) => Edge(Mathf.Sqrt(u * u + v * v) - 0.88f, k));
        crossSprite = SaveIcon("mm_cross", 64, (u, v) => { float a = Mathf.Max(Mathf.Abs(u) - 0.88f, Mathf.Abs(v) - 0.30f); float b = Mathf.Max(Mathf.Abs(u) - 0.30f, Mathf.Abs(v) - 0.88f); return Edge(Mathf.Min(a, b), k); });
        boxSprite = SaveIcon("mm_box", 64, (u, v) => { float d = Mathf.Max(Mathf.Abs(u) - 0.85f, Mathf.Abs(v) - 0.62f); float hole = Mathf.Max(Mathf.Abs(u) - 0.55f, Mathf.Abs(v) - 0.12f); return Mathf.Min(Edge(d, k), 1f - 0.65f * Edge(hole, k)); });
        genSprite = SaveIcon("mm_gen", 64, (u, v) => { float d = Mathf.Abs(u) + Mathf.Abs(v) - 0.92f; float hole = Mathf.Abs(u) + Mathf.Abs(v) - 0.34f; return Mathf.Min(Edge(d, k * 1.4f), 1f - 0.9f * Edge(hole, k * 1.4f)); });
    }

    static void AddBlip(GameObject go, Sprite icon, Color color, float sizeAdd, bool arrow, int layerView)
    {
        var b = go.AddComponent<Outpost.UI.MinimapBlip>();
        b.icon = icon; b.color = color; b.sizeAdd = sizeAdd; b.edgeArrow = arrow; b.layerIconView = layerView;
        b.arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RadarRoot + "/Icons/Arrows/Seta4.png");
        b.iconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RadarRoot + "/Prefab/Icon.prefab");
        b.arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RadarRoot + "/Prefab/Icon_Arrow.prefab");
        b.arrowIconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RadarRoot + "/Prefab/Arrow_Icon.prefab");
    }

    // ---------- senjata ----------
    static Sprite WeaponIconSprite(string file)
    {
        string path = UiDir + "/" + file + ".png";
        if (!File.Exists(path)) WeaponIcons("");
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void StripModel(GameObject inst)
    {
        foreach (var a in inst.GetComponentsInChildren<Animator>(true)) a.enabled = false;
        foreach (var a in inst.GetComponentsInChildren<Animation>(true)) { a.playAutomatically = false; a.enabled = false; }
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            var smr = r as SkinnedMeshRenderer; if (smr != null) smr.updateWhenOffscreen = true;
        }
        foreach (var c in inst.GetComponentsInChildren<Collider>(true)) c.enabled = false;
    }

    // Membuat satu slot senjata: holder (FpsWeapon) + model + muzzle/flash/tracer.
    static FpsWeapon MakeWeaponSlot(Transform parent, string name, GameObject modelPrefab, Vector3 modelPos, Vector3 modelEuler, float modelScale, Vector3? muzzlePos, out GameObject modelInst)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var w = go.AddComponent<FpsWeapon>();
        modelInst = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, go.transform);
        modelInst.name = "Model";
        modelInst.transform.localPosition = modelPos; modelInst.transform.localRotation = Quaternion.Euler(modelEuler); modelInst.transform.localScale = Vector3.one * modelScale;
        StripModel(modelInst);
        if (muzzlePos.HasValue)
        {
            var muzzle = new GameObject("Muzzle"); muzzle.transform.SetParent(go.transform, false);
            muzzle.transform.localPosition = muzzlePos.Value;
            var lightGo = new GameObject("MuzzleFlash"); lightGo.transform.SetParent(muzzle.transform, false);
            lightGo.transform.localPosition = new Vector3(0, 0, 0.12f);
            var lt = lightGo.AddComponent<Light>(); lt.type = LightType.Point; lt.color = new Color(1f, 0.7f, 0.35f); lt.intensity = 6f; lt.range = 9f; lt.shadows = LightShadows.None; lt.enabled = false;
            var tracerGo = new GameObject("Tracer"); tracerGo.transform.SetParent(go.transform, false);
            var lr = tracerGo.AddComponent<LineRenderer>();
            lr.positionCount = 2; lr.useWorldSpace = true; lr.widthMultiplier = 0.014f; lr.sharedMaterial = EnsureTracerMat();
            lr.shadowCastingMode = ShadowCastingMode.Off; lr.receiveShadows = false; lr.enabled = false; lr.numCapVertices = 2;
            w.muzzle = muzzle.transform; w.muzzleLight = lt; w.tracer = lr;
        }
        w.hitMask = ~((1 << LPlayer) | (1 << 2) | (1 << 5) | (1 << LMarker));
        return w;
    }

    static Transform MakeGrip(Transform parent, string name, Vector3 lp)
    {
        var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = lp; g.localRotation = Quaternion.identity;
        return g;
    }

    static WeaponManager BuildWeapons(Transform camTransform, FpsController ctrl)
    {
        var gunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharRoot + "/Prefab/Ak47.prefab");
        var pistolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PistolPath);
        if (gunPrefab == null || pistolPrefab == null) { Log("PREFAB SENJATA TIDAK LENGKAP"); return null; }
        Log("konversi material senjata: " + ConvertNewAssets());

        var wgo = new GameObject("Weapons"); wgo.transform.SetParent(camTransform, false);
        var mgr = wgo.AddComponent<WeaponManager>(); mgr.controller = ctrl;
        GameObject m;

        // slot 1: AK-47
        var ak = MakeWeaponSlot(wgo.transform, "AK47", gunPrefab, Vector3.zero, Vector3.zero, 1f, new Vector3(0f, 0.045f, 0.62f), out m);
        ak.controller = ctrl; ak.kind = WeaponKind.Rifle; ak.displayName = "AK-47"; ak.icon = WeaponIconSprite("icon_ak47"); ak.automatic = true;
        ak.damage = 28f; ak.roundsPerMinute = 600f; ak.magazineSize = 30; ak.reserveAmmo = 120; ak.maxReserve = 240; ak.ammoPerPickup = 60;
        ak.reloadTime = 2.1f; ak.hipSpread = 1.1f; ak.adsSpread = 0.25f; ak.noiseRadius = 28f;
        // pose ala PUBG: hip = senjata di kanan-bawah miring ke tengah, ADS = senjata di tengah
        ak.hipPosition = new Vector3(0.11f, -0.14f, 0.22f); ak.hipEuler = new Vector3(0f, -4f, 0f); ak.adsPosition = new Vector3(0f, -0.115f, 0.24f); ak.adsFov = 0.78f;
        ak.kickBack = 0.035f; ak.recoilUp = 0.9f; ak.recoilSide = 0.25f; ak.equipTime = 0.38f;
        ak.gripRight = MakeGrip(ak.transform, "GripR", new Vector3(0f, -0.085f, -0.075f)); ak.gripLeft = MakeGrip(ak.transform, "GripL", new Vector3(0f, -0.075f, 0.28f));   // tangan kiri di handguard

        // slot 2: pistol M1911 (model berasal dari Modern Guns - Handgun)
        var pi = MakeWeaponSlot(wgo.transform, "Pistol", pistolPrefab, new Vector3(0f, -0.105f, 0f), Vector3.zero, 1f, new Vector3(0f, -0.006f, 0.195f), out m);
        pi.controller = ctrl; pi.kind = WeaponKind.Pistol; pi.displayName = "M1911"; pi.icon = WeaponIconSprite("icon_pistol"); pi.automatic = false;
        pi.damage = 36f; pi.roundsPerMinute = 420f; pi.magazineSize = 12; pi.reserveAmmo = 48; pi.maxReserve = 96; pi.ammoPerPickup = 24;
        pi.reloadTime = 1.5f; pi.range = 160f; pi.hipSpread = 0.9f; pi.adsSpread = 0.15f; pi.noiseRadius = 20f;
        pi.hipPosition = new Vector3(0.07f, -0.11f, 0.32f); pi.hipEuler = new Vector3(0f, -2f, 0f); pi.adsPosition = new Vector3(0f, -0.025f, 0.26f); pi.adsFov = 0.88f;
        pi.kickBack = 0.05f; pi.recoilUp = 2.0f; pi.recoilSide = 0.4f; pi.equipTime = 0.26f; pi.slideTravel = 0.022f;
        pi.slide = FindDeep(m.transform, "Slider");
        pi.gripRight = MakeGrip(pi.transform, "GripR", new Vector3(0.025f, -0.114f, -0.093f)); pi.gripLeft = MakeGrip(pi.transform, "GripL", new Vector3(-0.045f, -0.1f, -0.06f));
        pi.adsPosition = new Vector3(0f, -0.03f, 0.30f);
        // genggam dua tangan ala PUBG: tangan kiri membungkus tangan kanan, jempol kanan di samping slide
        pi.customHands = true; pi.aimR = new Vector3(0f, 0.3f, 1f); pi.aimL = new Vector3(1f, -0.3f, 0.25f); pi.rollR = -75f; pi.rollL = 180f; pi.curlR = 80f; pi.curlL = 90f;

        // pisau sudah dihapus: hanya 2 slot (AK-47 dan pistol)
        mgr.weapons = new[] { ak, pi };
        mgr.startIndex = 0;
        WireWeaponAudio(ak, pi);
        return mgr;
    }

    // ---------- pickup (drop zombie) ----------
    static void FitModel(GameObject holder, GameObject inst, float targetMax)
    {
        inst.transform.localPosition = Vector3.zero;
        var b = WorldBounds(inst);
        float mx = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (mx < 1e-6f) { Log("PERINGATAN: bounds model nol: " + inst.name); return; }
        float f = targetMax / mx;
        inst.transform.localScale = inst.transform.localScale * f;   // pertahankan skala bawaan FBX, kalikan faktor
        b = WorldBounds(inst);
        inst.transform.localPosition = -(b.center - holder.transform.position);
    }

    static GameObject MakePickupPrefab(string name, PickupKind kind, string modelPath, Vector3 euler, float size, Color glow, Sprite blip, Color blipColor)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null) { Log("MODEL PICKUP TIDAK ADA: " + modelPath); return null; }
        EnsureFolder(PickupDir);
        var root = new GameObject(name); root.layer = LMarker;
        var vis = new GameObject("Visual"); vis.transform.SetParent(root.transform, false);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, vis.transform);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.name = "Model";
        foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var a in inst.GetComponentsInChildren<Animation>(true)) Object.DestroyImmediate(a);
        foreach (var a in inst.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        inst.transform.localRotation = Quaternion.Euler(euler);
        var b0 = WorldBounds(inst);
        FitModel(vis, inst, size);
        var b1 = WorldBounds(inst);
        Log("pickup " + name + ": bounds awal " + b0.size.ToString("F3") + " -> " + b1.size.ToString("F3"));
        SetLayerRecursive(root, LMarker);

        var lightGo = new GameObject("Glow"); lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        var lt = lightGo.AddComponent<Light>(); lt.type = LightType.Point; lt.color = glow; lt.intensity = 1.8f; lt.range = 3.6f; lt.shadows = LightShadows.None;

        var pk = root.AddComponent<PickupItem>();
        pk.kind = kind; pk.visual = vis.transform; pk.healAmount = 35f; pk.collectRadius = 1.7f; pk.lifetime = 45f; pk.hoverHeight = 0.55f;
        AddBlip(root, blip, blipColor, 0f, false, 0);

        string pp = PickupDir + "/" + name + ".prefab";
        var saved = PrefabUtility.SaveAsPrefabAsset(root, pp);
        Object.DestroyImmediate(root);
        Log("prefab pickup: " + pp);
        return saved;
    }

    static void BuildDrops()
    {
        EnsureMinimapIcons();
        var ammo = MakePickupPrefab("Pickup_Ammo", PickupKind.Ammo, AmmoBoxPath, Vector3.zero, 0.42f, new Color(1f, 0.82f, 0.3f), boxSprite, new Color(1f, 0.85f, 0.2f));
        var med = MakePickupPrefab("Pickup_Health", PickupKind.Health, MedkitPath, new Vector3(55f, 0f, 0f), 0.52f, new Color(0.35f, 1f, 0.45f), crossSprite, new Color(0.3f, 1f, 0.4f));
        var go = new GameObject("DropManager");
        var dm = go.AddComponent<DropManager>();
        dm.ammoPrefab = ammo; dm.healthPrefab = med; dm.normalDropChance = 0.30f; dm.maxActive = 14;
        dm.groundMask = ~((1 << LPlayer) | (1 << LEnemy) | (1 << 2) | (1 << 5) | (1 << LMarker));
        Log("DropManager ditempatkan (zombie biasa 30%, boss selalu ammo+medkit)");
    }

    // ---------- radar / minimap ----------
    static void BuildMinimap()
    {
        var player = GameObject.Find("Player_FPS");
        var mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RadarRoot + "/Prefab/Map_Status.prefab");
        var camPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RadarRoot + "/Prefab/Cam_Map.prefab");
        if (player == null || mapPrefab == null || camPrefab == null) { Log("MINIMAP: player/prefab radar tidak ditemukan"); return; }

        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RadarRoot + "/Textures/MiniMap.renderTexture");
        if (rt != null)
        {
            rt.Release(); rt.width = 1024; rt.height = 1024; rt.depth = 24; rt.antiAliasing = 1;
            EditorUtility.SetDirty(rt); AssetDatabase.SaveAssets();
            Log("RenderTexture minimap " + rt.width + "x" + rt.height + " depth " + rt.depth);
        }

        // canvas
        var cv = new GameObject("MinimapCanvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = cv.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
        var sc = cv.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
        var holder = new GameObject("MapHolder", typeof(RectTransform)); holder.transform.SetParent(cv.transform, false);
        var hr = (RectTransform)holder.transform; hr.anchorMin = Vector2.zero; hr.anchorMax = Vector2.one; hr.offsetMin = hr.offsetMax = Vector2.zero;
        var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(Image)); dimGo.transform.SetParent(holder.transform, false);
        var dr = (RectTransform)dimGo.transform; dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one; dr.offsetMin = dr.offsetMax = Vector2.zero;
        var dim = dimGo.GetComponent<Image>(); dim.color = new Color(0, 0, 0, 0); dim.raycastTarget = false;

        var mapInst = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab, holder.transform);
        var camInst = (GameObject)PrefabUtility.InstantiatePrefab(camPrefab);
        camInst.name = "Cam_Map";
        var ms = mapInst.GetComponentInChildren<Custom_MP>(true);
        var mp = camInst.GetComponent<MP>();
        var cam = camInst.GetComponent<Camera>();
        if (ms == null || mp == null || cam == null) { Log("MINIMAP: komponen aset tidak lengkap"); return; }
        var playerCam = player.GetComponentInChildren<Camera>(true);

        ms.Canvas_Control = canvas; ms.Cam_Player = playerCam; ms.WFT = null;
        mp.MS = ms; mp.Player_RY = player.transform; mp.Cam_Map = cam;
        mp.LayerSelect = ~((1 << LPlayer) | (1 << LEnemy) | (1 << LMarker) | (1 << 2) | (1 << 4) | (1 << 5));
        mp.CamFixed = 1; mp.CamSize = 27f; mp.Aux_ClipPlane_Far = 60f; mp.Min_Dis_Icon = 24f; mp.MMSize = 0.9f;
        mp.PosyMeLocal = new Vector3(0f, 24.5f, 0f);
        mp.Ray_On = 0;   // garis kisi bawaan aset dimatikan agar peta terbaca jelas

        cam.targetTexture = rt; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.allowHDR = false; cam.allowMSAA = false; cam.useOcclusionCulling = false; cam.depth = -10f; cam.orthographic = true;
        var cd = camInst.GetComponent<UniversalAdditionalCameraData>();
        if (cd == null) cd = camInst.AddComponent<UniversalAdditionalCameraData>();
        cd.renderPostProcessing = false; cd.renderShadows = false; cd.antialiasing = AntialiasingMode.None;
        cd.volumeLayerMask = 0; cd.requiresColorOption = CameraOverrideOption.Off; cd.requiresDepthOption = CameraOverrideOption.Off;

        // jalankan OnValidate milik aset (yang biasanya dijalankan Inspector) agar semua sprite/warna/pengaturan terpasang
        var ov = typeof(MP).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (ov != null) ov.Invoke(mp, null);

        var ctl = cv.AddComponent<Outpost.UI.MinimapController>();
        ctl.mp = mp; ctl.ms = ms; ctl.holder = hr; ctl.map = (RectTransform)mapInst.transform; ctl.mapCam = cam; ctl.player = player.transform; ctl.dim = dim;
        // peta besar: lingkaran memuat seluruh isi pagar outpost (≈95 x 55 m, pusat sedikit ke timur)
        ctl.mapCenter = new Vector3(0f, 0f, 0f); ctl.largeCamSize = 74f; ctl.smallCamSize = 27f; ctl.sunBoost = 2.4f;
        EditorUtility.SetDirty(mp); EditorUtility.SetDirty(ms);
        Log("minimap dipasang (M = peta besar)");
    }
}
#endif
