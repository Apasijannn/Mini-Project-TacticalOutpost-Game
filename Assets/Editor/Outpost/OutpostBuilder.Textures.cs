#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class OutpostBuilder
{
    static bool forceTextures = false;
    delegate void PixelFn(float u, float v, out Color c, out float h);

    // ---------- noise periodik ----------
    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= (h >> 16);
            return (h & 0x7fffffff) / (float)0x7fffffff;
        }
    }

    static float VN(float u, float v, int px, int py, int seed)
    {
        float fx = u * px, fy = v * py;
        int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
        float tx = fx - ix, ty = fy - iy;
        tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
        int x0 = ((ix % px) + px) % px, x1 = (x0 + 1) % px;
        int y0 = ((iy % py) + py) % py, y1 = (y0 + 1) % py;
        float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    static float FBM(float u, float v, int period, int oct, int seed)
    {
        float sum = 0, amp = 0.5f, tot = 0; int p = period;
        for (int i = 0; i < oct; i++) { sum += amp * VN(u, v, p, p, seed + i * 17); tot += amp; amp *= 0.5f; p *= 2; }
        return sum / tot;
    }

    static float Sstep(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static Color Mix(Color a, Color b, float t) { return Color.Lerp(a, b, Mathf.Clamp01(t)); }

    // ---------- tulis tekstur ----------
    static void GenTex(string name, int size, PixelFn fn, float normalStrength, bool alpha)
    {
        EnsureFolder(TexDir);
        string path = TexDir + "/" + name + ".png";
        string npath = TexDir + "/" + name + "_n.png";
        bool needNormal = normalStrength > 0f;
        if (!forceTextures && File.Exists(path) && (!needNormal || File.Exists(npath))) return;

        var cols = new Color32[size * size];
        var hts = new float[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                Color c; float h;
                fn(u, v, out c, out h);
                cols[y * size + x] = c;
                hts[y * size + x] = h;
            }
        }
        WritePng(path, cols, size);
        SetImport(path, false, alpha);

        if (needNormal)
        {
            var nc = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float hl = hts[y * size + (x - 1 + size) % size], hr = hts[y * size + (x + 1) % size];
                    float hd = hts[((y - 1 + size) % size) * size + x], hu = hts[((y + 1) % size) * size + x];
                    Vector3 n = new Vector3((hl - hr) * normalStrength, (hd - hu) * normalStrength, 1f).normalized;
                    nc[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            WritePng(npath, nc, size);
            SetImport(npath, true, false);
        }
    }

    static void WritePng(string path, Color32[] cols, int size)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.SetPixels32(cols);
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    static void SetImport(string path, bool normal, bool alpha)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        imp.sRGBTexture = !normal;
        imp.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        imp.alphaIsTransparency = alpha;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.filterMode = FilterMode.Trilinear;
        imp.mipmapEnabled = true;
        imp.anisoLevel = 8;
        imp.maxTextureSize = 1024;
        imp.SaveAndReimport();
    }

    static Texture2D LoadTex(string name, bool normal = false)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (name == "pack:asphalt") return AssetDatabase.LoadAssetAtPath<Texture2D>(PackRoot + "/Textures/Asphalt/Seamless_asphalt_v1/Seamless_asphalt_v1.tga");
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/" + name + (normal ? "_n" : "") + ".png");
    }

    static void BuildTextures()
    {
        // ASPAL
        GenTex("asphalt", 512, (float u, float v, out Color c, out float h) =>
        {
            float n = FBM(u, v, 8, 5, 3);
            float g = Mathf.Lerp(0.34f, 0.56f, n) + (Hash((int)(u * 512), (int)(v * 512), 9) - 0.5f) * 0.1f;
            float r1 = Mathf.Abs(VN(u, v, 5, 5, 21) - 0.5f) * 2f;
            float r2 = Mathf.Abs(VN(u, v, 11, 11, 33) - 0.5f) * 2f;
            float crack = Mathf.Max(1f - Sstep(0f, 0.035f, r1), (1f - Sstep(0f, 0.02f, r2)) * 0.7f);
            g *= 1f - crack * 0.55f;
            c = new Color(g, g, g * 1.03f, 1f);
            h = n * 0.5f + (Hash((int)(u * 512), (int)(v * 512), 9)) * 0.25f - crack * 0.6f;
        }, 6f, false);

        // PASIR
        GenTex("sand", 512, (float u, float v, out Color c, out float h) =>
        {
            float n = FBM(u, v, 4, 5, 7);
            float rip = Mathf.Sin((u * 4f + n * 0.9f) * Mathf.PI * 2f * 3f) * 0.5f + 0.5f;
            Color a = new Color(0.62f, 0.55f, 0.44f), b = new Color(0.8f, 0.72f, 0.58f);
            c = Mix(a, b, n * 0.8f + rip * 0.25f);
            c *= 0.94f + Hash((int)(u * 512), (int)(v * 512), 4) * 0.1f;
            c.a = 1f;
            h = n * 0.6f + rip * 0.3f;
        }, 4f, false);

        // BETON
        GenTex("concrete", 512, (float u, float v, out Color c, out float h) =>
        {
            float n = FBM(u, v, 6, 5, 11);
            float g = Mathf.Lerp(0.52f, 0.78f, n);
            float stain = Sstep(0.55f, 0.85f, FBM(u, v, 3, 4, 91)) * 0.18f;
            g -= stain;
            float seamU = Mathf.Abs(Mathf.Repeat(u * 4f, 1f) - 0.5f), seamV = Mathf.Abs(Mathf.Repeat(v * 4f, 1f) - 0.5f);
            float seam = (seamU > 0.494f || seamV > 0.494f) ? 1f : 0f;
            g *= 1f - seam * 0.35f;
            g += (Hash((int)(u * 512), (int)(v * 512), 2) - 0.5f) * 0.05f;
            c = new Color(g, g * 0.99f, g * 0.96f, 1f);
            h = n * 0.5f - seam * 0.4f;
        }, 4f, false);

        // SENG BERGELOMBANG (+ karat)
        GenTex("corr", 512, (float u, float v, out Color c, out float h) =>
        {
            float rib = Mathf.Sin(u * 14f * Mathf.PI * 2f) * 0.5f + 0.5f;
            float shade = 0.74f + rib * 0.26f;
            float rustMask = Sstep(0.52f, 0.82f, FBM(u, v, 4, 5, 5));
            float low = 1f - Sstep(0f, 0.55f, v);
            rustMask = Mathf.Clamp01(rustMask * (0.35f + low * 0.9f));
            float streak = VN(u, v, 48, 3, 77);
            Color baseC = new Color(0.9f, 0.9f, 0.9f) * (0.9f + (streak - 0.5f) * 0.16f);
            Color rust = new Color(0.52f, 0.24f, 0.1f);
            c = Mix(baseC, rust, rustMask * 0.7f) * shade;
            c.a = 1f;
            float scratch = (Hash((int)(u * 256), (int)(v * 512), 8) > 0.996f) ? 0.2f : 0f;
            c += new Color(scratch, scratch, scratch, 0f);
            h = rib + (FBM(u, v, 16, 3, 2) - 0.5f) * 0.15f;
        }, 2.2f, false);

        // KARUNG PASIR
        GenTex("sandbag", 512, (float u, float v, out Color c, out float h) =>
        {
            const int rows = 6;
            float fy = v * rows; int row = Mathf.FloorToInt(fy); float ly = fy - row;
            int cols = 3; float fx = u * cols + ((row % 2) == 0 ? 0f : 0.5f);
            int col = Mathf.FloorToInt(fx); float lx = fx - col;
            float ex = Mathf.Abs(lx * 2f - 1f), ey = Mathf.Abs(ly * 2f - 1f);
            float bag = Mathf.Clamp01(1f - Mathf.Pow(ex, 3.2f)) * Mathf.Clamp01(1f - Mathf.Pow(ey, 2.6f));
            float vr = Hash(((col % cols) + cols) % cols, row, 77);
            float n = FBM(u, v, 12, 4, 31);
            Color baseC = Mix(new Color(0.66f, 0.58f, 0.42f), new Color(0.78f, 0.7f, 0.52f), vr * 0.7f + n * 0.5f);
            float seam = Mathf.Pow(1f - bag, 1.5f);
            c = Mix(baseC, new Color(0.22f, 0.18f, 0.12f), Mathf.Clamp01(seam * 0.85f));
            c *= 0.86f + bag * 0.22f;
            // anyaman halus
            float weave = (Mathf.Sin(u * 512f * 0.9f) * Mathf.Sin(v * 512f * 0.9f)) * 0.03f;
            c = new Color(c.r + weave, c.g + weave, c.b + weave, 1f);
            h = bag * 0.8f + n * 0.12f;
        }, 3.5f, false);

        // KAYU
        GenTex("wood", 256, (float u, float v, out Color c, out float h) =>
        {
            int planks = 6;
            float pv = Mathf.Repeat(v * planks, 1f);
            int pi = Mathf.FloorToInt(v * planks);
            float grain = VN(u + Hash(pi, 1, 3), v, 3, planks * 6, 55);
            float seam = (pv < 0.04f || pv > 0.96f) ? 1f : 0f;
            Color a = new Color(0.42f, 0.28f, 0.16f), b = new Color(0.72f, 0.54f, 0.34f);
            c = Mix(a, b, grain * 0.8f + Hash(pi, 7, 1) * 0.25f) * (1f - seam * 0.5f);
            c.a = 1f;
            h = grain * 0.4f - seam;
        }, 0f, false);

        // TANGKI
        GenTex("tank", 512, (float u, float v, out Color c, out float h) =>
        {
            float g = 0.88f;
            float seamV = Mathf.Abs(Mathf.Repeat(u * 8f, 1f) - 0.5f) > 0.488f ? 1f : 0f;
            float seamH = Mathf.Abs(Mathf.Repeat(v * 3f, 1f) - 0.5f) > 0.492f ? 1f : 0f;
            float seam = Mathf.Max(seamV, seamH);
            float rustMask = Sstep(0.5f, 0.85f, FBM(u, v, 5, 4, 41)) * (1f - Sstep(0f, 0.6f, v)) * 1.1f;
            float stain = VN(u, v, 30, 2, 66);
            c = Mix(new Color(g, g, g * 0.95f) * (0.92f + stain * 0.08f), new Color(0.5f, 0.28f, 0.15f), Mathf.Clamp01(rustMask) * 0.6f) * (1f - seam * 0.35f);
            c.a = 1f;
            h = -seam;
        }, 0f, false);

        // GARIS BAHAYA
        GenTex("hazard", 256, (float u, float v, out Color c, out float h) =>
        {
            float s = Mathf.Repeat((u + v) * 4f, 1f);
            Color y = new Color(0.95f, 0.74f, 0.05f), k = new Color(0.07f, 0.07f, 0.07f);
            c = (s < 0.5f) ? y : k;
            float wear = FBM(u, v, 8, 3, 29);
            c = Mix(c, new Color(0.35f, 0.3f, 0.25f), Sstep(0.62f, 0.85f, wear) * 0.5f);
            c.a = 1f;
            h = 0f;
        }, 0f, false);

        // PAGAR KAWAT (alpha)
        GenTex("fence", 256, (float u, float v, out Color c, out float h) =>
        {
            float a = (u + v) * 4f, b = (u - v) * 4f;
            float da = Mathf.Abs(a - Mathf.Round(a)), db = Mathf.Abs(b - Mathf.Round(b));
            float d = Mathf.Min(da, db);
            float wire = d < 0.045f ? 1f : 0f;
            c = new Color(0.62f, 0.64f, 0.67f, wire);
            h = 0f;
        }, 0f, true);

        // NODA / BERCAK
        GenTex("stain", 256, (float u, float v, out Color c, out float h) =>
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float n = FBM(u, v, 4, 4, 17);
            float a = Mathf.Clamp01((1f - r) * 1.6f) * Sstep(0.3f, 0.7f, n + (1f - r) * 0.35f);
            c = new Color(0.04f, 0.035f, 0.03f, a * 0.8f);
            h = 0f;
        }, 0f, true);

        // TAMBAL PASIR
        GenTex("patch", 256, (float u, float v, out Color c, out float h) =>
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float n = FBM(u, v, 5, 5, 23);
            float a = Sstep(0.35f, 0.62f, n + (1f - r) * 0.55f - 0.25f) * Mathf.Clamp01((1f - r) * 2.2f);
            Color s = Mix(new Color(0.78f, 0.64f, 0.43f), new Color(0.9f, 0.78f, 0.57f), n);
            c = new Color(s.r, s.g, s.b, a * 0.95f);
            h = 0f;
        }, 0f, true);

        // PARTIKEL
        GenTex("soft", 128, (float u, float v, out Color c, out float h) =>
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2f);
            c = new Color(1f, 1f, 1f, a);
            h = 0f;
        }, 0f, true);

        GenTex("smoke", 128, (float u, float v, out Color c, out float h) =>
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float n = FBM(u, v, 4, 4, 51);
            float a = Mathf.Clamp01(1f - r) * Mathf.Lerp(0.5f, 1f, n);
            a = a * a * 1.3f;
            c = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            h = 0f;
        }, 0f, true);
    }

    // ---------- material ----------
    // mode: 0 = opaque, 1 = alpha clip (dua sisi), 2 = transparent
    static M MakeMat(string key, Color col, string tex, string nrm, float tile, float smooth, float metal, Color? emis = null, int mode = 0)
    {
        EnsureFolder(MatDir);
        string path = MatDir + "/M_" + key + ".mat";
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, path); }
        else mat.shader = sh;

        mat.SetColor("_BaseColor", col);
        mat.SetFloat("_Smoothness", smooth);
        mat.SetFloat("_Metallic", metal);
        mat.SetTexture("_BaseMap", LoadTex(tex));
        Texture2D n = string.IsNullOrEmpty(nrm) ? null : LoadTex(nrm, true);
        mat.SetTexture("_BumpMap", n);
        if (n != null) { mat.EnableKeyword("_NORMALMAP"); mat.SetFloat("_BumpScale", 1f); } else mat.DisableKeyword("_NORMALMAP");

        if (emis.HasValue) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", emis.Value); mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
        else { mat.SetColor("_EmissionColor", Color.black); mat.DisableKeyword("_EMISSION"); }

        // reset
        mat.SetFloat("_Surface", 0); mat.SetFloat("_AlphaClip", 0); mat.SetFloat("_Cull", 2);
        mat.DisableKeyword("_ALPHATEST_ON"); mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetFloat("_SrcBlend", 1); mat.SetFloat("_DstBlend", 0); mat.SetFloat("_ZWrite", 1);
        mat.SetOverrideTag("RenderType", "Opaque");
        mat.SetShaderPassEnabled("ShadowCaster", true);
        mat.renderQueue = -1;

        if (mode == 1)
        {
            mat.SetFloat("_AlphaClip", 1); mat.SetFloat("_Cutoff", 0.5f); mat.SetFloat("_Cull", 0);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = 2450;
            mat.doubleSidedGI = true;
        }
        else if (mode == 2)
        {
            mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0);
            mat.SetFloat("_SrcBlend", 5); mat.SetFloat("_DstBlend", 10);
            mat.SetFloat("_SrcBlendAlpha", 1); mat.SetFloat("_DstBlendAlpha", 10);
            mat.SetFloat("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetShaderPassEnabled("ShadowCaster", false);
            mat.renderQueue = 3000;
        }
        EditorUtility.SetDirty(mat);
        var m = new M { mat = mat, tile = tile, key = key };
        mats[key] = m;
        return m;
    }

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    static void BuildMaterials()
    {
        mats.Clear();
        MakeMat("sand", C(0.78f, 0.82f, 0.9f), "sand", "sand", 8f, 0.04f, 0f);
        MakeMat("concrete", C(0.82f, 0.8f, 0.76f), "concrete", "concrete", 5f, 0.08f, 0f);
        MakeMat("asphalt", C(0.98f, 0.98f, 1.0f), "pack:asphalt", null, 6f, 0.12f, 0f);
        MakeMat("road", C(0.72f, 0.72f, 0.74f), "pack:asphalt", null, 6f, 0.14f, 0f);
        MakeMat("pad", C(0.68f, 0.68f, 0.66f), "concrete", "concrete", 4f, 0.1f, 0f);
        MakeMat("bconcrete", C(0.74f, 0.71f, 0.66f), "concrete", "concrete", 3f, 0.08f, 0f);
        MakeMat("rubble", C(0.56f, 0.53f, 0.49f), "concrete", "concrete", 1.5f, 0.05f, 0f);

        MakeMat("wallgray", C(0.78f, 0.8f, 0.8f), "corr", "corr", 2.8f, 0.3f, 0.15f);
        MakeMat("wallcream", C(0.88f, 0.82f, 0.64f), "corr", "corr", 2.8f, 0.3f, 0.1f);
        MakeMat("wallgreen", C(0.42f, 0.52f, 0.4f), "corr", "corr", 2.8f, 0.3f, 0.1f);
        MakeMat("roof", C(0.5f, 0.53f, 0.56f), "corr", "corr", 2.8f, 0.35f, 0.4f);
        MakeMat("roofred", C(0.58f, 0.27f, 0.21f), "corr", "corr", 2.8f, 0.3f, 0.3f);

        MakeMat("cRed", C(0.66f, 0.18f, 0.12f), "corr", "corr", 2.5f, 0.35f, 0.25f);
        MakeMat("cBlue", C(0.13f, 0.28f, 0.55f), "corr", "corr", 2.5f, 0.35f, 0.25f);
        MakeMat("cGreen", C(0.2f, 0.45f, 0.24f), "corr", "corr", 2.5f, 0.35f, 0.25f);
        MakeMat("cOrange", C(0.86f, 0.44f, 0.1f), "corr", "corr", 2.5f, 0.35f, 0.25f);
        MakeMat("cGray", C(0.52f, 0.54f, 0.57f), "corr", "corr", 2.5f, 0.35f, 0.25f);
        MakeMat("cYellow", C(0.82f, 0.66f, 0.14f), "corr", "corr", 2.5f, 0.35f, 0.25f);
        MakeMat("cWhite", C(0.85f, 0.85f, 0.82f), "corr", "corr", 2.5f, 0.35f, 0.25f);

        MakeMat("sandbag", C(0.8f, 0.7f, 0.52f), "sandbag", "sandbag", 1.6f, 0.02f, 0f);
        MakeMat("wood", C(0.75f, 0.6f, 0.42f), "wood", null, 1.5f, 0.1f, 0f);
        MakeMat("pipe", C(0.5f, 0.34f, 0.24f), null, null, 1f, 0.4f, 0.6f);
        MakeMat("steel", C(0.6f, 0.62f, 0.65f), null, null, 1f, 0.45f, 0.8f);
        MakeMat("steelDark", C(0.18f, 0.19f, 0.21f), null, null, 1f, 0.4f, 0.7f);
        MakeMat("tank", C(0.92f, 0.92f, 0.88f), "tank", null, 3f, 0.35f, 0.3f);
        MakeMat("olive", C(0.36f, 0.42f, 0.27f), null, null, 1f, 0.25f, 0.1f);
        MakeMat("tan", C(0.62f, 0.52f, 0.36f), null, null, 1f, 0.2f, 0.05f);
        MakeMat("canvas", C(0.5f, 0.48f, 0.36f), null, null, 1f, 0.05f, 0f);
        MakeMat("burnt", C(0.1f, 0.085f, 0.07f), null, null, 1f, 0.1f, 0.3f);
        MakeMat("rust", C(0.45f, 0.24f, 0.14f), null, null, 1f, 0.2f, 0.4f);
        MakeMat("tire", C(0.04f, 0.04f, 0.045f), null, null, 1f, 0.15f, 0f);
        MakeMat("glass", C(0.05f, 0.09f, 0.12f), null, null, 1f, 0.95f, 0.1f);
        MakeMat("hazard", C(1f, 1f, 1f), "hazard", null, 1.2f, 0.25f, 0f);
        MakeMat("paintWhite", C(0.92f, 0.92f, 0.9f), null, null, 1f, 0.2f, 0f);
        MakeMat("paintYellow", C(0.95f, 0.75f, 0.1f), null, null, 1f, 0.2f, 0f);
        MakeMat("paintOrange", C(0.95f, 0.45f, 0.08f), null, null, 1f, 0.2f, 0f);
        MakeMat("paintBlue", C(0.2f, 0.5f, 0.95f), null, null, 1f, 0.2f, 0f);
        MakeMat("ammo", C(0.26f, 0.32f, 0.2f), null, null, 1f, 0.2f, 0.1f);

        MakeMat("barrelR", C(0.6f, 0.1f, 0.07f), null, null, 1f, 0.4f, 0.5f);
        MakeMat("barrelB", C(0.1f, 0.22f, 0.55f), null, null, 1f, 0.4f, 0.5f);
        MakeMat("barrelG", C(0.15f, 0.4f, 0.2f), null, null, 1f, 0.4f, 0.5f);
        MakeMat("barrelY", C(0.8f, 0.65f, 0.1f), null, null, 1f, 0.4f, 0.5f);
        MakeMat("barrelGray", C(0.4f, 0.42f, 0.44f), null, null, 1f, 0.35f, 0.6f);

        MakeMat("winEmis", C(0.08f, 0.06f, 0.04f), null, null, 1f, 0.5f, 0f, new Color(1f, 0.68f, 0.28f) * 2.2f);
        MakeMat("lampEmis", C(0.9f, 0.9f, 0.8f), null, null, 1f, 0.5f, 0f, new Color(1f, 0.85f, 0.55f) * 3.5f);
        MakeMat("redEmis", C(0.4f, 0.02f, 0.02f), null, null, 1f, 0.5f, 0f, new Color(1f, 0.06f, 0.03f) * 4f);
        MakeMat("greenEmis", C(0.02f, 0.2f, 0.05f), null, null, 1f, 0.5f, 0f, new Color(0.1f, 1f, 0.25f) * 2.5f);
        MakeMat("ringEmis", C(0.3f, 0.15f, 0.02f), null, null, 1f, 0.5f, 0f, new Color(1f, 0.55f, 0.1f) * 2.2f);
        MakeMat("zoneA", C(0.3f, 0.15f, 0.02f), null, null, 1f, 0.5f, 0f, new Color(1f, 0.5f, 0.05f) * 1.6f);
        MakeMat("zoneB", C(0.02f, 0.1f, 0.3f), null, null, 1f, 0.5f, 0f, new Color(0.15f, 0.5f, 1f) * 1.8f);

        MakeMat("fence", C(0.72f, 0.74f, 0.77f), "fence", null, 0.8f, 0.6f, 0.8f, null, 1);
        MakeMat("stain", C(1f, 1f, 1f), "stain", null, 1f, 0f, 0f, null, 1);
        MakeMat("patch", C(1f, 1f, 1f), "patch", null, 1f, 0f, 0f, null, 1);
        AssetDatabase.SaveAssets();
    }
}
#endif
