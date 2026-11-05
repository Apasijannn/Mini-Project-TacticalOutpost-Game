using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Outpost.Dungeon
{
    /// <summary>
    /// Eksperimen otomatis modul §102–§108 dan §126 (F9 di mode Dungeon). Generation grid dijalankan berkali-kali dengan
    /// parameter persis seperti di modul; eksperimen ukuran dungeon juga membangun scene-nya untuk menghitung GameObject dan waktu build.
    /// Hasil: Docs/Dungeon/ExperimentResults.md.
    /// </summary>
    public static class DungeonExperiments
    {
        public static string Run(DungeonSession s)
        {
            var baseProfile = s.Profile;
            var sb = new StringBuilder();
            float prevScale = Time.timeScale; Time.timeScale = 0f;
            var helper = DungeonBuilder.CreateHelper(s.Context);
            float tile = DungeonBuilder.ResolveTile(baseProfile, helper);
            var allowed = DungeonBuilder.PropAllowed(baseProfile, helper, tile);

            System.Func<System.Action<DungeonProfile>, DungeonProfile> With = tweak =>
            {
                var p = Object.Instantiate(baseProfile);
                p.skipCorridorChance = 0f; p.algorithm = DungeonAlgorithm.BSP; p.goalStrategy = GoalStrategy.C_PathFarthest;
                p.mapWidth = 60; p.mapHeight = 40; p.minLeafSize = 12; p.maxLeafSize = 20; p.minRoomSize = 5; p.roomMargin = 2;
                p.enemyCount = 8; p.itemCount = 5; p.useEnemyDensity = false; p.corridorWidth = 1; p.relayCount = 3;
                tweak(p);
                return p;
            };
            System.Func<DungeonProfile, int, DungeonLayout> Gen = (p, seed) => DungeonGenerator.Generate(p, seed, p.algorithm, allowed);

            sb.AppendLine("# Dungeon Experiment Results — Tactical Outpost (mode Procedural Dungeon, Modul 10)");
            sb.AppendLine();
            sb.AppendLine("Dibuat otomatis (F9) " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ". Default modul: map 60×40, minLeafSize 12, maxLeafSize 20, minRoomSize 5, " +
                          "roomMargin 2, enemyCount 8, itemCount 5, safeRoomCount 1, goal strategi C. 1 cell = " + tile.ToString("0.00") + " m di dunia.");
            sb.AppendLine();

            // ------------------------------------------------------------ Tugas eksperimen §126
            sb.AppendLine("## Tugas Eksperimen — 5 generation (seed berbeda)");
            sb.AppendLine();
            sb.AppendLine("| Seed | Rooms | Enemy | Item | Start–Goal Distance (BFS cell) | Valid | Attempt | Repair | Floor | Corridor cell | Layout hash |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---|---:|---:|---:|---:|---:|");
            var def = With(p => { });
            foreach (int seed in new[] { 101, 202, 303, 404, 505 })
            {
                var L = Gen(def, seed);
                sb.AppendLine("| " + seed + " | " + L.rooms.Count + " | " + L.Count(PlaceKind.Enemy) + (L.Count(PlaceKind.Boss) > 0 ? " + boss" : "") + " | " + L.Count(PlaceKind.Item) +
                              " | " + L.startGoalPathLength + " | " + (L.valid ? "Yes" : "No") + " | " + L.attempts + " | " + L.repairs + " | " + L.floorCount + " | " + L.corridorLength + " | " + L.LayoutHash() + " |");
            }
            sb.AppendLine();

            // ------------------------------------------------------------ Eksperimen 1 — Seed
            sb.AppendLine("## Eksperimen 1 — Seed (100–500)");
            sb.AppendLine();
            sb.AppendLine("| Seed | Rooms | Ukuran room (min / rata-rata / max cell) | Panjang corridor (cell) | Posisi Goal | Start–Goal BFS |");
            sb.AppendLine("|---:|---:|---|---:|---|---:|");
            foreach (int seed in new[] { 100, 200, 300, 400, 500 })
            {
                var L = Gen(def, seed);
                var areas = L.rooms.Select(r => r.Area).ToList();
                sb.AppendLine("| " + seed + " | " + L.rooms.Count + " | " + (areas.Count > 0 ? areas.Min() + " / " + areas.Average().ToString("0") + " / " + areas.Max() : "-") +
                              " | " + L.corridorLength + " | " + L.goalPosition + " | " + L.startGoalPathLength + " |");
            }
            sb.AppendLine();

            // ------------------------------------------------------------ Reproducibility
            sb.AppendLine("## Uji reproducibility (seed 500 × 3)");
            sb.AppendLine();
            var hashes = new List<int>();
            for (int i = 0; i < 3; i++) hashes.Add(Gen(def, 500).LayoutHash());
            sb.AppendLine("Hash: " + string.Join(", ", hashes.Select(h => h.ToString()).ToArray()) + " → " + (hashes.Distinct().Count() == 1 ? "**identik** (seed sama = dungeon sama)." : "**berbeda** (ada random di luar PcgRandom)."));
            sb.AppendLine();

            // ------------------------------------------------------------ Eksperimen 2 — Min Leaf Size
            sb.AppendLine("## Eksperimen 2 — minLeafSize (rata-rata seed 100–500)");
            sb.AppendLine();
            sb.AppendLine("| minLeafSize | maxLeafSize | Leaf BSP | Room | Rata-rata luas room (cell) | Floor % | Valid |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---:|---|");
            foreach (int leaf in new[] { 8, 12, 16 })
            {
                // maxLeafSize harus ≥ minLeafSize; untuk 16 dinaikkan ke 24 supaya split tetap mungkin.
                int maxLeaf = Mathf.Max(20, leaf + 8);
                var p = With(x => { x.minLeafSize = leaf; x.maxLeafSize = maxLeaf; });
                AppendAverages(sb, "| " + leaf + " | " + maxLeaf, p, Gen, L => new float[] { L.leafNodes.Count, L.rooms.Count, L.rooms.Count > 0 ? (float)L.rooms.Average(r => r.Area) : 0f, L.FloorPercent });
            }
            sb.AppendLine();

            // ------------------------------------------------------------ Eksperimen 3 — Room Margin
            sb.AppendLine("## Eksperimen 3 — roomMargin (rata-rata seed 100–500)");
            sb.AppendLine();
            sb.AppendLine("| roomMargin | Room | Jarak room → batas leaf (cell, rata-rata) | Rata-rata luas room | Floor % | Valid |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---|");
            foreach (int margin in new[] { 1, 2, 4 })
            {
                var p = With(x => x.roomMargin = margin);
                AppendAverages(sb, "| " + margin, p, Gen, L => new float[] { L.rooms.Count, LeafGap(L), L.rooms.Count > 0 ? (float)L.rooms.Average(r => r.Area) : 0f, L.FloorPercent });
            }
            sb.AppendLine();

            // ------------------------------------------------------------ Eksperimen 4 — Ukuran dungeon (dibangun ke scene)
            sb.AppendLine("## Eksperimen 4 — Ukuran dungeon (seed 500, dibangun ke scene tanpa NavMesh)");
            sb.AppendLine();
            sb.AppendLine("| Ukuran | Jumlah tile | Floor tile | Wall terlihat (cell) | GameObject | Waktu generation (ms) | Waktu build (ms) | Ukuran dunia (m) |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---|");
            var previewCtx = new DungeonBuildContext
            {
                generatorTemplate = s.Context.generatorTemplate, lampTemplate = s.Context.lampTemplate, surfaces = s.Context.surfaces,
                normalFilter = s.Context.normalFilter, bossFilter = s.Context.bossFilter, blockMask = s.Context.blockMask, probeMask = s.Context.probeMask, preview = true,
            };
            foreach (var size in new[] { new Vector2Int(40, 30), new Vector2Int(60, 40), new Vector2Int(80, 60) })
            {
                var p = With(x => { x.mapWidth = size.x; x.mapHeight = size.y; });
                var L = Gen(p, 500);
                var world = DungeonBuilder.Build(L, p, previewCtx, DungeonBuilder.CreateHelper(previewCtx), "EXPERIMENT " + size);
                world.root.position += Vector3.down * 400f;
                sb.AppendLine("| " + size.x + " × " + size.y + " | " + size.x * size.y + " | " + L.floorCount + " | " + L.visibleWallCount + " | " + world.gameObjectCount +
                              " | " + L.generationMs.ToString("0.0") + " | " + world.buildMs.ToString("0") + " | " + (size.x * world.tile).ToString("0") + " × " + (size.y * world.tile).ToString("0") + " |");
                world.root.gameObject.SetActive(false);
                Object.Destroy(world.root.gameObject);
                Object.Destroy(p);
            }
            sb.AppendLine();

            // ------------------------------------------------------------ Eksperimen 5 — Enemy density
            sb.AppendLine("## Eksperimen 5 — Enemy density (seed 500)");
            sb.AppendLine();
            sb.AppendLine("| Mode | Map | Floor | Enemy | Floor per enemy | Ditolak (alasan) |");
            sb.AppendLine("|---|---|---:|---:|---:|---|");
            foreach (int count in new[] { 5, 10, 20 })
            {
                var L = Gen(With(x => x.enemyCount = count), 500);
                int e = L.Count(PlaceKind.Enemy);
                sb.AppendLine("| Absolut " + count + " | 60×40 | " + L.floorCount + " | " + e + " | " + (L.floorCount / (float)Mathf.Max(1, e)).ToString("0") + " | " + L.enemyStats.Summary() + " |");
            }
            foreach (var size in new[] { new Vector2Int(40, 30), new Vector2Int(60, 40), new Vector2Int(80, 60) })
            {
                var L = Gen(With(x => { x.mapWidth = size.x; x.mapHeight = size.y; x.useEnemyDensity = true; x.floorTilesPerEnemy = 50; }), 500);
                int e = L.Count(PlaceKind.Enemy);
                sb.AppendLine("| Density 1/50 floor | " + size.x + "×" + size.y + " | " + L.floorCount + " | " + e + " | " + (L.floorCount / (float)Mathf.Max(1, e)).ToString("0") + " | " + L.enemyStats.Summary() + " |");
            }
            sb.AppendLine();

            // ------------------------------------------------------------ Eksperimen 6 — Start/Goal
            sb.AppendLine("## Eksperimen 6 — Strategi Start/Goal (BFS path length, cell)");
            sb.AppendLine();
            sb.AppendLine("| Seed | A: First → Last | B: Geometric farthest | C: Path farthest |");
            sb.AppendLine("|---:|---:|---:|---:|");
            var totals = new float[3];
            foreach (int seed in new[] { 101, 202, 303, 404, 505 })
            {
                var row = new List<int>();
                int i = 0;
                foreach (var g in new[] { GoalStrategy.A_FirstToLast, GoalStrategy.B_GeometricFarthest, GoalStrategy.C_PathFarthest })
                {
                    var L = Gen(With(x => x.goalStrategy = g), seed);
                    row.Add(L.startGoalPathLength); totals[i++] += L.startGoalPathLength;
                }
                sb.AppendLine("| " + seed + " | " + string.Join(" | ", row.Select(v => v.ToString()).ToArray()) + " |");
            }
            sb.AppendLine("| **rata-rata** | " + string.Join(" | ", totals.Select(v => (v / 5f).ToString("0.0")).ToArray()) + " |");
            sb.AppendLine();

            // ------------------------------------------------------------ Eksperimen 7 — BSP vs Random Walk
            sb.AppendLine("## Eksperimen 7 — BSP vs Random Walk (seed 100–500, rata-rata)");
            sb.AppendLine();
            sb.AppendLine("| Algoritma | Room | Floor % | Dead-end | Corridor/cave cell | Start–Goal BFS | Valid |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---|");
            foreach (var algo in new[] { DungeonAlgorithm.BSP, DungeonAlgorithm.RandomWalk })
            {
                var p = With(x => x.algorithm = algo);
                AppendAverages(sb, "| " + algo, p, Gen, L => new float[] { L.rooms.Count, L.FloorPercent, L.deadEnds, L.corridorLength, L.startGoalPathLength });
            }
            sb.AppendLine();

            // ------------------------------------------------------------ Eksperimen 8 — Validasi & repair
            sb.AppendLine("## Eksperimen 8 — Validasi BFS + repair (corridor sengaja dilewati, seed 100–500)");
            sb.AppendLine();
            sb.AppendLine("| skipCorridorChance | Corridor dilewati | Repair | Attempt | Valid | Contoh log |");
            sb.AppendLine("|---:|---:|---:|---:|---|---|");
            foreach (float chance in new[] { 0f, 0.3f, 0.6f })
            {
                int skipped = 0, repairs = 0, attempts = 0, valid = 0; string example = "-";
                foreach (int seed in new[] { 100, 200, 300, 400, 500 })
                {
                    var L = Gen(With(x => x.skipCorridorChance = chance), seed);
                    skipped += L.corridorsSkipped; repairs += L.repairs; attempts += L.attempts; if (L.valid) valid++;
                    if (L.repairs > 0 && example == "-" && L.attemptLog.Count > 0) example = "seed " + seed + ": " + L.attemptLog[L.attemptLog.Count - 1];
                }
                sb.AppendLine("| " + chance.ToString("0.0") + " | " + skipped + " | " + repairs + " | " + attempts + " | " + valid + "/5 | " + example + " |");
            }
            sb.AppendLine();

            // ------------------------------------------------------------ Corridor width
            sb.AppendLine("## Pengembangan — Corridor width (seed 100–500, rata-rata)");
            sb.AppendLine();
            sb.AppendLine("| corridorWidth | Corridor cell | Floor % | Dead-end | Valid |");
            sb.AppendLine("|---:|---:|---:|---:|---|");
            foreach (int width in new[] { 1, 2 })
                AppendAverages(sb, "| " + width, With(x => x.corridorWidth = width), Gen, L => new float[] { L.corridorLength, L.FloorPercent, L.deadEnds });
            sb.AppendLine();

            // ------------------------------------------------------------ Objektif: power relay
            sb.AppendLine("## Eksperimen 9 — Power relay (lock-and-key): jumlah relay vs panjang rute (seed 100–500, rata-rata)");
            sb.AppendLine();
            sb.AppendLine("Rute = BFS Start → relay terdekat berikutnya (greedy) → … → Goal. Relay wajib terjangkau tanpa melewati Goal (hard constraint).");
            sb.AppendLine();
            sb.AppendLine("| relayCount | Relay ditempatkan | Di luar critical path | Penjaga | Rute langsung Start→Goal | Rute dengan relay | Rasio | Valid |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---:|---:|---|");
            foreach (int n in new[] { 0, 1, 3, 5 })
            {
                float placed = 0, off = 0, guards = 0, direct = 0, route = 0; int valid = 0;
                foreach (int seed in new[] { 100, 200, 300, 400, 500 })
                {
                    var L = Gen(With(x => x.relayCount = n), seed);
                    placed += L.Count(PlaceKind.Relay); off += L.relaysOffPath; guards += L.relayGuards;
                    direct += L.startGoalPathLength; route += Mathf.Max(0, DungeonGenerator.RouteLength(L)); if (L.valid) valid++;
                }
                sb.AppendLine("| " + n + " | " + (placed / 5f).ToString("0.0") + " | " + (off / 5f).ToString("0.0") + " | " + (guards / 5f).ToString("0.0") + " | " + (direct / 5f).ToString("0.0") +
                              " | " + (route / 5f).ToString("0.0") + " | " + (route / Mathf.Max(1f, direct)).ToString("0.00") + "× | " + valid + "/5 |");
            }
            sb.AppendLine();

            // ------------------------------------------------------------ Objektif: clear room
            sb.AppendLine("## Eksperimen 10 — Clear room (default, relay 3)");
            sb.AppendLine();
            sb.AppendLine("| Seed | Room | Combat room (dikunci saat dimasuki) | Pintu (bukaan room) | Zombie di combat room | Zombie di corridor / room lain |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---:|");
            foreach (int seed in new[] { 101, 202, 303, 404, 505 })
            {
                var L = Gen(def, seed);
                var combat = DungeonGenerator.CombatRooms(L);
                int doors = 0; foreach (int r in combat) doors += DungeonGenerator.DoorEdges(L, r).Count;
                int inside = 0, outside = 0;
                foreach (var q in L.placements) if (q.kind == PlaceKind.Enemy) { if (combat.Contains(q.room)) inside++; else outside++; }
                sb.AppendLine("| " + seed + " | " + L.rooms.Count + " | " + combat.Count + " | " + doors + " | " + inside + " | " + outside + " |");
            }
            sb.AppendLine();

            // ------------------------------------------------------------ Dungeon aktif
            sb.AppendLine("## Dungeon yang sedang dimainkan (seed " + s.Seed + ", " + s.Algorithm + ")");
            sb.AppendLine();
            var cur = s.Layout;
            sb.AppendLine("| Metric | Nilai |"); sb.AppendLine("|---|---:|");
            sb.AppendLine("| Room Count | " + cur.rooms.Count + " |");
            sb.AppendLine("| Floor Count | " + cur.floorCount + " |");
            sb.AppendLine("| Wall Count (wall terlihat) | " + cur.visibleWallCount + " |");
            sb.AppendLine("| Corridor Length | " + cur.corridorLength + " |");
            sb.AppendLine("| Start–Goal Distance (BFS / lurus) | " + cur.startGoalPathLength + " / " + cur.startGoalEuclid.ToString("0.0") + " |");
            sb.AppendLine("| Enemy Count (+ boss) | " + cur.Count(PlaceKind.Enemy) + " + " + cur.Count(PlaceKind.Boss) + " |");
            sb.AppendLine("| Item Count | " + cur.Count(PlaceKind.Item) + " |");
            sb.AppendLine("| Relay (off-path / penjaga) | " + cur.Count(PlaceKind.Relay) + " (" + cur.relaysOffPath + " / " + cur.relayGuards + ") |");
            sb.AppendLine("| Rute Start → relay → Goal (BFS) | " + DungeonGenerator.RouteLength(cur) + " |");
            if (s.Objectives != null) sb.AppendLine("| Combat room / pintu | " + s.Objectives.locks.Count + " / " + s.Objectives.locks.Sum(x => x.doors.Count) + " |");
            sb.AppendLine("| Floor Percentage | " + cur.FloorPercent.ToString("0.0") + "% |");
            sb.AppendLine("| GameObject | " + s.World.gameObjectCount + " |");
            sb.AppendLine("| NavMesh | " + s.World.navReport + " |");
            sb.AppendLine();
            sb.AppendLine("Constraint:");
            sb.AppendLine();
            foreach (var c in cur.checks) sb.AppendLine("- " + (c.pass ? "✅" : "❌") + " " + (c.hard ? "**hard**" : "soft") + " — " + c.name + (string.IsNullOrEmpty(c.detail) ? "" : " (" + c.detail + ")"));
            sb.AppendLine();
            sb.AppendLine("Peta ASCII (`#` wall, `.` floor, `S` start, `G` goal, `E` enemy, `B` boss, `i` item, `R` relay, `o` props):");
            sb.AppendLine();
            sb.AppendLine("```text");
            sb.Append(DungeonGenerator.ToAscii(cur));
            sb.AppendLine("```");

            Object.Destroy(def);
            Time.timeScale = prevScale;
            string dir = Application.isEditor ? System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Docs", "Dungeon")) : Application.persistentDataPath;
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, "ExperimentResults.md");
            System.IO.File.WriteAllText(path, sb.ToString());
            Debug.Log("[DUNGEON] Eksperimen selesai: " + path);
            return path;
        }

        static void AppendAverages(StringBuilder sb, string head, DungeonProfile p, System.Func<DungeonProfile, int, DungeonLayout> gen, System.Func<DungeonLayout, float[]> metrics)
        {
            float[] sum = null; int valid = 0;
            foreach (int seed in new[] { 100, 200, 300, 400, 500 })
            {
                var L = gen(p, seed);
                var m = metrics(L);
                if (sum == null) sum = new float[m.Length];
                for (int i = 0; i < m.Length; i++) sum[i] += m[i];
                if (L.valid) valid++;
            }
            sb.AppendLine(head + " | " + string.Join(" | ", sum.Select(v => (v / 5f).ToString("0.0")).ToArray()) + " | " + valid + "/5 |");
            Object.Destroy(p);
        }

        /// <summary>Rata-rata jarak tepi room ke tepi leaf-nya (bukti roomMargin bekerja).</summary>
        static float LeafGap(DungeonLayout L)
        {
            float sum = 0f; int n = 0;
            foreach (var leaf in L.leafNodes)
            {
                if (leaf.room == null) continue;
                var r = leaf.room;
                sum += Mathf.Min(Mathf.Min(r.x - leaf.x, leaf.x + leaf.width - (r.x + r.width)), Mathf.Min(r.y - leaf.y, leaf.y + leaf.height - (r.y + r.height)));
                n++;
            }
            return n > 0 ? sum / n : 0f;
        }
    }
}
