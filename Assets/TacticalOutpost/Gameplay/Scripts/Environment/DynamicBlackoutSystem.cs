using System.Collections.Generic;
using UnityEngine;
using Outpost.Enemy;
using Outpost.Final;
using Outpost.Player;

namespace Outpost.Env
{
    public enum BlackoutTier
    {
        Normal,         // HP >= 75% (+shift): lampu stabil normal
        FlickerLow,     // HP < 75%: lampu mulai berkedip jarang
        FlickerHeavy,   // HP < 50%: kedipan makin intens + brownout singkat
        Chaos           // HP < 25%: chaos (kedipan tinggi + durasi padam total makin lama)
    }

    /// <summary>
    /// Dynamic Blackout System — kondisi pencahayaan lingkungan bereaksi terhadap HP Generator
    /// dan tingkat kesulitannya diskalakan oleh DDA (FinalSession.DdaOut):
    ///   - HP >= 75% : Normal (lampu stabil)
    ///   - HP <  75% : Kedipan jarang (voltage dip singkat sesekali)
    ///   - HP <  50% : Semakin sering berkedip + brownout singkat antar sirkuit lampu
    ///   - HP <  25% : Chaos / Total Blackout (kombinasi kedap-kedip intensitas tinggi & durasi padam total lebih lama)
    /// Bekerja harmonis dengan WaveEvent.Blackout (FinalSession), EnvFlicker, MinimapController, dan GeneratorDestruction.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class DynamicBlackoutSystem : MonoBehaviour
    {
        public static DynamicBlackoutSystem Instance { get; private set; }

        /// <summary>Faktor daya grid global (0..1) yang dibaca juga oleh EnvFlicker agar selaras saat blackout.</summary>
        public static float GlobalGridFactor { get; private set; } = 1f;

        public BlackoutTier CurrentTier { get; private set; } = BlackoutTier.Normal;
        public float CurrentGridFactor { get; private set; } = 1f;
        public float CombinedLightFactor { get; private set; } = 1f;
        public bool IsTotalDarkness { get; private set; }
        public float ActiveDifficulty { get; private set; } = 0.5f;
        public float FlickerScale { get; private set; } = 1f;
        public float OffDurationScale { get; private set; } = 1f;
        public float ThresholdFlickerLow { get; private set; } = 0.75f;
        public float ThresholdFlickerHeavy { get; private set; } = 0.50f;
        public float ThresholdChaos { get; private set; } = 0.25f;
        public bool WaveEventBlackoutActive { get { return waveEventActive; } }

        public string StatusLabel
        {
            get
            {
                if (IsTotalDarkness) return "TOTAL BLACKOUT";
                switch (CurrentTier)
                {
                    case BlackoutTier.FlickerLow: return "FLICKER";
                    case BlackoutTier.FlickerHeavy: return "UNSTABLE";
                    case BlackoutTier.Chaos: return "CHAOS";
                    default: return waveEventActive ? "EVENT BLACKOUT" : "STABLE";
                }
            }
        }

        struct TrackedLight
        {
            public Light light;
            public float baseIntensity;
            public bool isDirectional;
            public int circuit;      // 0..3 grup sirkuit lampu agar kedipan antar lampu tidak serentak kaku
            public float phase;
        }

        struct TrackedFixture
        {
            public Renderer renderer;
            public Color baseEmission;
            public int circuit;
            public MaterialPropertyBlock mpb;
        }

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        readonly List<TrackedLight> trackedLights = new List<TrackedLight>();
        readonly List<TrackedFixture> trackedFixtures = new List<TrackedFixture>();
        readonly HashSet<Light> knownLights = new HashSet<Light>();
        readonly HashSet<Renderer> knownFixtures = new HashSet<Renderer>();

        float baseAmbient = 1f;
        bool ambientCaptured;
        float nextScanTime;
        float waveEventFactor = 1f;
        bool waveEventActive;
        GeneratorDestruction cachedDestruction;

        // State internal siklus kedipan & padam
        float phaseTimer;
        bool inSurge;            // true = sedang fase padam / dip; false = sedang fase nyala / chaos strobe
        float nextBurstTimer;
        float burstEndTime;
        float smoothFactor = 1f;
        float lastToastTime = -99f;
        BlackoutTier lastAnnouncedTier = BlackoutTier.Normal;

        // Audio listrik sintetis (tanpa bergantung aset audio eksternal)
        AudioSource audioSrc;
        AudioClip buzzClip, powerDownClip, powerUpClip;

        public static DynamicBlackoutSystem EnsureInstalled()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Dynamic Blackout System");
            return go.AddComponent<DynamicBlackoutSystem>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            GlobalGridFactor = 1f;
            BuildAudio();
        }

        void OnDestroy()
        {
            RestoreAll();
            GlobalGridFactor = 1f;
            if (Instance == this) Instance = null;
            if (buzzClip != null) Destroy(buzzClip);
            if (powerDownClip != null) Destroy(powerDownClip);
            if (powerUpClip != null) Destroy(powerUpClip);
        }

        /// <summary>Dipanggil FinalSession saat WaveEvent.Blackout aktif/berakhir agar tidak bentrok menimpa intensitas lampu.</summary>
        public void SetWaveEventBlackout(bool active, float lightFactor)
        {
            waveEventActive = active;
            waveEventFactor = active ? Mathf.Clamp(lightFactor, 0.05f, 1f) : 1f;
            if (!ambientCaptured) CaptureEnvironment();
        }

        void LateUpdate()
        {
            if (!ambientCaptured || Time.time >= nextScanTime)
            {
                CaptureEnvironment();
                nextScanTime = Time.time + 2.5f;
            }

            UpdateDdaParameters();

            // Saat cinematic ledakan generator berjalan, tahan lampu redup stabil agar flash ledakan terlihat dramatis
            if (cachedDestruction != null && cachedDestruction.Playing)
            {
                ApplyLighting(0.08f, 0.12f, 0.18f, null);
                return;
            }

            var gen = ActiveGenerator();
            float hp = gen != null ? Mathf.Clamp01(gen.Fraction) : 1f;

            var session = FinalSession.Instance;
            bool enabledByProfile = session == null || session.Profile == null || session.Profile.dynamicBlackoutEnabled;
            BlackoutTier newTier = enabledByProfile ? EvaluateTier(hp) : BlackoutTier.Normal;

            if (newTier != CurrentTier)
                OnTierChanged(CurrentTier, newTier);
            CurrentTier = newTier;

            float dt = Time.deltaTime;
            float[] circuitFactors = EvaluateGridPower(hp, dt);
            ApplyLighting(CurrentGridFactor, ComputeDirectionalFactor(), ComputeAmbientFactor(), circuitFactors);
        }

        GeneratorHealth ActiveGenerator()
        {
            for (int i = 0; i < GeneratorHealth.All.Count; i++)
            {
                var g = GeneratorHealth.All[i];
                if (g != null && g.IsAlive) return g;
            }
            return GeneratorHealth.All.Count > 0 ? GeneratorHealth.All[0] : null;
        }

        void UpdateDdaParameters()
        {
            var session = FinalSession.Instance;
            if (session != null && session.Profile != null)
            {
                var p = session.Profile;
                bool ddaOn = session.DdaEnabled;
                ActiveDifficulty = ddaOn ? session.Difficulty : 0.5f;
                var o = DdaModel.Output(p, ActiveDifficulty);
                FlickerScale = ddaOn ? o.blackoutFlickerScale : 1f;
                OffDurationScale = ddaOn ? o.blackoutOffScale : 1f;
                float shift = ddaOn ? o.blackoutThresholdShift : 0f;
                ThresholdFlickerLow = Mathf.Clamp(p.blackoutTierFlickerLow + shift, 0.58f, 0.90f);
                ThresholdFlickerHeavy = Mathf.Clamp(p.blackoutTierFlickerHeavy + shift, 0.34f, ThresholdFlickerLow - 0.12f);
                ThresholdChaos = Mathf.Clamp(p.blackoutTierChaos + shift, 0.12f, ThresholdFlickerHeavy - 0.12f);
            }
            else
            {
                ActiveDifficulty = 0.5f;
                FlickerScale = 1f;
                OffDurationScale = 1f;
                ThresholdFlickerLow = 0.75f;
                ThresholdFlickerHeavy = 0.50f;
                ThresholdChaos = 0.25f;
            }
        }

        BlackoutTier EvaluateTier(float hp)
        {
            if (hp < ThresholdChaos) return BlackoutTier.Chaos;
            if (hp < ThresholdFlickerHeavy) return BlackoutTier.FlickerHeavy;
            if (hp < ThresholdFlickerLow) return BlackoutTier.FlickerLow;
            return BlackoutTier.Normal;
        }

        void OnTierChanged(BlackoutTier oldTier, BlackoutTier newTier)
        {
            inSurge = false;
            phaseTimer = 0f;
            nextBurstTimer = 0.2f;
            burstEndTime = 0f;

            bool worse = (int)newTier > (int)oldTier;
            if (worse)
            {
                if (newTier == BlackoutTier.Chaos) PlayClip(powerDownClip, 0.45f);
                else PlayClip(buzzClip, 0.30f);

                if (Time.unscaledTime - lastToastTime > 7f || (int)newTier > (int)lastAnnouncedTier)
                {
                    lastToastTime = Time.unscaledTime;
                    lastAnnouncedTier = newTier;
                    switch (newTier)
                    {
                        case BlackoutTier.FlickerLow:
                            HudEvents.ShowToast("POWER FLUCTUATION DETECTED", new Color(1f, 0.82f, 0.30f));
                            break;
                        case BlackoutTier.FlickerHeavy:
                            HudEvents.ShowToast("OUTPOST GRID UNSTABLE — PROTECT GENERATOR", new Color(1f, 0.55f, 0.20f));
                            break;
                        case BlackoutTier.Chaos:
                            HudEvents.ShowToast("CRITICAL POWER FAILURE — BLACKOUT IMMINENT", new Color(1f, 0.24f, 0.20f));
                            break;
                    }
                }
            }
            else if (oldTier != BlackoutTier.Normal)
            {
                PlayClip(powerUpClip, 0.32f);
                if (newTier == BlackoutTier.Normal && Time.unscaledTime - lastToastTime > 6f)
                {
                    lastToastTime = Time.unscaledTime;
                    HudEvents.ShowToast("POWER GRID STABILIZED", new Color(0.45f, 1f, 0.65f));
                }
            }

            if (FinalSession.Instance != null)
                FinalSession.Instance.Log("Power grid: " + oldTier + " → " + newTier + " (DDA flicker ×" + FlickerScale.ToString("0.00") + ", off ×" + OffDurationScale.ToString("0.00") + ")");
        }

        readonly float[] circuitBuf = new float[4];

        float[] EvaluateGridPower(float hp, float dt)
        {
            IsTotalDarkness = false;
            float t = Time.time;

            if (CurrentTier == BlackoutTier.Normal)
            {
                smoothFactor = Mathf.MoveTowards(smoothFactor, 1f, dt * 2.5f);
                CurrentGridFactor = smoothFactor;
                for (int i = 0; i < 4; i++) circuitBuf[i] = smoothFactor;
                return circuitBuf;
            }

            if (CurrentTier == BlackoutTier.FlickerLow)
            {
                // HP < 75%: mayoritas stabil, sesekali voltage dip singkat
                float severity = Mathf.InverseLerp(ThresholdFlickerLow, ThresholdFlickerHeavy, hp);
                nextBurstTimer -= dt;
                if (nextBurstTimer <= 0f)
                {
                    float dur = Random.Range(0.22f, 0.52f) * Mathf.Lerp(0.85f, 1.15f, severity) * OffDurationScale;
                    burstEndTime = t + dur;
                    nextBurstTimer = Random.Range(4.2f, 8.2f) * Mathf.Lerp(1.1f, 0.72f, severity) / Mathf.Max(0.4f, FlickerScale);
                    PlayClip(buzzClip, 0.16f);
                }

                float target = 0.96f;
                if (t < burstEndTime)
                {
                    float pulse = Mathf.PerlinNoise(t * 18f, 3.7f);
                    target = pulse < 0.45f ? Mathf.Lerp(0.32f, 0.55f, pulse / 0.45f) : Mathf.Lerp(0.68f, 0.96f, (pulse - 0.45f) / 0.55f);
                }
                smoothFactor = Mathf.MoveTowards(smoothFactor, target, dt * 14f);
                CurrentGridFactor = smoothFactor;
                for (int i = 0; i < 4; i++)
                {
                    float offset = (t < burstEndTime) ? (Mathf.PerlinNoise(t * 14f + i * 7.1f, 1.3f) - 0.5f) * 0.25f : 0f;
                    circuitBuf[i] = Mathf.Clamp01(smoothFactor + offset);
                }
                return circuitBuf;
            }

            if (CurrentTier == BlackoutTier.FlickerHeavy)
            {
                // HP < 50%: kedipan semakin intens + brownout singkat (lampu sempat hampir mati)
                float severity = Mathf.InverseLerp(ThresholdFlickerHeavy, ThresholdChaos, hp);
                phaseTimer -= dt;
                if (phaseTimer <= 0f)
                {
                    inSurge = !inSurge;
                    if (inSurge)
                    {
                        // Fase brownout singkat
                        phaseTimer = Random.Range(0.45f, 1.35f) * Mathf.Lerp(0.85f, 1.3f, severity) * OffDurationScale;
                        PlayClip(buzzClip, 0.24f);
                    }
                    else
                    {
                        // Fase kedip aktif antar brownout
                        phaseTimer = Random.Range(1.5f, 3.4f) * Mathf.Lerp(1.15f, 0.70f, severity) / Mathf.Max(0.4f, FlickerScale);
                    }
                }

                float baseVal = 0f;
                for (int i = 0; i < 4; i++)
                {
                    float n = Mathf.PerlinNoise(t * (11f + severity * 7f) * FlickerScale + i * 11.3f, 5.2f + i);
                    float c;
                    if (inSurge)
                    {
                        // Brownout berat: hampir gelap total dengan sesekali percikan listrik kecil
                        c = n > 0.84f ? Random.Range(0.28f, 0.55f) : Mathf.Lerp(0.04f, 0.16f, n);
                    }
                    else
                    {
                        // Kedap-kedip tidak stabil terus-menerus
                        c = n < 0.28f ? Mathf.Lerp(0.18f, 0.45f, n / 0.28f) : Mathf.Lerp(0.62f, 0.94f, (n - 0.28f) / 0.72f);
                    }
                    circuitBuf[i] = c;
                    baseVal += c;
                }
                baseVal *= 0.25f;
                smoothFactor = Mathf.MoveTowards(smoothFactor, baseVal, dt * 18f);
                CurrentGridFactor = smoothFactor;
                return circuitBuf;
            }

            // Tier 3 — CHAOS / TOTAL BLACKOUT (HP < 25%)
            // Kombinasi intensitas kedap-kedip tinggi + durasi lampu kondisi mati total yang semakin panjang
            float chaosT = Mathf.InverseLerp(ThresholdChaos, 0.02f, hp);
            phaseTimer -= dt;
            if (phaseTimer <= 0f)
            {
                inSurge = !inSurge;
                if (inSurge)
                {
                    // Fase PADAM TOTAL (semakin lama saat HP makin kritis & DDA makin tinggi)
                    float baseOff = Random.Range(2.1f, 4.8f) * Mathf.Lerp(1.0f, 1.75f, chaosT) * OffDurationScale;
                    phaseTimer = Mathf.Clamp(baseOff, 1.2f, 11f);
                    PlayClip(powerDownClip, 0.42f);
                }
                else
                {
                    // Fase CHAOS STROBE (kedap-kedip cepat intensitas tinggi, durasi makin pendek saat HP hampir 0)
                    float baseStrobe = Random.Range(1.2f, 2.8f) * Mathf.Lerp(1.1f, 0.55f, chaosT) / Mathf.Max(0.5f, FlickerScale);
                    phaseTimer = Mathf.Clamp(baseStrobe, 0.6f, 3.8f);
                    PlayClip(buzzClip, 0.34f);
                }
            }

            if (inSurge)
            {
                // Gelap total — seluruh lampu grid mati (sesekali 1 sirkuit memercik singkat saat hampir menyala lagi)
                IsTotalDarkness = true;
                bool preIgnite = phaseTimer < 0.35f;
                float sum = 0f;
                for (int i = 0; i < 4; i++)
                {
                    float spark = 0f;
                    if (preIgnite)
                    {
                        float p = Mathf.PerlinNoise(t * 28f + i * 9.7f, 2.1f);
                        spark = p > 0.72f ? (p - 0.72f) * 2.2f : 0f;
                    }
                    circuitBuf[i] = spark;
                    sum += spark;
                }
                smoothFactor = Mathf.MoveTowards(smoothFactor, sum * 0.25f, dt * 16f);
                CurrentGridFactor = smoothFactor;
                return circuitBuf;
            }
            else
            {
                // Chaos strobe berkecepatan tinggi: lonjakan tajam antara mati total (0) dan nyala terang (0.85) antar sirkuit
                float freq = Mathf.Lerp(15f, 24f, chaosT) * Mathf.Clamp(FlickerScale, 0.7f, 1.6f);
                float sum = 0f;
                for (int i = 0; i < 4; i++)
                {
                    float n1 = Mathf.PerlinNoise(t * freq + i * 13.7f, 8.4f);
                    float n2 = Mathf.PerlinNoise(t * (freq * 0.43f) + i * 5.1f, 19.2f);
                    float c;
                    if (n2 < 0.32f + chaosT * 0.22f)
                        c = 0f;                                          // sirkuit ini mati sementara
                    else if (n1 < 0.42f)
                        c = 0.04f;                                       // kedip jatuh gelap
                    else if (n1 > 0.76f)
                        c = Mathf.Lerp(0.70f, 1.05f, (n1 - 0.76f) / 0.24f); // lonjakan percikan terang
                    else
                        c = Mathf.Lerp(0.18f, 0.55f, (n1 - 0.42f) / 0.34f);
                    circuitBuf[i] = c;
                    sum += c;
                }
                smoothFactor = Mathf.MoveTowards(smoothFactor, sum * 0.25f, dt * 26f);
                CurrentGridFactor = smoothFactor;
                return circuitBuf;
            }
        }

        float ComputeDirectionalFactor()
        {
            float evDir = waveEventActive ? 0.5f : 1f;
            float dynDir;
            switch (CurrentTier)
            {
                case BlackoutTier.Normal: dynDir = 1f; break;
                case BlackoutTier.FlickerLow: dynDir = Mathf.Lerp(0.72f, 1f, CurrentGridFactor); break;
                case BlackoutTier.FlickerHeavy: dynDir = Mathf.Lerp(0.32f, 0.90f, CurrentGridFactor); break;
                default: dynDir = IsTotalDarkness ? 0.08f : Mathf.Lerp(0.10f, 0.72f, CurrentGridFactor); break;
            }
            return evDir * dynDir;
        }

        float ComputeAmbientFactor()
        {
            float evAmb = waveEventActive ? 0.42f : 1f;
            float dynAmb;
            switch (CurrentTier)
            {
                case BlackoutTier.Normal: dynAmb = 1f; break;
                case BlackoutTier.FlickerLow: dynAmb = Mathf.Lerp(0.75f, 1f, CurrentGridFactor); break;
                case BlackoutTier.FlickerHeavy: dynAmb = Mathf.Lerp(0.36f, 0.92f, CurrentGridFactor); break;
                default: dynAmb = IsTotalDarkness ? 0.14f : Mathf.Lerp(0.18f, 0.78f, CurrentGridFactor); break;
            }
            return evAmb * dynAmb;
        }

        void ApplyLighting(float gridFactor, float dirFactor, float ambFactor, float[] circuits)
        {
            float combinedGrid = gridFactor * waveEventFactor;
            CombinedLightFactor = combinedGrid;
            GlobalGridFactor = combinedGrid;

            for (int i = trackedLights.Count - 1; i >= 0; i--)
            {
                var tl = trackedLights[i];
                if (tl.light == null) { trackedLights.RemoveAt(i); continue; }
                if (tl.isDirectional)
                {
                    tl.light.intensity = tl.baseIntensity * dirFactor;
                }
                else
                {
                    float cf = circuits != null ? circuits[tl.circuit] : gridFactor;
                    tl.light.intensity = tl.baseIntensity * cf * waveEventFactor;
                }
            }

            if (ambientCaptured)
                RenderSettings.ambientIntensity = baseAmbient * ambFactor;

            for (int i = trackedFixtures.Count - 1; i >= 0; i--)
            {
                var tf = trackedFixtures[i];
                if (tf.renderer == null) { trackedFixtures.RemoveAt(i); continue; }
                float cf = circuits != null ? circuits[tf.circuit] : gridFactor;
                float em = Mathf.Clamp01(cf * waveEventFactor);
                tf.renderer.GetPropertyBlock(tf.mpb);
                tf.mpb.SetColor(EmissionId, tf.baseEmission * em);
                tf.renderer.SetPropertyBlock(tf.mpb);
            }
        }

        void CaptureEnvironment()
        {
            if (cachedDestruction == null)
                cachedDestruction = FindFirstObjectByType<GeneratorDestruction>();

            if (!ambientCaptured)
            {
                baseAmbient = RenderSettings.ambientIntensity;
                ambientCaptured = true;
            }

            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null || knownLights.Contains(l)) continue;
                if (ShouldIgnoreLight(l)) continue;
                knownLights.Add(l);
                int circuit = Mathf.Abs(l.GetInstanceID()) % 4;
                trackedLights.Add(new TrackedLight
                {
                    light = l,
                    baseIntensity = l.intensity,
                    isDirectional = l.type == LightType.Directional,
                    circuit = circuit,
                    phase = (circuit * 2.37f) + (trackedLights.Count * 0.71f),
                });
            }

            // Temukan fixture lampu yang memiliki material emisif (mis. StreetLamp2_Concrete_Night.mat) agar bohlam ikut berkedip/mati
            var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || knownFixtures.Contains(r)) continue;
                if (!IsLampFixture(r)) continue;
                var mat = r.sharedMaterial;
                if (mat == null || !mat.HasProperty(EmissionId)) continue;
                Color em = mat.GetColor(EmissionId);
                if (em.maxColorComponent < 0.25f) continue;
                knownFixtures.Add(r);
                trackedFixtures.Add(new TrackedFixture
                {
                    renderer = r,
                    baseEmission = em,
                    circuit = Mathf.Abs(r.GetInstanceID()) % 4,
                    mpb = new MaterialPropertyBlock(),
                });
            }
        }

        static bool ShouldIgnoreLight(Light l)
        {
            if (l.GetComponentInParent<PlayerHealth>() != null) return true;
            if (l.GetComponentInParent<AllyBrain>() != null) return true;
            if (l.GetComponentInParent<AllyBody>() != null) return true;
            if (l.GetComponentInParent<GeneratorDestruction>() != null) return true;
            if (l.GetComponentInParent<Camera>() != null) return true;
            return false;
        }

        static bool IsLampFixture(Renderer r)
        {
            if (r.GetComponentInParent<PlayerHealth>() != null) return false;
            if (r.GetComponentInParent<AllyBrain>() != null) return false;
            if (r.GetComponentInParent<EnemyHealth>() != null) return false;
            for (var t = r.transform; t != null; t = t.parent)
            {
                string n = t.name;
                if (n.IndexOf("Lamp", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Flood", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Light", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        void RestoreAll()
        {
            for (int i = 0; i < trackedLights.Count; i++)
                if (trackedLights[i].light != null) trackedLights[i].light.intensity = trackedLights[i].baseIntensity;
            if (ambientCaptured) RenderSettings.ambientIntensity = baseAmbient;
            for (int i = 0; i < trackedFixtures.Count; i++)
                if (trackedFixtures[i].renderer != null) trackedFixtures[i].renderer.SetPropertyBlock(null);
            trackedLights.Clear();
            trackedFixtures.Clear();
            knownLights.Clear();
            knownFixtures.Clear();
        }

        // ---------------------------------------------------------------- audio sintetis listrik

        void BuildAudio()
        {
            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 0f;

            buzzClip = SynthesizeBuzz(0.28f);
            powerDownClip = SynthesizeSweep(0.75f, 240f, 46f, 0.26f);
            powerUpClip = SynthesizeSweep(0.55f, 65f, 220f, 0.18f);
        }

        void PlayClip(AudioClip clip, float volume)
        {
            if (audioSrc == null || clip == null || AudioListener.pause) return;
            audioSrc.PlayOneShot(clip, volume);
        }

        static AudioClip SynthesizeBuzz(float duration)
        {
            const int rate = 22050;
            int len = Mathf.Max(1, Mathf.RoundToInt(rate * duration));
            var clip = AudioClip.Create("PowerBuzz", len, 1, rate, false);
            var data = new float[len];
            var rng = new System.Random(4207);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Sin(Mathf.PI * i / len);
                float hum = Mathf.Sin(t * Mathf.PI * 2f * 100f) * 0.45f + Mathf.Sin(t * Mathf.PI * 2f * 200f) * 0.25f;
                float crackle = ((float)rng.NextDouble() * 2f - 1f) * (rng.NextDouble() > 0.78 ? 0.85f : 0.12f);
                data[i] = (hum + crackle) * env * 0.22f;
            }
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip SynthesizeSweep(float duration, float startHz, float endHz, float gain)
        {
            const int rate = 22050;
            int len = Mathf.Max(1, Mathf.RoundToInt(rate * duration));
            var clip = AudioClip.Create("PowerSweep", len, 1, rate, false);
            var data = new float[len];
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                float u = (float)i / len;
                float hz = Mathf.Lerp(startHz, endHz, u * u);
                phase += hz * Mathf.PI * 2f / rate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.65f));
                data[i] = (Mathf.Sin(phase) * 0.7f + Mathf.Sin(phase * 2f) * 0.3f) * env * gain;
            }
            clip.SetData(data, 0);
            return clip;
        }
    }
}
