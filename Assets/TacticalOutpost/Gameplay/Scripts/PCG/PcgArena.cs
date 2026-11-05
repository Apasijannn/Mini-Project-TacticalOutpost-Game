using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Outpost.Env;
using Object = UnityEngine.Object;

namespace Outpost.PCG
{
    /// <summary>Alasan kandidat ditolak (atau diterima) — dipakai statistik, panel debug, dan laporan eksperimen.</summary>
    public enum PcgRule { Accepted, OutsideArea, NearGenerator, NearGate, NearPlayer, Spacing, Blocked, OffNavMesh, NoPath, Detour }

    public sealed class PcgStageStats
    {
        public readonly string name;
        public int requested, accepted, attempts, fallback;
        public readonly int[] rejected = new int[Enum.GetValues(typeof(PcgRule)).Length];
        public PcgStageStats(string name) { this.name = name; }
        public void Reject(PcgRule rule) { rejected[(int)rule]++; }
        public int TotalRejected { get { int n = 0; for (int i = 1; i < rejected.Length; i++) n += rejected[i]; return n; } }
        public string RejectionSummary()
        {
            var parts = new List<string>();
            for (int i = 1; i < rejected.Length; i++) if (rejected[i] > 0) parts.Add((PcgRule)i + " " + rejected[i]);
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }
        public override string ToString() { return name + " " + accepted + "/" + requested + " (tries " + attempts + ")"; }
    }

    public struct PcgMarker
    {
        public Vector3 position;
        public PcgRule rule;
        public char stage;   // O obstacle, D dressing, S spawn, C supply cache
    }

    public sealed class PcgGate
    {
        public LaneId lane;
        public int startSpan;
        public Vector3 center, inward, along;
        public float halfWidth;
        public Vector3 Inner(float depth) { return center + inward * depth; }
    }

    public sealed class PcgPlaced
    {
        public GameObject go;
        public string prefab;
        public Vector3 position;
        public float radius;
    }

    /// <summary>Hasil satu kali generate arena + semua data validasi.</summary>
    public sealed class PcgArena
    {
        public PcgProfile profile;
        public int seed;
        public Transform root, layoutRoot, dressingRoot;
        public float span, half, groundY;
        public bool spanAlongX;
        public readonly List<PcgGate> gates = new List<PcgGate>();
        public Vector3 generatorPos;
        public Bounds generatorBounds;
        public Vector3 playerSpawn;
        public float playerYaw;
        public readonly List<PcgPlaced> obstacles = new List<PcgPlaced>();
        public readonly List<GameObject> dressing = new List<GameObject>();
        public readonly Dictionary<string, int> obstacleCounts = new Dictionary<string, int>();
        public readonly List<Vector3> attackPositions = new List<Vector3>();
        public readonly Dictionary<LaneId, List<Vector3>> spawnPositions = new Dictionary<LaneId, List<Vector3>>();
        public readonly Dictionary<LaneId, string> gateReport = new Dictionary<LaneId, string>();
        public int layoutAttempts, repairRemovals;
        public bool connected;

        public readonly PcgStageStats obstacleStats = new PcgStageStats("Obstacles");
        public readonly PcgStageStats dressingStats = new PcgStageStats("Dressing");
        public readonly PcgStageStats spawnStats = new PcgStageStats("Spawn points");
        public readonly List<PcgMarker> markers = new List<PcgMarker>();

        public NavMeshSurface[] surfaces;
        /// <summary>Salinan objek yang SUDAH ADA di map industrial (generator + lampu merah, menara + floodlight, lampu jalan).</summary>
        public GameObject generatorTemplate, towerTemplate, lampTemplate;
        /// <summary>Objek manual dari scene PCG (Static Objects): ikut di-bake ke NavMesh dan dihindari obstacle.</summary>
        public Transform staticRoot;
        /// <summary>Preview di editor (tanpa NavMesh): validasi jalur dilewati.</summary>
        public bool preview;
        public NavMeshQueryFilter normalFilter, bossFilter;
        public int blockMask, probeMask;

        readonly NavMeshPath path = new NavMeshPath();
        static readonly Collider[] buffer = new Collider[32];

        public Vector3 Center { get { return new Vector3(0f, groundY, 0f); } }

        public void Mark(Vector3 p, PcgRule rule, char stage)
        {
            if (markers.Count < 1500) markers.Add(new PcgMarker { position = p, rule = rule, stage = stage });
        }

        public bool InArena(Vector3 p, float margin)
        {
            return Mathf.Abs(p.x) <= half - margin && Mathf.Abs(p.z) <= half - margin;
        }

        public bool SampleNav(Vector3 p, float radius, NavMeshQueryFilter filter, out Vector3 onNav)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(p, out hit, radius, filter)) { onNav = hit.position; return true; }
            onNav = p; return false;
        }

        /// <summary>Jalur NavMesh lengkap (PathComplete) terpendek ke salah satu titik di sekeliling generator.</summary>
        public bool PathToGenerator(Vector3 from, NavMeshQueryFilter filter, out float length)
        {
            length = float.MaxValue;
            bool ok = false;
            Vector3 start;
            if (!SampleNav(from, 2f, filter, out start)) return false;
            foreach (var t in GeneratorRing(8, 1.4f))
            {
                Vector3 target;
                if (!SampleNav(t, 3f, filter, out target)) continue;
                if (!NavMesh.CalculatePath(start, target, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                float l = 0f; var c = path.corners;
                for (int i = 1; i < c.Length; i++) l += Vector3.Distance(c[i - 1], c[i]);
                if (l < length) { length = l; ok = true; }
            }
            return ok;
        }

        public IEnumerable<Vector3> GeneratorRing(int count, float clearance)
        {
            var e = generatorBounds.extents;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                yield return new Vector3(generatorBounds.center.x + Mathf.Cos(a) * (e.x + clearance), groundY, generatorBounds.center.z + Mathf.Sin(a) * (e.z + clearance));
            }
        }

        /// <summary>Kapsul mulai 0,3 m di atas lantai: lantai tidak dihitung, tembok/obstacle/lampu terdeteksi.</summary>
        public bool Blocked(Vector3 ground, float radius, float height = 1.8f)
        {
            Vector3 a = ground + Vector3.up * (0.3f + radius), b = ground + Vector3.up * Mathf.Max(0.3f + radius, height - radius);
            return Physics.CheckCapsule(a, b, radius, blockMask, QueryTriggerInteraction.Ignore);
        }

        public bool Overlaps(GameObject self, Bounds worldBounds)
        {
            Vector3 c = worldBounds.center + Vector3.up * 0.15f;
            Vector3 h = new Vector3(worldBounds.extents.x * 0.92f, Mathf.Max(0.05f, worldBounds.extents.y - 0.15f), worldBounds.extents.z * 0.92f);
            int n = Physics.OverlapBoxNonAlloc(c, h, buffer, Quaternion.identity, blockMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (buffer[i] != null && !buffer[i].transform.IsChildOf(self.transform)) return true;
            return false;
        }

        /// <summary>
        /// Bake NavMesh runtime dari collider arena. Mesh asset yang tidak Read/Write dipakai sebagai kotak dari mesh.bounds
        /// (tanpa membaca vertex), sehingga tidak perlu mengubah import setting asset mana pun.
        /// </summary>
        public void BuildNavMesh()
        {
            Physics.SyncTransforms();
            if (surfaces == null || root == null) return;
            var sources = new List<NavMeshBuildSource>();
            var colliders = new List<Collider>(root.GetComponentsInChildren<Collider>());
            if (staticRoot != null) colliders.AddRange(staticRoot.GetComponentsInChildren<Collider>());
            foreach (var col in colliders)
            {
                if (!col.enabled || col.isTrigger) continue;
                // Obstacle & props hasil PCG selalu ikut di-bake (apa pun layer prefab-nya) supaya zombie tidak menembusnya.
                bool generated = (layoutRoot != null && col.transform.IsChildOf(layoutRoot)) || (dressingRoot != null && col.transform.IsChildOf(dressingRoot)) ||
                                 (staticRoot != null && col.transform.IsChildOf(staticRoot));
                if (!generated && (probeMask & (1 << col.gameObject.layer)) == 0) continue;
                var src = new NavMeshBuildSource { area = 0, shape = NavMeshBuildSourceShape.Box };
                var box = col as BoxCollider;
                var meshCol = col as MeshCollider;
                if (box != null)
                {
                    src.transform = Matrix4x4.TRS(box.transform.TransformPoint(box.center), box.transform.rotation, Vector3.one);
                    src.size = Vector3.Scale(box.size, box.transform.lossyScale);
                }
                else if (meshCol != null && meshCol.sharedMesh != null && meshCol.sharedMesh.isReadable)
                {
                    src.shape = NavMeshBuildSourceShape.Mesh;
                    src.sourceObject = meshCol.sharedMesh;
                    src.transform = meshCol.transform.localToWorldMatrix;
                }
                else if (meshCol != null && meshCol.sharedMesh != null)
                {
                    var mb = meshCol.sharedMesh.bounds;
                    src.transform = meshCol.transform.localToWorldMatrix * Matrix4x4.Translate(mb.center);
                    src.size = mb.size;
                }
                else
                {
                    var b = col.bounds;
                    src.transform = Matrix4x4.Translate(b.center);
                    src.size = b.size;
                }
                sources.Add(src);
            }
            foreach (var s in surfaces)
            {
                if (s == null) continue;
                s.enabled = true;
                var bounds = new Bounds(s.transform.InverseTransformPoint(Center + Vector3.up * 4f), new Vector3(half * 2f + 12f, 16f, half * 2f + 12f));
                var data = NavMeshBuilder.BuildNavMeshData(s.GetBuildSettings(), sources, bounds, s.transform.position, s.transform.rotation);
                if (data == null) continue;
                data.name = s.gameObject.name + " (PCG)";
                s.RemoveData();
                s.navMeshData = data;
                s.AddData();
            }
        }

        public int LayoutHash()
        {
            unchecked
            {
                int h = 17;
                foreach (var g in gates) h = h * 31 + g.startSpan;
                foreach (var o in obstacles) h = h * 31 + Mathf.RoundToInt(o.position.x * 100f) * 7 + Mathf.RoundToInt(o.position.z * 100f) + o.prefab.Length;
                return h;
            }
        }

        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = Flat(b - a), ap = Flat(p - a);
            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Flat(p - (a + ab * t)).magnitude;
        }

        public static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // ------------------------------------------------------------ helpers instantiate

        static readonly Dictionary<GameObject, Vector3> sizeCache = new Dictionary<GameObject, Vector3>();

        public static Bounds Measure(GameObject go)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero); bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (r.name.IndexOf("beam", StringComparison.OrdinalIgnoreCase) >= 0 || r.name.IndexOf("dust", StringComparison.OrdinalIgnoreCase) >= 0) continue;   // kerucut cahaya bukan bagian fisik
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any)
                foreach (var c in go.GetComponentsInChildren<Collider>())
                { if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds); }
            return b;
        }

        /// <summary>Ukuran prefab pada rotasi 0 (di-cache).</summary>
        public Vector3 SizeOf(GameObject prefab)
        {
            Vector3 s;
            if (sizeCache.TryGetValue(prefab, out s)) return s;
            var tmp = Object.Instantiate(prefab, new Vector3(0f, -500f, 0f), Quaternion.identity);
            s = Measure(tmp).size;
            tmp.SetActive(false); Kill(tmp);
            sizeCache[prefab] = s;
            return s;
        }

        /// <summary>Instantiate prefab existing: pusat bounds di titik XZ, dasar mesh tepat di lantai.</summary>
        public GameObject Place(GameObject prefab, Vector3 xz, float yaw, Transform parent, float scale = 1f)
        {
            // Pertahankan kemiringan bawaan objek (mis. model FBX yang berdiri dengan rotasi X -90), ganti yaw-nya saja.
            // Tanpa ini Corner Tower (root X -90) terguling rebah di arena.
            Quaternion baseRot = prefab.transform.rotation;
            Quaternion tilt = Quaternion.Euler(0f, -baseRot.eulerAngles.y, 0f) * baseRot;
            var go = Object.Instantiate(prefab, new Vector3(xz.x, groundY, xz.z), Quaternion.Euler(0f, yaw, 0f) * tilt, parent);
            if (!Mathf.Approximately(scale, 1f)) go.transform.localScale = prefab.transform.localScale * scale;
            var b = Measure(go);
            go.transform.position += new Vector3(xz.x - b.center.x, groundY - b.min.y, xz.z - b.center.z);
            go.name = prefab.name;
            return go;
        }

        /// <summary>
        /// Pastikan objek padat: beberapa prefab (barel, cone, cinder block, dll.) di asset-nya tidak punya collider,
        /// sehingga bisa ditembus player dan zombie. Bila tidak ada collider padat sama sekali, setiap mesh yang terlihat
        /// diberi BoxCollider seukuran mesh-nya. Collider yang ada tetapi nonaktif dinyalakan.
        /// </summary>
        public static int addedColliders;

        public static int EnsureSolid(GameObject go)
        {
            bool solid = false;
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) continue;
                c.enabled = true;
                if (c.gameObject.activeInHierarchy) solid = true;
            }
            if (solid) return 0;
            int added = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var r = mf.GetComponent<Renderer>();
                if (mf.sharedMesh == null || r == null || !r.enabled) continue;
                if (mf.name.IndexOf("beam", StringComparison.OrdinalIgnoreCase) >= 0 || mf.name.IndexOf("dust", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                var b = mf.sharedMesh.bounds;
                if (b.size.sqrMagnitude < 0.0004f) continue;
                var box = mf.gameObject.AddComponent<BoxCollider>();
                box.center = b.center; box.size = b.size;
                added++; addedColliders++;
            }
            return added;
        }

        /// <summary>Destroy saat Play, DestroyImmediate saat preview di editor.</summary>
        public static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        public static void Discard(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            Kill(go);
        }
    }
}
