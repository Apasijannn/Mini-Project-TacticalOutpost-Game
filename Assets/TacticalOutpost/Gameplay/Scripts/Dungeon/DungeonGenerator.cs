using System.Collections.Generic;
using UnityEngine;
using Outpost.PCG;

namespace Outpost.Dungeon
{
    /// <summary>
    /// Generator dungeon berbasis grid (Modul Praktikum 10). Murni data (tanpa GameObject), sehingga bisa diuji ribuan kali
    /// untuk eksperimen dan baru dibangun menjadi scene oleh DungeonBuilder setelah lolos validasi.
    ///
    /// Pipeline (generate-and-test, modul §96 dan §119):
    ///   Initialize Grid → BSP (atau Random Walk) → Room → Corridor L → Start/Goal
    ///   → Validasi hard constraint (BFS) → Repair (carve corridor ke area terputus) → bila tetap gagal: generate ulang
    ///   → Placement props/enemy/boss/item dengan constraint → Validasi akhir (BFS dengan props sebagai penghalang)
    ///   → Repair (buang props yang memutus jalur) → Metrics.
    /// Semua random memakai PcgRandom (System.Random per subsistem), bukan UnityEngine.Random, sehingga seed sama → dungeon sama.
    /// </summary>
    public static class DungeonGenerator
    {
        const int GoalClear = 1;   // footprint generator goal = 3x3 cell di sekitar Goal

        public static DungeonLayout Generate(DungeonProfile p, int seed, bool[] propAllowed = null)
        {
            return Generate(p, seed, p.algorithm, propAllowed);
        }

        public static DungeonLayout Generate(DungeonProfile p, int seed, DungeonAlgorithm algorithm, bool[] propAllowed = null)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            DungeonLayout last = null;
            var log = new List<string>();
            int max = Mathf.Max(1, p.maxGenerationAttempts);
            for (int attempt = 0; attempt < max; attempt++)
            {
                var L = new DungeonLayout(p.mapWidth, p.mapHeight) { seed = seed, algorithm = algorithm, goalStrategy = p.goalStrategy };
                var rng = PcgRandom.For(seed, "layout", attempt);

                // 1. Layout
                if (algorithm == DungeonAlgorithm.BSP)
                {
                    CreateBSP(L, p, rng);
                    CreateRooms(L, p, rng);
                    CreateCorridors(L, p, rng);
                    AddLoops(L, p, rng);
                }
                else RandomWalk(L, p, rng);

                // 2. Start / Goal
                PlaceStartAndGoal(L, p);

                // 3. Validasi + repair layout
                string reason;
                bool ok = ValidateLayout(L, p, out reason);
                for (int r = 0; !ok && r < 6; r++)
                {
                    if (!RepairLayout(L, p)) break;
                    L.repairs++;
                    PlaceStartAndGoal(L, p);
                    ok = ValidateLayout(L, p, out reason);
                }
                if (!ok)
                {
                    log.Add("attempt " + (attempt + 1) + ": GAGAL (" + reason + ") setelah " + L.repairs + " repair → generate ulang");
                    L.attempts = attempt + 1; L.failReason = reason; last = L;
                    continue;
                }

                // 4. Placement + validasi akhir
                Populate(L, p, seed, attempt, propAllowed);
                ok = FinalValidate(L, p, out reason);
                L.attempts = attempt + 1;
                log.Add("attempt " + (attempt + 1) + ": " + (ok ? "VALID" : "GAGAL (" + reason + ")") +
                        (L.repairs > 0 ? " | repair corridor " + L.repairs : "") + (L.propRemovals > 0 ? " | props dibuang " + L.propRemovals : ""));
                L.valid = ok; L.failReason = ok ? "" : reason;
                last = L;
                if (ok) break;
            }
            last.attemptLog.AddRange(log);
            if (last.checks.Count == 0) { ComputeDistances(last, true); FindPath(last, last.startPosition, last.goalPosition, last.criticalPath, true); BuildChecks(last, p); }
            ComputeMetrics(last);
            last.generationMs = (float)watch.Elapsed.TotalMilliseconds;
            return last;
        }

        // ================================================================ BSP (modul §48–§53)

        static void CreateBSP(DungeonLayout L, DungeonProfile p, PcgRandom rng)
        {
            L.root = new BSPNode(0, 0, L.width, L.height);
            SplitNode(L, p, rng, L.root);
        }

        static void SplitNode(DungeonLayout L, DungeonProfile p, PcgRandom rng, BSPNode node)
        {
            if (node.width <= p.maxLeafSize && node.height <= p.maxLeafSize) { L.leafNodes.Add(node); return; }
            bool splitHorizontal;
            if (node.width > node.height * 1.25f) splitHorizontal = false;          // terlalu lebar → potong vertical
            else if (node.height > node.width * 1.25f) splitHorizontal = true;      // terlalu tinggi → potong horizontal
            else splitHorizontal = rng.Value > 0.5f;
            if (splitHorizontal) SplitHorizontal(L, p, rng, node); else SplitVertical(L, p, rng, node);
        }

        static void SplitHorizontal(DungeonLayout L, DungeonProfile p, PcgRandom rng, BSPNode node)
        {
            if (node.height < p.minLeafSize * 2) { L.leafNodes.Add(node); return; }
            int split = rng.Range(p.minLeafSize, node.height - p.minLeafSize);
            node.left = new BSPNode(node.x, node.y, node.width, split, node.depth + 1);
            node.right = new BSPNode(node.x, node.y + split, node.width, node.height - split, node.depth + 1);
            SplitNode(L, p, rng, node.left);
            SplitNode(L, p, rng, node.right);
        }

        static void SplitVertical(DungeonLayout L, DungeonProfile p, PcgRandom rng, BSPNode node)
        {
            if (node.width < p.minLeafSize * 2) { L.leafNodes.Add(node); return; }
            int split = rng.Range(p.minLeafSize, node.width - p.minLeafSize);
            node.left = new BSPNode(node.x, node.y, split, node.height, node.depth + 1);
            node.right = new BSPNode(node.x + split, node.y, node.width - split, node.height, node.depth + 1);
            SplitNode(L, p, rng, node.left);
            SplitNode(L, p, rng, node.right);
        }

        // ================================================================ Room (modul §54–§55)

        static void CreateRooms(DungeonLayout L, DungeonProfile p, PcgRandom rng)
        {
            L.rooms.Clear();
            foreach (var leaf in L.leafNodes)
            {
                int maxWidth = leaf.width - p.roomMargin * 2;
                int maxHeight = leaf.height - p.roomMargin * 2;
                if (maxWidth < p.minRoomSize || maxHeight < p.minRoomSize) continue;   // leaf terlalu kecil: tanpa room
                int roomWidth = rng.Range(p.minRoomSize, maxWidth + 1);
                int roomHeight = rng.Range(p.minRoomSize, maxHeight + 1);
                int maxXOffset = leaf.width - roomWidth - p.roomMargin;
                int maxYOffset = leaf.height - roomHeight - p.roomMargin;
                int roomX = leaf.x + rng.Range(p.roomMargin, maxXOffset + 1);
                int roomY = leaf.y + rng.Range(p.roomMargin, maxYOffset + 1);
                var room = new DungeonRoom(roomX, roomY, roomWidth, roomHeight) { index = L.rooms.Count };
                leaf.room = room;
                L.rooms.Add(room);
                CarveRoom(L, room);
            }
        }

        static void CarveRoom(DungeonLayout L, DungeonRoom room)
        {
            for (int x = room.x; x < room.x + room.width; x++)
                for (int y = room.y; y < room.y + room.height; y++)
                    if (L.IsInsideGrid(x, y)) { L.grid[x, y] = TileType.Floor; L.roomId[x, y] = room.index; L.corridor[x, y] = false; }
        }

        // ================================================================ Corridor (modul §57–§62, §115)

        /// <summary>
        /// Versi BSP: setiap node internal menghubungkan room terdekat di subtree kiri dengan subtree kanan.
        /// Karena setiap pasangan sibling tersambung, seluruh room pasti terhubung (kecuali corridor sengaja dilewati untuk uji repair).
        /// </summary>
        static void CreateCorridors(DungeonLayout L, DungeonProfile p, PcgRandom rng)
        {
            var connected = new HashSet<long>();
            Connect(L, p, rng, L.root, connected);
            pairs = connected;
        }

        static HashSet<long> pairs;

        static List<DungeonRoom> Connect(DungeonLayout L, DungeonProfile p, PcgRandom rng, BSPNode node, HashSet<long> connected)
        {
            var result = new List<DungeonRoom>();
            if (node == null) return result;
            if (node.IsLeaf) { if (node.room != null) result.Add(node.room); return result; }
            var a = Connect(L, p, rng, node.left, connected);
            var b = Connect(L, p, rng, node.right, connected);
            if (a.Count > 0 && b.Count > 0)
            {
                DungeonRoom ra = null, rb = null; float best = float.MaxValue;
                foreach (var x in a) foreach (var y in b)
                {
                    float d = Vector2Int.Distance(x.Center, y.Center);
                    if (d < best) { best = d; ra = x; rb = y; }
                }
                if (p.skipCorridorChance > 0f && rng.Value < p.skipCorridorChance) L.corridorsSkipped++;
                else { CreateLCorridor(L, p, rng, ra.Center, rb.Center); connected.Add(Key(ra.index, rb.index)); }
            }
            result.AddRange(a); result.AddRange(b);
            return result;
        }

        static long Key(int a, int b) { return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; }

        /// <summary>Corridor tambahan ke room terdekat yang belum terhubung langsung (loop, supaya tidak terlalu linear).</summary>
        static void AddLoops(DungeonLayout L, DungeonProfile p, PcgRandom rng)
        {
            if (p.extraLoops <= 0 || L.rooms.Count < 3) return;
            var connected = pairs ?? new HashSet<long>();
            for (int k = 0; k < p.extraLoops; k++)
            {
                var candidates = new List<KeyValuePair<float, Vector2Int>>();
                for (int i = 0; i < L.rooms.Count; i++)
                    for (int j = i + 1; j < L.rooms.Count; j++)
                        if (!connected.Contains(Key(i, j)))
                            candidates.Add(new KeyValuePair<float, Vector2Int>(Vector2Int.Distance(L.rooms[i].Center, L.rooms[j].Center), new Vector2Int(i, j)));
                if (candidates.Count == 0) return;
                candidates.Sort((x, y) => x.Key.CompareTo(y.Key));
                var pick = candidates[rng.Range(0, Mathf.Min(3, candidates.Count))].Value;
                CreateLCorridor(L, p, rng, L.rooms[pick.x].Center, L.rooms[pick.y].Center);
                connected.Add(Key(pick.x, pick.y));
                L.extraLoopsAdded++;
            }
        }

        static void CreateLCorridor(DungeonLayout L, DungeonProfile p, PcgRandom rng, Vector2Int from, Vector2Int to)
        {
            bool horizontalFirst = rng.Value > 0.5f;
            Vector2Int corner;
            if (horizontalFirst)
            {
                CarveHorizontal(L, p, from.x, to.x, from.y);
                CarveVertical(L, p, from.y, to.y, to.x);
                corner = new Vector2Int(to.x, from.y);
            }
            else
            {
                CarveVertical(L, p, from.y, to.y, from.x);
                CarveHorizontal(L, p, from.x, to.x, to.y);
                corner = new Vector2Int(from.x, to.y);
            }
            L.corridors.Add(new[] { from, corner, to });
        }

        static void CarveHorizontal(DungeonLayout L, DungeonProfile p, int x1, int x2, int y)
        {
            int start = Mathf.Min(x1, x2), end = Mathf.Max(x1, x2);
            for (int x = start; x <= end; x++)
                for (int offset = 0; offset < p.corridorWidth; offset++) CarveCorridorCell(L, x, y + offset);
        }

        static void CarveVertical(DungeonLayout L, DungeonProfile p, int y1, int y2, int x)
        {
            int start = Mathf.Min(y1, y2), end = Mathf.Max(y1, y2);
            for (int y = start; y <= end; y++)
                for (int offset = 0; offset < p.corridorWidth; offset++) CarveCorridorCell(L, x + offset, y);
        }

        static void CarveCorridorCell(DungeonLayout L, int x, int y)
        {
            // Baris/kolom terluar dibiarkan Wall supaya dungeon selalu tertutup.
            if (x < 1 || y < 1 || x > L.width - 2 || y > L.height - 2) return;
            if (L.grid[x, y] == TileType.Wall) L.grid[x, y] = TileType.Floor;
            if (L.roomId[x, y] < 0) L.corridor[x, y] = true;
        }

        // ================================================================ Random Walk (alternatif, modul §123 dan §131)

        static void RandomWalk(DungeonLayout L, DungeonProfile p, PcgRandom rng)
        {
            var carved = new List<Vector2Int>();
            var center = new Vector2Int(L.width / 2, L.height / 2);
            int b = p.walkBrush;
            Vector2Int pos = center, last = center;
            for (int w = 0; w < p.walkers; w++)
            {
                if (w > 0 && carved.Count > 0) pos = carved[rng.Range(0, carved.Count)];   // walker berikutnya mulai dari jejak lama → tetap terhubung
                var dir = LayoutDirs[rng.Range(0, 4)];
                Brush(L, pos, b, carved);
                for (int s = 0; s < p.walkLength; s++)
                {
                    if (rng.Value > p.walkStraightChance) dir = LayoutDirs[rng.Range(0, 4)];
                    var next = pos + dir;
                    if (next.x < 1 || next.y < 1 || next.x + b > L.width - 1 || next.y + b > L.height - 1) { dir = new Vector2Int(-dir.x, -dir.y); continue; }
                    pos = next;
                    Brush(L, pos, b, carved);
                }
                last = pos;
            }
            L.startPosition = center;
            walkEnd = last;
        }

        static Vector2Int walkEnd;
        static readonly Vector2Int[] LayoutDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        static void Brush(DungeonLayout L, Vector2Int c, int size, List<Vector2Int> carved)
        {
            for (int dx = 0; dx < size; dx++)
                for (int dy = 0; dy < size; dy++)
                {
                    int x = c.x + dx, y = c.y + dy;
                    if (x < 1 || y < 1 || x > L.width - 2 || y > L.height - 2) continue;
                    if (L.grid[x, y] == TileType.Wall) { L.grid[x, y] = TileType.Floor; L.corridor[x, y] = true; carved.Add(new Vector2Int(x, y)); }
                }
        }

        // ================================================================ Start & Goal (modul §63–§64, §108)

        static void PlaceStartAndGoal(DungeonLayout L, DungeonProfile p)
        {
            // Bersihkan penanda lama (dipanggil ulang setelah repair).
            for (int x = 0; x < L.width; x++)
                for (int y = 0; y < L.height; y++)
                {
                    if (L.grid[x, y] == TileType.Start || L.grid[x, y] == TileType.Goal) L.grid[x, y] = TileType.Floor;
                    L.goalArea[x, y] = false;
                }

            if (L.algorithm == DungeonAlgorithm.BSP)
            {
                if (L.rooms.Count == 0) return;
                L.startRoom = 0;
                L.startPosition = L.rooms[0].Center;
                ComputeDistances(L, false);
                int goal = L.rooms.Count - 1;                                         // Strategi A: First Room → Last Room
                if (p.goalStrategy == GoalStrategy.B_GeometricFarthest)              // Strategi B: room terjauh secara geometris
                {
                    float far = -1f;
                    foreach (var r in L.rooms) { float d = Vector2Int.Distance(L.startPosition, r.Center); if (d > far) { far = d; goal = r.index; } }
                }
                else if (p.goalStrategy == GoalStrategy.C_PathFarthest)              // Strategi C: shortest-path (BFS) terpanjang
                {
                    int far = -1;
                    foreach (var r in L.rooms) { int d = L.Distance(r.Center); if (d > far) { far = d; goal = r.index; } }
                }
                L.goalRoom = goal;
                L.goalPosition = L.rooms[goal].Center;
            }
            else
            {
                L.startRoom = -1; L.goalRoom = -1;
                if (!L.IsFloor(L.startPosition)) return;
                ComputeDistances(L, false);
                Vector2Int goal = walkEnd;                                            // Strategi A: titik terakhir walker
                if (p.goalStrategy != GoalStrategy.A_FirstToLast)
                {
                    float best = -1f;
                    for (int pass = 0; pass < 2 && best < 0f; pass++)                 // pass 0: wajib area lapang 3x3 untuk generator
                        for (int x = 1; x < L.width - 1; x++)
                            for (int y = 1; y < L.height - 1; y++)
                            {
                                var c = new Vector2Int(x, y);
                                if (L.Distance(c) < 0 || (pass == 0 && !Open(L, c, 1))) continue;
                                float d = p.goalStrategy == GoalStrategy.B_GeometricFarthest ? Vector2Int.Distance(L.startPosition, c) : L.Distance(c);
                                if (d > best) { best = d; goal = c; }
                            }
                }
                L.goalPosition = goal;
            }

            if (L.IsFloor(L.startPosition)) L.grid[L.startPosition.x, L.startPosition.y] = TileType.Start;
            if (L.IsFloor(L.goalPosition) && L.goalPosition != L.startPosition)
            {
                L.grid[L.goalPosition.x, L.goalPosition.y] = TileType.Goal;
                for (int dx = -GoalClear; dx <= GoalClear; dx++)
                    for (int dy = -GoalClear; dy <= GoalClear; dy++)
                    {
                        var c = L.goalPosition + new Vector2Int(dx, dy);
                        if (L.IsFloor(c)) L.goalArea[c.x, c.y] = true;
                    }
            }
        }

        /// <summary>Semua cell dalam radius r (kotak) adalah floor tanpa props.</summary>
        static bool Open(DungeonLayout L, Vector2Int c, int r)
        {
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                    if (!L.Walkable(c + new Vector2Int(dx, dy))) return false;
            return true;
        }

        // ================================================================ BFS / flood fill (modul §93)

        /// <summary>Flood fill dari Start: jarak BFS ke setiap cell (-1 = tidak terjangkau). Props dihitung penghalang bila respectProps.</summary>
        public static void ComputeDistances(DungeonLayout L, bool respectProps, bool avoidGoal = false)
        {
            for (int x = 0; x < L.width; x++) for (int y = 0; y < L.height; y++) L.distance[x, y] = -1;
            if (!L.IsFloor(L.startPosition)) return;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(L.startPosition);
            L.distance[L.startPosition.x, L.startPosition.y] = 0;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int d = L.distance[current.x, current.y];
                foreach (var dir in DungeonLayout.Directions)
                {
                    var next = current + dir;
                    if (!L.IsInsideGrid(next) || L.distance[next.x, next.y] >= 0) continue;
                    if (L.grid[next.x, next.y] == TileType.Wall) continue;
                    if (respectProps && L.blocked[next.x, next.y]) continue;
                    if (avoidGoal && L.goalArea[next.x, next.y]) continue;            // generator masih terkunci: tidak boleh dilewati
                    L.distance[next.x, next.y] = d + 1;
                    queue.Enqueue(next);
                }
            }
        }

        /// <summary>HasPathToGoal versi modul §93: BFS dari Start sampai Goal ditemukan.</summary>
        public static bool HasPathToGoal(DungeonLayout L)
        {
            return L.IsInsideGrid(L.goalPosition) && L.distance[L.goalPosition.x, L.goalPosition.y] >= 0;
        }

        /// <summary>Jalur terpendek BFS dari 'from' ke 'to' (dipakai critical path dan path guide pemain).</summary>
        public static bool FindPath(DungeonLayout L, Vector2Int from, Vector2Int to, List<Vector2Int> path, bool respectProps = true)
        {
            path.Clear();
            if (!L.IsFloor(from) || !L.IsFloor(to)) return false;
            var parent = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from); parent[from] = from;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (c == to) break;
                foreach (var dir in DungeonLayout.Directions)
                {
                    var n = c + dir;
                    if (!L.IsFloor(n) || parent.ContainsKey(n)) continue;
                    if (respectProps && L.blocked[n.x, n.y] && n != to) continue;
                    parent[n] = c; queue.Enqueue(n);
                }
            }
            if (!parent.ContainsKey(to)) return false;
            for (var c = to; ; c = parent[c]) { path.Add(c); if (c == from) break; }
            path.Reverse();
            return true;
        }

        // ================================================================ Validasi layout (hard constraint, modul §91–§94, §118)

        static bool ValidateLayout(DungeonLayout L, DungeonProfile p, out string reason)
        {
            reason = "";
            ComputeDistances(L, false);
            if (L.algorithm == DungeonAlgorithm.BSP && L.rooms.Count < 2) { reason = "room < 2"; return false; }
            if (!L.IsInsideGrid(L.startPosition) || !L.IsFloor(L.startPosition)) { reason = "Start bukan floor"; return false; }
            if (!L.IsInsideGrid(L.goalPosition) || !L.IsFloor(L.goalPosition)) { reason = "Goal bukan floor"; return false; }
            if (L.startPosition == L.goalPosition) { reason = "Start = Goal"; return false; }
            if (!HasPathToGoal(L)) { reason = "Goal tidak terjangkau"; return false; }
            foreach (var r in L.rooms) if (L.Distance(r.Center) < 0) { reason = "room " + r.index + " terputus"; return false; }
            return true;
        }

        /// <summary>
        /// Repair: room (atau area floor) yang tidak terjangkau dihubungkan dengan corridor L ke cell terjangkau terdekat.
        /// Return false bila tidak ada yang bisa diperbaiki.
        /// </summary>
        static bool RepairLayout(DungeonLayout L, DungeonProfile p)
        {
            ComputeDistances(L, false);
            var targets = new List<Vector2Int>();
            foreach (var r in L.rooms) if (L.Distance(r.Center) < 0) targets.Add(r.Center);
            if (L.IsFloor(L.goalPosition) && L.Distance(L.goalPosition) < 0 && !targets.Contains(L.goalPosition)) targets.Add(L.goalPosition);
            if (targets.Count == 0) return false;
            var rng = PcgRandom.For(L.seed, "repair", L.repairs);
            foreach (var t in targets)
            {
                ComputeDistances(L, false);
                if (L.Distance(t) >= 0) continue;
                Vector2Int best = L.startPosition; float bd = float.MaxValue;
                for (int x = 0; x < L.width; x++)
                    for (int y = 0; y < L.height; y++)
                    {
                        if (L.distance[x, y] < 0) continue;
                        float d = Vector2Int.Distance(t, new Vector2Int(x, y));
                        if (d < bd) { bd = d; best = new Vector2Int(x, y); }
                    }
                CreateLCorridor(L, p, rng, t, best);
            }
            return true;
        }

        // ================================================================ Placement (modul §78–§82, §116–§117)

        static void Populate(DungeonLayout L, DungeonProfile p, int seed, int attempt, bool[] propAllowed)
        {
            ComputeDistances(L, false);
            FindPath(L, L.startPosition, L.goalPosition, L.criticalPath, false);

            // Safe room: room Start + (safeRoomCount-1) room terdekat berikutnya (jarak BFS).
            L.safeRooms.Clear();
            if (L.rooms.Count > 0 && p.safeRoomCount > 0)
            {
                var order = new List<DungeonRoom>(L.rooms);
                order.Sort((a, b) => L.Distance(a.Center).CompareTo(L.Distance(b.Center)));
                for (int i = 0; i < order.Count && L.safeRooms.Count < p.safeRoomCount; i++) if (order[i].index != L.goalRoom) L.safeRooms.Add(order[i].index);
            }

            PlaceProps(L, p, PcgRandom.For(seed, "props", attempt), propAllowed);
            ComputeDistances(L, true);
            PlaceRelays(L, p, PcgRandom.For(seed, "relay", attempt));
            PlaceEnemies(L, p, PcgRandom.For(seed, "enemy", attempt));
            PlaceItems(L, p, PcgRandom.For(seed, "item", attempt));
        }

        static void PlaceProps(DungeonLayout L, DungeonProfile p, PcgRandom rng, bool[] propAllowed)
        {
            if (p.propsPerRoom <= 0 || p.propTable == null || p.propTable.Length == 0) return;
            var weights = new List<float>();
            var largeWeights = new List<float>();
            for (int i = 0; i < p.propTable.Length; i++)
            {
                var e = p.propTable[i];
                bool ok = e != null && (propAllowed == null || (i < propAllowed.Length && propAllowed[i]));
                bool large = p.propSpan != null && i < p.propSpan.Length && p.propSpan[i] > 1;
                weights.Add(ok && !large ? Mathf.Max(0f, e.weight) : 0f);
                largeWeights.Add(ok && large ? Mathf.Max(0f, e.weight) : 0f);
            }
            var pathZone = new bool[L.width, L.height];
            foreach (var c in L.criticalPath)
                for (int dx = -p.pathBuffer; dx <= p.pathBuffer; dx++)
                    for (int dy = -p.pathBuffer; dy <= p.pathBuffer; dy++)
                        if (L.IsInsideGrid(c.x + dx, c.y + dy)) pathZone[c.x + dx, c.y + dy] = true;

            // Area kandidat: tiap room (BSP) atau seluruh floor (Random Walk, jumlah setara 4 "room").
            var areas = new List<RectInt>();
            if (L.algorithm == DungeonAlgorithm.BSP) foreach (var r in L.rooms) areas.Add(new RectInt(r.x, r.y, r.width, r.height));
            else for (int i = 0; i < 4; i++) areas.Add(new RectInt(1, 1, L.width - 2, L.height - 2));

            bool bsp = L.algorithm == DungeonAlgorithm.BSP;
            foreach (var area in areas)
            {
                int cells = area.width * area.height;
                var usedInRoom = new HashSet<int>();                                     // variasi: jenis yang belum dipakai di room didahulukan
                int large = PlaceLargeProps(L, p, rng, area, cells, largeWeights, usedInRoom, pathZone);
                int cover = bsp ? Mathf.Min(Mathf.Max(p.propsPerRoom, p.maxCoverPerRoom), p.propsPerRoom + cells / Mathf.Max(1, p.coverCellsPerExtra)) : p.propsPerRoom;
                for (int k = 0; k < cover; k++)
                {
                    L.propStats.requested++;
                    for (int attempt = 0; attempt < 30; attempt++)
                    {
                        L.propStats.attempts++;
                        var c = new Vector2Int(rng.Range(area.xMin, area.xMax), rng.Range(area.yMin, area.yMax));
                        DRule rule = PropRule(L, p, c, pathZone);
                        if (rule == DRule.Accepted && !Open(L, c, 1)) rule = DRule.NotOpen;            // sisakan lorong di semua sisi
                        if (rule != DRule.Accepted) { L.propStats.Reject(rule); continue; }
                        int index = PickVaried(rng, weights, usedInRoom);
                        if (index < 0) return;
                        L.blocked[c.x, c.y] = true;
                        L.placements.Add(new DungeonPlacement { kind = PlaceKind.Prop, cell = c, id = index.ToString(), room = L.RoomAt(c), yaw = 90f * rng.Range(0, 4) + rng.Range(-12f, 12f) });
                        usedInRoom.Add(index);
                        L.propStats.accepted++;
                        break;
                    }
                }

                // Dekorasi tepi: menempel dinding room, tidak menutup pintu/jalur wajib, tidak memutus room.
                int wall = bsp ? Mathf.Min(Mathf.Max(p.wallPropsPerRoom, p.maxWallPropsPerRoom), p.wallPropsPerRoom + cells / Mathf.Max(1, p.wallCellsPerExtra)) : p.wallPropsPerRoom;
                wall = Mathf.Max(1, wall - large);                                      // container sudah mengisi satu sisi dinding
                var edge = new List<KeyValuePair<Vector2Int, Vector2Int>>();
                for (int x = area.xMin; x < area.xMax; x++)
                    for (int y = area.yMin; y < area.yMax; y++)
                    {
                        var c = new Vector2Int(x, y);
                        Vector2Int dir;
                        if (L.Walkable(c) && WallSide(L, c, out dir)) edge.Add(new KeyValuePair<Vector2Int, Vector2Int>(c, dir));
                    }
                for (int k = 0; k < wall && edge.Count > 0; k++)
                {
                    L.propStats.requested++;
                    for (int attempt = 0; attempt < 30; attempt++)
                    {
                        L.propStats.attempts++;
                        var e = edge[rng.Range(0, edge.Count)];
                        var c = e.Key;
                        DRule rule = PropRule(L, p, c, pathZone);
                        if (rule == DRule.Accepted && !L.Walkable(c - e.Value)) rule = DRule.NotOpen;    // sisi dalam harus tetap lantai
                        if (rule != DRule.Accepted) { L.propStats.Reject(rule); continue; }
                        int index = PickVaried(rng, weights, usedInRoom);
                        if (index < 0) return;
                        L.blocked[c.x, c.y] = true;
                        float face = Mathf.Atan2(-e.Value.x, -e.Value.y) * Mathf.Rad2Deg;              // menghadap ke dalam room
                        L.placements.Add(new DungeonPlacement { kind = PlaceKind.Prop, cell = c, id = index.ToString(), room = L.RoomAt(c), yaw = face + rng.Range(-15f, 15f), wallDir = e.Value });
                        usedInRoom.Add(index);
                        L.propStats.accepted++;
                        break;
                    }
                }
            }
        }

        /// <summary>Weighted random, tetapi jenis yang sudah dipakai di room ini diberi bobot 0 selama masih ada jenis lain.</summary>
        static int PickVaried(PcgRandom rng, List<float> weights, HashSet<int> used)
        {
            var pick = new List<float>(weights);
            bool anyUnused = false;
            for (int j = 0; j < pick.Count; j++) if (pick[j] > 0f && !used.Contains(j)) anyUnused = true;
            if (anyUnused) for (int j = 0; j < pick.Count; j++) if (used.Contains(j)) pick[j] = 0f;
            return rng.WeightedIndex(pick);
        }

        /// <summary>
        /// Props besar (lebih panjang dari 1 cell, mis. cargo container): diletakkan menempel dinding room di atas deretan
        /// 2–3 cell tepi. Semua cell harus lantai tepi dengan dinding di sisi yang sama, sisi dalamnya tetap lantai, bukan
        /// mulut pintu / critical path / safe zone / goal, dan tidak bersebelahan dengan props lain → room tidak terputus.
        /// </summary>
        static int PlaceLargeProps(DungeonLayout L, DungeonProfile p, PcgRandom rng, RectInt area, int cells, List<float> largeWeights, HashSet<int> used, bool[,] pathZone)
        {
            int placed = 0;
            if (L.algorithm != DungeonAlgorithm.BSP || CountPositive(largeWeights) == 0) return 0;
            int count = rng.Value < p.largePropChance ? 1 : 0;
            if (count > 0 && cells >= p.largePropSecondCells && rng.Value < 0.5f) count = 2;
            for (int n = 0; n < count; n++)
            {
                int index = PickVaried(rng, largeWeights, used);
                if (index < 0) return placed;
                int span = p.propSpan[index];
                L.propStats.requested++;
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    L.propStats.attempts++;
                    var start = new Vector2Int(rng.Range(area.xMin, area.xMax), rng.Range(area.yMin, area.yMax));
                    Vector2Int dir;
                    if (!L.Walkable(start) || !WallSide(L, start, out dir)) { L.propStats.Reject(DRule.NotFloor); continue; }
                    var along = new Vector2Int(Mathf.Abs(dir.y), Mathf.Abs(dir.x));         // sejajar dinding
                    if (rng.Range(0, 2) == 0) along = new Vector2Int(-along.x, -along.y);
                    DRule rule = DRule.Accepted;
                    for (int k = 0; k < span && rule == DRule.Accepted; k++)
                    {
                        var c = start + along * k;
                        if (!L.IsInsideGrid(c) || L.RoomAt(c) != L.RoomAt(start)) { rule = DRule.NotFloor; break; }
                        rule = PropRule(L, p, c, pathZone);
                        if (rule == DRule.Accepted && L.IsFloor(c + dir)) rule = DRule.NotOpen;     // dinding harus di sisi yang sama
                        if (rule == DRule.Accepted && !L.Walkable(c - dir)) rule = DRule.NotOpen;   // sisi dalam tetap lantai
                    }
                    if (rule != DRule.Accepted) { L.propStats.Reject(rule); continue; }
                    for (int k = 0; k < span; k++) { var c = start + along * k; L.blocked[c.x, c.y] = true; }
                    float face = Mathf.Atan2(-dir.x, -dir.y) * Mathf.Rad2Deg;
                    L.placements.Add(new DungeonPlacement { kind = PlaceKind.Prop, cell = start, id = index.ToString(), room = L.RoomAt(start), yaw = face, wallDir = dir, span = span, spanDir = along });
                    used.Add(index);
                    L.propStats.accepted++;
                    placed++;
                    break;
                }
            }
            return placed;
        }

        static DRule PropRule(DungeonLayout L, DungeonProfile p, Vector2Int c, bool[,] pathZone)
        {
            if (!L.Walkable(c)) return DRule.NotFloor;
            if (L.goalArea[c.x, c.y] || Vector2Int.Distance(c, L.goalPosition) < 2.5f) return DRule.GoalArea;
            if (Vector2Int.Distance(c, L.startPosition) <= p.safeRadiusCells) return DRule.NearStart;
            if (pathZone[c.x, c.y]) return DRule.OnCriticalPath;                 // jangan halangi jalur wajib
            if (NearCorridor(L, c)) return DRule.Doorway;                         // pintu room tetap terbuka
            if (L.Occupied(c, 1)) return DRule.Occupied;
            return DRule.Accepted;
        }

        /// <summary>Cell lantai yang bersebelahan dengan dinding (bukan lantai) di salah satu sisi; dir = arah dinding.</summary>
        static bool WallSide(DungeonLayout L, Vector2Int c, out Vector2Int dir)
        {
            dir = Vector2Int.zero;
            foreach (var d in DungeonLayout.Directions)
                if (!L.IsInsideGrid(c + d) || !L.IsFloor(c + d)) { dir = d; return true; }
            return false;
        }

        static bool NearCorridor(DungeonLayout L, Vector2Int c)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var n = c + new Vector2Int(dx, dy);
                    if (L.IsInsideGrid(n) && L.corridor[n.x, n.y] && L.algorithm == DungeonAlgorithm.BSP) return true;
                }
            return false;
        }

        // ================================================================ Objective: power relay (lock-and-key)

        /// <summary>
        /// Relay ditempatkan dengan farthest-point sampling + constraint:
        ///  - di room selain Start/Goal/safe room (Random Walk: area lapang), jauh dari Start dan relay lain (relayMinSpacing),
        ///  - minimal relayOffPathMin relay di room yang tidak dilewati critical path (pemain wajib menjelajah),
        ///  - 3x3 di sekitarnya lapang, plus zombie penjaga di room yang sama.
        /// Validasi: BFS dengan Goal dianggap tertutup → semua relay harus terjangkau (lihat CheckReachability).
        /// </summary>
        static void PlaceRelays(DungeonLayout L, DungeonProfile p, PcgRandom rng)
        {
            L.relayStats.requested = p.relayCount;
            if (p.relayCount <= 0) return;
            var pathCells = new HashSet<Vector2Int>(L.criticalPath);
            var candidates = new List<KeyValuePair<Vector2Int, bool>>();   // (cell, offPath)
            if (L.algorithm == DungeonAlgorithm.BSP)
            {
                foreach (var r in L.rooms)
                {
                    if (r.index == L.startRoom || r.index == L.goalRoom || L.safeRooms.Contains(r.index)) { L.relayStats.Reject(DRule.SafeRoom); continue; }
                    bool offPath = true;
                    for (int x = r.x; x < r.x + r.width && offPath; x++)
                        for (int y = r.y; y < r.y + r.height && offPath; y++) if (pathCells.Contains(new Vector2Int(x, y))) offPath = false;
                    // Satu kandidat cell per room (acak di area lapang room).
                    for (int attempt = 0; attempt < 40; attempt++)
                    {
                        L.relayStats.attempts++;
                        var c = new Vector2Int(rng.Range(r.x + 1, r.x + r.width - 1), rng.Range(r.y + 1, r.y + r.height - 1));
                        if (!Open(L, c, 1)) { L.relayStats.Reject(DRule.NotOpen); continue; }
                        if (L.Occupied(c, 1)) { L.relayStats.Reject(DRule.Occupied); continue; }
                        candidates.Add(new KeyValuePair<Vector2Int, bool>(c, offPath));
                        break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < 400; i++)
                {
                    L.relayStats.attempts++;
                    var c = new Vector2Int(rng.Range(2, L.width - 2), rng.Range(2, L.height - 2));
                    if (!Open(L, c, 1) || L.goalArea[c.x, c.y] || L.Distance(c) < p.minEnemyDistance) continue;
                    bool offPath = true;
                    foreach (var q in L.criticalPath) if (Mathf.Abs(q.x - c.x) <= 3 && Mathf.Abs(q.y - c.y) <= 3) { offPath = false; break; }
                    candidates.Add(new KeyValuePair<Vector2Int, bool>(c, offPath));
                }
            }
            rng.Shuffle(candidates);

            var chosen = new List<Vector2Int>();
            for (int k = 0; k < p.relayCount; k++)
            {
                bool mustOff = p.relayOffPathMin - L.relaysOffPath >= p.relayCount - k;   // sisa slot harus dipakai relay di luar jalur
                float best = -1f; int bestIndex = -1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    var c = candidates[i].Key;
                    if (chosen.Contains(c) || (mustOff && !candidates[i].Value) || L.Distance(c) < 0) continue;
                    float near = Vector2Int.Distance(c, L.startPosition);
                    foreach (var q in chosen) near = Mathf.Min(near, Vector2Int.Distance(c, q));
                    if (near < p.relayMinSpacing) { L.relayStats.Reject(DRule.Spacing); continue; }
                    if (near > best) { best = near; bestIndex = i; }          // farthest-point sampling: relay saling berjauhan
                }
                if (bestIndex < 0) { L.relayStats.Reject(DRule.NoRoom); continue; }
                var cell = candidates[bestIndex].Key;
                chosen.Add(cell);
                if (candidates[bestIndex].Value) L.relaysOffPath++;
                L.placements.Add(new DungeonPlacement { kind = PlaceKind.Relay, cell = cell, id = "relay" + chosen.Count, room = L.RoomAt(cell), yaw = 90f * rng.Range(0, 4) });
                L.relayStats.accepted++;
            }

            // Zombie penjaga di sekitar setiap relay (room yang sama, 2-4 cell dari relay).
            var weights = new List<float>();
            foreach (var e in p.enemyTable) weights.Add(e != null && !string.IsNullOrEmpty(e.id) ? e.weight : 0f);
            foreach (var relay in chosen)
                for (int g = 0; g < p.relayGuards; g++)
                    for (int attempt = 0; attempt < 40; attempt++)
                    {
                        var c = relay + new Vector2Int(rng.Range(-4, 5), rng.Range(-4, 5));
                        if (!L.Walkable(c) || L.goalArea[c.x, c.y] || L.Distance(c) < p.minEnemyDistance || L.Occupied(c, 1)) continue;
                        if (Vector2Int.Distance(c, relay) < 2f || (L.RoomAt(relay) >= 0 && L.RoomAt(c) != L.RoomAt(relay))) continue;
                        int index = Mathf.Max(0, rng.WeightedIndex(weights));
                        L.placements.Add(new DungeonPlacement { kind = PlaceKind.Enemy, cell = c, id = p.enemyTable[index].id, room = L.RoomAt(c), yaw = rng.Range(0f, 360f) });
                        L.relayGuards++;
                        break;
                    }
        }

        /// <summary>Room yang berisi zombie (selain Start/Goal) = room "clear room": pintunya tertutup saat player masuk.</summary>
        public static List<int> CombatRooms(DungeonLayout L)
        {
            var list = new List<int>();
            foreach (var q in L.placements)
                if (q.kind == PlaceKind.Enemy && q.room >= 0 && q.room != L.startRoom && q.room != L.goalRoom && !list.Contains(q.room)) list.Add(q.room);
            list.Sort();
            return list;
        }

        /// <summary>Semua bukaan room: pasangan (cell room, cell luar room) yang sama-sama floor. Di sinilah pintu clear room dipasang.</summary>
        public static List<KeyValuePair<Vector2Int, Vector2Int>> DoorEdges(DungeonLayout L, int room)
        {
            var list = new List<KeyValuePair<Vector2Int, Vector2Int>>();
            if (room < 0 || room >= L.rooms.Count) return list;
            var r = L.rooms[room];
            for (int x = r.x; x < r.x + r.width; x++)
                for (int y = r.y; y < r.y + r.height; y++)
                {
                    var c = new Vector2Int(x, y);
                    foreach (var d in DungeonLayout.Directions)
                    {
                        var n = c + d;
                        if (L.IsFloor(n) && L.RoomAt(n) != room) list.Add(new KeyValuePair<Vector2Int, Vector2Int>(c, n));
                    }
                }
            return list;
        }

        /// <summary>Panjang rute pemain (BFS): Start → relay terdekat berikutnya (greedy) → ... → Goal.</summary>
        public static int RouteLength(DungeonLayout L)
        {
            var path = new List<Vector2Int>();
            var todo = new List<Vector2Int>();
            foreach (var q in L.placements) if (q.kind == PlaceKind.Relay) todo.Add(q.cell);
            var at = L.startPosition; int total = 0;
            while (todo.Count > 0)
            {
                int best = int.MaxValue, bi = -1;
                for (int i = 0; i < todo.Count; i++)
                    if (FindPath(L, at, todo[i], path) && path.Count - 1 < best) { best = path.Count - 1; bi = i; }
                if (bi < 0) return -1;
                total += best; at = todo[bi]; todo.RemoveAt(bi);
            }
            if (!FindPath(L, at, L.goalPosition, path)) return -1;
            return total + path.Count - 1;
        }

        /// <summary>
        /// Zombie bertingkat menurut jarak: area (room BSP, atau pita jarak BFS pada Random Walk) diurutkan dari jarak BFS ke Start,
        /// lalu dibagi rata ke tingkat zombieTiers (default 3/5/7/9). Makin jauh, makin banyak dan makin condong ke jenis berat.
        /// Penjaga relay yang sudah ada di room ikut dihitung dalam kuota room tersebut.
        /// </summary>
        static void PlaceEnemiesByDistance(DungeonLayout L, DungeonProfile p, PcgRandom rng)
        {
            var tiers = p.zombieTiers != null && p.zombieTiers.Length > 0 ? p.zombieTiers : new[] { 3, 5, 7, 9 };
            var areas = new List<List<Vector2Int>>();
            var areaRoom = new List<int>();
            if (L.algorithm == DungeonAlgorithm.BSP)
            {
                var rooms = new List<DungeonRoom>();
                foreach (var r in L.rooms) if (r.index != L.startRoom && !L.safeRooms.Contains(r.index) && RoomDistance(L, r) >= 0) rooms.Add(r);
                rooms.Sort((a, b) => RoomDistance(L, a).CompareTo(RoomDistance(L, b)));
                foreach (var r in rooms)
                {
                    var cells = new List<Vector2Int>();
                    for (int x = r.x; x < r.x + r.width; x++) for (int y = r.y; y < r.y + r.height; y++) cells.Add(new Vector2Int(x, y));
                    areas.Add(cells); areaRoom.Add(r.index);
                }
            }
            else
            {
                int max = 0;
                for (int x = 0; x < L.width; x++) for (int y = 0; y < L.height; y++) max = Mathf.Max(max, L.distance[x, y]);
                for (int t = 0; t < tiers.Length; t++) { areas.Add(new List<Vector2Int>()); areaRoom.Add(-1); }
                for (int x = 0; x < L.width; x++)
                    for (int y = 0; y < L.height; y++)
                    {
                        int d = L.distance[x, y];
                        if (d < p.minEnemyDistance) continue;
                        int band = Mathf.Min(tiers.Length - 1, (d - p.minEnemyDistance) * tiers.Length / Mathf.Max(1, max - p.minEnemyDistance + 1));
                        areas[band].Add(new Vector2Int(x, y));
                    }
            }

            for (int a = 0; a < areas.Count; a++)
            {
                int tier = L.algorithm == DungeonAlgorithm.BSP ? Mathf.Min(tiers.Length - 1, a * tiers.Length / Mathf.Max(1, areas.Count)) : a;
                float far = tiers.Length > 1 ? tier / (float)(tiers.Length - 1) : 0f;
                // Bobot jenis: entry ke-j (walker → bloater) makin berat makin dominan di area jauh; bloater tidak ada di tingkat pertama.
                var weights = new List<float>();
                for (int j = 0; j < p.enemyTable.Length; j++)
                {
                    var e = p.enemyTable[j];
                    float w = e != null && !string.IsNullOrEmpty(e.id) ? e.weight : 0f;
                    if (tier == 0 && j == p.enemyTable.Length - 1 && p.enemyTable.Length > 2) w = 0f;
                    weights.Add(w * (1f + far * p.heavyBiasFar * j));
                }
                int quota = tiers[tier];
                int existing = 0;
                foreach (var q in L.placements) if (q.kind == PlaceKind.Enemy && areaRoom[a] >= 0 && q.room == areaRoom[a]) existing++;
                var used = new HashSet<int>();
                var cells = areas[a];
                for (int i = existing; i < quota; i++)
                {
                    L.enemyStats.requested++;
                    // Variasi: jenis yang belum dipakai di area ini didahulukan.
                    var pick = new List<float>(weights);
                    if (used.Count < CountPositive(weights)) for (int j = 0; j < pick.Count; j++) if (used.Contains(j)) pick[j] = 0f;
                    int index = Mathf.Max(0, rng.WeightedIndex(pick));
                    bool placed = false;
                    for (int attempt = 0; attempt < 80 && !placed && cells.Count > 0; attempt++)
                    {
                        L.enemyStats.attempts++;
                        var c = cells[rng.Range(0, cells.Count)];
                        DRule rule = DRule.Accepted;
                        if (!L.Walkable(c)) rule = DRule.NotFloor;
                        else if (L.goalArea[c.x, c.y]) rule = DRule.GoalArea;
                        else if (L.Distance(c) < 0) rule = DRule.Unreachable;
                        else if (L.Distance(c) < p.minEnemyDistance || Vector2Int.Distance(c, L.startPosition) <= p.safeRadiusCells) rule = DRule.NearStart;
                        else if (L.Occupied(c, attempt < 50 ? 1 : 0)) rule = DRule.Occupied;          // room kecil: boleh bersebelahan
                        if (rule != DRule.Accepted) { L.enemyStats.Reject(rule); continue; }
                        L.placements.Add(new DungeonPlacement { kind = PlaceKind.Enemy, cell = c, id = p.enemyTable[index].id, room = L.RoomAt(c), yaw = rng.Range(0f, 360f) });
                        L.enemyStats.accepted++;
                        used.Add(index);
                        placed = true;
                    }
                }
            }
        }

        /// <summary>Jarak BFS terdekat dari Start ke cell mana pun di room (center bisa saja tertutup props).</summary>
        public static int RoomDistance(DungeonLayout L, DungeonRoom r)
        {
            int best = int.MaxValue;
            for (int x = r.x; x < r.x + r.width; x++)
                for (int y = r.y; y < r.y + r.height; y++) { int d = L.distance[x, y]; if (d >= 0 && d < best) best = d; }
            return best == int.MaxValue ? -1 : best;
        }

        static void PlaceBoss(DungeonLayout L, DungeonProfile p, PcgRandom rng)
        {
            // Boss Room (modul §116): boss hanya boleh spawn di Goal Room.
            if (!p.bossRoom) return;
            var candidates = new List<Vector2Int>();
            for (int x = 1; x < L.width - 1; x++)
                for (int y = 1; y < L.height - 1; y++)
                {
                    var c = new Vector2Int(x, y);
                    bool inGoal = L.goalRoom >= 0 ? L.RoomAt(c) == L.goalRoom : Vector2Int.Distance(c, L.goalPosition) <= 5f;
                    if (!inGoal || !L.Walkable(c) || L.goalArea[c.x, c.y] || L.Distance(c) < 0 || L.Occupied(c, 1)) continue;
                    if (Vector2Int.Distance(c, L.goalPosition) < 2.5f) continue;
                    candidates.Add(c);
                }
            if (candidates.Count > 0)
            {
                var c = candidates[rng.Range(0, candidates.Count)];
                L.placements.Add(new DungeonPlacement { kind = PlaceKind.Boss, cell = c, id = "boss", room = L.RoomAt(c), yaw = rng.Range(0f, 360f) });
            }
        }

        static int CountPositive(List<float> w) { int n = 0; foreach (var x in w) if (x > 0f) n++; return n; }

        static void PlaceEnemies(DungeonLayout L, DungeonProfile p, PcgRandom rng)
        {
            PlaceBoss(L, p, rng);                                   // dipesan dulu supaya Goal room tidak keburu penuh zombie
            if (p.zombiesByDistance) PlaceEnemiesByDistance(L, p, rng);
            int count = !p.zombiesByDistance ? (p.useEnemyDensity ? Mathf.Max(1, CountFloor(L) / Mathf.Max(1, p.floorTilesPerEnemy)) : p.enemyCount) : 0;
            var weights = new List<float>();
            foreach (var e in p.enemyTable) weights.Add(e != null && !string.IsNullOrEmpty(e.id) ? e.weight : 0f);
            for (int i = 0; i < count; i++)
            {
                L.enemyStats.requested++;
                for (int attempt = 0; attempt < 100; attempt++)
                {
                    L.enemyStats.attempts++;
                    var c = new Vector2Int(rng.Range(1, L.width - 1), rng.Range(1, L.height - 1));   // GetRandomFloorPosition (modul §78)
                    DRule rule = DRule.Accepted;
                    if (!L.Walkable(c)) rule = DRule.NotFloor;
                    else if (L.goalArea[c.x, c.y]) rule = DRule.GoalArea;
                    else if (L.Distance(c) < 0) rule = DRule.Unreachable;
                    else if (L.Distance(c) < p.minEnemyDistance || Vector2Int.Distance(c, L.startPosition) <= p.safeRadiusCells) rule = DRule.NearStart;   // modul §80
                    else if (L.RoomAt(c) >= 0 && L.safeRooms.Contains(L.RoomAt(c))) rule = DRule.SafeRoom;
                    else if (L.Occupied(c, 1)) rule = DRule.Occupied;
                    if (rule != DRule.Accepted) { L.enemyStats.Reject(rule); continue; }
                    int index = Mathf.Max(0, rng.WeightedIndex(weights));
                    L.placements.Add(new DungeonPlacement { kind = PlaceKind.Enemy, cell = c, id = p.enemyTable[index].id, room = L.RoomAt(c), yaw = rng.Range(0f, 360f) });
                    L.enemyStats.accepted++;
                    break;
                }
            }


        }

        static void PlaceItems(DungeonLayout L, DungeonProfile p, PcgRandom rng)
        {
            var deadEnds = new List<Vector2Int>();
            var sideRooms = new List<Vector2Int>();
            var others = new List<Vector2Int>();
            for (int x = 1; x < L.width - 1; x++)
                for (int y = 1; y < L.height - 1; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (!L.Walkable(c)) continue;
                    if (IsDeadEnd(L, c)) deadEnds.Add(c);
                    else if (L.RoomAt(c) >= 0 && L.RoomAt(c) != L.startRoom && L.RoomAt(c) != L.goalRoom) sideRooms.Add(c);
                    else others.Add(c);
                }
            rng.Shuffle(deadEnds); rng.Shuffle(sideRooms); rng.Shuffle(others);
            var order = new List<Vector2Int>();
            if (p.treasureRule) { order.AddRange(deadEnds); order.AddRange(sideRooms); order.AddRange(others); }
            else { order.AddRange(deadEnds); order.AddRange(sideRooms); order.AddRange(others); rng.Shuffle(order); }

            var weights = new List<float>();
            foreach (var e in p.itemTable) weights.Add(e != null ? e.weight : 0f);
            int placed = 0;
            foreach (var c in order)
            {
                if (placed >= p.itemCount) break;
                L.itemStats.attempts++;
                DRule rule = DRule.Accepted;
                if (L.goalArea[c.x, c.y]) rule = DRule.GoalArea;
                else if (L.Distance(c) < 0) rule = DRule.Unreachable;
                else if (c == L.startPosition) rule = DRule.NearStart;
                else if (L.Occupied(c, 1)) rule = DRule.Occupied;
                else
                    foreach (var q in L.placements)
                        if (q.kind == PlaceKind.Item && Vector2Int.Distance(q.cell, c) < p.itemSpacing) { rule = DRule.Spacing; break; }
                if (rule != DRule.Accepted) { L.itemStats.Reject(rule); continue; }
                var kind = p.itemTable.Length > 0 ? p.itemTable[Mathf.Max(0, rng.WeightedIndex(weights))].kind : SupplyKind.Ammo;
                L.placements.Add(new DungeonPlacement { kind = PlaceKind.Item, cell = c, id = kind.ToString(), room = L.RoomAt(c), yaw = rng.Range(0f, 360f) });
                placed++;
                L.itemStats.accepted++;
            }
            L.itemStats.requested = p.itemCount;
        }

        static bool IsDeadEnd(DungeonLayout L, Vector2Int c)
        {
            int n = 0;
            foreach (var d in DungeonLayout.Directions) if (L.Walkable(c + d)) n++;
            return n == 1;
        }

        static int CountFloor(DungeonLayout L)
        {
            int n = 0;
            for (int x = 0; x < L.width; x++) for (int y = 0; y < L.height; y++) if (L.grid[x, y] != TileType.Wall) n++;
            return n;
        }

        // ================================================================ Validasi akhir + repair props

        static bool FinalValidate(DungeonLayout L, DungeonProfile p, out string reason)
        {
            bool ok = CheckReachability(L, out reason);
            int guard = 0;
            while (!ok && guard++ < 50)
            {
                // Repair: buang props yang paling dekat dengan target yang tidak terjangkau, lalu flood fill ulang.
                Vector2Int target = FirstUnreachable(L);
                int worst = -1; float best = float.MaxValue;
                for (int i = 0; i < L.placements.Count; i++)
                {
                    if (L.placements[i].kind != PlaceKind.Prop) continue;
                    float d = Vector2Int.Distance(L.placements[i].cell, target);
                    if (d < best) { best = d; worst = i; }
                }
                if (worst < 0) break;
                var removed = L.placements[worst];
                for (int k = 0; k < removed.Cells; k++) { var c = removed.CellAt(k); L.blocked[c.x, c.y] = false; }
                L.placements.RemoveAt(worst);
                L.propRemovals++;
                ok = CheckReachability(L, out reason);
            }
            FindPath(L, L.startPosition, L.goalPosition, L.criticalPath, true);
            BuildChecks(L, p);
            return ok;
        }

        static bool CheckReachability(DungeonLayout L, out string reason)
        {
            reason = "";
            ComputeDistances(L, true);
            if (!HasPathToGoal(L)) { reason = "Goal terhalang props"; return false; }
            foreach (var r in L.rooms)
            {
                bool any = false;
                for (int x = r.x; x < r.x + r.width && !any; x++)
                    for (int y = r.y; y < r.y + r.height && !any; y++) if (L.distance[x, y] >= 0) any = true;
                if (!any) { reason = "room " + r.index + " terhalang"; return false; }
            }
            foreach (var q in L.placements)
                if (q.kind != PlaceKind.Prop && L.Distance(q.cell) < 0) { reason = q.kind + " di " + q.cell + " tidak terjangkau"; return false; }
            // Lock-and-key: semua relay harus bisa dicapai TANPA melewati generator Goal yang masih terkunci.
            ComputeDistances(L, true, true);
            Vector2Int lockedRelay = new Vector2Int(-1, -1);
            foreach (var q in L.placements) if (q.kind == PlaceKind.Relay && L.Distance(q.cell) < 0) { lockedRelay = q.cell; break; }
            ComputeDistances(L, true);
            if (lockedRelay.x >= 0) { reason = "relay di " + lockedRelay + " hanya terjangkau lewat Goal"; return false; }
            return true;
        }

        static Vector2Int FirstUnreachable(DungeonLayout L)
        {
            if (!HasPathToGoal(L)) return L.goalPosition;
            foreach (var q in L.placements) if (q.kind != PlaceKind.Prop && L.Distance(q.cell) < 0) return q.cell;
            foreach (var r in L.rooms)
            {
                bool any = false;
                for (int x = r.x; x < r.x + r.width && !any; x++)
                    for (int y = r.y; y < r.y + r.height && !any; y++) if (L.distance[x, y] >= 0) any = true;
                if (!any) return r.Center;
            }
            return L.goalPosition;
        }

        static void BuildChecks(DungeonLayout L, DungeonProfile p)
        {
            L.checks.Clear();
            System.Action<string, bool, bool, string> Add = (name, hard, pass, detail) => L.checks.Add(new DungeonCheck { name = name, hard = hard, pass = pass, detail = detail });
            int unreachableRooms = 0;
            foreach (var r in L.rooms) if (L.Distance(r.Center) < 0 && !L.blocked[r.Center.x, r.Center.y]) unreachableRooms++;
            int enemies = L.Count(PlaceKind.Enemy), items = L.Count(PlaceKind.Item);
            bool enemiesOnFloor = true, enemiesFar = true;
            foreach (var q in L.placements)
            {
                if (q.kind == PlaceKind.Prop) continue;
                if (!L.IsFloor(q.cell) || L.blocked[q.cell.x, q.cell.y]) enemiesOnFloor = false;
                if (q.kind == PlaceKind.Enemy && L.Distance(q.cell) < p.minEnemyDistance) enemiesFar = false;
            }

            if (L.algorithm == DungeonAlgorithm.BSP) Add("Room minimal dua", true, L.rooms.Count >= 2, L.rooms.Count + " room");
            Add("Start tersedia di floor", true, L.IsFloor(L.startPosition), L.startPosition.ToString());
            Add("Goal tersedia di floor", true, L.IsFloor(L.goalPosition), L.goalPosition.ToString());
            Add("Start–Goal terhubung (BFS)", true, HasPathToGoal(L), "path " + Mathf.Max(0, L.criticalPath.Count - 1) + " cell");
            Add("Semua room terjangkau", true, unreachableRooms == 0, unreachableRooms + " terputus");
            Add("Enemy/item di floor valid", true, enemiesOnFloor, "");
            Add("Enemy jauh dari Start (≥ " + p.minEnemyDistance + " cell)", true, enemiesFar, "");

            if (p.relayCount > 0)
            {
                ComputeDistances(L, true, true);
                int lockedRelays = 0; foreach (var q in L.placements) if (q.kind == PlaceKind.Relay && L.Distance(q.cell) < 0) lockedRelays++;
                ComputeDistances(L, true);
                int relays = L.Count(PlaceKind.Relay);
                Add("Semua relay terjangkau tanpa melewati Goal", true, lockedRelays == 0, relays + " relay, " + lockedRelays + " terkunci");
                Add("Jumlah relay sesuai (" + p.relayCount + ")", false, relays == p.relayCount, relays + " relay");
                Add("Relay di luar critical path ≥ " + p.relayOffPathMin, false, L.relaysOffPath >= p.relayOffPathMin, L.relaysOffPath + " relay");
            }
            Add("Start–Goal cukup jauh (≥ " + p.minStartGoalDistance + ")", false, L.criticalPath.Count - 1 >= p.minStartGoalDistance, (L.criticalPath.Count - 1) + " cell");
            int floor = CountFloor(L);
            Add("Enemy tidak terlalu padat (≤ 1 / 10 floor)", false, enemies <= floor / 10, enemies + " enemy / " + floor + " floor");
            Add("Item tersebar (spacing ≥ " + p.itemSpacing + ")", false, items >= Mathf.Min(p.itemCount, 1), items + " item");
            int smallest = int.MaxValue; foreach (var r in L.rooms) smallest = Mathf.Min(smallest, r.Area);
            if (L.rooms.Count > 0) Add("Room tidak terlalu kecil", false, smallest >= p.minRoomSize * p.minRoomSize, "terkecil " + smallest + " cell");
            int junctions = 0;
            for (int x = 1; x < L.width - 1; x++)
                for (int y = 1; y < L.height - 1; y++)
                    if (L.corridor[x, y] && L.IsFloor(x, y))
                    {
                        int n = 0; foreach (var d in DungeonLayout.Directions) if (L.IsFloor(new Vector2Int(x, y) + d)) n++;
                        if (n >= 3) junctions++;
                    }
            Add("Tidak terlalu linear (ada cabang/loop)", false, L.extraLoopsAdded > 0 || junctions > 0, "loop " + L.extraLoopsAdded + ", junction " + junctions);
        }

        // ================================================================ Metrics (modul §120)

        static void ComputeMetrics(DungeonLayout L)
        {
            L.floorCount = CountFloor(L);
            L.visibleWallCount = 0; L.corridorLength = 0; L.deadEnds = 0; L.reachableFloor = 0;
            for (int x = 0; x < L.width; x++)
                for (int y = 0; y < L.height; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (L.grid[x, y] == TileType.Wall)
                    {
                        // IsWallVisible (modul §69): hanya wall yang bersebelahan dengan floor yang perlu dibangun.
                        bool visible = false;
                        for (int dx = -1; dx <= 1 && !visible; dx++)
                            for (int dy = -1; dy <= 1 && !visible; dy++) if (L.IsFloor(x + dx, y + dy)) visible = true;
                        if (visible) L.visibleWallCount++;
                        continue;
                    }
                    if (L.corridor[x, y] && L.roomId[x, y] < 0) L.corridorLength++;
                    if (L.Walkable(c) && IsDeadEnd(L, c)) L.deadEnds++;
                    if (L.distance[x, y] >= 0) L.reachableFloor++;
                }
            L.startGoalPathLength = Mathf.Max(0, L.criticalPath.Count - 1);
            L.startGoalEuclid = Vector2Int.Distance(L.startPosition, L.goalPosition);
        }

        /// <summary>Peta ASCII (# wall, . floor, S start, G goal, E enemy, B boss, i item, R relay, o props, O props besar) seperti contoh di modul §3.</summary>
        public static string ToAscii(DungeonLayout L)
        {
            var sb = new System.Text.StringBuilder();
            var marks = new Dictionary<Vector2Int, char>();
            foreach (var q in L.placements)
            {
                marks[q.cell] = q.kind == PlaceKind.Enemy ? 'E' : q.kind == PlaceKind.Boss ? 'B' : q.kind == PlaceKind.Item ? 'i' : q.kind == PlaceKind.Relay ? 'R' : q.Cells > 1 ? 'O' : 'o';
                for (int k = 1; k < q.Cells; k++) marks[q.CellAt(k)] = 'O';
            }
            for (int y = L.height - 1; y >= 0; y--)
            {
                for (int x = 0; x < L.width; x++)
                {
                    var c = new Vector2Int(x, y);
                    char ch;
                    if (c == L.startPosition) ch = 'S';
                    else if (c == L.goalPosition) ch = 'G';
                    else if (marks.TryGetValue(c, out ch)) { }
                    else ch = L.grid[x, y] == TileType.Wall ? '#' : '.';
                    sb.Append(ch);
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
