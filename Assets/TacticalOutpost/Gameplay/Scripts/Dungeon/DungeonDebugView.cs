using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Outpost.Player;

namespace Outpost.Dungeon
{
    /// <summary>
    /// Panel mode Dungeon. Ringkas selalu tampil (seed + algoritma + hotkey). F10 = panel detail + peta grid
    /// (wall, floor, room, corridor, leaf BSP, critical path, start, goal, enemy, item, props, posisi player).
    /// F5 path guide, F6 seed baru, F7 ulangi seed, F9 eksperimen otomatis, F11 demo teleport dekat goal, F12 ganti BSP ↔ Random Walk.
    /// </summary>
    public sealed class DungeonDebugView : MonoBehaviour
    {
        bool details;
        GUIStyle label;
        Texture2D map;
        int mapSeed = int.MinValue;
        string lastExperiment;

        void Update()
        {
            var s = DungeonSession.Instance;
            var kb = Keyboard.current;
            if (s == null || !s.Ready || kb == null) return;
            if (kb.f10Key.wasPressedThisFrame) details = !details;
            if (kb.f5Key.wasPressedThisFrame) { s.ShowPath = !s.ShowPath; HudEvents.ShowToast(s.ShowPath ? "PATH GUIDE ON" : "PATH GUIDE OFF", new Color(1f, .82f, .35f)); }
            if (kb.f6Key.wasPressedThisFrame) DungeonMode.Restart(System.Environment.TickCount & 0x7fffff);
            if (kb.f7Key.wasPressedThisFrame) DungeonMode.Restart(s.Seed);
            if (kb.f11Key.wasPressedThisFrame) s.DemoTeleportNearGoal();   // demo pesan goal tanpa berjalan jauh
            if (kb.f12Key.wasPressedThisFrame)
            {
                DungeonMode.AlgorithmOverride = s.Algorithm == DungeonAlgorithm.BSP ? DungeonAlgorithm.RandomWalk : DungeonAlgorithm.BSP;
                DungeonMode.Restart(s.Seed);
            }
            if (kb.f9Key.wasPressedThisFrame && Time.timeScale > 0f)
            {
                lastExperiment = DungeonExperiments.Run(s);
                HudEvents.ShowToast("DUNGEON EXPERIMENTS SAVED: " + System.IO.Path.GetFileName(lastExperiment), new Color(.5f, 1f, .6f));
            }
        }

        void OnDestroy() { if (map != null) Destroy(map); }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            var s = DungeonSession.Instance;
            if (s == null || !s.Ready || s.Layout == null) return;
            var view = PlayerDeathView.Instance;
            if (view != null && view.IsActive) return;
            if (label == null) label = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = false };
            var L = s.Layout;

            if (!details)
            {
                Panel(new List<string> { "DUNGEON MODE   SEED " + s.Seed + "   " + s.Algorithm, "F10 details+map | F5 path | F6 new | F7 replay | F12 algo" }, 340f);
                return;
            }

            DrawMap(s);
            var p = s.Profile; var w = s.World;
            var lines = new List<string>
            {
                "DUNGEON MODE   SEED " + s.Seed + "   " + s.Algorithm + "   history: " + string.Join(", ", DungeonMode.SeedHistory.Skip(1).Take(4).Select(x => x.ToString()).ToArray()),
                "Grid " + L.width + " x " + L.height + "  tile " + w.tile.ToString("0.00") + " m  (" + (w.HalfX * 2f).ToString("0") + " x " + (w.HalfZ * 2f).ToString("0") + " m)",
                s.Algorithm == DungeonAlgorithm.BSP
                    ? "BSP leaf " + p.minLeafSize + "-" + p.maxLeafSize + "  room min " + p.minRoomSize + " margin " + p.roomMargin + "  corridor w" + p.corridorWidth + "  loops " + L.extraLoopsAdded
                    : "Random walk: walkers " + p.walkers + " x " + p.walkLength + " steps, brush " + p.walkBrush + ", straight " + p.walkStraightChance.ToString("0.00"),
                "Leaves " + L.leafNodes.Count + "  rooms " + L.rooms.Count + "  corridors " + L.corridors.Count + "  floor " + L.floorCount + " (" + L.FloorPercent.ToString("0") + "%)  dead-ends " + L.deadEnds,
                "Goal " + L.goalStrategy + ": BFS " + L.startGoalPathLength + " cell (" + (L.startGoalPathLength * w.tile).ToString("0") + " m), lurus " + L.startGoalEuclid.ToString("0.0"),
                "Generate-and-test: attempt " + L.attempts + "/" + p.maxGenerationAttempts + "  repair " + L.repairs + "  skipped corr " + L.corridorsSkipped + "  props removed " + L.propRemovals + "  VALID " + (L.valid ? "YES" : "NO"),
            };
            foreach (var line in L.attemptLog.Skip(Mathf.Max(0, L.attemptLog.Count - 3))) lines.Add("  " + line);
            foreach (var c in L.checks) lines.Add((c.pass ? "  [OK] " : "  [X]  ") + (c.hard ? "HARD " : "soft ") + c.name + (string.IsNullOrEmpty(c.detail) ? "" : "  (" + c.detail + ")"));
            lines.Add("NavMesh: " + w.navReport);
            lines.Add("Enemy " + L.enemyStats.accepted + "/" + L.enemyStats.requested + " + boss " + L.Count(PlaceKind.Boss) + "  rejected: " + L.enemyStats.Summary());
            lines.Add("Item " + L.itemStats.accepted + "/" + L.itemStats.requested + "  rejected: " + L.itemStats.Summary());
            var ob = s.Objectives;
            if (ob != null)
            {
                lines.Add("Relay " + ob.ActiveRelays + "/" + ob.relays.Count + " aktif  (off-path " + L.relaysOffPath + ", penjaga " + L.relayGuards + ")  rejected: " + L.relayStats.Summary());
                var locked = ob.LockedRoom;
                lines.Add("Clear room: " + ob.RoomsCleared + "/" + ob.locks.Count + " clear, " + ob.locks.Sum(x => x.doors.Count) + " pintu" + (locked != null ? "  |  room " + locked.room + " SEALED " + (Time.time - locked.lockedAt).ToString("0") + " s" : ""));
                lines.Add("Boss: " + (ob.BossDeferred ? (ob.BossSpawned ? (ob.Boss != null && ob.Boss.IsDead ? "dead" : "AWAKE") : "menunggu relay terakhir") : "spawn di awal") + "   generator " + (ob.GeneratorUnlocked ? "UNLOCKED" : "LOCKED"));
            }
            lines.Add("Props " + L.Count(PlaceKind.Prop) + "/" + L.propStats.requested + "  rejected: " + L.propStats.Summary());
            lines.Add("Alive " + s.EnemiesAlive + "/" + s.enemies.Count + "  kills " + s.Kills + "  time " + DungeonSession.FormatTime(s.ElapsedTime) + "  to goal " + Mathf.RoundToInt(s.RemainingCells * w.tile) + " m");
            lines.Add("Build " + w.gameObjectCount + " GO (floor " + w.floorObjects + ", wall " + w.wallObjects + ", column " + w.columnObjects + ", light " + w.lightObjects + ")  gen " +
                      L.generationMs.ToString("0") + " ms, build " + w.buildMs.ToString("0") + " ms, NavMesh " + w.navMs.ToString("0") + " ms");
            if (!string.IsNullOrEmpty(lastExperiment)) lines.Add("Experiments: " + lastExperiment);
            lines.Add("F10 hide | E relay | F5 path | F6 new | F7 replay | F9 experiments | F11 demo teleport | F12 BSP/RW");
            Panel(lines, 600f);
        }

        // ---------------------------------------------------------------- peta grid

        static readonly Color CWall = new Color(.07f, .08f, .09f, .92f), CFloor = new Color(.42f, .44f, .45f, .95f), CCorridor = new Color(.62f, .6f, .52f, .95f);
        static readonly Color CPath = new Color(1f, .75f, .2f, 1f), CStart = new Color(.2f, 1f, .35f, 1f), CGoal = new Color(1f, .2f, .15f, 1f);
        static readonly Color CEnemy = new Color(1f, .25f, .3f, 1f), CBoss = new Color(.8f, .2f, 1f, 1f), CItem = new Color(.3f, .85f, 1f, 1f), CProp = new Color(.55f, .35f, .18f, 1f), CRelay = new Color(.8f, 1f, .2f, 1f), CLeaf = new Color(1f, 1f, 1f, .5f);

        void BuildMap(DungeonSession s)
        {
            var L = s.Layout;
            if (map != null) Destroy(map);
            map = new Texture2D(L.width, L.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < L.width; x++)
                for (int y = 0; y < L.height; y++)
                {
                    Color c = CWall;
                    if (L.grid[x, y] != TileType.Wall)
                    {
                        c = L.roomId[x, y] >= 0 ? Color.Lerp(CFloor, Color.HSVToRGB((L.roomId[x, y] * 0.137f) % 1f, .35f, .75f), .35f) : CCorridor;
                        if (L.roomId[x, y] >= 0 && L.safeRooms.Contains(L.roomId[x, y])) c = Color.Lerp(c, CStart, .25f);
                    }
                    map.SetPixel(x, y, c);
                }
            foreach (var leaf in L.leafNodes)
            {
                for (int x = leaf.x; x < leaf.x + leaf.width; x++) { Blend(x, leaf.y); Blend(x, leaf.y + leaf.height - 1); }
                for (int y = leaf.y; y < leaf.y + leaf.height; y++) { Blend(leaf.x, y); Blend(leaf.x + leaf.width - 1, y); }
            }
            foreach (var c in L.criticalPath) map.SetPixel(c.x, c.y, Color.Lerp(map.GetPixel(c.x, c.y), CPath, .8f));
            foreach (var q in L.placements)
                map.SetPixel(q.cell.x, q.cell.y, q.kind == PlaceKind.Enemy ? CEnemy : q.kind == PlaceKind.Boss ? CBoss : q.kind == PlaceKind.Item ? CItem : q.kind == PlaceKind.Relay ? CRelay : CProp);
            map.SetPixel(L.startPosition.x, L.startPosition.y, CStart);
            map.SetPixel(L.goalPosition.x, L.goalPosition.y, CGoal);
            map.Apply();
            mapSeed = s.Seed;
        }

        void Blend(int x, int y)
        {
            if (x < 0 || y < 0 || x >= map.width || y >= map.height) return;
            map.SetPixel(x, y, Color.Lerp(map.GetPixel(x, y), CLeaf, .45f));
        }

        void DrawMap(DungeonSession s)
        {
            if (map == null || mapSeed != s.Seed) BuildMap(s);
            var L = s.Layout;
            float px = Mathf.Floor(Mathf.Min(560f / L.width, 420f / L.height));
            px = Mathf.Max(3f, px);
            var rect = new Rect(14f, Screen.height - L.height * px - 64f, L.width * px, L.height * px);
            GUI.Box(new Rect(rect.x - 6f, rect.y - 24f, rect.width + 12f, rect.height + 52f), GUIContent.none);
            GUI.Box(new Rect(rect.x - 6f, rect.y - 24f, rect.width + 12f, rect.height + 52f), GUIContent.none);
            label.normal.textColor = new Color(1f, .86f, .45f);
            GUI.Label(new Rect(rect.x, rect.y - 21f, rect.width, 18f), "GRID " + L.width + "x" + L.height + "  (white = BSP leaf, amber = critical path)", label);
            // Texture (0,0) di kiri bawah = cell (0,0) = arah -X/-Z dunia: sama dengan orientasi minimap dari atas.
            GUI.DrawTexture(rect, map);
            // Posisi player & enemy hidup (real time).
            var hp = PlayerHealth.Instance;
            foreach (var e in s.enemies)
            {
                if (e == null || e.IsDead) continue;
                Dot(rect, px, s.World.WorldToCell(e.transform.position), CEnemy, 0.9f);
            }
            if (hp != null) Dot(rect, px, s.World.WorldToCell(hp.transform.position), Color.yellow, 1.6f);
            label.normal.textColor = Color.white;
            GUI.Label(new Rect(rect.x, rect.yMax + 4f, rect.width + 200f, 18f),
                "S start  G goal  red enemy  purple boss  cyan item  lime relay  brown props  yellow = you", label);
            var o = s.Objectives;
            if (o != null)
                foreach (var r in o.relays)
                    Dot(rect, px, r.cell, r.active ? new Color(.3f, 1f, .45f) : new Color(.8f, 1f, .2f), 1.3f);
        }

        static void Dot(Rect rect, float px, Vector2Int c, Color color, float size)
        {
            var prev = GUI.color; GUI.color = color;
            float d = Mathf.Max(3f, px * size);
            GUI.DrawTexture(new Rect(rect.x + (c.x + 0.5f) * px - d * .5f, rect.yMax - (c.y + 0.5f) * px - d * .5f, d, d), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void Panel(List<string> lines, float width)
        {
            float h = 16f * lines.Count + 12f;
            var rect = new Rect(Screen.width - width - 10f, 120f, width, h);
            GUI.Box(rect, GUIContent.none);
            GUI.Box(rect, GUIContent.none);
            label.normal.textColor = new Color(1f, .86f, .45f);
            for (int i = 0; i < lines.Count; i++) GUI.Label(new Rect(rect.x + 8f, rect.y + 5f + i * 16f, width - 12f, 18f), lines[i], label);
        }
    }
}
