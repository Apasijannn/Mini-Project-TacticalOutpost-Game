using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Outpost.UI;

namespace Outpost.Dungeon
{
    /// <summary>
    /// Mode Procedural Dungeon (Modul 10). Seperti mode PCG, dipasang lewat hook sceneLoaded tanpa mengubah script lama:
    /// bila mode aktif saat scene game dimuat, IndustrialGameSession dimatikan sebelum Start() dan diganti DungeonSession.
    /// Pemilihan mode ada di kartu ketiga layar CHOOSE YOUR OPERATION (PcgModeSelect).
    /// </summary>
    public static class DungeonMode
    {
        public static bool Active { get; set; }
        public static int? PendingSeed { get; set; }
        /// <summary>Override algoritma dari F12 (null = pakai profile).</summary>
        public static DungeonAlgorithm? AlgorithmOverride { get; set; }
        static readonly List<int> history = new List<int>();
        public static IReadOnlyList<int> SeedHistory { get { return history; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = false; PendingSeed = null; AlgorithmOverride = null; history.Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path == GameMenu.MenuScene) Active = false;
            else if (scene.path == GameMenu.GameScene && Active) DungeonSession.Install(scene);
        }

        public static int NextSeed(DungeonProfile profile)
        {
            int seed;
            if (PendingSeed.HasValue) { seed = PendingSeed.Value; PendingSeed = null; }
            else if (profile == null || profile.useRandomSeed) seed = System.Environment.TickCount & 0x7fffff;
            else seed = profile.seed;
            history.Remove(seed);
            history.Insert(0, seed);
            if (history.Count > 8) history.RemoveAt(history.Count - 1);
            return seed;
        }

        /// <summary>Muat ulang mode dungeon dengan seed tertentu (F6 seed baru, F7 ulangi seed, F12 ganti algoritma).</summary>
        public static void Restart(int seed)
        {
            Active = true;
            PendingSeed = seed;
            Time.timeScale = 1f;
            SceneManager.LoadScene(GameMenu.GameScene);
        }
    }
}
