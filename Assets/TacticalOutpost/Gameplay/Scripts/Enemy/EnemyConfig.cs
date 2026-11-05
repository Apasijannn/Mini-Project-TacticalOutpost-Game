using UnityEngine;

namespace Outpost.Enemy
{
    public enum EnemyKind { Normal, Boss }

    /// <summary>Statistik satu jenis musuh (ScriptableObject). Ubah angka di sini untuk balancing, tanpa menyentuh kode.</summary>
    [CreateAssetMenu(menuName = "Outpost/Enemy Config", fileName = "EnemyConfig")]
    public class EnemyConfig : ScriptableObject
    {
        [Header("Identitas")]
        public string displayName = "Zombie";
        public EnemyKind kind = EnemyKind.Normal;

        [Header("Statistik")]
        public float maxHealth = 100f;
        public float walkSpeed = 1.2f;       // m/s saat belum menyadari player
        public float runSpeed = 3.4f;        // m/s saat mengejar
        public float acceleration = 10f;
        public float angularSpeed = 220f;

        [Header("Serangan")]
        public float attackDamage = 12f;
        public float attackRange = 1.7f;
        public float attackCooldown = 1.4f;
        public float attackHitDelay = 0.45f;          // detik setelah animasi mulai sampai damage terjadi
        public float generatorDamageMultiplier = 1.5f;

        [Header("Persepsi")]
        public float sightRadius = 22f;
        public float sightFov = 150f;
        public float closeSenseRadius = 3f;           // di dalam jarak ini player selalu terdeteksi (tanpa cek sudut)
        public float hearingMultiplier = 1f;          // pengali radius suara tembakan
        public float memoryTime = 8f;                 // detik mengingat posisi terakhir player

        [Header("Terkena tembakan")]
        public float headshotMultiplier = 2f;
        public bool canStagger = true;
        public float staggerThreshold = 0.12f;        // pecahan dari HP maks dalam satu tembakan
        public float staggerDuration = 0.35f;

        [Header("Visual")]
        public float visualScale = 1f;
        public float animSpeed = 1f;
        public Color tint = Color.white;
        public float corpseTime = 6f;

        [Header("Khusus boss")]
        public bool screamOnSpawn = false;
        public float screamDuration = 2.4f;
        [Range(0f, 1f)] public float enrageHealthFraction = 0.5f;
        public float enrageSpeedMultiplier = 1.3f;
        public float enrageCooldownMultiplier = 0.7f;
    }
}
