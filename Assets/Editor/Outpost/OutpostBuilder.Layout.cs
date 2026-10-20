#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Outpost.Env;

// Layout "Tactical Outpost" mengikuti gambar referensi (koordinat dalam piksel gambar 1024x572).
public static partial class OutpostBuilder
{
    static Transform gGround, gPerim, gBuild, gCont, gVeh, gCover, gInd, gDecor, gLights, gPlay;

    // ---------- util ----------
    static Mesh QuadMesh()
    {
        Mesh m;
        if (meshCache.TryGetValue("quad1", out m)) return m;
        var b = new MB();
        b.Quad(new Vector3(-.5f, 0, -.5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f), new Vector3(.5f, 0, -.5f), Vector3.up,
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        return Register(b.Build("quad1"), "quad1");
    }

    static void Decal(Transform parent, string key, float x, float z, float w, float d, float yaw, float y = 0.05f)
    {
        var go = MakeMesh("Decal_" + key, parent, QuadMesh(), Mat(key), new Vector3(x, y, z), Quaternion.Euler(0, yaw, 0), 0);
        go.transform.localScale = new Vector3(w, 1f, d);
        var r = go.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = true;
    }

    static void Mark(Transform parent, float x, float z, float w, float d, float yaw, string matKey, float y = 0.07f)
    { Box(parent, "Mark", new Vector3(x, y, z), new Vector3(w, 0.012f, d), Mat(matKey), 0, false, yaw); }

    static GameObject Slab(Transform parent, string name, float px0, float py0, float px1, float py1, float topY, M m, bool collide, int layer = 0)
    {
        Vector2 c = V((px0 + px1) * 0.5f, (py0 + py1) * 0.5f);
        return BoxQ(parent, name, new Vector3(c.x, topY - 0.15f, c.y), new Vector3(Mathf.Abs(px1 - px0) * S, 0.3f, Mathf.Abs(py1 - py0) * S), m, layer, collide, Quaternion.identity);
    }

    static Vector3 W(float px, float py) { Vector2 p = V(px, py); return new Vector3(p.x, 0f, p.y); }
    static float YawToward(Vector2 from, Vector2 to) { return YawAlongX(to - from); }



    // ---------- tanah ----------
    static void BuildGround()
    {
        BoxQ(gGround, "Sand", new Vector3(0, -0.15f, 0), new Vector3(170, 0.3f, 116), Mat("sand"), LGround, true, Quaternion.identity);
        BoxQ(gGround, "SandFar", new Vector3(0, -0.2f, 0), new Vector3(1600, 0.3f, 1600), Mat("sand"), 0, false, Quaternion.identity);
        Slab(gGround, "BaseAsphalt", 285, 45, 985, 545, 0.03f, Mat("asphalt"), true, LGround);
        Slab(gGround, "RoadWest", -150, 250, 300, 318, 0.045f, Mat("road"), true, LGround);
        Slab(gGround, "RoadSouth", 195, 318, 265, 700, 0.045f, Mat("road"), true, LGround);
        // beton: apron komando, halaman timur, helipad, area tangki
        Slab(gGround, "ApronCmd", 590, 205, 800, 400, 0.06f, Mat("concrete"), false);
        Slab(gGround, "ApronEast", 800, 180, 890, 425, 0.06f, Mat("concrete"), false);
        Slab(gGround, "PadNE", 818, 70, 925, 168, 0.07f, Mat("pad"), false);
        Slab(gGround, "PadSE", 820, 370, 948, 495, 0.07f, Mat("pad"), false);
        Slab(gGround, "PadFuel", 294, 392, 436, 522, 0.06f, Mat("pad"), false);
        Slab(gGround, "PadWestBay", 62, 232, 150, 252, 0.05f, Mat("pad"), false);
    }

    // ---------- pagar & menara ----------
    static void FenceLine(Transform parent, Vector2 aPx, Vector2 bPx, float h = 3.4f, bool collide = true, string name = "Fence")
    {
        // Pagar dari aset environment: modul beton + jaring besi (Concrete_fence_v2), dipasang berderet.
        Vector2 a = V(aPx.x, aPx.y), b = V(bPx.x, bPx.y);
        Vector2 d = b - a; float L = d.magnitude;
        bool gate = name.StartsWith("Gate");
        string rel = gate ? "Fences/Concrete_fences/Concrete_fence_v2/Concrete_fence_v2_Gate.prefab" : "Fences/Concrete_fences/Concrete_fence_v2/Concrete_fence_v2_S.prefab";
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
        if (!gate)
            foreach (var e in new[] { a, b })
                PlacePrefab("Fences/Concrete_fences/Concrete_fence_v1/Concrete_fence_v1_column.prefab", root, e.x, e.y, 0f, 1.25f, 0f, LFence, true);
    }

    static void BuildPerimeter()
    {
        const float x0 = 55, x1 = 985, y0 = 45, y1 = 545;
        FenceLine(gPerim, new Vector2(x0, y0), new Vector2(x1, y0), 3.4f, true, "Fence_N");
        FenceLine(gPerim, new Vector2(x0, y1), new Vector2(x1, y1), 3.4f, true, "Fence_S");
        FenceLine(gPerim, new Vector2(x1, y0), new Vector2(x1, y1), 3.4f, true, "Fence_E");
        FenceLine(gPerim, new Vector2(x0, y0), new Vector2(x0, 255), 3.4f, true, "Fence_W_N");
        FenceLine(gPerim, new Vector2(x0, 317), new Vector2(x0, y1), 3.4f, true, "Fence_W_S");
        // gerbang barat (tertutup)
        FenceLine(gPerim, new Vector2(x0, 255), new Vector2(x0, 317), 3.6f, true, "Gate_W");
        Vector2 g0 = V(x0, 255), g1 = V(x0, 317);
        foreach (var gp in new[] { g0, g1 })
            PlacePrefab("Fences/Concrete_fences/Concrete_fence_v1/Concrete_fence_v1_column.prefab", gPerim, gp.x, gp.y, 0f, 1.25f, 0f, LFence, true);

        // menara jaga dari aset "Tower" di empat sudut (agak ke dalam supaya tidak menembus pagar)
        WatchTower(gPerim, "Tower_NW", x0 + 26, y0 + 28, 0f);
        WatchTower(gPerim, "Tower_NE", x1 - 26, y0 + 28, 180f);
        WatchTower(gPerim, "Tower_SW", x0 + 22, y1 - 30, 0f);
        WatchTower(gPerim, "Tower_SE", x1 - 26, y1 - 28, 180f);
    }

    static void WatchTower(Transform parent, string name, float px, float py, float yaw)
    {
        Vector2 p = V(px, py);
        var go = PlacePrefab("Assets/Tower/Models/Tower.FBX", parent, p.x, p.y, yaw, 1f, 0f, LObstacle, true);
        if (go == null) { Log("PERINGATAN: model Tower tidak ditemukan"); return; }
        go.name = name;
        Slot(go, EnvCategory.Tower, "GuardTower", RBounds(go).size);
        Bounds b = RBounds(go);
        var l = AddLight(go.transform, "TopGlow", Vector3.zero, LightType.Point, new Color(1f, 0.72f, 0.4f), 2.4f, 14f);
        l.transform.position = new Vector3(b.center.x, b.max.y + 0.8f, b.center.z);
    }

    // ---------- tembok dalam ----------
    static void BuildInnerWall()
    {
        var wallRoot = Grp("InnerWall", gBuild);
        float x = 285;
        float[][] segs = { new[] { 45f, 112f }, new[] { 152f, 258f }, new[] { 312f, 356f }, new[] { 398f, 545f } };
        foreach (var s in segs) ConcreteWallP(wallRoot, new Vector2(x, s[0]), new Vector2(x, s[1]));
        // tiang di ujung bukaan
        foreach (float ye in new float[] { 112, 152, 258, 312, 356, 398 })
        {
            Vector2 p = V(x, ye);
            PlacePrefab("Fences/Concrete_fences/Concrete_fence_v1/Concrete_fence_v1_column.prefab", wallRoot, p.x, p.y, 0, 1.25f, 0f, LObstacle, true);
        }
        // marka kuning di ujung gerbang utama
        foreach (float ye in new float[] { 258, 312 })
        {
            Vector2 p = V(x + 4f, ye);
            Mark(gDecor, p.x, p.y, 0.5f, 5f, 0, "paintYellow", 0.075f);
        }
    }

    // ---------- bangunan ----------


    static void BuildBuildings()
    {
        Vector2 p;
        // gudang besar timur laut
        p = V(707, 128);
        PlacePrefab("Buildings/Industrial/Hangars/Hangar_v2/Hangar_v2.prefab", gBuild, p.x, p.y, 0f, 1f, 0f, LObstacle, true);
        // gudang tenggara
        p = V(729, 448);
        PlacePrefab("Buildings/Industrial/Hangars/Hangar_v2/Hangar_v2_basic.prefab", gBuild, p.x, p.y, 0f, 1f, 0f, LObstacle, true);
        // barak timur
        p = V(917, 256);
        PlacePrefab("Buildings/Industrial/Hangars/Hangar_v4/Hangar_v4.prefab", gBuild, p.x, p.y, 0f, 1f, 0f, LObstacle, true);
        // hangar kecil utara
        p = V(549, 87);
        PlacePrefab("Buildings/Industrial/Hangars/Hangar_v1/Hangar_v1_full.prefab", gBuild, p.x, p.y, 0f, 1f, 0f, LObstacle, true);
        // gudang kecil selatan & rumah pompa
        p = V(565, 470);
        PlacePrefab("Buildings/Industrial/Hangars/Hangar_v2/Hangar_v2_outbuilding.prefab", gBuild, p.x, p.y, 0f, 1f, 0f, LObstacle, true);
        p = V(476, 458);
        PlacePrefab("Buildings/Industrial/Hangars/Hangar_v3/Hangar_v3_outbuilding_2.prefab", gBuild, p.x, p.y, 0f, 1f, 0f, LObstacle, true);
        CommandPostP();
    }

    // ---------- kontainer, tumpukan, pipa ----------
    static void BuildContainersAndStacks()
    {
        // yard barat atas
        ContainerP(gCont, 190, 93, 214, 152, 0);
        ContainerP(gCont, 190, 155, 214, 214, 1);
        ContainerP(gCont, 217, 93, 241, 152, 2);
        ContainerP(gCont, 217, 155, 241, 214, 0);
        ContainerP(gCont, 190, 93, 214, 152, 1, 3.0f, true);   // tumpuk
        ContainerP(gCont, 82, 198, 141, 220, 2);
        // yard barat bawah
        ContainerP(gCont, 66, 380, 90, 439, 0);
        ContainerP(gCont, 108, 497, 167, 519, 1);
        // yard utara dalam
        ContainerP(gCont, 296, 57, 355, 79, 1);
        ContainerP(gCont, 296, 83, 355, 105, 0);
        ContainerP(gCont, 344, 218, 403, 240, 0);
        ContainerP(gCont, 441, 218, 500, 240, 1);
        ContainerP(gCont, 503, 218, 562, 240, 2);
        ContainerP(gCont, 503, 218, 562, 240, 0, 3.0f, true);   // tumpuk hijau
        // baris selatan
        ContainerP(gCont, 343, 330, 402, 352, 1);
        ContainerP(gCont, 434, 330, 493, 352, 0);
        ContainerP(gCont, 526, 330, 585, 352, 0);
        ContainerP(gCont, 434, 330, 493, 352, 2, 3.0f, true);
        // area pertahanan
        ContainerP(gCont, 806, 395, 830, 454, 1);

        // kayu & karung (utara)
        PalletRectP(gCover, 488, 157, 565, 208);
        PalletRectP(gCover, 293, 192, 332, 236);
        CratePileP(gCover, 318, 172, 4, 1.4f);
        // pipa
        PipeSetP(gCover, "Pipes_set_v1_H_set_v4.prefab", 430, 79, 0);
        PipeSetP(gCover, "Pipes_set_v1_H_set_v4.prefab", 424, 198, 0);
        PipeSetP(gCover, "Pipes_set_v1_H_set_v3.prefab", 497, 84, 90);
        PipeSetP(gCover, "Pipes_set_v1_H_set_v4.prefab", 640, 396, 0);
        // dumpster
        DumpsterP(gCover, 316, 262, 0, true);
        DumpsterP(gCover, 316, 308, 0, false);
        DumpsterP(gCover, 150, 222, 90, true);
        DumpsterP(gCover, 876, 360, 90, false);
    }

    // ---------- area bahan bakar ----------
    static void BuildFuelStorage()
    {
        TankP(gInd, 328, 428, 0.7f); TankP(gInd, 396, 428, 0.7f);
        TankP(gInd, 328, 490, 0.7f); TankP(gInd, 396, 490, 0.7f);
        FenceLine(gInd, new Vector2(294, 394), new Vector2(436, 394), 3f, true, "FuelFence_N");
        FenceLine(gInd, new Vector2(294, 522), new Vector2(436, 522), 3f, true, "FuelFence_S");
        FenceLine(gInd, new Vector2(436, 394), new Vector2(436, 438), 3f, true, "FuelFence_E1");
        FenceLine(gInd, new Vector2(436, 472), new Vector2(436, 522), 3f, true, "FuelFence_E2");
        // pipa horizontal & tiang industri
        PlacePrefab("Other_props/Pipes/Industrial_pipes/Industrial_pipe_v1/Industrial_pipe_v1.prefab", gInd, V(420, 520).x, V(420, 520).y, 0, 0.8f, 0f, LObstacle);
        BarrelClusterP(gCover, 308, 408, 3);
    }

    // ---------- helipad ----------


    // ---------- kendaraan ----------

    // ---------- barang kecil ----------

    // ---------- dekor: decal, marka jalan, batu ----------

    // ---------- generator ----------
    static GameObject GeneratorP(string id, float px, float py, Vector3[] offs, out Transform[] attackPts)
    {
        Vector2 c = V(px, py);
        var root = Obj(gPlay, "Generator_" + id, c.x, c.y, 0).gameObject;
        root.layer = LGenerator;
        var bc = root.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 1.3f, 0); bc.size = new Vector3(5.2f, 2.6f, 2.4f);
        Transform t = root.transform;
        var body = PlacePrefab("Other_props/Generators/Generator_v1/Generator_v1.prefab", t, c.x, c.y, 0f, 1.45f, 0f, LGenerator, false, null, false);
        if (body != null) { foreach (var col in body.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(col); BrightenGenerator(body); }
        WorkLightW(t, c.x + 3.0f, c.y + 1.9f, 0f);
        BottlesW(t, c.x + 3.0f, c.y - 2.0f, 2);
        AddLight(t, "Glow", new Vector3(0, 3.3f, 0), LightType.Point, new Color(1f, 0.78f, 0.45f), 5.0f, 12f);
        AddLight(t, "FrontFill", new Vector3(0, 2.2f, 3.2f), LightType.Point, new Color(1f, 0.95f, 0.85f), 3.5f, 9f);
        AddLight(t, "BackFill", new Vector3(0, 2.2f, -3.2f), LightType.Point, new Color(1f, 0.95f, 0.85f), 3.5f, 9f);
        float rr = 3.0f;
        root.AddComponent<Outpost.Enemy.GeneratorHealth>();
        AddBlip(root, genSprite, new Color(0.35f, 0.9f, 1f), 0.35f, true, 0);
        var slotC = root.AddComponent<EnvGeneratorSlot>();
        slotC.id = id; slotC.repairRadius = rr;
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

    static void BuildLayoutOld()
    {
        BuildGround();
        BuildPerimeter();
        BuildInnerWall();
        BuildBuildings();
        BuildContainersAndStacks();
        BuildFuelStorage();
        BuildVehiclesP();
        BuildSmallPropsP();
        BuildDecorP();
    }
}
#endif
