#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Outpost.Env;

// Layout "Tactical Outpost" v2: mengikuti foto top-down referensi (1649 x 954 px).
// Dua jalur masuk (barat & timur) + satu generator di tengah. Semua objek memakai prefab dari aset yang sudah diimport.
public static partial class OutpostBuilder
{
    const float K2 = 0.0958f;                                       // meter per piksel foto
    static Vector2 Ph(float px, float py) { return new Vector2((px - 820f) * K2, -(py - 457f) * K2); }
    static Vector3 PhW(float px, float py) { var p = Ph(px, py); return new Vector3(p.x, 0f, p.y); }
    static Vector3 playerSpawnPos = Vector3.zero;

    const string HB = "Buildings/Industrial/Hangars/Hangar_v2/Hangar_v2_basic.prefab";
    const string H2O = "Buildings/Industrial/Hangars/Hangar_v2/Hangar_v2_outbuilding.prefab";
    const string H2O1 = "Buildings/Industrial/Hangars/Hangar_v2/Hangar_v2_outbuilding1.prefab";
    const string H3B = "Buildings/Industrial/Hangars/Hangar_v3/Hangar_v3_basic.prefab";
    const string H3O1 = "Buildings/Industrial/Hangars/Hangar_v3/Hangar_v3_outbuilding_1.prefab";
    const string H3O2 = "Buildings/Industrial/Hangars/Hangar_v3/Hangar_v3_outbuilding_2.prefab";
    const string H1F = "Buildings/Industrial/Hangars/Hangar_v1/Hangar_v1_full.prefab";
    const string CLD1 = "Containers/Cargo_container_v1/Cargo_container_v1_LD1close.prefab";
    const string CLD2 = "Containers/Cargo_container_v1/Cargo_container_v1_LD2close.prefab";
    const string PipeDir = "Other_props/Pipes/Pipe_sets/Pipes_set_v1/";

    // ---------- util ----------
    static readonly Dictionary<string, Vector3> sizeCache2 = new Dictionary<string, Vector3>();
    static Vector3 PrefabSize(string rel)
    {
        Vector3 s; if (sizeCache2.TryGetValue(rel, out s)) return s;
        var a = LoadPrefab(rel); if (a == null) return Vector3.one;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(a);
        go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity; go.transform.localScale = Vector3.one;
        s = RBounds(go).size; UnityEngine.Object.DestroyImmediate(go);
        sizeCache2[rel] = s; return s;
    }

    // taruh prefab sehingga jejaknya (lebar x tinggi pada foto, dalam piksel) pas dengan foto
    static GameObject FitW(string rel, Transform parent, float cx, float cy, float wpx, float hpx, float yaw, int layer, EnvCategory cat, string label, bool solid = false, float yScale = -1f)
    {
        Vector3 s = PrefabSize(rel);
        bool swap = Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) > 0.7f;
        float tx = (swap ? hpx : wpx) * K2, tz = (swap ? wpx : hpx) * K2;
        float sx = tx / Mathf.Max(0.01f, s.x), sz = tz / Mathf.Max(0.01f, s.z);
        float sy = yScale > 0f ? yScale : Mathf.Clamp(Mathf.Sqrt(sx * sz), 0.75f, 1.5f);
        var p = Ph(cx, cy);
        var g = PlacePrefab(rel, parent, p.x, p.y, yaw, 1f, 0f, layer, solid, new Vector3(sx, sy, sz));
        if (g != null) Slot(g, cat, label, RBounds(g).size);
        return g;
    }

    static GameObject SlabP(Transform parent, string name, float px0, float py0, float px1, float py1, float topY, string matKey, bool collide, int layer = 0)
    {
        Vector2 a = Ph(px0, py0), b = Ph(px1, py1); Vector2 c = (a + b) * 0.5f;
        return BoxQ(parent, name, new Vector3(c.x, topY - 0.15f, c.y), new Vector3(Mathf.Abs(b.x - a.x), 0.3f, Mathf.Abs(b.y - a.y)), Mat(matKey), layer, collide, Quaternion.identity);
    }

    static void MarkP(float cx, float cy, float wm, float dm, string key, float yaw = 0f, float y = 0.075f)
    { var p = Ph(cx, cy); Mark(gDecor, p.x, p.y, wm, dm, yaw, key, y); }

    static GameObject ContW(Transform parent, float cx, float cy, bool vertical, int color, float len)
    {
        string rel = rng.NextDouble() < 0.5 ? CLD1 : CLD2;
        var p = Ph(cx, cy);
        float yaw = (vertical ? 90f : 0f) + (rng.NextDouble() < 0.5 ? 0f : 180f);
        var go = PlacePrefab(rel, parent, p.x, p.y, yaw, len / 8f, 0f, LCover);
        if (go == null) return null;
        var mat = color >= 0 ? ContMat(color) : null;
        if (mat != null) foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true)) { if (r.sharedMaterials.Length == 1) r.sharedMaterial = mat; }
        go.name = "Container_" + (parent != null ? parent.childCount : 0);
        Slot(go, EnvCategory.Container, "Cargo", RBounds(go).size);
        return go;
    }

    static void FenceW(Transform parent, float ax, float ay, float bx, float by, bool columns = true, string name = "Fence", float h = 3.4f)
    {
        Vector2 a = Ph(ax, ay), b = Ph(bx, by);
        Vector2 d = b - a; float L = d.magnitude;
        const string rel = "Fences/Concrete_fences/Concrete_fence_v2/Concrete_fence_v2_S.prefab";
        const float mod = 5.67f;
        int n = Mathf.Max(1, Mathf.RoundToInt(L / mod));
        float seg = L / n;
        float yaw = YawAlongZ(d);
        var root = Grp(name, parent);
        root.gameObject.layer = LFence;
        Slot(root.gameObject, EnvCategory.Fence, "ConcreteMesh", new Vector3(L, h, 0.7f));
        for (int i = 0; i < n; i++)
        {
            Vector2 c = a + d * ((i + 0.5f) / n);
            var g = PlacePrefab(rel, root, c.x, c.y, yaw, 1f, 0f, LFence, true, new Vector3(1.3f, 1.3f, seg / mod));
            if (g != null) g.name = name + "_" + i;
        }
        if (columns)
            foreach (var e in new[] { a, b })
                PlacePrefab("Fences/Concrete_fences/Concrete_fence_v1/Concrete_fence_v1_column.prefab", root, e.x, e.y, 0f, 1.25f, 0f, LFence, true);
    }

    static void TowerW(Transform parent, string name, float px, float py, float yaw)
    {
        Vector2 p = Ph(px, py);
        var go = PlacePrefab("Assets/Tower/Models/Tower.FBX", parent, p.x, p.y, yaw, 1f, 0f, LObstacle, true);
        if (go == null) { Log("PERINGATAN: model Tower tidak ditemukan"); return; }
        go.name = name;
        Slot(go, EnvCategory.Tower, "GuardTower", RBounds(go).size);
        Bounds b = RBounds(go);
        var l = AddLight(go.transform, "TopGlow", Vector3.zero, LightType.Point, new Color(1f, 0.72f, 0.4f), 2.4f, 14f);
        l.transform.position = new Vector3(b.center.x, b.max.y + 0.8f, b.center.z);
    }

    static void TankW(Transform parent, float px, float py, float scale)
    {
        Vector2 p = Ph(px, py);
        var g = PlacePrefab("Oil_tanks/Oil_tank_v1/Oil_tank_v1.prefab", parent, p.x, p.y, Rf(0, 360), scale, 0f, LObstacle, true);
        if (g != null) Slot(g, EnvCategory.FuelTank, "Tank", RBounds(g).size);
    }

    static void CrateAt(float px, float py, int n, float scale = 1.4f)
    {
        Vector2 p = Ph(px, py);
        float step = 0.82f * scale + 0.05f;
        int cols = Mathf.CeilToInt(Mathf.Sqrt(n));
        for (int i = 0; i < n; i++)
        {
            int cx = i % cols, cz = i / cols;
            CrateP(gCover, p.x + (cx - (cols - 1) * 0.5f) * step, p.y + (cz - (cols - 1) * 0.5f) * step, scale, Rf(-8, 8) + (rng.NextDouble() < 0.5 ? 0 : 90));
        }
        if (n >= 3) CrateP(gCover, p.x, p.y, scale * 0.9f, Rf(-15, 15), 0.82f * scale);
    }

    static void BarrelsAt(float px, float py, int n, bool radio = false)
    {
        Vector2 p = Ph(px, py);
        for (int i = 0; i < n; i++)
        {
            float a = (i / (float)n) * Mathf.PI * 2f + Rf(-0.3f, 0.3f);
            float r = n == 1 ? 0f : Rf(0.65f, 0.95f);
            BarrelP(gCover, p.x + Mathf.Cos(a) * r, p.y + Mathf.Sin(a) * r, i + (int)(px * 0.1f), radio);
        }
    }

    static void PropAt(string rel, Transform parent, float px, float py, float yaw, float scale, EnvCategory cat, string label)
    { var p = Ph(px, py); PropW(rel, parent, p.x, p.y, yaw, scale, cat, label, true, LCover); }

    // ---------- tanah & jalan ----------
    static void BuildGroundV2()
    {
        BoxQ(gGround, "Sand", new Vector3(0, -0.15f, 0), new Vector3(170, 0.3f, 116), Mat("sand"), LGround, true, Quaternion.identity);
        BoxQ(gGround, "SandFar", new Vector3(0, -0.2f, 0), new Vector3(1600, 0.3f, 1600), Mat("sand"), 0, false, Quaternion.identity);
        // kompleks tengah (aspal gelap), plaza beton, halaman barat
        SlabP(gGround, "BaseAsphalt", 700, 92, 1160, 830, 0.03f, "asphalt", true, LGround);
        SlabP(gGround, "ApronNW", 440, 296, 700, 432, 0.05f, "concrete", false);
        SlabP(gGround, "ApronCenter", 690, 300, 1010, 620, 0.06f, "concrete", false);
        SlabP(gGround, "PadSW", 200, 650, 365, 780, 0.06f, "concrete", false);
        SlabP(gGround, "PadGenerator", 757, 408, 917, 520, 0.07f, "pad", false);
        // jalan barat
        SlabP(gGround, "RoadWestA", -300, 405, 450, 508, 0.045f, "road", true, LGround);
        SlabP(gGround, "RoadWestB", 450, 432, 715, 508, 0.045f, "road", true, LGround);
        SlabP(gGround, "RoadWestN", 255, 318, 450, 405, 0.045f, "road", true, LGround);
        SlabP(gGround, "RoadWestS", 365, 508, 425, 700, 0.045f, "road", true, LGround);
        // jalan timur
        SlabP(gGround, "RoadEastA", 963, 432, 1230, 508, 0.045f, "road", true, LGround);
        SlabP(gGround, "RoadEastB", 1230, 405, 1950, 508, 0.045f, "road", true, LGround);
        SlabP(gGround, "RoadEastS", 1230, 508, 1360, 557, 0.045f, "road", true, LGround);
        SlabP(gGround, "RoadEastN", 1310, 385, 1400, 405, 0.045f, "road", true, LGround);
    }

    static void BuildMarksV2()
    {
        // garis tengah putus-putus
        for (float px = -250; px < 715; px += 30) MarkP(px, 458, 1.2f, 0.15f, "paintWhite");
        for (float px = 975; px < 1900; px += 30) MarkP(px, 458, 1.2f, 0.15f, "paintWhite");
        // garis tepi kuning
        { var a = Ph(-250, 405); var b = Ph(450, 405); Mark(gDecor, (a.x + b.x) * 0.5f, a.y, b.x - a.x, 0.18f, 0, "paintYellow", 0.075f); }
        { var a = Ph(450, 432); var b = Ph(715, 432); Mark(gDecor, (a.x + b.x) * 0.5f, a.y, b.x - a.x, 0.18f, 0, "paintYellow", 0.075f); }
        { var a = Ph(963, 432); var b = Ph(1230, 432); Mark(gDecor, (a.x + b.x) * 0.5f, a.y, b.x - a.x, 0.18f, 0, "paintYellow", 0.075f); }
        { var a = Ph(1230, 405); var b = Ph(1900, 405); Mark(gDecor, (a.x + b.x) * 0.5f, a.y, b.x - a.x, 0.18f, 0, "paintYellow", 0.075f); }
        // zebra cross di dekat gerbang
        foreach (float cx in new float[] { 150, 274, 1385, 1503 })
            for (float py = 412; py < 504; py += 11) MarkP(cx, py, 2.2f, 0.6f, "paintWhite");
    }

    // ---------- pagar, gerbang, menara ----------
    static void BuildPerimeterV2()
    {
        const float x0 = 167, x1 = 1475, y0 = 85, y1 = 830;
        FenceW(gPerim, x0, y0, x1, y0, true, "Fence_N");
        FenceW(gPerim, x0, y1, x1, y1, true, "Fence_S");
        FenceW(gPerim, x0, y0, x0, 365, true, "Fence_W_N");
        FenceW(gPerim, x0, 545, x0, y1, true, "Fence_W_S");
        FenceW(gPerim, x1, y0, x1, 372, true, "Fence_E_N");
        FenceW(gPerim, x1, 545, x1, y1, true, "Fence_E_S");
        // rumah gerbang di kedua sisi jalan
        FitW(H2O, gBuild, 178, 383, 115, 33, 0f, LObstacle, EnvCategory.Building, "Gatehouse");
        FitW(H2O, gBuild, 172, 527, 120, 30, 0f, LObstacle, EnvCategory.Building, "Gatehouse");
        FitW(H2O, gBuild, 1471, 388, 142, 33, 0f, LObstacle, EnvCategory.Building, "Gatehouse");
        FitW(H2O, gBuild, 1470, 527, 150, 32, 0f, LObstacle, EnvCategory.Building, "Gatehouse");
        MarkP(1372, 527, 5.0f, 2.4f, "paintYellow", 0f, 0.08f);
        MarkP(308, 522, 10.0f, 2.4f, "paintYellow", 0f, 0.08f);
        // menara jaga
        TowerW(gPerim, "Tower_NW", 212, 112, 0f);
        TowerW(gPerim, "Tower_NE", 1445, 118, 180f);
        TowerW(gPerim, "Tower_SW", 197, 805, 0f);
        TowerW(gPerim, "Tower_SE", 1465, 805, 180f);
        // pagar dalam: kompleks tangki & sisi timur
        FenceW(gInd, 962, 92, 962, 298, true, "TankFence_W", 3f);
        FenceW(gInd, 962, 298, 1160, 298, true, "TankFence_S", 3f);
        FenceW(gPerim, 1160, 92, 1160, 412, true, "InnerFence_E_N");
        FenceW(gPerim, 1160, 520, 1160, 830, true, "InnerFence_E_S");
    }

    // ---------- bangunan, tangki, kontainer, pipa ----------
    static void BuildStructuresV2()
    {
        var B = gBuild;
        FitW(HB, B, 549, 206, 242, 156, 180f, LObstacle, EnvCategory.Building, "Hangar");
        FitW(HB, B, 555, 643, 242, 182, 0f, LObstacle, EnvCategory.Building, "Hangar");
        FitW(H3B, B, 553, 782, 95, 102, 0f, LObstacle, EnvCategory.Building, "Warehouse");
        FitW(H3B, B, 829, 177, 92, 106, 0f, LObstacle, EnvCategory.Building, "Warehouse");
        FitW(H3O1, B, 221, 687, 58, 70, 0f, LObstacle, EnvCategory.Building, "Shed");
        FitW(H2O1, B, 350, 752, 98, 47, 0f, LObstacle, EnvCategory.Building, "Cabin");
        FitW(H2O1, B, 460, 783, 80, 56, 0f, LObstacle, EnvCategory.Building, "Cabin");
        FitW(H2O, B, 580, 380, 140, 45, 0f, LObstacle, EnvCategory.Building, "Cabin");
        FitW(H2O1, B, 747, 184, 71, 50, 0f, LObstacle, EnvCategory.Building, "Cabin");
        FitW(H1F, B, 910, 203, 53, 95, 90f, LObstacle, EnvCategory.Building, "RedRoof");
        FitW(H1F, B, 827, 731, 87, 138, 90f, LObstacle, EnvCategory.Building, "RedRoof");
        FitW(H2O1, B, 1099, 775, 90, 61, 0f, LObstacle, EnvCategory.Building, "Cabin");
        FitW(H3O2, B, 1118, 566, 32, 48, 0f, LObstacle, EnvCategory.Building, "RustyShed");
        FitW(H3O2, B, 949, 589, 30, 32, 0f, LObstacle, EnvCategory.Building, "RustyShed");

        // tangki bahan bakar (4 buah, dalam pagar)
        foreach (var t in new[] { new Vector2(1017, 160), new Vector2(1102, 160), new Vector2(1017, 247), new Vector2(1105, 247) }) TankW(gInd, t.x, t.y, 0.6f);

        // kontainer
        ContW(gCont, 1334, 144, false, 1, 9.3f);
        ContW(gCont, 1415, 276, true, 0, 8.8f);
        ContW(gCont, 1232, 660, true, 0, 7.7f);
        ContW(gCont, 1232, 742, true, 2, 8.1f);
        ContW(gCont, 1282, 718, true, 1, 12.2f);
        ContW(gCont, 828, 635, false, 0, 8.0f);
        ContW(gCont, 923, 635, false, 1, 8.0f);
        ContW(gCont, 1046, 635, false, 0, 8.7f);
        ContW(gCont, 1054, 560, false, 2, 8.7f);
        ContW(gCont, 1050, 351, false, 0, 8.1f);
        foreach (float py in new float[] { 618, 646, 674 }) ContW(gCont, 1121, py, true, -1, 3.9f);

        // pipa (bentuk U di halaman tenggara)
        PlacePrefab("Other_props/Pipes/Pipe_sets/Pipes_set_v1/Pipes_set_v1_H_set_v4.prefab", gCover, Ph(941, 680).x, Ph(941, 680).y, 0f, 1f, 0f, LCover, true);
        PlacePrefab("Other_props/Pipes/Pipe_sets/Pipes_set_v1/Pipes_set_v1_H_set_v4.prefab", gCover, Ph(1033, 680).x, Ph(1033, 680).y, 0f, 1f, 0f, LCover, true);
        PlacePrefab("Other_props/Pipes/Pipe_sets/Pipes_set_v1/Pipes_set_v1_H_set_v4.prefab", gCover, Ph(900, 725).x, Ph(900, 725).y, 90f, 1f, 0f, LCover, true);
        PlacePrefab("Other_props/Pipes/Pipe_sets/Pipes_set_v1/Pipes_set_v1_H_set_v4.prefab", gCover, Ph(950, 762).x, Ph(950, 762).y, 0f, 1f, 0f, LCover, true);
        // pipa pendek di dekat tangki dan kontainer hijau
        PipeAt("Pipes_set_v1_H_set_v4.prefab", 1055, 406, 0f);
        PipeAt("Pipes_set_v1_H_set_v4.prefab", 1055, 523, 0f);
        PipeAt("Pipes_set_v1_H_set_v3.prefab", 1350, 218, 0f);
    }

    static void PipeAt(string variant, float px, float py, float yaw)
    {
        Vector2 p = Ph(px, py);
        var g = PlacePrefab("Other_props/Pipes/Pipe_sets/Pipes_set_v1/" + variant, gCover, p.x, p.y, yaw, 1f, 0f, LCover, true);
        if (g != null) Slot(g, EnvCategory.Pipe, variant, RBounds(g).size);
    }

    // ---------- props kecil ----------
    static void BuildPropsV2()
    {
        // kendaraan (hanya properti diam)
        { var p = Ph(331, 229); PropW(PickupAsset, gVeh, p.x, p.y, 90f, 0.85f, EnvCategory.Vehicle, "Pickup", true, LCover); }
        { var p = Ph(308, 610); PropW(PickupAsset, gVeh, p.x, p.y, -20f, 0.85f, EnvCategory.Vehicle, "Pickup", true, LCover); }
        { var p = Ph(1405, 750); PropW(BikeAsset, gVeh, p.x, p.y, 30f, 0.85f, EnvCategory.Vehicle, "Motorcycle", true, LCover); }
        { var p = Ph(1212, 545); PropW(BikeAsset, gVeh, p.x, p.y, 140f, 0.85f, EnvCategory.Vehicle, "Motorcycle", true, LCover); }

        // peti & pallet
        float[][] crates = {
            new[]{440f,350f,4f}, new[]{485f,372f,4f}, new[]{726f,266f,3f}, new[]{757f,274f,2f}, new[]{951f,328f,4f}, new[]{960f,358f,3f},
            new[]{742f,424f,3f}, new[]{934f,424f,3f}, new[]{742f,500f,3f}, new[]{934f,503f,3f}, new[]{747f,706f,3f}, new[]{1122f,710f,3f},
            new[]{1190f,242f,3f}, new[]{1232f,361f,3f}, new[]{1321f,607f,2f}, new[]{1388f,645f,3f}, new[]{214f,741f,3f}, new[]{790f,540f,3f}, new[]{862f,572f,2f} };
        foreach (var c in crates) CrateAt(c[0], c[1], (int)c[2]);

        // barel
        BarrelsAt(854, 292, 3);
        BarrelsAt(1000, 420, 3, true);
        BarrelsAt(1170, 380, 3);
        BarrelsAt(520, 330, 3, true);
        BarrelsAt(1405, 750, 3);
        BarrelsAt(300, 640, 3);

        // kotak listrik & AC
        string eb = "Other_props/Electric_box/Electric_box_v3/Electric_box_v3.prefab";
        PropAt(eb, gCover, 358, 197, 90f, 1f, EnvCategory.Other, "ElectricBox");
        PropAt(eb, gCover, 812, 292, 0f, 1f, EnvCategory.Other, "ElectricBox");
        PropAt(eb, gCover, 388, 705, 0f, 1f, EnvCategory.Other, "ElectricBox");
        PropAt(eb, gCover, 750, 785, 0f, 1f, EnvCategory.Other, "ElectricBox");
        PropAt("Other_props/Conditioners/Conditioner_v1/Conditioner_v1.prefab", gCover, 380, 120, 0f, 1.3f, EnvCategory.Other, "Conditioner");

        // lampu kerja
        float[][] lamps = { new[]{300f,418f}, new[]{300f,497f}, new[]{640f,425f}, new[]{740f,522f}, new[]{1000f,425f}, new[]{1100f,522f}, new[]{880f,330f},
            new[]{700f,330f}, new[]{1180f,330f}, new[]{560f,528f}, new[]{1300f,418f}, new[]{1300f,497f}, new[]{450f,330f}, new[]{838f,600f} };
        foreach (var l in lamps) { var p = Ph(l[0], l[1]); WorkLightW(gLights, p.x, p.y, 0f); }

        // debu halus di jalur
        foreach (var dp in new[] { new Vector2(560, 470), new Vector2(1100, 470), new Vector2(300, 455), new Vector2(1350, 455), new Vector2(837, 560) })
        {
            Vector2 p = Ph(dp.x, dp.y);
            AddParticles(gDecor, "Dust", new Vector3(p.x, 0.1f, p.y), "smoke", new Color(0.78f, 0.66f, 0.48f, 0.12f), new Color(0.78f, 0.66f, 0.48f, 0f), 2.5f, 5f, 8f, 0.3f, 0.7f, 3f, 5f, 80f, 6f, 0f, 1.6f);
        }
    }

    static void BuildLayout()
    {
        BuildGroundV2();
        BuildMarksV2();
        BuildPerimeterV2();
        BuildStructuresV2();
        BuildPropsV2();
    }

    // ---------- generator tunggal di tengah (scale x3) ----------
    static GameObject GeneratorW(string id, Vector2 c, float scale, Vector3[] offs, out Transform[] attackPts)
    {
        float f = scale / 1.45f;
        var root = Obj(gPlay, "Generator_" + id, c.x, c.y, 0).gameObject;
        root.layer = LGenerator;
        var bc = root.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.9f * scale, 0); bc.size = new Vector3(3.65f * scale + 0.2f, 1.75f * scale + 0.1f, 1.305f * scale + 0.2f);
        Transform t = root.transform;
        var body = PlacePrefab("Other_props/Generators/Generator_v1/Generator_v1.prefab", t, c.x, c.y, 0f, scale, 0f, LGenerator, false, null, false);
        if (body != null) { foreach (var col in body.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(col); BrightenGenerator(body); }
        WorkLightW(t, c.x + 10.4f, c.y + 5.0f, 0f);
        BottlesW(t, c.x + 10.4f, c.y - 5.2f, 2);
        AddLight(t, "Glow", new Vector3(0, 1.8f * scale + 1.2f, 0), LightType.Point, new Color(1f, 0.78f, 0.45f), 8.0f, 30f);
        AddLight(t, "FrontFill", new Vector3(0, 2.2f * f, 3.2f * f), LightType.Point, new Color(1f, 0.95f, 0.85f), 4.5f, 14f);
        AddLight(t, "BackFill", new Vector3(0, 2.2f * f, -3.2f * f), LightType.Point, new Color(1f, 0.95f, 0.85f), 4.5f, 14f);
        root.AddComponent<Outpost.Enemy.GeneratorHealth>();
        WireGeneratorBar(root);   // bar darah besar di atas generator
        AddBlip(root, genSprite, new Color(0.35f, 0.9f, 1f), 0.5f, true, 0);
        var slotC = root.AddComponent<EnvGeneratorSlot>();
        slotC.id = id; slotC.repairRadius = 12f;
        Slot(root, EnvCategory.Generator, "Generator_" + id, bc.size);
        attackPts = new Transform[offs.Length];
        for (int i = 0; i < offs.Length; i++)
        {
            var ap = new GameObject("AttackPoint_" + i).transform;
            ap.SetParent(t, false); ap.localPosition = offs[i];
            attackPts[i] = ap;
        }
        slotC.attackPoints = attackPts;
        return root;
    }

    static EnvLane MakeLaneP(string name, LaneId id, Color col, Vector2[] ptsPx)
    {
        var go = new GameObject(name);
        go.transform.SetParent(gMarkers, false);
        var lane = go.AddComponent<EnvLane>();
        lane.lane = id; lane.gizmoColor = col;
        for (int i = 0; i < ptsPx.Length; i++)
        {
            var wp = new GameObject("WP_" + i.ToString("00")).transform;
            wp.SetParent(go.transform, false);
            wp.position = PhW(ptsPx[i].x, ptsPx[i].y);
        }
        lanes.Add(lane);
        return lane;
    }

    static void BuildGameplayMarkers()
    {
        gMarkers = Grp("Gameplay_Markers", envRoot);
        lanes.Clear();
        MakeLaneP("Lane_West", LaneId.West, new Color(1f, 0.9f, 0.2f), new[] { new Vector2(215, 455), new Vector2(300, 455), new Vector2(420, 462), new Vector2(520, 470), new Vector2(620, 468), new Vector2(700, 462), new Vector2(735, 462) });
        MakeLaneP("Lane_East", LaneId.East, new Color(1f, 0.5f, 0.2f), new[] { new Vector2(1440, 455), new Vector2(1350, 458), new Vector2(1250, 470), new Vector2(1130, 470), new Vector2(1020, 468), new Vector2(965, 462), new Vector2(940, 462) });

        var spawns = Grp("AttackerSpawns", gMarkers);
        float[] rows = { 428f, 458f, 488f };
        for (int side = 0; side < 2; side++)
            for (int r = 0; r < 3; r++)
                for (int i = 0; i < 6; i++)
                {
                    var go = new GameObject("Spawn_" + (side == 0 ? "West" : "East") + "_" + (r * 6 + i));
                    go.transform.SetParent(spawns, false);
                    go.transform.position = PhW(side == 0 ? 195 + i * 28 : 1450 - i * 28, rows[r]);
                    go.transform.rotation = Quaternion.Euler(0, side == 0 ? 90 : 270, 0);
                    var sp = go.AddComponent<EnvSpawnPoint>(); sp.lane = side == 0 ? LaneId.West : LaneId.East; sp.group = "Enemy";
                    TrySetTag(go, "SpawnPoint");
                }
        playerSpawnPos = PhW(838, 540);
        var ps = new GameObject("PlayerSpawn");
        ps.transform.SetParent(gMarkers, false);
        ps.transform.position = playerSpawnPos;
        ps.transform.rotation = Quaternion.Euler(0, 270, 0);
        var psp = ps.AddComponent<EnvSpawnPoint>(); psp.lane = LaneId.Base; psp.group = "Player";
        TrySetTag(ps, "SpawnPoint");

        Transform[] pts;
        var gen = // skala generator 3 (ukuran akhir pilihanmu); 8 titik serang: 2 di ujung + 3 di tiap sisi panjang, 1,3 m dari tepi collider
        GeneratorW("A", Ph(837, 464), 3f, new[] { new Vector3(-6.9f, 0, 0), new Vector3(6.9f, 0, 0), new Vector3(-3.3f, 0, 3.4f), new Vector3(0, 0, 3.4f), new Vector3(3.3f, 0, 3.4f), new Vector3(-3.3f, 0, -3.4f), new Vector3(0, 0, -3.4f), new Vector3(3.3f, 0, -3.4f) }, out pts);
        TrySetTag(gen, "Generator");
    }
}
#endif
