using System;
using UnityEngine;

namespace Outpost.PCG
{
    [Serializable]
    public class PrefabEntry
    {
        public string name;
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
    }

    public enum SupplyKind { Ammo, Health }

    /// <summary>Satu baris tabel enemy: id = id varian di EnemyCatalog game (walker, runner, skinless, bloater).</summary>
    [Serializable]
    public class EnemyEntry
    {
        public string id;
        [Min(0f)] public float weight = 1f;
        [Tooltip("Biaya difficulty budget per zombie.")]
        [Min(1)] public int cost = 1;
        [Tooltip("Varian baru boleh dipilih mulai wave ini.")]
        [Min(1)] public int minWave = 1;
    }

    [Serializable]
    public class SupplyEntry
    {
        public SupplyKind kind = SupplyKind.Ammo;
        [Min(0f)] public float weight = 1f;
    }

    /// <summary>
    /// Parameter mode Procedural Content Generation. Semua prefab di sini adalah asset yang sudah ada di project
    /// (diisi otomatis oleh menu Tactical Outpost > PCG > Create or Update Profile). Tidak ada geometri buatan sendiri.
    /// </summary>
    [CreateAssetMenu(menuName = "Outpost/PCG Profile", fileName = "PCG_Profile")]
    public class PcgProfile : ScriptableObject
    {
        public const string ResourcePath = "PCG_Profile";

        [Header("Seed")]
        [Tooltip("True = seed baru setiap match. False = selalu memakai 'seed'.")]
        public bool useRandomSeed = true;
        public int seed = 12345;

        [Header("Arena (asset existing)")]
        public GameObject floorTile;
        public GameObject fenceSpan;
        public GameObject fenceEnd;
        public GameObject generator;
        public GameObject[] generatorProps = new GameObject[0];
        public GameObject cornerTower;
        public GameObject lamp;
        [Tooltip("Jumlah segmen pagar per sisi arena.")]
        [Min(6)] public int spansPerSide = 15;
        [Tooltip("Lebar gerbang dalam segmen pagar.")]
        [Min(1)] public int gateSpans = 2;
        [Tooltip("Gerbang tidak boleh lebih dekat dari sekian segmen ke sudut.")]
        [Min(1)] public int gateCornerMargin = 2;
        [Min(1)] public int lampEverySpans = 2;

        [Header("Obstacle layout (generate-and-test)")]
        [Min(0)] public int obstacleCount = 34;
        public PrefabEntry[] obstacleTable = new PrefabEntry[0];
        [Tooltip("Jarak bebas minimum antar-obstacle (m): lorong untuk zombie dan boss.")]
        public float obstacleGap = 2.5f;
        public float generatorClearRadius = 9f;
        public float gateClearRadius = 9f;
        [Tooltip("Radius safe zone player: bebas obstacle/props, ditandai lampu hijau di tepinya.")]
        public float playerClearRadius = 6f;
        public float wallMargin = 2.5f;
        [Min(1)] public int obstacleMaxAttempts = 40;
        [Tooltip("Bila jalur gerbang→generator terputus, layout dibuat ulang dengan stream berikutnya.")]
        [Min(1)] public int maxLayoutAttempts = 5;
        [Tooltip("Panjang jalur NavMesh maksimal dibanding jarak lurus gerbang→generator.")]
        public float maxDetourRatio = 1.8f;

        [Header("Dressing kosmetik (tanpa collider)")]
        [Min(0)] public int dressingCount = 35;
        public PrefabEntry[] dressingTable = new PrefabEntry[0];
        public Vector2 dressingScale = new Vector2(0.85f, 1.15f);
        public float dressingSpacing = 2f;

        [Header("Spawn point zombie (per gerbang)")]
        public float spawnZoneDepth = 10f;
        public float spawnSpacing = 1.6f;
        [Tooltip("Rule: spawn point zombie minimal sejauh ini dari posisi awal player (minDistanceFromPlayer di modul).")]
        public float minEnemySpawnDistance = 18f;

        [Header("Safe zone (garis melingkar hijau bercahaya di tepi area)")]
        public Color safeZoneColor = new Color(0.15f, 1f, 0.35f);
        [Tooltip("Kecerahan garis (HDR, > 1 memicu bloom sehingga garis tampak bercahaya).")]
        public float safeZoneGlow = 4f;
        public float safeZoneLineWidth = 0.12f;

        [Header("Enemy wave (weighted random + difficulty budget)")]
        [Tooltip("Komposisi zombie tiap wave dipilih dari tabel ini sampai budget wave habis.")]
        public EnemyEntry[] enemyTable =
        {
            new EnemyEntry { id = "walker",   weight = 50f, cost = 1, minWave = 1 },
            new EnemyEntry { id = "runner",   weight = 25f, cost = 2, minWave = 1 },
            new EnemyEntry { id = "skinless", weight = 15f, cost = 2, minWave = 1 },
            new EnemyEntry { id = "bloater",  weight = 10f, cost = 3, minWave = 2 },
        };
        [Tooltip("Difficulty budget per wave (indeks 0 = wave 1). Boss tetap mengikuti WaveDirector.")]
        public int[] waveBudget = { 38, 58, 78 };

        [Header("Supply cache (per wave)")]
        [Min(0)] public int cachesPerWave = 3;
        public SupplyEntry[] supplyTable = { new SupplyEntry { kind = SupplyKind.Ammo, weight = 60f }, new SupplyEntry { kind = SupplyKind.Health, weight = 40f } };
        public float supplyMinRadius = 6f;
        public float supplyMaxRadius = 30f;
        public float supplySpacing = 6f;
        public float supplyMinFromGate = 10f;
    }
}
