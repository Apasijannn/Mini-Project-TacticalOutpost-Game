#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using Outpost.Env;

// Generator environment "Tactical Outpost". Menu: Tools > Tactical Outpost
public static partial class OutpostBuilder
{
    public const float S = 0.135f;                 // meter per pixel (disesuaikan agar kontainer pack 8 m ≈ 59 px pada gambar referensi)
    const string Root = "Assets/Outpost";
    const string MatDir = Root + "/Materials";
    const string TexDir = Root + "/Textures";
    const string MeshDir = Root + "/Meshes";
    const string MeshAssetPath = MeshDir + "/OutpostMeshes.asset";
    const string ScenePath = "Assets/Scenes/TacticalOutpost.unity";

    public const int LPlayer = 8, LEnemy = 9, LObstacle = 10, LCover = 11, LGenerator = 12, LGround = 13, LMarker = 14, LFence = 15;

    // ---------- koordinat ----------
    public static Vector2 V(float px, float py) { return new Vector2((px - 512f) * S, (286f - py) * S); }
    static string F(float v) { return v.ToString("F3", CultureInfo.InvariantCulture); }
    static float YawAlongX(Vector2 d) { return Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg; }   // local +X searah d
    static float YawFacing(Vector2 d) { return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg; }    // local +Z searah d

    static System.Random rng = new System.Random(12345);
    static float Rf(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

    // ---------- material wrapper ----------
    sealed class M { public Material mat; public float tile; public string key; }
    static readonly Dictionary<string, M> mats = new Dictionary<string, M>();
    static M Mat(string key)
    {
        M m;
        if (!mats.TryGetValue(key, out m)) throw new Exception("Material tidak ada: " + key);
        return m;
    }

    // ---------- mesh builder ----------
    sealed class MB
    {
        public List<Vector3> v = new List<Vector3>();
        public List<Vector3> n = new List<Vector3>();
        public List<Vector2> uv = new List<Vector2>();
        public List<int> t = new List<int>();

        public void TriN(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            Vector3 cr = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(cr, na + nb + nc) < 0f)
            {
                Vector3 tv = b; b = c; c = tv;
                Vector3 tn = nb; nb = nc; nc = tn;
                Vector2 tu = ub; ub = uc; uc = tu;
            }
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            n.Add(na); n.Add(nb); n.Add(nc);
            uv.Add(ua); uv.Add(ub); uv.Add(uc);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 nrm, Vector2 ua, Vector2 ub, Vector2 uc)
        { TriN(a, b, c, nrm, nrm, nrm, ua, ub, uc); }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nrm, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        { Tri(a, b, c, nrm, ua, ub, uc); Tri(a, c, d, nrm, ua, uc, ud); }
        public void QuadN(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        { TriN(a, b, c, na, nb, nc, ua, ub, uc); TriN(a, c, d, na, nc, nd, ua, uc, ud); }

        public Mesh Build(string name)
        {
            var m = new Mesh();
            m.name = name;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }

    static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
    static Mesh meshHost;

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void ResetMeshAsset()
    {
        meshCache.Clear();
        meshHost = null;
        EnsureFolder(MeshDir);
        if (File.Exists(MeshAssetPath)) AssetDatabase.DeleteAsset(MeshAssetPath);
    }

    static Mesh Register(Mesh m, string key)
    {
        m.name = key;
        if (meshHost == null) { AssetDatabase.CreateAsset(m, MeshAssetPath); meshHost = m; }
        else AssetDatabase.AddObjectToAsset(m, meshHost);
        meshCache[key] = m;
        return m;
    }

    static void AddFace(MB b, Vector3 o, Vector3 du, Vector3 dv, Vector3 n, float tile)
    {
        float lu = du.magnitude / tile, lv = dv.magnitude / tile;
        b.Quad(o, o + du, o + du + dv, o + dv, n, new Vector2(0, 0), new Vector2(lu, 0), new Vector2(lu, lv), new Vector2(0, lv));
    }

    static Mesh BoxMesh(float sx, float sy, float sz, float tile)
    {
        string key = "box_" + F(sx) + "_" + F(sy) + "_" + F(sz) + "_" + F(tile);
        Mesh c;
        if (meshCache.TryGetValue(key, out c)) return c;
        float hx = sx * 0.5f, hy = sy * 0.5f, hz = sz * 0.5f;
        var b = new MB();
        AddFace(b, new Vector3(hx, -hy, hz), new Vector3(0, 0, -sz), new Vector3(0, sy, 0), new Vector3(1, 0, 0), tile);
        AddFace(b, new Vector3(-hx, -hy, -hz), new Vector3(0, 0, sz), new Vector3(0, sy, 0), new Vector3(-1, 0, 0), tile);
        AddFace(b, new Vector3(-hx, -hy, hz), new Vector3(sx, 0, 0), new Vector3(0, sy, 0), new Vector3(0, 0, 1), tile);
        AddFace(b, new Vector3(hx, -hy, -hz), new Vector3(-sx, 0, 0), new Vector3(0, sy, 0), new Vector3(0, 0, -1), tile);
        AddFace(b, new Vector3(-hx, hy, -hz), new Vector3(0, 0, sz), new Vector3(sx, 0, 0), new Vector3(0, 1, 0), tile);
        AddFace(b, new Vector3(-hx, -hy, -hz), new Vector3(sx, 0, 0), new Vector3(0, 0, sz), new Vector3(0, -1, 0), tile);
        return Register(b.Build(key), key);
    }

    static Mesh CylMesh(float r, float h, int seg, float tile)
    {
        string key = "cyl_" + F(r) + "_" + F(h) + "_" + seg + "_" + F(tile);
        Mesh c;
        if (meshCache.TryGetValue(key, out c)) return c;
        var b = new MB();
        float circ = 2f * Mathf.PI * r;
        float hh = h * 0.5f;
        for (int i = 0; i < seg; i++)
        {
            float a0 = (float)i / seg * Mathf.PI * 2f, a1 = (float)(i + 1) / seg * Mathf.PI * 2f;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            Vector3 p0b = d0 * r + new Vector3(0, -hh, 0), p1b = d1 * r + new Vector3(0, -hh, 0);
            Vector3 p0t = d0 * r + new Vector3(0, hh, 0), p1t = d1 * r + new Vector3(0, hh, 0);
            float u0 = circ * i / seg / tile, u1 = circ * (i + 1) / seg / tile, vh = h / tile;
            b.QuadN(p0b, p1b, p1t, p0t, d0, d1, d1, d0, new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, vh), new Vector2(u0, vh));
            Vector3 ct = new Vector3(0, hh, 0), cb = new Vector3(0, -hh, 0);
            b.Tri(ct, p0t, p1t, Vector3.up, new Vector2(0.5f, 0.5f), new Vector2(p0t.x / tile + 0.5f, p0t.z / tile + 0.5f), new Vector2(p1t.x / tile + 0.5f, p1t.z / tile + 0.5f));
            b.Tri(cb, p0b, p1b, Vector3.down, new Vector2(0.5f, 0.5f), new Vector2(p0b.x / tile + 0.5f, p0b.z / tile + 0.5f), new Vector2(p1b.x / tile + 0.5f, p1b.z / tile + 0.5f));
        }
        return Register(b.Build(key), key);
    }

    // atap pelana: bubungan sepanjang X (panjang sx), lebar sz, tinggi h
    static Mesh PrismMesh(float sx, float sz, float h, float tile)
    {
        string key = "prism_" + F(sx) + "_" + F(sz) + "_" + F(h) + "_" + F(tile);
        Mesh c;
        if (meshCache.TryGetValue(key, out c)) return c;
        float hx = sx * 0.5f, hz = sz * 0.5f;
        Vector3 a0 = new Vector3(-hx, 0, -hz), a1 = new Vector3(hx, 0, -hz), r1 = new Vector3(hx, h, 0), r0 = new Vector3(-hx, h, 0);
        Vector3 b0 = new Vector3(-hx, 0, hz), b1 = new Vector3(hx, 0, hz);
        float slope = Mathf.Sqrt(hz * hz + h * h);
        Vector3 nL = new Vector3(0, hz, -h).normalized, nR = new Vector3(0, hz, h).normalized;
        var b = new MB();
        b.Quad(a0, a1, r1, r0, nL, new Vector2(0, 0), new Vector2(sx / tile, 0), new Vector2(sx / tile, slope / tile), new Vector2(0, slope / tile));
        b.Quad(b1, b0, r0, r1, nR, new Vector2(0, 0), new Vector2(sx / tile, 0), new Vector2(sx / tile, slope / tile), new Vector2(0, slope / tile));
        b.Tri(a0, b0, r0, Vector3.left, new Vector2(a0.z / tile, 0), new Vector2(b0.z / tile, 0), new Vector2(0, h / tile));
        b.Tri(a1, b1, r1, Vector3.right, new Vector2(a1.z / tile, 0), new Vector2(b1.z / tile, 0), new Vector2(0, h / tile));
        return Register(b.Build(key), key);
    }

    // ---------- placement ----------
    static Transform Grp(string name, Transform parent)
    {
        var g = new GameObject(name).transform;
        if (parent != null) g.SetParent(parent, false);
        return g;
    }

    static Transform Obj(Transform parent, string name, float x, float z, float yaw, float y = 0f)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = new Vector3(x, y, z);
        t.localRotation = Quaternion.Euler(0, yaw, 0);
        return t;
    }

    static GameObject MakeMesh(string name, Transform parent, Mesh mesh, M m, Vector3 lp, Quaternion lr, int layer, bool isStatic = true)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = lp;
        go.transform.localRotation = lr;
        go.layer = layer;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = m.mat;
        if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static GameObject BoxQ(Transform parent, string name, Vector3 lp, Vector3 size, M m, int layer, bool collide, Quaternion rot, bool isStatic = true)
    {
        var go = MakeMesh(name, parent, BoxMesh(size.x, size.y, size.z, m.tile), m, lp, rot, layer, isStatic);
        if (collide) { var bc = go.AddComponent<BoxCollider>(); bc.size = size; }
        return go;
    }

    static GameObject Box(Transform parent, string name, Vector3 lp, Vector3 size, M m, int layer = 0, bool collide = false, float yaw = 0f)
    { return BoxQ(parent, name, lp, size, m, layer, collide, Quaternion.Euler(0, yaw, 0)); }

    // balok berdiri di tanah: x,z pusat; baseY alas
    static GameObject BoxG(Transform parent, string name, float x, float z, float sx, float sy, float sz, float yaw, M m, int layer = 0, bool collide = true, float baseY = 0f)
    { return Box(parent, name, new Vector3(x, baseY + sy * 0.5f, z), new Vector3(sx, sy, sz), m, layer, collide, yaw); }

    static GameObject BoxPx(Transform parent, string name, float x0, float y0, float x1, float y1, float h, M m, int layer = 0, bool collide = true, float baseY = 0f, float yaw = 0f)
    {
        Vector2 c = V((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
        return BoxG(parent, name, c.x, c.y, Mathf.Abs(x1 - x0) * S, h, Mathf.Abs(y1 - y0) * S, yaw, m, layer, collide, baseY);
    }

    static GameObject CylQ(Transform parent, string name, Vector3 lp, float r, float h, M m, int layer, bool collide, Quaternion rot, int seg = 18, bool isStatic = true)
    {
        var go = MakeMesh(name, parent, CylMesh(r, h, seg, m.tile), m, lp, rot, layer, isStatic);
        if (collide)
        {
            var cc = go.AddComponent<CapsuleCollider>();
            cc.direction = 1; cc.radius = r; cc.height = Mathf.Max(h, r * 2f);
        }
        return go;
    }
    static GameObject Cyl(Transform parent, string name, Vector3 lp, float r, float h, M m, int layer = 0, bool collide = false, int seg = 18)
    { return CylQ(parent, name, lp, r, h, m, layer, collide, Quaternion.identity, seg); }

    static GameObject Roof(Transform parent, string name, Vector3 lp, float sx, float sz, float h, M m, float yaw)
    { return MakeMesh(name, parent, PrismMesh(sx, sz, h, m.tile), m, lp, Quaternion.Euler(0, yaw, 0), 0); }

    static EnvSlot Slot(GameObject go, EnvCategory cat, string variant, Vector3 size)
    {
        var s = go.AddComponent<EnvSlot>();
        s.category = cat; s.variant = variant; s.size = size;
        return s;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }

    // ---------- layer & tag ----------
    static void EnsureLayersAndTags()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return;
        var tm = new SerializedObject(assets[0]);
        var layers = tm.FindProperty("layers");
        string[] names = { "Player", "Enemy", "Obstacle", "Cover", "Generator", "Ground", "Marker", "Fence" };
        for (int i = 0; i < names.Length; i++)
        {
            var p = layers.GetArrayElementAtIndex(8 + i);
            if (string.IsNullOrEmpty(p.stringValue)) p.stringValue = names[i];
            else if (p.stringValue != names[i]) Log("PERINGATAN: layer " + (8 + i) + " sudah dipakai: " + p.stringValue + " (diharapkan " + names[i] + ")");
        }
        var tags = tm.FindProperty("tags");
        string[] want = { "Enemy", "Generator", "CoverPoint", "SpawnPoint" };
        foreach (var w in want)
        {
            bool found = false;
            for (int i = 0; i < tags.arraySize; i++) if (tags.GetArrayElementAtIndex(i).stringValue == w) { found = true; break; }
            if (!found)
            {
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = w;
            }
        }
        tm.ApplyModifiedProperties();
    }

    // ---------- log ----------
    static readonly System.Text.StringBuilder report = new System.Text.StringBuilder();
    static void Log(string s) { report.AppendLine(s); }
    static string StatusDir { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../BuilderStatus")); } }
}
#endif
