#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static partial class OutpostBuilder
{
    struct CamDef { public string name; public Vector3 pos; public Vector3 look; public float fov; public bool ortho; public float size; }

    static List<CamDef> PreviewDefs()
    {
        var l = new List<CamDef>();
        Func<float, float, float, Vector3> P = (px, py, h) => { Vector2 v = V(px, py); return new Vector3(v.x, h, v.y); };
        l.Add(new CamDef { name = "top", pos = new Vector3(0, 120, -1.2f), look = new Vector3(0, 0, -1.2f), ortho = true, size = 41f });
        l.Add(new CamDef { name = "top2", pos = new Vector3(0, 175, -4f), look = new Vector3(0, 0, -1.2f), fov = 38 });
        l.Add(new CamDef { name = "iso_sw", pos = P(40, 560, 70), look = P(500, 270, 0), fov = 55 });
        l.Add(new CamDef { name = "iso_se", pos = P(1010, 560, 75), look = P(560, 250, 0), fov = 55 });
        l.Add(new CamDef { name = "iso_ne", pos = P(1000, 20, 70), look = P(520, 300, 0), fov = 55 });
        l.Add(new CamDef { name = "spawn", pos = P(95, 285, 2.2f), look = P(330, 285, 1.2f), fov = 68 });
        l.Add(new CamDef { name = "main_lane", pos = P(320, 287, 2.0f), look = P(640, 290, 1.5f), fov = 68 });
        l.Add(new CamDef { name = "north_lane", pos = P(300, 132, 2.0f), look = P(600, 150, 1.5f), fov = 68 });
        l.Add(new CamDef { name = "south_lane", pos = P(300, 374, 2.0f), look = P(640, 376, 1.5f), fov = 68 });
        l.Add(new CamDef { name = "gen_a", pos = P(660, 236, 3.5f), look = P(740, 229, 1.2f), fov = 62 });
        l.Add(new CamDef { name = "gen_b", pos = P(670, 372, 3.5f), look = P(740, 376, 1.2f), fov = 62 });
        l.Add(new CamDef { name = "gen_c", pos = P(480, 380, 3.5f), look = P(545, 396, 1.2f), fov = 62 });
        l.Add(new CamDef { name = "defender", pos = P(860, 290, 1.8f), look = P(600, 290, 2.0f), fov = 75 });
        l.Add(new CamDef { name = "fuel", pos = P(300, 380, 5f), look = P(380, 460, 1.5f), fov = 62 });
        l.Add(new CamDef { name = "command", pos = P(600, 330, 4f), look = P(736, 300, 4f), fov = 62 });
        l.Add(new CamDef { name = "gate", pos = P(160, 285, 3f), look = P(285, 285, 2.5f), fov = 70 });
        return l;
    }

    // "cam <nama> px py h lpx lpy lh [fov]" -> kamera kustom
    static CamDef? ParseCam(string[] t)
    {
        if (t.Length < 8 || t[0] != "cam") return null;
        Func<string, float> f = x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture);
        Vector2 a = V(f(t[2]), f(t[3])), b = V(f(t[5]), f(t[6]));
        return new CamDef { name = t[1], pos = new Vector3(a.x, f(t[4]), a.y), look = new Vector3(b.x, f(t[7]), b.y), fov = t.Length > 8 ? f(t[8]) : 60f };
    }

    public static string RenderPreviews(string args)
    {
        var toks = (args ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var custom = ParseCam(toks);
        Directory.CreateDirectory(StatusDir);
        var want = new HashSet<string>((args ?? "").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries));
        bool all = want.Count == 0 || want.Contains("all");
        var sb = new System.Text.StringBuilder();
        int w = 1600, h = 900;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.antiAliasing = 4;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        var go = new GameObject("PreviewCam");
        var cam = go.AddComponent<Camera>();
        var cd = go.AddComponent<UniversalAdditionalCameraData>();
        cd.renderPostProcessing = true;
        cam.targetTexture = rt;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 500f;
        Physics.SyncTransforms();
        var defs = PreviewDefs();
        if (custom.HasValue) { defs.Clear(); defs.Add(custom.Value); all = true; }
        foreach (var d in defs)
        {
            if (!all && !want.Contains(d.name)) continue;
            cam.orthographic = d.ortho;
            if (d.ortho) cam.orthographicSize = d.size; else cam.fieldOfView = d.fov;
            go.transform.position = d.pos;
            go.transform.rotation = d.ortho ? Quaternion.Euler(90, 0, 0) : Quaternion.LookRotation((d.look - d.pos).normalized, Vector3.up);
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(StatusDir, "preview_" + d.name + ".png"), tex.EncodeToJPG(88).Length > 0 ? tex.EncodeToPNG() : null);
            sb.AppendLine("preview_" + d.name + ".png");
        }
        UnityEngine.Object.DestroyImmediate(go);
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(tex);
        return sb.ToString();
    }
}
#endif
