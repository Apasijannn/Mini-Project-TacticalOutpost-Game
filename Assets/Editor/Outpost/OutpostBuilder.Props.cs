#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Outpost.Env;

public static partial class OutpostBuilder
{
    // ======================= lampu & partikel =======================
    static Light AddLight(Transform parent, string name, Vector3 lp, LightType type, Color col, float intensity, float range, float spot = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lp;
        var l = go.AddComponent<Light>();
        l.type = type; l.color = col; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        if (type == LightType.Spot) { l.spotAngle = spot; l.innerSpotAngle = spot * 0.6f; }
        return l;
    }

    static readonly Dictionary<string, Material> particleMats = new Dictionary<string, Material>();
    static Material ParticleMat(string tex)
    {
        Material m;
        if (particleMats.TryGetValue(tex, out m) && m != null) return m;
        EnsureFolder(MatDir);
        string path = MatDir + "/MP_" + tex + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); } else m.shader = sh;
        m.SetTexture("_BaseMap", LoadTex(tex));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
        m.SetFloat("_SrcBlend", 5); m.SetFloat("_DstBlend", 10); m.SetFloat("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        particleMats[tex] = m;
        return m;
    }

    static ParticleSystem AddParticles(Transform parent, string name, Vector3 lp, string tex, Color c0, Color c1, float rate,
        float life0, float life1, float speed0, float speed1, float size0, float size1, float angle, float radius, float gravity, float grow)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lp;
        go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life0, life1);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed0, speed1);
        main.startSize = new ParticleSystem.MinMaxCurve(size0, size1);
        main.startColor = c0;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = angle; sh.radius = radius;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new GradientColorKey[] { new GradientColorKey(c0, 0f), new GradientColorKey(c1, 1f) },
                  new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(c0.a, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, grow)));
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ParticleMat(tex);
        r.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    static void Fire(Transform parent, Vector3 lp)
    {
        AddParticles(parent, "Flame", lp, "soft", new Color(1f, 0.55f, 0.12f, 0.9f), new Color(1f, 0.2f, 0.02f, 0f), 28f, 0.45f, 0.8f, 0.8f, 1.6f, 0.35f, 0.6f, 6f, 0.18f, -0.15f, 0.2f);
        AddParticles(parent, "Smoke", lp + new Vector3(0, 0.3f, 0), "smoke", new Color(0.12f, 0.11f, 0.1f, 0.5f), new Color(0.2f, 0.19f, 0.18f, 0f), 7f, 2.2f, 3.4f, 0.7f, 1.2f, 0.7f, 1.2f, 10f, 0.15f, -0.02f, 3.2f);
        var l = AddLight(parent, "FireLight", lp + new Vector3(0, 0.6f, 0), LightType.Point, new Color(1f, 0.52f, 0.18f), 2.2f, 7f);
        var f = l.gameObject.AddComponent<EnvFlicker>(); f.target = l; f.baseIntensity = 2.2f; f.amount = 0.45f;
    }

    static void Smoke(Transform parent, Vector3 lp, float scale)
    {
        AddParticles(parent, "SmokeColumn", lp, "smoke", new Color(0.1f, 0.1f, 0.1f, 0.55f), new Color(0.25f, 0.24f, 0.23f, 0f), 6f, 4f, 6f, 1f, 1.6f, 1.2f * scale, 2f * scale, 8f, 0.4f, -0.02f, 3.5f);
    }

    // ======================= kendaraan =======================
    static void Wheels(Transform t, M tire, float r, float w, float[] xs, float zOff)
    {
        foreach (float x in xs)
            foreach (float sz in new float[] { -1f, 1f })
                CylQ(t, "Wheel", new Vector3(x, r, sz * zOff), r, w, tire, 0, false, Quaternion.Euler(90, 0, 0), 14);
    }

    static GameObject Jeep(Transform parent, string name, float x, float z, float yaw, bool burnt = false, bool pickup = false)
    {
        var root = Obj(parent, name, x, z, yaw).gameObject;
        root.layer = LCover;
        var bc = root.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.9f, 0); bc.size = new Vector3(4.0f, 1.7f, 1.9f);
        Slot(root, EnvCategory.Vehicle, burnt ? "JeepWreck" : (pickup ? "Pickup" : "Jeep"), bc.size);
        M body = Mat(burnt ? "burnt" : "olive"), tire = Mat("tire"), glass = Mat(burnt ? "burnt" : "glass");
        Transform t = root.transform;
        Box(t, "Chassis", new Vector3(0, 0.62f, 0), new Vector3(3.9f, 0.45f, 1.8f), body);
        Box(t, "Hood", new Vector3(1.2f, 0.98f, 0), new Vector3(1.4f, 0.32f, 1.7f), body);
        if (!pickup)
        {
            Box(t, "Cabin", new Vector3(-0.35f, 1.35f, 0), new Vector3(1.7f, 0.8f, 1.7f), body);
            Box(t, "Glass", new Vector3(0.52f, 1.4f, 0), new Vector3(0.08f, 0.5f, 1.5f), glass);
        }
        else
        {
            Box(t, "Cab", new Vector3(0.2f, 1.3f, 0), new Vector3(1.3f, 0.75f, 1.7f), body);
            Box(t, "Glass", new Vector3(0.86f, 1.35f, 0), new Vector3(0.08f, 0.45f, 1.5f), glass);
            Box(t, "Bed", new Vector3(-1.2f, 1.0f, 0), new Vector3(1.6f, 0.4f, 1.8f), body);
        }
        Wheels(t, tire, 0.4f, 0.3f, new float[] { 1.2f, -1.2f }, 0.95f);
        if (!burnt)
        {
            Box(t, "Headlight", new Vector3(1.9f, 0.9f, 0.6f), new Vector3(0.08f, 0.2f, 0.25f), Mat("lampEmis"));
            Box(t, "Headlight", new Vector3(1.9f, 0.9f, -0.6f), new Vector3(0.08f, 0.2f, 0.25f), Mat("lampEmis"));
        }
        else Smoke(t, new Vector3(0, 1.2f, 0), 0.8f);
        return root;
    }

    static GameObject Truck(Transform parent, string name, float x, float z, float yaw, bool burnt = false)
    {
        var root = Obj(parent, name, x, z, yaw).gameObject;
        root.layer = LCover;
        var bc = root.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 1.5f, 0); bc.size = new Vector3(7.2f, 2.8f, 2.5f);
        Slot(root, EnvCategory.Vehicle, burnt ? "TruckWreck" : "Truck", bc.size);
        M body = Mat(burnt ? "burnt" : "olive"), tire = Mat("tire"), glass = Mat(burnt ? "burnt" : "glass"), cv = Mat(burnt ? "burnt" : "canvas");
        Transform t = root.transform;
        Box(t, "Chassis", new Vector3(0, 0.8f, 0), new Vector3(7f, 0.4f, 2.2f), Mat("steelDark"));
        Box(t, "Cab", new Vector3(2.5f, 1.9f, 0), new Vector3(2.0f, 1.9f, 2.4f), body);
        Box(t, "Windshield", new Vector3(3.52f, 2.3f, 0), new Vector3(0.08f, 0.8f, 2.0f), glass);
        Box(t, "BedFloor", new Vector3(-1.0f, 1.1f, 0), new Vector3(4.6f, 0.2f, 2.4f), Mat("wood"));
        Box(t, "Canvas", new Vector3(-1.0f, 2.2f, 0), new Vector3(4.4f, 1.9f, 2.3f), cv);
        Wheels(t, tire, 0.55f, 0.36f, new float[] { 2.6f, -0.3f, -2.4f }, 1.1f);
        if (!burnt)
        {
            Box(t, "Headlight", new Vector3(3.52f, 1.2f, 0.8f), new Vector3(0.08f, 0.25f, 0.3f), Mat("lampEmis"));
            Box(t, "Headlight", new Vector3(3.52f, 1.2f, -0.8f), new Vector3(0.08f, 0.25f, 0.3f), Mat("lampEmis"));
        }
        else Smoke(t, new Vector3(-1f, 2.2f, 0), 1.0f);
        return root;
    }

    static GameObject Apc(Transform parent, string name, float x, float z, float yaw, bool burnt)
    {
        var root = Obj(parent, name, x, z, yaw).gameObject;
        root.layer = LCover;
        var bc = root.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 1.4f, 0); bc.size = new Vector3(6.4f, 2.8f, 2.9f);
        Slot(root, EnvCategory.Vehicle, burnt ? "ApcWreck" : "Apc", bc.size);
        M body = Mat(burnt ? "burnt" : "olive"), tire = Mat("tire"), dark = Mat(burnt ? "burnt" : "steelDark");
        Transform t = root.transform;
        Box(t, "Hull", new Vector3(0, 1.15f, 0), new Vector3(6.2f, 1.5f, 2.8f), body);
        Box(t, "Upper", new Vector3(-0.5f, 2.2f, 0), new Vector3(3.2f, 0.8f, 2.4f), body);
        Box(t, "Front", new Vector3(2.8f, 1.1f, 0), new Vector3(0.6f, 1.2f, 2.6f), body, 0, false, 0f);
        CylQ(t, "Turret", new Vector3(0.9f, 2.75f, 0), 0.85f, 0.45f, body, 0, false, Quaternion.identity, 16);
        CylQ(t, "Gun", new Vector3(2.0f, 2.8f, 0), 0.09f, 2.4f, dark, 0, false, Quaternion.Euler(0, 0, 90), 10);
        Wheels(t, tire, 0.6f, 0.45f, new float[] { 2.2f, 0.75f, -0.75f, -2.2f }, 1.3f);
        if (burnt) { Smoke(t, new Vector3(-0.5f, 2.6f, 0), 1.4f); }
        return root;
    }

    // ======================= props =======================
    static GameObject Barrel(Transform parent, float x, float z, string matKey, bool tipped = false)
    {
        var root = Obj(parent, "Barrel", x, z, Rf(0, 360)).gameObject;
        root.layer = LCover;
        Slot(root, EnvCategory.Barrel, matKey, new Vector3(0.6f, 0.9f, 0.6f));
        M m = Mat(matKey);
        if (!tipped)
        {
            var cc = root.AddComponent<CapsuleCollider>(); cc.center = new Vector3(0, 0.45f, 0); cc.radius = 0.3f; cc.height = 0.9f;
            Cyl(root.transform, "Body", new Vector3(0, 0.45f, 0), 0.3f, 0.9f, m, 0, false, 14);
            Cyl(root.transform, "Rim", new Vector3(0, 0.3f, 0), 0.315f, 0.05f, Mat("steelDark"), 0, false, 14);
            Cyl(root.transform, "Rim", new Vector3(0, 0.62f, 0), 0.315f, 0.05f, Mat("steelDark"), 0, false, 14);
        }
        else
        {
            var bc = root.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.3f, 0); bc.size = new Vector3(0.9f, 0.6f, 0.6f);
            CylQ(root.transform, "Body", new Vector3(0, 0.3f, 0), 0.3f, 0.9f, m, 0, false, Quaternion.Euler(0, 0, 90), 14);
        }
        return root;
    }

    static void BarrelCluster(Transform parent, float x, float z, int n, string[] keys)
    {
        for (int i = 0; i < n; i++)
            Barrel(parent, x + Rf(-0.9f, 0.9f), z + Rf(-0.9f, 0.9f), keys[rng.Next(keys.Length)], i == n - 1 && n > 2);
    }

    static void BurningBarrel(Transform parent, float x, float z)
    {
        var b = Barrel(parent, x, z, "barrelGray");
        Fire(b.transform, new Vector3(0, 0.95f, 0));
    }

    static GameObject Crate(Transform parent, float x, float z, float size, float yaw, string matKey = "wood", float baseY = 0f)
    {
        var go = BoxG(parent, "Crate", x, z, size, size, size, yaw, Mat(matKey), LCover, true, baseY);
        Slot(go, EnvCategory.Crate, matKey, new Vector3(size, size, size));
        Box(go.transform, "StrapA", new Vector3(0, 0, 0), new Vector3(size + 0.02f, size * 0.12f, size + 0.02f), Mat("steelDark"));
        return go;
    }

    static GameObject AmmoCrate(Transform parent, float x, float z, float yaw)
    {
        var go = BoxG(parent, "AmmoCrate", x, z, 1.2f, 0.6f, 0.7f, yaw, Mat("ammo"), LCover, true);
        Slot(go, EnvCategory.Crate, "Ammo", new Vector3(1.2f, 0.6f, 0.7f));
        Box(go.transform, "MarkA", new Vector3(0, 0.305f, 0), new Vector3(0.5f, 0.01f, 0.12f), Mat("paintWhite"));
        Box(go.transform, "MarkB", new Vector3(0, 0.306f, 0), new Vector3(0.12f, 0.01f, 0.5f), Mat("paintWhite"));
        return go;
    }

    static GameObject Hedgehog(Transform parent, float x, float z, float yaw)
    {
        var root = Obj(parent, "Hedgehog", x, z, yaw).gameObject;
        root.layer = LCover;
        var bc = root.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.6f, 0); bc.size = new Vector3(1.1f, 1.2f, 1.1f);
        Slot(root, EnvCategory.Barrier, "Hedgehog", bc.size);
        M st = Mat("steelDark");
        BoxQ(root.transform, "BarA", new Vector3(0, 0.62f, 0), new Vector3(1.7f, 0.1f, 0.1f), st, 0, false, Quaternion.Euler(0, 0, 45));
        BoxQ(root.transform, "BarB", new Vector3(0, 0.62f, 0), new Vector3(0.1f, 0.1f, 1.7f), st, 0, false, Quaternion.Euler(45, 0, 0));
        BoxQ(root.transform, "BarC", new Vector3(0, 0.62f, 0), new Vector3(1.7f, 0.1f, 0.1f), st, 0, false, Quaternion.Euler(0, 90, -45));
        return root;
    }

    static GameObject JerseyBarrier(Transform parent, float x, float z, float yaw, string matKey = "bconcrete")
    {
        var root = Obj(parent, "JerseyBarrier", x, z, yaw).gameObject;
        root.layer = LCover;
        var bc = root.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.45f, 0); bc.size = new Vector3(3.0f, 0.9f, 0.7f);
        Slot(root, EnvCategory.Barrier, "Jersey", bc.size);
        M m = Mat(matKey);
        Box(root.transform, "Base", new Vector3(0, 0.17f, 0), new Vector3(3.0f, 0.34f, 0.7f), m);
        Box(root.transform, "Mid", new Vector3(0, 0.55f, 0), new Vector3(3.0f, 0.42f, 0.42f), m);
        Box(root.transform, "Top", new Vector3(0, 0.82f, 0), new Vector3(3.0f, 0.16f, 0.26f), m);
        Box(root.transform, "Stripe", new Vector3(0, 0.55f, 0.215f), new Vector3(3.0f, 0.14f, 0.01f), Mat("paintYellow"));
        return root;
    }

    static GameObject SandbagWall(Transform parent, Vector2 a, Vector2 b, float h = 1.1f, float thick = 0.75f)
    {
        Vector2 d = b - a; float L = d.magnitude; Vector2 mid = (a + b) * 0.5f;
        var go = BoxG(parent, "SandbagWall", mid.x, mid.y, L, h, thick, YawAlongX(d), Mat("sandbag"), LCover, true);
        Slot(go, EnvCategory.Sandbag, "Wall", new Vector3(L, h, thick));
        return go;
    }

    static GameObject HazardBarrier(Transform parent, float x, float z, float yaw)
    {
        var go = BoxG(parent, "HazardBarrier", x, z, 2.4f, 1.0f, 0.35f, yaw, Mat("hazard"), LCover, true);
        Slot(go, EnvCategory.Barrier, "Hazard", new Vector3(2.4f, 1.0f, 0.35f));
        return go;
    }

    static void Rubble(Transform parent, float x, float z, float radius, int count)
    {
        var g = Obj(parent, "Rubble", x, z, 0).gameObject;
        Slot(g, EnvCategory.Rubble, "Pile", new Vector3(radius * 2, 0.8f, radius * 2));
        for (int i = 0; i < count; i++)
        {
            float a = Rf(0, 6.28f), r = Rf(0, radius);
            float sx = Rf(0.3f, 1.0f), sy = Rf(0.25f, 0.8f), sz = Rf(0.3f, 0.9f);
            var p = BoxQ(g.transform, "Chunk", new Vector3(Mathf.Cos(a) * r, sy * 0.35f, Mathf.Sin(a) * r), new Vector3(sx, sy, sz), Mat("rubble"), LCover, sy > 0.4f,
                Quaternion.Euler(Rf(-12, 12), Rf(0, 360), Rf(-12, 12)));
        }
    }

    static void Pallets(Transform parent, float x0, float z0, float x1, float z1, int maxStack)
    {
        // isi persegi (meter, world) dengan tumpukan palet + kargo
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f, w = Mathf.Abs(x1 - x0), d = Mathf.Abs(z1 - z0);
        var root = Obj(parent, "PalletStack", cx, cz, 0).gameObject;
        root.layer = LCover;
        float hmax = 0.15f + maxStack * 1.0f;
        var bc = root.AddComponent<BoxCollider>(); bc.center = new Vector3(0, hmax * 0.5f, 0); bc.size = new Vector3(w, hmax, d);
        Slot(root, EnvCategory.Pallet, "Stack", bc.size);
        int nx = Mathf.Max(1, Mathf.RoundToInt(w / 1.25f)), nz = Mathf.Max(1, Mathf.RoundToInt(d / 1.05f));
        string[] cargo = { "wood", "paintWhite", "cGray", "sandbag", "tan" };
        for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                float px = -w * 0.5f + (ix + 0.5f) * (w / nx), pz = -d * 0.5f + (iz + 0.5f) * (d / nz);
                int levels = rng.Next(1, maxStack + 1);
                Box(root.transform, "Pallet", new Vector3(px, 0.075f, pz), new Vector3(w / nx * 0.96f, 0.15f, d / nz * 0.96f), Mat("wood"));
                for (int l = 0; l < levels; l++)
                    Box(root.transform, "Cargo", new Vector3(px, 0.15f + 0.45f + l * 0.9f, pz), new Vector3(w / nx * 0.88f, 0.9f, d / nz * 0.88f), Mat(cargo[rng.Next(cargo.Length)]), 0, false, Rf(-3, 3));
            }
    }

    static void PipeStack(Transform parent, float cx, float cz, float length, float yaw, int rows)
    {
        var root = Obj(parent, "PipeStack", cx, cz, yaw).gameObject;
        root.layer = LCover;
        float r = 0.34f;
        float width = rows * r * 2f;
        var bc = root.AddComponent<BoxCollider>(); bc.center = new Vector3(0, rows * r * 1.6f * 0.5f, 0); bc.size = new Vector3(length, rows * r * 1.7f, width);
        Slot(root, EnvCategory.Pipe, "Stack", bc.size);
        for (int row = 0; row < rows; row++)
        {
            int n = rows - row;
            for (int i = 0; i < n; i++)
            {
                float z = (i - (n - 1) * 0.5f) * r * 2.05f;
                CylQ(root.transform, "Pipe", new Vector3(0, r + row * r * 1.75f, z), r, length, Mat(row % 2 == 0 ? "pipe" : "steel"), 0, false, Quaternion.Euler(0, 0, 90), 12);
            }
        }
    }

    static void CableSpool(Transform parent, float x, float z)
    {
        var root = Obj(parent, "CableSpool", x, z, Rf(0, 360)).gameObject;
        root.layer = LCover;
        var bc = root.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.8f, 0); bc.size = new Vector3(1.3f, 1.6f, 1.6f);
        Slot(root, EnvCategory.Other, "Spool", bc.size);
        CylQ(root.transform, "FlangeA", new Vector3(-0.5f, 0.8f, 0), 0.8f, 0.08f, Mat("wood"), 0, false, Quaternion.Euler(0, 0, 90), 18);
        CylQ(root.transform, "FlangeB", new Vector3(0.5f, 0.8f, 0), 0.8f, 0.08f, Mat("wood"), 0, false, Quaternion.Euler(0, 0, 90), 18);
        CylQ(root.transform, "Core", new Vector3(0, 0.8f, 0), 0.5f, 0.9f, Mat("steelDark"), 0, false, Quaternion.Euler(0, 0, 90), 14);
    }

    static void TireStack(Transform parent, float x, float z, int n)
    {
        var root = Obj(parent, "Tires", x, z, 0).gameObject;
        root.layer = LCover;
        var bc = root.AddComponent<BoxCollider>(); bc.center = new Vector3(0, n * 0.14f, 0); bc.size = new Vector3(1.0f, n * 0.28f, 1.0f);
        Slot(root, EnvCategory.Other, "Tires", bc.size);
        for (int i = 0; i < n; i++)
            Cyl(root.transform, "Tire", new Vector3(Rf(-0.04f, 0.04f), 0.14f + i * 0.28f, Rf(-0.04f, 0.04f)), 0.45f, 0.28f, Mat("tire"), 0, false, 14);
    }

    static void LampPost(Transform parent, float x, float z, float yaw, bool withLight = true)
    {
        var root = Obj(parent, "LampPost", x, z, yaw).gameObject;
        root.layer = LObstacle;
        var cc = root.AddComponent<CapsuleCollider>(); cc.center = new Vector3(0, 2.75f, 0); cc.radius = 0.15f; cc.height = 5.5f;
        Slot(root, EnvCategory.Light, "LampPost", new Vector3(0.3f, 5.5f, 0.3f));
        Cyl(root.transform, "Pole", new Vector3(0, 2.75f, 0), 0.09f, 5.5f, Mat("steelDark"), 0, false, 10);
        Box(root.transform, "Arm", new Vector3(0.6f, 5.45f, 0), new Vector3(1.2f, 0.08f, 0.08f), Mat("steelDark"));
        Box(root.transform, "Head", new Vector3(1.15f, 5.38f, 0), new Vector3(0.7f, 0.1f, 0.35f), Mat("steelDark"));
        Box(root.transform, "Lens", new Vector3(1.15f, 5.32f, 0), new Vector3(0.6f, 0.04f, 0.28f), Mat("lampEmis"));
        if (withLight) AddLight(root.transform, "Light", new Vector3(1.15f, 5.1f, 0), LightType.Point, new Color(1f, 0.82f, 0.55f), 3.2f, 16f);
    }

    static GameObject CommsDish(Transform parent, Vector3 lp, float yaw, float scale, bool spin)
    {
        var root = Obj(parent, "Dish", lp.x, lp.z, yaw, lp.y).gameObject;
        Slot(root, EnvCategory.Other, "Dish", Vector3.one * 2f * scale);
        Cyl(root.transform, "Mast", new Vector3(0, 0.6f * scale, 0), 0.12f * scale, 1.2f * scale, Mat("steelDark"), 0, false, 10);
        var head = new GameObject("Head").transform; head.SetParent(root.transform, false); head.localPosition = new Vector3(0, 1.2f * scale, 0);
        if (spin) { var sp = head.gameObject.AddComponent<EnvSpin>(); sp.axis = Vector3.up; sp.degreesPerSecond = 40f; }
        var dish = CylQ(head, "Dish", new Vector3(0, 0.3f * scale, 0.5f * scale), 1.3f * scale, 0.08f * scale, Mat("steel"), 0, false, Quaternion.Euler(70, 0, 0), 24, false);
        CylQ(head, "Feed", new Vector3(0, 0.75f * scale, 0.95f * scale), 0.05f * scale, 0.9f * scale, Mat("steelDark"), 0, false, Quaternion.Euler(70, 0, 0), 8, false);
        return root;
    }

    static void AntennaMast(Transform parent, Vector3 lp, float height)
    {
        var root = Obj(parent, "Antenna", lp.x, lp.z, 0, lp.y).gameObject;
        Slot(root, EnvCategory.Other, "Antenna", new Vector3(0.5f, height, 0.5f));
        Cyl(root.transform, "Mast", new Vector3(0, height * 0.5f, 0), 0.07f, height, Mat("steel"), 0, false, 8);
        Box(root.transform, "CrossA", new Vector3(0, height * 0.75f, 0), new Vector3(1.6f, 0.05f, 0.05f), Mat("steel"));
        Box(root.transform, "CrossB", new Vector3(0, height * 0.6f, 0), new Vector3(1.0f, 0.05f, 0.05f), Mat("steel"));
        var beacon = Box(root.transform, "Beacon", new Vector3(0, height + 0.1f, 0), new Vector3(0.22f, 0.22f, 0.22f), Mat("redEmis"));
        var l = AddLight(root.transform, "BeaconLight", new Vector3(0, height + 0.3f, 0), LightType.Point, new Color(1f, 0.1f, 0.05f), 1.6f, 8f);
        var bl = beacon.AddComponent<EnvBlink>(); bl.target = beacon.GetComponent<MeshRenderer>(); bl.lightTarget = l; bl.period = 1.8f;
    }

    // ======================= kontainer =======================
    static GameObject Container(Transform parent, string name, float x0, float y0, float x1, float y1, string matKey, float baseY = 0f, bool doorEast = true)
    {
        float w = Mathf.Abs(x1 - x0) * S, d = Mathf.Abs(y1 - y0) * S;
        bool vertical = d > w;
        float len = vertical ? d : w, wid = vertical ? w : d;
        Vector2 c = V((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
        var root = Obj(parent, name, c.x, c.y, vertical ? 90f : 0f, baseY).gameObject;
        root.layer = LCover;
        float h = 2.6f;
        var bc = root.AddComponent<BoxCollider>(); bc.center = new Vector3(0, h * 0.5f, 0); bc.size = new Vector3(len, h, wid);
        Slot(root, EnvCategory.Container, matKey, bc.size);
        Transform t = root.transform;
        Box(t, "Body", new Vector3(0, h * 0.5f, 0), new Vector3(len, h, wid), Mat(matKey));
        M frame = Mat("steelDark"), steel = Mat("steel");
        foreach (float sx in new float[] { -1f, 1f })
            foreach (float sz in new float[] { -1f, 1f })
                Box(t, "Corner", new Vector3(sx * (len * 0.5f - 0.05f), h * 0.5f, sz * (wid * 0.5f - 0.05f)), new Vector3(0.14f, h + 0.02f, 0.14f), frame);
        float de = doorEast ? 1f : -1f;
        Box(t, "TopRail", new Vector3(0, h - 0.04f, 0), new Vector3(len + 0.02f, 0.08f, wid + 0.02f), frame);
        Box(t, "BottomRail", new Vector3(0, 0.05f, 0), new Vector3(len + 0.02f, 0.1f, wid + 0.02f), frame);
        foreach (float oz in new float[] { -0.35f, 0.35f })
            Box(t, "LockBar", new Vector3(de * (len * 0.5f + 0.02f), h * 0.5f, oz), new Vector3(0.05f, h - 0.3f, 0.05f), steel);
        Box(t, "DoorSeam", new Vector3(de * (len * 0.5f + 0.01f), h * 0.5f, 0), new Vector3(0.03f, h - 0.2f, 0.03f), frame);
        return root;
    }

    // ======================= generator mesin =======================
    static GameObject GeneratorMachine(Transform parent, string id, float x, float z, float yaw, out Transform[] attackPts)
    {
        var root = Obj(parent, "Generator_" + id, x, z, yaw).gameObject;
        root.layer = LGenerator;
        var bc = root.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 1.15f, 0); bc.size = new Vector3(3.4f, 2.3f, 2.2f);
        Slot(root, EnvCategory.Generator, "Generator_" + id, bc.size);
        Transform t = root.transform;
        Box(t, "Slab", new Vector3(0, 0.1f, 0), new Vector3(4.2f, 0.2f, 3.0f), Mat("bconcrete"));
        Box(t, "Skid", new Vector3(0, 0.3f, 0), new Vector3(3.4f, 0.2f, 2.0f), Mat("steelDark"));
        Box(t, "Body", new Vector3(0, 1.25f, 0), new Vector3(3.2f, 1.8f, 1.9f), Mat("cYellow"));
        Box(t, "Top", new Vector3(0, 2.2f, 0), new Vector3(3.3f, 0.1f, 2.0f), Mat("steelDark"));
        Box(t, "Grill", new Vector3(1.61f, 1.2f, 0), new Vector3(0.05f, 1.2f, 1.4f), Mat("steelDark"));
        Box(t, "GrillBars", new Vector3(1.64f, 1.2f, 0), new Vector3(0.03f, 1.1f, 0.06f), Mat("steel"));
        Box(t, "Panel", new Vector3(-0.2f, 1.35f, 0.96f), new Vector3(1.2f, 0.7f, 0.06f), Mat("steelDark"));
        Box(t, "LedOK", new Vector3(-0.55f, 1.5f, 1.0f), new Vector3(0.12f, 0.12f, 0.04f), Mat("greenEmis"));
        var warn = Box(t, "LedWarn", new Vector3(-0.2f, 1.5f, 1.0f), new Vector3(0.12f, 0.12f, 0.04f), Mat("redEmis"));
        var bl = warn.AddComponent<EnvBlink>(); bl.target = warn.GetComponent<MeshRenderer>(); bl.period = 2.2f;
        Cyl(t, "Exhaust", new Vector3(-1.2f, 2.9f, -0.5f), 0.14f, 1.6f, Mat("steelDark"), 0, false, 10);
        Cyl(t, "ExhaustCap", new Vector3(-1.2f, 3.75f, -0.5f), 0.2f, 0.1f, Mat("steel"), 0, false, 10);
        CylQ(t, "Cable", new Vector3(0.4f, 0.55f, 1.4f), 0.06f, 2.2f, Mat("tire"), 0, false, Quaternion.Euler(0, 0, 90), 6);
        AddLight(t, "Glow", new Vector3(0, 2.6f, 0), LightType.Point, new Color(1f, 0.6f, 0.25f), 1.4f, 7f);
        // cincin radius perbaikan
        float rr = 2.5f; int seg = 28;
        for (int i = 0; i < seg; i++)
        {
            float a = (i + 0.5f) / seg * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(a) * rr, 0.045f, Mathf.Sin(a) * rr);
            float segLen = 2f * Mathf.PI * rr / seg * 0.55f;
            Box(t, "Ring", p, new Vector3(segLen, 0.02f, 0.16f), Mat("ringEmis"), 0, false, -a * Mathf.Rad2Deg + 90f);
        }
        var slotC = root.AddComponent<EnvGeneratorSlot>();
        slotC.id = id;
        attackPts = new Transform[3];
        Vector3[] offs = { new Vector3(-2.4f, 0, 0), new Vector3(0, 0, 2.2f), new Vector3(0, 0, -2.2f) };
        for (int i = 0; i < 3; i++)
        {
            var ap = new GameObject("AttackPoint_" + i).transform;
            ap.SetParent(t, false); ap.localPosition = offs[i];
            attackPts[i] = ap;
        }
        slotC.attackPoints = attackPts;
        return root;
    }

    // ======================= tangki BBM =======================
    static void FuelTank(Transform parent, string name, float x, float z, float r, float h)
    {
        var root = Obj(parent, name, x, z, 0).gameObject;
        root.layer = LObstacle;
        var cc = root.AddComponent<CapsuleCollider>(); cc.center = new Vector3(0, h * 0.5f, 0); cc.radius = r; cc.height = h;
        Slot(root, EnvCategory.FuelTank, "Tank", new Vector3(r * 2, h, r * 2));
        Transform t = root.transform;
        Cyl(t, "Body", new Vector3(0, h * 0.5f, 0), r, h, Mat("tank"), 0, false, 28);
        Cyl(t, "Lid", new Vector3(0, h + 0.07f, 0), r * 0.97f, 0.14f, Mat("steel"), 0, false, 28);
        Cyl(t, "Cap", new Vector3(r * 0.3f, h + 0.3f, r * 0.2f), 0.45f, 0.3f, Mat("steelDark"), 0, false, 12);
        Cyl(t, "Base", new Vector3(0, 0.12f, 0), r * 1.03f, 0.24f, Mat("bconcrete"), 0, false, 28);
        Cyl(t, "Band", new Vector3(0, h * 0.5f, 0), r * 1.01f, 0.12f, Mat("steelDark"), 0, false, 28);
        // tangga
        float lx = 0f, lz = -r - 0.05f;
        Box(t, "LadderRailL", new Vector3(lx - 0.25f, h * 0.5f, lz), new Vector3(0.05f, h, 0.05f), Mat("steelDark"));
        Box(t, "LadderRailR", new Vector3(lx + 0.25f, h * 0.5f, lz), new Vector3(0.05f, h, 0.05f), Mat("steelDark"));
        for (int i = 1; i < 9; i++) Box(t, "Rung", new Vector3(lx, i * h / 9f, lz), new Vector3(0.5f, 0.04f, 0.04f), Mat("steelDark"));
        // pipa & katup
        CylQ(t, "Pipe", new Vector3(r + 0.4f, 0.7f, 0), 0.1f, 1.2f, Mat("pipe"), 0, false, Quaternion.Euler(0, 0, 90), 8);
        Cyl(t, "Valve", new Vector3(r + 0.6f, 0.95f, 0), 0.18f, 0.05f, Mat("redEmis"), 0, false, 10);
    }

    // ======================= menara jaga =======================
    static void Tower(Transform parent, string name, float x, float z, Vector2 faceDir, float phase)
    {
        var root = Obj(parent, name, x, z, YawFacing(faceDir)).gameObject;
        root.layer = LObstacle;
        var bc = root.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 3.5f, 0); bc.size = new Vector3(3.0f, 7f, 3.0f);
        Slot(root, EnvCategory.Tower, "GuardTower", bc.size);
        Transform t = root.transform;
        M st = Mat("steelDark"), wl = Mat("wallgray");
        foreach (float sx in new float[] { -1.3f, 1.3f })
            foreach (float sz in new float[] { -1.3f, 1.3f })
                Box(t, "Leg", new Vector3(sx, 3.5f, sz), new Vector3(0.2f, 7f, 0.2f), st);
        float ang = Mathf.Atan2(2.6f, 6f) * Mathf.Rad2Deg, blen = Mathf.Sqrt(2.6f * 2.6f + 6f * 6f);
        BoxQ(t, "BraceF", new Vector3(0, 3.5f, 1.3f), new Vector3(0.1f, blen, 0.1f), st, 0, false, Quaternion.Euler(0, 0, -ang));
        BoxQ(t, "BraceB", new Vector3(0, 3.5f, -1.3f), new Vector3(0.1f, blen, 0.1f), st, 0, false, Quaternion.Euler(0, 0, ang));
        BoxQ(t, "BraceL", new Vector3(-1.3f, 3.5f, 0), new Vector3(0.1f, blen, 0.1f), st, 0, false, Quaternion.Euler(-ang, 0, 0));
        BoxQ(t, "BraceR", new Vector3(1.3f, 3.5f, 0), new Vector3(0.1f, blen, 0.1f), st, 0, false, Quaternion.Euler(ang, 0, 0));
        Box(t, "Platform", new Vector3(0, 7.1f, 0), new Vector3(3.2f, 0.2f, 3.2f), st);
        Box(t, "Cabin", new Vector3(0, 8.0f, 0), new Vector3(2.4f, 1.6f, 2.4f), wl);
        Box(t, "Window", new Vector3(0, 8.15f, 1.22f), new Vector3(2.0f, 0.55f, 0.05f), Mat("winEmis"));
        Box(t, "WindowSide", new Vector3(1.22f, 8.15f, 0), new Vector3(0.05f, 0.55f, 1.6f), Mat("winEmis"));
        Box(t, "Roof", new Vector3(0, 8.95f, 0), new Vector3(3.2f, 0.15f, 3.2f), st);
        var lamp = new GameObject("Searchlight").transform;
        lamp.SetParent(t, false); lamp.localPosition = new Vector3(0, 7.55f, 1.5f); lamp.localRotation = Quaternion.Euler(16, 0, 0);
        CylQ(lamp, "Housing", new Vector3(0, 0, 0.1f), 0.28f, 0.5f, Mat("steel"), 0, false, Quaternion.Euler(90, 0, 0), 12, false);
        Box(lamp, "Lens", new Vector3(0, 0, 0.36f), new Vector3(0.4f, 0.4f, 0.04f), Mat("lampEmis"), 0, false, 0f).isStatic = false;
        var l = AddLight(lamp, "Spot", new Vector3(0, 0, 0.4f), LightType.Spot, new Color(1f, 0.93f, 0.8f), 22f, 65f, 32f);
        var sw = lamp.gameObject.AddComponent<EnvSearchlight>(); sw.phase = phase; sw.speed = 0.3f; sw.sweepAngle = 38f;
    }
}
#endif
