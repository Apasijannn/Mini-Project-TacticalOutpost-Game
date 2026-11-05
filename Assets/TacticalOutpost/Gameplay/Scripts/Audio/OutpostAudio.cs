using UnityEngine;
using Outpost.Enemy;
using Outpost.Player;

namespace Outpost.Audio
{
    /// <summary>
    /// Pusat SFX game (satu objek "AudioManager" di scene). Semua AudioClip diisi di Inspector, jadi mengganti suara
    /// cukup drag-drop file baru. Suara senjata ada di komponen FpsWeapon, suara langkah di FpsController
    /// (karena beda per senjata / per gerakan). Suara zombie dipasang otomatis lewat <see cref="EnemyAudio"/>.
    /// </summary>
    public class OutpostAudio : MonoBehaviour
    {
        public static OutpostAudio Instance { get; private set; }

        [Header("Player")]
        public AudioClip switchWeapon;
        public AudioClip playerDead;
        public AudioClip pickAmmo;
        public AudioClip pickHealth;
        [Range(0f, 1f)] public float switchVolume = 0.8f;
        [Range(0f, 1f)] public float deadVolume = 1f;
        [Range(0f, 1f)] public float pickVolume = 0.9f;

        [Header("Zombie biasa")]
        public AudioClip zombieGroan;           // suara dasar zombie (erangan, kena tembak, mati)
        public AudioClip zombieBite;            // suara saat zombie menyerang
        [Range(0f, 1f)] public float groanVolume = 0.55f;
        [Range(0f, 1f)] public float biteVolume = 0.85f;
        public float groanIntervalMin = 6f;     // detik antar erangan acak
        public float groanIntervalMax = 14f;
        public float hearDistance = 38f;        // jarak maksimum zombie masih terdengar

        [Header("Boss")]
        public AudioClip bossRoar;              // raungan boss (muncul, erangan, mati)
        [Range(0f, 1f)] public float bossVolume = 1f;
        public float bossHearDistance = 70f;

        AudioSource ui;
        AudioSource radio;
        AudioClip radioTick;
        float lastRadioTick = -1f;
        PlayerHealth hooked;

        void Awake()
        {
            Instance = this;
            ui = gameObject.AddComponent<AudioSource>();
            ui.playOnAwake = false; ui.spatialBlend = 0f;
            radio = gameObject.AddComponent<AudioSource>(); radio.playOnAwake = false; radio.spatialBlend = 0f;
            radioTick = AudioClip.Create("Terminal input", 400, 1, 22050, false);
            var samples = new float[400];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / 22050f, envelope = Mathf.Sin(Mathf.PI * i / samples.Length);
                samples[i] = (Mathf.Sin(t * Mathf.PI * 2 * 1350) + .25f * Mathf.Sin(t * Mathf.PI * 2 * 2100)) * envelope * .18f;
            }
            radioTick.SetData(samples, 0);
        }

        void OnEnable() { EnemyHealth.AnySpawned += OnEnemySpawned; }
        void OnDisable() { EnemyHealth.AnySpawned -= OnEnemySpawned; Unhook(); }
        void OnDestroy() { if (Instance == this) Instance = null; if (radioTick != null) Destroy(radioTick); }

        void Update()
        {
            // PlayerHealth baru ada setelah Awake player, jadi pasang pendengar di sini sekali saja
            if (hooked == null && PlayerHealth.Instance != null)
            {
                hooked = PlayerHealth.Instance;
                hooked.OnDied += OnPlayerDied;
            }
        }

        void Unhook() { if (hooked != null) { hooked.OnDied -= OnPlayerDied; hooked = null; } }

        void OnPlayerDied() { Play2D(playerDead, deadVolume); }

        // setiap zombie yang muncul otomatis diberi komponen suara 3D
        void OnEnemySpawned(EnemyHealth e)
        {
            if (e == null || e.GetComponent<EnemyAudio>() != null) return;
            e.gameObject.AddComponent<EnemyAudio>();
        }

        public void Play2D(AudioClip c, float vol)
        {
            if (c == null || ui == null) return;
            ui.PlayOneShot(c, vol);
        }

        // jalan pintas statis (aman dipanggil walau AudioManager tidak ada di scene)
        public static void Switch() { if (Instance != null) Instance.Play2D(Instance.switchWeapon, Instance.switchVolume); }
        public static void PickAmmo() { if (Instance != null) Instance.Play2D(Instance.pickAmmo, Instance.pickVolume); }
        public static void PickHealth() { if (Instance != null) Instance.Play2D(Instance.pickHealth, Instance.pickVolume); }
        public static void RadioTick()
        {
            var audio = Instance;
            if (audio == null || Time.unscaledTime - audio.lastRadioTick < .055f || AudioListener.pause) return;
            audio.lastRadioTick = Time.unscaledTime; audio.radio.PlayOneShot(audio.radioTick, .25f);
        }
    }
}
