#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Outpost.Env;

public static partial class OutpostBuilder
{
    const string PackRoot = "Assets/RPG_FPS_game_assets_industrial";

    // ---------- konversi material pack (Standard -> URP/Lit) ----------
    static string ConvertPackMaterials()
    {
        var sb = new StringBuilder();
        int n = 0, skipped = 0;
        var urp = Shader.Find("Universal Render Pipeline/Lit");
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { PackRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == null) continue;
            string sn = m.shader.name;
            if (sn == "Universal Render Pipeline/Lit" || sn == "Universal Render Pipeline/Simple Lit") { skipped++; continue; }
            if (sn != "Standard" && sn != "Standard (Specular setup)" && sn != "Hidden/InternalErrorShader" && !sn.StartsWith("Legacy Shaders") && !sn.StartsWith("Mobile/")) { sb.AppendLine("lewati (shader " + sn + "): " + path); skipped++; continue; }

            Texture main = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            Vector2 sc = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 off = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;
            Color col = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            Texture bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
            float bumpScale = m.HasProperty("_BumpScale") ? m.GetFloat("_BumpScale") : 1f;
            Texture mg = m.HasProperty("_MetallicGlossMap") ? m.GetTexture("_MetallicGlossMap") : null;
            Texture occ = m.HasProperty("_OcclusionMap") ? m.GetTexture("_OcclusionMap") : null;
            Texture emT = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
            Color emC = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            float metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
            float gloss = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.4f;
            float mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : 0f;
            float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;

            m.shader = urp;
            m.SetTexture("_BaseMap", main);
            m.SetTextureScale("_BaseMap", sc);
            m.SetTextureOffset("_BaseMap", off);
            m.SetColor("_BaseColor", col);
            m.SetFloat("_Smoothness", gloss);
            m.SetFloat("_Metallic", metallic);
            m.SetTexture("_BumpMap", bump);
            m.SetFloat("_BumpScale", bumpScale);
            if (bump != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", mg);
            if (mg != null) m.EnableKeyword("_METALLICSPECGLOSSMAP"); else m.DisableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_OcclusionMap", occ);
            if (occ != null) m.EnableKeyword("_OCCLUSIONMAP"); else m.DisableKeyword("_OCCLUSIONMAP");
            if (emT != null || emC.maxColorComponent > 0.01f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emC);
                m.SetTexture("_EmissionMap", emT);
            }
            else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }

            if (mode == 1f)
            {
                m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", cutoff); m.EnableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = 2450;
            }
            else if (mode >= 2f)
            {
                m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
                m.SetFloat("_SrcBlend", 5); m.SetFloat("_DstBlend", 10); m.SetFloat("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
            }
            else
            {
                m.SetFloat("_Surface", 0); m.SetFloat("_AlphaClip", 0);
                m.SetFloat("_SrcBlend", 1); m.SetFloat("_DstBlend", 0); m.SetFloat("_ZWrite", 1);
                m.DisableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Opaque"); m.renderQueue = -1;
            }
            EditorUtility.SetDirty(m);
            n++;
        }
        AssetDatabase.SaveAssets();
        sb.AppendLine("material dikonversi ke URP/Lit: " + n + ", dilewati: " + skipped);
        return sb.ToString();
    }

    // ---------- dump ukuran prefab ----------
    static string DumpPrefabs()
    {
        var sb = new StringBuilder();
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PackRoot });
        Array.Sort(guids, (a, b) => string.Compare(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b), StringComparison.Ordinal));
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = null;
            try
            {
                go = PrefabUtility.LoadPrefabContents(path);
                var rs = go.GetComponentsInChildren<Renderer>(true);
                bool has = false; Bounds b = new Bounds();
                int tris = 0;
                foreach (var r in rs)
                {
                    if (r is ParticleSystemRenderer) continue;
                    if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                }
                int cols = go.GetComponentsInChildren<Collider>(true).Length;
                int ps = go.GetComponentsInChildren<ParticleSystem>(true).Length;
                string rel = path.Substring(PackRoot.Length + 1);
                if (has)
                    sb.AppendLine(rel + " | size " + F(b.size.x) + " x " + F(b.size.y) + " x " + F(b.size.z) + " | ctr " + F(b.center.x) + "," + F(b.center.y) + "," + F(b.center.z) + " | r" + rs.Length + " t" + tris + " c" + cols + (ps > 0 ? " ps" + ps : ""));
                else
                    sb.AppendLine(rel + " | (tanpa renderer) c" + cols + " ps" + ps);
            }
            catch (Exception e) { sb.AppendLine(path + " | ERROR " + e.Message); }
            finally { if (go != null) PrefabUtility.UnloadPrefabContents(go); }
        }
        File.WriteAllText(Path.Combine(StatusDir, "prefabs.txt"), sb.ToString());
        return "prefabs.txt: " + guids.Length + " prefab";
    }

    // ---------- perintah kustom ("run <nama>") ----------
    public static string RunCustom(string args)
    {
        string a0 = (args ?? "").Trim();
        if (a0.StartsWith("probe "))
        {
            var t = a0.Substring(6).Split(' ');
            float wx = float.Parse(t[0], System.Globalization.CultureInfo.InvariantCulture), wz = float.Parse(t[1], System.Globalization.CultureInfo.InvariantCulture), rr = float.Parse(t[2], System.Globalization.CultureInfo.InvariantCulture);
            var sb = new System.Text.StringBuilder();
            foreach (var c in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                var b = c.bounds;
                if (Mathf.Abs(b.center.x - wx) < rr + b.extents.x && Mathf.Abs(b.center.z - wz) < rr + b.extents.z && !(c.gameObject.layer == 13))
                {
                    string path = c.name; var tr = c.transform.parent; int n = 0;
                    while (tr != null && n++ < 4) { path = tr.name + "/" + path; tr = tr.parent; }
                    sb.AppendLine(path + " layer " + c.gameObject.layer + " center " + b.center.ToString("F1") + " size " + b.size.ToString("F1"));
                }
            }
            System.IO.File.WriteAllText(StatusDir + "/probe_objs.txt", sb.ToString());
            return "probe ok";
        }
        switch (a0)
        {
            case "convert": return ConvertPackMaterials();
            case "dump": return DumpPrefabs();
            default: return "perintah run tidak dikenal: " + args;
        }
    }
}
#endif
