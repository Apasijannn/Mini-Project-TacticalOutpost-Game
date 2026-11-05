using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outpost.Dungeon
{
    /// <summary>
    /// Suasana malam khusus mode Procedural Dungeon (mode lain tidak disentuh, scene PCG tidak diubah di disk):
    ///  - Skybox "Outpost/Skybox Moonlit Dungeon": langit malam terang prosedural + bintang & bulan dari Stagit (skyboxv1_starsonly, skyboxv1_moon1).
    ///  - Cahaya bulan: directional light diarahkan dari posisi bulan di langit, warna biru pucat, bayangan lembut.
    ///  - Ambient trilight biru malam + fog exponential-squared tipis yang warnanya menyatu dengan cakrawala.
    /// </summary>
    public static class DungeonAtmosphere
    {
        public const string SkyResource = "Dungeon/Dungeon_MoonlitSky";

        public sealed class Snapshot
        {
            public Material skybox;
            public AmbientMode ambientMode;
            public Color ambientSky, ambientEquator, ambientGround, ambientLight;
            public float ambientIntensity;
            public bool fog;
            public FogMode fogMode;
            public Color fogColor;
            public float fogDensity;
            public readonly List<KeyValuePair<Light, (Quaternion rot, Color color, float intensity, LightShadows shadows, float strength)>> lights =
                new List<KeyValuePair<Light, (Quaternion, Color, float, LightShadows, float)>>();

            public void Restore()
            {
                RenderSettings.skybox = skybox;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientSkyColor = ambientSky; RenderSettings.ambientEquatorColor = ambientEquator;
                RenderSettings.ambientGroundColor = ambientGround; RenderSettings.ambientLight = ambientLight;
                RenderSettings.ambientIntensity = ambientIntensity;
                RenderSettings.fog = fog; RenderSettings.fogMode = fogMode; RenderSettings.fogColor = fogColor; RenderSettings.fogDensity = fogDensity;
                foreach (var kv in lights)
                {
                    if (kv.Key == null) continue;
                    kv.Key.transform.rotation = kv.Value.rot; kv.Key.color = kv.Value.color; kv.Key.intensity = kv.Value.intensity;
                    kv.Key.shadows = kv.Value.shadows; kv.Key.shadowStrength = kv.Value.strength;
                }
            }
        }

        public static Material LoadSky(DungeonProfile p)
        {
            return p != null && p.skybox != null ? p.skybox : Resources.Load<Material>(SkyResource);
        }

        /// <summary>Arah bulan di dunia: _MoonTarget (ruang langit) diputar _Rotation persis seperti vertex skybox di shader.</summary>
        public static Vector3 MoonWorldDirection(Material sky)
        {
            Vector3 t = sky.HasProperty("_MoonTarget") ? (Vector3)sky.GetVector("_MoonTarget") : new Vector3(-0.341f, 0.5876f, 0.7338f);
            float rot = sky.HasProperty("_Rotation") ? sky.GetFloat("_Rotation") : 0f;
            float a = rot * Mathf.Deg2Rad, s = Mathf.Sin(a), c = Mathf.Cos(a);
            return new Vector3(c * t.x - s * t.z, t.y, s * t.x + c * t.z).normalized;
        }

        /// <summary>Pasang suasana moonlit. Return snapshot (untuk dikembalikan di Editor), null bila material langit tidak ada.</summary>
        public static Snapshot Apply(DungeonProfile p, IEnumerable<Light> sceneLights, out string report)
        {
            report = "";
            var sky = LoadSky(p);
            if (sky == null || !p.useMoonlitSky) { report = "moonlit sky tidak dipakai (material " + (sky == null ? "tidak ada" : "dimatikan") + ")"; return null; }
            var snap = new Snapshot
            {
                skybox = RenderSettings.skybox, ambientMode = RenderSettings.ambientMode,
                ambientSky = RenderSettings.ambientSkyColor, ambientEquator = RenderSettings.ambientEquatorColor,
                ambientGround = RenderSettings.ambientGroundColor, ambientLight = RenderSettings.ambientLight, ambientIntensity = RenderSettings.ambientIntensity,
                fog = RenderSettings.fog, fogMode = RenderSettings.fogMode, fogColor = RenderSettings.fogColor, fogDensity = RenderSettings.fogDensity,
            };

            RenderSettings.skybox = sky;
            Vector3 moon = MoonWorldDirection(sky);
            int moons = 0;
            Light main = null;
            foreach (var l in sceneLights)
            {
                if (l == null || l.type != LightType.Directional) continue;
                snap.lights.Add(new KeyValuePair<Light, (Quaternion, Color, float, LightShadows, float)>(l, (l.transform.rotation, l.color, l.intensity, l.shadows, l.shadowStrength)));
                if (main == null)
                {
                    main = l;
                    l.transform.rotation = Quaternion.LookRotation(-moon);      // cahaya datang dari arah bulan
                    l.color = p.moonLightColor; l.intensity = p.moonLightIntensity;
                    l.shadows = LightShadows.Soft; l.shadowStrength = p.moonShadowStrength;
                }
                else l.intensity = 0f;                                          // satu sumber cahaya bulan saja
                moons++;
            }
            if (main != null) RenderSettings.sun = main;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSkyColor * p.ambientBoost;
            RenderSettings.ambientEquatorColor = p.ambientEquatorColor * p.ambientBoost;
            RenderSettings.ambientGroundColor = p.ambientGroundColor * p.ambientBoost;
            RenderSettings.fog = p.fogDensity > 0f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = p.fogColor;
            RenderSettings.fogDensity = p.fogDensity;
            DynamicGI.UpdateEnvironment();
            report = "moonlit sky aktif: bulan elevasi " + (Mathf.Asin(moon.y) * Mathf.Rad2Deg).ToString("0") + "°, azimut " +
                     (Mathf.Atan2(moon.x, moon.z) * Mathf.Rad2Deg).ToString("0") + "°, directional " + moons + ", fog " + p.fogDensity.ToString("0.0000");
            return snap;
        }
    }
}
