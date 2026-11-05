using UnityEngine;
using Outpost.Enemy;

namespace Outpost.Audio
{
    /// <summary>
    /// Suara satu zombie (3D, makin jauh makin pelan). Ditambahkan otomatis oleh <see cref="OutpostAudio"/>.
    /// Zombie biasa: erangan acak, erangan kecil saat kena tembak, gigit saat menyerang, erangan berat saat mati.
    /// Boss: raungan saat muncul / marah (enrage), erangan berat berkala, gigitan keras, raungan saat mati.
    /// </summary>
    public class EnemyAudio : MonoBehaviour
    {
        EnemyHealth health;
        EnemyBrain brain;
        AudioSource src;
        bool boss;
        EnemyBrain.State lastState;
        float nextGroan, nextPain, biteAt = -1f;
        static float lastGroanTime = -10f;     // supaya puluhan zombie tidak mengerang serentak

        void Awake()
        {
            health = GetComponent<EnemyHealth>();
            brain = GetComponent<EnemyBrain>();
            boss = health != null && health.config != null && health.config.kind == EnemyKind.Boss;
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false; src.loop = false;
            src.spatialBlend = 1f; src.rolloffMode = AudioRolloffMode.Linear; src.dopplerLevel = 0f;
            var a = OutpostAudio.Instance;
            src.minDistance = boss ? 6f : 2.5f;
            src.maxDistance = a != null ? (boss ? a.bossHearDistance : a.hearDistance) : (boss ? 70f : 38f);
            lastState = EnemyBrain.State.Dead;     // agar state awal (mis. Spawning pada boss) terdeteksi sebagai perubahan
            ScheduleGroan(true);
        }

        void OnEnable()
        {
            if (health != null) { health.OnDamaged += OnDamaged; health.OnDied += OnDied; }
        }

        void OnDisable()
        {
            if (health != null) { health.OnDamaged -= OnDamaged; health.OnDied -= OnDied; }
        }

        void ScheduleGroan(bool first)
        {
            var a = OutpostAudio.Instance;
            float mn = a != null ? a.groanIntervalMin : 6f, mx = a != null ? a.groanIntervalMax : 14f;
            if (boss) { mn *= 1.4f; mx *= 1.4f; }
            nextGroan = Time.time + (first ? Random.Range(0.5f, mx * 0.6f) : Random.Range(mn, mx));
        }

        AudioClip BaseClip()
        {
            var a = OutpostAudio.Instance; if (a == null) return null;
            return boss ? a.bossRoar : a.zombieGroan;
        }

        float BaseVolume()
        {
            var a = OutpostAudio.Instance; if (a == null) return 0f;
            return boss ? a.bossVolume : a.groanVolume;
        }

        void Say(AudioClip c, float vol, float pitch)
        {
            if (c == null || src == null) return;
            src.pitch = pitch;
            src.PlayOneShot(c, vol);
        }

        void Update()
        {
            if (health == null || health.IsDead || brain == null) return;
            var a = OutpostAudio.Instance; if (a == null) return;

            var st = brain.CurrentState;
            if (st != lastState)
            {
                if ((st == EnemyBrain.State.AttackPlayer || st == EnemyBrain.State.AttackGenerator) && brain.config != null)
                    biteAt = Time.time + brain.config.attackHitDelay;      // gigitan terdengar tepat saat serangan mengenai
                if (boss && st == EnemyBrain.State.Spawning)
                    Say(a.bossRoar, a.bossVolume, 1f);                       // raungan saat muncul & saat enrage
                lastState = st;
            }
            if (biteAt > 0f && Time.time >= biteAt)
            {
                biteAt = -1f;
                Say(a.zombieBite, a.biteVolume * (boss ? 1f : 0.9f), boss ? 0.78f : Random.Range(0.94f, 1.06f));
            }
            if (Time.time >= nextGroan)
            {
                ScheduleGroan(false);
                if (Time.time - lastGroanTime > 0.5f && st != EnemyBrain.State.Spawning)
                {
                    lastGroanTime = Time.time;
                    Say(BaseClip(), BaseVolume() * (boss ? 0.8f : 1f), boss ? Random.Range(0.85f, 0.95f) : Random.Range(0.9f, 1.1f));
                }
            }
        }

        void OnDamaged(float dmg, Vector3 dir)
        {
            if (health.IsDead || boss || Time.time < nextPain) return;      // boss tidak "mengerang" tiap kena tembak
            nextPain = Time.time + 1.4f;
            var a = OutpostAudio.Instance; if (a == null) return;
            Say(a.zombieGroan, a.groanVolume * 0.8f, Random.Range(1.1f, 1.25f));
        }

        void OnDied(EnemyHealth h)
        {
            var a = OutpostAudio.Instance; if (a == null) return;
            if (boss) Say(a.bossRoar, a.bossVolume, 0.8f);
            else Say(a.zombieGroan, a.groanVolume, Random.Range(0.72f, 0.85f));
        }
    }
}
