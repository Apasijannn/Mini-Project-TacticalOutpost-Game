#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Outpost.Env;

public static partial class OutpostBuilder
{
    static Transform envRoot, gMarkers;
    static NavMeshSurface navSurface;
    static readonly List<EnvLane> lanes = new List<EnvLane>();
    static int coverCount;

    static readonly Vector2[] laneNorth = { new Vector2(200, 285), new Vector2(262, 265), new Vector2(262, 205), new Vector2(262, 132), new Vector2(330, 136), new Vector2(430, 134), new Vector2(560, 136), new Vector2(591, 170), new Vector2(591, 225), new Vector2(630, 229), new Vector2(668, 229) };
    static readonly Vector2[] laneMain = { new Vector2(200, 285), new Vector2(300, 285), new Vector2(380, 300), new Vector2(450, 304), new Vector2(520, 306), new Vector2(570, 302), new Vector2(604, 300) };
    static readonly Vector2[] laneSouth = { new Vector2(200, 300), new Vector2(250, 335), new Vector2(262, 375), new Vector2(320, 374), new Vector2(420, 374), new Vector2(480, 376), new Vector2(530, 374), new Vector2(560, 372) };

    static EnvLane MakeLane(string name, LaneId id, Color col, Vector2[] pts)
    {
        var go = new GameObject(name);
        go.transform.SetParent(gMarkers, false);
        var lane = go.AddComponent<EnvLane>();
        lane.lane = id; lane.gizmoColor = col;
        for (int i = 0; i < pts.Length; i++)
        {
            var wp = new GameObject("WP_" + i.ToString("00")).transform;
            wp.SetParent(go.transform, false);
            wp.position = W(pts[i].x, pts[i].y);
        }
        lanes.Add(lane);
        return lane;
    }

    static void TrySetTag(GameObject go, string tag) { try { go.tag = tag; } catch (Exception) { Log("PERINGATAN: tag belum ada: " + tag); } }

    static void BuildGameplayMarkers()
    {
        gMarkers = Grp("Gameplay_Markers", envRoot);
        lanes.Clear();
        MakeLane("Lane_North_Flank", LaneId.North, new Color(0.3f, 0.8f, 1f), laneNorth);
        MakeLane("Lane_Main", LaneId.Main, new Color(1f, 0.9f, 0.2f), laneMain);
        MakeLane("Lane_South_Flank", LaneId.South, new Color(1f, 0.5f, 0.2f), laneSouth);

        var spawns = Grp("AttackerSpawns", gMarkers);
        LaneId[] ids = { LaneId.North, LaneId.Main, LaneId.South };
        float[] rows = { 266f, 286f, 306f };
        for (int r = 0; r < 3; r++)
            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject("Spawn_" + ids[r] + "_" + i);
                go.transform.SetParent(spawns, false);
                go.transform.position = W(110 + i * 20, rows[r]);
                go.transform.rotation = Quaternion.Euler(0, 90, 0);
                var sp = go.AddComponent<EnvSpawnPoint>(); sp.lane = ids[r]; sp.group = "Enemy";
                TrySetTag(go, "SpawnPoint");
            }
        var ps = new GameObject("PlayerSpawn");
        ps.transform.SetParent(gMarkers, false);
        ps.transform.position = W(842, 290);
        ps.transform.rotation = Quaternion.Euler(0, 270, 0);
        var psp = ps.AddComponent<EnvSpawnPoint>(); psp.lane = LaneId.Base; psp.group = "Player";
        TrySetTag(ps, "SpawnPoint");

        Transform[] pts;
        var gA = GeneratorP("A", 632, 300, new[] { new Vector3(-3.8f, 0, 0), new Vector3(0, 0, 3.6f), new Vector3(0, 0, -3.6f) }, out pts); TrySetTag(gA, "Generator");
        var gB = GeneratorP("B", 700, 229, new[] { new Vector3(-4.2f, 0, 0), new Vector3(4.2f, 0, 0) }, out pts); TrySetTag(gB, "Generator");
        var gC = GeneratorP("C", 560, 396, new[] { new Vector3(-3.8f, 0, 0), new Vector3(0, 0, 3.6f), new Vector3(0, 0, -3.6f) }, out pts); TrySetTag(gC, "Generator");
    }

    // ---------- cover points ----------
    static LaneId NearestLane(Vector3 p)
    {
        float best = 1e9f; LaneId id = LaneId.None;
        foreach (var l in lanes)
            foreach (var wp in l.Points())
            {
                float d = (new Vector2(wp.x, wp.z) - new Vector2(p.x, p.z)).magnitude;
                if (d < best) { best = d; id = l.lane; }
            }
        return best < 24f ? id : LaneId.None;
    }

    static string GenerateCoverPoints()
    {
        Physics.SyncTransforms();
        var parent = Grp("CoverPoints", gMarkers);
        int obstacleMask = (1 << LObstacle) | (1 << LCover) | (1 << LFence) | (1 << LGenerator);
        var existing = new List<Vector3>();
        var cols = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var sorted = new List<Collider>();
        foreach (var c in cols) if (c.gameObject.layer == LCover && !c.isTrigger) sorted.Add(c);
        sorted.Sort((a, b) => string.Compare(a.gameObject.name + a.transform.position.x.ToString("F2"), b.gameObject.name + b.transform.position.x.ToString("F2"), StringComparison.Ordinal));
        int cand = 0, rejNav = 0, rejInside = 0, rejDup = 0;
        foreach (var col in sorted)
        {
            Vector3 center, half; Quaternion rot;
            var box = col as BoxCollider;
            if (box != null)
            {
                Transform t = box.transform; Vector3 ls = t.lossyScale;
                center = t.TransformPoint(box.center);
                half = Vector3.Scale(box.size, new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z))) * 0.5f;
                rot = t.rotation;
            }
            else { var b = col.bounds; center = b.center; half = b.extents; rot = Quaternion.identity; }
            float height = half.y * 2f;
            if (height < 0.55f || half.x < 0.25f || half.z < 0.25f) continue;
            CoverHeight ch = height < 1.55f ? CoverHeight.Low : CoverHeight.High;
            Vector3[] normals = { rot * Vector3.right, rot * Vector3.left, rot * Vector3.forward, rot * Vector3.back };
            float[] ext = { half.x, half.x, half.z, half.z };
            float[] along = { half.z, half.z, half.x, half.x };
            Vector3[] tang = { rot * Vector3.forward, rot * Vector3.forward, rot * Vector3.right, rot * Vector3.right };
            for (int f = 0; f < 4; f++)
            {
                float len = along[f] * 2f;
                int n = Mathf.Max(1, Mathf.FloorToInt(len / 3.4f));
                for (int k = 0; k < n; k++)
                {
                    float off = n == 1 ? 0f : ((k + 0.5f) / n - 0.5f) * len;
                    Vector3 pos = center + normals[f] * (ext[f] + 0.95f) + tang[f] * off;
                    pos.y = 0.2f;
                    cand++;
                    NavMeshHit hit;
                    if (!NavMesh.SamplePosition(pos, out hit, 0.7f, NavMesh.AllAreas)) { rejNav++; continue; }
                    if (Physics.CheckSphere(hit.position + Vector3.up * 1.0f, 0.42f, obstacleMask, QueryTriggerInteraction.Ignore)) { rejInside++; continue; }
                    bool dup = false;
                    foreach (var e in existing) if ((e - hit.position).sqrMagnitude < 1.3f * 1.3f) { dup = true; break; }
                    if (dup) { rejDup++; continue; }
                    existing.Add(hit.position);
                    var go = new GameObject("Cover_" + existing.Count.ToString("000"));
                    go.transform.SetParent(parent, false);
                    go.transform.position = hit.position;
                    Vector3 toObj = -normals[f]; toObj.y = 0;
                    go.transform.rotation = Quaternion.LookRotation(toObj.normalized, Vector3.up);
                    var cp = go.AddComponent<EnvCoverPoint>();
                    cp.height = ch; cp.lane = NearestLane(hit.position); cp.face = col.gameObject.name;
                    TrySetTag(go, "CoverPoint");
                }
            }
        }
        coverCount = existing.Count;
        return "cover point: " + coverCount + " (kandidat " + cand + ", ditolak navmesh " + rejNav + ", di dalam objek " + rejInside + ", duplikat " + rejDup + ")";
    }

    // ---------- pencahayaan ----------
    static void BuildLighting()
    {
        var sunGo = new GameObject("Sun (Directional Light)");
        sunGo.transform.SetParent(envRoot, false);
        sunGo.transform.rotation = Quaternion.Euler(24f, -48f, 0f);
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.74f, 0.5f);
        sun.intensity = 2.3f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.92f;
        var sunData = sunGo.AddComponent<UniversalAdditionalLightData>();
        sunData.softShadowQuality = SoftShadowQuality.Medium;
        RenderSettings.sun = sun;

        var sky = AssetDatabase.LoadAssetAtPath<Material>(PackRoot + "/Textures/Skyboxes/Skybox_v9.mat");
        if (sky != null) RenderSettings.skybox = sky; else Log("PERINGATAN: Skybox_v9 tidak ditemukan");
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.50f, 0.58f, 0.70f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.46f, 0.38f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.19f, 0.16f);
        RenderSettings.ambientIntensity = 1.0f;
        RenderSettings.reflectionIntensity = 0.8f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0034f;
        RenderSettings.fogColor = new Color(0.66f, 0.62f, 0.6f);

        // volume pasca-proses
        EnsureFolder(Root);
        string vpPath = Root + "/OutpostVolumeProfile.asset";
        if (File.Exists(vpPath)) AssetDatabase.DeleteAsset(vpPath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, vpPath);
        var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);
        bloom.threshold.Override(1.0f); bloom.intensity.Override(0.7f); bloom.scatter.Override(0.65f);
        var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
        var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.26f); vig.smoothness.Override(0.5f);
        var ca = profile.Add<ColorAdjustments>(true);
        ca.postExposure.Override(0.25f); ca.contrast.Override(14f); ca.saturation.Override(10f);
        var wb = profile.Add<WhiteBalance>(true); wb.temperature.Override(8f);
        foreach (var comp in profile.components) AssetDatabase.AddObjectToAsset(comp, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        var volGo = new GameObject("Global Volume");
        volGo.transform.SetParent(envRoot, false);
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true; vol.sharedProfile = profile; vol.priority = 1;

        // kamera utama di titik spawn pemain, menghadap jalur tengah
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = W(842, 290) + new Vector3(0, 1.7f, 0);
        camGo.transform.rotation = Quaternion.Euler(4f, 270f, 0f);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 70f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 400f; cam.clearFlags = CameraClearFlags.Skybox;
        camGo.AddComponent<AudioListener>();
        var cd = camGo.AddComponent<UniversalAdditionalCameraData>();
        cd.renderPostProcessing = true; cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
    }

    // ---------- navmesh ----------
    static string BakeNavMesh()
    {
        int modCount = 0;
        foreach (var col in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            int ly = col.gameObject.layer;
            if (ly == LGround || col.isTrigger) continue;
            if (col.GetComponent<NavMeshModifier>() != null) continue;
            var mod = col.gameObject.AddComponent<NavMeshModifier>();
            mod.overrideArea = true; mod.area = 1; mod.applyToChildren = false;
            modCount++;
        }
        var navGo = new GameObject("NavMesh");
        navGo.transform.SetParent(envRoot, false);
        navSurface = navGo.AddComponent<NavMeshSurface>();
        navSurface.agentTypeID = 0;
        navSurface.collectObjects = CollectObjects.All;
        navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        navSurface.layerMask = (1 << LGround) | (1 << LObstacle) | (1 << LCover) | (1 << LFence) | (1 << LGenerator);
        navSurface.defaultArea = 0;
        Physics.SyncTransforms();
        navSurface.BuildNavMesh();
        string path = Root + "/NavMesh_TacticalOutpost.asset";
        if (navSurface.navMeshData != null)
        {
            if (!AssetDatabase.Contains(navSurface.navMeshData))
            {
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(navSurface.navMeshData, path);
            }
        }
        var tri = NavMesh.CalculateTriangulation();
        double area = 0;
        for (int i = 0; i + 2 < tri.indices.Length; i += 3)
        {
            Vector3 a = tri.vertices[tri.indices[i]], b = tri.vertices[tri.indices[i + 1]], c = tri.vertices[tri.indices[i + 2]];
            area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
        }
        return "modifier tak-berjalan: " + modCount + "; navmesh: " + (tri.indices.Length / 3) + " segitiga, luas ±" + Mathf.RoundToInt((float)area) + " m2";
    }

    static float PathLen(Vector3[] c) { float l = 0; for (int i = 1; i < c.Length; i++) l += Vector3.Distance(c[i - 1], c[i]); return l; }

    static string Validate()
    {
        var sb = new StringBuilder();
        int bad = 0;
        var gens = new Dictionary<string, Vector3>();
        foreach (var g in UnityEngine.Object.FindObjectsByType<EnvGeneratorSlot>(FindObjectsSortMode.None))
        {
            gens[g.id] = g.transform.position;
            if (g.attackPoints != null)
                for (int k = 0; k < g.attackPoints.Length; k++)
                {
                    NavMeshHit ah;
                    bool on = NavMesh.SamplePosition(g.attackPoints[k].position, out ah, 0.6f, NavMesh.AllAreas);
                    sb.AppendLine("titik serang " + g.id + "#" + k + ": " + (on ? "di navmesh" : "DI LUAR NAVMESH (>0.6 m)"));
                    if (!on) bad++;
                    if (k == 0 && on) gens[g.id] = ah.position;
                }
        }
        var sp = new Dictionary<LaneId, Vector3>();
        foreach (var s in UnityEngine.Object.FindObjectsByType<EnvSpawnPoint>(FindObjectsSortMode.None))
            if (s.group == "Enemy" && !sp.ContainsKey(s.lane) && s.name.EndsWith("_0")) sp[s.lane] = s.transform.position;
        Vector3 player = Vector3.zero;
        foreach (var s in UnityEngine.Object.FindObjectsByType<EnvSpawnPoint>(FindObjectsSortMode.None)) if (s.group == "Player") player = s.transform.position;

        var path = new NavMeshPath();
        Func<Vector3, Vector3, string> pl = (a, b) =>
        {
            NavMeshHit h1, h2;
            if (!NavMesh.SamplePosition(a, out h1, 2f, NavMesh.AllAreas)) return "start di luar navmesh";
            if (!NavMesh.SamplePosition(b, out h2, 2f, NavMesh.AllAreas)) return "tujuan di luar navmesh";
            bool ok = NavMesh.CalculatePath(h1.position, h2.position, NavMesh.AllAreas, path);
            if (!ok || path.status != NavMeshPathStatus.PathComplete) { bad++; return "TIDAK ADA JALUR (" + path.status + ")"; }
            return PathLen(path.corners).ToString("F1") + " m (lurus " + Vector3.Distance(h1.position, h2.position).ToString("F1") + ")";
        };
        foreach (var kv in sp)
            foreach (var g in gens)
                sb.AppendLine("spawn " + kv.Key + " -> generator " + g.Key + ": " + pl(kv.Value, g.Value));
        foreach (var g in gens) sb.AppendLine("pemain -> generator " + g.Key + ": " + pl(player, g.Value));
        // kontinuitas waypoint lane
        foreach (var l in lanes)
        {
            var pts = l.Points();
            for (int i = 1; i < pts.Length; i++)
            {
                string r = pl(pts[i - 1], pts[i]);
                float straight = Vector3.Distance(pts[i - 1], pts[i]);
                if (r.StartsWith("TIDAK") || r.Contains("luar")) { sb.AppendLine("LANE " + l.lane + " WP" + (i - 1) + "->" + i + ": " + r); }
                else
                {
                    NavMesh.CalculatePath(pts[i - 1], pts[i], NavMesh.AllAreas, path);
                    float pl2 = PathLen(path.corners);
                    if (pl2 > straight * 1.45f + 2f) { sb.AppendLine("LANE " + l.lane + " WP" + (i - 1) + "->" + i + ": memutar jauh (" + pl2.ToString("F1") + " vs " + straight.ToString("F1") + ")"); bad++; sb.AppendLine("   dari " + pts[i - 1] + " ke " + pts[i]); foreach (var cc in path.corners) sb.AppendLine("   corner " + cc); }
                }
            }
        }
        // total panjang jalur tiap lane (spawn pertama -> tengah)
        foreach (var l in lanes)
        {
            var pts = l.Points(); float total = 0; bool okAll = true;
            for (int i = 1; i < pts.Length; i++)
            {
                if (NavMesh.CalculatePath(pts[i - 1], pts[i], NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete) total += PathLen(path.corners); else okAll = false;
            }
            sb.AppendLine("panjang lane " + l.lane + ": " + total.ToString("F0") + " m" + (okAll ? "" : " (ADA SEGMEN PUTUS)"));
        }
        sb.AppendLine("masalah navigasi: " + bad);
        return sb.ToString();
    }

    // ---------- entry points ----------
    public static string ValidateOnly(string args)
    {
        lanes.Clear();
        foreach (var l in UnityEngine.Object.FindObjectsByType<EnvLane>(FindObjectsSortMode.None)) lanes.Add(l);
        lanes.Sort((a, b) => ((int)a.lane).CompareTo((int)b.lane));
        string s = Validate();
        File.WriteAllText(Path.Combine(StatusDir, "nav.txt"), s);
        return s;
    }

    public static string BuildAll(string args)
    {
        forceTextures = (args ?? "").Contains("force");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        report.Clear(); missingPrefabs.Clear(); prefabCache.Clear(); particleMats.Clear(); contMats = null; prefabCount = 0; coverCount = 0;
        rng = new System.Random(12345);
        Directory.CreateDirectory(StatusDir);
        AssetDatabase.StartAssetEditing();
        try { EnsureLayersAndTags(); } finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();

        Log(ConvertPackMaterials());
        ResetMeshAsset();
        BuildTextures();
        BuildMaterials();
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        envRoot = Grp("TacticalOutpost_Env", null);
        gGround = Grp("Ground", envRoot); gPerim = Grp("Perimeter", envRoot); gBuild = Grp("Buildings", envRoot);
        gCont = Grp("Containers", envRoot); gVeh = Grp("Vehicles", envRoot); gCover = Grp("Cover_Props", envRoot);
        gInd = Grp("Industrial", envRoot); gDecor = Grp("Decor", envRoot); gLights = Grp("Street_Lights", envRoot); gPlay = Grp("Objectives", envRoot);

        BuildLayout();
        BuildGameplayMarkers();
        BuildLighting();
        Physics.SyncTransforms();
        Log(BakeNavMesh());
        Log(GenerateCoverPoints());
        Log("--- validasi navigasi ---");
        Log(Validate());
        if (missingPrefabs.Count > 0) { Log("PREFAB HILANG:"); foreach (var m in missingPrefabs) Log("  " + m); }
        Log("prefab pack dipakai: " + prefabCount);

        EditorSceneManager.MarkSceneDirty(scene);
        string dir = Path.GetDirectoryName(ScenePath).Replace('\\', '/');
        EnsureFolder(dir);
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        Log("scene disimpan: " + saved + " -> " + ScenePath);
        var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
        foreach (var s in EditorBuildSettings.scenes) if (s.path != ScenePath) list.Add(s);
        EditorBuildSettings.scenes = list.ToArray();
        AssetDatabase.SaveAssets();
        Log("waktu build: " + sw.Elapsed.TotalSeconds.ToString("F1") + " dtk");
        File.WriteAllText(Path.Combine(StatusDir, "report.txt"), report.ToString());
        return report.ToString();
    }
}
#endif
