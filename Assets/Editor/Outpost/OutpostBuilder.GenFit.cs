#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine.AI;
using UnityEngine;
using Outpost.Enemy;
using Outpost.Env;

// Perintah editor untuk generator: "run gendiag" (laporan ukuran/collider/titik serang), "run setupgenbar" (pasang health bar)
public static partial class OutpostBuilder
{
    static string GenDiag()
    {
        var sb = new StringBuilder();
        foreach (var g in Object.FindObjectsByType<GeneratorHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var t = g.transform;
            sb.AppendLine("GEN " + g.name + " pos " + t.position.ToString("F2") + " localScale " + t.localScale.ToString("F3") + " lossy " + t.lossyScale.ToString("F3") + " rot " + t.eulerAngles.ToString("F0"));
            var bc = g.GetComponent<BoxCollider>();
            if (bc != null) sb.AppendLine("  BoxCollider center " + bc.center.ToString("F2") + " size " + bc.size.ToString("F2") + " world bounds " + bc.bounds.min.ToString("F2") + " .. " + bc.bounds.max.ToString("F2") + " (size " + bc.bounds.size.ToString("F2") + ")");
            Bounds rb = new Bounds(t.position, Vector3.zero); bool first = true;
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r.GetComponent<Light>() != null) continue;
                if (first) { rb = r.bounds; first = false; } else rb.Encapsulate(r.bounds);
            }
            sb.AppendLine("  render bounds " + rb.min.ToString("F2") + " .. " + rb.max.ToString("F2") + " (size " + rb.size.ToString("F2") + ")");
            foreach (Transform c in t) sb.AppendLine("  child " + c.name + " lp " + c.localPosition.ToString("F2") + " ls " + c.localScale.ToString("F3") + " active " + c.gameObject.activeSelf);
            var slot = g.GetComponent<EnvGeneratorSlot>();
            if (slot != null)
            {
                sb.AppendLine("  slot repairRadius " + slot.repairRadius + " attackPoints " + (slot.attackPoints != null ? slot.attackPoints.Length : 0));
                if (slot.attackPoints != null)
                    foreach (var ap in slot.attackPoints) if (ap != null) { var cp = bc != null ? bc.ClosestPoint(ap.position) : t.position; sb.AppendLine("   " + ap.name + " wp " + ap.position.ToString("F2") + " distToCollider " + Vector3.Distance(new Vector3(ap.position.x, cp.y, ap.position.z), cp).ToString("F2")); }
            }
            UnityEngine.AI.NavMeshHit h; bool onNav = UnityEngine.AI.NavMesh.SamplePosition(t.position, out h, 1f, UnityEngine.AI.NavMesh.AllAreas);
            sb.AppendLine("  navmesh di pusat generator: " + (onNav ? "ADA (navmesh masih di bawah generator -> perlu rebake)" : "tidak ada"));
        }
        return sb.ToString();
    }
    // pasang bar darah besar di atas generator (dipakai builder dan perintah "run setupgenbar")
    static void WireGeneratorBar(GameObject genRoot)
    {
        var gh = genRoot.GetComponent<GeneratorHealth>(); if (gh == null) return;
        var bar = genRoot.GetComponent<GeneratorHealthBar>(); if (bar == null) bar = genRoot.AddComponent<GeneratorHealthBar>();
        bar.generator = gh;
        bar.barBackground = GuiSprite("Ui/Icon_34.png"); bar.barFill = GuiSprite("Ui/Icon_35.png");
        bar.width = 12f; bar.heightAboveTop = 2f; bar.label = "GENERATOR";
        EditorUtility.SetDirty(bar);
    }

    static string SetupGenBar()
    {
        int n = 0;
        foreach (var g in Object.FindObjectsByType<GeneratorHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { WireGeneratorBar(g.gameObject); n++; }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        bool ok = UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        return "health bar generator dipasang pada " + n + " generator, scene tersimpan=" + ok;
    }

    // bangun ulang navmesh pada surface yang sudah ada (tanpa membuat objek baru, tanpa menyentuh environment)
    static string RebakeNav()
    {
        var surf = Object.FindFirstObjectByType<NavMeshSurface>();
        if (surf == null) return "NavMeshSurface tidak ditemukan";
        Physics.SyncTransforms();
        surf.BuildNavMesh();
        string path = Root + "/NavMesh_TacticalOutpost.asset";
        if (surf.navMeshData != null)
        {
            if (!AssetDatabase.Contains(surf.navMeshData))
            {
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(surf.navMeshData, path);
            }
            else EditorUtility.SetDirty(surf.navMeshData);
        }
        EditorUtility.SetDirty(surf);
        AssetDatabase.SaveAssets();
        var tri = NavMesh.CalculateTriangulation();
        return "navmesh dibangun ulang: " + (tri.indices.Length / 3) + " segitiga";
    }

    // "run genfit": sesuaikan collider, titik serang zombie, dan lampu generator ke ukuran model generator sekarang, lalu rebake navmesh
    static string GenFit()
    {
        var sb = new StringBuilder();
        foreach (var g in Object.FindObjectsByType<GeneratorHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var t = g.transform;
            Transform model = null;
            foreach (Transform c in t) if (c.name.StartsWith("Generator_v1")) model = c;
            if (model == null) { sb.AppendLine("model generator tidak ditemukan di " + g.name); continue; }
            bool first = true; Bounds mb = new Bounds();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer) continue;
                if (first) { mb = r.bounds; first = false; } else mb.Encapsulate(r.bounds);
            }
            if (first) { sb.AppendLine("model tanpa renderer"); continue; }
            sb.AppendLine("model " + model.name + " skala " + model.localScale.x.ToString("F2") + " bounds size " + mb.size.ToString("F2") + " center " + mb.center.ToString("F2"));

            // 1) collider mengikuti model (+ margin 0.15 m tiap sisi)
            var bc = g.GetComponent<BoxCollider>(); if (bc == null) bc = g.gameObject.AddComponent<BoxCollider>();
            const float margin = 0.15f;
            Vector3 ls = t.lossyScale;
            bc.center = t.InverseTransformPoint(mb.center);
            bc.size = new Vector3((mb.size.x + 2 * margin) / ls.x, (mb.size.y + 2 * margin) / ls.y, (mb.size.z + 2 * margin) / ls.z);
            EditorUtility.SetDirty(bc);
            float hx = bc.size.x * 0.5f, hz = bc.size.z * 0.5f; Vector3 cc = bc.center;

            // 2) titik serang: 2 di ujung + 3 di tiap sisi panjang, 1,3 m dari tepi collider
            const float gap = 1.3f;
            var offs = new List<Vector3> { new Vector3(cc.x - hx - gap, 0f, cc.z), new Vector3(cc.x + hx + gap, 0f, cc.z) };
            foreach (float sz in new[] { 1f, -1f }) foreach (float sx in new[] { -0.6f, 0f, 0.6f }) offs.Add(new Vector3(cc.x + sx * hx, 0f, cc.z + sz * (hz + gap)));
            var slot = g.GetComponent<EnvGeneratorSlot>();
            var pts = new Transform[offs.Count];
            for (int i = 0; i < offs.Count; i++)
            {
                var ap = t.Find("AttackPoint_" + i);
                if (ap == null) { ap = new GameObject("AttackPoint_" + i).transform; ap.SetParent(t, false); }
                ap.localPosition = offs[i]; ap.localRotation = Quaternion.identity;
                pts[i] = ap;
            }
            for (int i = offs.Count; ; i++) { var extra = t.Find("AttackPoint_" + i); if (extra == null) break; Object.DestroyImmediate(extra.gameObject); }
            if (slot != null) { slot.attackPoints = pts; EditorUtility.SetDirty(slot); }

            // 3) lampu mengikuti ukuran model
            float sc = model.localScale.x, f = sc / 1.45f;
            var glow = t.Find("Glow"); if (glow != null) glow.localPosition = new Vector3(0f, 1.8f * sc + 1.2f, 0f);
            var ff = t.Find("FrontFill"); if (ff != null) ff.localPosition = new Vector3(0f, 2.2f * f, 3.2f * f);
            var bf = t.Find("BackFill"); if (bf != null) bf.localPosition = new Vector3(0f, 2.2f * f, -3.2f * f);
            sb.AppendLine("collider size " + bc.size.ToString("F2") + " center " + bc.center.ToString("F2") + "; " + offs.Count + " titik serang");
        }
        sb.AppendLine(RebakeNav());
        // 4) tempelkan titik serang ke navmesh baru
        foreach (var g in Object.FindObjectsByType<GeneratorHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var slot = g.GetComponent<EnvGeneratorSlot>(); if (slot == null || slot.attackPoints == null) continue;
            foreach (var ap in slot.attackPoints)
            {
                NavMeshHit h;
                if (NavMesh.SamplePosition(ap.position, out h, 2.5f, NavMesh.AllAreas)) { sb.AppendLine("  " + ap.name + " snap " + Vector3.Distance(ap.position, h.position).ToString("F2") + " m"); ap.position = new Vector3(h.position.x, ap.position.y, h.position.z); }
                else sb.AppendLine("  " + ap.name + " TIDAK ADA navmesh dalam 2.5 m!");
            }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        sb.AppendLine("scene tersimpan=" + UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes());
        return sb.ToString();
    }
}
#endif
