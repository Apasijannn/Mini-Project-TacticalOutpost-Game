using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Outpost.Enemy;
using Outpost.Env;
using Outpost.PCG;
using Outpost.Pickups;
using Outpost.Player;
using Outpost.UI;

namespace Outpost.Dungeon
{
    /// <summary>
    /// Pengganti IndustrialGameSession khusus mode Procedural Dungeon (dipasang saat runtime, scene game tidak diubah).
    /// Alur: atmosfer malam dari scene PCG → WaveDirector & generator objective Mini Project dinonaktifkan untuk sesi ini
    /// → DungeonGenerator (grid, BSP, validasi BFS) → DungeonBuilder (prefab existing + NavMesh) → player di Start,
    /// zombie/boss/item di posisi hasil generation → player harus mencapai generator di Goal.
    /// </summary>
    public sealed class DungeonSession : MonoBehaviour
    {
        public static DungeonSession Instance { get; private set; }

        public GameObject systems;
        public NavMeshSurface navigation;
        public NavMeshSurface[] additionalNavigation;

        public DungeonProfile Profile { get; private set; }
        public DungeonLayout Layout { get; private set; }
        public DungeonWorld World { get; private set; }
        public DungeonAlgorithm Algorithm { get; private set; }
        public int Seed { get; private set; }
        public bool Ready { get; private set; }
        public bool GoalReached { get; private set; }
        public bool ShowPath { get; set; }
        public float ElapsedTime { get { return (GoalReached ? finishTime : Time.time) - startTime; } }
        public int RemainingCells { get; private set; }
        public int Kills { get; private set; }
        public int SpawnedEnemies { get; private set; }
        public int SpawnedItems { get; private set; }
        public readonly List<EnemyHealth> enemies = new List<EnemyHealth>();
        public DungeonBuildContext Context { get; private set; }
        public DungeonObjectives Objectives { get; private set; }
        /// <summary>Zombie → index room asal (untuk objektif clear room).</summary>
        public readonly Dictionary<EnemyHealth, int> roomEnemies = new Dictionary<EnemyHealth, int>();

        Scene gameScene;
        EnemyCatalog catalog;
        float bossScale = 2f;
        string bossVariantId = "boss_v1";
        GameObject generatorTemplate, lampTemplate;
        float startTime, finishTime;
        LineRenderer pathCore, pathGlow;
        readonly List<Vector2Int> guide = new List<Vector2Int>();
        float nextGuide;

        public static void Install(Scene scene)
        {
            IndustrialGameSession original = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                original = root.GetComponentInChildren<IndustrialGameSession>(true);
                if (original != null) break;
            }
            if (original == null) { Debug.LogError("[DUNGEON] IndustrialGameSession tidak ditemukan; mode Dungeon dibatalkan."); return; }
            original.enabled = false;
            var s = original.gameObject.AddComponent<DungeonSession>();
            s.systems = original.systems;
            s.navigation = original.navigation;
            s.additionalNavigation = original.additionalNavigation;
            s.gameScene = scene;
            original.gameObject.AddComponent<DungeonDebugView>();
        }

        void Awake() { Instance = this; ShowPath = true; }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            EnemyHealth.AnyDied -= OnEnemyDied;
            if (PlayerHealth.Instance != null) PlayerHealth.Instance.OnDied -= OnPlayerDied;
        }

        IEnumerator Start()
        {
            if (systems == null) { Debug.LogError("[DUNGEON] Systems root kosong.", this); yield break; }
            Profile = DungeonProfile.Load();

            // 1. Atmosfer malam + template lampu/generator dari scene PCG (bisa diedit manual di sana).
            if (SceneUtility.GetBuildIndexByScenePath(PcgEnvironment.ScenePath) >= 0)
            {
                var environment = SceneManager.GetSceneByPath(PcgEnvironment.ScenePath);
                if (!environment.isLoaded)
                {
                    var loading = SceneManager.LoadSceneAsync(PcgEnvironment.ScenePath, LoadSceneMode.Additive);
                    if (loading != null) yield return loading;
                    environment = SceneManager.GetSceneByPath(PcgEnvironment.ScenePath);
                }
                var env = PcgEnvironment.FindIn(environment);
                if (env != null)
                {
                    generatorTemplate = env.generatorTemplate; lampTemplate = env.lampTemplate;
                    if (env.templatesRoot != null) env.templatesRoot.gameObject.SetActive(false);
                    if (env.staticObjects != null) env.staticObjects.gameObject.SetActive(false);   // objek manual khusus arena PCG
                    SceneManager.SetActiveScene(environment);
                    DynamicGI.UpdateEnvironment();
                }
            }
            else Debug.LogWarning("[DUNGEON] Scene PCG tidak ada di Build Settings; dungeon dibangun tanpa atmosfer malam.", this);
            var industrial = SceneManager.GetSceneByPath(IndustrialGameSession.EnvironmentScene);
            if (industrial.IsValid() && industrial.isLoaded) yield return SceneManager.UnloadSceneAsync(industrial);
            foreach (var t in gameScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
                if (t.name == "Approach lane invisible walls") t.gameObject.SetActive(false);

            // 2. Objective Mini Project (wave + generator HP) tidak dipakai di mode ini. Script aslinya tidak diubah:
            //    komponen WaveDirector instance ini dihapus sebelum aktif, generator objective dinonaktifkan.
            var wd = FindInGame<WaveDirector>();
            if (wd != null) { catalog = wd.catalog; bossScale = wd.bossScale; bossVariantId = wd.bossVariantId; Destroy(wd); }
            var gen = FindInGame<GeneratorHealth>();
            if (gen != null) { if (gen.gameObject != systems) gen.gameObject.SetActive(false); else gen.enabled = false; }
            yield return null;

            // 3. Generate (grid) → validasi → build (scene).
            Seed = DungeonMode.NextSeed(Profile);
            Algorithm = DungeonMode.AlgorithmOverride ?? Profile.algorithm;
            Context = MakeContext(false);
            Generate();

            // 4. Nyalakan gameplay asli, lalu isi dungeon.
            systems.SetActive(true);
            yield return null;
            var area = FindInGame<PlayArea>();
            if (area != null) { area.min = new Vector2(-World.HalfX - 1f, -World.HalfZ - 1f); area.max = new Vector2(World.HalfX + 1f, World.HalfZ + 1f); }
            var minimap = FindInGame<MinimapController>();
            if (minimap != null) { minimap.mapCenter = Vector3.zero; minimap.largeCamSize = Mathf.Max(World.HalfX, World.HalfZ) * 1.06f; }
            var fps = FindInGame<FpsController>();
            if (fps != null && fps.isActiveAndEnabled) fps.Teleport(World.startWorld + Vector3.up * 0.1f, World.startYaw);
            Vector2Int? bossCell;
            SpawnContent(out bossCell);
            Objectives = gameObject.AddComponent<DungeonObjectives>();
            Objectives.Setup(this, BossVariant(), bossScale, bossCell);
            BuildHud();
            BuildPathGuide();
            EnemyHealth.AnyDied += OnEnemyDied;
            if (PlayerHealth.Instance != null) PlayerHealth.Instance.OnDied += OnPlayerDied;
            startTime = Time.time;
            Ready = true;
            HudEvents.ShowToast("DUNGEON SEED " + Seed + "  //  " + (Objectives.relays.Count > 0 ? "ACTIVATE " + Objectives.relays.Count + " POWER RELAYS" : "REACH THE GENERATOR"), new Color(1f, .82f, .35f));
        }

        public DungeonBuildContext MakeContext(bool preview)
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
            var normal = catalog != null ? catalog.variants.FirstOrDefault(v => v != null && v.prefab != null && v.kind == EnemyKind.Normal) : null;
            var boss = BossVariant();
            return new DungeonBuildContext
            {
                generatorTemplate = generatorTemplate, lampTemplate = lampTemplate, surfaces = surfaces.ToArray(),
                normalFilter = FilterFor(normal != null ? normal.prefab : null), bossFilter = FilterFor(boss != null ? boss.prefab : null),
                blockMask = block, probeMask = probe, preview = preview,
            };
        }

        static NavMeshQueryFilter FilterFor(GameObject prefab)
        {
            var agent = prefab != null ? prefab.GetComponent<NavMeshAgent>() : null;
            return agent != null ? new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }
                                 : new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
        }

        EnemyVariant BossVariant()
        {
            if (catalog == null) return null;
            return catalog.variants.FirstOrDefault(v => v != null && v.prefab != null && v.id == bossVariantId)
                ?? catalog.variants.FirstOrDefault(v => v != null && v.prefab != null && v.kind == EnemyKind.Boss);
        }

        void Generate()
        {
            var helper = DungeonBuilder.CreateHelper(Context);
            float tile = DungeonBuilder.ResolveTile(Profile, helper);
            var allowed = DungeonBuilder.PropAllowed(Profile, helper, tile);
            Layout = DungeonGenerator.Generate(Profile, Seed, Algorithm, allowed);
            World = DungeonBuilder.Build(Layout, Profile, Context, helper, "DUNGEON (seed " + Seed + ", " + Algorithm + ")");
            Debug.Log("[DUNGEON] Seed " + Seed + " | " + Algorithm + " " + Layout.width + "x" + Layout.height + " tile " + World.tile.ToString("0.00") + " m | rooms " + Layout.rooms.Count +
                      " | leaves " + Layout.leafNodes.Count + " | attempts " + Layout.attempts + " repairs " + Layout.repairs + " | valid " + Layout.valid +
                      " | start-goal BFS " + Layout.startGoalPathLength + " cell | NavMesh " + World.navReport + " | " + World.gameObjectCount + " GameObject, build " +
                      World.buildMs.ToString("0") + " ms + NavMesh " + World.navMs.ToString("0") + " ms", this);
            foreach (var line in Layout.attemptLog) Debug.Log("[DUNGEON] " + line, this);
            foreach (var c in Layout.checks) if (!c.pass) Debug.LogWarning("[DUNGEON] " + (c.hard ? "HARD" : "soft") + " constraint gagal: " + c.name + " " + c.detail, this);
        }

        // ---------------------------------------------------------------- spawn enemy, boss, item

        void SpawnContent(out Vector2Int? bossCell)
        {
            bossCell = null;
            bool deferBoss = Profile.bossAfterRelays && Layout.Count(PlaceKind.Relay) > 0;
            var byId = catalog != null ? catalog.variants.Where(v => v != null && v.prefab != null && v.kind == EnemyKind.Normal).GroupBy(v => v.id).ToDictionary(g => g.Key, g => g.First())
                                       : new Dictionary<string, EnemyVariant>();
            var boss = BossVariant();
            var drop = FindInGame<DropManager>();
            var counts = new Dictionary<string, int>();
            var itemRoot = new GameObject("Items (ammo / medkit)").transform; itemRoot.SetParent(World.root, false);
            foreach (var q in Layout.placements)
            {
                Vector3 pos = World.CellToWorld(q.cell);
                if (q.kind == PlaceKind.Boss) bossCell = q.cell;
                if (q.kind == PlaceKind.Boss && deferBoss) continue;               // boss baru muncul setelah relay terakhir aktif
                if (q.kind == PlaceKind.Enemy || q.kind == PlaceKind.Boss)
                {
                    EnemyVariant v = null;
                    if (q.kind == PlaceKind.Boss) v = boss;
                    else if (!byId.TryGetValue(q.id, out v)) v = byId.Values.FirstOrDefault();
                    if (v == null) continue;
                    var go = EnemyDebugSpawner.SpawnAt(v.prefab, pos);
                    if (go == null) continue;
                    go.transform.rotation = Quaternion.Euler(0f, q.yaw, 0f);
                    go.name = (q.kind == PlaceKind.Boss ? "Boss " : "Zombie ") + v.id + " @" + q.cell.x + "," + q.cell.y;
                    var hp = go.GetComponent<EnemyHealth>();
                    if (q.kind == PlaceKind.Boss && hp != null && hp.config != null)
                        go.transform.localScale = Vector3.one * (bossScale / Mathf.Max(0.01f, hp.config.visualScale));
                    if (hp != null) { enemies.Add(hp); if (q.kind == PlaceKind.Enemy) roomEnemies[hp] = q.room; }
                    counts.TryGetValue(v.id, out int n); counts[v.id] = n + 1;
                    SpawnedEnemies++;
                }
                else if (q.kind == PlaceKind.Item && drop != null)
                {
                    var prefab = q.id == SupplyKind.Health.ToString() ? drop.healthPrefab : drop.ammoPrefab;
                    if (prefab == null) continue;
                    var go = Instantiate(prefab, pos, Quaternion.Euler(0f, q.yaw, 0f), itemRoot);
                    go.name = "Item " + q.id + " @" + q.cell.x + "," + q.cell.y;
                    var item = go.GetComponent<PickupItem>();
                    if (item != null) item.lifetime = 100000f;
                    SpawnedItems++;
                }
            }
            Debug.Log("[DUNGEON] Spawned: " + string.Join(", ", counts.Select(k => k.Key + " " + k.Value)) + " | item " + SpawnedItems +
                      " | rencana enemy " + Layout.Count(PlaceKind.Enemy) + " + boss " + Layout.Count(PlaceKind.Boss), this);
        }

        void OnEnemyDied(EnemyHealth e) { if (enemies.Contains(e)) Kills++; }

        void OnPlayerDied()
        {
            DungeonMode.PendingSeed = Seed;   // ENTER setelah gugur = ulangi dungeon yang sama
        }

        public int EnemiesAlive { get { int n = 0; foreach (var e in enemies) if (e != null && !e.IsDead) n++; return n; } }

        // ---------------------------------------------------------------- loop: path guide + goal

        void Update()
        {
            if (!Ready) return;
            var hp = PlayerHealth.Instance;
            if (hp != null && !GoalReached && Time.time >= nextGuide)
            {
                nextGuide = Time.time + 0.25f;
                var cell = World.WorldToCell(hp.transform.position);
                if (!Layout.IsFloor(cell)) cell = NearestFloor(cell);
                var target = Objectives != null ? Objectives.GuideTarget(cell, guide) : Layout.goalPosition;
                if (DungeonGenerator.FindPath(Layout, cell, target, guide)) RemainingCells = guide.Count - 1;
                UpdateGuideLine();
            }
            if (hp != null && !hp.IsDead && !GoalReached && (Objectives == null || Objectives.GeneratorUnlocked) && NearGoal(hp.transform.position)) ReachGoal();
            if (GoalReached)
            {
                var kb = Keyboard.current;
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                    DungeonMode.Restart(System.Environment.TickCount & 0x7fffff);
            }
            UpdateHud();
        }

        public bool NearGoal(Vector3 p)
        {
            if (World.goalBounds.size.sqrMagnitude <= 0f) return false;
            Vector3 closest = World.goalBounds.ClosestPoint(new Vector3(p.x, World.goalBounds.center.y, p.z));
            return PcgArena.Flat(closest - p).magnitude < 2.6f;
        }

        /// <summary>Demo (F11): ke samping relay belum aktif terdekat; bila semua aktif, ke critical path beberapa cell sebelum Goal.</summary>
        public void DemoTeleportNearGoal()
        {
            if (Layout.criticalPath.Count < 2 || GoalReached) return;
            Vector3 relayPos; float relayYaw;
            if (Objectives != null && Objectives.DemoTarget(out relayPos, out relayYaw))
            {
                var fpsR = FindInGame<FpsController>();
                if (fpsR != null) fpsR.Teleport(relayPos + Vector3.up * 0.1f, relayYaw);
                HudEvents.ShowToast("DEMO: TELEPORT TO RELAY", new Color(1f, .82f, .35f));
                return;
            }
            var cell = Layout.criticalPath[Mathf.Max(0, Layout.criticalPath.Count - 6)];
            var next = Layout.criticalPath[Mathf.Max(0, Layout.criticalPath.Count - 5)];
            Vector3 d = World.CellToWorld(next) - World.CellToWorld(cell);
            var fps = FindInGame<FpsController>();
            if (fps != null) fps.Teleport(World.CellToWorld(cell) + Vector3.up * 0.1f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            HudEvents.ShowToast("DEMO: TELEPORT NEAR GOAL", new Color(1f, .82f, .35f));
        }

        Vector2Int NearestFloor(Vector2Int c)
        {
            for (int r = 1; r < 4; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                        if (Layout.IsFloor(c + new Vector2Int(dx, dy))) return c + new Vector2Int(dx, dy);
            return Layout.startPosition;
        }

        void ReachGoal()
        {
            GoalReached = true; finishTime = Time.time;
            var hp = PlayerHealth.Instance;
            var c = hp != null ? hp.GetComponent<FpsController>() : null;
            if (c != null) c.InputEnabled = false;
            FpsController.LockCursor(false);
            foreach (var l in World.root.GetComponentsInChildren<Light>()) if (l.transform.IsChildOf(World.root.Find("Goal (generator)"))) { l.color = new Color(0.55f, 1f, 0.6f); l.intensity *= 1.6f; }
            Debug.Log("[DUNGEON] GOAL tercapai: seed " + Seed + ", waktu " + ElapsedTime.ToString("0.0") + " s, kill " + Kills + "/" + enemies.Count, this);
            string extra = Objectives != null ? "  //  relays " + Objectives.ActiveRelays + "/" + Objectives.relays.Count + "  //  rooms cleared " + Objectives.RoomsCleared : "";
            ShowEnd("POWER RESTORED", "Goal reached in " + FormatTime(ElapsedTime) + "  //  seed " + Seed + "  //  " + Kills + " kills" + extra + "\nENTER new dungeon   ·   F7 replay this seed");
            StartCoroutine(FreezeLater());
        }

        IEnumerator FreezeLater() { yield return new WaitForSecondsRealtime(1.2f); if (GoalReached) Time.timeScale = 0f; }

        public static string FormatTime(float s) { int m = Mathf.FloorToInt(s / 60f); return m + ":" + Mathf.FloorToInt(s - m * 60).ToString("00"); }

        // ---------------------------------------------------------------- bantuan visual: path guide

        void BuildPathGuide()
        {
            var root = new GameObject("Path guide (BFS player → goal)").transform; root.SetParent(World.root, false);
            var pts = new List<Vector3> { Vector3.zero, Vector3.zero };
            pathCore = DungeonBuilder.Line(root, "Path core", pts, Profile.lineWidth, Profile.pathColor * Profile.lineGlow, false, false);
            pathGlow = DungeonBuilder.Line(root, "Path glow", pts, Profile.lineWidth * 4f, Profile.pathColor * 0.35f, true, false);
        }

        void UpdateGuideLine()
        {
            if (pathCore == null) return;
            bool on = ShowPath && guide.Count > 1;
            pathCore.enabled = pathGlow.enabled = on;
            if (!on) return;
            var pts = guide.Select(c => World.CellToWorld(c) + Vector3.up * 0.07f).ToArray();
            pathCore.positionCount = pathGlow.positionCount = pts.Length;
            pathCore.SetPositions(pts); pathGlow.SetPositions(pts);
        }

        // ---------------------------------------------------------------- HUD

        Canvas hud;
        CanvasGroup hudGroup;
        TMP_Text title, status, endText, prompt;
        Image endPanel, endEdge;
        MilitaryText endTyping;

        void BuildHud()
        {
            var cg = new GameObject("DungeonHUD", typeof(Canvas), typeof(CanvasScaler));
            cg.transform.SetParent(transform, false);
            hud = cg.GetComponent<Canvas>(); hud.renderMode = RenderMode.ScreenSpaceOverlay; hud.sortingOrder = 60;
            hudGroup = cg.AddComponent<CanvasGroup>();
            var sc = cg.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            var top = new Vector2(0.5f, 1f);
            HudStyle.Box("Dungeon status panel", cg.transform, top, new Vector2(0, -28), new Vector2(720, 106), HudStyle.Panel);
            HudStyle.Box("Dungeon status top edge", cg.transform, top, new Vector2(0, -28), new Vector2(720, 2), HudStyle.Amber);
            title = HudStyle.Text("DungeonTitle", cg.transform, 30, TextAnchor.UpperCenter, top, new Vector2(0, -40), new Vector2(690, 45));
            status = HudStyle.Text("DungeonStatus", cg.transform, 18, TextAnchor.UpperCenter, top, new Vector2(0, -92), new Vector2(690, 28), true);
            status.color = HudStyle.Muted;

            prompt = HudStyle.Text("DungeonPrompt", cg.transform, 26, TextAnchor.MiddleCenter, new Vector2(.5f, .3f), Vector2.zero, new Vector2(1000, 50));
            prompt.color = new Color(1f, .82f, .35f);

            var pgo = new GameObject("EndPanel", typeof(RectTransform), typeof(Image));
            pgo.transform.SetParent(cg.transform, false);
            var pr = (RectTransform)pgo.transform; pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = pr.offsetMax = Vector2.zero;
            endPanel = pgo.GetComponent<Image>(); endPanel.raycastTarget = false; endPanel.enabled = false;
            endEdge = HudStyle.Box("End edge", cg.transform, new Vector2(.5f, .5f), new Vector2(0, 117), new Vector2(560, 3), HudStyle.Amber); endEdge.enabled = false;
            endText = HudStyle.Text("EndText", cg.transform, 64, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), Vector2.zero, new Vector2(1300, 240));
            endText.overflowMode = TextOverflowModes.Overflow;
            endTyping = endText.gameObject.AddComponent<MilitaryText>();
            endText.enabled = false;
        }

        void ShowEnd(string head, string sub)
        {
            if (endPanel == null) return;
            endPanel.color = new Color(HudStyle.Panel.r, HudStyle.Panel.g, HudStyle.Panel.b, .72f); endPanel.enabled = true;
            endEdge.enabled = true; endText.enabled = true;
            endTyping.Speak(head + "\n<size=24>" + sub + "</size>");
        }

        void UpdateHud()
        {
            if (title == null) return;
            hudGroup.alpha = MinimapController.Instance != null && MinimapController.Instance.IsLarge ? 0f : 1f;
            title.text = "PROCEDURAL DUNGEON  //  SEED " + Seed;
            var hp = PlayerHealth.Instance;
            if (GoalReached) status.text = "GOAL REACHED  //  " + FormatTime(ElapsedTime);
            else if (hp != null && hp.IsDead) status.text = "KIA  //  ENTER retries seed " + Seed;
            else status.text = (Objectives != null ? Objectives.ObjectiveText() : "REACH THE GENERATOR") + "  //  " + Mathf.RoundToInt(RemainingCells * World.tile) + " m  //  CONTACTS " + EnemiesAlive + "/" + enemies.Count + "  //  " + FormatTime(ElapsedTime);
            prompt.text = !GoalReached && Objectives != null && !string.IsNullOrEmpty(Objectives.Prompt) ? Objectives.Prompt : "";
        }

        // ---------------------------------------------------------------- util

        public T FindInGame<T>() where T : Component
        {
            foreach (var root in gameScene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }
    }
}
