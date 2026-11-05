using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Outpost.Enemy;
using Outpost.PCG;
using Outpost.Player;

namespace Outpost.Dungeon
{
    /// <summary>
    /// Objektif di sepanjang dungeon (sebelum Goal):
    ///  1. POWER RELAY (lock-and-key): generator di Goal terkunci sampai semua relay aktif (tekan E di dekat relay).
    ///     Posisi relay hasil generator (DungeonGenerator.PlaceRelays) dan sudah divalidasi BFS: terjangkau tanpa melewati Goal.
    ///  2. CLEAR ROOM: masuk room berisi zombie → semua bukaan room ditutup pintu (segmen dinding naik dari lantai)
    ///     sampai zombie milik room itu habis. Relay di room yang sedang terkunci baru bisa diaktifkan setelah room bersih.
    ///  3. BOSS: boss baru muncul di Goal room setelah relay terakhir aktif lalu memburu player; generator baru bisa
    ///     dinyalakan setelah boss mati (bossMustDie).
    /// Semua objek memakai prefab existing (Electric_box_v3 untuk relay, segmen dinding untuk pintu). Lampu indikator relay
    /// adalah komponen Light (merah = mati, hijau = aktif).
    /// </summary>
    public sealed class DungeonObjectives : MonoBehaviour
    {
        public sealed class Relay
        {
            public Vector2Int cell;
            public int room;
            public GameObject go;
            public Light indicator;
            public bool active;
        }

        public sealed class Door
        {
            public Transform visual;
            public Collider[] colliders;
            public NavMeshObstacle obstacle;
            public float closedY, openY;
        }

        public sealed class RoomLock
        {
            public int room;
            public readonly List<EnemyHealth> enemies = new List<EnemyHealth>();
            public readonly List<Door> doors = new List<Door>();
            public bool locked, cleared;
            public float lockedAt;
            public int timesLocked;
        }

        DungeonSession s;
        public readonly List<Relay> relays = new List<Relay>();
        public readonly List<RoomLock> locks = new List<RoomLock>();
        public EnemyHealth Boss { get; private set; }
        public bool BossSpawned { get; private set; }
        public bool BossDeferred { get; private set; }
        public string Prompt { get; private set; }
        public int ActiveRelays { get { int n = 0; foreach (var r in relays) if (r.active) n++; return n; } }
        public bool RelaysDone { get { return ActiveRelays >= relays.Count; } }
        public bool BossDone { get { return !s.Profile.bossMustDie || (!BossDeferred && Boss == null) || (Boss != null && Boss.IsDead) || (BossDeferred && BossSpawned && Boss == null); } }
        public bool GeneratorUnlocked { get { return RelaysDone && BossDone; } }
        public RoomLock LockedRoom { get { foreach (var l in locks) if (l.locked) return l; return null; } }
        public int RoomsCleared { get { int n = 0; foreach (var l in locks) if (l.cleared) n++; return n; } }

        static readonly System.Reflection.MethodInfo hearGunfire =
            typeof(EnemyBrain).GetMethod("HearGunfire", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        static readonly Color RelayOff = new Color(1f, 0.15f, 0.1f), RelayOn = new Color(0.3f, 1f, 0.45f);

        public void Setup(DungeonSession session, EnemyVariant bossVariant, float bossScale, Vector2Int? bossCell)
        {
            s = session;
            bossVariantCache = bossVariant; this.bossScale = bossScale; this.bossCell = bossCell;
            BossDeferred = s.Profile.bossAfterRelays && bossCell.HasValue && bossVariant != null && s.Layout.Count(PlaceKind.Relay) > 0;
            if (!BossDeferred) Boss = s.enemies.FirstOrDefault(e => e != null && e.config != null && e.config.kind == EnemyKind.Boss);

            // Relay: objek sudah dibangun builder (ikut NavMesh). Tambah lampu indikator.
            foreach (var q in s.Layout.placements)
            {
                if (q.kind != PlaceKind.Relay) continue;
                GameObject go;
                s.World.relayObjects.TryGetValue(q.cell, out go);
                var r = new Relay { cell = q.cell, room = q.room, go = go };
                if (go != null)
                {
                    var b = PcgArena.Measure(go);
                    var lightGo = new GameObject("Relay indicator light");
                    lightGo.transform.SetParent(go.transform, true);
                    lightGo.transform.position = new Vector3(b.center.x, b.max.y + 0.4f, b.center.z);
                    r.indicator = lightGo.AddComponent<Light>();
                    r.indicator.type = LightType.Point; r.indicator.range = 7f; r.indicator.intensity = 4f; r.indicator.color = RelayOff;
                    r.indicator.shadows = LightShadows.None;
                }
                relays.Add(r);
            }
            if (relays.Count > 0) SetGeneratorLights(new Color(1f, 0.12f, 0.08f), 1f);

            // Clear room: room selain Start/Goal yang punya zombie.
            if (s.Profile.clearRoomLock && s.Layout.algorithm == DungeonAlgorithm.BSP)
            {
                var doorRoot = new GameObject("Clear-room doors").transform; doorRoot.SetParent(s.World.root, false);
                foreach (int room in DungeonGenerator.CombatRooms(s.Layout))
                {
                    var l = new RoomLock { room = room };
                    foreach (var kv in s.roomEnemies.Where(k => k.Value == room)) l.enemies.Add(kv.Key);
                    if (l.enemies.Count == 0) continue;
                    foreach (var edge in DungeonGenerator.DoorEdges(s.Layout, room)) l.doors.Add(BuildDoor(edge.Key, edge.Value, doorRoot));
                    locks.Add(l);
                }
            }
            Debug.Log("[DUNGEON] Objektif: relay " + relays.Count + " (off-path " + s.Layout.relaysOffPath + ", penjaga " + s.Layout.relayGuards + ") | clear room " + locks.Count +
                      " room, " + locks.Sum(l => l.doors.Count) + " pintu | boss " + (BossDeferred ? "muncul setelah relay terakhir" : "langsung") + (s.Profile.bossMustDie ? ", wajib dikalahkan" : ""), this);
        }

        EnemyVariant bossVariantCache;
        float bossScale;
        Vector2Int? bossCell;

        // ---------------------------------------------------------------- pintu

        Door BuildDoor(Vector2Int roomCell, Vector2Int outCell, Transform parent)
        {
            var w = s.World;
            var prefab = s.Profile.doorPrefab != null ? s.Profile.doorPrefab : s.Profile.wallSpan;
            Vector3 pos = (w.CellToWorld(roomCell) + w.CellToWorld(outCell)) * 0.5f;
            bool runsAlongZ = outCell.x != roomCell.x;
            Vector3 size = w.helper.SizeOf(prefab);
            bool spanAlongX = size.x >= size.z;
            float yaw = runsAlongZ ? (spanAlongX ? 90f : 0f) : (spanAlongX ? 0f : 90f);
            var go = w.helper.Place(prefab, pos, yaw, parent);
            PcgArena.EnsureSolid(go);
            var t = DungeonBuilder.Stretch(go, pos, runsAlongZ ? 90f : 0f, new Vector3(w.tile / Mathf.Max(0.1f, Mathf.Max(size.x, size.z)), 1f, 1f), parent);
            t.name = "Door room " + s.Layout.RoomAt(roomCell) + " @" + roomCell.x + "," + roomCell.y;
            float height = PcgArena.Measure(t.gameObject).size.y;

            var obstacleGo = new GameObject("Door NavMesh obstacle");
            obstacleGo.transform.SetParent(parent, false);
            obstacleGo.transform.position = pos + Vector3.up * height * 0.5f;
            var obstacle = obstacleGo.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = runsAlongZ ? new Vector3(0.6f, height, w.tile) : new Vector3(w.tile, height, 0.6f);
            obstacle.carving = true; obstacle.carveOnlyStationary = false;
            obstacle.enabled = false;

            var door = new Door { visual = t, colliders = t.GetComponentsInChildren<Collider>(true), obstacle = obstacle, closedY = t.position.y, openY = t.position.y - height - 0.3f };
            SetDoor(door, false, true);
            return door;
        }

        static void SetDoor(Door d, bool closed, bool instant)
        {
            if (d.visual == null) return;
            foreach (var c in d.colliders) if (c != null) c.enabled = closed;
            if (d.obstacle != null) d.obstacle.enabled = closed;
            if (closed) d.visual.gameObject.SetActive(true);
            if (instant)
            {
                var p = d.visual.position; p.y = closed ? d.closedY : d.openY; d.visual.position = p;
                if (!closed) d.visual.gameObject.SetActive(false);
            }
        }

        void AnimateDoors()
        {
            foreach (var l in locks)
                foreach (var d in l.doors)
                {
                    if (d.visual == null || !d.visual.gameObject.activeSelf) continue;
                    float target = l.locked ? d.closedY : d.openY;
                    var p = d.visual.position;
                    p.y = Mathf.MoveTowards(p.y, target, Time.deltaTime * 6f);
                    d.visual.position = p;
                    if (!l.locked && Mathf.Approximately(p.y, d.openY)) d.visual.gameObject.SetActive(false);
                }
        }

        // ---------------------------------------------------------------- loop

        void Update()
        {
            if (s == null || !s.Ready) return;
            Prompt = null;
            AnimateDoors();
            var hp = PlayerHealth.Instance;
            if (hp == null || hp.IsDead || s.GoalReached) return;
            var cell = s.World.WorldToCell(hp.transform.position);
            UpdateRoomLocks(cell, hp.transform.position);
            UpdateRelays(hp.transform.position);
            if (!GeneratorUnlocked && s.NearGoal(hp.transform.position))
                Prompt = !RelaysDone ? "GENERATOR LOCKED  //  ACTIVATE RELAYS " + ActiveRelays + "/" + relays.Count : "ELIMINATE THE BOSS FIRST";
        }

        void UpdateRoomLocks(Vector2Int playerCell, Vector3 playerPos)
        {
            foreach (var l in locks)
            {
                if (l.cleared) continue;
                int alive = AliveInside(l);
                if (!l.locked)
                {
                    if (alive == 0) { l.cleared = true; continue; }                   // dibersihkan dari luar
                    var r = s.Layout.rooms[l.room];
                    bool inside = playerCell.x > r.x && playerCell.x < r.x + r.width - 1 && playerCell.y > r.y && playerCell.y < r.y + r.height - 1;
                    if (!inside) continue;
                    l.locked = true; l.lockedAt = Time.time; l.timesLocked++;
                    foreach (var d in l.doors) SetDoor(d, true, false);
                    foreach (var e in l.enemies) Alert(e, playerPos);                  // zombie room langsung sadar posisi player
                    HudEvents.ShowToast("ROOM SEALED  //  ELIMINATE " + alive + " CONTACTS", new Color(1f, .3f, .2f));
                    Debug.Log("[DUNGEON] Clear room: room " + l.room + " terkunci, " + alive + " zombie, " + l.doors.Count + " pintu.", this);
                }
                else if (alive == 0 || Time.time - l.lockedAt > s.Profile.lockTimeout)
                {
                    bool timeout = alive > 0;
                    l.locked = false; l.cleared = true;
                    foreach (var d in l.doors) SetDoor(d, false, false);
                    HudEvents.ShowToast(timeout ? "ROOM LOCK TIMEOUT  //  DOORS OPEN" : "ROOM CLEARED  //  DOORS OPEN", new Color(.5f, 1f, .6f));
                    Debug.Log("[DUNGEON] Clear room: room " + l.room + (timeout ? " dibuka karena timeout." : " bersih, pintu terbuka (" + (Time.time - l.lockedAt).ToString("0") + " s)."), this);
                }
            }
        }

        /// <summary>Zombie milik room yang masih hidup dan masih berada di room (± 1 cell).</summary>
        int AliveInside(RoomLock l)
        {
            var r = s.Layout.rooms[l.room];
            int n = 0;
            foreach (var e in l.enemies)
            {
                if (e == null || e.IsDead) continue;
                var c = s.World.WorldToCell(e.transform.position);
                if (c.x >= r.x - 1 && c.x <= r.x + r.width && c.y >= r.y - 1 && c.y <= r.y + r.height) n++;
            }
            return n;
        }

        static void Alert(EnemyHealth e, Vector3 position)
        {
            if (e == null || e.IsDead || hearGunfire == null) return;
            var brain = e.GetComponent<EnemyBrain>();
            if (brain != null && brain.enabled) hearGunfire.Invoke(brain, new object[] { position });
        }

        void UpdateRelays(Vector3 playerPos)
        {
            Relay near = null; float best = float.MaxValue;
            foreach (var r in relays)
            {
                if (r.active) continue;
                float d = RelayDistance(r, playerPos);
                if (d < best) { best = d; near = r; }
            }
            if (near == null || best > s.Profile.relayInteractRadius) return;
            var lockOfRoom = locks.Find(l => l.room == near.room && l.locked);
            if (lockOfRoom != null) { Prompt = "CLEAR THE ROOM FIRST  //  " + AliveInside(lockOfRoom) + " CONTACTS"; return; }
            Prompt = "[E]  ACTIVATE POWER RELAY  " + (ActiveRelays + 1) + "/" + relays.Count;
            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) Activate(near);
        }

        void Activate(Relay r)
        {
            r.active = true;
            if (r.indicator != null) { r.indicator.color = RelayOn; r.indicator.intensity = 6f; }
            HudEvents.ShowToast("POWER RELAY " + ActiveRelays + "/" + relays.Count + " ONLINE", new Color(.5f, 1f, .6f));
            Debug.Log("[DUNGEON] Relay " + r.cell + " aktif (" + ActiveRelays + "/" + relays.Count + ") pada " + DungeonSession.FormatTime(s.ElapsedTime), this);
            if (!RelaysDone) return;
            SetGeneratorLights(new Color(1f, 0.75f, 0.3f), 1.4f);                   // generator siap (amber)
            if (BossDeferred && !BossSpawned) SpawnBoss();
            else HudEvents.ShowToast("ALL RELAYS ONLINE  //  RESTORE THE GENERATOR", new Color(1f, .82f, .35f));
        }

        // ---------------------------------------------------------------- boss

        void SpawnBoss()
        {
            BossSpawned = true;
            var go = EnemyDebugSpawner.SpawnAt(bossVariantCache.prefab, s.World.CellToWorld(bossCell.Value));
            if (go == null) { BossDeferred = false; return; }
            go.name = "Boss " + bossVariantCache.id + " (bangun setelah relay)";
            var hp = go.GetComponent<EnemyHealth>();
            if (hp != null && hp.config != null) go.transform.localScale = Vector3.one * (bossScale / Mathf.Max(0.01f, hp.config.visualScale));
            Boss = hp;
            if (hp != null) s.enemies.Add(hp);
            var player = PlayerHealth.Instance;
            if (player != null) Alert(hp, player.transform.position);                 // boss langsung memburu player
            HudEvents.ShowToast("BOSS AWAKE  //  IT IS HUNTING YOU", new Color(1f, .2f, .15f));
            Debug.Log("[DUNGEON] Boss muncul di Goal room " + bossCell.Value + " setelah semua relay aktif.", this);
        }

        void SetGeneratorLights(Color c, float intensityMul)
        {
            var goal = s.World.root.Find("Goal (generator)");
            if (goal == null) return;
            foreach (var l in goal.GetComponentsInChildren<Light>())
            {
                if (!baseIntensity.ContainsKey(l)) baseIntensity[l] = l.intensity;
                l.color = c; l.intensity = baseIntensity[l] * intensityMul;
            }
        }

        readonly Dictionary<Light, float> baseIntensity = new Dictionary<Light, float>();

        /// <summary>Target path guide: relay belum aktif terdekat (jarak BFS), lalu Goal.</summary>
        public Vector2Int GuideTarget(Vector2Int from, List<Vector2Int> buffer)
        {
            Vector2Int target = s.Layout.goalPosition; int best = int.MaxValue;
            foreach (var r in relays)
            {
                if (r.active) continue;
                if (DungeonGenerator.FindPath(s.Layout, from, r.cell, buffer) && buffer.Count < best) { best = buffer.Count; target = r.cell; }
            }
            return target;
        }

        public string ObjectiveText()
        {
            var l = LockedRoom;
            if (l != null) return "ROOM SEALED  //  CLEAR " + AliveInside(l) + " CONTACTS";
            if (!RelaysDone) return "ACTIVATE POWER RELAYS " + ActiveRelays + "/" + relays.Count;
            if (!BossDone) return "BOSS AWAKE  //  ELIMINATE IT";
            return "RESTORE THE GENERATOR";
        }

        /// <summary>Demo (F11): ke relay belum aktif terdekat, atau ke dekat Goal bila semua relay sudah aktif.</summary>
        public bool DemoTarget(out Vector3 position, out float yaw)
        {
            position = Vector3.zero; yaw = 0f;
            foreach (var r in relays)
                if (!r.active)
                {
                    foreach (var d in DungeonLayout.Directions)
                    {
                        if (!s.Layout.Walkable(r.cell + d)) continue;
                        Vector3 center = s.World.CellToWorld(r.cell);
                        position = center + new Vector3(d.x, 0f, d.y) * s.World.tile * 0.55f;   // tepat di samping relay, masih di cell relay
                        Vector3 look = center - position;
                        yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
                        return true;
                    }
                }
            return false;
        }

        /// <summary>Jarak datar player ke badan relay (bukan ke titik tengah cell).</summary>
        float RelayDistance(Relay r, Vector3 p)
        {
            if (r.go == null) return PcgArena.Flat(s.World.CellToWorld(r.cell) - p).magnitude;
            var b = PcgArena.Measure(r.go);
            return PcgArena.Flat(b.ClosestPoint(new Vector3(p.x, b.center.y, p.z)) - p).magnitude;
        }
    }
}
