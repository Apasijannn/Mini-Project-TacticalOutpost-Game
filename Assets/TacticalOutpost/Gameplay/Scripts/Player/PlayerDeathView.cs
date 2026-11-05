using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Outpost.Player
{
    public sealed class PlayerDeathView : MonoBehaviour
    {
        public static PlayerDeathView Instance { get; private set; }
        public bool IsActive { get; private set; }
        public bool Settled => IsActive && Time.unscaledTime - started > 1.6f;
        FpsController controller;
        VolumeProfile profile;
        ColorAdjustments color;
        DepthOfField focus;
        Vector3 startPosition, endPosition;
        Quaternion startRotation, endRotation;
        float started;
        void Awake() { Instance = this; controller = GetComponent<FpsController>(); }
        void OnDestroy() { if (Instance == this) Instance = null; if(profile != null) Destroy(profile); }
        public void Begin()
        {
            if (IsActive || controller == null || controller.playerCamera == null) return;
            IsActive = true; started = Time.unscaledTime;
            Time.timeScale = .25f;
            Outpost.UI.MinimapController.CloseLargeMap();
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (!canvas.transform.IsChildOf(transform)) canvas.gameObject.SetActive(false);
            foreach (var debug in FindObjectsByType<Outpost.Enemy.EnemyDebugSpawner>(FindObjectsSortMode.None)) debug.enabled = false;
            controller.InputEnabled = false; controller.enabled = false;
            foreach (var source in GetComponents<AudioSource>()) source.Stop();
            foreach (var weapon in GetComponentsInChildren<FpsWeapon>(true)) weapon.gameObject.SetActive(false);
            var view = controller.playerCamera;
            startPosition = view.transform.position; startRotation = view.transform.rotation;
            endPosition = transform.position + Vector3.up * .35f;
            if (Physics.Raycast(startPosition, Vector3.down, out var hit, 3, ~((1<<8)|(1<<9)|(1<<14)), QueryTriggerInteraction.Ignore)) endPosition = hit.point + Vector3.up * .32f;
            endRotation = Quaternion.Euler(12, view.transform.eulerAngles.y, 76);
            var cameraData = view.GetUniversalAdditionalCameraData(); cameraData.renderPostProcessing = true; cameraData.requiresDepthTexture = true;
            var effects = new GameObject("Death post processing"); effects.transform.SetParent(transform, false);
            var volume = effects.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 1000;
            cameraData.volumeLayerMask |= 1;
            profile = ScriptableObject.CreateInstance<VolumeProfile>(); volume.sharedProfile = profile;
            color = profile.Add<ColorAdjustments>(true); color.saturation.Override(0);
            var vignette = profile.Add<Vignette>(true); vignette.intensity.Override(.46f); vignette.smoothness.Override(.5f);
            focus = profile.Add<DepthOfField>(true); focus.mode.Override(DepthOfFieldMode.Gaussian);
            focus.gaussianStart.Override(.1f); focus.gaussianEnd.Override(1.8f); focus.gaussianMaxRadius.Override(.5f); focus.highQualitySampling.Override(true);
            FpsController.LockCursor(false);
        }
        void LateUpdate()
        {
            if (!IsActive) return;
            float t = Mathf.Clamp01((Time.unscaledTime - started) / 1.5f), smooth = Mathf.SmoothStep(0, 1, t);
            Time.timeScale = Mathf.Lerp(.25f, 1f, Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - started - 1.5f) / .5f)));
            controller.playerCamera.transform.SetPositionAndRotation(Vector3.Lerp(startPosition, endPosition, smooth), Quaternion.Slerp(startRotation, endRotation, smooth));
            color.saturation.Override(-100 * smooth); focus.gaussianMaxRadius.Override(Mathf.Lerp(.5f, 1.35f, smooth));
        }
    }
}
