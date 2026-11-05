using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Outpost.PCG;

/// <summary>
/// Membuat Resources/PCG_Profile.asset untuk mode PCG. Hanya memakai prefab yang SUDAH DIPAKAI di scene game
/// (dependency TacticalOutpost_Solo / _Game / _Menu): prefab yang tidak dipakai di scene mana pun dilewati.
/// </summary>
[InitializeOnLoad]
static class PcgProfileSetup
{
    const string Folder = "Assets/TacticalOutpost/Gameplay/Resources";
    const string AssetPath = Folder + "/PCG_Profile.asset";
    static readonly string[] UsedScenes =
    {
        "Assets/Scenes/TacticalOutpost_Solo.unity",
        "Assets/Scenes/Gameplay/TacticalOutpost_Game.unity",
        "Assets/Scenes/Gameplay/TacticalOutpost_Menu.unity",
    };

    const string R = "Assets/RPG_FPS_game_assets_industrial/";

    static PcgProfileSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<PcgProfile>(AssetPath) == null) CreateOrUpdate();
        };
    }

    [MenuItem("Tactical Outpost/PCG/Create or Update Profile")]
    public static void CreateOrUpdate()
    {
        var used = new HashSet<string>(AssetDatabase.GetDependencies(UsedScenes.Where(File.Exists).ToArray(), true));
        var report = new List<string>();

        GameObject First(string slot, params string[] paths)
        {
            foreach (var p in paths)
            {
                if (!used.Contains(p)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) { report.Add(slot + ": " + Path.GetFileNameWithoutExtension(p)); return go; }
            }
            report.Add(slot + ": (tidak ada kandidat yang dipakai di scene)");
            return null;
        }

        PrefabEntry[] Table(string slot, params (string path, float weight)[] entries)
        {
            var list = new List<PrefabEntry>();
            foreach (var e in entries)
            {
                if (!used.Contains(e.path)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(e.path);
                if (go != null) list.Add(new PrefabEntry { name = go.name, prefab = go, weight = e.weight });
            }
            report.Add(slot + ": " + (list.Count == 0 ? "(kosong)" : string.Join(", ", list.Select(x => x.name))));
            return list.ToArray();
        }

        if (!AssetDatabase.IsValidFolder(Folder)) { Directory.CreateDirectory(Folder); AssetDatabase.Refresh(); }
        var profile = AssetDatabase.LoadAssetAtPath<PcgProfile>(AssetPath);
        bool created = profile == null;
        if (created) { profile = ScriptableObject.CreateInstance<PcgProfile>(); AssetDatabase.CreateAsset(profile, AssetPath); }

        profile.floorTile = First("Floor",
            R + "Roads/Road_sets/Road_set_v1/Road_set_v1_s_floor.prefab", R + "Roads/Road_sets/Road_set_v1/Road_set_v1_b_floor.prefab",
            R + "Roads/Road_sets/Road_set_v1/Road_set_v1_s_tile_1.prefab", R + "Roads/Road_sets/Road_set_v1/Road_set_v1_s_tile_2.prefab");
        profile.fenceSpan = First("Fence",
            R + "Fences/Concrete_fences/Concrete_fence_v2/Concrete_fence_v2_S.prefab", R + "Fences/Concrete_fences/Concrete_fence_v1/Concrete_fence_v1_s1.prefab",
            R + "Fences/Concrete_fences/UNIConcrete_fences/UNIConcrete_fence_v1_s.prefab");
        profile.fenceEnd = First("Fence end",
            R + "Fences/Concrete_fences/Concrete_fence_v2/Concrete_fence_v2_end.prefab", R + "Fences/Concrete_fences/UNIConcrete_fences/UNIConcrete_fence_v1_end.prefab",
            R + "Fences/Concrete_fences/Concrete_fence_v1/Concrete_fence_v1_column.prefab");
        profile.generator = First("Generator",
            R + "Other_props/Generators/Generator_v1/Generator_v1.prefab", R + "Oil_tanks/Oil_tank_v2/Oil_tank_v2.prefab", R + "Oil_tanks/Oil_tank_v1/Oil_tank_v1.prefab");
        var props = new List<GameObject>();
        foreach (var p in new[] { R + "Other_props/Electric_box/Electric_box_v1/Electric_box_v1.prefab", R + "Other_props/Electric_box/Electric_box_v2/Electric_box_v2.prefab",
                                  R + "Other_props/Electric_box/Electric_box_v3/Electric_box_v3.prefab", R + "Other_props/Conditioners/Conditioner_v1/Conditioner_v1.prefab" })
            if (used.Contains(p) && props.Count < 2) { var go = AssetDatabase.LoadAssetAtPath<GameObject>(p); if (go != null) props.Add(go); }
        profile.generatorProps = props.ToArray();
        report.Add("Generator props: " + string.Join(", ", props.Select(x => x.name)));
        profile.cornerTower = First("Corner tower", "Assets/TacticalOutpost/Prefabs/Tower_SingleFloodlight.prefab");
        profile.lamp = First("Lamp",
            "Assets/Sat Productions/G-01/Street Lights Pack 01/Prefabs/Street_Light_A_01.prefab", "Assets/Sat Productions/G-01/Street Lights Pack 01/Prefabs/Street_Light_A_05.prefab",
            "Assets/SpaceZeta_StreetLamps/Prefabs/StreetLamp1_Tall (Concrete).prefab", "Assets/SpaceZeta_StreetLamps/Prefabs/StreetLamp2_Tall (Concrete).prefab",
            "Assets/Industrial Props/Prefabs/WorkLight.prefab");

        profile.obstacleTable = Table("Obstacles",
            (R + "Containers/Cargo_container_v1/Cargo_container_v1_LD1close.prefab", 2f),
            (R + "Containers/Cargo_container_v1/Cargo_container_v1_LD2close.prefab", 1f),
            (R + "Oil_tanks/Oil_tank_v1/Oil_tank_v1.prefab", 1f),
            (R + "Dumpsters/Dumpsters_v1/Dumpsters_v1_garbadge.prefab", 1.5f),
            (R + "Dumpsters/Dumpsters_v1/Dumpsters_v1_empty.prefab", 1f),
            ("Assets/Sector Warzone/Concrete Barriers/Prefabs/PRE_Concrete Barrier_1.prefab", 1f),
            ("Assets/Sector Warzone/Concrete Barriers/Prefabs/PRE_Concrete Barrier_2.prefab", 1f),
            ("Assets/Sector Warzone/Concrete Barriers/Prefabs/PRE_Concrete Barrier_3.prefab", 1f),
            (R + "Fences/Road_blocks/Road_block_v1/Road_block_v1.prefab", 2f),
            (R + "Other_props/Palets/Bags_on_pallet_v1/Bags_on_pallet_v1_1.prefab", 2f),
            (R + "Other_props/Palets/Bags_on_pallet_v1/Bags_on_pallet_v1_2.prefab", 1f),
            (R + "Boxes/Wooden_box_v1/Wooden_box_v1_LD2square.prefab", 1.5f),
            (R + "Boxes/Wooden_box_v1/Wooden_box_v1_LD1.prefab", 1f),
            (R + "Barrels/Barrel_v2/Barrel_v2_quadro.prefab", 1.5f),
            (R + "Barrels/Barrel_v3/Barrel_v3_quadro.prefab", 1f),
            ("Assets/TacticalOutpost/Prefabs/BurningBarrels/BurningBarrel_Military.prefab", 1f),
            ("Assets/TacticalOutpost/Prefabs/BurningBarrels/BurningBarrel_Biohazard.prefab", 0.5f));

        profile.dressingTable = Table("Dressing",
            ("Assets/Industrial Props/Prefabs/ConstructionCone.prefab", 3f),
            ("Assets/Industrial Props/Prefabs/CinderBlock.prefab", 3f),
            ("Assets/Industrial Props/Prefabs/pallet.prefab", 2f),
            (R + "Barrels/Barrel_v1/Barrel_v1_LD2.prefab", 1f),
            (R + "Barrels/Barrel_v3/Barrel_v3_single.prefab", 1f),
            ("Assets/Sector Warzone/Post-Apocalyptic Barrels/Prefabs/PRE_Barrel_Military.prefab", 1f));

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log("[PCG] " + (created ? "Created " : "Updated ") + AssetPath + "\n" + string.Join("\n", report), profile);
    }

    /// <summary>Nyalakan tombol Gizmos di Scene View supaya Gizmos PCG (spawn area, safe zone, kandidat) terlihat.</summary>
    [MenuItem("Tactical Outpost/PCG/Show Gizmos in Scene View")]
    static void ShowGizmos()
    {
        foreach (SceneView view in SceneView.sceneViews) { view.drawGizmos = true; view.Repaint(); }
        Debug.Log("[PCG] Gizmos Scene View dinyalakan.");
    }

    /// <summary>Nyala/matikan Gizmos di Game View (misalnya untuk merekam demo PCG).</summary>
    [MenuItem("Tactical Outpost/PCG/Toggle Gizmos in Game View")]
    static void ToggleGameViewGizmos()
    {
        var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        var prop = gameViewType != null ? gameViewType.GetProperty("drawGizmos", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) : null;
        if (prop == null) return;
        foreach (var w in Resources.FindObjectsOfTypeAll(gameViewType))
        {
            bool now = !(bool)prop.GetValue(w);
            prop.SetValue(w, now);
            ((EditorWindow)w).Repaint();
            Debug.Log("[PCG] Gizmos Game View " + (now ? "ON" : "OFF"));
        }
    }

    [MenuItem("Tactical Outpost/PCG/Select Profile")]
    static void Select()
    {
        var profile = AssetDatabase.LoadAssetAtPath<PcgProfile>(AssetPath);
        if (profile == null) { CreateOrUpdate(); profile = AssetDatabase.LoadAssetAtPath<PcgProfile>(AssetPath); }
        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);
    }
}
