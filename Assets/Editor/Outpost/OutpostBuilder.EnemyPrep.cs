#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Persiapan aset musuh: konversi material (Standard/HDRP -> URP/Lit) dan lineup preview.
public static partial class OutpostBuilder
{
    const string ZombieRoot = "Assets/Zombie";
    const string SkinlessRoot = "Assets/Tensori/SkinlessZombie";
    const string MutantRoot = "Assets/Mutant1";

    static T LoadByName<T>(string folder, string namePart) where T : Object
    {
        foreach (var g in AssetDatabase.FindAssets(namePart, new[] { folder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == namePart) { var o = AssetDatabase.LoadAssetAtPath<T>(p); if (o != null) return o; }
        }
        return null;
    }

    static void SetTexImport(string path, bool normal, bool srgb)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        bool dirty = false;
        if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
        if (!normal && ti.sRGBTexture != srgb) { ti.sRGBTexture = srgb; dirty = true; }
        if (dirty) ti.SaveAndReimport();
    }

    // Skinless Zombie memakai shader HDRP -> bangun ulang sebagai URP/Lit dari tekstur BaseMap/MaskMap/Normal.
    static string FixSkinlessMaterials()
    {
        var sb = new StringBuilder();
        string tex = SkinlessRoot + "/Art/Textures";
        var map = new Dictionary<string, string> { { "Skin", "zombie low_zombie 2048x2048" }, { "Pants", "zombie pants low_zombie pants" }, { "Axe", "axe_fireaxe" } };
        var urp = Shader.Find("Universal Render Pipeline/Lit");
        foreach (var kv in map)
        {
            string mp = SkinlessRoot + "/Art/Materials/" + kv.Key + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { sb.AppendLine("material tidak ada: " + mp); continue; }
            string bp = tex + "/" + kv.Value + "_BaseMap.png", mk = tex + "/" + kv.Value + "_MaskMap.png", nm = tex + "/" + kv.Value + "_Normal.png";
            SetTexImport(nm, true, false); SetTexImport(mk, false, false);
            var b = AssetDatabase.LoadAssetAtPath<Texture2D>(bp); var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(mk); var n = AssetDatabase.LoadAssetAtPath<Texture2D>(nm);
            m.shader = urp;
            m.SetTexture("_BaseMap", b); m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", n); if (n != null) m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", mask); if (mask != null) m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_OcclusionMap", mask); if (mask != null) m.EnableKeyword("_OCCLUSIONMAP");
            m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 0.5f); m.SetFloat("_OcclusionStrength", 1f);
            EditorUtility.SetDirty(m);
            sb.AppendLine("material " + kv.Key + ": base=" + (b != null) + " mask=" + (mask != null) + " normal=" + (n != null));
        }
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    static string PrepareEnemyAssets()
    {
        var sb = new StringBuilder();
        sb.AppendLine("zombie: " + ConvertMaterialsIn(ZombieRoot));
        sb.AppendLine("mutant: " + ConvertMaterialsIn(MutantRoot));
        sb.Append(FixSkinlessMaterials());
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    static readonly string[] lineupPaths = {
        ZombieRoot + "/Prefabs/Zombie1.prefab", ZombieRoot + "/Prefabs/Zombie2.prefab", ZombieRoot + "/Prefabs/Zombie3.prefab",
        SkinlessRoot + "/Prefabs/skinless zombie.prefab",
        MutantRoot + "/Prefab/Mutated1.prefab", MutantRoot + "/Prefab/Mutated1_V2.prefab", MutantRoot + "/Prefab/Mutated1_V3.prefab" };

    static string Lineup()
    {
        var root = GameObject.Find("TMP_LINEUP"); if (root != null) Object.DestroyImmediate(root);
        root = new GameObject("TMP_LINEUP");
        for (int i = 0; i < lineupPaths.Length; i++)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(lineupPaths[i]);
            if (pf == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.transform);
            go.transform.position = new Vector3(i * 2.6f, 0f, -70f);
            go.transform.rotation = Quaternion.identity;
        }
        return "lineup: x 0.." + ((lineupPaths.Length - 1) * 2.6f) + " z -70";
    }

    static string Lineup2(string mode)
    {
        var root = GameObject.Find("TMP_LINEUP"); if (root != null) Object.DestroyImmediate(root);
        root = new GameObject("TMP_LINEUP");
        var info = new StringBuilder();
        string[] names = { "Zombie_Walker", "Zombie_Bloater", "Zombie_Runner", "Zombie_Skinless", "Boss_Mutant_V1", "Boss_Mutant_V2", "Boss_Mutant_V3" };
        string A = ZombieRoot + "/Animations/Zombie@";
        AnimationClip clip = null; float t = 0.3f;
        if (mode == "run") clip = FindClip(A + "Z_Run_InPlace.FBX", "Z_Run_InPlace");
        else if (mode == "attack") { clip = FindClip(A + "Z_Attack.FBX", "Z_Attack"); t = 0.5f; }
        else if (mode == "death") { clip = FindClip(A + "Z_FallingBack.FBX", "Z_FallingBack"); t = 1.3f; }
        else if (mode == "scream") { clip = FindClip(SkinlessRoot + "/Art/Animations/angry01.fbx", "angry"); t = 1.2f; }
        else clip = FindClip(A + "Z_Idle.FBX", "Z_Idle");
        for (int i = 0; i < names.Length; i++)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyDir + "/Prefabs/" + names[i] + ".prefab");
            if (pf == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.transform);
            go.transform.position = new Vector3(i * 2.8f, 0f, -70f);
            go.transform.rotation = Quaternion.identity;
            var an = go.GetComponentInChildren<Animator>();
            if (an != null && clip != null) { an.Rebind(); clip.SampleAnimation(an.gameObject, t); }
            Bounds b = new Bounds(go.transform.position, Vector3.zero); bool f = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (f) { b = r.bounds; f = false; } else b.Encapsulate(r.bounds); }
            float vMin = 1e9f, vMax = -1e9f;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var bm = new Mesh(); smr.BakeMesh(bm, true);
                var m4 = smr.transform.localToWorldMatrix;
                foreach (var v in bm.vertices) { float wy = m4.MultiplyPoint3x4(v).y; if (wy < vMin) vMin = wy; if (wy > vMax) vMax = wy; }
                Object.DestroyImmediate(bm);
            }
            info.Append("{baked y " + vMin.ToString("F2") + ".." + vMax.ToString("F2") + "} ");
            var model = go.transform.Find("Model");
            var hpc = go.GetComponent<Outpost.Enemy.EnemyHealth>(); var hb = hpc != null ? hpc.headBone : null;
            var hips = an != null ? an.GetBoneTransform(HumanBodyBones.Hips) : null;
            info.Append("[head y " + (hb != null ? hb.position.y.ToString("F2") : "-") + " hips " + (hips != null ? hips.position.ToString("F2") : "-") + " root " + go.transform.position.ToString("F1") + " boundsCenter " + b.center.ToString("F1") + " animator scale " + (an != null ? an.transform.lossyScale.ToString("F2") : "-") + "] ");
            info.AppendLine(names[i] + " modelScale " + (model != null ? model.localScale.ToString("F2") : "-") + " lossy " + (model != null ? model.lossyScale.ToString("F2") : "-") + " bounds size " + b.size.ToString("F2") + " minY " + b.min.y.ToString("F2") + " maxY " + b.max.y.ToString("F2"));
        }
        System.IO.File.WriteAllText(StatusDir + "/lineup_info.txt", info.ToString());
        return "lineup2 " + mode;
    }

    static string LineupClear()
    {
        var root = GameObject.Find("TMP_LINEUP"); if (root != null) Object.DestroyImmediate(root);
        return "lineup dibersihkan";
    }

    static string RunEnemyCmd(string a0)
    {
        switch (a0)
        {
            case "enemyprep": return PrepareEnemyAssets();
            case "lineup": return Lineup();
            case "lineupclear": return LineupClear();
            case "lineup_run": return Lineup2("run");
            case "lineup_idle": return Lineup2("idle");
            case "lineup_attack": return Lineup2("attack");
            case "lineup_death": return Lineup2("death");
            case "lineup_scream": return Lineup2("scream");
        }
        return RunEnemyCmd2(a0);
    }
}
#endif
