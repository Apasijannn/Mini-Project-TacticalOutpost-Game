#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Outpost.Env;

// Penempatan prefab asli dari Industrial Set v2.0.
public static partial class OutpostBuilder
{
    static readonly Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();
    static readonly List<string> missingPrefabs = new List<string>();
    static int prefabCount;

    static GameObject LoadPrefab(string rel)
    {
        GameObject go;
        if (prefabCache.TryGetValue(rel, out go) && go != null) return go;
        go = AssetDatabase.LoadAssetAtPath<GameObject>(PackRoot + "/" + rel);
        if (go == null) { if (!missingPrefabs.Contains(rel)) missingPrefabs.Add(rel); return null; }
        prefabCache[rel] = go;
        return go;
    }

    static Bounds RBounds(GameObject go)
    {
        bool has = false; Bounds b = new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    const StaticEditorFlags StaticAll = StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;

    // Menaruh prefab sehingga pusat bounds berada di (x,z) dan alasnya di baseY.
    static GameObject PlacePrefab(string rel, Transform parent, float x, float z, float yaw, float scale = 1f, float baseY = 0f, int layer = 0, bool solidBox = false, Vector3? scaleXYZ = null, bool isStatic = true)
    {
        var asset = LoadPrefab(rel);
        if (asset == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scaleXYZ.HasValue ? scaleXYZ.Value : Vector3.one * scale;

        bool hasCol = go.GetComponentsInChildren<Collider>(true).Length > 0;
        if (solidBox || !hasCol)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            Bounds b0 = RBounds(go);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = go.transform.InverseTransformPoint(b0.center);
            Vector3 ls = go.transform.lossyScale;
            bc.size = new Vector3(b0.size.x / Mathf.Abs(ls.x), b0.size.y / Mathf.Abs(ls.y), b0.size.z / Mathf.Abs(ls.z));
        }
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        Bounds b = RBounds(go);
        go.transform.position += new Vector3(x - b.center.x, baseY - b.min.y, z - b.center.z);
        if (layer != 0) SetLayerRecursive(go, layer);
        if (isStatic) foreach (var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticAll);
        prefabCount++;
        return go;
    }

    static Vector2 PxC(float x0, float y0, float x1, float y1) { return V((x0 + x1) * 0.5f, (y0 + y1) * 0.5f); }

    // ---------- kontainer ----------
    static Material[] contMats;
    static Material ContMat(int i)
    {
        if (contMats == null)
        {
            contMats = new Material[3];
            for (int k = 0; k < 3; k++)
                contMats[k] = AssetDatabase.LoadAssetAtPath<Material>(PackRoot + "/Containers/Cargo_container_v1/Source/Cargo_container_v" + (k + 1) + ".mat");
        }
        return contMats[Mathf.Clamp(i, 0, 2)];
    }

    static GameObject ContainerP(Transform parent, float px0, float py0, float px1, float py1, int color, float baseY = 0f, bool stacked = false, bool open = false)
    {
        float w = Mathf.Abs(px1 - px0), d = Mathf.Abs(py1 - py0);
        bool vertical = d > w;
        Vector2 c = PxC(px0, py0, px1, py1);
        string rel = open ? "Containers/Cargo_container_v1/Cargo_container_v1_LD2open.prefab" : (rng.NextDouble() < 0.5 ? "Containers/Cargo_container_v1/Cargo_container_v1_LD1close.prefab" : "Containers/Cargo_container_v1/Cargo_container_v1_LD2close.prefab");
        float yaw = (vertical ? 90f : 0f) + (rng.NextDouble() < 0.5 ? 0f : 180f);
        var go = PlacePrefab(rel, parent, c.x, c.y, yaw, 1f, baseY, stacked ? LObstacle : LCover);
        if (go == null) return null;
        var mat = ContMat(color);
        if (mat != null) foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true)) { if (r.sharedMaterials.Length == 1) r.sharedMaterial = mat; }
        go.name = "Container_" + (parent != null ? parent.childCount : 0);
        Slot(go, EnvCategory.Container, "Cargo", RBounds(go).size);
        return go;
    }

    // ---------- barrel / peti / pallet / pipa ----------
    static readonly string[] barrelPrefabs = {
        "Barrels/Barrel_v1/Barrel_v1_LD2.prefab", "Barrels/Barrel_v2/Barrel_v2_single.prefab", "Barrels/Barrel_v3/Barrel_v3_single.prefab", "Barrels/Barrel_v1/Barrel_v1_LD3.prefab" };

    static GameObject BarrelP(Transform parent, float x, float z, int variant = -1)
    {
        string rel = barrelPrefabs[variant >= 0 ? variant % barrelPrefabs.Length : rng.Next(barrelPrefabs.Length)];
        var go = PlacePrefab(rel, parent, x, z, Rf(0, 360), 1f, 0f, LCover);
        if (go != null) Slot(go, EnvCategory.Barrel, "Barrel", RBounds(go).size);
        return go;
    }

    static void BarrelClusterP(Transform parent, float px, float py, int n)
    {
        Vector2 p = V(px, py);
        if (n >= 3 && rng.NextDouble() < 0.6)
        {
            string rel = rng.NextDouble() < 0.5 ? "Barrels/Barrel_v2/Barrel_v2_quadro.prefab" : "Barrels/Barrel_v3/Barrel_v3_quadro.prefab";
            var g = PlacePrefab(rel, parent, p.x, p.y, Rf(0, 360), 1f, 0f, LCover);
            if (g != null) { Slot(g, EnvCategory.Barrel, "Quad", RBounds(g).size); n -= 3; }
        }
        for (int i = 0; i < n; i++) BarrelP(parent, p.x + Rf(-1.4f, 1.4f), p.y + Rf(-1.4f, 1.4f));
    }

    static GameObject BurningBarrelP(Transform parent, float px, float py)
    {
        Vector2 p = V(px, py);
        var b = BarrelP(parent, p.x, p.y, 0);
        if (b != null) Fire(b.transform, new Vector3(0f, 0.55f, 0f));
        return b;
    }

    static GameObject CrateP(Transform parent, float x, float z, float scale, float yaw, float baseY = 0f, bool longBox = false)
    {
        string rel = longBox ? "Boxes/Wooden_box_v1/Wooden_box_v1_LD1.prefab" : (rng.NextDouble() < 0.5 ? "Boxes/Wooden_box_v1/Wooden_box_v1_LD1square.prefab" : "Boxes/Wooden_box_v1/Wooden_box_v1_LD2square.prefab");
        var go = PlacePrefab(rel, parent, x, z, yaw, scale, baseY, LCover);
        if (go != null) Slot(go, EnvCategory.Crate, "Wood", RBounds(go).size);
        return go;
    }

    static void CratePileP(Transform parent, float px, float py, int n, float scale = 1.5f)
    {
        Vector2 p = V(px, py);
        float step = 0.82f * scale + 0.05f;
        int cols = Mathf.CeilToInt(Mathf.Sqrt(n));
        int placed = 0;
        for (int i = 0; i < n; i++)
        {
            int cx = i % cols, cz = i / cols;
            CrateP(parent, p.x + (cx - (cols - 1) * 0.5f) * step, p.y + (cz - (cols - 1) * 0.5f) * step, scale, Rf(-8, 8) + (rng.NextDouble() < 0.5 ? 0 : 90));
            placed++;
        }
        if (placed >= 3) CrateP(parent, p.x, p.y, scale * 0.9f, Rf(-15, 15), 0.82f * scale);
    }

    static void PalletRectP(Transform parent, float px0, float py0, float px1, float py1)
    {
        // isi persegi (pixel) dengan pallet berisi karung/kargo
        Vector2 a = V(px0, py0), b = V(px1, py1);
        float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), z0 = Mathf.Min(a.y, b.y), z1 = Mathf.Max(a.y, b.y);
        float cw = 2.6f, cd = 1.75f;
        int nx = Mathf.Max(1, Mathf.FloorToInt((x1 - x0) / cw)), nz = Mathf.Max(1, Mathf.FloorToInt((z1 - z0) / cd));
        string[] rels = { "Other_props/Palets/Bags_on_pallet_v1/Bags_on_pallet_v1_1.prefab", "Other_props/Palets/Bags_on_pallet_v1/Bags_on_pallet_v1_2.prefab", "Other_props/Palets/Palet_v1/Palet_v1_set.prefab" };
        for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                float x = x0 + (ix + 0.5f) * (x1 - x0) / nx, z = z0 + (iz + 0.5f) * (z1 - z0) / nz;
                var g = PlacePrefab(rels[rng.Next(rels.Length)], parent, x, z, (rng.NextDouble() < 0.5 ? 0f : 180f) + Rf(-4, 4), 1f, 0f, LCover);
                if (g != null) Slot(g, EnvCategory.Pallet, "Pallet", RBounds(g).size);
            }
    }

    static GameObject PipeSetP(Transform parent, string variantRel, float px, float py, float yaw)
    {
        Vector2 p = V(px, py);
        var g = PlacePrefab("Other_props/Pipes/Pipe_sets/Pipes_set_v1/" + variantRel, parent, p.x, p.y, yaw, 1f, 0f, LCover);
        if (g != null) Slot(g, EnvCategory.Pipe, variantRel, RBounds(g).size);
        return g;
    }

    static GameObject DumpsterP(Transform parent, float px, float py, float yaw, bool garbage)
    {
        Vector2 p = V(px, py);
        var g = PlacePrefab("Dumpsters/Dumpsters_v1/Dumpsters_v1_" + (garbage ? "garbadge" : "empty") + ".prefab", parent, p.x, p.y, yaw, 1f, 0f, LCover);
        if (g != null) Slot(g, EnvCategory.Other, "Dumpster", RBounds(g).size);
        return g;
    }

    static GameObject RoadBlockP(Transform parent, float px, float py, float yaw)
    {
        Vector2 p = V(px, py);
        var g = PlacePrefab("Fences/Road_blocks/Road_block_v1/Road_block_v1.prefab", parent, p.x, p.y, yaw, 1.25f, 0f, LCover);
        if (g != null) Slot(g, EnvCategory.Barrier, "RoadBlock", RBounds(g).size);
        return g;
    }

    static GameObject TankP(Transform parent, float px, float py, float scale)
    {
        Vector2 p = V(px, py);
        var g = PlacePrefab("Oil_tanks/Oil_tank_v1/Oil_tank_v1.prefab", parent, p.x, p.y, Rf(0, 360), scale, 0f, LObstacle, true);
        if (g != null) Slot(g, EnvCategory.FuelTank, "Tank", RBounds(g).size);
        return g;
    }

    // dinding beton: dari (px,py a) ke (b); dipecah dalam modul 9.37 m yang diskalakan memanjang
    static void ConcreteWallP(Transform parent, Vector2 aPx, Vector2 bPx)
    {
        Vector2 a = V(aPx.x, aPx.y), b = V(bPx.x, bPx.y);
        Vector2 d = b - a; float L = d.magnitude;
        const float mod = 9.368f;
        int n = Mathf.Max(1, Mathf.RoundToInt(L / mod));
        float seg = L / n;
        float yaw = YawAlongZ(d);
        for (int i = 0; i < n; i++)
        {
            Vector2 c = a + d * ((i + 0.5f) / n);
            // modul memanjang di sumbu Z lokal
            PlacePrefab("Fences/Concrete_fences/Concrete_fence_v1/Concrete_fence_v1_wall_set_v1.prefab", parent, c.x, c.y, yaw, 1f, 0f, LObstacle, true, new Vector3(1.25f, 1.0f, seg / mod));
        }
    }

    static float YawAlongZ(Vector2 d) { return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg; }  // sumbu Z lokal searah d
}
#endif
