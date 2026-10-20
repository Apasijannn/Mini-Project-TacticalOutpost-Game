using System.Collections.Generic;
using UnityEngine;
using Outpost.Enemy;
using Outpost.Player;

namespace Outpost.Pickups
{
    /// <summary>
    /// Menjatuhkan item saat zombie mati. Zombie biasa punya peluang (adaptif: lebih sering medkit saat darah rendah,
    /// lebih sering ammo saat peluru menipis). Boss selalu menjatuhkan ammo box + medkit.
    /// </summary>
    public class DropManager : MonoBehaviour
    {
        public GameObject ammoPrefab;
        public GameObject healthPrefab;
        [Range(0f, 1f)] public float normalDropChance = 0.30f;
        public int maxActive = 14;
        public LayerMask groundMask = ~0;

        readonly List<GameObject> spawned = new List<GameObject>();

        void OnEnable() { EnemyHealth.AnyDied += OnEnemyDied; }
        void OnDisable() { EnemyHealth.AnyDied -= OnEnemyDied; }

        void OnEnemyDied(EnemyHealth e)
        {
            if (e == null) return;
            bool boss = e.config != null && e.config.kind == EnemyKind.Boss;
            Vector3 p = e.transform.position;
            if (boss)
            {
                Spawn(ammoPrefab, p + new Vector3(0.9f, 0, 0.3f));
                Spawn(healthPrefab, p + new Vector3(-0.9f, 0, -0.3f));
                return;
            }
            if (Random.value > normalDropChance) return;

            float wAmmo = 0.55f, wHealth = 0.45f;
            var hp = PlayerHealth.Instance;
            if (hp != null) { float f = hp.Health / hp.maxHealth; if (f < 0.35f) { wHealth += 0.35f; wAmmo -= 0.15f; } else if (f > 0.9f) { wHealth -= 0.25f; wAmmo += 0.15f; } }
            var wm = hp != null ? hp.GetComponentInChildren<WeaponManager>(true) : null;
            if (wm != null && wm.weapons != null)
            {
                int low = 0, firearms = 0;
                foreach (var w in wm.weapons) if (w != null) { firearms++; if (w.Reserve < w.magazineSize) low++; }
                if (firearms > 0 && low == firearms) { wAmmo += 0.3f; wHealth -= 0.1f; }
                else if (wm != null && !wm.NeedsAmmo) { wHealth += 0.2f; wAmmo -= 0.2f; }
            }
            wAmmo = Mathf.Max(0.05f, wAmmo); wHealth = Mathf.Max(0.05f, wHealth);
            Spawn(Random.value < wAmmo / (wAmmo + wHealth) ? ammoPrefab : healthPrefab, p);
        }

        void Spawn(GameObject prefab, Vector3 pos)
        {
            if (prefab == null) return;
            spawned.RemoveAll(g => g == null);
            while (spawned.Count >= maxActive) { Destroy(spawned[0]); spawned.RemoveAt(0); }
            Vector3 o = pos + Vector3.up * 1.2f;
            RaycastHit hit;
            if (Physics.Raycast(o, Vector3.down, out hit, 6f, groundMask, QueryTriggerInteraction.Ignore)) pos = hit.point; else pos.y = Mathf.Max(0f, pos.y);
            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            spawned.Add(go);
        }
    }
}
