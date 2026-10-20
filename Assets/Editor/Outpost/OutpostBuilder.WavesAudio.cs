#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Outpost.Audio;
using Outpost.Enemy;
using Outpost.Player;

// Wave, SFX, dan penghapusan pisau. Dipakai oleh build penuh DAN oleh perintah "run setupwaves"
// (yang mengubah scene yang sedang terbuka tanpa menyentuh environment).
public static partial class OutpostBuilder
{
    const string AudioDir = "Assets/Audio";

    static AudioClip Clip(string file)
    {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/" + file);
        if (c == null) Log("AUDIO TIDAK ADA: " + file);
        return c;
    }

    // ---------- suara senjata (per senjata) ----------
    static void WireWeaponAudio(FpsWeapon ak, FpsWeapon pi)
    {
        if (ak != null)
        {
            ak.shootClip = Clip("ak47_shoot_single.wav"); ak.reloadClip = Clip("ak47_reload.mp3"); ak.emptyClip = Clip("empty_ammo.flac");
            ak.shootVolume = 0.8f; ak.reloadVolume = 0.9f; ak.emptyVolume = 0.8f;
        }
        if (pi != null)
        {
            pi.shootClip = Clip("pistol_shoot.mp3"); pi.reloadClip = Clip("pistol_reload.wav"); pi.emptyClip = Clip("empty_ammo.flac");
            pi.shootVolume = 0.85f; pi.reloadVolume = 0.9f; pi.emptyVolume = 0.8f;
        }
    }

    // ---------- suara langkah (di FpsController) ----------
    static void WireFootstepAudio(FpsController c)
    {
        if (c == null) return;
        c.walkClip = Clip("player_walk.wav"); c.runClip = Clip("player_run.wav");
        c.walkVolume = 0.5f; c.runVolume = 0.7f; c.crouchVolume = 0.25f;
    }

    // ---------- AudioManager (suara player lain + zombie) ----------
    static void PlaceAudioManager()
    {
        var am = Object.FindFirstObjectByType<OutpostAudio>();
        GameObject go;
        if (am == null) { go = new GameObject("AudioManager"); am = go.AddComponent<OutpostAudio>(); }
        am.switchWeapon = Clip("switch_weapon.wav");
        am.playerDead = Clip("player_dead.wav");
        am.pickAmmo = Clip("pick_ammo.wav");
        am.pickHealth = Clip("pick_health.wav");
        am.zombieGroan = Clip("zombie.wav");
        am.zombieBite = Clip("zombie_bite.wav");
        am.bossRoar = Clip("zombie_bos.mp3");
        EditorUtility.SetDirty(am);
        Log("AudioManager ditempatkan");
    }

    // ---------- WaveDirector ----------
    static void PlaceWaveDirector()
    {
        var cat = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath);
        var wd = Object.FindFirstObjectByType<WaveDirector>();
        if (wd == null) { var go = new GameObject("WaveDirector"); wd = go.AddComponent<WaveDirector>(); }
        wd.catalog = cat;
        wd.waves = new[]
        {
            new WaveDirector.Wave { perSide = 5, boss = false },   // wave 1: 5 kiri + 5 kanan = 10
            new WaveDirector.Wave { perSide = 7, boss = false },   // wave 2: 7 + 7 = 14
            new WaveDirector.Wave { perSide = 9, boss = true },    // wave 3: 9 + 9 = 18 + 1 boss
        };
        wd.bossScale = 2f; wd.bossVariantId = "boss_v1";
        EditorUtility.SetDirty(wd);
        Log("WaveDirector ditempatkan (10 / 14 / 18 + boss x2)");
    }

    // ---------- buang pisau dari manajer senjata ----------
    static string RemoveKnife(WeaponManager mgr)
    {
        if (mgr == null || mgr.weapons == null) return "manajer senjata tidak ada";
        var keep = new List<FpsWeapon>(); GameObject knife = null;
        foreach (var w in mgr.weapons)
        {
            if (w == null) continue;
            if (w.name == "Knife") knife = w.gameObject; else keep.Add(w);
        }
        if (knife != null) Object.DestroyImmediate(knife);
        var g = mgr.transform.Find("GripL_Cam");      // pegangan tangan kiri milik pisau
        if (g != null)
        {
            bool used = false;
            foreach (var w in keep) if (w.gripLeft == g || w.gripRight == g) used = true;
            if (!used) Object.DestroyImmediate(g.gameObject);
        }
        mgr.weapons = keep.ToArray(); mgr.startIndex = 0;
        return "pisau " + (knife != null ? "dihapus" : "sudah tidak ada") + ", slot senjata sekarang " + keep.Count;
    }

    // "run setupwaves": ubah prefab player (hapus pisau + SFX), lalu tempatkan AudioManager & WaveDirector di scene terbuka
    static string SetupWavesAudio()
    {
        var sb = new StringBuilder();
        var contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var mgr = contents.GetComponentInChildren<WeaponManager>(true);
            sb.AppendLine(RemoveKnife(mgr));
            FpsWeapon ak = null, pi = null;
            foreach (var w in mgr.weapons) { if (w.kind == WeaponKind.Rifle) ak = w; else if (w.kind == WeaponKind.Pistol) pi = w; }
            WireWeaponAudio(ak, pi);
            WireFootstepAudio(contents.GetComponent<FpsController>());
            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }

        // runner & skinless boleh muncul sejak wave 1 supaya wave 1 dan 2 hanya beda jumlah
        var cat = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath);
        if (cat != null)
        {
            foreach (var v in cat.variants) if (v != null && (v.id == "runner" || v.id == "skinless")) v.minWave = 1;
            EditorUtility.SetDirty(cat);
        }

        PlaceAudioManager();
        PlaceWaveDirector();
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        bool ok = UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        var pl = GameObject.Find("Player_FPS");
        var m2 = pl != null ? pl.GetComponentInChildren<WeaponManager>(true) : null;
        sb.AppendLine("scene tersimpan=" + ok + ", senjata di scene: " + (m2 != null && m2.weapons != null ? m2.weapons.Length.ToString() : "?"));
        return sb.ToString();
    }
}
#endif
