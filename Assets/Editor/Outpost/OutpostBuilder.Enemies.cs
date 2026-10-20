#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Outpost.Enemy;

// Membangun controller animasi, config, prefab, dan katalog musuh.
public static partial class OutpostBuilder
{
    const string EnemyDir = "Assets/Outpost/Enemies";
    const string CtrlPath = EnemyDir + "/Enemy_Zombie.controller";
    const string CatalogPath = EnemyDir + "/EnemyCatalog.asset";

    static AnimationClip FindClip(string fbxPath, string clipName)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            var c = o as AnimationClip;
            if (c != null && c.name == clipName) return c;
        }
        Log("KLIP TIDAK DITEMUKAN: " + clipName + " di " + fbxPath);
        return null;
    }

    static AnimatorController BuildEnemyController()
    {
        EnsureFolder(EnemyDir);
        AssetDatabase.DeleteAsset(CtrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        string A = ZombieRoot + "/Animations/Zombie@";
        var idle = FindClip(A + "Z_Idle.FBX", "Z_Idle");
        var walk = FindClip(A + "Z_Walk_InPlace.FBX", "Z_Walk_InPlace");
        var walk1 = FindClip(A + "Z_Walk1_InPlace.FBX", "Z_Walk1_InPlace");
        var run = FindClip(A + "Z_Run_InPlace.FBX", "Z_Run_InPlace");
        var atk = FindClip(A + "Z_Attack.FBX", "Z_Attack");
        var fallBack = FindClip(A + "Z_FallingBack.FBX", "Z_FallingBack");
        var fallFwd = FindClip(A + "Z_FallingForward.FBX", "Z_FallingForward");
        var scream = FindClip(SkinlessRoot + "/Art/Animations/angry01.fbx", "angry");

        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Scream", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("DeathType", AnimatorControllerParameterType.Int);
        ctrl.AddParameter("AltWalk", AnimatorControllerParameterType.Bool);

        var sm = ctrl.layers[0].stateMachine;
        foreach (var st in sm.states) sm.RemoveState(st.state);

        AnimatorState MakeLoco(string name, AnimationClip walkClip, Vector3 pos)
        {
            BlendTree bt;
            var st = ctrl.CreateBlendTreeInController(name, out bt, 0);
            bt.blendParameter = "Speed"; bt.blendType = BlendTreeType.Simple1D; bt.useAutomaticThresholds = false;
            bt.AddChild(idle, 0f); bt.AddChild(walkClip, 1.2f); bt.AddChild(run, 3.4f);
            var ch = bt.children; for (int i = 0; i < ch.Length; i++) { ch[i].timeScale = 1f; } bt.children = ch;
            return st;
        }
        var loco = MakeLoco("Locomotion", walk, new Vector3(300, 0));
        var locoAlt = MakeLoco("LocomotionAlt", walk1, new Vector3(300, 80));
        sm.defaultState = loco;
        loco.writeDefaultValues = true; locoAlt.writeDefaultValues = true;

        var tToAlt = loco.AddTransition(locoAlt); tToAlt.hasExitTime = false; tToAlt.duration = 0.05f; tToAlt.AddCondition(AnimatorConditionMode.If, 0, "AltWalk");
        var tToLoco = locoAlt.AddTransition(loco); tToLoco.hasExitTime = false; tToLoco.duration = 0.05f; tToLoco.AddCondition(AnimatorConditionMode.IfNot, 0, "AltWalk");

        var sAttack = sm.AddState("Attack", new Vector3(560, -60)); sAttack.motion = atk;
        var sScream = sm.AddState("Scream", new Vector3(560, 20)); sScream.motion = scream;
        var sDeathB = sm.AddState("DeathBack", new Vector3(560, 120)); sDeathB.motion = fallBack;
        var sDeathF = sm.AddState("DeathForward", new Vector3(560, 190)); sDeathF.motion = fallFwd;

        var a1 = sm.AddAnyStateTransition(sAttack); a1.hasExitTime = false; a1.duration = 0.08f; a1.canTransitionToSelf = false; a1.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        var a2 = sm.AddAnyStateTransition(sScream); a2.hasExitTime = false; a2.duration = 0.1f; a2.canTransitionToSelf = false; a2.AddCondition(AnimatorConditionMode.If, 0, "Scream");
        var d1 = sm.AddAnyStateTransition(sDeathB); d1.hasExitTime = false; d1.duration = 0.1f; d1.canTransitionToSelf = false; d1.AddCondition(AnimatorConditionMode.If, 0, "Die"); d1.AddCondition(AnimatorConditionMode.Equals, 0, "DeathType");
        var d2 = sm.AddAnyStateTransition(sDeathF); d2.hasExitTime = false; d2.duration = 0.1f; d2.canTransitionToSelf = false; d2.AddCondition(AnimatorConditionMode.If, 0, "Die"); d2.AddCondition(AnimatorConditionMode.Equals, 1, "DeathType");

        foreach (var from in new[] { sAttack, sScream })
        {
            var e1 = from.AddTransition(loco); e1.hasExitTime = true; e1.exitTime = 0.92f; e1.duration = 0.12f; e1.AddCondition(AnimatorConditionMode.IfNot, 0, "AltWalk");
            var e2 = from.AddTransition(locoAlt); e2.hasExitTime = true; e2.exitTime = 0.92f; e2.duration = 0.12f; e2.AddCondition(AnimatorConditionMode.If, 0, "AltWalk");
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Log("controller musuh dibuat: " + CtrlPath);
        return ctrl;
    }

    static EnemyConfig MakeConfig(string file, System.Action<EnemyConfig> set)
    {
        EnsureFolder(EnemyDir + "/Configs");
        string path = EnemyDir + "/Configs/" + file + ".asset";
        var c = AssetDatabase.LoadAssetAtPath<EnemyConfig>(path);
        if (c == null) { c = ScriptableObject.CreateInstance<EnemyConfig>(); AssetDatabase.CreateAsset(c, path); }
        set(c);
        EditorUtility.SetDirty(c);
        return c;
    }

    static GameObject MakeEnemyPrefab(string name, string modelPath, EnemyConfig cfg, AnimatorController ctrl)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null) { Log("MODEL MUSUH TIDAK ADA: " + modelPath); return null; }
        float sc = cfg.visualScale;
        var root = new GameObject(name);
        root.layer = LEnemy;
        try { root.tag = "Enemy"; } catch { }

        var mi = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        mi.name = "Model";
        mi.transform.localPosition = Vector3.zero; mi.transform.localRotation = Quaternion.identity; mi.transform.localScale = Vector3.one * sc;
        var an = mi.GetComponentInChildren<Animator>();
        if (an != null)
        {
            an.runtimeAnimatorController = ctrl; an.applyRootMotion = false; an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }
        SetLayerRecursive(root, LEnemy);

        float h = 1.85f * sc, r = 0.36f * sc;
        var cap = root.AddComponent<CapsuleCollider>(); cap.height = h; cap.radius = r; cap.center = new Vector3(0, h * 0.5f, 0);
        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = Mathf.Min(0.45f, r); agent.height = h; agent.baseOffset = 0f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        agent.speed = cfg.walkSpeed; agent.acceleration = cfg.acceleration; agent.angularSpeed = cfg.angularSpeed;

        var hp = root.AddComponent<EnemyHealth>(); hp.config = cfg;
        if (an != null && an.avatar != null && an.avatar.isHuman) hp.headBone = an.GetBoneTransform(HumanBodyBones.Head);
        if (hp.headBone == null) Log("peringatan: tulang kepala tidak ditemukan untuk " + name);
        var brain = root.AddComponent<EnemyBrain>(); brain.config = cfg; brain.animator = an;
        var bar = root.AddComponent<EnemyHealthBar>();
        bar.health = hp;
        bar.barBackground = GuiSprite("Ui/Icon_34.png"); bar.barFill = GuiSprite("Ui/Icon_35.png");
        bool isBoss = cfg.kind == EnemyKind.Boss;
        bar.width = isBoss ? 1.7f : 0.85f; bar.showName = isBoss; bar.label = cfg.displayName; bar.visibleDistance = isBoss ? 60f : 30f;
        EnsureMinimapIcons();
        if (cfg.kind == EnemyKind.Boss) AddBlip(root, dotSprite, new Color(1f, 0.55f, 0.05f), 0.6f, true, 2);
        else AddBlip(root, dotSprite, new Color(1f, 0.22f, 0.2f), 0f, false, 1);

        EnsureFolder(EnemyDir + "/Prefabs");
        string pp = EnemyDir + "/Prefabs/" + name + ".prefab";
        var saved = PrefabUtility.SaveAsPrefabAsset(root, pp);
        Object.DestroyImmediate(root);
        Log("prefab musuh: " + pp + " (" + cfg.displayName + ", HP " + cfg.maxHealth + ")");
        return saved;
    }

    static string BuildEnemies()
    {
        var sb = new StringBuilder();
        Log("persiapan aset musuh:\n" + PrepareEnemyAssets());
        var ctrl = BuildEnemyController();

        // ---- konfigurasi ----
        var cA = MakeConfig("Zombie_Walker", c => { c.displayName = "Zombie Walker"; c.kind = EnemyKind.Normal; c.maxHealth = 110; c.walkSpeed = 1.1f; c.runSpeed = 3.0f; c.attackDamage = 10; c.attackCooldown = 1.5f; c.visualScale = 1f; c.headshotMultiplier = 3f; });
        var cB = MakeConfig("Zombie_Bloater", c => { c.displayName = "Zombie Bloater"; c.kind = EnemyKind.Normal; c.maxHealth = 150; c.walkSpeed = 0.9f; c.runSpeed = 2.4f; c.attackDamage = 14; c.attackCooldown = 1.7f; c.staggerThreshold = 0.2f; c.visualScale = 1.05f; c.headshotMultiplier = 3f; });
        var cC = MakeConfig("Zombie_Runner", c => { c.displayName = "Zombie Runner"; c.kind = EnemyKind.Normal; c.maxHealth = 80; c.walkSpeed = 1.4f; c.runSpeed = 3.9f; c.attackDamage = 9; c.attackCooldown = 1.2f; c.visualScale = 0.97f; c.animSpeed = 1.1f; c.headshotMultiplier = 3f; });
        var cS = MakeConfig("Zombie_Skinless", c => { c.displayName = "Skinless Zombie"; c.kind = EnemyKind.Normal; c.maxHealth = 90; c.walkSpeed = 1.5f; c.runSpeed = 4.3f; c.attackDamage = 13; c.attackCooldown = 1.1f; c.visualScale = 1f; c.animSpeed = 1.15f; c.sightRadius = 26f; c.headshotMultiplier = 3f; });
        System.Action<EnemyConfig, string> boss = (c, n) =>
        {
            c.displayName = n; c.kind = EnemyKind.Boss; c.maxHealth = 1000; c.walkSpeed = 2.4f; c.runSpeed = 5.2f; c.acceleration = 14f; c.angularSpeed = 300f;
            c.attackDamage = 35; c.attackRange = 2.5f; c.attackCooldown = 1.5f; c.attackHitDelay = 0.55f; c.generatorDamageMultiplier = 2.5f;
            c.sightRadius = 45f; c.sightFov = 220f; c.closeSenseRadius = 8f; c.hearingMultiplier = 2f; c.memoryTime = 14f;
            c.headshotMultiplier = 2f; c.canStagger = false; c.visualScale = 1.35f; c.animSpeed = 1.1f; c.screamOnSpawn = true; c.screamDuration = 2.4f;
            c.enrageHealthFraction = 0.5f; c.enrageSpeedMultiplier = 1.3f; c.enrageCooldownMultiplier = 0.7f; c.corpseTime = 9f;
        };
        var cM1 = MakeConfig("Boss_Mutant_V1", c => boss(c, "Mutant Boss"));
        var cM2 = MakeConfig("Boss_Mutant_V2", c => boss(c, "Mutant Boss (V2)"));
        var cM3 = MakeConfig("Boss_Mutant_V3", c => boss(c, "Mutant Boss (V3)"));
        AssetDatabase.SaveAssets();

        // ---- prefab ----
        var pA = MakeEnemyPrefab("Zombie_Walker", ZombieRoot + "/Prefabs/Zombie1.prefab", cA, ctrl);
        var pB = MakeEnemyPrefab("Zombie_Bloater", ZombieRoot + "/Prefabs/Zombie2.prefab", cB, ctrl);
        var pC = MakeEnemyPrefab("Zombie_Runner", ZombieRoot + "/Prefabs/Zombie3.prefab", cC, ctrl);
        var pS = MakeEnemyPrefab("Zombie_Skinless", SkinlessRoot + "/Prefabs/skinless zombie.prefab", cS, ctrl);
        var pM1 = MakeEnemyPrefab("Boss_Mutant_V1", MutantRoot + "/Prefab/Mutated1.prefab", cM1, ctrl);
        var pM2 = MakeEnemyPrefab("Boss_Mutant_V2", MutantRoot + "/Prefab/Mutated1_V2.prefab", cM2, ctrl);
        var pM3 = MakeEnemyPrefab("Boss_Mutant_V3", MutantRoot + "/Prefab/Mutated1_V3.prefab", cM3, ctrl);

        // ---- katalog (aturan variasi per wave; mudah diubah di Inspector) ----
        var cat = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath);
        if (cat == null) { cat = ScriptableObject.CreateInstance<EnemyCatalog>(); AssetDatabase.CreateAsset(cat, CatalogPath); }
        cat.variants = new List<EnemyVariant>
        {
            new EnemyVariant { id = "walker",   prefab = pA, kind = EnemyKind.Normal, minWave = 1, maxWave = 3, weight = 3f },
            new EnemyVariant { id = "bloater",  prefab = pB, kind = EnemyKind.Normal, minWave = 1, maxWave = 3, weight = 1.5f },
            new EnemyVariant { id = "runner",   prefab = pC, kind = EnemyKind.Normal, minWave = 1, maxWave = 3, weight = 2f },
            new EnemyVariant { id = "skinless", prefab = pS, kind = EnemyKind.Normal, minWave = 1, maxWave = 3, weight = 2f },
            new EnemyVariant { id = "boss_v1",  prefab = pM1, kind = EnemyKind.Boss, minWave = 3, maxWave = 3, weight = 1f },
            new EnemyVariant { id = "boss_v2",  prefab = pM2, kind = EnemyKind.Boss, minWave = 3, maxWave = 3, weight = 1f },
            new EnemyVariant { id = "boss_v3",  prefab = pM3, kind = EnemyKind.Boss, minWave = 3, maxWave = 3, weight = 1f },
        };
        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        Log("katalog musuh: " + CatalogPath + " (" + cat.variants.Count + " varian)");
        return sb.ToString();
    }

    // dipanggil dari BuildAll setelah scene jadi
    static void PlaceEnemyDebugSpawner()
    {
        var cat = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath);
        if (cat == null) return;
        var go = new GameObject("EnemyDebugSpawner");
        var sp = go.AddComponent<EnemyDebugSpawner>(); sp.catalog = cat;
        Log("EnemyDebugSpawner ditempatkan (F1 zombie, F2 boss, F3 x5, F4 bunuh semua)");
    }

    static string RunEnemyCmd2(string a0)
    {
        if (a0 == "enemies") { BuildEnemies(); AssetDatabase.Refresh(); return "enemies dibangun"; }
        return RunWeaponCmd(a0);
    }
}
#endif
