using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Outpost.Env;
using Outpost.Player;

namespace Outpost.Enemy
{
    /// <summary>
    /// Alur wave: Wave 1 -> 2 -> 3. Zombie muncul dari lane map industrial secara bergiliran dengan jumlah
    /// Wave berikutnya saat clear atau timer habis. Wave final menambah satu boss di setiap lane.
    /// Menang: wave terakhir (termasuk boss) habis. Kalah: generator hancur (atau player gugur, ditangani FpsHud).
    /// Semua angka bisa diubah di Inspector (objek "WaveDirector").
    /// </summary>
    public class WaveDirector : MonoBehaviour
    {
        [Serializable]
        public class Wave
        {
            public int perSide = 5;      // nama field repo dipertahankan agar data tetap kompatibel; total = perSide x 2, dibagi ke seluruh lane
            public bool boss;            // true = tambah satu boss di tiap lane
        }

        public enum Phase { Waiting, Banner, Spawning, Fighting, Cleared, Victory, Defeat }

        [Header("Sumber musuh")]
        public EnemyCatalog catalog;

        [Header("Daftar wave (urutan = Wave 1, 2, 3 ...)")]
        public Wave[] waves =
        {
            new Wave { perSide = 12 },
            new Wave { perSide = 18 },
            new Wave { perSide = 24, boss = true },
        };

        [Header("Waktu (detik)")]
        public float firstWaveDelay = 6f;
        [Min(10f)] public float waveInterval = 60f;
        public float bannerTime = 3f;
        public float spawnInterval = 0.45f;     // jeda antar zombie, lane bergiliran
        public float bossDelay = 3.5f;          // jeda setelah zombie terakhir muncul sampai boss muncul

        [Header("Boss")]
        public string bossVariantId = "boss_v1";    // id varian di EnemyCatalog; kosong = acak
        public float bossScale = 2f;                // ukuran akhir boss dibanding zombie biasa (x2)

        public static WaveDirector Instance { get; private set; }
        public Phase CurrentPhase { get; private set; }
        public int CurrentWave { get; private set; }
        public int TotalWaves { get { return waves != null ? waves.Length : 0; } }
        public int EnemiesAlive
        {
            get { int n = 0; foreach (var e in tracked) if (e != null && !e.IsDead) n++; return n; }
        }

        public event Action<int> OnWaveStarted;
        public event Action<int> OnWaveCleared;
        public event Action OnVictory;
        public event Action OnDefeat;

        readonly List<EnemyHealth> tracked = new List<EnemyHealth>();
        readonly List<EnvSpawnPoint> spawnPoints = new List<EnvSpawnPoint>();
        PlayerHealth playerHealth;

        Canvas canvas;
        Text waveText, countText, bannerText, subText, endText;
        Image endPanel;
        Image bossGlow;
        Sprite bossGlowSprite;
        bool dangerBanner;
        float bannerUntil, bannerStart;
        string bannerTitle = "";
        bool ended;
        float nextWaveAt = -1f;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            BuildUi();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (bossGlowSprite != null) { Destroy(bossGlowSprite.texture); Destroy(bossGlowSprite); }
            Time.timeScale = 1f;
        }

        void Start()
        {
            CollectSpawnPoints();
            playerHealth = PlayerHealth.Instance;
            if (playerHealth != null) playerHealth.OnDied += OnPlayerDied;
            foreach (var g in GeneratorHealth.All) g.OnDestroyed += OnGeneratorDestroyed;
            StartCoroutine(Run());
        }

        void OnDisable()
        {
            foreach (var g in GeneratorHealth.All) if (g != null) g.OnDestroyed -= OnGeneratorDestroyed;
            if (playerHealth != null) playerHealth.OnDied -= OnPlayerDied;
        }

        void CollectSpawnPoints()
        {
            spawnPoints.Clear();
            foreach (var p in FindObjectsByType<EnvSpawnPoint>(FindObjectsSortMode.None))
                if (p.group == "Enemy") spawnPoints.Add(p);
            if (spawnPoints.Count == 0) FailSpawn("No enemy routes are configured for the industrial map.");
        }

        // ---------------------------------------------------------------- alur

        IEnumerator Run()
        {
            if (ended) yield break;
            CurrentPhase = Phase.Waiting;
            yield return Countdown("READY", firstWaveDelay);
            for (int w = 0; w < waves.Length; w++)
            {
                if (ended) yield break;
                CurrentWave = w + 1;
                var def = waves[w];
                bool final = w == waves.Length - 1;
                nextWaveAt = final ? -1f : Time.time + Mathf.Max(waveInterval, bannerTime + spawnInterval * def.perSide * 2 + 1f);
                CurrentPhase = Phase.Banner;
                Banner(final ? "FINAL WAVE" : "WAVE " + CurrentWave, def.perSide * 2 + " zombies incoming. Hold the line!", bannerTime);
                yield return new WaitForSeconds(bannerTime);
                if (ended) yield break;
                CurrentPhase = Phase.Spawning;
                if (OnWaveStarted != null) OnWaveStarted(CurrentWave);
                yield return SpawnWave(def);
                if (ended) yield break;
                CurrentPhase = Phase.Fighting;
                while (!ended && EnemiesAlive > 0 && (final || Time.time < nextWaveAt))
                    yield return new WaitForSeconds(0.25f);
                if (ended) yield break;
                if (EnemiesAlive == 0 && OnWaveCleared != null) OnWaveCleared(CurrentWave);
            }
            Victory();
        }

        IEnumerator Countdown(string title, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !ended)
            {
                int left = Mathf.CeilToInt(end - Time.time);
                Banner(title, "First contact in " + left + "s. Get ready.", 1.2f);
                yield return new WaitForSeconds(0.25f);
            }
        }

        IEnumerator SpawnWave(Wave def)
        {
            var lanes = spawnPoints.GroupBy(p => p.lane).Select(g => Shuffled(g.ToList())).ToArray();
            if (lanes.Length == 0) { FailSpawn("No enemy approach lanes are available."); yield break; }
            for (int i = 0; i < def.perSide * 2; i++)
            {
                if (ended) yield break;
                var lane = lanes[i % lanes.Length];
                SpawnOne(lane[(i / lanes.Length) % lane.Count], EnemyKind.Normal);
                yield return new WaitForSeconds(spawnInterval);
            }
            if (def.boss)
            {
                Banner(lanes.Length > 1 ? "BOSSES INBOUND" : "BOSS INBOUND", "Heavy contact approaching. Protect the generator!", Mathf.Max(3f, bossDelay), true);
                yield return new WaitForSeconds(bossDelay);
                if (ended) yield break;
                foreach (var lane in lanes)
                {
                    if (ended) yield break;
                    SpawnOne(lane[0], EnemyKind.Boss);
                }
            }
        }

        void SpawnOne(EnvSpawnPoint point, EnemyKind kind)
        {
            if (catalog == null) { FailSpawn("Enemy catalog is not assigned."); return; }
            EnemyVariant v = null;
            if (kind == EnemyKind.Boss && !string.IsNullOrEmpty(bossVariantId))
                foreach (var x in catalog.variants) if (x != null && x.id == bossVariantId && x.prefab != null) { v = x; break; }
            if (v == null) v = catalog.Pick(kind, kind == EnemyKind.Boss ? Mathf.Max(3, CurrentWave) : CurrentWave);
            if (v == null) { FailSpawn("Enemy variant is unavailable: " + kind); return; }

            Vector2 jitter = UnityEngine.Random.insideUnitCircle * 0.6f;
            var go = EnemyDebugSpawner.SpawnAt(v.prefab, point.transform.position + new Vector3(jitter.x, 0f, jitter.y));
            if (go == null) { FailSpawn("Enemy entry point is outside the navigation surface: " + point.name); return; }
            go.transform.rotation = point.transform.rotation;
            var hp = go.GetComponent<EnemyHealth>();
            if (hp == null) { Destroy(go); FailSpawn("Enemy prefab has no health component."); return; }
            if (kind == EnemyKind.Boss && hp.config != null)
                go.transform.localScale = Vector3.one * (bossScale / Mathf.Max(0.01f, hp.config.visualScale));
            tracked.Add(hp);
        }

        void FailSpawn(string message)
        {
            Debug.LogError("[WaveDirector] " + message, this);
            ended = true; CurrentPhase = Phase.Defeat;
            ShowEnd("DEPLOYMENT FAILED", "Enemy routes are unavailable. Press ENTER to try again.", new Color(0.35f, 0f, 0f, 0.7f));
            StopControls();
            Time.timeScale = 0f;
        }

        void OnPlayerDied()
        {
            if (ended) return;
            ended = true; CurrentPhase = Phase.Defeat;
            if (OnDefeat != null) OnDefeat();
            StopControls();
            Time.timeScale = 0f;
        }

        List<EnvSpawnPoint> Shuffled(List<EnvSpawnPoint> src)
        {
            var l = new List<EnvSpawnPoint>(src);
            for (int i = l.Count - 1; i > 0; i--) { int k = UnityEngine.Random.Range(0, i + 1); var t = l[i]; l[i] = l[k]; l[k] = t; }
            return l;
        }

        // ---------------------------------------------------------------- akhir permainan

        void OnGeneratorDestroyed(GeneratorHealth g)
        {
            if (ended) return;
            ended = true; CurrentPhase = Phase.Defeat;
            if (OnDefeat != null) OnDefeat();
            ShowEnd("OUTPOST LOST", "The generator is down. Press ENTER to try again.", new Color(0.35f, 0f, 0f, 0.7f));
            StopControls();
            Time.timeScale = 0f;
        }

        void Victory()
        {
            if (ended) return;
            ended = true; CurrentPhase = Phase.Victory;
            if (OnVictory != null) OnVictory();
            ShowEnd("OUTPOST SECURED", "You held the line. Press ENTER to play again.", new Color(0f, 0.18f, 0.05f, 0.6f));
            StopControls();
        }

        void StopControls()
        {
            var hp = PlayerHealth.Instance;
            var c = hp != null ? hp.GetComponent<FpsController>() : null;
            if (c != null) c.InputEnabled = false;
            FpsController.LockCursor(false);
        }

        // ---------------------------------------------------------------- UI

        void Banner(string title, string sub, float seconds, bool danger = false)
        {
            if (bannerText == null) return;
            dangerBanner = danger;
            bannerText.color = danger ? new Color(1f, 0.18f, 0.12f, 0f) : new Color(1f, 0.82f, 0.3f, 0f);
            subText.color = danger ? new Color(1f, 0.85f, 0.8f, 0f) : new Color(1f, 1f, 1f, 0f);
            bannerText.text = title; subText.text = sub;
            if (title != bannerTitle) { bannerTitle = title; bannerStart = Time.unscaledTime; }   // fade-in hanya saat judul berganti
            bannerUntil = Time.unscaledTime + Mathf.Max(0.3f, seconds);
        }

        void ShowEnd(string title, string sub, Color panel)
        {
            if (endPanel == null) return;
            endPanel.color = panel; endPanel.enabled = true;
            endText.text = title + "\n<size=28>" + sub + "</size>"; endText.enabled = true;
            bannerUntil = 0f;
        }

        Text MakeText(string n, Transform parent, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.sizeDelta = box; rt.anchoredPosition = pos;
            var tx = go.GetComponent<Text>();
            tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tx.fontSize = size; tx.alignment = align; tx.color = new Color(1f, 0.96f, 0.85f, 0.95f);
            tx.horizontalOverflow = HorizontalWrapMode.Overflow; tx.verticalOverflow = VerticalWrapMode.Overflow;
            tx.raycastTarget = false; tx.supportRichText = true;
            var sh = go.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.75f); sh.effectDistance = new Vector2(2f, -2f);
            return tx;
        }

        void BuildUi()
        {
            var cg = new GameObject("WaveHUD", typeof(Canvas), typeof(CanvasScaler));
            cg.transform.SetParent(transform, false);
            canvas = cg.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 60;
            var sc = cg.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            var top = new Vector2(0.5f, 1f);
            waveText = MakeText("WaveText", cg.transform, 38, TextAnchor.UpperCenter, top, new Vector2(0, -22), new Vector2(700, 50));
            countText = MakeText("CountText", cg.transform, 26, TextAnchor.UpperCenter, top, new Vector2(0, -68), new Vector2(700, 36));
            var glow = new GameObject("BossWarningGlow", typeof(RectTransform), typeof(Image));
            glow.transform.SetParent(cg.transform, false);
            var gr = (RectTransform)glow.transform;
            gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.72f);
            gr.sizeDelta = new Vector2(1500, 270); gr.anchoredPosition = new Vector2(0, -30);
            var texture = new Texture2D(64, 16, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 16; y++) for (int x = 0; x < 64; x++)
            {
                float u = (x + 0.5f - 32f) / 32f, v = (y + 0.5f - 8f) / 8f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(u * u + v * v))));
            }
            texture.Apply();
            bossGlowSprite = Sprite.Create(texture, new Rect(0, 0, 64, 16), new Vector2(0.5f, 0.5f));
            bossGlow = glow.GetComponent<Image>(); bossGlow.sprite = bossGlowSprite;
            bossGlow.raycastTarget = false; bossGlow.color = Color.clear;
            bannerText = MakeText("Banner", cg.transform, 84, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(1400, 120));
            subText = MakeText("BannerSub", cg.transform, 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.72f), new Vector2(0, -78), new Vector2(1400, 50));
            bannerText.color = new Color(1f, 0.82f, 0.3f, 0f); subText.color = new Color(1f, 1f, 1f, 0f);

            var pgo = new GameObject("EndPanel", typeof(RectTransform), typeof(Image));
            pgo.transform.SetParent(cg.transform, false);
            var pr = (RectTransform)pgo.transform; pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = pr.offsetMax = Vector2.zero;
            endPanel = pgo.GetComponent<Image>(); endPanel.raycastTarget = false; endPanel.enabled = false;
            endText = MakeText("EndText", cg.transform, 64, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 220));
            endText.enabled = false;
        }

        void Update()
        {
            if (waveText != null)
            {
                bool started = CurrentWave > 0;
                waveText.text = started ? "WAVE " + CurrentWave + " / " + TotalWaves : "";
                countText.text = started && CurrentPhase != Phase.Victory ? "ZOMBIES: " + EnemiesAlive + (nextWaveAt > 0f ? "   NEXT WAVE IN " + Mathf.Max(0, Mathf.CeilToInt(nextWaveAt - Time.time)) + "s" : "   FINAL WAVE") : "";
            }
            if (bannerText != null)
            {
                float left = bannerUntil - Time.unscaledTime;
                float a = left <= 0f ? 0f : Mathf.Clamp01(Mathf.Min(left / 0.4f, (Time.unscaledTime - bannerStart) / 0.25f));
                var c = bannerText.color; c.a = a; bannerText.color = c;
                var c2 = subText.color; c2.a = a; subText.color = c2;
                if (bossGlow != null)
                    bossGlow.color = new Color(0.65f, 0.005f, 0.005f, dangerBanner ? a * (0.38f + 0.08f * Mathf.Sin((Time.unscaledTime - bannerStart) * 5f)) : 0f);
            }
            if (ended)
            {
                var kb = Keyboard.current;
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                {
                    Time.timeScale = 1f;
                    SceneManager.LoadScene(gameObject.scene.buildIndex);
                }
            }
        }
    }
}
