using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Outpost.UI;

namespace Outpost.Final
{
    /// <summary>
    /// Mode Final Project (MODE 04): map industrial statis Mini Project + DDA + PCG event wave + NPC sekutu.
    /// Dipasang lewat hook sceneLoaded seperti mode PCG/Dungeon. IndustrialGameSession TIDAK diganti:
    /// map, wave, dan AI zombie asli tetap jalan, FinalSession hanya menempel di atasnya.
    /// </summary>
    public static class FinalMode
    {
        public static bool Active { get; set; }
        public static int? PendingSeed { get; set; }
        /// <summary>Difficulty yang dibawa ke percobaan berikutnya (ENTER setelah kalah/menang). Null = pakai startDifficulty.</summary>
        public static float? CarriedDifficulty { get; set; }
        static readonly List<int> history = new List<int>();
        public static IReadOnlyList<int> SeedHistory { get { return history; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = false; PendingSeed = null; CarriedDifficulty = null; history.Clear();
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
            if (scene.path == GameMenu.MenuScene) { Active = false; CarriedDifficulty = null; }
            else if (scene.path == GameMenu.GameScene && Active)
            {
                var go = new GameObject("Final Session");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<FinalSession>();
            }
        }

        public static int NextSeed(FinalProfile profile)
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
    }
}
