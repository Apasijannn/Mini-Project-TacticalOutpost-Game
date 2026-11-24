using UnityEngine;

namespace Outpost.Final
{
    /// <summary>
    /// Semua parameter mode Final (DDA, event wave PCG, titik taktis, NPC sekutu).
    /// Asset: Resources/Final/Final_Profile.asset (menu Tactical Outpost → Final → Create or Select Profile).
    /// Bila asset belum ada, nilai default di bawah dipakai.
    /// </summary>
    [CreateAssetMenu(menuName = "Outpost/Final Profile", fileName = "Final_Profile")]
    public class FinalProfile : ScriptableObject
    {
        public const string ResourcePath = "Final/Final_Profile";

        [Header("Seed (PCG event + titik taktis)")]
        public bool useRandomSeed = true;
        public int seed = 2026;

        [Header("DDA — evaluasi tiap awal wave")]
        public bool ddaEnabled = true;
        [Range(0f, 1f)] public float startDifficulty = 0.5f;
        [Tooltip("Bobot skor performa terbaru terhadap skill lama (EMA).")]
        [Range(0.05f, 1f)] public float smoothing = 0.6f;
        [Tooltip("Perubahan difficulty maksimum per evaluasi.")]
        [Range(0.05f, 0.5f)] public float maxStep = 0.2f;
        [Tooltip("Difficulty dikurangi sebanyak ini setelah player kalah lalu mencoba lagi.")]
        [Range(0f, 0.5f)] public float retryRelief = 0.15f;

        [Header("DDA — indikator performa (bobot)")]
        public float accuracyWeight = 0.30f;
        public float damageTakenWeight = 0.25f;
        public float generatorLossWeight = 0.25f;
        public float killRateWeight = 0.20f;

        [Header("DDA — normalisasi indikator")]
        public float accuracyLow = 0.15f;              // akurasi <= ini = skor 0
        public float accuracyHigh = 0.60f;             // akurasi >= ini = skor 1
        public float damagePerMinuteHigh = 60f;        // HP player hilang per menit yang dianggap sangat buruk
        public float generatorLossPerMinuteHigh = 150f;
        public float killsPerMinuteHigh = 20f;
        public int minShotsForAccuracy = 10;           // di bawah ini akurasi dianggap netral (0.5)

        [Header("DDA — output (nilai easy → hard)")]
        public Vector2 zombieCountMultiplier = new Vector2(0.75f, 1.30f);
        public Vector2 spawnInterval = new Vector2(0.60f, 0.30f);
        public Vector2 enemyHealthMultiplier = new Vector2(0.85f, 1.20f);
        public Vector2 enemySpeedMultiplier = new Vector2(0.90f, 1.10f);
        public Vector2 dropChance = new Vector2(0.40f, 0.18f);
        [Tooltip("Bobot varian zombie (x = easy, y = hard). Id harus sama dengan EnemyCatalog.")]
        public VariantWeight[] variantWeights =
        {
            new VariantWeight { id = "walker",   weight = new Vector2(5f, 2f) },
            new VariantWeight { id = "runner",   weight = new Vector2(1.5f, 3f) },
            new VariantWeight { id = "skinless", weight = new Vector2(1f, 3f) },
            new VariantWeight { id = "bloater",  weight = new Vector2(0.8f, 2f) },
        };

        [Header("Dynamic Blackout (reaktif HP generator + skala DDA)")]
        public bool dynamicBlackoutEnabled = true;
        [Range(0.5f, 0.95f)] public float blackoutTierFlickerLow = 0.75f;
        [Range(0.3f, 0.75f)] public float blackoutTierFlickerHeavy = 0.50f;
        [Range(0.1f, 0.45f)] public float blackoutTierChaos = 0.25f;
        [Tooltip("Skala intensitas & kecepatan kedipan oleh DDA (x = easy, y = hard).")]
        public Vector2 blackoutFlickerMultiplier = new Vector2(0.70f, 1.45f);
        [Tooltip("Skala durasi lampu padam total oleh DDA (x = easy, y = hard).")]
        public Vector2 blackoutOffDurationMultiplier = new Vector2(0.60f, 1.65f);
        [Tooltip("Pergeseran ambang HP generator oleh DDA (easy = lebih lambat gelap, hard = lebih cepat gelap).")]
        public Vector2 blackoutThresholdShift = new Vector2(-0.08f, 0.08f);

        [Header("PCG — event wave (weighted + budget + constraint)")]
        public float eventBudgetBase = 0.5f;
        public float eventBudgetPerWave = 0.75f;
        public float eventBudgetByDifficulty = 2.0f;
        [Range(1, 30)] public int maxEventAttempts = 12;
        [Tooltip("Di bawah difficulty ini Supply Drop selalu ditambahkan sebagai bantuan.")]
        [Range(0f, 1f)] public float supplyAssistBelow = 0.35f;
        public float blackoutLightFactor = 0.25f;
        public float blackoutSightFactor = 0.7f;
        public float fogDensity = 0.035f;
        public float hordeCountFactor = 1.3f;
        public float hordeIntervalFactor = 0.6f;
        public float frenzySpeedFactor = 1.2f;
        public float frenzyCooldownFactor = 0.8f;
        public int supplyDropCount = 4;

        [Header("PCG — titik taktis sekutu")]
        public int tacticalPointCount = 8;
        public int tacticalCandidates = 160;
        public float tacticalMinRadius = 5f;
        public float tacticalMaxRadius = 18f;
        public float tacticalSpacing = 4f;
        public float tacticalAttackPointClear = 2.5f;
        public float tacticalSightRange = 30f;

        [Header("NPC sekutu (Utility AI)")]
        [Range(0, 3)] public int allyCount = 1;
        public float allyHealth = 220f;
        public float allyDamage = 18f;
        public float allyRoundsPerMinute = 450f;
        public int allyMagazine = 30;
        public float allyReloadTime = 2.4f;
        public float allySpread = 2.2f;
        public float allyRange = 40f;
        public float allyNoiseRadius = 16f;
        public float allyMoveSpeed = 4.2f;
        public float allyRepairPerSecond = 12f;
        public float allyRegenPerSecond = 6f;
        public float allyRespawnDelay = 15f;
        [Header("Utility weights (dasar, digeser DDA)")]
        public float defendWeight = 0.35f;
        public float interceptWeight = 0.9f;
        public float supportWeight = 1.0f;
        public float repairWeight = 0.9f;
        public float retreatWeight = 1.0f;
        [Tooltip("Pergeseran bobot oleh DDA: difficulty rendah → support/repair naik, difficulty tinggi → intercept naik.")]
        public float ddaAllyShift = 0.35f;

        [System.Serializable]
        public struct VariantWeight { public string id; public Vector2 weight; }

        static FinalProfile cached;
        public static FinalProfile Load()
        {
            if (cached == null) cached = Resources.Load<FinalProfile>(ResourcePath);
            if (cached == null) { cached = CreateInstance<FinalProfile>(); cached.name = "Final_Profile (default)"; }
            return cached;
        }
    }
}
