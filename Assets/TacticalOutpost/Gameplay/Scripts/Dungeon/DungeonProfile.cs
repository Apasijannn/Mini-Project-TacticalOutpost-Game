using System;
using UnityEngine;
using Outpost.PCG;

namespace Outpost.Dungeon
{
    public enum DungeonAlgorithm { BSP, RandomWalk }

    /// <summary>Eksperimen 6 modul: A = room pertama → room terakhir, B = room terjauh (jarak lurus), C = room terjauh (jarak BFS).</summary>
    public enum GoalStrategy { A_FirstToLast, B_GeometricFarthest, C_PathFarthest }

    [Serializable]
    public class DungeonEnemyEntry
    {
        [Tooltip("id varian di EnemyCatalog game (walker, runner, skinless, bloater).")]
        public string id;
        [Min(0f)] public float weight = 1f;
    }

    /// <summary>
    /// Parameter mode Procedural Dungeon (Modul Praktikum 10). Nilai default = nilai di modul
    /// (mapWidth 60, mapHeight 40, minLeafSize 12, maxLeafSize 20, minRoomSize 5, roomMargin 2, enemyCount 8, itemCount 5, safeRoomCount 1).
    /// Semua prefab adalah asset yang sudah ada di project. Field prefab yang kosong diisi otomatis dari PCG_Profile.
    /// </summary>
    [CreateAssetMenu(menuName = "Outpost/Dungeon Profile", fileName = "Dungeon_Profile")]
    public class DungeonProfile : ScriptableObject
    {
        public const string ResourcePath = "Dungeon/Dungeon_Profile";

        [Header("Generation")]
        [Tooltip("True = seed baru setiap masuk mode. False = selalu memakai 'seed'.")]
        public bool useRandomSeed = true;
        public int seed = 12345;
        public DungeonAlgorithm algorithm = DungeonAlgorithm.BSP;
        [Tooltip("Generate-and-test: batas jumlah generate ulang bila validasi gagal setelah repair.")]
        [Min(1)] public int maxGenerationAttempts = 10;

        [Header("Dungeon Size (cell)")]
        [Min(16)] public int mapWidth = 60;
        [Min(16)] public int mapHeight = 40;
        [Tooltip("Ukuran 1 cell di dunia Unity (m). 0 = otomatis = panjang 1 segmen dinding, jadi dinding tidak perlu di-scale.")]
        [Min(0f)] public float tileSize = 0f;

        [Header("BSP Settings")]
        [Min(4)] public int minLeafSize = 12;
        [Min(6)] public int maxLeafSize = 20;

        [Header("Room Settings")]
        [Min(3)] public int minRoomSize = 5;
        [Min(1)] public int roomMargin = 2;

        [Header("Corridor")]
        [Tooltip("Lebar corridor dalam cell (modul §115 Corridor Width 2).")]
        [Range(1, 3)] public int corridorWidth = 1;
        [Tooltip("Corridor tambahan antar-room terdekat supaya dungeon tidak terlalu linear (soft constraint).")]
        [Min(0)] public int extraLoops = 1;
        [Tooltip("Uji repair: peluang sebuah corridor BSP sengaja dilewati. 0 = normal.")]
        [Range(0f, 1f)] public float skipCorridorChance = 0f;

        [Header("Random Walk (algoritma alternatif)")]
        [Min(1)] public int walkers = 4;
        [Tooltip("Jumlah langkah tiap walker.")]
        [Min(10)] public int walkLength = 450;
        [Tooltip("Lebar jejak walker (cell).")]
        [Range(1, 3)] public int walkBrush = 2;
        [Tooltip("Peluang walker tetap lurus (semakin besar = lorong lebih panjang).")]
        [Range(0f, 0.95f)] public float walkStraightChance = 0.55f;

        [Header("Start / Goal")]
        public GoalStrategy goalStrategy = GoalStrategy.C_PathFarthest;
        [Tooltip("Soft constraint: jarak BFS start → goal minimal (cell).")]
        [Min(0)] public int minStartGoalDistance = 25;
        [Tooltip("Radius safe zone di sekitar start (cell): bebas enemy dan props.")]
        [Min(1)] public int safeRadiusCells = 3;

        [Header("Spawn Settings")]
        [Tooltip("True = jumlah zombie per room bertingkat menurut jarak BFS dari Start (zombieTiers). False = enemyCount / density acak seperti modul.")]
        public bool zombiesByDistance = true;
        [Tooltip("Jumlah zombie per room dari room terdekat → terjauh (dibagi rata ke beberapa tingkat). Room Start/safe room selalu 0.")]
        public int[] zombieTiers = { 3, 5, 7, 9 };
        [Tooltip("Seberapa kuat room jauh condong ke zombie berat (runner/skinless/bloater). 0 = sama semua.")]
        [Range(0f, 3f)] public float heavyBiasFar = 1.5f;
        [Tooltip("Zombie room baru di-spawn saat player masuk room. Cell spawn minimal sejauh ini (cell) dari player.")]
        [Min(0f)] public float spawnMinPlayerCells = 4f;
        [Tooltip("Random Walk (tanpa room): zombie di-spawn saat player berada dalam radius ini (cell).")]
        [Min(1f)] public float looseSpawnCells = 7f;
        [Min(0)] public int enemyCount = 8;
        [Tooltip("Modul §107: jumlah enemy = floor / floorTilesPerEnemy (bukan angka absolut).")]
        public bool useEnemyDensity = false;
        [Min(5)] public int floorTilesPerEnemy = 50;
        [Min(0)] public int itemCount = 5;
        [Tooltip("Room awal (dan room terdekat berikutnya) yang tidak boleh berisi enemy.")]
        [Min(0)] public int safeRoomCount = 1;
        [Tooltip("Enemy tidak boleh lebih dekat dari ini ke Start (jarak BFS, cell). Modul: 8.")]
        [Min(0)] public int minEnemyDistance = 8;
        [Tooltip("Pengembangan Boss Room: boss hanya muncul di Goal Room.")]
        public bool bossRoom = true;
        [Tooltip("Pengembangan Treasure: item diutamakan di dead-end dan room tanpa Start/Goal.")]
        public bool treasureRule = true;
        [Min(1)] public int itemSpacing = 4;
        public DungeonEnemyEntry[] enemyTable =
        {
            new DungeonEnemyEntry { id = "walker",   weight = 50f },
            new DungeonEnemyEntry { id = "runner",   weight = 25f },
            new DungeonEnemyEntry { id = "skinless", weight = 15f },
            new DungeonEnemyEntry { id = "bloater",  weight = 10f },
        };
        public SupplyEntry[] itemTable = { new SupplyEntry { kind = SupplyKind.Ammo, weight = 60f }, new SupplyEntry { kind = SupplyKind.Health, weight = 40f } };

        [Header("Objective 1: power relay (lock-and-key)")]
        [Tooltip("Generator di Goal terkunci sampai semua relay aktif. 0 = tanpa relay.")]
        [Range(0, 6)] public int relayCount = 3;
        [Tooltip("Jarak minimum antar-relay dan dari Start (cell).")]
        [Min(1)] public int relayMinSpacing = 10;
        [Tooltip("Minimal sekian relay harus di room yang TIDAK dilewati critical path (memaksa eksplorasi).")]
        [Min(0)] public int relayOffPathMin = 1;
        [Tooltip("Zombie penjaga per relay (di room yang sama).")]
        [Min(0)] public int relayGuards = 1;
        [Tooltip("Jarak (m) dari badan relay untuk menekan E.")]
        public float relayInteractRadius = 2.2f;

        [Header("Objective 2: clear room")]
        [Tooltip("Masuk room berisi zombie → semua pintu room tertutup sampai zombie di room itu habis.")]
        public bool clearRoomLock = true;
        [Tooltip("Pengaman anti-softlock: pintu terbuka sendiri setelah sekian detik.")]
        [Min(10f)] public float lockTimeout = 120f;

        [Header("Objective 3: boss")]
        [Tooltip("Boss baru muncul (dan memburu player) setelah relay terakhir aktif.")]
        public bool bossAfterRelays = true;
        [Tooltip("Generator baru bisa dinyalakan setelah boss mati.")]
        public bool bossMustDie = true;

        [Header("Cover props di dalam room (tidak boleh menghalangi jalur wajib)")]
        [Min(0)] public int propsPerRoom = 2;
        [Tooltip("Tambahan 1 props cover per sekian cell luas room (room besar lebih terisi).")]
        [Min(1)] public int coverCellsPerExtra = 30;
        [Min(0)] public int maxCoverPerRoom = 3;
        [Tooltip("Props dekorasi menempel dinding room (barrel, pallet, dumpster, barrier) supaya room tidak kosong.")]
        [Min(0)] public int wallPropsPerRoom = 2;
        [Min(1)] public int wallCellsPerExtra = 24;
        [Min(0)] public int maxWallPropsPerRoom = 3;
        [Tooltip("Peluang tiap room mendapat 1 props besar (container, dumpster, road block) yang menempel dinding; room luas bisa 2.")]
        [Range(0f, 1f)] public float largePropChance = 0.75f;
        [Min(1)] public int largePropSecondCells = 60;
        [Tooltip("Diisi otomatis oleh DungeonBuilder.PropAllowed: panjang props (cell) di sepanjang dinding. 1 = props biasa.")]
        [HideInInspector] public int[] propSpan;
        [Tooltip("Props tidak boleh berada sejauh ini (cell) dari critical path start → goal.")]
        [Min(0)] public int pathBuffer = 1;

        [Header("Prefab (asset existing; kosong = ambil dari PCG_Profile)")]
        public GameObject floorTile;
        public GameObject wallSpan;
        public GameObject wallColumn;
        public GameObject goalPrefab;
        public GameObject lamp;
        [Tooltip("Penerangan di tikungan corridor (mis. burning barrel).")]
        public GameObject corridorLight;
        [Tooltip("Model relay (default: Electric_box_v3 dari PCG_Profile).")]
        public GameObject relayPrefab;
        [Tooltip("Pintu room clear (kosong = segmen dinding yang naik dari lantai).")]
        public GameObject doorPrefab;
        [Header("Ikon minimap objektif")]
        [Tooltip("Ikon relay di minimap (mm_dot). Kosong = ikon pickup.")]
        public Sprite mapRelayIcon;
        [Tooltip("Ikon generator di minimap (mm_gen, sama dengan Mini Project). Kosong = ikon generator scene game.")]
        public Sprite mapGeneratorIcon;
        [Min(0f)] public float mapRelayIconSize = 0.85f;
        [Min(0f)] public float mapGeneratorIconSize = 1.3f;
        [Tooltip("Tambahan ukuran ikon ammo di minimap (ikon kotak ammo tipis, jadi sedikit diperbesar).")]
        [Min(0f)] public float mapAmmoIconSize = 0.35f;
        [Min(4)] public int corridorLightSpacing = 7;
        public PrefabEntry[] propTable = new PrefabEntry[0];

        [Header("Pencahayaan dungeon")]
        [Tooltip("Lampu pengisi per room (komponen Light tanpa mesh), satu per kotak ~roomLightSpacing cell.")]
        public bool roomFillLights = true;
        [Min(2)] public int roomLightSpacing = 4;
        public Color roomLightColor = new Color(1f, 0.88f, 0.7f);
        [Min(0f)] public float roomLightIntensity = 17f;
        [Min(1f)] public float roomLightHeight = 5f;
        [Tooltip("Lampu pengisi di corridor setiap sekian cell.")]
        [Min(2)] public int corridorFillSpacing = 5;
        [Min(0f)] public float corridorLightIntensity = 10f;
        [Tooltip("Pengali directional light (cahaya bulan bawaan scene PCG) selama mode Dungeon.")]
        [Min(0f)] public float moonBoost = 2.2f;
        [Tooltip("Pengali ambient bawaan scene PCG selama mode Dungeon.")]
        [Min(1f)] public float ambientBoost = 2.5f;

        [Header("Visual helper")]
        public Color safeZoneColor = new Color(0.15f, 1f, 0.35f);
        public Color pathColor = new Color(1f, 0.72f, 0.2f);
        public float lineGlow = 4f;
        public float lineWidth = 0.14f;

        /// <summary>Isi field prefab yang kosong dari PCG_Profile (asset yang sama dengan mode PCG).</summary>
        public void FillFrom(PcgProfile pcg)
        {
            if (pcg == null) return;
            if (floorTile == null) floorTile = pcg.floorTile;
            if (wallSpan == null) wallSpan = pcg.fenceSpan;
            if (wallColumn == null) wallColumn = pcg.fenceEnd;
            if (goalPrefab == null) goalPrefab = pcg.generator;
            if (lamp == null) lamp = pcg.lamp;
            if (relayPrefab == null && pcg.generatorProps != null)
                foreach (var g in pcg.generatorProps) if (g != null) { relayPrefab = g; break; }
            if (corridorLight == null && pcg.obstacleTable != null)
                foreach (var e in pcg.obstacleTable)
                    if (e != null && e.prefab != null && e.prefab.name.Contains("Burning")) { corridorLight = e.prefab; break; }
            if (propTable == null || propTable.Length == 0)
            {
                var list = new System.Collections.Generic.List<PrefabEntry>();
                if (pcg.obstacleTable != null) foreach (var e in pcg.obstacleTable) if (e != null && e.prefab != null) list.Add(e);
                if (pcg.dressingTable != null) foreach (var e in pcg.dressingTable) if (e != null && e.prefab != null) list.Add(e);
                propTable = list.ToArray();
            }
        }

        public static DungeonProfile Load()
        {
            var p = Resources.Load<DungeonProfile>(ResourcePath);
            p = p != null ? Instantiate(p) : CreateInstance<DungeonProfile>();   // salinan runtime: asset tidak berubah saat Play
            p.name = "Dungeon_Profile (runtime)";
            p.FillFrom(Resources.Load<PcgProfile>(PcgProfile.ResourcePath));
            return p;
        }
    }
}
