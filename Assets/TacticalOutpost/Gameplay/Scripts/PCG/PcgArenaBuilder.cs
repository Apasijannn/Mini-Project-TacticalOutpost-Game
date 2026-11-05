using System.Collections.Generic;
using UnityEngine;
using Outpost.Env;

namespace Outpost.PCG
{
    /// <summary>
    /// Menyusun arena PCG dari prefab yang SUDAH ADA di project (lantai jalan, pagar beton, generator, tower floodlight,
    /// lampu jalan, container, barrier, dumpster, barel, sandbag, crate). Tidak ada mesh/primitive buatan sendiri.
    ///
    /// Urutan (semua dari seed, sehingga reproducible):
    ///  1. Lantai + pagar perimeter dengan 3 gerbang (posisi gerbang procedural)
    ///  2. Generator objective di tengah + menara sudut + lampu jalan
    ///  3. Obstacle/cover: weighted random + generate-and-test (6 aturan)
    ///  4. NavMesh di-bake ulang lalu divalidasi: setiap gerbang harus punya jalur ke generator (zombie & boss),
    ///     tidak memutar lebih dari maxDetourRatio. Gagal → layout dibuat ulang (stream berikut), lalu repair.
    ///  5. Dressing kosmetik, spawn point zombie per gerbang, attack point generator, posisi player.
    /// </summary>
    public static class PcgArenaBuilder
    {
        public static PcgArena Build(PcgProfile p, int seed, PcgArena template)
        {
            var a = new PcgArena
            {
                profile = p, seed = seed,
                surfaces = template.surfaces, normalFilter = template.normalFilter, bossFilter = template.bossFilter,
                blockMask = template.blockMask, probeMask = template.probeMask,
                generatorTemplate = template.generatorTemplate, towerTemplate = template.towerTemplate, lampTemplate = template.lampTemplate,
                staticRoot = template.staticRoot, preview = template.preview,
            };
            PcgArena.addedColliders = 0;
            a.root = new GameObject("PCG_Arena (seed " + seed + ")").transform;
            a.groundY = 0f;

            BuildFloor(a);
            BuildPerimeter(a);
            BuildObjective(a);
            BuildLights(a);
            ChoosePlayerSpawn(a);
            BuildLayout(a);
            BuildDressing(a);
            RevalidateWithDressing(a);
            PlaceSpawnPoints(a);
            PlaceAttackPoints(a);
            BuildSafeZone(a);
            a.root.gameObject.AddComponent<PcgGizmos>().arena = a;   // spawn area, safe zone, dll. terlihat di Scene View
            LogSolidCheck(a);
            return a;
        }

        // ---------------------------------------------------------------- 1. lantai + pagar

        static void BuildFloor(PcgArena a)
        {
            var p = a.profile;
            Vector3 fence = a.SizeOf(p.fenceSpan);
            a.spanAlongX = fence.x >= fence.z;
            a.span = Mathf.Max(fence.x, fence.z);
            a.half = p.spansPerSide * a.span * 0.5f;
            if (p.floorTile == null) return;

            var floorRoot = new GameObject("Floor").transform; floorRoot.SetParent(a.root, false);
            Vector3 tile = a.SizeOf(p.floorTile);
            float extent = a.half + 4f;
            // Batasi jumlah tile (maks ~22 x 22): tile kecil diperbesar seragam.
            float scale = Mathf.Max(1f, extent * 2f / Mathf.Max(0.5f, Mathf.Min(tile.x, tile.z)) / 22f);
            tile *= scale;
            int nx = Mathf.CeilToInt(extent * 2f / Mathf.Max(0.5f, tile.x)), nz = Mathf.CeilToInt(extent * 2f / Mathf.Max(0.5f, tile.z));
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    var xz = new Vector3(-extent + (ix + 0.5f) * tile.x, 0f, -extent + (iz + 0.5f) * tile.z);
                    var go = a.Place(p.floorTile, xz, 0f, floorRoot, scale);
                    // Permukaan atas lantai = 0 m.
                    var b = PcgArena.Measure(go);
                    go.transform.position += Vector3.up * (a.groundY - b.max.y);
                }
        }

        static void BuildPerimeter(PcgArena a)
        {
            var p = a.profile;
            var rng = PcgRandom.For(a.seed, "gates");
            var wallRoot = new GameObject("Perimeter").transform; wallRoot.SetParent(a.root, false);
            int n = p.spansPerSide;
            int maxStart = Mathf.Max(p.gateCornerMargin, n - p.gateCornerMargin - p.gateSpans);

            // Sisi: West (x = -half), North (z = +half), South (z = -half), East (x = +half, tanpa gerbang).
            var sides = new[] { LaneId.West, LaneId.North, LaneId.South, LaneId.East };
            foreach (var side in sides)
            {
                bool alongX = side == LaneId.North || side == LaneId.South;
                Vector3 inward = side == LaneId.West ? Vector3.right : side == LaneId.East ? Vector3.left : side == LaneId.North ? Vector3.back : Vector3.forward;
                Vector3 wallPoint = -inward * a.half;
                Vector3 along = alongX ? Vector3.right : Vector3.forward;
                float yaw = (alongX == a.spanAlongX) ? 0f : 90f;

                int gateStart = -100;
                if (side != LaneId.East)
                {
                    gateStart = rng.Range(p.gateCornerMargin, maxStart + 1);
                    var gate = new PcgGate
                    {
                        lane = side, startSpan = gateStart, inward = inward, along = along,
                        center = wallPoint + along * (-a.half + (gateStart + p.gateSpans * 0.5f) * a.span),
                        halfWidth = p.gateSpans * a.span * 0.5f,
                    };
                    a.gates.Add(gate);
                }

                for (int i = 0; i < n; i++)
                {
                    if (i >= gateStart && i < gateStart + p.gateSpans) continue;
                    Vector3 c = wallPoint + along * (-a.half + (i + 0.5f) * a.span);
                    a.Place(p.fenceSpan, c, yaw, wallRoot);
                }
                if (side != LaneId.East && p.fenceEnd != null)
                {
                    Vector3 g0 = wallPoint + along * (-a.half + gateStart * a.span);
                    Vector3 g1 = wallPoint + along * (-a.half + (gateStart + p.gateSpans) * a.span);
                    a.Place(p.fenceEnd, g0 + inward * 0.3f, yaw, wallRoot);
                    a.Place(p.fenceEnd, g1 + inward * 0.3f, yaw, wallRoot);
                }
            }
        }

        // ---------------------------------------------------------------- 2. objective + lampu

        static void BuildObjective(PcgArena a)
        {
            var p = a.profile;
            var objRoot = new GameObject("Generator Objective").transform; objRoot.SetParent(a.root, false);
            // Generator yang sama dengan map industrial (lengkap dengan 6 floodlight merah & dust beam) bila tersedia.
            var gen = a.Place(a.generatorTemplate != null ? a.generatorTemplate : p.generator, Vector3.zero, 0f, objRoot);
            EnableLights(gen);
            PcgArena.EnsureSolid(gen);
            Bounds b = PcgArena.Measure(gen);
            if (a.generatorTemplate == null && p.generatorProps != null)
                for (int i = 0; i < p.generatorProps.Length; i++)
                {
                    var prop = p.generatorProps[i];
                    if (prop == null) continue;
                    Vector3 size = a.SizeOf(prop);
                    float side = i % 2 == 0 ? 1f : -1f;
                    float offset = b.extents.x + size.x * 0.5f + 0.6f + (i / 2) * (size.x + 0.4f);
                    var go = a.Place(prop, new Vector3(side * offset, 0f, 0f), side > 0 ? 270f : 90f, objRoot);
                    PcgArena.EnsureSolid(go);
                    b.Encapsulate(PcgArena.Measure(go));
                }
            a.generatorBounds = b;
            a.generatorPos = new Vector3(b.center.x, a.groundY, b.center.z);
        }

        static void BuildLights(PcgArena a)
        {
            var p = a.profile;
            var lightRoot = new GameObject("Lighting (asset lampu & tower)").transform; lightRoot.SetParent(a.root, false);
            var tower = a.towerTemplate != null ? a.towerTemplate : p.cornerTower;
            if (tower != null)
            {
                float c = a.half - 4f;
                foreach (var corner in new[] { new Vector3(c, 0f, c), new Vector3(-c, 0f, c), new Vector3(c, 0f, -c), new Vector3(-c, 0f, -c) })
                {
                    float yaw = Mathf.Atan2(-corner.x, -corner.z) * Mathf.Rad2Deg;
                    var t = a.Place(tower, corner, yaw, lightRoot);
                    EnableLights(t); PcgArena.EnsureSolid(t);
                }
            }
            var lamp = a.lampTemplate != null ? a.lampTemplate : p.lamp;
            if (lamp == null) return;
            int n = p.spansPerSide;
            foreach (var side in new[] { LaneId.West, LaneId.North, LaneId.South, LaneId.East })
            {
                bool alongX = side == LaneId.North || side == LaneId.South;
                Vector3 inward = side == LaneId.West ? Vector3.right : side == LaneId.East ? Vector3.left : side == LaneId.North ? Vector3.back : Vector3.forward;
                Vector3 along = alongX ? Vector3.right : Vector3.forward;
                PcgGate gate = a.gates.Find(g => g.lane == side);
                for (int i = p.lampEverySpans / 2 + 1; i < n - 1; i += p.lampEverySpans)
                {
                    if (gate != null && i >= gate.startSpan - 1 && i <= gate.startSpan + p.gateSpans) continue;
                    Vector3 pos = -inward * a.half + inward * 1.4f + along * (-a.half + (i + 0.5f) * a.span);
                    float yaw = Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg;
                    var l = a.Place(lamp, pos, yaw, lightRoot);
                    EnableLights(l); PcgArena.EnsureSolid(l);
                }
            }
        }

        /// <summary>Lampu bawaan prefab (mis. floodlight menara) di asset-nya dalam keadaan off; di arena dinyalakan.</summary>
        static void EnableLights(GameObject go)
        {
            foreach (var l in go.GetComponentsInChildren<Light>(true)) { l.enabled = true; l.gameObject.SetActive(true); }
        }

        static void ChoosePlayerSpawn(PcgArena a)
        {
            var rng = PcgRandom.For(a.seed, "player");
            var e = a.generatorBounds.extents;
            // Sisi timur generator (tidak pernah ada gerbang di timur) → jauh dari semua spawn zone zombie.
            a.playerSpawn = a.generatorPos + new Vector3(e.x + rng.Range(9f, 12f), 0f, rng.Range(-3f, 3f));
            var target = a.gates.Count > 0 ? a.gates[0].center : a.generatorPos + Vector3.left * 10f;
            Vector3 d = PcgArena.Flat(target - a.playerSpawn);
            a.playerYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        // ---------------------------------------------------------------- 3 + 4. obstacle layout + validasi NavMesh

        static void BuildLayout(PcgArena a)
        {
            var p = a.profile;
            a.layoutRoot = new GameObject("Obstacles").transform; a.layoutRoot.SetParent(a.root, false);
            if (a.preview)
            {
                // Preview editor: tidak ada NavMesh, jadi cukup satu layout tanpa validasi jalur.
                a.layoutAttempts = 1;
                PlaceObstacles(a, PcgRandom.For(a.seed, "layout", 0));
                a.connected = true;
                return;
            }
            bool ok = false;
            for (int attempt = 0; attempt < p.maxLayoutAttempts && !ok; attempt++)
            {
                a.layoutAttempts = attempt + 1;
                ClearLayout(a);
                PlaceObstacles(a, PcgRandom.For(a.seed, "layout", attempt));
                a.BuildNavMesh();
                ok = Validate(a, true);
            }
            // Repair: buang obstacle yang paling dekat dengan garis gerbang→generator yang gagal, sampai tersambung.
            int guard = 0;
            while (!ok && a.obstacles.Count > 0 && guard++ < 12)
            {
                PcgGate failing = null;
                foreach (var g in a.gates) if (a.gateReport.TryGetValue(g.lane, out var r) && !r.StartsWith("OK")) { failing = g; break; }
                Vector3 from = failing != null ? failing.Inner(4f) : a.playerSpawn;
                PcgPlaced worst = null; float best = float.MaxValue;
                foreach (var o in a.obstacles)
                {
                    float d = PcgArena.DistanceToSegment(o.position, from, a.generatorPos) - o.radius;
                    if (d < best) { best = d; worst = o; }
                }
                if (worst == null) break;
                a.obstacles.Remove(worst); a.obstacleCounts[worst.prefab]--;
                PcgArena.Discard(worst.go); a.repairRemovals++;
                a.BuildNavMesh();
                ok = Validate(a, false);
            }
            a.connected = ok;
        }

        static void ClearLayout(PcgArena a)
        {
            foreach (var o in a.obstacles) PcgArena.Discard(o.go);
            a.obstacles.Clear(); a.obstacleCounts.Clear();
            a.markers.RemoveAll(m => m.stage == 'O');
        }

        static void PlaceObstacles(PcgArena a, PcgRandom rng)
        {
            var p = a.profile;
            var weights = new List<float>();
            foreach (var e in p.obstacleTable) weights.Add(e != null && e.prefab != null ? e.weight : 0f);
            if (weights.Count == 0) return;
            float limit = a.half - p.wallMargin;

            for (int i = 0; i < p.obstacleCount; i++)
            {
                a.obstacleStats.requested++;
                bool placed = false;
                for (int attempt = 0; attempt < p.obstacleMaxAttempts && !placed; attempt++)
                {
                    a.obstacleStats.attempts++;
                    var entry = p.obstacleTable[Mathf.Max(0, rng.WeightedIndex(weights))];
                    Vector3 c = new Vector3(rng.Range(-limit, limit), a.groundY, rng.Range(-limit, limit));
                    float yaw = 90f * rng.Range(0, 4) + rng.Range(-10f, 10f);
                    Vector3 size = a.SizeOf(entry.prefab);
                    float r = 0.5f * Mathf.Sqrt(size.x * size.x + size.z * size.z);

                    PcgRule rule = PcgRule.Accepted;
                    // Rule 1: masih di dalam pagar
                    if (!a.InArena(c, p.wallMargin + r * 0.6f)) rule = PcgRule.OutsideArea;
                    // Rule 2: area generator tetap lapang (attack point)
                    else if (PcgArena.Flat(c - a.generatorPos).magnitude - r < p.generatorClearRadius) rule = PcgRule.NearGenerator;
                    // Rule 3: mulut gerbang tetap terbuka
                    else if (NearGate(a, c, r)) rule = PcgRule.NearGate;
                    // Rule 4: player safe zone
                    else if (PcgArena.Flat(c - a.playerSpawn).magnitude - r < p.playerClearRadius) rule = PcgRule.NearPlayer;
                    // Rule 5: minimum spacing (lorong untuk zombie & boss)
                    else foreach (var o in a.obstacles)
                        if (PcgArena.Flat(c - o.position).magnitude - r - o.radius < p.obstacleGap) { rule = PcgRule.Spacing; break; }

                    GameObject go = null;
                    if (rule == PcgRule.Accepted)
                    {
                        go = a.Place(entry.prefab, c, yaw, a.layoutRoot);
                        PcgArena.EnsureSolid(go);   // obstacle wajib padat untuk player & zombie
                        Physics.SyncTransforms();
                        // Rule 6: tidak menabrak pagar, lampu, menara, atau obstacle lain (OverlapBox + LayerMask)
                        if (a.Overlaps(go, PcgArena.Measure(go))) rule = PcgRule.Blocked;
                    }

                    a.Mark(c, rule, 'O');
                    if (rule != PcgRule.Accepted) { a.obstacleStats.Reject(rule); PcgArena.Discard(go); continue; }
                    a.obstacles.Add(new PcgPlaced { go = go, prefab = entry.prefab.name, position = c, radius = r });
                    a.obstacleCounts.TryGetValue(entry.prefab.name, out int n); a.obstacleCounts[entry.prefab.name] = n + 1;
                    a.obstacleStats.accepted++;
                    placed = true;
                }
            }
        }

        static bool NearGate(PcgArena a, Vector3 c, float r)
        {
            foreach (var g in a.gates)
                if (PcgArena.Flat(c - g.Inner(3f)).magnitude - r < a.profile.gateClearRadius) return true;
            return false;
        }

        /// <summary>Validasi tingkat layout: setiap gerbang punya jalur lengkap ke generator (zombie dan boss) tanpa memutar berlebihan.</summary>
        static bool Validate(PcgArena a, bool countStats)
        {
            bool ok = true;
            a.gateReport.Clear();
            foreach (var g in a.gates)
            {
                Vector3 start = g.Inner(4f);
                float straight = PcgArena.Flat(a.generatorPos - start).magnitude;
                float len, bossLen;
                string report;
                if (!a.PathToGenerator(start, a.normalFilter, out len)) { report = "NO PATH (zombie)"; if (countStats) a.obstacleStats.Reject(PcgRule.NoPath); ok = false; }
                else if (!a.PathToGenerator(start, a.bossFilter, out bossLen)) { report = "NO PATH (boss)"; if (countStats) a.obstacleStats.Reject(PcgRule.NoPath); ok = false; }
                else if (len > straight * a.profile.maxDetourRatio) { report = "DETOUR " + (len / straight).ToString("0.00") + "x"; if (countStats) a.obstacleStats.Reject(PcgRule.Detour); ok = false; }
                else report = "OK " + len.ToString("0") + " m (" + (len / Mathf.Max(1f, straight)).ToString("0.00") + "x)";
                a.gateReport[g.lane] = report;
            }
            float pl;
            if (!a.PathToGenerator(a.playerSpawn, a.normalFilter, out pl)) { a.gateReport[LaneId.East] = "player NO PATH"; ok = false; }
            return ok;
        }

        // ---------------------------------------------------------------- 5. dressing, spawn, attack, player

        static void BuildDressing(PcgArena a)
        {
            var p = a.profile;
            if (p.dressingCount <= 0 || p.dressingTable == null || p.dressingTable.Length == 0) return;
            var rng = PcgRandom.For(a.seed, "dressing");
            var root = new GameObject("Dressing (props kecil, padat)").transform; root.SetParent(a.root, false);
            a.dressingRoot = root;
            var weights = new List<float>();
            foreach (var e in p.dressingTable) weights.Add(e != null && e.prefab != null ? e.weight : 0f);
            float limit = a.half - 1.5f;
            for (int i = 0; i < p.dressingCount; i++)
            {
                a.dressingStats.requested++;
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    a.dressingStats.attempts++;
                    var entry = p.dressingTable[Mathf.Max(0, rng.WeightedIndex(weights))];
                    Vector3 c = new Vector3(rng.Range(-limit, limit), a.groundY, rng.Range(-limit, limit));
                    float yaw = rng.Range(0f, 360f), scale = rng.Range(p.dressingScale.x, p.dressingScale.y);
                    PcgRule rule = PcgRule.Accepted;
                    Vector3 onNav;
                    if (PcgArena.Flat(c - a.generatorPos).magnitude - a.generatorBounds.extents.magnitude * 0.5f < 4f) rule = PcgRule.NearGenerator;
                    else if (NearGate(a, c, 0.5f)) rule = PcgRule.NearGate;
                    else if (PcgArena.Flat(c - a.playerSpawn).magnitude < p.playerClearRadius) rule = PcgRule.NearPlayer;   // safe zone
                    else if (!a.preview && !a.SampleNav(c, 0.4f, a.normalFilter, out onNav)) rule = PcgRule.OffNavMesh;
                    // Props sekarang padat: sisakan celah ≥ 1 m dari obstacle/pagar supaya tidak membuat lorong buntu.
                    else if (a.Blocked(c, 1.0f)) rule = PcgRule.Blocked;
                    else foreach (var d in a.dressing)
                        if (PcgArena.Flat(d.transform.position - c).magnitude < p.dressingSpacing) { rule = PcgRule.Spacing; break; }
                    a.Mark(c, rule, 'D');
                    if (rule != PcgRule.Accepted) { a.dressingStats.Reject(rule); continue; }
                    var go = a.Place(entry.prefab, c, yaw, root, scale);
                    PcgArena.EnsureSolid(go);
                    Physics.SyncTransforms();
                    a.dressing.Add(go);
                    a.dressingStats.accepted++;
                    break;
                }
            }
        }

        /// <summary>Props ikut memblokir: NavMesh di-bake ulang dan jalur divalidasi lagi; props yang memutus jalur dibuang.</summary>
        static void RevalidateWithDressing(PcgArena a)
        {
            if (a.preview) return;
            a.BuildNavMesh();
            bool ok = Validate(a, false);
            int guard = 0;
            while (!ok && a.dressing.Count > 0 && guard++ < 15)
            {
                PcgGate failing = null;
                foreach (var g in a.gates) if (a.gateReport.TryGetValue(g.lane, out var r) && !r.StartsWith("OK")) { failing = g; break; }
                Vector3 from = failing != null ? failing.Inner(4f) : a.playerSpawn;
                GameObject worst = null; float best = float.MaxValue;
                foreach (var d in a.dressing)
                {
                    float dist = PcgArena.DistanceToSegment(d.transform.position, from, a.generatorPos);
                    if (dist < best) { best = dist; worst = d; }
                }
                if (worst == null) break;
                a.dressing.Remove(worst); a.dressingStats.accepted--;
                PcgArena.Discard(worst);
                a.BuildNavMesh();
                ok = Validate(a, false);
            }
            a.connected = a.connected && ok;
        }

        /// <summary>Cek: setiap obstacle dan props punya collider padat (tidak bisa ditembus).</summary>
        static void LogSolidCheck(PcgArena a)
        {
            int solidObs = 0, solidProps = 0;
            foreach (var o in a.obstacles) if (HasSolid(o.go)) solidObs++;
            foreach (var d in a.dressing) if (HasSolid(d)) solidProps++;
            Debug.Log("[PCG] Solid check: obstacle " + solidObs + "/" + a.obstacles.Count + ", props " + solidProps + "/" + a.dressing.Count +
                      " punya collider | collider ditambahkan " + PcgArena.addedColliders + " | jalur setelah props: " + (a.connected ? "OK" : "GAGAL"));
        }

        static bool HasSolid(GameObject go)
        {
            if (go == null) return false;
            foreach (var c in go.GetComponentsInChildren<Collider>()) if (c.enabled && !c.isTrigger) return true;
            return false;
        }

        /// <summary>
        /// Penanda safe zone player: garis lingkaran hijau bercahaya yang menyambung di tepi radius playerClearRadius.
        /// Garis = LineRenderer tipis menempel di lantai (penanda visual, bukan objek/obstacle; tanpa collider).
        /// Inti garis memakai warna HDR sehingga terkena bloom post-processing map industrial; ditambah lapisan glow additive yang lebih lebar.
        /// </summary>
        static void BuildSafeZone(PcgArena a)
        {
            var p = a.profile;
            var root = new GameObject("Safe Zone (garis hijau)").transform; root.SetParent(a.root, false);
            Ring(root, a, "Safe zone ring (core)", p.safeZoneLineWidth, p.safeZoneColor * p.safeZoneGlow, false, 0.05f);
            Ring(root, a, "Safe zone ring (glow)", p.safeZoneLineWidth * 4f, p.safeZoneColor * 0.4f, true, 0.04f);
        }

        static void Ring(Transform parent, PcgArena a, string name, float width, Color color, bool additive, float height)
        {
            var p = a.profile;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // TransformZ menghadap ke atas → garis rebah di lantai
            var lr = go.AddComponent<LineRenderer>();
            const int segments = 128;
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = segments;
            lr.alignment = LineAlignment.TransformZ;
            lr.widthMultiplier = width;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            for (int i = 0; i < segments; i++)
            {
                float t = i * Mathf.PI * 2f / segments;
                Vector3 pt = a.playerSpawn + new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t)) * p.playerClearRadius;
                // Ikuti tinggi lantai bila tidak rata.
                RaycastHit hit;
                if (Physics.Raycast(pt + Vector3.up * 3f, Vector3.down, out hit, 6f, a.probeMask, QueryTriggerInteraction.Ignore) && hit.collider.transform.IsChildOf(a.root))
                    pt.y = hit.point.y;
                else pt.y = a.groundY;
                lr.SetPosition(i, pt + Vector3.up * height);
            }
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = name + " material" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);   // terlihat dari atas maupun bawah
            if (additive && mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 2f);   // additive
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            lr.sharedMaterial = mat;
            lr.startColor = lr.endColor = Color.white;
        }

        const int SpawnPerGate = 7;

        static void PlaceSpawnPoints(PcgArena a)
        {
            var p = a.profile;
            foreach (var g in a.gates)
            {
                var rng = PcgRandom.For(a.seed, "spawn", (int)g.lane);
                var list = new List<Vector3>();
                for (int k = 0; k < SpawnPerGate; k++)
                {
                    a.spawnStats.requested++;
                    bool ok = false;
                    for (int attempt = 0; attempt < 40 && !ok; attempt++)
                    {
                        a.spawnStats.attempts++;
                        Vector3 c = g.Inner(rng.Range(1.5f, p.spawnZoneDepth)) + g.along * rng.Range(-g.halfWidth + 1f, g.halfWidth - 1f);
                        Vector3 onNav; float len;
                        PcgRule rule = PcgRule.Accepted;
                        if (!a.SampleNav(c, 0.8f, a.normalFilter, out onNav)) rule = PcgRule.OffNavMesh;
                        // Rule player safe distance (minDistanceFromPlayer di modul): zombie tidak boleh muncul dekat player.
                        else if (PcgArena.Flat(onNav - a.playerSpawn).magnitude < p.minEnemySpawnDistance) rule = PcgRule.NearPlayer;
                        else if (a.Blocked(onNav, 0.45f)) rule = PcgRule.Blocked;
                        else
                        {
                            foreach (var u in list) if (PcgArena.Flat(u - onNav).magnitude < p.spawnSpacing) { rule = PcgRule.Spacing; break; }
                            if (rule == PcgRule.Accepted && !a.PathToGenerator(onNav, a.normalFilter, out len)) rule = PcgRule.NoPath;
                        }
                        a.Mark(onNav, rule, 'S');
                        if (rule != PcgRule.Accepted) { a.spawnStats.Reject(rule); continue; }
                        list.Add(onNav); a.spawnStats.accepted++; ok = true;
                    }
                    if (!ok) { list.Add(g.Inner(3f + k * 0.9f)); a.spawnStats.fallback++; }
                }
                a.spawnPositions[g.lane] = list;
            }
        }

        static void PlaceAttackPoints(PcgArena a)
        {
            a.attackPositions.Clear();
            foreach (var t in a.GeneratorRing(6, 1.3f))
            {
                Vector3 onNav;
                a.attackPositions.Add(a.SampleNav(t, 2f, a.normalFilter, out onNav) ? onNav : t);
            }
            Vector3 ps;
            if (a.SampleNav(a.playerSpawn, 3f, a.normalFilter, out ps)) a.playerSpawn = ps;
        }
    }
}
