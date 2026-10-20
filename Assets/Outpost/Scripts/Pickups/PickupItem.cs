using UnityEngine;
using Outpost.Audio;
using Outpost.Player;

namespace Outpost.Pickups
{
    public enum PickupKind { Ammo, Health }

    /// <summary>
    /// Item jatuhan dari zombie. Melayang, berputar, dan diambil otomatis saat player mendekat.
    /// Ammo box hanya terambil bila ada senjata yang cadangannya belum penuh; medkit hanya bila darah belum penuh.
    /// </summary>
    public class PickupItem : MonoBehaviour
    {
        public PickupKind kind = PickupKind.Ammo;
        public float healAmount = 35f;
        public float collectRadius = 1.7f;
        public float lifetime = 45f;
        public Transform visual;
        public float hoverHeight = 0.55f;

        float born, nextHint;
        Renderer[] rends;
        Vector3 baseScale = Vector3.one;
        Vector3 basePos;
        WeaponManager wm;

        public static int ActiveCount { get; private set; }
        void OnEnable() { ActiveCount++; }
        void OnDisable() { ActiveCount--; }

        void Start()
        {
            born = Time.time;
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (visual != null) { baseScale = visual.localScale; basePos = visual.localPosition; }
            rends = GetComponentsInChildren<Renderer>();
        }

        void Update()
        {
            float age = Time.time - born;
            if (visual != null)
            {
                visual.Rotate(0f, 85f * Time.deltaTime, 0f, Space.World);
                visual.localPosition = basePos + new Vector3(0f, hoverHeight + Mathf.Sin(age * 2.4f) * 0.07f, 0f);
                float pop = Mathf.Clamp01(age / 0.25f);                 // muncul membesar
                float fade = Mathf.Clamp01((lifetime - age) / 1.5f);     // menyusut sebelum hilang
                visual.localScale = baseScale * Mathf.Min(pop, fade);
            }
            if (age > lifetime - 7f && rends != null)                    // berkedip 7 detik terakhir
            {
                bool on = Mathf.FloorToInt(age * 5f) % 2 == 0;
                foreach (var r in rends) if (r != null) r.enabled = on;
            }
            if (age >= lifetime) { Destroy(gameObject); return; }

            var player = PlayerHealth.Instance;
            if (player == null || player.IsDead) return;
            Vector3 d = player.transform.position - transform.position;
            if (new Vector2(d.x, d.z).sqrMagnitude > collectRadius * collectRadius || Mathf.Abs(d.y) > 2.5f) return;
            TryCollect(player);
        }

        void TryCollect(PlayerHealth player)
        {
            if (kind == PickupKind.Health)
            {
                if (player.Health >= player.maxHealth - 0.01f) { Hint("Darah penuh", new Color(0.6f, 1f, 0.6f)); return; }
                float before = player.Health;
                player.Heal(healAmount);
                HudEvents.ShowToast("+" + Mathf.RoundToInt(player.Health - before) + " DARAH", new Color(0.45f, 1f, 0.5f));
                OutpostAudio.PickHealth();
                Destroy(gameObject);
            }
            else
            {
                if (wm == null) wm = player.GetComponentInChildren<WeaponManager>(true);
                if (wm == null) return;
                if (!wm.NeedsAmmo) { Hint("Amunisi penuh", new Color(1f, 0.9f, 0.5f)); return; }
                int n = wm.GiveAmmoPickup();
                HudEvents.ShowToast("+" + n + " AMUNISI", new Color(1f, 0.85f, 0.35f));
                OutpostAudio.PickAmmo();
                Destroy(gameObject);
            }
        }

        void Hint(string s, Color c)
        {
            if (Time.time < nextHint) return;
            nextHint = Time.time + 2f;
            HudEvents.ShowToast(s, c);
        }
    }
}
