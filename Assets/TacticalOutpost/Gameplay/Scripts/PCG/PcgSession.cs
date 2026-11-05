using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Outpost.Enemy;
using Outpost.Env;
using Outpost.Pickups;
using Outpost.Player;
using Outpost.UI;

namespace Outpost.PCG
{
    /// <summary>
    /// Pengganti IndustrialGameSession khusus mode PCG (dipasang saat runtime, scene tidak diubah).
    /// Alur: muat environment industrial hanya untuk langit/cahaya bulan/post-processing → semua geometri industrial dimatikan
    /// → arena procedural dibangun dari prefab existing → NavMesh di-bake ulang → objective, spawn point, player, play area,
    /// dan minimap dipindah ke arena → sistem gameplay (WaveDirector, zombie AI, HUD) dinyalakan TANPA modifikasi.
    /// </summary>
    public sealed class PcgSession : MonoBehaviour
    {
        public static PcgSession Instance { get; private set; }

        public GameObject systems;
        public NavMeshSurface navigation;
        public NavMeshSurface[] additionalNavigation;
        public PcgProfile profile;

        public PcgArena Arena { get; private set; }
        public int Seed { get; private set; }
        public bool Ready { get; private set; }
        public readonly PcgStageStats supplyStats = new PcgStageStats("Supply");
        public readonly Dictionary<SupplyKind, int> supplyCounts = new Dictionary<SupplyKind, int>();

        // Enemy selection PCG (weighted + difficulty budget), lihat PcgWavePlanner.
        public readonly List<PcgWavePlan> wavePlans = new List<PcgWavePlan>();
        public readonly Dictionary<string, int> spawnedThisWave = new Dictionary<string, int>();
        public PcgWavePlan ActivePlan { get; private set; }
        public int spawnedBosses;
        public readonly Dictionary<string, int> spawnedPrefabs = new Dictionary<string, int>();   // verifikasi: nama prefab yang benar-benar muncul
        Dictionary<string, EnemyVariant> variantById;
        EnemyVariant feedVariant;
        int fed;
        bool feeding;
        readonly HashSet<int> seenEnemies = new HashSet<int>();

        Scene gameScene;
        readonly List<GameObject> supplies = new List<GameObject>();
        int supplyWave = -1;
        WaveDirector subscribed;

        public static void Install(Scene scene)
        {
            IndustrialGameSession original = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                original = root.GetComponentInChildren<IndustrialGameSession>(true);
                if (original != null) break;
            }
            if (original == null) { Debug.LogError("[PCG] IndustrialGameSession tidak ditemukan; mode PCG dibatalkan."); return; }
            original.enabled = false;                                // Start() sesi industrial tidak pernah jalan
            var s = original.gameObject.AddComponent<PcgSession>();
            s.systems = original.systems;
            s.navigation = original.navigation;
            s.additionalNavigation = original.additionalNavigation;
            s.gameScene = scene;
            original.gameObject.AddComponent<PcgDebugView>();
        }

        void Awake()
        {
            Instance = this;
            if (profile == null) profile = Resources.Load<PcgProfile>(PcgProfile.ResourcePath);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (subscribed != null) { subscribed.OnWaveStarted -= OnWaveStarted; subscribed.OnDefeat -= OnDefeat; }
        }

        IEnumerator Start()
        {
            if (profile == null || profile.fenceSpan == null || profile.generator == null)
            {
                Debug.LogError("[PCG] Resources/PCG_Profile belum lengkap. Jalankan menu Tactical Outpost > PCG > Create or Update Profile.", this);
                yield break;
            }
            if (systems == null) { Debug.LogError("[PCG] Systems root kosong.", this); yield break; }

            // 1. Scene khusus PCG (Assets/Scenes/PCG/TacticalOutpost_PCG.unity): atmosphere, template, dan objek manual
            //    yang bisa diedit sendiri. Bila belum dibuat (menu Tactical Outpost > PCG > Create PCG Environment Scene),
            //    fallback ke cara lama: map industrial dimuat lalu hanya atmosfernya yang dipakai.
            PcgEnvironment env = null;
            Scene environment = default(Scene);
            if (SceneUtility.GetBuildIndexByScenePath(PcgEnvironment.ScenePath) >= 0)
            {
                environment = SceneManager.GetSceneByPath(PcgEnvironment.ScenePath);
                if (!environment.isLoaded)
                {
                    var loading = SceneManager.LoadSceneAsync(PcgEnvironment.ScenePath, LoadSceneMode.Additive);
                    if (loading != null) yield return loading;
                    environment = SceneManager.GetSceneByPath(PcgEnvironment.ScenePath);
                }
                env = PcgEnvironment.FindIn(environment);
            }
            if (env != null)
            {
                var industrial = SceneManager.GetSceneByPath(IndustrialGameSession.EnvironmentScene);
                if (industrial.IsValid() && industrial.isLoaded) yield return SceneManager.UnloadSceneAsync(industrial);
                if (env.profile != null) profile = env.profile;
                generatorTemplate = env.generatorTemplate; towerTemplate = env.towerTemplate; lampTemplate = env.lampTemplate;
                staticRoot = env.staticObjects;
                if (env.templatesRoot != null) env.templatesRoot.gameObject.SetActive(false);   // template tidak tampil di arena
                SceneManager.SetActiveScene(environment);
                DynamicGI.UpdateEnvironment();
                Debug.Log("[PCG] Environment dari scene " + PcgEnvironment.ScenePath, env);
            }
            else
            {
                environment = SceneManager.GetSceneByPath(IndustrialGameSession.EnvironmentScene);
                if (!environment.IsValid() || !environment.isLoaded)
                {
                    var loading = SceneManager.LoadSceneAsync(IndustrialGameSession.EnvironmentScene, LoadSceneMode.Additive);
                    if (loading != null) yield return loading;
                    environment = SceneManager.GetSceneByPath(IndustrialGameSession.EnvironmentScene);
                }
                if (environment.IsValid() && environment.isLoaded)
                {
                    CaptureTemplates(environment);
                    foreach (var root in environment.GetRootGameObjects())
                    {
                        if (templateHolder != null && root == templateHolder) continue;
                        foreach (var surface in root.GetComponentsInChildren<NavMeshSurface>(true)) surface.enabled = false;
                        KeepAtmosphereOnly(root.transform);
                    }
                    SceneManager.SetActiveScene(environment);
                }
            }

            // 2. Hal khusus map industrial di scene gameplay dimatikan (tembok tak terlihat jalur lama).
            foreach (var t in gameScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
                if (t.name == "Approach lane invisible walls") t.gameObject.SetActive(false);

            // 3. Bangun arena.
            Seed = PcgMode.NextSeed(profile);
            Arena = PcgArenaBuilder.Build(profile, Seed, Template());
            ApplyToScene(Arena);
            Debug.Log("[PCG] Seed " + Seed + " | layout attempt " + Arena.layoutAttempts + " | " + Arena.obstacleStats +
                      " ditolak: " + Arena.obstacleStats.RejectionSummary() + " | connected " + Arena.connected, this);

            // 4. Komposisi zombie tiap wave dari tabel weighted + difficulty budget.
            SetupEnemyPlan();

            // 5. Nyalakan gameplay asli.
            Random.InitState(Seed);
            systems.SetActive(true);
            Ready = true;
            yield return null;
            var fps = FindInGame<FpsController>();
            if (fps != null && fps.isActiveAndEnabled) fps.Teleport(Arena.playerSpawn + Vector3.up * 0.1f, Arena.playerYaw);
            subscribed = WaveDirector.Instance;
            if (subscribed != null) { subscribed.OnWaveStarted += OnWaveStarted; subscribed.OnDefeat += OnDefeat; }
        }

        PcgArena Template()
        {
            var surfaces = new List<NavMeshSurface>();
            if (navigation != null) surfaces.Add(navigation);
            if (additionalNavigation != null) surfaces.AddRange(additionalNavigation.Where(s => s != null));

            int block = ~0, probe = ~0;
            foreach (var n in new[] { "TransparentFX", "Ignore Raycast", "Water", "UI", "Player", "Enemy", "Marker" })
            {
                int l = LayerMask.NameToLayer(n);
                if (l >= 0) { block &= ~(1 << l); probe &= ~(1 << l); }
            }
            int ground = LayerMask.NameToLayer("Ground");
            if (ground >= 0) block &= ~(1 << ground);

            var wave = FindInGame<WaveDirector>();
            EnemyCatalog catalog = wave != null ? wave.catalog : null;
            var normal = catalog != null ? catalog.variants.FirstOrDefault(v => v != null && v.prefab != null && v.kind == EnemyKind.Normal) : null;
            var boss = catalog != null ? catalog.variants.FirstOrDefault(v => v != null && v.prefab != null && v.kind == EnemyKind.Boss) : null;
            return new PcgArena
            {
                generatorTemplate = generatorTemplate, towerTemplate = towerTemplate, lampTemplate = lampTemplate, staticRoot = staticRoot,
                surfaces = surfaces.ToArray(), blockMask = block, probeMask = probe,
                normalFilter = FilterFor(normal != null ? normal.prefab : null),
                bossFilter = FilterFor(boss != null ? boss.prefab : null),
            };
        }

        static NavMeshQueryFilter FilterFor(GameObject prefab)
        {
            var agent = prefab != null ? prefab.GetComponent<NavMeshAgent>() : null;
            return agent != null ? new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }
                                 : new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
        }

        /// <summary>Matikan semua geometri environment; sisakan directional light dan Volume (langit malam & post-processing).</summary>
        static bool KeepAtmosphereOnly(Transform t)
        {
            bool keepSelf = false;
            foreach (var l in t.GetComponents<Light>()) if (l.type == LightType.Directional) keepSelf = true;
            if (t.GetComponent<Volume>() != null) keepSelf = true;
            if (keepSelf)
            {
                foreach (var r in t.GetComponents<Renderer>()) r.enabled = false;
                foreach (var c in t.GetComponents<Collider>()) c.enabled = false;
            }
            bool anyChildKept = false;
            for (int i = 0; i < t.childCount; i++) anyChildKept |= KeepAtmosphereOnly(t.GetChild(i));
            if (!keepSelf && !anyChildKept) { t.gameObject.SetActive(false); return false; }
            if (!keepSelf)
            {
                foreach (var r in t.GetComponents<Renderer>()) r.enabled = false;
                foreach (var c in t.GetComponents<Collider>()) c.enabled = false;
                foreach (var l in t.GetComponents<Light>()) l.enabled = false;
                foreach (var b in t.GetComponents<MonoBehaviour>()) b.enabled = false;
            }
            return true;
        }

        GameObject templateHolder, generatorTemplate, towerTemplate, lampTemplate;
        Transform staticRoot;

        /// <summary>
        /// Salin objek pencahayaan yang sudah di-setup di map industrial supaya suasana arena sama:
        /// generator besar + "Generator red security floodlights" (lampu merah + dust beam), menara + "Corner floodlights",
        /// dan lampu jalan StreetLamp2 dengan lampu hangat. Salinan disimpan nonaktif sebagai template.
        /// </summary>
        void CaptureTemplates(Scene environment)
        {
            var all = environment.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
            Transform redLights = all.FirstOrDefault(t => t.name == "Generator red security floodlights");
            Transform towerLights = all.FirstOrDefault(t => t.name == "Corner floodlights - four single heads");
            Transform lamp = all.FirstOrDefault(t => t.name.StartsWith("Roadside lamp") && t.GetComponentInChildren<Light>(true) != null)
                          ?? all.FirstOrDefault(t => t.name.Contains("StreetLamp2") && t.GetComponentInChildren<Light>(true) != null);
            templateHolder = new GameObject("PCG templates (salinan objek map industrial)");
            templateHolder.SetActive(false);
            GameObject Copy(Transform source)
            {
                if (source == null) return null;
                var copy = Instantiate(source.gameObject, templateHolder.transform, true);
                copy.name = source.name;
                copy.SetActive(true);
                return copy;
            }
            generatorTemplate = Copy(redLights != null ? redLights.parent : null);
            towerTemplate = Copy(towerLights != null ? towerLights.parent : null);
            lampTemplate = Copy(lamp);
            // Di map industrial floodlight merah menyinari area 45 m. Arena PCG jauh lebih kecil, jadi jangkauannya
            // dipersempit supaya merahnya hanya mengelilingi generator dan tidak membanjiri seluruh arena.
            if (generatorTemplate != null)
                foreach (var l in generatorTemplate.GetComponentsInChildren<Light>(true))
                {
                    l.range = Mathf.Min(l.range, 16f);
                    l.intensity *= 0.3f;
                }
            Debug.Log("[PCG] Template dari map industrial: generator " + (generatorTemplate != null ? generatorTemplate.name : "-") +
                      ", tower " + (towerTemplate != null ? towerTemplate.name : "-") + ", lamp " + (lampTemplate != null ? lampTemplate.name : "-"), this);
        }

        T FindInGame<T>() where T : Component
        {
            foreach (var root in gameScene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        List<T> FindAllInGame<T>() where T : Component
        {
            var list = new List<T>();
            foreach (var root in gameScene.GetRootGameObjects()) list.AddRange(root.GetComponentsInChildren<T>(true));
            return list;
        }

        /// <summary>Pindahkan objek gameplay yang sudah ada ke posisi hasil generate (tidak ada objek baru).</summary>
        void ApplyToScene(PcgArena a, bool movePlayer = true)
        {
            // Generator objective (HP, health bar, target zombie) → generator arena.
            var gen = FindInGame<GeneratorHealth>();
            if (gen != null)
            {
                gen.transform.SetPositionAndRotation(new Vector3(a.generatorPos.x, a.groundY, a.generatorPos.z), Quaternion.identity);
                var box = gen.GetComponent<BoxCollider>();
                if (box != null)
                {
                    Vector3 s = gen.transform.lossyScale;
                    box.center = gen.transform.InverseTransformPoint(a.generatorBounds.center);
                    box.size = new Vector3((a.generatorBounds.size.x + 0.6f) / s.x, a.generatorBounds.size.y / s.y, (a.generatorBounds.size.z + 0.6f) / s.z);
                }
                var slot = gen.GetComponent<EnvGeneratorSlot>();
                if (slot != null) slot.attackPoints = BuildAttackPoints(a, slot);
            }

            // Titik spawn zombie (EnvSpawnPoint yang sama) → zona di dalam gerbang procedural.
            foreach (var group in FindAllInGame<EnvSpawnPoint>().Where(s => s.group == "Enemy").GroupBy(s => s.lane))
            {
                List<Vector3> list;
                var gate = a.gates.Find(g => g.lane == group.Key);
                if (gate == null || !a.spawnPositions.TryGetValue(group.Key, out list) || list.Count == 0) continue;
                int i = 0;
                foreach (var point in group)
                {
                    point.transform.SetPositionAndRotation(list[i % list.Count], Quaternion.LookRotation(gate.inward, Vector3.up));
                    i++;
                }
            }

            var player = FindInGame<PlayerHealth>();
            var controller = player != null ? player.GetComponent<FpsController>() : null;
            if (movePlayer && controller != null && controller.isActiveAndEnabled) controller.Teleport(a.playerSpawn + Vector3.up * 0.1f, a.playerYaw);
            else if (movePlayer && player != null) player.transform.SetPositionAndRotation(a.playerSpawn + Vector3.up * 0.1f, Quaternion.Euler(0f, a.playerYaw, 0f));

            var area = FindInGame<PlayArea>();
            if (area != null) { area.min = new Vector2(-a.half, -a.half); area.max = new Vector2(a.half, a.half); }

            var minimap = FindInGame<MinimapController>();
            if (minimap != null) { minimap.mapCenter = a.Center; minimap.largeCamSize = a.half * 1.12f; }

            // Cinematic ledakan sama seperti Mini Project, diarahkan ke generator arena
            // (bangunan, pipa, dan tangki industrial tidak ada di arena, jadi dilewati).
            var destruction = FindInGame<GeneratorDestruction>();
            if (destruction != null) ConfigureDestruction(destruction, a);
        }

        // ---------------------------------------------------------------- supply cache per wave

        void OnWaveStarted(int wave)
        {
            supplyWave = wave - 1; PlaceSupply(0);
            if (feedVariant == null || wave - 1 >= wavePlans.Count) return;
            ActivePlan = wavePlans[wave - 1];
            fed = 0; spawnedBosses = 0; spawnedThisWave.Clear(); spawnedPrefabs.Clear();
            foreach (var e in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)) seenEnemies.Add(e.GetInstanceID());
            feeding = ActivePlan.queue.Count > 0;
            Feed(0);
        }

        // ---------------------------------------------------------------- enemy selection (weighted + budget)

        /// <summary>
        /// WaveDirector tidak diubah: field publik catalog dan waves milik instance runtime diganti.
        /// waves[i].perSide = jumlah zombie hasil rencana / 2; catalog = katalog runtime berisi boss asli + satu slot zombie
        /// biasa yang isinya diganti sesuai antrean rencana tiap kali zombie muncul. Hasil: urutan jenis zombie persis rencana PCG.
        /// </summary>
        void SetupEnemyPlan()
        {
            wavePlans.Clear();
            var wd = FindInGame<WaveDirector>();
            if (wd == null || wd.catalog == null || wd.waves == null || profile.enemyTable == null || profile.enemyTable.Length == 0) return;
            variantById = wd.catalog.variants.Where(v => v != null && v.prefab != null && v.kind == EnemyKind.Normal)
                                             .GroupBy(v => v.id).ToDictionary(g => g.Key, g => g.First());
            var plans = new List<PcgWavePlan>();
            for (int i = 0; i < wd.waves.Length; i++) plans.Add(PcgWavePlanner.Plan(profile, Seed, i + 1, variantById.Keys));
            if (variantById.Count == 0 || plans.Any(pl => pl.queue.Count == 0))
            {
                Debug.LogWarning("[PCG] Tabel enemy tidak cocok dengan EnemyCatalog game; komposisi wave memakai WaveDirector asli.", this);
                return;
            }
            var catalog = ScriptableObject.CreateInstance<EnemyCatalog>();
            catalog.name = "EnemyCatalog (PCG runtime)";
            foreach (var v in wd.catalog.variants) if (v != null && v.kind == EnemyKind.Boss) catalog.variants.Add(v);
            feedVariant = new EnemyVariant { id = plans[0].queue[0], prefab = variantById[plans[0].queue[0]].prefab, kind = EnemyKind.Normal, minWave = 1, maxWave = 999, weight = 1f };
            catalog.variants.Add(feedVariant);
            var waves = new WaveDirector.Wave[wd.waves.Length];
            for (int i = 0; i < waves.Length; i++)
            {
                waves[i] = JsonUtility.FromJson<WaveDirector.Wave>(JsonUtility.ToJson(wd.waves[i]));
                waves[i].perSide = plans[i].queue.Count / 2;
            }
            wd.waves = waves;
            wd.catalog = catalog;
            wavePlans.AddRange(plans);
            foreach (var pl in plans) Debug.Log("[PCG] Enemy plan wave " + pl.wave + ": " + pl.Summary(), this);
        }

        void Feed(int index)
        {
            if (ActivePlan == null || feedVariant == null || ActivePlan.queue.Count == 0) return;
            string id = ActivePlan.queue[Mathf.Min(index, ActivePlan.queue.Count - 1)];
            EnemyVariant v;
            if (variantById.TryGetValue(id, out v)) { feedVariant.id = id; feedVariant.prefab = v.prefab; }
        }

        void Update()
        {
            UnstickEnemies();
            if (!feeding || ActivePlan == null) return;
            foreach (var e in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
            {
                if (!seenEnemies.Add(e.GetInstanceID())) continue;
                if (e.config != null && e.config.kind == EnemyKind.Boss) { spawnedBosses++; continue; }
                string prefabName = e.gameObject.name.Replace("(Clone)", "").Trim();
                spawnedPrefabs.TryGetValue(prefabName, out int pn); spawnedPrefabs[prefabName] = pn + 1;
                string id = fed < ActivePlan.queue.Count ? ActivePlan.queue[fed] : "extra";
                spawnedThisWave.TryGetValue(id, out int n); spawnedThisWave[id] = n + 1;
                fed++;
                Feed(fed);
            }
            var wd = WaveDirector.Instance;
            if (fed >= ActivePlan.queue.Count && wd != null && wd.CurrentPhase != WaveDirector.Phase.Spawning)
            {
                feeding = false;
                Debug.Log("[PCG] Wave " + ActivePlan.wave + " spawned: rencana " + ActivePlan.Summary() + " | prefab yang muncul: " +
                          string.Join(", ", spawnedPrefabs.Select(k => k.Key + " " + k.Value)) + (spawnedBosses > 0 ? " | boss " + spawnedBosses : ""), this);
            }
        }

        // ---------------------------------------------------------------- titik serang generator + zombie macet

        /// <summary>
        /// Generator asli hanya punya 6 titik serang (1 titik = 1 zombie). Zombie ke-7 dst. diarahkan ke pusat generator;
        /// di arena PCG pusat itu jauh dari NavMesh (generator lebih besar) sehingga zombie diam di spawn sampai melihat/mendengar player.
        /// Solusi: titik serang dibuat mengelilingi generator (2 cincin dalam jangkauan serang), cukup untuk satu wave penuh.
        /// </summary>
        Transform[] BuildAttackPoints(PcgArena a, EnvGeneratorSlot slot)
        {
            var points = new List<Transform>();
            var holder = new GameObject("Generator attack points (PCG)").transform; holder.SetParent(a.root, false);
            foreach (float clearance in new[] { 1.0f, 2.0f })
                foreach (var t in a.GeneratorRing(24, clearance))
                {
                    Vector3 onNav;
                    if (!a.SampleNav(t, 1.2f, a.normalFilter, out onNav)) continue;
                    if (points.Any(q => (q.position - onNav).sqrMagnitude < 0.36f)) continue;
                    var point = new GameObject("Attack point " + points.Count).transform;
                    point.SetParent(holder, false); point.position = onNav;
                    points.Add(point);
                }
            Debug.Log("[PCG] Titik serang generator: " + points.Count + " (generator asli: " + (slot.attackPoints != null ? slot.attackPoints.Length : 0) + ").", this);
            return points.Count > 0 ? points.ToArray() : slot.attackPoints;
        }

        float nextUnstick;
        int unstuck, unstuckLogged;

        /// <summary>Jaring pengaman: zombie bertarget generator yang diam jauh dari generator diarahkan ke sisi generator terdekat.</summary>
        void UnstickEnemies()
        {
            if (!Ready || Time.time < nextUnstick) return;
            if (unstuck != unstuckLogged && unstuck % 5 == 0) { unstuckLogged = unstuck; Debug.Log("[PCG] Jaring pengaman: " + unstuck + " kali zombie diam diarahkan ulang ke generator.", this); }
            nextUnstick = Time.time + 1f;
            GeneratorHealth gen = null;
            foreach (var g in GeneratorHealth.All) if (g != null && g.IsAlive) { gen = g; break; }
            if (gen == null) return;
            foreach (var brain in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
            {
                if (!brain.enabled || brain.DebugTarget == "Player") continue;
                var agent = brain.GetComponent<NavMeshAgent>();
                if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.isStopped || agent.pathPending) continue;
                if (agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.5f) continue;          // masih berjalan
                Vector3 near = gen.Body != null ? gen.Body.ClosestPoint(brain.transform.position) : gen.transform.position;
                if (Vector3.Distance(near, brain.transform.position) < 4f) continue;                             // sudah di generator
                NavMeshHit hit;
                if (NavMesh.SamplePosition(near, out hit, 4f, NavMesh.AllAreas)) { agent.SetDestination(hit.position); unstuck++; }
            }
        }

        // ---------------------------------------------------------------- kekalahan: cinematic ledakan seperti Mini Project

        string generatorMeshName;

        void ConfigureDestruction(GeneratorDestruction d, PcgArena a)
        {
            d.generatorPath = null; d.pipePaths = null; d.buildingPath = null; d.buildingDebris = null;
            d.tanks = new GeneratorDestruction.Breakable[0];
            d.containmentWalls = new GeneratorDestruction.Breakable[0];
            Bounds b = a.generatorBounds;
            d.buildingCenter = b.center;                                  // kamera drone mengorbit generator arena
            float y = a.groundY + 0.5f;
            d.blastSites = new[] { new Vector3(b.min.x, y, b.min.z), new Vector3(b.max.x, y, b.min.z), new Vector3(b.min.x, y, b.max.z), new Vector3(b.max.x, y, b.max.z) };
            generatorMeshName = a.generatorTemplate != null ? a.generatorTemplate.name : null;
        }

        // Sekuens ledakan PCG: lebih heboh dari ledakan dasar + api merata. Hanya aset yang sama dengan Mini Project
        // (prefab explosion, fire, suara blast); kamera drone milik GeneratorDestruction diarahkan lebih dekat.
        float defeatStart = -1f, fxShake;
        GeneratorDestruction activeDestruction;
        System.Random fxRng = new System.Random();

        void OnDefeat()
        {
            defeatStart = Time.unscaledTime;
            activeDestruction = FindInGame<GeneratorDestruction>();
            fxRng = new System.Random(Seed);
            var ring = Arena != null && Arena.root != null ? Arena.root.Find("Safe Zone (garis hijau)") : null;
            if (ring != null) ring.gameObject.SetActive(false);                 // penanda gameplay disembunyikan saat cinematic
            StartCoroutine(BreakArenaGenerator());
            StartCoroutine(DefeatSpectacle());
        }

        float FxRange(float a, float b) { return a + (float)fxRng.NextDouble() * (b - a); }

        IEnumerator DefeatSpectacle()
        {
            var d = activeDestruction;
            if (d == null || Arena == null || d.explosion == null) yield break;
            Bounds b = Arena.generatorBounds; Vector3 c = b.center; Vector3 e = b.extents; float ground = Arena.groundY;

            // 1) Detik-detik sebelum meledak: lampu merah generator berkedip + letupan kecil di badan generator.
            var lights = new List<Light>(); var baseIntensity = new List<float>();
            foreach (var t in Arena.root.GetComponentsInChildren<Transform>())
                if (t.name == generatorMeshName) { foreach (var l in t.GetComponentsInChildren<Light>()) { lights.Add(l); baseIntensity.Add(l.intensity); } break; }
            float start = Time.unscaledTime; int pop = 0; float[] pops = { 0.3f, 0.62f, 0.9f, 1.05f };
            while (Time.unscaledTime - start < 1.2f)
            {
                float t = Time.unscaledTime - start;
                for (int i = 0; i < lights.Count; i++) if (lights[i] != null) lights[i].intensity = baseIntensity[i] * (Random.value < 0.35f ? 0.05f : Random.Range(0.6f, 1.8f));
                if (pop < pops.Length && t >= pops[pop])
                {
                    pop++;
                    Vector3 p = new Vector3(c.x + FxRange(-e.x, e.x), b.min.y + FxRange(0.6f, b.size.y), c.z + FxRange(-e.z, e.z));
                    Boom(d, p, FxRange(0.7f, 1.1f), pop % 2 == 0 ? d.tankBlastSound : null, 0.22f);
                    fxShake = Mathf.Max(fxShake, 0.35f);
                }
                yield return null;
            }
            foreach (var l in lights) if (l != null) l.enabled = false;           // blackout

            // 2) Ledakan utama diperbesar + gelombang ledakan melempar props di sekitar generator.
            Boom(d, c + Vector3.up * (e.y + 2f), 4.8f, d.buildingBlastSound, 0.5f);
            Boom(d, new Vector3(c.x + e.x * 0.6f, b.min.y + 2f, c.z - e.z * 0.4f), 3.2f, null, 0f);
            Boom(d, new Vector3(c.x - e.x * 0.5f, b.min.y + 2.5f, c.z + e.z * 0.5f), 3.2f, null, 0f);
            fxShake = 1.5f;
            ShockwaveProps(c, Mathf.Max(e.x, e.z) + 12f);

            // 3) Ledakan berantai melingkari generator dengan jeda acak.
            const int chain = 10;
            for (int i = 0; i < chain; i++)
            {
                yield return new WaitForSecondsRealtime(FxRange(0.14f, 0.36f));
                float ang = (i / (float)chain) * Mathf.PI * 2f + FxRange(-0.3f, 0.3f);
                Vector3 p = new Vector3(c.x + Mathf.Cos(ang) * (e.x + FxRange(0.5f, 4.5f)), ground + FxRange(0.6f, 3f), c.z + Mathf.Sin(ang) * (e.z + FxRange(0.5f, 4.5f)));
                Boom(d, p, FxRange(1.5f, 2.7f), i % 3 == 0 ? d.tankBlastSound : (i % 3 == 1 ? d.generatorBlastSound : null), 0.2f);
                fxShake = Mathf.Max(fxShake, 0.7f);
            }

            // 4) Api menyebar merata dari pusat ke luar (pola sunflower di elips sekitar generator).
            if (d.fire == null) yield break;
            const int fires = 38;
            float ax = e.x + 8f, az = e.z + 8f;
            for (int i = 0; i < fires; i++)
            {
                float k = (i + 0.5f) / fires, r = Mathf.Sqrt(k), ang = i * 2.39996f;
                Vector3 p = new Vector3(c.x + Mathf.Cos(ang) * r * ax + FxRange(-0.6f, 0.6f), ground + 12f, c.z + Mathf.Sin(ang) * r * az + FxRange(-0.6f, 0.6f));
                RaycastHit hit;
                p.y = Physics.Raycast(p, Vector3.down, out hit, 30f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : ground;
                Flame(d, p, Mathf.Lerp(2.4f, 1.4f, r) * FxRange(0.85f, 1.15f));
                yield return new WaitForSecondsRealtime(FxRange(0.03f, 0.09f));
            }
        }

        void Boom(GeneratorDestruction d, Vector3 position, float scale, AudioClip clip, float volume)
        {
            var fx = Instantiate(d.explosion, position, Quaternion.Euler(0f, FxRange(0f, 360f), 0f), d.transform);
            fx.transform.localScale *= scale;
            StartFx(fx); Destroy(fx, 12f);
            var flash = new GameObject("PCG blast flash", typeof(Light)).GetComponent<Light>();
            flash.transform.SetParent(d.transform, false); flash.transform.position = position + Vector3.up * 2f;
            flash.type = LightType.Point; flash.color = new Color(1f, 0.5f, 0.12f);
            flash.range = 10f + scale * 7f; flash.intensity = 18f + scale * 8f;
            StartCoroutine(FadeLight(flash, 0.5f + scale * 0.12f));
            if (clip != null && volume > 0f)
            {
                var src = new GameObject(clip.name, typeof(AudioSource)).GetComponent<AudioSource>();
                src.transform.SetParent(d.transform, false); src.transform.position = position;
                src.clip = clip; src.volume = volume; src.pitch = FxRange(0.85f, 1.1f); src.spatialBlend = 0.6f;
                src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 25f; src.maxDistance = 200f; src.dopplerLevel = 0f;
                src.Play(); Destroy(src.gameObject, clip.length + 0.3f);
            }
        }

        void Flame(GeneratorDestruction d, Vector3 position, float scale)
        {
            var flame = Instantiate(d.fire, position, Quaternion.Euler(0f, FxRange(0f, 360f), 0f), d.transform);
            flame.transform.localScale *= scale;
            foreach (var t in flame.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("Distortion")) t.gameObject.SetActive(false);
            StartFx(flame);
        }

        static void StartFx(GameObject fx)
        {
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>()) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate; main.useUnscaledTime = true;
                ps.Play(false);
            }
        }

        IEnumerator FadeLight(Light light, float duration)
        {
            float t = 0f, from = light.intensity;
            while (t < duration && light != null) { t += Time.unscaledDeltaTime; light.intensity = Mathf.Lerp(from, 0f, t / duration); yield return null; }
            if (light != null) Destroy(light.gameObject);
        }

        /// <summary>Props/obstacle hasil generator di dekat generator ikut terlempar (hanya yang collider-nya primitif).</summary>
        void ShockwaveProps(Vector3 center, float radius)
        {
            var roots = new HashSet<Transform>();
            foreach (var col in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform t = col.transform;
                while (t.parent != null && t.parent != Arena.dressingRoot && t.parent != Arena.layoutRoot) t = t.parent;
                if (t.parent == null) continue;
                roots.Add(t);
            }
            int thrown = 0;
            foreach (var prop in roots)
            {
                if (thrown >= 30) break;
                bool primitive = true;
                foreach (var col in prop.GetComponentsInChildren<Collider>()) if (col is MeshCollider mc && !mc.convex) { primitive = false; break; }
                if (!primitive) continue;
                foreach (var obs in prop.GetComponentsInChildren<NavMeshObstacle>()) obs.enabled = false;
                var body = prop.GetComponent<Rigidbody>(); if (body == null) body = prop.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = false; body.mass = 15f; body.linearDamping = 0.2f;
                body.AddExplosionForce(FxRange(11f, 17f), center - Vector3.up * 2f, radius * 1.3f, 2.2f, ForceMode.VelocityChange);
                body.AddTorque(Random.insideUnitSphere * 8f, ForceMode.VelocityChange);
                thrown++;
            }
            Debug.Log("[PCG] Ledakan generator: " + thrown + " props terlempar.", this);
        }

        void LateUpdate()
        {
            // Kamera drone (milik GeneratorDestruction) dibuat lebih dekat ke generator arena + guncangan sesuai ledakan.
            var d = activeDestruction;
            if (d == null || !d.Playing || d.View == null || Arena == null || defeatStart < 0f) return;
            float t = Time.unscaledTime - defeatStart;
            Vector3 target = Arena.generatorBounds.center + Vector3.up * 2f;
            float pull = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 1.2f) / 9f));
            float dist = Mathf.Lerp(24f, 42f, pull), height = Mathf.Lerp(9f, 22f, pull);
            float ang = (-135f + t * 8f) * Mathf.Deg2Rad;
            Vector3 pos = target + new Vector3(Mathf.Sin(ang) * dist, height, Mathf.Cos(ang) * dist);
            fxShake = Mathf.MoveTowards(fxShake, 0f, Time.unscaledDeltaTime * 1.5f);
            Vector3 jitter = new Vector3(Mathf.Sin(t * 53f), Mathf.Cos(t * 67f), Mathf.Sin(t * 47f)) * fxShake * 0.6f;
            d.View.transform.SetPositionAndRotation(pos + jitter, Quaternion.LookRotation(target - pos) * Quaternion.Euler(jitter * 2f));
        }

        /// <summary>Saat ledakan utama (1,2 detik), generator arena dipecah memakai debris yang sama dengan Mini Project.</summary>
        IEnumerator BreakArenaGenerator()
        {
            yield return new WaitForSecondsRealtime(1.2f);
            var d = FindInGame<GeneratorDestruction>();
            if (d == null || d.debris == null || Arena == null || Arena.root == null) yield break;
            Transform source = null;
            foreach (var t in Arena.root.GetComponentsInChildren<Transform>())
                if (t.name == generatorMeshName && t.GetComponent<MeshFilter>() != null) { source = t; break; }
            if (source == null) yield break;
            var breakMethod = typeof(GeneratorDestruction).GetMethod("Break", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (breakMethod != null) breakMethod.Invoke(d, new object[] { source, d.debris, Arena.generatorBounds.center, 340f, 90f, false });
        }

        public void RerollSupply(int roll) { if (supplyWave >= 0) PlaceSupply(roll); }

        void PlaceSupply(int roll)
        {
            foreach (var s in supplies) PcgArena.Discard(s);
            supplies.Clear(); supplyCounts.Clear();
            supplyStats.requested = supplyStats.accepted = supplyStats.attempts = 0;
            System.Array.Clear(supplyStats.rejected, 0, supplyStats.rejected.Length);
            Arena.markers.RemoveAll(m => m.stage == 'C');
            var drop = FindInGame<DropManager>();
            if (drop == null || profile.cachesPerWave <= 0) return;
            var rng = PcgRandom.For(Seed, "supply", supplyWave * 100 + roll);
            var weights = profile.supplyTable.Select(e => e != null ? e.weight : 0f).ToList();
            var a = Arena;
            for (int k = 0; k < profile.cachesPerWave; k++)
            {
                supplyStats.requested++;
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    supplyStats.attempts++;
                    float ang = rng.Range(0f, Mathf.PI * 2f);
                    float rad = Mathf.Sqrt(Mathf.Lerp(profile.supplyMinRadius * profile.supplyMinRadius, profile.supplyMaxRadius * profile.supplyMaxRadius, rng.Value));
                    var entry = profile.supplyTable[Mathf.Max(0, rng.WeightedIndex(weights))];
                    float yaw = rng.Range(0f, 360f);
                    Vector3 c = a.generatorPos + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * rad;
                    Vector3 onNav; float len;
                    PcgRule rule = PcgRule.Accepted;
                    if (!a.InArena(c, 2f)) rule = PcgRule.OutsideArea;
                    else if (!a.SampleNav(c, 0.6f, a.normalFilter, out onNav)) rule = PcgRule.OffNavMesh;
                    else if (a.Blocked(onNav, 0.4f)) rule = PcgRule.Blocked;
                    else if (a.gates.Any(g => PcgArena.Flat(onNav - g.Inner(3f)).magnitude < profile.supplyMinFromGate)) rule = PcgRule.NearGate;
                    else if (supplies.Any(s => PcgArena.Flat(s.transform.position - onNav).magnitude < profile.supplySpacing)) rule = PcgRule.Spacing;
                    else if (!a.PathToGenerator(onNav, a.normalFilter, out len)) rule = PcgRule.NoPath;
                    a.Mark(c, rule, 'C');
                    if (rule != PcgRule.Accepted) { supplyStats.Reject(rule); continue; }
                    var prefab = entry.kind == SupplyKind.Ammo ? drop.ammoPrefab : drop.healthPrefab;
                    if (prefab == null) break;
                    var go = Instantiate(prefab, new Vector3(c.x, a.groundY, c.z), Quaternion.Euler(0f, yaw, 0f), a.root);
                    go.name = "PCG Supply " + entry.kind + " " + k;
                    var item = go.GetComponent<PickupItem>();
                    if (item != null) item.lifetime = 100000f;
                    supplies.Add(go);
                    supplyCounts.TryGetValue(entry.kind, out int n); supplyCounts[entry.kind] = n + 1;
                    supplyStats.accepted++;
                    break;
                }
            }
            int ammo, health; supplyCounts.TryGetValue(SupplyKind.Ammo, out ammo); supplyCounts.TryGetValue(SupplyKind.Health, out health);
            if (supplies.Count > 0) HudEvents.ShowToast("PCG SUPPLY CACHE  " + ammo + " AMMO  /  " + health + " MEDKIT", new Color(1f, .82f, .35f));
        }

        // ---------------------------------------------------------------- eksperimen (F9)

        /// <summary>Bangun ulang arena untuk beberapa konfigurasi, catat hasilnya, lalu kembalikan arena seed aktif.</summary>
        public string RunExperiments()
        {
            float prevScale = Time.timeScale; Time.timeScale = 0f;
            var rows = new List<string>();
            var template = Template();
            System.Func<int, System.Action<PcgProfile>, PcgArena> run = (seed, tweak) =>
            {
                var p = Instantiate(profile); tweak(p);
                if (Arena != null && Arena.root != null) PcgArena.Discard(Arena.root.gameObject);
                var arena = PcgArenaBuilder.Build(p, seed, template);
                PcgArena.Discard(arena.root.gameObject);
                Destroy(p);
                Arena = null;
                return arena;
            };
            string Row(string key, PcgArena x) =>
                "| " + key + " | " + x.obstacles.Count + "/" + x.profile.obstacleCount + " | " + x.obstacleStats.attempts + " | " + x.obstacleStats.TotalRejected +
                " | " + x.layoutAttempts + " | " + x.repairRemovals + " | " + (x.connected ? "yes" : "NO") + " | " + x.LayoutHash() +
                " | " + string.Join(" ", x.gates.Select(g => g.lane.ToString()[0] + "" + g.startSpan)) + " | " + x.obstacleStats.RejectionSummary() + " |";
            const string head = "| {0} | Obstacles | Attempts | Rejected | Layout tries | Repair | Connected | Layout hash | Gates (span) | Rejection detail |\n|---:|---:|---:|---:|---:|---:|---|---:|---|---|";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# PCG Experiment Results — Tactical Outpost (mode PCG)");
            sb.AppendLine();
            sb.AppendLine("Dibuat otomatis (F9) " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ". Setiap baris membangun arena lengkap (pagar, gerbang, obstacle, NavMesh, validasi jalur) dari prefab existing.");
            sb.AppendLine();
            sb.AppendLine("## Eksperimen 1 — Seed berbeda"); sb.AppendLine(); sb.AppendLine(string.Format(head, "Seed"));
            foreach (int s in new[] { 100, 200, 300 }) sb.AppendLine(Row(s.ToString(), run(s, p => { })));
            sb.AppendLine();
            sb.AppendLine("## Eksperimen 2 — Reproducibility (seed 500 × 3)"); sb.AppendLine(); sb.AppendLine(string.Format(head, "Run"));
            var hashes = new List<int>();
            for (int i = 1; i <= 3; i++) { var x = run(500, p => { }); hashes.Add(x.LayoutHash()); sb.AppendLine(Row(i.ToString(), x)); }
            sb.AppendLine(); sb.AppendLine(hashes.Distinct().Count() == 1 ? "**Identik**: ketiga run menghasilkan layout hash yang sama." : "**Berbeda**: ada sumber random di luar PcgRandom.");
            sb.AppendLine();
            sb.AppendLine("## Eksperimen 3 — Player safe radius (`playerClearRadius`, seed 500)"); sb.AppendLine(); sb.AppendLine(string.Format(head, "Radius (m)"));
            foreach (float r in new[] { 3f, 5f, 8f }) { float rr = r; sb.AppendLine(Row(r.ToString("0"), run(500, p => p.playerClearRadius = rr))); }
            sb.AppendLine();
            sb.AppendLine("## Eksperimen 4 — Density (`obstacleGap`, seed 500)"); sb.AppendLine(); sb.AppendLine(string.Format(head, "Gap (m)"));
            foreach (float g in new[] { 1.0f, 2.5f, 4.0f }) { float gg = g; sb.AppendLine(Row(g.ToString("0.0"), run(500, p => p.obstacleGap = gg))); }
            sb.AppendLine();
            sb.AppendLine("## Eksperimen 5 — Weighted random (10.000 pemilihan)"); sb.AppendLine();
            sb.AppendLine("| Weight A : B : C | A | B | C |"); sb.AppendLine("|---|---:|---:|---:|");
            foreach (var w in new[] { new[] { 60f, 30f, 10f }, new[] { 33f, 33f, 34f } })
            {
                var rng = new PcgRandom(500); var hits = new int[3];
                for (int i = 0; i < 10000; i++) hits[rng.WeightedIndex(w)]++;
                sb.AppendLine("| " + string.Join(" : ", w.Select(v => v.ToString("0"))) + " | " + string.Join(" | ", hits.Select(h => (h / 100f).ToString("0.0") + "%")) + " |");
            }

            sb.AppendLine();
            sb.AppendLine("## Eksperimen 6 — Enemy selection (weighted + difficulty budget)"); sb.AppendLine();
            sb.AppendLine("Tabel: " + string.Join(", ", profile.enemyTable.Select(e => e.id + " w" + e.weight.ToString("0") + " cost " + e.cost + (e.minWave > 1 ? " (wave " + e.minWave + "+)" : ""))) +
                          ". Budget per wave: " + string.Join(" / ", profile.waveBudget) + ".");
            sb.AppendLine();
            sb.AppendLine("| Seed | Wave | Komposisi | Zombie | Cost / budget |"); sb.AppendLine("|---:|---:|---|---:|---|");
            var ids = variantById != null && variantById.Count > 0 ? (ICollection<string>)variantById.Keys : null;
            foreach (int sd in new[] { 100, 200, 300, 500, 500 })
                for (int w = 1; w <= profile.waveBudget.Length; w++)
                {
                    var pl = PcgWavePlanner.Plan(profile, sd, w, ids);
                    sb.AppendLine("| " + sd + " | " + w + " | " + string.Join(", ", pl.counts.OrderByDescending(k => k.Value).Select(k => k.Key + " " + k.Value)) +
                                  " | " + pl.queue.Count + " | " + pl.spent + " / " + pl.budget + " |");
                }
            sb.AppendLine();
            sb.AppendLine("Seed 500 muncul dua kali untuk uji reproducibility komposisi wave. Distribusi 10.000 pemilihan weighted (tanpa budget, wave 3):");
            sb.AppendLine();
            {
                var table = profile.enemyTable.Where(e => e != null).ToList();
                var rng = new PcgRandom(500); var hits = new int[table.Count];
                for (int i = 0; i < 10000; i++) hits[rng.WeightedIndex(table.Select(e => e.weight).ToList())]++;
                float total = table.Sum(e => e.weight);
                sb.AppendLine("| Enemy | Weight | Expected | Hasil |"); sb.AppendLine("|---|---:|---:|---:|");
                for (int i = 0; i < table.Count; i++)
                    sb.AppendLine("| " + table[i].id + " | " + table[i].weight.ToString("0") + " | " + (table[i].weight / total * 100f).ToString("0.0") + "% | " + (hits[i] / 100f).ToString("0.0") + "% |");
            }

            // Kembalikan arena match ini (seed sama → layout sama).
            Arena = PcgArenaBuilder.Build(profile, Seed, template);
            ApplyToScene(Arena, false);
            if (supplyWave >= 0) PlaceSupply(0);
            Time.timeScale = prevScale;

            string dir = Application.isEditor ? System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Docs", "PCG")) : Application.persistentDataPath;
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, "ExperimentResults.md");
            System.IO.File.WriteAllText(path, sb.ToString());
            Debug.Log("[PCG] Eksperimen selesai: " + path, this);
            return path;
        }
    }
}
