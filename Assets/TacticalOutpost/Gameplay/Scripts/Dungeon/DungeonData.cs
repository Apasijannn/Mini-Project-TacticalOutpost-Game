using System.Collections.Generic;
using UnityEngine;

namespace Outpost.Dungeon
{
    /// <summary>Isi satu cell grid (modul §31).</summary>
    public enum TileType { Wall, Floor, Start, Goal }

    /// <summary>Rectangle room (modul §32).</summary>
    [System.Serializable]
    public class DungeonRoom
    {
        public int x, y, width, height;
        public int index;
        public DungeonRoom(int x, int y, int width, int height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public Vector2Int Center { get { return new Vector2Int(x + width / 2, y + height / 2); } }
        public bool Contains(Vector2Int c) { return c.x >= x && c.x < x + width && c.y >= y && c.y < y + height; }
        public int Area { get { return width * height; } }
    }

    /// <summary>Node BSP (modul §35). Leaf = node tanpa anak.</summary>
    public class BSPNode
    {
        public int x, y, width, height, depth;
        public BSPNode left, right;
        public DungeonRoom room;
        public BSPNode(int x, int y, int width, int height, int depth = 0) { this.x = x; this.y = y; this.width = width; this.height = height; this.depth = depth; }
        public bool IsLeaf { get { return left == null && right == null; } }
    }

    public enum PlaceKind { Enemy, Boss, Item, Prop, Relay }

    /// <summary>Alasan kandidat ditolak saat placement (constraint-based PCG).</summary>
    public enum DRule { Accepted, NotFloor, NearStart, SafeRoom, GoalArea, Occupied, OnCriticalPath, Doorway, NotOpen, Spacing, Unreachable, NoRoom }

    public struct DungeonPlacement
    {
        public PlaceKind kind;
        public Vector2Int cell;
        public string id;      // id enemy (walker/runner/...) atau "Ammo"/"Health" atau index prefab props
        public int room;       // -1 = corridor / tidak di room
        public float yaw;
        public Vector2Int wallDir;   // props dekorasi: arah dinding terdekat (0,0 = props cover di tengah)
        public int span;             // props besar (container dsb.): jumlah cell sepanjang dinding (0/1 = satu cell)
        public Vector2Int spanDir;   // arah deretan cell props besar

        public int Cells { get { return span > 1 ? span : 1; } }
        public Vector2Int CellAt(int k) { return cell + spanDir * k; }
    }

    public struct DungeonCheck
    {
        public string name;
        public bool hard, pass;
        public string detail;
    }

    public sealed class DRuleStats
    {
        public int requested, accepted, attempts;
        public readonly int[] rejected = new int[System.Enum.GetValues(typeof(DRule)).Length];
        public void Reject(DRule r) { rejected[(int)r]++; }
        public string Summary()
        {
            var parts = new List<string>();
            for (int i = 1; i < rejected.Length; i++) if (rejected[i] > 0) parts.Add((DRule)i + " " + rejected[i]);
            return parts.Count == 0 ? "none" : string.Join(", ", parts.ToArray());
        }
    }

    /// <summary>Hasil satu kali generation: grid + BSP + room + start/goal + placement + validasi + metrics.</summary>
    public sealed class DungeonLayout
    {
        public int seed, width, height;
        public DungeonAlgorithm algorithm;
        public GoalStrategy goalStrategy;
        public TileType[,] grid;
        public bool[,] corridor;        // cell hasil carve corridor (bukan bagian room)
        public int[,] roomId;           // -1 = bukan room
        public bool[,] blocked;         // cell ditempati props padat
        public bool[,] goalArea;        // footprint generator goal (tidak boleh ditempati objek lain)
        public int[,] distance;         // jarak BFS dari Start (-1 = tidak terjangkau)

        public BSPNode root;
        public readonly List<BSPNode> leafNodes = new List<BSPNode>();
        public readonly List<DungeonRoom> rooms = new List<DungeonRoom>();
        public readonly List<Vector2Int[]> corridors = new List<Vector2Int[]>();   // pasangan titik corridor L (from, corner, to)

        public Vector2Int startPosition, goalPosition;
        public int startRoom = -1, goalRoom = -1;
        public readonly List<Vector2Int> criticalPath = new List<Vector2Int>();
        public readonly List<DungeonPlacement> placements = new List<DungeonPlacement>();
        public readonly HashSet<int> safeRooms = new HashSet<int>();

        // Statistik generate-and-test
        public int attempts, repairs, corridorsSkipped, propRemovals, extraLoopsAdded;
        public bool valid;
        public string failReason = "";
        public readonly List<string> attemptLog = new List<string>();
        public readonly List<DungeonCheck> checks = new List<DungeonCheck>();
        public readonly DRuleStats enemyStats = new DRuleStats(), itemStats = new DRuleStats(), propStats = new DRuleStats(), relayStats = new DRuleStats();
        public int relaysOffPath, relayGuards;

        // Metrics (modul §120)
        public int floorCount, visibleWallCount, corridorLength, deadEnds, startGoalPathLength, reachableFloor;
        public float startGoalEuclid, generationMs;

        public DungeonLayout(int w, int h)
        {
            width = w; height = h;
            grid = new TileType[w, h];
            corridor = new bool[w, h];
            roomId = new int[w, h];
            blocked = new bool[w, h];
            goalArea = new bool[w, h];
            distance = new int[w, h];
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) { grid[x, y] = TileType.Wall; roomId[x, y] = -1; }
        }

        public bool IsInsideGrid(int x, int y) { return x >= 0 && x < width && y >= 0 && y < height; }
        public bool IsInsideGrid(Vector2Int c) { return IsInsideGrid(c.x, c.y); }
        public bool IsFloor(int x, int y) { return IsInsideGrid(x, y) && grid[x, y] != TileType.Wall; }
        public bool IsFloor(Vector2Int c) { return IsFloor(c.x, c.y); }
        public bool Walkable(Vector2Int c) { return IsFloor(c) && !blocked[c.x, c.y]; }

        public int RoomAt(Vector2Int c) { return IsInsideGrid(c) ? roomId[c.x, c.y] : -1; }
        public int Distance(Vector2Int c) { return IsInsideGrid(c) ? distance[c.x, c.y] : -1; }

        public int Count(PlaceKind k) { int n = 0; foreach (var p in placements) if (p.kind == k) n++; return n; }

        public bool Occupied(Vector2Int c, int radius)
        {
            foreach (var p in placements)
                for (int k = 0; k < p.Cells; k++)
                {
                    var q = p.CellAt(k);
                    if (Mathf.Abs(q.x - c.x) <= radius && Mathf.Abs(q.y - c.y) <= radius) return true;
                }
            return false;
        }

        public float FloorPercent { get { return 100f * floorCount / Mathf.Max(1, width * height); } }

        /// <summary>Hash layout: grid + start/goal + placement. Seed sama → hash sama (uji reproducibility).</summary>
        public int LayoutHash()
        {
            unchecked
            {
                int h = 17;
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                        h = h * 31 + (int)grid[x, y] + (blocked[x, y] ? 7 : 0);
                h = h * 31 + startPosition.x * 1000 + startPosition.y;
                h = h * 31 + goalPosition.x * 1000 + goalPosition.y;
                foreach (var p in placements) h = h * 31 + (int)p.kind * 100000 + p.cell.x * 100 + p.cell.y + (p.id != null ? p.id.Length : 0);
                return h;
            }
        }

        public static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
    }
}
