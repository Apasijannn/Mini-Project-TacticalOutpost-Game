using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Outpost
{
    // Keep gameplay authoring in its own scene; the industrial scene remains the environment source.
    [DefaultExecutionOrder(-1000)]
    public sealed class IndustrialGameSession : MonoBehaviour
    {
        public const string EnvironmentScene = "Assets/Scenes/TacticalOutpost_Solo.unity";
        public GameObject systems;
        public NavMeshSurface navigation;
        public NavMeshSurface[] additionalNavigation;

        IEnumerator Start()
        {
            if (systems == null)
            {
                Debug.LogError("Industrial game session has no systems root.", this);
                yield break;
            }
            var environment = SceneManager.GetSceneByPath(EnvironmentScene);
            if (!environment.IsValid() || !environment.isLoaded)
            {
                var loading = SceneManager.LoadSceneAsync(EnvironmentScene, LoadSceneMode.Additive);
                if (loading == null) yield break;
                yield return loading;
                environment = SceneManager.GetSceneByPath(EnvironmentScene);
            }
            if (!environment.IsValid() || !environment.isLoaded)
            {
                Debug.LogError("Industrial environment could not be loaded.", this);
                yield break;
            }
            foreach (var root in environment.GetRootGameObjects())
            {
                foreach (var walkthrough in root.GetComponentsInChildren<SoloWalkthroughController>(true))
                    walkthrough.gameObject.SetActive(false);
                foreach (var surface in root.GetComponentsInChildren<NavMeshSurface>(true))
                    surface.enabled = false;
            }
            // Use the environment's current sky, fog and sun; restart handlers use their owning scene.
            SceneManager.SetActiveScene(environment);
            if (navigation != null) navigation.enabled = true;
            if (additionalNavigation != null)
                foreach (var surface in additionalNavigation) if (surface != null) surface.enabled = true;
            systems.SetActive(true);
        }
    }
}
