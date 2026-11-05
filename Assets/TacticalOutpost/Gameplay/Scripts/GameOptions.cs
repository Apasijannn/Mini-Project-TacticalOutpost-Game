using Outpost.Player;
using UnityEngine;

namespace Outpost.UI
{
    public static class GameOptions
    {
        const string Prefix = "Outpost.Settings.";
        public static float Volume => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Volume", 1f));
        public static float Sensitivity => Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "Sensitivity", .11f), .04f, .3f);
        public static float FieldOfView => Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "Fov", 70f), 60f, 100f);
        public static bool HeadBob => PlayerPrefs.GetInt(Prefix + "HeadBob", 1) != 0;
        public static int Quality => Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Quality", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
        public static bool Fullscreen => PlayerPrefs.GetInt(Prefix + "Fullscreen", Screen.fullScreen ? 1 : 0) != 0;

        public static void ApplyGlobal()
        {
            AudioListener.volume = Volume;
            if (PlayerPrefs.HasKey(Prefix + "Quality") && QualitySettings.GetQualityLevel() != Quality)
                QualitySettings.SetQualityLevel(Quality, true);
            if (!Application.isEditor && PlayerPrefs.HasKey(Prefix + "Fullscreen")) Screen.fullScreen = Fullscreen;
        }

        public static void ApplyToPlayer(FpsController player)
        {
            player.mouseSensitivity = Sensitivity;
            player.baseFov = FieldOfView;
            player.headBob = HeadBob;
        }

        public static void Save(float volume, float sensitivity, float fov, bool headBob, int quality, bool fullscreen)
        {
            PlayerPrefs.SetFloat(Prefix + "Volume", Mathf.Clamp01(volume));
            PlayerPrefs.SetFloat(Prefix + "Sensitivity", Mathf.Clamp(sensitivity, .04f, .3f));
            PlayerPrefs.SetFloat(Prefix + "Fov", Mathf.Clamp(fov, 60f, 100f));
            PlayerPrefs.SetInt(Prefix + "HeadBob", headBob ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "Quality", Mathf.Clamp(quality, 0, QualitySettings.names.Length - 1));
            PlayerPrefs.SetInt(Prefix + "Fullscreen", fullscreen ? 1 : 0);
            PlayerPrefs.Save();
            ApplyGlobal();
            if (PlayerHealth.Instance != null) ApplyToPlayer(PlayerHealth.Instance.GetComponent<FpsController>());
        }
    }
}
