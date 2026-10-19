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

    static GameObject Veh(GameObject g, float s = 1.2f) { if (g != null) g.transform.localScale = Vector3.one * s; return g; }
    static GameObject JeepPx(Transform p, string n, float px, float py, float yaw, bool burnt = false, bool pickup = false) { var v = V(px, py); return Veh(Jeep(p, n, v.x, v.y, yaw, burnt, pickup)); }
    static GameObject TruckPx(Transform p, string n, float px, float py, float yaw, bool burnt = false) { var v = V(px, py); return Veh(Truck(p, n, v.x, v.y, yaw, burnt)); }
    static GameObject ApcPx(Transform p, string n, float px, float py, float yaw, bool burnt) { var v = V(px, py); return Veh(Apc(p, n, v.x, v.y, yaw, burnt)); }

    static void SandbagPx(Transform p, float x0, float y0, float x1, float y1, float h = 1.15f) { SandbagWall(p, V(x0, y0), V(x1, y1), h, 0.85f); }

    // ---------- tanah ----------
    static void BuildGround()
    {
        BoxQ(gGround, "Sand", new Vector3(0, -0.15f, 0), new Vector3(170, 0.3f, 116), Mat("sand"), LGround, true, Quaternion.identity);
        BoxQ(gGround, "SandFar", new Vector3(0, -0.2f, 0), new Vector3(1600, 0.3f, 1600), Mat("sand"), 0, false, Quaternion.identity);
        Slab(gGround, "BaseAsphalt", 285, 45, 985, 545, 0.03f, Mat("asphalt"), true, LGround);
        Slab(gGround, "RoadWest", -150, 250, 300, 318, 0.045f, Mat("road"), true, LGround);
        Slab(gGround, "RoadSouth", 195, 318, 265, 700, 0.045f, Mat("road"), true, LGround);
        // beton: apron komando, halaman timur, helipad, area tangki
        Slab(gGround, "ApronCmd", 590, 205, 842, 400, 0.06f, Mat("concrete"), false);
        Slab(gGround, "ApronEast", 800, 180, 890, 425, 0.06f, Mat("concrete"), false);
        Slab(gGround, "PadNE", 818, 70, 925, 168, 0.07f, Mat("pad"), false);
        Slab(gGround, "PadSE", 820, 370, 948, 495, 0.07f, Mat("pad"), false);
        Slab(gGround, "PadFuel", 294, 392, 436, 522, 0.06f, Mat("pad"), false);
        Slab(gGround, "PadWestBay", 62, 232, 150, 252, 0.05f, Mat("pad"), false);
    }

    // ---------- pagar & menara ----------
    static void FenceLine(Transform parent, Vector2 aPx, Vector2 bPx, float h = 3.4f, bool collide = true, string name = "Fence")
    {
        Vector2 a = V(aPx.x, aPx.y), b = V(bPx.x, bPx.y);
        Vector2 d = b - a; float L = d.magnitude;
        Vector2 mid = (a + b) * 0.5f;
        var root = Obj(parent, name, mid.x, mid.y, YawAlongX(d));
        root.gameObject.layer = LFence;
        if (collide) { var bc = root.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, h * 0.5f, 0); bc.size = new Vector3(L, h, 0.4f); }
        Slot(root.gameObject, EnvCategory.Fence, "ChainLink", new Vector3(L, h, 0.1f));
        Box(root, "Mesh", new Vector3(0, h * 0.5f, 0), new Vector3(L, h, 0.04f), Mat("fence"));
        int n = Mathf.CeilToInt(L / 3.6f);
        for (int i = 0; i <= n; i++)
        {
            float x = -L * 0.5f + L * i / n;
            Cyl(root, "Post", new Vector3(x, (h + 0.3f) * 0.5f, 0), 0.07f, h + 0.3f, Mat("steelDark"), 0, false, 8);
            BoxQ(root, "ArmL", new Vector3(x, h + 0.2f, 0.2f), new Vector3(0.05f, 0.05f, 0.55f), Mat("steelDark"), 0, false, Quaternion.Euler(-35, 0, 0));
            BoxQ(root, "ArmR", new Vector3(x, h + 0.2f, -0.2f), new Vector3(0.05f, 0.05f, 0.55f), Mat("steelDark"), 0, false, Quaternion.Euler(35, 0, 0));
        }
        Box(root, "RailTop", new Vector3(0, h, 0), new Vector3(L, 0.06f, 0.06f), Mat("steelDark"));
        Box(root, "RailBottom", new Vector3(0, 0.12f, 0), new Vector3(L, 0.06f, 0.06f), Mat("steelDark"));
        Box(root, "Barb1", new Vector3(0, h + 0.38f, 0.33f), new Vector3(L, 0.025f, 0.025f), Mat("steelDark"));
        Box(root, "Barb2", new Vector3(0, h + 0.38f, -0.33f), new Vector3(L, 0.025f, 0.025f), Mat("steelDark"));
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
        {
            var post = Obj(gPerim, "GatePost", gp.x, gp.y, 0);
            Box(post, "Pillar", new Vector3(0, 2.2f, 0), new Vector3(0.9f, 4.4f, 0.9f), Mat("bconcrete"), LFence, true);
            Box(post, "Cap", new Vector3(0, 4.5f, 0), new Vector3(1.1f, 0.2f, 1.1f), Mat("steelDark"));
        }
        Vector2 gm = (g0 + g1) * 0.5f;
        Box(gPerim, "GateStripe", new Vector3(gm.x, 3.3f, gm.y), new Vector3(0.12f, 0.35f, Mathf.Abs(g1.y - g0.y)), Mat("hazard"));

        // menara sudut + menara gerbang
        Vector2 ctr = Vector2.zero;
        float ph = 0f;
        foreach (var p in new[] { new Vector2(x0 + 4, y0 + 4), new Vector2(x1 - 4, y0 + 4), new Vector2(x0 + 4, y1 - 4), new Vector2(x1 - 4, y1 - 4) })
        {
            Vector2 w = V(p.x, p.y);
            Tower(gPerim, "Tower_" + (ph / 1.7f).ToString("0"), w.x, w.y, (ctr - w).normalized, ph);
            ph += 1.7f;
        }
        Vector2 gtN = V(x0 + 9, 226), gtS = V(x0 + 9, 346);
        Tower(gPerim, "GateTower_N", gtN.x, gtN.y, new Vector2(1f, -0.5f).normalized, 0.4f);
        Tower(gPerim, "GateTower_S", gtS.x, gtS.y, new Vector2(1f, 0.5f).normalized, 2.2f);
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
        // reruntuhan tembok di sisi bukaan (sempit, tidak menutup jalan)
        Vector2 r;
        r = V(300, 108); Rubble(gCover, r.x, r.y, 1.6f, 9);
        r = V(300, 156); Rubble(gCover, r.x, r.y, 1.6f, 9);
        r = V(300, 352); Rubble(gCover, r.x, r.y, 1.6f, 9);
        r = V(300, 402); Rubble(gCover, r.x, r.y, 1.6f, 9);
    }

    // ---------- bangunan ----------
    static void CommandPost()
    {
        const float px0 = 672, py0 = 250, px1 = 800, py1 = 355;
        float w = (px1 - px0) * S, d = (py1 - py0) * S;
        Vector2 c = V((px0 + px1) * 0.5f, (py0 + py1) * 0.5f);
        var root = Obj(gBuild, "Command_Post", c.x, c.y, 0);
        root.gameObject.layer = LObstacle;
        float wallH = 6.4f;
        var bc = root.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, (wallH + 3f) * 0.5f, 0); bc.size = new Vector3(w, wallH + 3f, d);
        Slot(root.gameObject, EnvCategory.Building, "Command", bc.size);
        M wall = Mat("wallgray"), dark = Mat("steelDark"), conc = Mat("bconcrete");
        Box(root, "Foundation", new Vector3(0, 0.25f, 0), new Vector3(w + 0.6f, 0.5f, d + 0.6f), conc);
        Box(root, "Body", new Vector3(0, wallH * 0.5f, 0), new Vector3(w, wallH, d), wall);
        Box(root, "RoofSlab", new Vector3(0, wallH + 0.15f, 0), new Vector3(w + 0.5f, 0.3f, d + 0.5f), Mat("roof"));
        float py = wallH + 0.55f;
        Box(root, "ParapetN", new Vector3(0, py, d * 0.5f + 0.1f), new Vector3(w + 0.5f, 0.8f, 0.3f), conc);
        Box(root, "ParapetS", new Vector3(0, py, -d * 0.5f - 0.1f), new Vector3(w + 0.5f, 0.8f, 0.3f), conc);
        Box(root, "ParapetE", new Vector3(w * 0.5f + 0.1f, py, 0), new Vector3(0.3f, 0.8f, d + 0.5f), conc);
        Box(root, "ParapetW", new Vector3(-w * 0.5f - 0.1f, py, 0), new Vector3(0.3f, 0.8f, d + 0.5f), conc);
        // tingkat atas + parabola
        Box(root, "Tier", new Vector3(3.2f, wallH + 0.3f + 1.4f, 2.2f), new Vector3(6.4f, 2.8f, 5f), wall);
        Box(root, "TierRoof", new Vector3(3.2f, wallH + 0.3f + 2.9f, 2.2f), new Vector3(6.8f, 0.25f, 5.4f), Mat("roof"));
        Box(root, "TierWindow", new Vector3(3.2f, wallH + 1.9f, 2.2f - 2.52f), new Vector3(4.8f, 0.8f, 0.06f), Mat("winEmis"));
        CommsDish(root, new Vector3(3.2f, wallH + 3.05f, 2.2f), 0f, 1.8f, true);
        AntennaMast(root, new Vector3(-w * 0.5f + 1.5f, wallH + 0.3f, -d * 0.5f + 1.5f), 6.5f);
        AntennaMast(root, new Vector3(w * 0.5f - 1.2f, wallH + 0.3f, d * 0.5f - 1.2f), 4.5f);
        // AC & ventilasi atap
        for (int i = 0; i < 3; i++) Box(root, "RoofUnit", new Vector3(-4.5f + i * 1.9f, wallH + 0.3f + 0.6f, -2.0f), new Vector3(1.4f, 1.2f, 1.4f), Mat("cGray"));
        // pintu masuk barat dengan lampu biru
        float fx = -w * 0.5f - 0.02f;
        Box(root, "DoorRecess", new Vector3(fx, 1.5f, 0), new Vector3(0.12f, 3.0f, 3.2f), dark);
        Box(root, "DoorGlowTop", new Vector3(fx - 0.06f, 3.15f, 0), new Vector3(0.1f, 0.14f, 3.4f), Mat("zoneB"));
        Box(root, "DoorGlowL", new Vector3(fx - 0.06f, 1.6f, 1.65f), new Vector3(0.1f, 3.2f, 0.12f), Mat("zoneB"));
        Box(root, "DoorGlowR", new Vector3(fx - 0.06f, 1.6f, -1.65f), new Vector3(0.1f, 3.2f, 0.12f), Mat("zoneB"));
        AddLight(root, "EntranceGlow", new Vector3(fx - 2f, 2.6f, 0), LightType.Point, new Color(0.3f, 0.6f, 1f), 2.4f, 10f);
        // jendela celah
        for (int i = 0; i < 4; i++)
        {
            float z = -d * 0.5f + 2f + i * (d - 4f) / 3f;
            bool lit = (i % 2) == 0;
            Box(root, "SlitW", new Vector3(fx - 0.02f, 4.4f, z * 0.8f), new Vector3(0.08f, 0.55f, 1.4f), Mat(lit ? "winEmis" : "glass"));
            Box(root, "SlitE", new Vector3(w * 0.5f + 0.02f, 4.4f, z * 0.8f), new Vector3(0.08f, 0.55f, 1.4f), Mat(lit ? "winEmis" : "glass"));
        }
        for (int i = 0; i < 4; i++)
        {
            float xx = -w * 0.5f + 2.5f + i * (w - 5f) / 3f;
            Box(root, "SlitS", new Vector3(xx, 4.4f, -d * 0.5f - 0.02f), new Vector3(1.6f, 0.55f, 0.08f), Mat(i % 2 == 0 ? "winEmis" : "glass"));
            Box(root, "SlitN", new Vector3(xx, 4.4f, d * 0.5f + 0.02f), new Vector3(1.6f, 0.55f, 0.08f), Mat(i % 3 == 0 ? "winEmis" : "glass"));
        }
        // strip zona B di lantai
        float rx = w * 0.5f + 2.2f, rz = d * 0.5f + 2.2f;
        Mark(root, 0, rz, w + 4.4f, 0.22f, 0, "zoneB", 0.09f); Mark(root, 0, -rz, w + 4.4f, 0.22f, 0, "zoneB", 0.09f);
        Mark(root, rx, 0, 0.22f, d + 4.4f, 0, "zoneB", 0.09f); Mark(root, -rx, 0, 0.22f, d + 4.4f, 0, "zoneB", 0.09f);
        // trafo & kotak listrik
        Vector2 ep = V(672 - 14, 262);
        PlacePrefab("Other_props/Electric_box/Electric_box_v3/Electric_box_v3.prefab", gCover, ep.x, ep.y, 90f, 1f, 0f, LCover);
    }

    static void GuardBooth(string name, float px0, float py0, float px1, float py1)
    {
        float w = (px1 - px0) * S, d = (py1 - py0) * S;
        Vector2 c = V((px0 + px1) * 0.5f, (py0 + py1) * 0.5f);
        var root = Obj(gBuild, name, c.x, c.y, 0);
        root.gameObject.layer = LObstacle;
        var bc = root.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 1.7f, 0); bc.size = new Vector3(w, 3.4f, d);
        Slot(root.gameObject, EnvCategory.Building, "GuardBooth", bc.size);
        Box(root, "Body", new Vector3(0, 1.4f, 0), new Vector3(w, 2.8f, d), Mat("wallgreen"));
        Box(root, "Roof", new Vector3(0, 3.0f, 0), new Vector3(w + 0.5f, 0.2f, d + 0.5f), Mat("steelDark"));
        Box(root, "Window", new Vector3(w * 0.5f + 0.02f, 1.7f, 0), new Vector3(0.06f, 0.8f, d - 0.8f), Mat("winEmis"));
        Box(root, "Stripe", new Vector3(0, 0.2f, 0), new Vector3(w + 0.04f, 0.4f, d + 0.04f), Mat("hazard"));
        AddLight(root, "Glow", new Vector3(w * 0.5f + 1.2f, 2.2f, 0), LightType.Point, new Color(1f, 0.75f, 0.4f), 1.6f, 7f);
    }

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
        CommandPost();
        GuardBooth("GuardBooth_N", 62, 226, 88, 248);
        GuardBooth("GuardBooth_S", 62, 322, 88, 344);
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
        ContainerP(gCont, 92, 497, 151, 519, 1);
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
        Vector2 p = V(362, 459);
        Cyl(gInd, "Manifold", new Vector3(p.x, 0.5f, p.y), 0.14f, 4f, Mat("pipe"), 0, false, 10);
        PlacePrefab("Other_props/Pipes/Industrial_pipes/Industrial_pipe_v1/Industrial_pipe_v1.prefab", gInd, V(420, 520).x, V(420, 520).y, 0, 0.8f, 0f, LObstacle);
        Vector2 q = V(300, 405);
        BarrelClusterP(gCover, 308, 408, 3);
    }

    // ---------- helipad ----------
    static void Helipad(float px0, float py0, float px1, float py1, bool withHeli)
    {
        Vector2 c = PxC(px0, py0, px1, py1);
        float r = Mathf.Min(px1 - px0, py1 - py0) * S * 0.36f;
        int seg = 36;
        for (int i = 0; i < seg; i++)
        {
            float a = (i + 0.5f) / seg * Mathf.PI * 2f;
            Vector3 pos = new Vector3(c.x + Mathf.Cos(a) * r, 0.085f, c.y + Mathf.Sin(a) * r);
            float segLen = 2f * Mathf.PI * r / seg * 0.95f;
            Box(gDecor, "HRing", pos, new Vector3(segLen, 0.012f, 0.45f), Mat("paintWhite"), 0, false, -a * Mathf.Rad2Deg + 90f);
        }
        float hh = r * 0.55f;
        Mark(gDecor, c.x - hh * 0.5f, c.y, 0.45f, hh * 1.7f, 0, "paintWhite", 0.09f);
        Mark(gDecor, c.x + hh * 0.5f, c.y, 0.45f, hh * 1.7f, 0, "paintWhite", 0.09f);
        Mark(gDecor, c.x, c.y, hh, 0.45f, 0, "paintWhite", 0.09f);
        float hx = (px1 - px0) * S * 0.5f - 0.7f, hz = (py1 - py0) * S * 0.5f - 0.7f;
        foreach (float sx in new float[] { -1, 1 })
            foreach (float sz in new float[] { -1, 1 })
            {
                Box(gDecor, "PadLight", new Vector3(c.x + sx * hx, 0.18f, c.y + sz * hz), new Vector3(0.35f, 0.25f, 0.35f), Mat("lampEmis"));
                AddLight(gLights, "PadLightL", new Vector3(c.x + sx * hx, 0.8f, c.y + sz * hz), LightType.Point, new Color(1f, 0.85f, 0.5f), 1.2f, 6f);
            }
        if (withHeli) Helicopter(gVeh, c.x, c.y, 25f);
    }

    static void Helicopter(Transform parent, float x, float z, float yaw)
    {
        var root = Obj(parent, "Helicopter", x, z, yaw);
        root.gameObject.layer = LCover;
        var bc = root.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 1.6f, -0.6f); bc.size = new Vector3(9.5f, 3.2f, 2.8f);
        Slot(root.gameObject, EnvCategory.Vehicle, "Helicopter", bc.size);
        M body = Mat("olive"), dk = Mat("steelDark");
        Box(root, "Cabin", new Vector3(0, 1.75f, 0), new Vector3(3.8f, 1.9f, 2.4f), body);
        Box(root, "Nose", new Vector3(2.4f, 1.55f, 0), new Vector3(1.4f, 1.4f, 2.0f), body);
        Box(root, "Glass", new Vector3(3.12f, 1.75f, 0), new Vector3(0.08f, 0.95f, 1.8f), Mat("glass"));
        Box(root, "Boom", new Vector3(-4.2f, 2.1f, 0), new Vector3(4.8f, 0.55f, 0.6f), body);
        Box(root, "Fin", new Vector3(-6.4f, 2.8f, 0), new Vector3(0.6f, 1.5f, 0.14f), body);
        Box(root, "Stabilizer", new Vector3(-6.0f, 2.2f, 0), new Vector3(0.7f, 0.08f, 1.5f), body);
        foreach (float sz in new float[] { -1.0f, 1.0f })
        {
            Box(root, "Skid", new Vector3(0.2f, 0.25f, sz), new Vector3(5.2f, 0.1f, 0.12f), dk);
            Box(root, "StrutF", new Vector3(1.2f, 0.7f, sz), new Vector3(0.08f, 1.0f, 0.08f), dk);
            Box(root, "StrutB", new Vector3(-1.2f, 0.7f, sz), new Vector3(0.08f, 1.0f, 0.08f), dk);
        }
        Cyl(root, "Mast", new Vector3(0, 3.0f, 0), 0.14f, 0.6f, dk, 0, false, 10);
        var rot = new GameObject("Rotor").transform; rot.SetParent(root, false); rot.localPosition = new Vector3(0, 3.35f, 0);
        var sp = rot.gameObject.AddComponent<EnvSpin>(); sp.axis = Vector3.up; sp.degreesPerSecond = 90f;
        BoxQ(rot, "BladeA", Vector3.zero, new Vector3(10f, 0.05f, 0.4f), dk, 0, false, Quaternion.identity, false);
        BoxQ(rot, "BladeB", Vector3.zero, new Vector3(0.4f, 0.05f, 10f), dk, 0, false, Quaternion.identity, false);
    }

    // ---------- kendaraan ----------
    static void BuildVehicles()
    {
        // yard barat atas
        TruckPx(gVeh, "Truck_W1", 150, 100, 40);
        JeepPx(gVeh, "Jeep_W1", 104, 150, 90);
        JeepPx(gVeh, "Jeep_W2", 106, 182, -8, false, true);
        TruckPx(gVeh, "Truck_W2", 122, 130, 78, true);
        // yard barat bawah
        JeepPx(gVeh, "Jeep_W3", 118, 336, 6);
        TruckPx(gVeh, "Truck_W3", 135, 380, 0);
        TruckPx(gVeh, "Truck_W4", 163, 450, 90);
        JeepPx(gVeh, "Jeep_W4", 104, 466, 200, true);
        ApcPx(gVeh, "Apc_W1", 130, 420, 30, false);
        // jalur tengah (bangkai penghalang)
        JeepPx(gVeh, "Wreck_Jeep_M1", 425, 262, 15, true);
        TruckPx(gVeh, "Wreck_Truck_M1", 560, 275, 10, true);
        JeepPx(gVeh, "Jeep_M2", 398, 322, 170);
        // halaman belakang
        JeepPx(gVeh, "Jeep_D1", 842, 150, 90, false, true);
        ApcPx(gVeh, "Apc_D1", 848, 214, 0, false);
        // jalur selatan
        ApcPx(gVeh, "Wreck_Apc_S1", 604, 372, 110, true);
        JeepPx(gVeh, "Wreck_Jeep_N1", 400, 116, 160, true);
        Helipad(820, 372, 948, 495, true);
        Helipad(818, 70, 925, 168, false);
    }

    // ---------- barang kecil ----------
    static void BuildSmallProps()
    {
        BarrelClusterP(gCover, 297, 242, 4);
        BarrelClusterP(gCover, 297, 330, 3);
        BarrelClusterP(gCover, 352, 196, 3);
        BarrelClusterP(gCover, 505, 130, 3);
        BarrelClusterP(gCover, 470, 400, 3);
        BarrelClusterP(gCover, 860, 118, 4);
        BarrelClusterP(gCover, 838, 392, 3);
        BarrelClusterP(gCover, 150, 245, 4);
        BarrelClusterP(gCover, 112, 214, 3);
        BarrelClusterP(gCover, 78, 352, 3);
        BarrelClusterP(gCover, 180, 425, 3);
        foreach (var p in new[] { new Vector2(330, 262), new Vector2(505, 322), new Vector2(618, 236), new Vector2(618, 356), new Vector2(305, 172), new Vector2(560, 395), new Vector2(852, 300) })
            BurningBarrelP(gCover, p.x, p.y);
        CratePileP(gCover, 868, 322, 4, 1.3f);
        CratePileP(gCover, 150, 160, 5, 1.4f);
        CratePileP(gCover, 252, 440, 3, 1.4f);
        CratePileP(gCover, 612, 420, 3, 1.3f);
        { Vector2 p = V(872, 335); AmmoCrate(gCover, p.x, p.y, 20); p = V(872, 342); AmmoCrate(gCover, p.x, p.y, -10); }
        { Vector2 p = V(850, 246); AmmoCrate(gCover, p.x, p.y, 0); }

        // sandbag
        SandbagPx(gCover, 598, 255, 598, 282);
        SandbagPx(gCover, 598, 322, 598, 350);
        SandbagPx(gCover, 460, 258, 460, 284);
        SandbagPx(gCover, 524, 386, 524, 416);
        SandbagPx(gCover, 596, 386, 596, 416);
        SandbagPx(gCover, 748, 250, 770, 250, 1.0f);
        // blok jalan / jersey
        RoadBlockP(gCover, 318, 276, 0); RoadBlockP(gCover, 318, 296, 0);
        RoadBlockP(gCover, 622, 214, 0); RoadBlockP(gCover, 662, 246, 0);
        RoadBlockP(gCover, 650, 364, 0); RoadBlockP(gCover, 650, 390, 0);
        RoadBlockP(gCover, 404, 285, 90);
        // landak baja
        foreach (var hp in new[] { new Vector2(335, 132), new Vector2(335, 377), new Vector2(380, 288), new Vector2(262, 285), new Vector2(598, 160) })
        { Vector2 p = V(hp.x, hp.y); Hedgehog(gCover, p.x, p.y, Rf(0, 90)); }
        // ban, gulungan kabel
        { Vector2 p = V(350, 248); TireStack(gCover, p.x, p.y, 3); p = V(442, 352); TireStack(gCover, p.x, p.y, 4); p = V(612, 340); TireStack(gCover, p.x, p.y, 3); p = V(250, 420); TireStack(gCover, p.x, p.y, 3); }
        { Vector2 p = V(150, 400); CableSpool(gCover, p.x, p.y); p = V(512, 432); CableSpool(gCover, p.x, p.y); }
        // lampu jalan
        float[][] lamps = {
            new[]{312f,238f}, new[]{312f,334f}, new[]{452f,243f}, new[]{520f,140f}, new[]{430f,370f}, new[]{575f,372f}, new[]{648f,236f},
            new[]{648f,344f}, new[]{705f,230f}, new[]{705f,386f}, new[]{835f,232f}, new[]{835f,352f}, new[]{380f,112f}, new[]{600f,175f}, new[]{140f,250f}, new[]{140f,320f} };
        foreach (var l in lamps)
        {
            Vector2 p = V(l[0], l[1]);
            Vector2 toC = (V(512, 286) - p);
            LampPost(gLights, p.x, p.y, YawAlongX(toC), true);
        }
    }

    // ---------- dekor: decal, marka jalan, batu ----------
    static void BuildDecor()
    {
        // marka jalan barat
        for (float px = -140; px < 280; px += 22)
        { Vector2 p = V(px, 285); Mark(gDecor, p.x, p.y, 3.2f, 0.18f, 0, "paintYellow", 0.075f); }
        { Vector2 a = V(-140, 253), b = V(285, 253); Mark(gDecor, (a.x + b.x) * 0.5f, a.y, b.x - a.x, 0.16f, 0, "paintWhite", 0.075f); }
        { Vector2 a = V(-140, 315), b = V(285, 315); Mark(gDecor, (a.x + b.x) * 0.5f, a.y, b.x - a.x, 0.16f, 0, "paintWhite", 0.075f); }
        // chevron area spawn penyerang
        for (int i = 0; i < 5; i++)
        {
            Vector2 p = V(105 + i * 30, 285);
            BoxQ(gDecor, "ChevA", new Vector3(p.x, 0.09f, p.y + 1.1f), new Vector3(3.4f, 0.015f, 0.3f), Mat("zoneA"), 0, false, Quaternion.Euler(0, -35, 0));
            BoxQ(gDecor, "ChevB", new Vector3(p.x, 0.09f, p.y - 1.1f), new Vector3(3.4f, 0.015f, 0.3f), Mat("zoneA"), 0, false, Quaternion.Euler(0, 35, 0));
        }
        // marka penanda zona
        { Vector2 p = V(842, 290); float hs = 3.6f;
          Box(gDecor, "DefPadN", new Vector3(p.x, 0.09f, p.y + hs), new Vector3(hs * 2f, 0.02f, 0.2f), Mat("zoneB"));
          Box(gDecor, "DefPadS", new Vector3(p.x, 0.09f, p.y - hs), new Vector3(hs * 2f, 0.02f, 0.2f), Mat("zoneB"));
          Box(gDecor, "DefPadE", new Vector3(p.x + hs, 0.09f, p.y), new Vector3(0.2f, 0.02f, hs * 2f), Mat("zoneB"));
          Box(gDecor, "DefPadW", new Vector3(p.x - hs, 0.09f, p.y), new Vector3(0.2f, 0.02f, hs * 2f), Mat("zoneB")); }

        // bercak aspal & pasir
        var r2 = new System.Random(777);
        for (int i = 0; i < 90; i++)
        {
            float px = 300 + (float)r2.NextDouble() * 670, py = 55 + (float)r2.NextDouble() * 480;
            Vector2 p = V(px, py);
            float s = 2f + (float)r2.NextDouble() * 5f;
            Decal(gDecor, "stain", p.x, p.y, s, s * (0.6f + (float)r2.NextDouble() * 0.6f), (float)r2.NextDouble() * 360f, 0.052f + i * 0.00005f);
        }
        for (int i = 0; i < 80; i++)
        {
            bool west = i < 35;
            float px, py;
            if (west) { px = 60 + (float)r2.NextDouble() * 225; py = 50 + (float)r2.NextDouble() * 490; }
            else
            {
                // pasir menumpuk di tepi dalam kompleks
                int edge = r2.Next(4);
                if (edge == 0) { px = 300 + (float)r2.NextDouble() * 660; py = 50 + (float)r2.NextDouble() * 30; }
                else if (edge == 1) { px = 300 + (float)r2.NextDouble() * 660; py = 510 + (float)r2.NextDouble() * 32; }
                else if (edge == 2) { px = 945 + (float)r2.NextDouble() * 38; py = 50 + (float)r2.NextDouble() * 490; }
                else { px = 290 + (float)r2.NextDouble() * 30; py = 50 + (float)r2.NextDouble() * 490; }
            }
            Vector2 p = V(px, py);
            float s = 4f + (float)r2.NextDouble() * 9f;
            Decal(gDecor, "patch", p.x, p.y, s, s * (0.5f + (float)r2.NextDouble() * 0.6f), (float)r2.NextDouble() * 360f, 0.058f + i * 0.00006f);
        }
        // batu di luar pagar
        for (int i = 0; i < 70; i++)
        {
            float x = (float)(r2.NextDouble() * 2 - 1) * 78f, z = (float)(r2.NextDouble() * 2 - 1) * 50f;
            bool inside = x > -63f && x < 65f && z > -37f && z < 34f;
            if (inside) continue;
            if (Mathf.Abs(z - V(0, 285).y) < 5.5f && x < -60f) continue;    // jangan tutup jalan barat
            float s = 0.5f + (float)r2.NextDouble() * 1.8f;
            BoxQ(gDecor, "Rock", new Vector3(x, s * 0.25f, z), new Vector3(s * 1.4f, s * 0.7f, s), Mat("rubble"), 0, false, Quaternion.Euler((float)r2.NextDouble() * 15f, (float)r2.NextDouble() * 360f, (float)r2.NextDouble() * 15f));
        }
        // debu halus di jalur
        foreach (var dp in new[] { new Vector2(430, 285), new Vector2(560, 300), new Vector2(380, 130), new Vector2(560, 375), new Vector2(200, 285) })
        {
            Vector2 p = V(dp.x, dp.y);
            AddParticles(gDecor, "Dust", new Vector3(p.x, 0.1f, p.y), "smoke", new Color(0.78f, 0.66f, 0.48f, 0.12f), new Color(0.78f, 0.66f, 0.48f, 0f), 2.5f, 5f, 8f, 0.3f, 0.7f, 3f, 5f, 80f, 6f, 0f, 1.6f);
        }
    }

    // ---------- generator ----------
    static GameObject GeneratorP(string id, float px, float py, Vector3[] offs, out Transform[] attackPts)
    {
        Vector2 c = V(px, py);
        var root = Obj(gPlay, "Generator_" + id, c.x, c.y, 0).gameObject;
        root.layer = LGenerator;
        var bc = root.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 1.3f, 0); bc.size = new Vector3(5.2f, 2.6f, 2.4f);
        Transform t = root.transform;
        Box(t, "Slab", new Vector3(0, 0.1f, 0), new Vector3(7f, 0.2f, 5f), Mat("bconcrete"));
        var body = PlacePrefab("Other_props/Generators/Generator_v1/Generator_v1.prefab", t, c.x, c.y, 0f, 1.45f, 0.2f, LGenerator, false, null, false);
        if (body != null) foreach (var col in body.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(col);
        Box(t, "Beacon", new Vector3(0, 3.1f, 0), new Vector3(0.3f, 0.3f, 0.3f), Mat("ringEmis"));
        Cyl(t, "BeaconPost", new Vector3(0, 2.6f, 0), 0.06f, 1.0f, Mat("steelDark"), 0, false, 8);
        var warn = Box(t, "LedWarn", new Vector3(-1.2f, 2.3f, 1.0f), new Vector3(0.18f, 0.18f, 0.06f), Mat("redEmis"));
        var bl = warn.AddComponent<EnvBlink>(); bl.target = warn.GetComponent<MeshRenderer>(); bl.period = 2.2f;
        AddLight(t, "Glow", new Vector3(0, 3.3f, 0), LightType.Point, new Color(1f, 0.6f, 0.25f), 2.0f, 9f);
        float rr = 3.0f; int seg = 32;
        for (int i = 0; i < seg; i++)
        {
            float a = (i + 0.5f) / seg * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(a) * rr, 0.09f, Mathf.Sin(a) * rr);
            float segLen = 2f * Mathf.PI * rr / seg * 0.55f;
            Box(t, "Ring", p, new Vector3(segLen, 0.02f, 0.2f), Mat("ringEmis"), 0, false, -a * Mathf.Rad2Deg + 90f);
        }
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

    static void BuildLayout()
    {
        BuildGround();
        BuildPerimeter();
        BuildInnerWall();
        BuildBuildings();
        BuildContainersAndStacks();
        BuildFuelStorage();
        BuildVehicles();
        BuildSmallProps();
        BuildDecor();
    }
}
#endif
