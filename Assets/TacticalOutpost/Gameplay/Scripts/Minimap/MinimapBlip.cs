using UnityEngine;
using MiniMapModular;
using Outpost.Enemy;

namespace Outpost.UI
{
    /// <summary>
    /// Menampilkan objek ini di radar/minimap (aset Minimap Modular Radar). Membuat komponen Aux_MP milik aset itu saat runtime,
    /// dan menyembunyikannya otomatis saat musuh mati.
    /// </summary>
    public class MinimapBlip : MonoBehaviour
    {
        public Sprite icon;
        public Color color = Color.white;
        public float sizeAdd = 0f;
        public bool edgeArrow = false;           // tampilkan panah di tepi radar bila di luar jangkauan
        public Sprite arrowSprite;
        public int layerIconView = 1;            // 0 bawah, 1 tengah, 2 atas
        public GameObject iconPrefab, arrowPrefab, arrowIconPrefab;

        Aux_MP aux;
        EnemyHealth enemy;

        void Awake()
        {
            enemy = GetComponent<EnemyHealth>();
            if (enemy != null)
            {
                bool boss = enemy.config != null && enemy.config.kind == EnemyKind.Boss;
                color = boss ? new Color(1f, 0.12f, 0.08f) : new Color(1f, 0.85f, 0.1f);
                sizeAdd = Mathf.Max(sizeAdd, boss ? 0.9f : 0.45f);
                edgeArrow = true;
            }
            if (GameObject.Find("Cam_Map") == null || iconPrefab == null) { enabled = false; return; }
            aux = gameObject.AddComponent<Aux_MP>();
            UpdateMarkerSize();
            aux.Icon = iconPrefab; aux.Icon_Arrow = arrowPrefab; aux.Arrow_Icon = arrowIconPrefab;
            aux.Sprite_Me = icon; aux.Icon_C = color; aux.SizeIconAdd = sizeAdd;
            aux.Arrow = edgeArrow && arrowPrefab != null ? 1 : 0; aux.Icon_Dir = 0; aux.HiddenIcon = 0; aux.RotIconMe = 0;
            aux.LayerIconView = layerIconView;
            aux.Sprite_Arrow = arrowSprite; aux.Arrow_C = color;
        }

        void Start()
        {
            var h = GetComponent<EnemyHealth>();
            if (h != null) h.OnDied += OnDied;
        }

        void LateUpdate() { UpdateMarkerSize(); }

        void UpdateMarkerSize()
        {
            if (enemy == null || aux == null) return;
            bool boss = enemy.config != null && enemy.config.kind == EnemyKind.Boss;
            bool large = MinimapController.Instance != null && MinimapController.Instance.IsLarge;
            aux.IconSizeMultiplier = large ? (boss ? 0.245f : 0.20f) : (boss ? 0.29f : 0.24f);
            aux.ArrowSizeMultiplier = large ? 0.32f : 0.42f;
        }

        void OnDied(EnemyHealth h) { Hide(); }

        void OnDestroy() { if (enemy != null) enemy.OnDied -= OnDied; }

        public void Hide() { if (aux != null) aux.enabled = false; }
    }
}
