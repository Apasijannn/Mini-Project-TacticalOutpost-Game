#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Outpost.Env;

// Seluruh prop lingkungan sekarang hanya memakai prefab dari aset yang diimport:
//   Industrial Props Kit, Radioactive Barrel (H&L), Post-Apocalyptic Barrels (Sector Warzone),
//   Defense Line Vol.3 (RSG), motor & pickup post-apocalyptic (RetroStyleGames; hanya properti diam).
public static partial class OutpostBuilder
{
    const string RsgDir = "Assets/RSG/URP/Prefabs/P_rsg_military_";
    const string RadDir = "Assets/H&L Assets/Radioactive barrel/URP/Prefabs/barrel_toxic_";
    const string WarDir = "Assets/Sector Warzone/Post-Apocalyptic Barrels/Prefabs/PRE_Barrel_";
    const string IndDir = "Assets/Industrial Props/Prefabs/";
    const string BikeAsset = "Assets/RetroStyleGames/LastGuns/URP/Bike_B/Prefabs/P_RSG_Bike_B_Dirty_URP.prefab";
    const string PickupAsset = "Assets/RetroStyleGames/LastGuns/URP/Crossroad_Pickup/Prefabs/P_RSG_Crossroad_Pickup_Clean_URP.prefab";

    static readonly string[] radBarrels = { RadDir + "01.prefab", RadDir + "02.prefab", RadDir + "04.prefab" };
    static readonly string[] warBarrels = { WarDir + "Biohazard.prefab", WarDir + "Custom.prefab", WarDir + "Military.prefab" };

    // Industrial Props memakai shader Standard -> ubah ke URP/Lit, beton sedikit digelapkan agar tidak silau
    static string TuneIndustrialMaterials()
    {
        string r = ConvertMaterialsIn("Assets/Industrial Props");
        foreach (var n in new[] { "Concrete", "Concrete2" })
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Industrial Props/Materials 1/" + n + ".mat");
            if (m != null) { m.SetColor("_BaseColor", new Color(0.72f, 0.72f, 0.72f, 1f)); EditorUtility.SetDirty(m); }
        }
        AssetDatabase.SaveAssets();
        return r;
    }

    // Pindai: mesh di scene yang BUKAN bagian dari prefab (artinya dibuat manual lewat kode)
    static string ProcScan()
    {
        var sb = new System.Text.StringBuilder();
        var counts = new Dictionary<string, int>();
        foreach (var mf in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (PrefabUtility.IsPartOfAnyPrefab(mf.gameObject)) continue;
            string top = mf.transform.root.name;
            Transform t = mf.transform; string path = t.name;
            while (t.parent != null && t.parent.parent != null) { t = t.parent; }
            string key = (t.parent != null ? t.parent.name + "/" : "") + t.name.Split('_')[0];
            int v; counts.TryGetValue(key, out v); counts[key] = v + 1;
        }
        foreach (var kv in counts) sb.AppendLine(kv.Key + " : " + kv.Value);
        return sb.ToString();
    }

    // ---------- helper umum ----------
    static GameObject PropW(string rel, Transform parent, float x, float z, float yaw, float scale, EnvCategory cat, string label, bool solid = true, int layer = -1, bool noCol = false, float baseY = 0f)
    {
        int ly = layer < 0 ? LCover : layer;
        var g = PlacePrefab(rel, parent, x, z, yaw, scale, baseY, ly, solid);
        if (g == null) return null;
        if (noCol) foreach (var c in g.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        Slot(g, cat, label, RBounds(g).size);
        return g;
    }

    static GameObject Prop(string rel, Transform parent, float px, float py, float yaw, float scale, EnvCategory cat, string label, bool solid = true, int layer = -1, bool noCol = false, float baseY = 0f)
    {
        Vector2 p = V(px, py);
        return PropW(rel, parent, p.x, p.y, yaw, scale, cat, label, solid, layer, noCol, baseY);
    }

    // ---------- barel ----------
    static GameObject BarrelP(Transform parent, float x, float z, int variant = -1, bool radio = false)
    {
        var list = radio ? radBarrels : warBarrels;
        string rel = list[variant >= 0 ? variant % list.Length : rng.Next(list.Length)];
        return PropW(rel, parent, x, z, Rf(0, 360), 1.15f, EnvCategory.Barrel, radio ? "RadBarrel" : "WarBarrel");
    }

    static void BarrelClusterP(Transform parent, float px, float py, int n, bool radio = false)
    {
        Vector2 p = V(px, py);
        for (int i = 0; i < n; i++)
        {
            float a = (i / (float)n) * Mathf.PI * 2f + Rf(-0.3f, 0.3f);
            float r = n == 1 ? 0f : Rf(0.65f, 0.95f);
            BarrelP(parent, p.x + Mathf.Cos(a) * r, p.y + Mathf.Sin(a) * r, i + (int)(px * 0.1f), radio);
        }
    }

    static GameObject BurningBarrelP(Transform parent, float px, float py)
    {
        Vector2 p = V(px, py);
        var b = PropW(WarDir + "Military.prefab", parent, p.x, p.y, Rf(0, 360), 1.15f, EnvCategory.Barrel, "BurnBarrel");
        if (b != null) Fire(b.transform, new Vector3(0f, 0.8f, 0f));
        return b;
    }

    // ---------- barikade RSG ----------
    static GameObject RoadBlockP(Transform parent, float px, float py, float yaw)
    { return Prop(RsgDir + "concret_block_01.prefab", parent, px, py, yaw, 1.2f, EnvCategory.Barrier, "ConcreteBlock"); }

    static GameObject HedgehogP(Transform parent, float px, float py)
    { return Prop(RsgDir + "antitank_hedgehog_01.prefab", parent, px, py, Rf(0, 90), 1.2f, EnvCategory.Barrier, "Hedgehog"); }

    static void BarbedP(Transform parent, float px, float py, float yaw)
    { Prop(RsgDir + "barbed_wire_01.prefab", parent, px, py, yaw, 1.0f, EnvCategory.Barrier, "BarbedWire"); }

    // deretan karung pasir (kotak sandbox RSG) dari (x0,y0) ke (x1,y1) dalam piksel
    static void SandboxLine(Transform parent, float px0, float py0, float px1, float py1)
    {
        Vector2 a = V(px0, py0), b = V(px1, py1);
        Vector2 d = b - a; float L = d.magnitude;
        const float unit = 1.2f;
        int n = Mathf.Max(1, Mathf.RoundToInt(L / unit));
        float yaw = YawAlongZ(d);
        for (int i = 0; i < n; i++)
        {
            Vector2 c = a + d * ((i + 0.5f) / n);
            PropW(RsgDir + "sandbox_01.prefab", parent, c.x, c.y, yaw + Rf(-5, 5), 0.95f, EnvCategory.Sandbag, "Sandbox");
        }
    }

    // ---------- lampu kerja & tiang listrik (Industrial Props) ----------
    static void WorkLightP(Transform parent, float px, float py, float yaw)
    { Vector2 p = V(px, py); WorkLightW(parent, p.x, p.y, yaw, YawAlongX(V(512, 286) - p)); }

    static void WorkLightW(Transform parent, float x, float z, float yaw, float unused = 0f)
    {
        var g = PropW(IndDir + "WorkLight.prefab", parent, x, z, yaw, 1.25f, EnvCategory.Light, "WorkLight", false, LObstacle, true);
        if (g == null) return;
        Bounds b = RBounds(g);
        var cc = g.AddComponent<CapsuleCollider>();
        cc.center = g.transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y + 1.3f, b.center.z)); cc.radius = 0.35f; cc.height = 2.6f;
        AddLight(g.transform, "Light", g.transform.InverseTransformPoint(new Vector3(b.center.x, b.max.y - 0.25f, b.center.z)), LightType.Point, new Color(1f, 0.82f, 0.55f), 3.4f, 17f);
    }

    static void UtilityPoleP(Transform parent, float px, float py, float yaw)
    {
        var g = Prop(IndDir + "LightPole.prefab", parent, px, py, yaw, 0.85f, EnvCategory.Light, "UtilityPole", false, LObstacle, true);
        if (g == null) return;
        Bounds b = RBounds(g); Vector3 ls = g.transform.lossyScale;
        var bc = g.AddComponent<BoxCollider>();
        bc.center = g.transform.InverseTransformPoint(b.center);
        bc.size = new Vector3(0.5f / Mathf.Abs(ls.x), b.size.y / Mathf.Abs(ls.y), 0.5f / Mathf.Abs(ls.z));
    }

    static void ConeP(Transform parent, float px, float py)
    { Prop(IndDir + "ConstructionCone.prefab", parent, px, py, Rf(0, 360), 1.0f, EnvCategory.Other, "Cone", false, LCover, true); }

    static void BottlesP(Transform parent, float px, float py, int n, float baseY = 0f)
    { Vector2 p = V(px, py); BottlesW(parent, p.x, p.y, n, baseY); }

    static void BottlesW(Transform parent, float x, float z, int n, float baseY = 0f)
    {
        for (int i = 0; i < n; i++)
            PropW(IndDir + "OxygenTank.prefab", parent, x + (i % 2) * 0.55f + Rf(-0.05f, 0.05f), z + (i / 2) * 0.55f, Rf(0, 360), 1.0f, EnvCategory.Other, "GasBottle", true, LCover, false, baseY);
    }

    // ---------- pos komando: bangunan dari pack (menggantikan bangunan buatan manual) ----------
    static void CommandPostP()
    {
        var g = Prop("Buildings/Industrial/Hangars/Hangar_v2/Hangar_v2_3.prefab", gBuild, 736, 302, 0f, 1f, EnvCategory.Building, "Command", true, LObstacle);
        if (g != null) g.name = "Command_Post";
        Vector2 ep = V(672 - 14, 262);
        PlacePrefab("Other_props/Electric_box/Electric_box_v3/Electric_box_v3.prefab", gCover, ep.x, ep.y, 90f, 1f, 0f, LCover);
    }

    // ---------- kendaraan: hanya properti diam ----------
    static void BuildVehiclesP()
    {
        // pickup & motor tidak bisa dikendarai: collider kotak solid saja.
        PropV(PickupAsset, 102, 122, 35f, 0.85f, "Pickup");
        PropV(PickupAsset, 886, 434, 100f, 0.85f, "Pickup");
        PropV(BikeAsset, 246, 232, 70f, 0.85f, "Motorcycle");
        PropV(BikeAsset, 872, 118, -30f, 0.85f, "Motorcycle");
    }

    static void PropV(string rel, float px, float py, float yaw, float scale, string label)
    { Prop(rel, gVeh, px, py, yaw, scale, EnvCategory.Vehicle, label, true, LCover); }

    // ---------- barang kecil ----------
    static void BuildSmallPropsP()
    {
        var c = gCover;
        // --- pos gerbang barat ---
        Prop(IndDir + "RoadDivider.prefab", c, 82, 264, 0f, 1.0f, EnvCategory.Barrier, "Divider");
        Prop(IndDir + "RoadDivider.prefab", c, 82, 308, 0f, 1.0f, EnvCategory.Barrier, "Divider");
        BarbedP(c, 70, 238, 90f); BarbedP(c, 70, 334, 90f);
        foreach (var p in new[] { new Vector2(98, 252), new Vector2(98, 320), new Vector2(150, 251), new Vector2(196, 251), new Vector2(214, 318), new Vector2(240, 318) })
            ConeP(c, p.x, p.y);
        // --- pintu dalam (pintu masuk kompleks) ---
        HedgehogP(c, 276, 268); HedgehogP(c, 276, 304);
        RoadBlockP(c, 318, 272, 90f); RoadBlockP(c, 318, 300, 90f);
        RoadBlockP(c, 300, 108, 90f); RoadBlockP(c, 300, 156, 90f);
        RoadBlockP(c, 300, 352, 90f); RoadBlockP(c, 300, 402, 90f);

        // --- barel: radioaktif di area bahan bakar/generator, barel pasca-kiamat di tempat lain ---
        BarrelClusterP(c, 308, 408, 3, true);
        BarrelClusterP(c, 470, 400, 3, true);
        BarrelClusterP(c, 352, 196, 3, true);
        BarrelClusterP(c, 600, 410, 3, true);
        BarrelClusterP(c, 155, 243, 3);
        BarrelClusterP(c, 100, 345, 3);
        BarrelClusterP(c, 180, 425, 3);
        BarrelClusterP(c, 505, 130, 3);
        BarrelClusterP(c, 297, 330, 3);
        BarrelClusterP(c, 900, 150, 3);
        BarrelClusterP(c, 838, 392, 3);
        foreach (var p in new[] { new Vector2(345, 258), new Vector2(505, 322), new Vector2(618, 236), new Vector2(618, 356), new Vector2(560, 395) })
            BurningBarrelP(c, p.x, p.y);

        // --- peti kayu (pack lingkungan) ---
        CratePileP(c, 868, 322, 4, 1.3f);
        CratePileP(c, 150, 160, 5, 1.4f);
        CratePileP(c, 252, 440, 3, 1.4f);
        CratePileP(c, 612, 420, 3, 1.3f);

        // --- karung pasir (RSG sandbox) ---
        SandboxLine(c, 598, 255, 598, 282);
        SandboxLine(c, 598, 322, 598, 350);
        SandboxLine(c, 460, 258, 460, 284);
        SandboxLine(c, 524, 386, 524, 416);
        SandboxLine(c, 824, 262, 824, 284);     // posisi bertahan pemain
        SandboxLine(c, 824, 300, 824, 326);

        // --- landak baja & blok beton di jalur utama ---
        HedgehogP(c, 380, 288); HedgehogP(c, 345, 118); HedgehogP(c, 342, 392); HedgehogP(c, 598, 160);
        RoadBlockP(c, 404, 285, 90f);
        RoadBlockP(c, 622, 214, 0f);
        RoadBlockP(c, 650, 364, 0f); RoadBlockP(c, 650, 390, 0f);

        // --- area konstruksi / peralatan (Industrial Props) ---
        Prop(IndDir + "Scaffolding.prefab", c, 130, 455, 0f, 1.0f, EnvCategory.Other, "Scaffolding");
        Prop(IndDir + "CinderBlock.prefab", c, 165, 440, 20f, 1.3f, EnvCategory.Other, "Cinder", true, LCover, false, 0f);
        Prop(IndDir + "CinderBlock.prefab", c, 168, 446, 70f, 1.3f, EnvCategory.Other, "Cinder", true, LCover, false, 0f);
        BottlesP(c, 100, 470, 3);
        Prop(IndDir + "Ibeam.prefab", c, 470, 112, 0f, 1.0f, EnvCategory.Other, "Ibeam", false, LCover, true);
        Prop(IndDir + "Ibeam.prefab", c, 150, 332, 0f, 1.0f, EnvCategory.Other, "Ibeam", false, LCover, true);
        Prop(IndDir + "WaterTank.prefab", c, 458, 436, 15f, 1.0f, EnvCategory.Other, "WaterTank");
        Prop(IndDir + "WaterTank.prefab", c, 458, 466, -10f, 1.0f, EnvCategory.Other, "WaterTank");
        Prop(IndDir + "WaterTank.prefab", c, 862, 190, 90f, 1.0f, EnvCategory.Other, "WaterTank");
        Prop(IndDir + "pallet.prefab", c, 508, 462, 0f, 0.5f, EnvCategory.Pallet, "Pallet");
        BottlesP(c, 508, 462, 2, 0.16f);

        // --- lampu kerja (menggantikan tiang lampu buatan) & tiang listrik ---
        float[][] lamps = {
            new[]{312f,238f}, new[]{312f,334f}, new[]{452f,243f}, new[]{520f,140f}, new[]{430f,370f}, new[]{575f,372f}, new[]{648f,236f},
            new[]{705f,386f}, new[]{835f,232f}, new[]{835f,352f}, new[]{380f,112f}, new[]{600f,175f} };
        foreach (var l in lamps) WorkLightP(gLights, l[0], l[1], 0f);
        UtilityPoleP(gLights, 100, 236, 0f); UtilityPoleP(gLights, 100, 338, 0f); UtilityPoleP(gLights, 272, 332, 0f);
    }

    // ---------- dekor ----------
    static void BuildDecorP()
    {
        // marka jalan barat (cat di atas aspal, bukan prop)
        for (float px = -140; px < 280; px += 22)
        { Vector2 p = V(px, 285); Mark(gDecor, p.x, p.y, 3.2f, 0.18f, 0, "paintYellow", 0.075f); }
        { Vector2 a = V(-140, 253), b = V(285, 253); Mark(gDecor, (a.x + b.x) * 0.5f, a.y, b.x - a.x, 0.16f, 0, "paintWhite", 0.075f); }
        { Vector2 a = V(-140, 315), b = V(285, 315); Mark(gDecor, (a.x + b.x) * 0.5f, a.y, b.x - a.x, 0.16f, 0, "paintWhite", 0.075f); }
        // debu halus di jalur
        foreach (var dp in new[] { new Vector2(430, 285), new Vector2(560, 300), new Vector2(380, 130), new Vector2(560, 375), new Vector2(200, 285) })
        {
            Vector2 p = V(dp.x, dp.y);
            AddParticles(gDecor, "Dust", new Vector3(p.x, 0.1f, p.y), "smoke", new Color(0.78f, 0.66f, 0.48f, 0.12f), new Color(0.78f, 0.66f, 0.48f, 0f), 2.5f, 5f, 8f, 0.3f, 0.7f, 3f, 5f, 80f, 6f, 0f, 1.6f);
        }
    }
}
#endif
