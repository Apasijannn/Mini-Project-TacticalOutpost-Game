using UnityEngine;
using MiniMapModular;
using Outpost.UI;

namespace Outpost.Dungeon
{
    /// <summary>
    /// Ikon objektif dungeon (power relay, generator) di minimap/peta besar, memakai sistem ikon aset Minimap Modular Radar
    /// (Aux_MP) dengan prefab ikon/panah yang sama seperti pickup. Ikon lebih besar dari pickup tetapi tetap jauh lebih kecil
    /// dari room, warnanya mengikuti status objektif, dan ada panah di tepi radar bila objektif di luar jangkauan.
    /// </summary>
    public sealed class DungeonMapIcon : MonoBehaviour
    {
        public Sprite sprite;
        public Color color = Color.white;
        [Tooltip("Tambahan ukuran dibanding ikon pickup (0 = sama dengan pickup).")]
        public float sizeAdd = 0.6f;
        [Tooltip("Pengali ukuran saat peta besar (M) dibuka, supaya ikon tidak menutupi room yang tampil lebih kecil.")]
        public float largeMapMultiplier = 0.7f;
        public bool edgeArrow;
        public MinimapBlip template;

        Aux_MP aux;
        bool dirty = true;

        public static DungeonMapIcon Attach(GameObject go, MinimapBlip template, Sprite sprite, Color color, float sizeAdd, bool edgeArrow)
        {
            if (go == null || template == null || sprite == null) return null;
            var icon = go.AddComponent<DungeonMapIcon>();
            icon.template = template; icon.sprite = sprite; icon.color = color; icon.sizeAdd = sizeAdd; icon.edgeArrow = edgeArrow;
            return icon;
        }

        /// <summary>Ganti warna/panah. Aux_MP hanya membaca warna saat ikon dibuat, jadi ikon dibuat ulang bila berubah.</summary>
        public void Set(Color c, bool arrow)
        {
            if (aux != null && c == color && arrow == edgeArrow) return;
            color = c; edgeArrow = arrow; dirty = true;
        }

        void LateUpdate()
        {
            if (dirty) { dirty = false; Rebuild(); }
            if (aux == null) return;
            bool large = MinimapController.Instance != null && MinimapController.Instance.IsLarge;
            aux.IconSizeMultiplier = large ? largeMapMultiplier : 1f;
            aux.ArrowSizeMultiplier = large ? 0.32f : 0.5f;
        }

        void Rebuild()
        {
            if (template == null || sprite == null || GameObject.Find("Cam_Map") == null) return;
            if (aux != null) Destroy(aux);                    // OnDisable milik Aux_MP ikut menghapus ikon lama
            aux = gameObject.AddComponent<Aux_MP>();
            aux.Icon = template.iconPrefab; aux.Icon_Arrow = template.arrowPrefab; aux.Arrow_Icon = template.arrowIconPrefab;
            aux.Sprite_Me = sprite; aux.Icon_C = color; aux.SizeIconAdd = sizeAdd;
            aux.Arrow = edgeArrow && template.arrowPrefab != null ? 1 : 0;
            aux.Icon_Dir = 0; aux.HiddenIcon = 0; aux.RotIconMe = 0;
            aux.LayerIconView = 2;                            // di atas ikon pickup & zombie
            aux.Sprite_Arrow = template.arrowSprite; aux.Arrow_C = color;
        }

        void OnDestroy() { if (aux != null) Destroy(aux); }
    }
}
