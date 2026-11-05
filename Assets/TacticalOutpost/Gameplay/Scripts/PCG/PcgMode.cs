using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Outpost.UI;

namespace Outpost.PCG
{
    /// <summary>
    /// Mode permainan terpisah. Tidak ada script lama yang diubah: mode dipasang lewat hook sceneLoaded.
    ///  - Scene menu: tombol START OPERATION diarahkan ke pemilih mode (PcgModeSelect).
    ///  - Scene game: bila mode PCG aktif, IndustrialGameSession dimatikan sebelum Start() dan diganti PcgSession.
    /// Mode Mini Project = alur lama, tidak disentuh sama sekali.
    /// </summary>
    public static class PcgMode
    {
        public static bool Active { get; set; }
        public static int? PendingSeed { get; set; }
        static readonly List<int> history = new List<int>();
        public static IReadOnlyList<int> SeedHistory { get { return history; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = false; PendingSeed = null; history.Clear();
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
            if (scene.path == GameMenu.MenuScene)
            {
                Active = false;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var menu in root.GetComponentsInChildren<GameMenu>(true))
                        if (menu.mainMenu && menu.GetComponent<PcgModeSelect>() == null) menu.gameObject.AddComponent<PcgModeSelect>();
            }
            else if (scene.path == GameMenu.GameScene && Active)
            {
                PcgSession.Install(scene);
            }
        }

        public static int NextSeed(PcgProfile profile)
        {
            int seed;
            if (PendingSeed.HasValue) { seed = PendingSeed.Value; PendingSeed = null; }
            else if (profile == null || profile.useRandomSeed) seed = System.Environment.TickCount & 0x7fffffff;
            else seed = profile.seed;
            history.Remove(seed);
            history.Insert(0, seed);
            if (history.Count > 8) history.RemoveAt(history.Count - 1);
            return seed;
        }

        /// <summary>Muat ulang mode PCG dengan seed tertentu (F6 seed baru, F7 ulangi seed).</summary>
        public static void Restart(int seed)
        {
            Active = true;
            PendingSeed = seed;
            Time.timeScale = 1f;
            SceneManager.LoadScene(GameMenu.GameScene);
        }
    }
}
