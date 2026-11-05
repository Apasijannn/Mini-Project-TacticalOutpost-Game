using System;
using UnityEngine;
using Outpost.Audio;
using UnityEngine.InputSystem;

namespace Outpost.Player
{
    /// <summary>
    /// Mengelola 2 slot senjata. Ganti senjata dengan tombol 1/2, scroll mouse, atau Q (senjata sebelumnya).
    /// Slot 1 = AK-47, slot 2 = Pistol.
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
        public FpsController controller;
        public FpsWeapon[] weapons;
        public int startIndex = 0;

        public int CurrentIndex { get; private set; }
        public FpsWeapon Current { get { return (weapons != null && CurrentIndex >= 0 && CurrentIndex < weapons.Length) ? weapons[CurrentIndex] : null; } }
        public event Action<FpsWeapon, int> OnWeaponChanged;

        int lastIndex = -1;
        float nextScroll;
        bool started;

        void Awake()
        {
            if (controller == null) controller = GetComponentInParent<FpsController>();
            CurrentIndex = -1;
            if (weapons == null) return;
            for (int i = 0; i < weapons.Length; i++) if (weapons[i] != null) weapons[i].gameObject.SetActive(false);
        }

        void Start()
        {
            started = true;
            Equip(Mathf.Clamp(startIndex, 0, weapons.Length - 1));
        }

        void Update()
        {
            if (!started || weapons == null || weapons.Length == 0) return;
            var kb = Keyboard.current; var mouse = Mouse.current;
            bool active = controller != null && controller.InputEnabled && Cursor.lockState == CursorLockMode.Locked;
            if (!active) return;

            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Select(0);
                else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Select(1);
                else if (kb.qKey.wasPressedThisFrame && lastIndex >= 0) Select(lastIndex);
            }
            if (mouse != null)
            {
                float sc = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(sc) > 0.1f && Time.unscaledTime >= nextScroll)
                {
                    nextScroll = Time.unscaledTime + 0.12f;
                    // scroll ke atas = slot berikutnya (naik di daftar HUD), ke bawah = slot sebelumnya
                    int n = weapons.Length;
                    Select(((CurrentIndex + (sc > 0f ? 1 : -1)) % n + n) % n);
                }
            }
        }

        public void Select(int index)
        {
            if (weapons == null || index < 0 || index >= weapons.Length || index == CurrentIndex || weapons[index] == null) return;
            Equip(index);
        }

        void Equip(int index)
        {
            bool first = CurrentIndex < 0;
            if (Current != null) Current.gameObject.SetActive(false);
            lastIndex = CurrentIndex;
            CurrentIndex = index;
            weapons[index].gameObject.SetActive(true);
            if (!first) OutpostAudio.Switch();                    // bunyi ganti senjata (bukan saat pertama muncul)
            if (OnWeaponChanged != null) OnWeaponChanged(weapons[index], index);
        }

        /// <summary>Tambah amunisi cadangan ke semua senjata api. Mengembalikan total peluru yang masuk.</summary>
        public int GiveAmmoPickup()
        {
            int total = 0;
            foreach (var w in weapons) if (w != null) total += w.AddAmmo(w.ammoPerPickup);
            return total;
        }

        public bool NeedsAmmo
        {
            get { foreach (var w in weapons) if (w != null && w.NeedsAmmo) return true; return false; }
        }

        public void RefillAll() { foreach (var w in weapons) if (w != null) w.Refill(); }
    }
}
