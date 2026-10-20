using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Outpost.Env;
using Outpost.Player;

namespace Outpost.Enemy
{
    /// <summary>
    /// Alur wave: Wave 1 -> 2 -> 3. Zombie muncul dari dua gerbang (kiri = Lane West, kanan = Lane East) dengan jumlah
    /// sama di tiap sisi. Wave berikutnya mulai setelah semua zombie wave itu mati. Wave 3 menambah 1 boss berskala x2.
    /// Menang: wave terakhir (termasuk boss) habis. Kalah: generator hancur (atau player gugur, ditangani FpsHud).
    /// Semua angka bisa diubah di Inspector (objek "WaveDirector").
    /// </summary>
    public class WaveDirector : MonoBehaviour
    {
        [Serializable]
        public class Wave
        {
            public int perSide = 5;      // jumlah zombie di SETIAP gerbang (total = perSide x 2)
            public bool boss;            // true = tambah 1 boss di wave ini
        }

        public enum Phase { Waiting, Banner, Spawning, Fighting, Cleared, Victory, Defeat }

        [Header("Sumber musuh")]
        public EnemyCatalog catalog;

        [Header("Daftar wave (urutan = Wave 1, 2, 3 ...)")]
        public Wave[] waves =
        {
            new Wave { perSide = 5 },                 // wave 1: 10 zombie (5 kiri + 5 kanan)
            new Wave { perSide = 7 },                 // wave 2: 14 zombie (7 + 7)
            new Wave { perSide = 9, boss = true },    // wave 3: 18 zombie (9 + 9) + 1 boss
        };

        [Header("Waktu (detik)")]
        public float firstWaveDelay = 6f;
        public float betweenWavesDelay = 10f;
        public float bannerTime = 3f;
        public float spawnInterval = 0.45f;     // jeda antar zombie saat muncul (kiri-kanan bergantian)
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
        readonly List<EnvSpawnPoint> west = new List<EnvSpawnPoint>();
        readonly List<EnvSpawnPoint> east = new List<EnvSpawnPoint>();

        Canvas canvas;
        Text waveText, countText, bannerText, subText, endText;
        Image endPanel;
        float bannerUntil, bannerStart;
        string bannerTitle = "";
        bool ended;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            BuildUi();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        void Start()
        {
            CollectSpawnPoints();
            foreach (var g in GeneratorHealth.All) g.OnDestroyed += OnGeneratorDestroyed;
            StartCoroutine(Run());
        }

        void OnDisable()
        {
            foreach (var g in GeneratorHealth.All) if (g != null) g.OnDestroyed -= OnGeneratorDestroyed;
        }

        void CollectSpawnPoints()
        {
            west.Clear(); east.Clear();
            foreach (var p in FindObjectsByType<EnvSpawnPoint>(FindObjectsSortMode.None))
            {
                if (p.group != "Enemy") continue;
                if (p.lane == LaneId.West) west.Add(p);
                else if (p.lane == LaneId.East) east.Add(p);
            }
            if (west.Count == 0 || east.Count == 0) Debug.LogWarning("[WaveDirector] titik spawn West/East tidak ditemukan (West=" + west.Count + ", East=" + east.Count + ")");
        }

        // ---------------------------------------------------------------- alur

        IEnumerator Run()
        {
            CurrentPhase = Phase.Waiting;
            yield return Countdown("BERSIAPLAH", firstWaveDelay);

            for (int w = 0; w < waves.Length; w++)
            {
                CurrentWave = w + 1;
                var def = waves[w];
                int total = def.perSide * 2 + (def.boss ? 1 : 0);

                CurrentPhase = Phase.Banner;
                Banner("WAVE " + CurrentWave, def.perSide * 2 + " zombie dari kiri & kanan" + (def.boss ? "  +  BOSS" : ""), bannerTime);
                yield return new WaitForSeconds(bannerTime);
                if (ended) yield break;

                CurrentPhase = Phase.Spawning;
                if (OnWaveStarted != null) OnWaveStarted(CurrentWave);
                yield return SpawnWave(def);
                if (ended) yield break;

                CurrentPhase = Phase.Fighting;
                while (EnemiesAlive > 0 && !ended) yield return new WaitForSeconds(0.25f);
                if (ended) yield break;

                if (OnWaveCleared != null) OnWaveCleared(CurrentWave);
                if (w < waves.Length - 1)
                {
                    CurrentPhase = Phase.Cleared;
                    yield return Countdown("WAVE " + CurrentWave + " SELESAI", betweenWavesDelay);
                    if (ended) yield break;
                }
            }
            Victory();
        }

        IEnumerator Countdown(string title, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !ended)
            {
                int left = Mathf.CeilToInt(end - Time.time);
                Banner(title, (CurrentWave < waves.Length ? "wave " + (CurrentWave + 1) : "wave 1") + " dimulai dalam " + left, 1.2f);
                yield return new WaitForSeconds(0.25f);
            }
        }

        IEnumerator SpawnWave(Wave def)
        {
            var wl = Shuffled(west); var el = Shuffled(east);
            for (int i = 0; i < def.perSide; i++)
            {
                if (ended) yield break;
                SpawnOne(wl, i, EnemyKind.Normal, 1);
                SpawnOne(el, i, EnemyKind.Normal, -1);
                yield return new WaitForSeconds(spawnInterval);
            }
            if (def.boss)
            {
                yield return new WaitForSeconds(bossDelay);
                if (ended) yield break;
                Banner("BOSS ZOMBIE", "waspada!", 3f);
                // boss keluar dari salah satu gerbang secara acak
                bool fromWest = UnityEngine.Random.value < 0.5f;
                SpawnOne(fromWest ? wl : el, 0, EnemyKind.Boss, fromWest ? 1 : -1);
            }
        }

        void SpawnOne(List<EnvSpawnPoint> pts, int index, EnemyKind kind, float side)
        {
            if (catalog == null || pts.Count == 0) return;
            EnemyVariant v = null;
            if (kind == EnemyKind.Boss && !string.IsNullOrEmpty(bossVariantId))
                foreach (var x in catalog.variants) if (x != null && x.id == bossVariantId && x.prefab != null) { v = x; break; }
            if (v == null) v = catalog.Pick(kind, kind == EnemyKind.Boss ? Mathf.Max(3, CurrentWave) : CurrentWave);
            if (v == null) { Debug.LogWarning("[WaveDirector] tidak ada varian " + kind + " untuk wave " + CurrentWave); return; }

            var pt = pts[index % pts.Count];
            Vector2 j = UnityEngine.Random.insideUnitCircle * 0.8f;
            var go = EnemyDebugSpawner.SpawnAt(v.prefab, pt.transform.position + new Vector3(j.x, 0f, j.y));
            if (go == null) return;
            go.transform.rotation = Quaternion.Euler(0f, side > 0f ? 90f : 270f, 0f);   // menghadap ke tengah peta

            var hp = go.GetComponent<EnemyHealth>();
            if (kind == EnemyKind.Boss && hp != null && hp.config != null)
                go.transform.localScale = Vector3.one * (bossScale / Mathf.Max(0.01f, hp.config.visualScale));   // ukuran akhir = bossScale x zombie biasa
            if (hp != null) tracked.Add(hp);
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
            ShowEnd("GENERATOR HANCUR", "tekan ENTER untuk mengulang", new Color(0.35f, 0f, 0f, 0.7f));
            StopControls();
            Time.timeScale = 0f;
        }

        void Victory()
        {
            if (ended) return;
            ended = true; CurrentPhase = Phase.Victory;
            if (OnVictory != null) OnVictory();
            ShowEnd("MISI BERHASIL", "semua wave dikalahkan  -  tekan ENTER untuk main lagi", new Color(0f, 0.18f, 0.05f, 0.6f));
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

        void Banner(string title, string sub, float seconds)
        {
            if (bannerText == null) return;
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
                countText.text = started && CurrentPhase != Phase.Victory ? "ZOMBIE: " + EnemiesAlive : "";
            }
            if (bannerText != null)
            {
                float left = bannerUntil - Time.unscaledTime;
                float a = left <= 0f ? 0f : Mathf.Clamp01(Mathf.Min(left / 0.4f, (Time.unscaledTime - bannerStart) / 0.25f));
                var c = bannerText.color; c.a = a; bannerText.color = c;
                var c2 = subText.color; c2.a = a; subText.color = c2;
            }
            if (ended)
            {
                var kb = Keyboard.current;
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                {
                    Time.timeScale = 1f;
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
            }
        }
    }
}
