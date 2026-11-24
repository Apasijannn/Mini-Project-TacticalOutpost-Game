using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Outpost.Enemy;
using Outpost.Env;
using Outpost.Pickups;
using Outpost.Player;
using Outpost.UI;

namespace Outpost.Final
{
    /// <summary>
    /// Orkestrasi mode Final. Alur integrasi:
    ///   Player Performance (PerformanceTracker) → DDA (DdaModel) → PCG event wave + parameter wave/zombie + bobot Utility AI sekutu → gameplay.
    /// Map, WaveDirector, dan EnemyBrain asli tetap dipakai; yang diganti hanya field publik instance runtime
    /// (waves[i].perSide, spawnInterval, catalog, DropManager.normalDropChance) dan config clone per zombie.
    /// </summary>
    public sealed class FinalSession : MonoBehaviour
    {
        public static FinalSession Instance { get; private set; }

        public FinalProfile Profile { get; private set; }
        public int Seed { get; private set; }
        public float Difficulty { get; private set; }
        public float Skill { get; private set; }
        public DdaOutput DdaOut { get; private set; }
        public DdaModel.Components LastScore { get; private set; }
        public DdaModel.Components PreviewScore { get; private set; }
        public float PreviewDifficulty { get; private set; }
        public bool DdaEnabled { get; set; }
        public WaveEventPlan CurrentEvent { get; private set; }
        public WaveEventPlan NextEvent { get; private set; }
        public TacticalPoints.Result Tactical { get; private set; }
        public PerformanceTracker Tracker { get; private set; }
        public GeneratorHealth Generator { get { foreach (var g in GeneratorHealth.All) if (g != null && g.IsAlive) return g; return GeneratorHealth.All.Count > 0 ? GeneratorHealth.All[0] : null; } }
        public Vector3 GeneratorPosition { get { var g = Generator; return g != null ? g.transform.position : Vector3.zero; } }
        /// <summary>Pusat generator di permukaan tanah (rata-rata tinggi attack point).</summary>
        public Vector3 GroundCenter
        {
            get
            {
                var g = Generator; if (g == null) return Vector3.zero;
                Vector3 c = g.transform.position; float y = 0f; int n = 0;
                foreach (var ap in g.AttackPoints) if (ap != null) { y += ap.position.y; n++; }
                if (n > 0) c.y = y / n;
                return c;
            }
        }
        public bool Running { get { return ready && wave != null && wave.CurrentPhase != WaveDirector.Phase.Victory && wave.CurrentPhase != WaveDirector.Phase.Defeat; } }
        public IReadOnlyList<string> Messages { get { return messages; } }
        public readonly List<string> History = new List<string>();          // ringkasan per wave untuk panel
        public int AgentTypeId { get; private set; }

        WaveDirector wave;
        DropManager drops;
        EnemyCatalog runtimeCatalog;
        int[] basePerSide;
        float baseInterval, baseDrop;
        bool ready;
        float nextPlan;
        WaveEvent? previousEvent;
        readonly List<string> messages = new List<string>();
        readonly List<ScriptableObject> clones = new List<ScriptableObject>();
        readonly Dictionary<Light, float> lightBackup = new Dictionary<Light, float>();
        bool fogBackupSet, fogWas; float fogDensityWas; Color fogColorWas; FogMode fogModeWas; float ambientWas;
        float eventSight = 1f, eventSpeed = 1f, eventCooldown = 1f;

        static readonly FieldInfo MaxHealthField = typeof(EnemyHealth).GetField("<MaxHealth>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo HealthField = typeof(EnemyHealth).GetField("<Health>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

        Canvas canvas; TMP_Text eventTitle, eventSub; Image eventPanel, eventEdge; MilitaryText eventTyping; float eventUntil;

        void Awake() { Instance = this; }

        IEnumerator Start()
        {
            // tunggu IndustrialGameSession selesai memuat map dan menyalakan sistem gameplay
            float timeout = Time.time + 30f;
            while ((WaveDirector.Instance == null || PlayerHealth.Instance == null || GeneratorHealth.All.Count == 0) && Time.time < timeout) yield return null;
            wave = WaveDirector.Instance;
            if (wave == null) { Debug.LogError("[FINAL] WaveDirector tidak ditemukan; mode Final tidak aktif.", this); yield break; }
            yield return null;                                               // biarkan NavMesh & WaveDirector.Start selesai

            Profile = FinalProfile.Load();
            DdaEnabled = Profile.ddaEnabled;
            Seed = FinalMode.NextSeed(Profile);
            Difficulty = Skill = FinalMode.CarriedDifficulty ?? Profile.startDifficulty;
            DdaOut = DdaModel.Output(Profile, Difficulty);
            PreviewDifficulty = Difficulty;
            drops = FindAnyObjectByType<DropManager>();
            baseDrop = drops != null ? drops.normalDropChance : 0.3f;
            baseInterval = wave.spawnInterval;
            wave.waves = wave.waves.Select(w => JsonUtility.FromJson<WaveDirector.Wave>(JsonUtility.ToJson(w))).ToArray();
            basePerSide = wave.waves.Select(w => w.perSide).ToArray();
            if (wave.catalog != null)
            {
                runtimeCatalog = Instantiate(wave.catalog); runtimeCatalog.name = "EnemyCatalog (Final runtime)";
                runtimeCatalog.variants = wave.catalog.variants.Select(v => new EnemyVariant { id = v.id, prefab = v.prefab, kind = v.kind, minWave = v.minWave, maxWave = v.maxWave, weight = v.weight }).ToList();
                wave.catalog = runtimeCatalog;
                var normal = runtimeCatalog.variants.FirstOrDefault(v => v.prefab != null && v.kind == EnemyKind.Normal);
                var na = normal != null ? normal.prefab.GetComponent<NavMeshAgent>() : null;
                AgentTypeId = na != null ? na.agentTypeID : 0;
            }

            Tracker = gameObject.AddComponent<PerformanceTracker>();
            Tracker.Begin(PlayerHealth.Instance);
            EnemyHealth.AnySpawned += OnEnemySpawned;
            wave.OnWaveStarted += OnWaveStarted;
            wave.OnDefeat += OnDefeat;
            wave.OnVictory += OnVictory;

            Tactical = TacticalPoints.Generate(Profile, Seed, Generator, AgentTypeId);
 Log("Generator " + GeneratorPosition.ToString("F1") + " ground " + GroundCenter.ToString("F1") + " · attack points " + (Generator != null ? Generator.AttackPoints.Length : 0));
            Log("Seed " + Seed + " · tactical points " + Tactical.points.Count + "/" + Profile.tacticalPointCount
                + " (candidates " + Tactical.candidates + ", valid " + Tactical.valid + ", rejected: " + Tactical.RejectionText + ")");
            for (int i = 0; i < Profile.allyCount; i++) SpawnAlly(i);

            BuildUi();
            DynamicBlackoutSystem.EnsureInstalled();
            gameObject.AddComponent<FinalDebugView>();
            ApplyOutputs();
            ready = true;
            PlanNextWave();
            Log("DDA start difficulty " + Difficulty.ToString("0.00") + " (" + DdaModel.Label(Difficulty) + ")" + (FinalMode.CarriedDifficulty.HasValue ? " — carried from last attempt" : ""));
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            EnemyHealth.AnySpawned -= OnEnemySpawned;
            if (wave != null) { wave.OnWaveStarted -= OnWaveStarted; wave.OnDefeat -= OnDefeat; wave.OnVictory -= OnVictory; }
            ClearEventEffects();
            foreach (var c in clones) if (c != null) Destroy(c);
            if (runtimeCatalog != null) Destroy(runtimeCatalog);
        }

        void Update()
        {
            if (!ready) return;
            if (Time.time >= nextPlan) { nextPlan = Time.time + 0.5f; PlanNextWave(); }
            UpdateEventBanner();
        }

        // ---------------------------------------------------------------- DDA + rencana wave

        /// <summary>Pratinjau bergulir: difficulty sementara dari performa wave berjalan → isi definisi wave berikutnya sebelum banner muncul.</summary>
        void PlanNextWave()
        {
            if (!Running || wave.CurrentPhase == WaveDirector.Phase.Banner || wave.CurrentPhase == WaveDirector.Phase.Spawning) return;
            int next = wave.CurrentWave + 1;
            if (next > wave.TotalWaves) return;
            float d = Difficulty, s = Skill;
            if (wave.CurrentWave > 0 && DdaEnabled)
            {
                PreviewScore = DdaModel.Score(Profile, Tracker.Current);
                DdaModel.Step(Profile, PreviewScore.score, ref s, ref d);
            }
            PreviewDifficulty = d;
            NextEvent = WaveEventPlanner.Plan(Profile, Seed, next, wave.TotalWaves, d, previousEvent);
            wave.waves[next - 1].perSide = PlannedPerSide(next, DdaModel.Output(Profile, d), NextEvent);
        }

        int PlannedPerSide(int waveNumber, DdaOutput o, WaveEventPlan plan)
        {
            float f = (DdaEnabled ? o.countMultiplier : 1f) * (plan != null && plan.Has(WaveEvent.Horde) ? Profile.hordeCountFactor : 1f);
            return Mathf.Max(2, Mathf.RoundToInt(basePerSide[waveNumber - 1] * f));
        }

        void OnWaveStarted(int n)
        {
            // 1) DDA: evaluasi performa wave sebelumnya (wave 1 memakai difficulty awal)
            var window = Tracker.Cut();
            if (n > 1 && DdaEnabled)
            {
                LastScore = DdaModel.Score(Profile, window);
                float d = Difficulty, s = Skill;
                DdaModel.Step(Profile, LastScore.score, ref s, ref d);
                Log(string.Format("DDA wave {0} → acc {1:0%} ({2:0.00}) · dmg {3:0} ({4:0.00}) · gen -{5:0} ({6:0.00}) · kills {7} ({8:0.00}) → perf {9:0.00} · skill {10:0.00} · difficulty {11:0.00} → {12:0.00}",
                    n - 1, window.Accuracy, LastScore.accuracy, window.damageTaken, LastScore.damage, window.generatorLoss, LastScore.generator,
                    window.kills, LastScore.killRate, LastScore.score, s, Difficulty, d));
                Skill = s; Difficulty = d;
            }
            DdaOut = DdaModel.Output(Profile, Difficulty);

            // 2) PCG: event wave (seed + difficulty yang sudah dikunci)
            CurrentEvent = WaveEventPlanner.Plan(Profile, Seed, n, wave.TotalWaves, Difficulty, previousEvent);
            previousEvent = CurrentEvent.primary;
            wave.waves[n - 1].perSide = PlannedPerSide(n, DdaOut, CurrentEvent);   // dibaca ulang SpawnWave tiap iterasi
            ApplyOutputs();
            ApplyEvent(CurrentEvent);

            string line = "W" + n + "  D " + Difficulty.ToString("0.00") + " " + DdaModel.Label(Difficulty) + "  " + CurrentEvent.Title + "  zombies " + wave.waves[n - 1].perSide * 2;
            History.Add(line);
            Log("Wave " + n + ": " + CurrentEvent.Title + " (budget " + CurrentEvent.budget.ToString("0.0") + ", attempts " + CurrentEvent.attempts
                + ", rejected " + CurrentEvent.RejectionText + ") · zombies " + wave.waves[n - 1].perSide * 2);
            WriteCsv(n - 1, window);
        }

        /// <summary>Terapkan output DDA ke parameter runtime (interval spawn, bobot varian, drop chance).</summary>
        void ApplyOutputs()
        {
            var o = DdaOut;
            bool horde = CurrentEvent != null && CurrentEvent.Has(WaveEvent.Horde);
            wave.spawnInterval = (DdaEnabled ? o.spawnInterval : baseInterval) * (horde ? Profile.hordeIntervalFactor : 1f);
            if (drops != null) drops.normalDropChance = DdaEnabled ? o.dropChance : baseDrop;
            if (runtimeCatalog != null && DdaEnabled)
                foreach (var v in runtimeCatalog.variants)
                    if (v.kind == EnemyKind.Normal) v.weight = DdaModel.VariantWeight(Profile, v.id, Difficulty, v.weight);
        }

        /// <summary>Demo/debug: paksa difficulty naik/turun (F6/F7).</summary>
        public void ForceDifficulty(float delta)
        {
            Difficulty = Skill = Mathf.Clamp01(Difficulty + delta);
            DdaOut = DdaModel.Output(Profile, Difficulty);
            ApplyOutputs();
            Log("Difficulty forced to " + Difficulty.ToString("0.00") + " (" + DdaModel.Label(Difficulty) + ")");
        }

        // ---------------------------------------------------------------- modifier zombie per spawn

        void OnEnemySpawned(EnemyHealth e)
        {
            if (!ready || e == null || e.config == null) return;
            var src = e.config;
            var c = Instantiate(src); c.name = src.name + " (Final)";
            float hp = DdaEnabled ? DdaOut.enemyHealth : 1f, spd = DdaEnabled ? DdaOut.enemySpeed : 1f;
            c.maxHealth = src.maxHealth * hp;
            c.walkSpeed = src.walkSpeed * spd;
            c.runSpeed = src.runSpeed * spd * eventSpeed;
            c.attackCooldown = src.attackCooldown * eventCooldown;
            c.sightRadius = src.sightRadius * eventSight;
            clones.Add(c);
            e.config = c;
            var brain = e.GetComponent<EnemyBrain>();
            if (brain != null) brain.config = c;
            if (MaxHealthField != null && HealthField != null) { MaxHealthField.SetValue(e, c.maxHealth); HealthField.SetValue(e, c.maxHealth); }
        }

        // ---------------------------------------------------------------- event wave

        void ApplyEvent(WaveEventPlan plan)
        {
            ClearEventEffects();
            eventSight = eventSpeed = eventCooldown = 1f;
            if (plan.Has(WaveEvent.Blackout))
            {
                if (DynamicBlackoutSystem.Instance != null)
                {
                    DynamicBlackoutSystem.Instance.SetWaveEventBlackout(true, Profile.blackoutLightFactor);
                }
                else
                {
                    foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    {
                        if (l == null || l.GetComponentInParent<AllyBrain>() != null || l.GetComponentInParent<PlayerHealth>() != null) continue;
                        lightBackup[l] = l.intensity;
                        l.intensity *= l.type == LightType.Directional ? 0.5f : Profile.blackoutLightFactor;
                    }
                    ambientWas = RenderSettings.ambientIntensity; RenderSettings.ambientIntensity *= 0.4f;
                }
                eventSight = Profile.blackoutSightFactor;
            }
            if (plan.Has(WaveEvent.Fog))
            {
                fogBackupSet = true; fogWas = RenderSettings.fog; fogDensityWas = RenderSettings.fogDensity; fogColorWas = RenderSettings.fogColor; fogModeWas = RenderSettings.fogMode;
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = Profile.fogDensity;
                RenderSettings.fogColor = new Color(0.12f, 0.13f, 0.14f);
            }
            if (plan.Has(WaveEvent.Frenzy)) { eventSpeed = Profile.frenzySpeedFactor; eventCooldown = Profile.frenzyCooldownFactor; }
            if (plan.Has(WaveEvent.SupplyDrop)) DropSupplies(plan.wave);
            var info = WaveEventPlanner.Info(plan.primary);
            bool danger = info.cost >= 1.5f;
            ShowEvent(plan.Title, plan.assistSupply && plan.primary != WaveEvent.SupplyDrop ? info.subtitle + " Command sent extra supplies." : info.subtitle, danger);
        }

        void ClearEventEffects()
        {
            if (DynamicBlackoutSystem.Instance != null)
                DynamicBlackoutSystem.Instance.SetWaveEventBlackout(false, 1f);
            foreach (var kv in lightBackup) if (kv.Key != null) kv.Key.intensity = kv.Value;
            if (lightBackup.Count > 0) RenderSettings.ambientIntensity = ambientWas;
            lightBackup.Clear();
            if (fogBackupSet)
            {
                RenderSettings.fog = fogWas; RenderSettings.fogDensity = fogDensityWas; RenderSettings.fogColor = fogColorWas; RenderSettings.fogMode = fogModeWas;
                fogBackupSet = false;
            }
        }

        void DropSupplies(int waveNumber)
        {
            if (drops == null) return;
            var rng = new System.Random(Seed ^ 0x5AFE ^ waveNumber);
            var filter = new NavMeshQueryFilter { agentTypeID = AgentTypeId, areaMask = NavMesh.AllAreas };
            int placed = 0;
            for (int i = 0; i < Profile.supplyDropCount * 6 && placed < Profile.supplyDropCount; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 3f + (float)rng.NextDouble() * 5f;
                Vector3 p = GroundCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(p, out hit, 2.5f, filter)) continue;
                var prefab = placed % 2 == 0 ? drops.ammoPrefab : drops.healthPrefab;
                if (prefab != null) Instantiate(prefab, hit.position, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                placed++;
            }
            Log("Supply drop: " + placed + " crates near the generator.");
        }

        // ---------------------------------------------------------------- sekutu

        void SpawnAlly(int index)
        {
            var player = PlayerHealth.Instance;
            Vector3 basePos = player != null ? player.transform.position : GeneratorPosition;
            Vector3 offset = player != null ? (player.transform.right * (index % 2 == 0 ? 2.2f : -2.2f) - player.transform.forward * 1.2f) : Vector3.right * 2f;
            NavMeshHit hit;
            var filter = new NavMeshQueryFilter { agentTypeID = AgentTypeId, areaMask = NavMesh.AllAreas };
            if (!NavMesh.SamplePosition(basePos + offset, out hit, 4f, filter) && !NavMesh.SamplePosition(GeneratorPosition, out hit, 6f, filter))
            { Log("Ally " + (index + 1) + " could not be placed on the NavMesh."); return; }

            var go = new GameObject("Ally Defender " + (index + 1));
            go.SetActive(false);                                          // agent harus memakai agentTypeID zombie sebelum menempel ke NavMesh
            go.layer = 8;
            go.transform.position = hit.position;
            go.transform.rotation = player != null ? Quaternion.LookRotation(Vector3.ProjectOnPlane(player.transform.forward, Vector3.up)) : Quaternion.identity;
            var capsule = go.AddComponent<CapsuleCollider>(); capsule.center = new Vector3(0f, 0.9f, 0f); capsule.height = 1.8f; capsule.radius = 0.3f;
            var agent = go.AddComponent<NavMeshAgent>(); agent.agentTypeID = AgentTypeId;
            var body = go.AddComponent<AllyBody>();
            if (!body.Build(player, index == 0 ? new Color(0.3f, 0.75f, 1f) : new Color(0.4f, 1f, 0.5f)))
            { Destroy(go); Log("Ally model could not be built from the player model."); return; }
            var brain = go.AddComponent<AllyBrain>();
            string[] names = { "VIPER", "ECHO", "NOMAD" };
            brain.Init(Profile, this, body, AgentTypeId, names[index % names.Length]);
            Color allyMapColor = index == 0 ? new Color(0.22f, 0.90f, 1.00f, 1f) : new Color(0.40f, 1.00f, 0.55f, 1f);
            AllyMinimapBlip.Attach(go, allyMapColor);
            go.SetActive(true);
            if (!agent.isOnNavMesh) agent.Warp(hit.position);
            Log(brain.Callsign + " deployed (Utility AI) at " + hit.position.ToString("F1") + ", on NavMesh " + agent.isOnNavMesh + ", agent type " + AgentTypeId);
        }

        /// <summary>Titik taktis terbaik untuk sekutu. intercept = utamakan titik yang melihat generator dan dekat ancaman.</summary>
        public TacticalPoint BestPoint(AllyBrain ally, bool intercept)
        {
            if (Tactical == null || Tactical.points.Count == 0) return null;
            Vector3 threat = GeneratorPosition; int n = 0; Vector3 sum = Vector3.zero;
            if (intercept)
            {
                foreach (var e in EnemyHealth.Alive)
                    if (e != null && !e.IsDead && Vector3.Distance(e.transform.position, GeneratorPosition) < 12f) { sum += e.transform.position; n++; }
                if (n > 0) threat = sum / n;
            }
            TacticalPoint best = null; float bestScore = float.MinValue;
            foreach (var p in Tactical.points)
            {
                if (p.owner != null && p.owner != ally) continue;
                float s = intercept
                    ? (p.seesGenerator ? 0.4f : 0f) + 0.6f * Mathf.Clamp01(1f - Vector3.Distance(p.position, threat) / 30f) + 0.1f * p.cover
                    : p.score - 0.01f * Vector3.Distance(p.position, ally.transform.position);
                if (p == ally.HoldPoint) s += 0.05f;
                if (s > bestScore) { bestScore = s; best = p; }
            }
            return best;
        }

        public TacticalPoint SafestPoint(AllyBrain ally)
        {
            if (Tactical == null) return null;
            TacticalPoint best = null; float bestScore = float.MinValue;
            foreach (var p in Tactical.points)
            {
                if (p.owner != null && p.owner != ally) continue;
                float nearest = 60f;
                foreach (var e in EnemyHealth.Alive) if (e != null && !e.IsDead) nearest = Mathf.Min(nearest, Vector3.Distance(e.transform.position, p.position));
                float s = nearest / 60f + 0.3f * p.cover;
                if (s > bestScore) { bestScore = s; best = p; }
            }
            return best;
        }

        // ---------------------------------------------------------------- akhir

        void OnDefeat()
        {
            FinalMode.PendingSeed = Seed;
            FinalMode.CarriedDifficulty = Mathf.Max(0f, Difficulty - Profile.retryRelief);
            ClearEventEffects();
            Log("Defeat. Retry (ENTER) keeps seed " + Seed + " and lowers difficulty to " + FinalMode.CarriedDifficulty.Value.ToString("0.00") + ".");
            WriteCsv(wave.CurrentWave, Tracker.Current, "defeat");
        }

        void OnVictory()
        {
            FinalMode.CarriedDifficulty = Difficulty;
            ClearEventEffects();
            Log("Victory at difficulty " + Difficulty.ToString("0.00") + ".");
            WriteCsv(wave.CurrentWave, Tracker.Current, "victory");
        }

        // ---------------------------------------------------------------- log

        public void Log(string message)
        {
            Debug.Log("[FINAL] " + message, this);
            messages.Insert(0, message);
            if (messages.Count > 8) messages.RemoveAt(messages.Count - 1);
        }

        /// <summary>Satu baris per evaluasi untuk analisis EAS (DDA on/off, distribusi event).</summary>
        void WriteCsv(int evaluatedWave, PerformanceWindow w, string result = "")
        {
            try
            {
                string dir = Application.isEditor ? Path.Combine(Application.dataPath, "../Docs/Final") : Application.persistentDataPath;
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "SessionLog.csv");
                bool header = !File.Exists(path);
                using (var sw = new StreamWriter(path, true))
                {
                    if (header) sw.WriteLine("time,seed,dda,wave,shots,hits,accuracy,damage_taken,generator_loss,kills,ally_kills,seconds,performance,skill,difficulty,event,zombies_next,result");
                    var s = DdaModel.Score(Profile, w);
                    int nextIndex = Mathf.Clamp(evaluatedWave, 0, wave.waves.Length - 1);
                    sw.WriteLine(string.Join(",", new[] {
                        System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Seed.ToString(), DdaEnabled ? "on" : "off", evaluatedWave.ToString(),
                        w.shots.ToString(), w.hits.ToString(), w.Accuracy.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
                        w.damageTaken.ToString("0", System.Globalization.CultureInfo.InvariantCulture), w.generatorLoss.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
                        w.kills.ToString(), w.allyKills.ToString(), w.seconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
                        s.score.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture), Skill.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
                        Difficulty.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture), CurrentEvent != null ? CurrentEvent.primary.ToString() : "-",
                        (wave.waves[nextIndex].perSide * 2).ToString(), result }));
                }
            }
            catch (System.Exception ex) { Debug.LogWarning("[FINAL] SessionLog.csv tidak bisa ditulis: " + ex.Message); }
        }

        // ---------------------------------------------------------------- UI event banner

        void BuildUi()
        {
            var cg = new GameObject("Final HUD", typeof(Canvas), typeof(CanvasScaler));
            cg.transform.SetParent(transform, false);
            canvas = cg.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 61;
            var sc = cg.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            var anchor = new Vector2(.5f, .56f);
            eventPanel = HudStyle.Box("Event panel", cg.transform, anchor, Vector2.zero, new Vector2(760, 110), Color.clear);
            eventEdge = HudStyle.Box("Event edge", cg.transform, anchor, new Vector2(-380, 0), new Vector2(3, 110), Color.clear);
            eventTitle = HudStyle.Text("Event title", cg.transform, 44, TextAnchor.MiddleCenter, anchor, new Vector2(0, 16), new Vector2(720, 56));
            eventSub = HudStyle.Text("Event sub", cg.transform, 20, TextAnchor.MiddleCenter, anchor, new Vector2(0, -32), new Vector2(720, 30), true);
            eventTyping = eventTitle.gameObject.AddComponent<MilitaryText>();
            eventTitle.color = Color.clear; eventSub.color = Color.clear;
        }

        void ShowEvent(string title, string sub, bool danger)
        {
            if (eventTitle == null) return;
            eventTitle.color = danger ? HudStyle.Danger : HudStyle.Amber;
            eventSub.color = HudStyle.Ivory;
            eventTyping.Speak("// " + title);
            eventSub.text = sub;
            eventEdge.color = danger ? HudStyle.Danger : HudStyle.Amber;
            eventPanel.color = new Color(HudStyle.Panel.r, HudStyle.Panel.g, HudStyle.Panel.b, .9f);
            eventUntil = Time.unscaledTime + 4.5f;
        }

        void UpdateEventBanner()
        {
            if (eventTitle == null) return;
            float a = Mathf.Clamp01((eventUntil - Time.unscaledTime) / 0.5f);
            if (MinimapController.Instance != null && MinimapController.Instance.IsLarge) a = 0f;
            var c = eventTitle.color; c.a = a; eventTitle.color = c;
            c = eventSub.color; c.a = a; eventSub.color = c;
            c = eventPanel.color; c.a = .9f * a; eventPanel.color = c;
            c = eventEdge.color; c.a = a; eventEdge.color = c;
        }
    }
}
