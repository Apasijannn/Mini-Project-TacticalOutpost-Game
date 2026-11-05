using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Outpost.UI;

namespace Outpost.Player
{
    /// <summary>Pesan singkat di HUD (mis. "+60 AMUNISI"). Dipanggil dari pickup dsb.</summary>
    public static class HudEvents
    {
        public static event Action<string, Color> Toast;
        public static void ShowToast(string text, Color color) { if (Toast != null) Toast(text, color); }
    }

    /// <summary>HUD lewat kode: crosshair, hit marker, daftar slot senjata (kanan bawah), amunisi, darah, layar damage, pesan gugur.</summary>
    public class FpsHud : MonoBehaviour
    {
        public WeaponManager manager;
        public PlayerHealth health;
        public FpsController controller;

        [Header("Skin (sprite dari aset 2D GUI Shooter, diisi builder)")]
        public Sprite sprSlotBg;
        public Sprite sprSlotFrame;
        public Sprite sprCross;
        public Sprite sprHitCross;
        public Sprite sprHeadCross;
        public Sprite sprBarBg;
        public Sprite sprBarFill;
        public Sprite sprAmmo;
        public Sprite sprHealth;

        FpsWeapon weapon { get { return manager != null ? manager.Current : null; } }

        Canvas canvas;
        Image[] cross = new Image[4];
        Image[] hitMark = new Image[4];
        Image damageFlash, healthFill, healthBg, deathPanel, dot, crossImg, hitImg, headImg;
        float headUntil;
        TMP_Text ammoText, healthText, msgText, deathText, nameText, toastText;
        MilitaryText promptTyping, toastTyping, deathTyping;
        Image toastPanel, promptPanel;
        bool deathShown;
        float hitUntil, flashAlpha, toastUntil;
        float spreadVis;

        class Slot { public Image bg, accent, icon; public TMP_Text num; public RectTransform rt; public float t; }
        Slot[] slots;

        void Awake()
        {
            if (manager == null) manager = GetComponentInChildren<WeaponManager>(true);
            if (health == null) health = GetComponent<PlayerHealth>();
            if (controller == null) controller = GetComponent<FpsController>();
            Build();
        }

        void OnEnable()
        {
            if (health != null) { health.OnDamaged += OnDamaged; health.OnDied += OnDied; }
            if (manager != null && manager.weapons != null) foreach (var w in manager.weapons) if (w != null) w.OnHit += OnHit;
            HudEvents.Toast += OnToast;
        }
        void OnDisable()
        {
            if (health != null) { health.OnDamaged -= OnDamaged; health.OnDied -= OnDied; }
            if (manager != null && manager.weapons != null) foreach (var w in manager.weapons) if (w != null) w.OnHit -= OnHit;
            HudEvents.Toast -= OnToast;
        }

        void OnHit()
        {
            hitUntil = Time.time + 0.14f;
            if (Time.time - Outpost.Enemy.EnemyHealth.LastHeadshotTime < 0.08f)
            {
                headUntil = Time.time + 0.28f;
                OnToast("HEADSHOT", new Color(1f, 0.82f, 0.25f, 1f));
            }
        }
        void OnDamaged(Vector3 d) { flashAlpha = Mathf.Min(0.55f, flashAlpha + 0.35f); }
        void OnToast(string s, Color c) { if (toastText == null) return; toastTyping.Speak(s); toastText.color = c; toastUntil = Time.time + 1.6f; }
        void OnDied()
        {
            var view = GetComponent<PlayerDeathView>();
            if (view != null)
            {
                view.Begin();
                foreach (Transform child in canvas.transform) if (child != deathPanel.transform && child != deathText.transform) child.gameObject.SetActive(false);
                deathPanel.color = new Color(0, 0, 0, .13f);
                var rect = (RectTransform)deathText.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .16f); rect.anchoredPosition = Vector2.zero;
            }
            else ShowDeath();
            if (controller != null) controller.InputEnabled = false;
            FpsController.LockCursor(false);
        }
        void ShowDeath()
        {
            if (deathShown) return;
            deathShown = true; deathPanel.enabled = true; deathText.enabled = true;
            deathTyping.Speak("DEAD\n<size=28>Get back in the fight. Press ENTER to try again.</size>");
        }

        Image MakeImage(string n, Transform parent, Color c, Vector2 size, Vector2 pos, Vector2 anchor)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            var im = go.GetComponent<Image>(); im.color = c; im.raycastTarget = false;
            return im;
        }

        TMP_Text MakeText(string n, Transform parent, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box, bool mono = false)
        {
            return HudStyle.Text(n, parent, size, align, anchor, pos, box, mono);
        }

        void Build()
        {
            var cg = new GameObject("PlayerHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cg.transform.SetParent(transform, false);
            canvas = cg.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
            var sc = cg.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            var root = cg.transform;
            var mid = new Vector2(0.5f, 0.5f);

            damageFlash = MakeImage("DamageFlash", root, new Color(0.8f, 0f, 0f, 0f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            var fr = (RectTransform)damageFlash.transform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;

            Color cc = new Color(1f, 1f, 1f, 0.9f);
            for (int i = 0; i < 4; i++)
            {
                bool horiz = i < 2;
                cross[i] = MakeImage("Cross" + i, root, cc, horiz ? new Vector2(14, 2) : new Vector2(2, 14), Vector2.zero, mid);
                hitMark[i] = MakeImage("Hit" + i, root, new Color(1f, 0.25f, 0.2f, 0f), new Vector2(14, 2.5f), Vector2.zero, mid);
                hitMark[i].transform.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? 45f : -45f);
            }
            dot = MakeImage("Dot", root, new Color(1, 1, 1, 0.85f), new Vector2(3, 3), Vector2.zero, mid);
            if (sprCross != null)
            {
                crossImg = MakeImage("CrossSprite", root, new Color(1f, 1f, 1f, 0.9f), new Vector2(48, 48), Vector2.zero, mid); crossImg.sprite = sprCross;
                for (int i = 0; i < 4; i++) cross[i].enabled = false;
                dot.enabled = false;
            }
            if (sprHitCross != null) { hitImg = MakeImage("HitSprite", root, new Color(1f, 1f, 1f, 0f), new Vector2(64, 64), Vector2.zero, mid); hitImg.sprite = sprHitCross; for (int i = 0; i < 4; i++) hitMark[i].enabled = false; }
            if (sprHeadCross != null) { headImg = MakeImage("HeadSprite", root, new Color(1f, 1f, 1f, 0f), new Vector2(84, 84), Vector2.zero, mid); headImg.sprite = sprHeadCross; }

            promptPanel = MakeImage("Prompt panel", root, HudStyle.Panel, new Vector2(680, 58), Vector2.zero, new Vector2(.5f, .3f));
            toastPanel = MakeImage("Message panel", root, HudStyle.Panel, new Vector2(680, 58), Vector2.zero, new Vector2(.5f, .36f));
            promptPanel.enabled = toastPanel.enabled = false;
            msgText = MakeText("Msg", root, 25, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.3f), Vector2.zero, new Vector2(650, 58), true);
            toastText = MakeText("Toast", root, 25, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.36f), Vector2.zero, new Vector2(650, 58), true);
            toastText.text = "";
            promptTyping = msgText.gameObject.AddComponent<MilitaryText>(); toastTyping = toastText.gameObject.AddComponent<MilitaryText>();

            // ----- kanan bawah: daftar slot senjata (slot 1 paling bawah, seperti HUD loadout game FPS) -----
            int n = manager != null && manager.weapons != null ? manager.weapons.Length : 0;
            slots = new Slot[n];
            Vector2 br = new Vector2(1, 0);
            MakeImage("Weapon panel", root, HudStyle.Panel, new Vector2(320, 132), new Vector2(-40, 40), br);
            MakeImage("Weapon panel orange edge", root, HudStyle.Amber, new Vector2(3, 132), new Vector2(-40, 40), br);
            var loadoutLabel = MakeText("Loadout label", root, 16, TextAnchor.LowerRight, br, new Vector2(-40, 336), new Vector2(320, 24), true); loadoutLabel.text = "LOADOUT"; loadoutLabel.color = HudStyle.Muted;
            for (int i = 0; i < n; i++)
            {
                var s = new Slot();
                float y = 190f + i * 68f;
                s.bg = MakeImage("Slot" + (i + 1), root, HudStyle.Panel, new Vector2(320, 58), new Vector2(-40, y), br);
                s.rt = (RectTransform)s.bg.transform;
                s.accent = MakeImage("Right orange edge", s.bg.transform, HudStyle.Amber, new Vector2(3, 58), Vector2.zero, new Vector2(1, 0));
                s.num = MakeText("Num", s.bg.transform, 22, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(40, 40), true);
                s.num.text = (i + 1).ToString();
                s.icon = MakeImage("Icon", s.bg.transform, HudStyle.Ivory, new Vector2(220, 52), new Vector2(-16, 0), new Vector2(1, 0.5f));
                s.icon.preserveAspect = true;
                var w = manager.weapons[i];
                if (w != null && w.icon != null) s.icon.sprite = w.icon; else s.icon.enabled = false;
                slots[i] = s;
            }
            nameText = MakeText("WeaponName", root, 26, TextAnchor.LowerRight, br, new Vector2(-60, 136), new Vector2(280, 36));
            ammoText = MakeText("Ammo", root, 48, TextAnchor.LowerRight, br, new Vector2(-60, 78), new Vector2(280, 58), true);
            var ammoLabel = MakeText("Ammo label", root, 14, TextAnchor.LowerRight, br, new Vector2(-60, 55), new Vector2(280, 22), true); ammoLabel.text = "MAG / RESERVE"; ammoLabel.color = HudStyle.Muted;

            var bl = Vector2.zero;
            MakeImage("Vitals panel", root, HudStyle.Panel, new Vector2(320, 132), new Vector2(40, 40), bl);
            MakeImage("Vitals orange edge", root, HudStyle.Amber, new Vector2(3, 132), new Vector2(40, 40), bl);
            var vitals = MakeText("Vitals label", root, 16, TextAnchor.LowerLeft, bl, new Vector2(60, 140), new Vector2(280, 24), true); vitals.text = "VITALS"; vitals.color = HudStyle.Muted;
            healthBg = MakeImage("HealthBg", root, new Color(.23f, .27f, .24f), new Vector2(280, 6), new Vector2(60, 62), bl);
            healthFill = MakeImage("HealthFill", root, HudStyle.Amber, new Vector2(280, 6), new Vector2(60, 62), bl);
            healthText = MakeText("HP", root, 44, TextAnchor.LowerLeft, bl, new Vector2(60, 82), new Vector2(280, 52), true);

            deathPanel = MakeImage("DeathPanel", root, new Color(0.1f, 0f, 0f, 0.65f), Vector2.zero, Vector2.zero, mid);
            var dr = (RectTransform)deathPanel.transform; dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one; dr.offsetMin = dr.offsetMax = Vector2.zero;
            deathText = MakeText("DeathText", root, 54, TextAnchor.MiddleCenter, mid, Vector2.zero, new Vector2(1200, 200));
            deathText.text = "DEAD\n<size=28>Get back in the fight. Press ENTER to try again.</size>";
            deathTyping = deathText.gameObject.AddComponent<MilitaryText>();
            deathPanel.enabled = false; deathText.enabled = false;
        }

        void UpdateSlots()
        {
            if (slots == null || manager == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i]; if (s == null) continue;
                bool on = i == manager.CurrentIndex;
                s.t = Mathf.MoveTowards(s.t, on ? 1f : 0f, 8f * Time.deltaTime);
                s.bg.color = Color.Lerp(HudStyle.Panel, new Color(.18f, .15f, .09f, .96f), s.t);
                s.accent.color = new Color(HudStyle.Amber.r, HudStyle.Amber.g, HudStyle.Amber.b, Mathf.Lerp(.3f, 1, s.t));
                s.icon.color = Color.Lerp(new Color(1f, 1f, 1f, 0.6f), Color.white, s.t);
                s.num.color = Color.Lerp(HudStyle.Muted, HudStyle.Amber, s.t);
                var w = manager.weapons[i];
                // slot yang kehabisan amunisi total diredupkan merah
                if (w != null && w.Ammo == 0 && w.Reserve == 0) s.icon.color = Color.Lerp(s.icon.color, new Color(1f, 0.3f, 0.3f, 0.7f), 0.7f);
            }
        }

        void Update()
        {
            UpdateSlots();
            var wp = weapon;
            if (wp != null)
            {
                nameText.text = wp.displayName.ToUpper();
                {
                    ammoText.text = wp.Ammo + "  <size=28>| " + wp.Reserve + "</size>";
                    promptTyping.Speak((!wp.IsReloading && wp.Ammo == 0 && wp.Reserve > 0) ? "Press R to reload" :
                                   (!wp.IsReloading && wp.Ammo == 0 && wp.Reserve == 0) ? "Out of ammo. Find a resupply." : "");
                    promptPanel.enabled = msgText.text.Length > 0;
                }

                // crosshair melebar saat bergerak/menembak
                float target = 8f;
                if (controller != null) target += controller.HorizontalVelocity.magnitude * 1.6f + (controller.IsGrounded ? 0f : 10f);
                target += wp.RecoilFraction * 6f;
                if (wp.IsAiming) target = 3f + wp.RecoilFraction * 2f;
                spreadVis = Mathf.Lerp(spreadVis, target, 14f * Time.deltaTime);
                float a = MinimapController.Instance != null && MinimapController.Instance.IsLarge ? 0 : wp.IsAiming ? .35f : .9f;
                Vector2[] offs = { new Vector2(-spreadVis - 7, 0), new Vector2(spreadVis + 7, 0), new Vector2(0, spreadVis + 7), new Vector2(0, -spreadVis - 7) };
                for (int i = 0; i < 4; i++)
                {
                    ((RectTransform)cross[i].transform).anchoredPosition = offs[i];
                    cross[i].color = new Color(1f, 1f, 1f, a);
                }
                if (crossImg != null)
                {
                    float sz = 30f + spreadVis * 2.6f;
                    crossImg.rectTransform.sizeDelta = new Vector2(sz, sz);
                    crossImg.color = new Color(1f, 1f, 1f, a);
                }
                dot.color = new Color(1, 1, 1, a);
            }
            bool hit = Time.time < hitUntil;
            if (hitImg != null) hitImg.color = new Color(1f, 1f, 1f, hit ? 1f : 0f);
            if (headImg != null) headImg.color = new Color(1f, 1f, 1f, Mathf.Clamp01((headUntil - Time.time) / 0.28f));
            for (int i = 0; i < 4; i++)
            {
                Vector2[] hd = { new Vector2(-9, 9), new Vector2(9, 9), new Vector2(-9, -9), new Vector2(9, -9) };
                ((RectTransform)hitMark[i].transform).anchoredPosition = hd[i];
                hitMark[i].color = new Color(1f, 0.25f, 0.2f, hit ? 1f : 0f);
            }
            if (toastText != null)
            {
                float left = toastUntil - Time.time;
                var c = toastText.color; c.a = Mathf.Clamp01(left / 0.5f); toastText.color = c;
                toastPanel.enabled = left > 0;
            }
            if (health != null)
            {
                float f = health.Health / health.maxHealth;
                healthFill.rectTransform.sizeDelta = new Vector2(280f * f, 6);
                healthFill.color = f < .3f ? HudStyle.Danger : HudStyle.Amber;
                healthText.text = Mathf.CeilToInt(health.Health) + " <size=20>/ " + Mathf.CeilToInt(health.maxHealth) + " HP</size>";
                if (health.IsDead)
                {
                    var view = GetComponent<PlayerDeathView>();
                    if (view != null && view.Settled) ShowDeath();
                    var kb = Keyboard.current;
                    if (Outpost.Enemy.WaveDirector.Instance == null && (view == null || view.Settled) && kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                        SceneManager.LoadScene(gameObject.scene.buildIndex);
                }
            }
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, 0.8f * Time.deltaTime);
            damageFlash.color = new Color(0.75f, 0f, 0f, flashAlpha);
        }
    }
}
