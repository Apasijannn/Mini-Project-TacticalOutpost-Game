using UnityEngine;
using Outpost.Enemy;
using Outpost.Player;

namespace Outpost.Final
{
    /// <summary>
    /// Mengumpulkan indikator performa player untuk DDA lewat event yang sudah ada (tanpa mengubah script player):
    /// tembakan/hit (FpsWeapon.OnFired/OnHit), damage diterima (PlayerHealth.OnHealthChanged),
    /// HP generator hilang (GeneratorHealth.OnDamaged), kill (EnemyHealth.AnyDied; kill sekutu dipisah).
    /// </summary>
    public sealed class PerformanceTracker : MonoBehaviour
    {
        PerformanceWindow window, total;
        float windowStart, sessionStart;
        float lastPlayerHp = -1f;
        PlayerHealth player;
        FpsWeapon[] weapons;
        readonly System.Collections.Generic.Dictionary<GeneratorHealth, float> generatorHp = new System.Collections.Generic.Dictionary<GeneratorHealth, float>();

        public PerformanceWindow Current { get { var w = window; w.seconds = Time.time - windowStart; w.playerHealthFraction = PlayerFraction; return w; } }
        public PerformanceWindow Total { get { var w = total; w.seconds = Time.time - sessionStart; w.playerHealthFraction = PlayerFraction; return w; } }
        float PlayerFraction { get { return player != null && player.maxHealth > 0f ? player.Health / player.maxHealth : 0f; } }

        public void Begin(PlayerHealth playerHealth)
        {
            player = playerHealth;
            windowStart = sessionStart = Time.time;
            if (player != null)
            {
                lastPlayerHp = player.Health;
                player.OnHealthChanged += OnPlayerHealth;
                var manager = player.GetComponentInChildren<WeaponManager>(true);
                weapons = manager != null ? manager.weapons : player.GetComponentsInChildren<FpsWeapon>(true);
                if (weapons != null) foreach (var w in weapons) if (w != null) { w.OnFired += OnFired; w.OnHit += OnHit; }
            }
            foreach (var g in GeneratorHealth.All) { generatorHp[g] = g.Health; g.OnDamaged += OnGenerator; }
            EnemyHealth.AnyDied += OnEnemyDied;
        }

        /// <summary>Tutup jendela evaluasi dan mulai jendela baru. Mengembalikan data jendela yang ditutup.</summary>
        public PerformanceWindow Cut()
        {
            var w = Current;
            window = new PerformanceWindow();
            windowStart = Time.time;
            return w;
        }

        void OnFired() { window.shots++; total.shots++; }
        void OnHit() { window.hits++; total.hits++; }

        void OnPlayerHealth(float hp, float max)
        {
            if (lastPlayerHp >= 0f && hp < lastPlayerHp) { window.damageTaken += lastPlayerHp - hp; total.damageTaken += lastPlayerHp - hp; }
            lastPlayerHp = hp;
        }

        void OnGenerator(GeneratorHealth g, float hp)
        {
            float before;
            if (generatorHp.TryGetValue(g, out before) && hp < before) { window.generatorLoss += before - hp; total.generatorLoss += before - hp; }
            generatorHp[g] = hp;
        }

        void OnEnemyDied(EnemyHealth e)
        {
            if (EnemyBrain.DamageSource != null) { window.allyKills++; total.allyKills++; }
            else { window.kills++; total.kills++; }
        }

        void OnDestroy()
        {
            if (player != null) player.OnHealthChanged -= OnPlayerHealth;
            if (weapons != null) foreach (var w in weapons) if (w != null) { w.OnFired -= OnFired; w.OnHit -= OnHit; }
            foreach (var g in generatorHp.Keys) if (g != null) g.OnDamaged -= OnGenerator;
            EnemyHealth.AnyDied -= OnEnemyDied;
        }
    }
}
