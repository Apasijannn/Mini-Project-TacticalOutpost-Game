using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TheKnightShift.SkyboxSampler
{
    /// <summary>
    /// Drop on any GameObject, fill the Skyboxes array with the sky materials,
    /// then press Left / Right arrow in Play mode to switch skies.
    /// Works with the classic Input Manager, the Input System package, or both.
    /// </summary>
    public class SkyboxCycler : MonoBehaviour
    {
        public Material[] skyboxes;
        public bool showLabel = true;

        int index;

        void Start()
        {
            if (skyboxes != null && skyboxes.Length > 0) Apply(0);
        }

        void Update()
        {
            if (skyboxes == null || skyboxes.Length == 0) return;
            if (Pressed(true)) Apply(index + 1);
            else if (Pressed(false)) Apply(index - 1);
        }

        // right = true checks the Right arrow, false the Left arrow.
        static bool Pressed(bool right)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (right ? kb.rightArrowKey : kb.leftArrowKey).wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(right ? KeyCode.RightArrow : KeyCode.LeftArrow)) return true;
#endif
            return false;
        }

        void Apply(int i)
        {
            index = (i % skyboxes.Length + skyboxes.Length) % skyboxes.Length;
            RenderSettings.skybox = skyboxes[index];
            DynamicGI.UpdateEnvironment();
        }

        void OnGUI()
        {
            if (!showLabel || skyboxes == null || skyboxes.Length == 0 || skyboxes[index] == null) return;
            GUI.Label(new Rect(12, 10, 600, 24),
                (index + 1) + "/" + skyboxes.Length + "  " + skyboxes[index].name + "   (Left/Right arrow)");
        }
    }
}
