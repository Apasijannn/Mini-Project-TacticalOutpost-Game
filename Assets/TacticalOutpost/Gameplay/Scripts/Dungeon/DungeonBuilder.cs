using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Outpost.PCG;

namespace Outpost.Dungeon
{
    /// <summary>Referensi dari scene yang dibutuhkan builder (template lampu/generator, NavMesh surface, filter agent).</summary>
    public sealed class DungeonBuildContext
    {
        public GameObject generatorTemplate, lampTemplate;
        public NavMeshSurface[] surfaces;
        public NavMeshQueryFilter normalFilter, bossFilter;
        public int blockMask = ~0, probeMask = ~0;
        public bool preview;
    }

    /// <summary>Hasil BuildDungeon: GameObject hasil generation + mapping grid ↔ world + laporan NavMesh.</summary>
    public sealed class DungeonWorld
    {
        public DungeonLayout layout;
        public DungeonProfile profile;
        public PcgArena helper;                     // helper instantiate/measure/NavMesh yang sama dengan mode PCG
        public float tile, wallHeight, wallThickness;
        public Transform root, floorRoot, wallRoot, propRoot, lightRoot, markerRoot;
        public Bounds goalBounds;
        public Vector3 startWorld;
        public float startYaw;
        public readonly Dictionary<Vector2Int, GameObject> relayObjects = new Dictionary<Vector2Int, GameObject>();
        public int floorObjects, wallObjects, columnObjects, propObjects, lightObjects, gameObjectCount;
        public float buildMs, navMs;
        public bool navValid;
        public string navReport = "-";

        public float HalfX { get { return layout.width * tile * 0.5f; } }
        public float HalfZ { get { return layout.height * tile * 0.5f; } }

        /// <summary>GridToWorld (modul §68). Grid dipusatkan di (0,0,0) supaya NavMesh bake area sama dengan mode PCG.</summary>
        public Vector3 CellToWorld(Vector2Int c)
        {
            return new Vector3((c.x + 0.5f - layout.width * 0.5f) * tile, 0f, (c.y + 0.5f - layout.height * 0.5f) * tile);
        }

        public Vector2Int WorldToCell(Vector3 p)
        {
            return new Vector2Int(Mathf.FloorToInt(p.x / tile + layout.width * 0.5f), Mathf.FloorToInt(p.z / tile + layout.height * 0.5f));
        }

        public Vector3 VertexToWorld(int vx, int vy)
        {
            return new Vector3((vx - layout.width * 0.5f) * tile, 0f, (vy - layout.height * 0.5f) * tile);
        }
    }

    /// <summary>
    /// Mengubah grid menjadi scene Unity (modul §65–§70) dengan prefab modular yang SUDAH ADA di project:
    ///   Floor  → Road_set_v1_s_floor (1 tile per cell, di-scale ke tileSize)
    ///   Wall   → Concrete_fence_v2_S: hanya sisi wall yang bersebelahan dengan floor (IsWallVisible, §69), kolom di sudut
    ///   Goal   → generator + lampu merah (template scene PCG)
    ///   Lampu  → lampu jalan per room + burning barrel di tikungan corridor
    ///   Props  → obstacle/dressing mode PCG yang muat di 1 cell
    /// Tidak ada mesh/primitive buatan sendiri. Garis safe zone dan path guide memakai LineRenderer (penanda, tanpa collider).
    /// </summary>
    public static class DungeonBuilder
    {
        public static PcgArena CreateHelper(DungeonBuildContext ctx)
        {
            return new PcgArena
            {
                surfaces = ctx.surfaces, normalFilter = ctx.normalFilter, bossFilter = ctx.bossFilter,
                blockMask = ctx.blockMask, probeMask = ctx.probeMask, preview = ctx.preview,
                generatorTemplate = ctx.generatorTemplate, lampTemplate = ctx.lampTemplate, groundY = 0f,
            };
        }

        /// <summary>tileSize 0 = panjang satu segmen dinding (dinding tidak di-scale).</summary>
        public static float ResolveTile(DungeonProfile p, PcgArena helper)
        {
            if (p.tileSize > 0.01f) return p.tileSize;
            if (p.wallSpan == null) return 3f;
            Vector3 s = helper.SizeOf(p.wallSpan);
            return Mathf.Clamp(Mathf.Max(s.x, s.z), 2f, 8f);
        }

        /// <summary>Props yang boleh dipakai: footprint muat di satu cell (container panjang otomatis dilewati).</summary>
        public static bool[] PropAllowed(DungeonProfile p, PcgArena helper, float tile)
        {
            var allowed = new bool[p.propTable != null ? p.propTable.Length : 0];
            for (int i = 0; i < allowed.Length; i++)
            {
                var e = p.propTable[i];
                if (e == null || e.prefab == null) continue;
                Vector3 s = helper.SizeOf(e.prefab);
                allowed[i] = Mathf.Max(s.x, s.z) <= tile * 0.8f && s.y <= 4f;
            }
            return allowed;
        }

        public static DungeonWorld Build(DungeonLayout L, DungeonProfile p, DungeonBuildContext ctx, PcgArena helper, string rootName)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var w = new DungeonWorld { layout = L, profile = p, helper = helper };
            w.tile = ResolveTile(p, helper);
            w.root = new GameObject(rootName).transform;
            helper.root = w.root;
            helper.half = Mathf.Max(L.width, L.height) * w.tile * 0.5f + w.tile;
            w.floorRoot = Child(w.root, "Floor (grid → tile)");
            w.wallRoot = Child(w.root, "Walls (sisi wall yang terlihat)");
            w.propRoot = Child(w.root, "Props (cover, di luar jalur wajib)");
            w.lightRoot = Child(w.root, "Lighting (lampu existing)");
            w.markerRoot = Child(w.root, "Markers (safe zone)");
            helper.layoutRoot = w.propRoot;      // props selalu ikut di-bake ke NavMesh
            helper.dressingRoot = w.wallRoot;    // dinding juga, apa pun layer prefab-nya

            BuildFloor(w);
            BuildWalls(w);
            BuildGoal(w, ctx);
            BuildLights(w, ctx);
            BuildProps(w);
            BuildRelays(w);
            BuildSafeZone(w);
            ChooseStart(w);
            w.buildMs = (float)watch.Elapsed.TotalMilliseconds;

            if (!ctx.preview) BakeAndValidate(w, ctx);
            w.gameObjectCount = w.root.GetComponentsInChildren<Transform>(true).Length;
            w.root.gameObject.AddComponent<DungeonGizmos>().world = w;
            return w;
        }

        static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); return t;
        }

        /// <summary>Scale objek di sumbu dunia lewat pivot (aman untuk prefab FBX yang punya rotasi bawaan).</summary>
        public static Transform Stretch(GameObject go, Vector3 center, float yaw, Vector3 scale, Transform parent)
        {
            if (Mathf.Abs(scale.x - 1f) < 0.01f && Mathf.Abs(scale.y - 1f) < 0.01f && Mathf.Abs(scale.z - 1f) < 0.01f) return go.transform;
            var pivot = new GameObject(go.name).transform;
            pivot.SetParent(parent, false);
            pivot.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
            go.transform.SetParent(pivot, true);
            pivot.localScale = scale;
            return pivot;
        }

        // ---------------------------------------------------------------- floor

        static void BuildFloor(DungeonWorld w)
        {
            var p = w.profile; var L = w.layout;
            if (p.floorTile == null) return;
            Vector3 size = w.helper.SizeOf(p.floorTile);
            var scale = new Vector3(w.tile / Mathf.Max(0.1f, size.x), 1f, w.tile / Mathf.Max(0.1f, size.z));
            for (int x = 0; x < L.width; x++)
                for (int y = 0; y < L.height; y++)
                {
                    if (L.grid[x, y] == TileType.Wall) continue;
                    Vector3 c = w.CellToWorld(new Vector2Int(x, y));
                    var go = w.helper.Place(p.floorTile, c, 0f, w.floorRoot);
                    var t = Stretch(go, c, 0f, scale, w.floorRoot);
                    var b = PcgArena.Measure(t.gameObject);
                    t.position += Vector3.up * (0f - b.max.y);          // permukaan atas lantai = 0 m
                    t.name = "Floor " + x + "," + y;
                    w.floorObjects++;
                }
        }

        // ---------------------------------------------------------------- walls

        sealed class Vertex { public bool h, v; public Vector2 normal; }

        static void BuildWalls(DungeonWorld w)
        {
            var p = w.profile; var L = w.layout;
            if (p.wallSpan == null) return;
            Vector3 size = w.helper.SizeOf(p.wallSpan);
            bool spanAlongX = size.x >= size.z;
            float span = Mathf.Max(size.x, size.z);
            w.wallThickness = Mathf.Min(size.x, size.z);
            w.wallHeight = size.y;
            float factor = w.tile / Mathf.Max(0.1f, span);
            var vertices = new Dictionary<Vector2Int, Vertex>();
            System.Action<int, int, bool, Vector2> AddVertex = (vx, vy, horizontal, n) =>
            {
                var key = new Vector2Int(vx, vy);
                Vertex v; if (!vertices.TryGetValue(key, out v)) { v = new Vertex(); vertices[key] = v; }
                if (horizontal) v.h = true; else v.v = true;
                v.normal += n;
            };

            for (int x = 0; x < L.width; x++)
                for (int y = 0; y < L.height; y++)
                {
                    if (L.grid[x, y] == TileType.Wall) continue;
                    var cell = new Vector2Int(x, y);
                    Vector3 c = w.CellToWorld(cell);
                    foreach (var d in DungeonLayout.Directions)
                    {
                        if (L.IsFloor(cell + d)) continue;                          // hanya sisi yang menghadap Wall
                        bool runsAlongZ = d.x != 0;
                        Vector3 outward = new Vector3(d.x, 0f, d.y);
                        Vector3 pos = c + outward * (w.tile * 0.5f + w.wallThickness * 0.5f);
                        float yaw = runsAlongZ ? (spanAlongX ? 90f : 0f) : (spanAlongX ? 0f : 90f);
                        var go = w.helper.Place(p.wallSpan, pos, yaw, w.wallRoot);
                        PcgArena.EnsureSolid(go);
                        var t = Stretch(go, pos, runsAlongZ ? 90f : 0f, new Vector3(factor, 1f, 1f), w.wallRoot);
                        t.name = "Wall " + x + "," + y + " " + (d.x > 0 ? "E" : d.x < 0 ? "W" : d.y > 0 ? "N" : "S");
                        w.wallObjects++;
                        var n = new Vector2(d.x, d.y);
                        if (d.x > 0) { AddVertex(x + 1, y, false, n); AddVertex(x + 1, y + 1, false, n); }
                        else if (d.x < 0) { AddVertex(x, y, false, n); AddVertex(x, y + 1, false, n); }
                        else if (d.y > 0) { AddVertex(x, y + 1, true, n); AddVertex(x + 1, y + 1, true, n); }
                        else { AddVertex(x, y, true, n); AddVertex(x + 1, y, true, n); }
                    }
                }

            // Kolom di sudut (pertemuan dinding horizontal & vertical) menutup celah tebal dinding.
            if (p.wallColumn == null) return;
            float half = w.wallThickness * 0.5f;
            foreach (var kv in vertices)
            {
                if (!kv.Value.h || !kv.Value.v) continue;
                Vector2 n = kv.Value.normal;
                Vector3 pos = w.VertexToWorld(kv.Key.x, kv.Key.y) + new Vector3(Mathf.Sign(n.x) * (Mathf.Abs(n.x) > 0.01f ? half : 0f), 0f, Mathf.Sign(n.y) * (Mathf.Abs(n.y) > 0.01f ? half : 0f));
                var go = w.helper.Place(p.wallColumn, pos, 0f, w.wallRoot);
                PcgArena.EnsureSolid(go);
                go.name = "Column " + kv.Key.x + "," + kv.Key.y;
                w.columnObjects++;
            }
        }

        // ---------------------------------------------------------------- goal + lampu

        static void BuildGoal(DungeonWorld w, DungeonBuildContext ctx)
        {
            var L = w.layout;
            var prefab = ctx.generatorTemplate != null ? ctx.generatorTemplate : w.profile.goalPrefab;
            if (prefab == null || !L.IsFloor(L.goalPosition)) return;
            Vector3 size = w.helper.SizeOf(prefab);
            float limit = w.tile * 2.6f;                                              // footprint 3x3 cell, sisakan lorong di tepinya
            float scale = Mathf.Min(1f, limit / Mathf.Max(0.1f, Mathf.Max(size.x, size.z)));
            var goalRoot = Child(w.root, "Goal (generator)");
            var go = w.helper.Place(prefab, w.CellToWorld(L.goalPosition), 0f, goalRoot, scale);
            EnableLights(go);
            PcgArena.EnsureSolid(go);
            go.name = "Goal Generator";
            w.goalBounds = PcgArena.Measure(go);
            w.helper.generatorBounds = w.goalBounds;
            w.helper.generatorPos = new Vector3(w.goalBounds.center.x, 0f, w.goalBounds.center.z);
        }

        static void BuildLights(DungeonWorld w, DungeonBuildContext ctx)
        {
            var L = w.layout; var p = w.profile;
            var lamp = ctx.lampTemplate != null ? ctx.lampTemplate : p.lamp;
            var lit = new List<Vector2Int>();
            if (lamp != null)
            {
                if (L.algorithm == DungeonAlgorithm.BSP)
                {
                    foreach (var r in L.rooms)
                    {
                        if (r.index == L.goalRoom) continue;                          // goal room diterangi lampu merah generator
                        var corners = new[] { new Vector2Int(r.x, r.y), new Vector2Int(r.x + r.width - 1, r.y), new Vector2Int(r.x, r.y + r.height - 1), new Vector2Int(r.x + r.width - 1, r.y + r.height - 1) };
                        foreach (var c in corners)
                        {
                            if (!FreeForDecor(L, c)) continue;
                            PlaceLamp(w, lamp, c, r.Center);
                            lit.Add(c);
                            break;
                        }
                    }
                }
                else
                {
                    var rng = PcgRandom.For(L.seed, "lamps");
                    var cells = new List<Vector2Int>();
                    for (int x = 2; x < L.width - 2; x++) for (int y = 2; y < L.height - 2; y++) if (L.IsFloor(x, y) && !L.IsFloor(x - 1, y)) cells.Add(new Vector2Int(x, y));
                    rng.Shuffle(cells);
                    int target = Mathf.Max(4, L.floorCount / 110);
                    foreach (var c in cells)
                    {
                        if (lit.Count >= target) break;
                        if (!FreeForDecor(L, c) || lit.Exists(q => Vector2Int.Distance(q, c) < 9f)) continue;
                        PlaceLamp(w, lamp, c, c + Vector2Int.right);
                        lit.Add(c);
                    }
                }
            }

            // Corridor / dead-end diberi burning barrel (sumber cahaya existing) dengan jarak minimum.
            if (p.corridorLight == null) return;
            var placed = new List<Vector2Int>();
            for (int x = 1; x < L.width - 1; x++)
                for (int y = 1; y < L.height - 1; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (!L.corridor[x, y] || L.roomId[x, y] >= 0 || !FreeForDecor(L, c)) continue;
                    bool hx = L.IsFloor(c + Vector2Int.left) || L.IsFloor(c + Vector2Int.right);
                    bool hy = L.IsFloor(c + Vector2Int.up) || L.IsFloor(c + Vector2Int.down);
                    int n = 0; foreach (var d in DungeonLayout.Directions) if (L.IsFloor(c + d)) n++;
                    // Lorong biasa (2 tetangga) atau dead-end; persimpangan dibiarkan kosong supaya tidak menghalangi.
                    if (n > 2 || (!hx && !hy)) continue;
                    if (placed.Exists(q => Vector2Int.Distance(q, c) < p.corridorLightSpacing)) continue;
                    // Geser ke sudut dalam supaya tidak menghalangi lorong.
                    Vector3 offset = Vector3.zero;
                    foreach (var d in DungeonLayout.Directions) if (!L.IsFloor(c + d)) offset += new Vector3(d.x, 0f, d.y);
                    if (offset.sqrMagnitude < 0.01f)   // lorong lurus: dinding kiri & kanan saling meniadakan → tempel ke salah satu dinding
                        foreach (var d in DungeonLayout.Directions) if (!L.IsFloor(c + d)) { offset = new Vector3(d.x, 0f, d.y); break; }
                    Vector3 pos = w.CellToWorld(c) + offset.normalized * w.tile * 0.36f;
                    var go = w.helper.Place(p.corridorLight, pos, 0f, w.lightRoot);
                    EnableLights(go); PcgArena.EnsureSolid(go);
                    go.name = "Corridor light " + x + "," + y;
                    placed.Add(c);
                    w.lightObjects++;
                }
        }

        static bool FreeForDecor(DungeonLayout L, Vector2Int c)
        {
            if (!L.Walkable(c) || L.goalArea[c.x, c.y] || c == L.startPosition) return false;
            foreach (var q in L.placements) if (q.cell == c) return false;
            foreach (var d in DungeonLayout.Directions)
                if (L.IsInsideGrid(c + d) && L.corridor[c.x + d.x, c.y + d.y] && L.roomId[c.x, c.y] >= 0) return false;   // jangan di mulut pintu
            return true;
        }

        static void PlaceLamp(DungeonWorld w, GameObject lamp, Vector2Int cell, Vector2Int lookAt)
        {
            Vector3 c = w.CellToWorld(cell);
            Vector3 toCenter = w.CellToWorld(lookAt) - c; toCenter.y = 0f;
            // Lampu menempel ke pojok dinding, menghadap ke tengah room.
            Vector3 offset = Vector3.zero;
            foreach (var d in DungeonLayout.Directions) if (!w.layout.IsFloor(cell + d)) offset += new Vector3(d.x, 0f, d.y);
            Vector3 pos = c + (offset.sqrMagnitude > 0f ? offset.normalized : Vector3.zero) * w.tile * 0.3f;
            float yaw = toCenter.sqrMagnitude > 0.01f ? Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg : 0f;
            var go = w.helper.Place(lamp, pos, yaw, w.lightRoot);
            EnableLights(go); PcgArena.EnsureSolid(go);
            go.name = "Room lamp " + cell.x + "," + cell.y;
            w.lightObjects++;
        }

        static void EnableLights(GameObject go)
        {
            foreach (var l in go.GetComponentsInChildren<Light>(true)) { l.enabled = true; l.gameObject.SetActive(true); }
        }

        // ---------------------------------------------------------------- props

        static void BuildProps(DungeonWorld w)
        {
            var p = w.profile;
            foreach (var q in w.layout.placements)
            {
                if (q.kind != PlaceKind.Prop) continue;
                int index;
                if (!int.TryParse(q.id, out index) || p.propTable == null || index < 0 || index >= p.propTable.Length || p.propTable[index] == null || p.propTable[index].prefab == null) continue;
                var go = w.helper.Place(p.propTable[index].prefab, w.CellToWorld(q.cell), q.yaw, w.propRoot);
                PcgArena.EnsureSolid(go);
                go.name = "Prop " + q.cell.x + "," + q.cell.y + " " + p.propTable[index].prefab.name;
                w.propObjects++;
            }
        }

        /// <summary>Relay (objektif lock-and-key): Electric_box_v3 di cell hasil generator, padat dan ikut NavMesh.</summary>
        static void BuildRelays(DungeonWorld w)
        {
            if (w.profile.relayPrefab == null) return;
            var root = Child(w.root, "Power relays");
            foreach (var q in w.layout.placements)
            {
                if (q.kind != PlaceKind.Relay) continue;
                var go = w.helper.Place(w.profile.relayPrefab, w.CellToWorld(q.cell), q.yaw, root);
                PcgArena.EnsureSolid(go);
                go.name = "Power relay " + q.id + " @" + q.cell.x + "," + q.cell.y;
                w.relayObjects[q.cell] = go;
            }
        }

        // ---------------------------------------------------------------- start + safe zone

        static void ChooseStart(DungeonWorld w)
        {
            var L = w.layout;
            w.startWorld = w.CellToWorld(L.startPosition);
            Vector2Int look = L.criticalPath.Count > 3 ? L.criticalPath[3] : L.goalPosition;
            Vector3 d = w.CellToWorld(look) - w.startWorld;
            w.startYaw = d.sqrMagnitude > 0.01f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : 0f;
        }

        /// <summary>Garis hijau bercahaya di tepi safe zone; bagian yang melewati Wall tidak digambar.</summary>
        static void BuildSafeZone(DungeonWorld w)
        {
            var p = w.profile; var L = w.layout;
            float radius = p.safeRadiusCells * w.tile;
            Vector3 center = w.CellToWorld(L.startPosition);
            const int segments = 160;
            var run = new List<Vector3>();
            var runs = new List<List<Vector3>>();
            bool all = true;
            for (int i = 0; i <= segments; i++)
            {
                float t = i * Mathf.PI * 2f / segments;
                Vector3 pt = center + new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t)) * radius;
                if (L.IsFloor(w.WorldToCell(pt))) run.Add(pt + Vector3.up * 0.05f);
                else { all = false; if (run.Count > 1) runs.Add(run); run = new List<Vector3>(); }
            }
            if (run.Count > 1) runs.Add(run);
            int k = 0;
            foreach (var r in runs)
            {
                Line(w.markerRoot, "Safe zone (core) " + k, r, p.lineWidth, p.safeZoneColor * p.lineGlow, false, all);
                Line(w.markerRoot, "Safe zone (glow) " + k, r, p.lineWidth * 4f, p.safeZoneColor * 0.4f, true, all);
                k++;
            }
        }

        /// <summary>LineRenderer rebah di lantai (dipakai safe zone dan path guide). Tanpa collider.</summary>
        public static LineRenderer Line(Transform parent, string name, List<Vector3> points, float width, Color color, bool additive, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true; lr.loop = loop; lr.alignment = LineAlignment.TransformZ;
            lr.widthMultiplier = width; lr.numCornerVertices = 2; lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
            lr.positionCount = points.Count;
            lr.SetPositions(points.ToArray());
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = name + " material" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            if (additive && mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f); mat.SetFloat("_Blend", 2f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            lr.sharedMaterial = mat;
            lr.startColor = lr.endColor = Color.white;
            return lr;
        }

        // ---------------------------------------------------------------- NavMesh + validasi tingkat scene

        static void BakeAndValidate(DungeonWorld w, DungeonBuildContext ctx)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            w.helper.BuildNavMesh();
            float len = 0f;
            Vector3 start;
            bool startOk = w.helper.SampleNav(w.startWorld, 2f, ctx.normalFilter, out start);
            bool zombie = startOk && w.helper.PathToGenerator(start, ctx.normalFilter, out len);
            w.navReport = !startOk ? "Start di luar NavMesh" : zombie ? "OK start → goal " + len.ToString("0") + " m" : "NO PATH start → goal";
            if (startOk) w.startWorld = start;

            // Repair tingkat scene: bila mesh props ternyata memotong lorong, props terdekat jalur kritis dibuang lalu bake ulang.
            int removed = 0;
            while (startOk && !zombie && w.propRoot.childCount > 0 && removed < 20)
            {
                Transform worst = null; float best = float.MaxValue;
                foreach (Transform t in w.propRoot)
                    foreach (var c in w.layout.criticalPath)
                    {
                        float d = (t.position - w.CellToWorld(c)).sqrMagnitude;
                        if (d < best) { best = d; worst = t; }
                    }
                if (worst == null) break;
                PcgArena.Discard(worst.gameObject); removed++; w.propObjects--;
                w.helper.BuildNavMesh();
                zombie = w.helper.PathToGenerator(start, ctx.normalFilter, out len);
                w.navReport = (zombie ? "OK start → goal " + len.ToString("0") + " m" : "NO PATH") + " (props dibuang " + removed + ")";
            }
            w.navValid = zombie;
            w.navMs = (float)watch.Elapsed.TotalMilliseconds;
        }
    }
}
