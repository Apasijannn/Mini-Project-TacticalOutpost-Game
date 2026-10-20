using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Outpost.Player
{
    public enum WeaponKind { Rifle, Pistol }

    /// <summary>
    /// Satu senjata first person (AK-47 atau pistol). Dikelola oleh <see cref="WeaponManager"/>:
    /// hanya senjata yang sedang dipegang yang aktif (GameObject-nya menyala), jadi status amunisi tiap senjata tersimpan.
    /// Kontrol: klik kiri tembak, klik kanan bidik, R reload.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class FpsWeapon : MonoBehaviour
    {
        [Header("Identitas")]
        public WeaponKind kind = WeaponKind.Rifle;
        public string displayName = "AK-47";
        public Sprite icon;
        public bool automatic = true;       // false = semi-auto (satu klik satu peluru)

        [Header("Referensi")]
        public FpsController controller;
        public Transform muzzle;
        public Light muzzleLight;
        public ParticleSystem muzzleFlash;
        public GameObject impactPrefab;
        public GameObject enemyImpactPrefab;
        public LineRenderer tracer;
        public Transform slide;             // bagian slide pistol (opsional), bergerak mundur saat menembak
        public Transform gripRight;         // target tangan kanan (badan player memegang di sini)
        public Transform gripLeft;          // target tangan kiri (kosong = tangan bebas)

        [Header("Senjata")]
        public float damage = 28f;
        public float roundsPerMinute = 600f;
        public int magazineSize = 30;
        public int reserveAmmo = 120;       // cadangan awal
        public int maxReserve = 240;        // batas cadangan (ammo box tidak melewati ini)
        public int ammoPerPickup = 60;      // jumlah cadangan dari satu ammo box
        public float reloadTime = 2.1f;
        public float range = 250f;
        public float hipSpread = 1.1f;      // derajat
        public float adsSpread = 0.25f;
        public LayerMask hitMask = ~0;

        [Header("Suara (SFX) - isi dengan AudioClip di Inspector")]
        public AudioClip shootClip;
        public AudioClip reloadClip;
        public AudioClip emptyClip;            // bunyi "klik" saat peluru habis total
        [Range(0f, 1f)] public float shootVolume = 0.8f;
        [Range(0f, 1f)] public float reloadVolume = 0.9f;
        [Range(0f, 1f)] public float emptyVolume = 0.8f;

        [Header("Posisi view model (relatif kamera)")]
        public Vector3 hipPosition = new Vector3(0.17f, -0.19f, 0.34f);
        public Vector3 hipEuler = Vector3.zero;
        public Vector3 adsPosition = new Vector3(0.0f, -0.115f, 0.28f);
        public Vector3 adsEuler = Vector3.zero;

        [Header("Pose tangan khusus (opsional; dipakai FpsBody bila customHands aktif)")]
        public bool customHands;
        public Vector3 aimR = new Vector3(0f, -0.3f, 1f);       // arah telapak kanan (ruang senjata): pergelangan -> pangkal jari
        public Vector3 aimL = new Vector3(0f, -0.3f, 1f);
        public float rollR, rollL;                               // putaran telapak di sekitar arah itu (derajat)
        public float curlR = 60f, curlL = 70f;                   // lekukan jari
        public Vector3 poleR = new Vector3(0.55f, -0.9f, -0.2f); // arah siku (x keluar, y atas, z depan; relatif badan)
        public Vector3 poleL = new Vector3(0.55f, -0.9f, -0.2f);
        public float adsFov = 0.78f;        // pengali FOV saat bidik (1 = tanpa zoom)
        public float kickBack = 0.035f;
        public float recoilUp = 0.9f;       // derajat per tembakan
        public float recoilSide = 0.25f;
        [Header("Recoil response")]
        public Vector2[] recoilPattern = { new Vector2(0f, 1f), new Vector2(-0.25f, 1.08f), new Vector2(0.35f, 1.15f), new Vector2(0.7f, 1.2f), new Vector2(0.5f, 1.25f), new Vector2(-0.6f, 1.25f), new Vector2(-0.8f, 1.3f), new Vector2(-0.3f, 1.3f) };
        [Range(0f, 1f)] public float recoilRandomness = 0.12f;
        [Min(1f)] public float recoilResponse = 38f;
        [Min(0.1f)] public float recoilRecovery = 7f;
        [Min(0f)] public float recoilRecoveryDelay = 0.08f;
        [Min(0.05f)] public float burstResetDelay = 0.28f;
        [Min(0.1f)] public float maxRecoilPitch = 10f;
        [Min(0.1f)] public float maxRecoilYaw = 3f;
        public float equipTime = 0.32f;
        public float slideTravel = 0.02f;

        int ammo; int initialReserve; bool inited;
        public int Ammo { get { EnsureInit(); return ammo; } }
        public int Reserve { get { EnsureInit(); return reserveAmmo; } }
        public bool NeedsAmmo { get { EnsureInit(); return reserveAmmo < maxReserve; } }
        public bool IsReloading { get; private set; }
        public bool IsAiming { get; private set; }
        public event Action OnHit;          // peluru/tebasan kena target yang bisa dilukai
        public event Action OnFired;
        /// <summary>Suara tembakan (posisi, radius). Didengar musuh.</summary>
        public static event Action<Vector3, float> NoiseEmitted;
        public float noiseRadius = 28f;

        float nextFire, reloadEnd, flashEnd, tracerEnd, equipStart;
        float nextDry;
        AudioSource sfx;
        float slideK; Vector3 slideRest; bool slideCached;
        Vector3 kick; float adsT;
        float recoilP, recoilY;
        Vector2 recoilTarget;
        int burstShot;
        float lastShot = -999f;
        public float RecoilFraction { get { return Mathf.Clamp01(recoilP / Mathf.Max(0.1f, maxRecoilPitch)); } }
        Transform t;

        void EnsureInit()
        {
            if (inited) return;
            inited = true; ammo = magazineSize; initialReserve = reserveAmmo;
        }

        void Awake()
        {
            t = transform;
            EnsureInit();
            if (controller == null) controller = GetComponentInParent<FpsController>();
            if (muzzleLight != null) muzzleLight.enabled = false;
            if (tracer != null) tracer.enabled = false;
            sfx = GetComponent<AudioSource>();
            if (sfx == null) sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false; sfx.loop = false; sfx.spatialBlend = 0f;   // 2D: terdengar sama di kiri/kanan
        }

        void OnEnable()
        {
            if (t == null) t = transform;
            EnsureInit();
            if (controller == null) controller = GetComponentInParent<FpsController>();
            if (slide != null && !slideCached) { slideRest = slide.localPosition; slideCached = true; }
            IsReloading = false; IsAiming = false; adsT = 0f; kick = Vector3.zero; slideK = 0f;
            ResetRecoil();
            equipStart = Time.time; nextFire = Mathf.Max(nextFire, Time.time + equipTime * 0.6f);
            t.localPosition = hipPosition + new Vector3(0f, -0.3f, 0f);
            t.localRotation = Quaternion.Euler(hipEuler) * Quaternion.Euler(40f, 0f, 0f);
            if (muzzleLight != null) muzzleLight.enabled = false;
            if (tracer != null) tracer.enabled = false;
        }

        void OnDisable()
        {
            IsReloading = false; IsAiming = false;
            if (sfx != null) sfx.Stop();                         // suara reload terpotong bila senjata diganti
            if (controller != null) controller.fovMultiplier = 1f;
            ResetRecoil();
            if (muzzleFlash != null) muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void Update()
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            bool active = controller != null && controller.InputEnabled && Cursor.lockState == CursorLockMode.Locked;
            bool equipping = Time.time < equipStart + equipTime;

            bool wantAim = active && !equipping && mouse != null && mouse.rightButton.isPressed && !IsReloading && !controller.IsSprinting;
            IsAiming = wantAim;
            adsT = Mathf.MoveTowards(adsT, IsAiming ? 1f : 0f, 8f * Time.deltaTime);
            if (controller != null) controller.fovMultiplier = Mathf.Lerp(1f, adsFov, adsT);

            if (IsReloading && Time.time >= reloadEnd) FinishReload();
            if (active && !equipping && kb != null && kb.rKey.wasPressedThisFrame) StartReload();

            bool trigger = mouse != null && (automatic ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame);
            if (active && !equipping && trigger && !controller.IsSprinting) TryFire();

            if (Time.time - lastShot > recoilRecoveryDelay)
                recoilTarget *= Mathf.Exp(-recoilRecovery * Time.deltaTime);
            float response = 1f - Mathf.Exp(-recoilResponse * Time.deltaTime);
            recoilP = Mathf.Lerp(recoilP, recoilTarget.y, response);
            recoilY = Mathf.Lerp(recoilY, recoilTarget.x, response);
            if (controller != null) controller.SetRecoil(recoilP, recoilY);

            AnimateViewModel();
            if (muzzleLight != null && muzzleLight.enabled && Time.time > flashEnd) muzzleLight.enabled = false;
            if (tracer != null && tracer.enabled && Time.time > tracerEnd) tracer.enabled = false;
        }

        void LateUpdate()
        {
            if (slide == null || !slideCached) return;
            slideK = Mathf.MoveTowards(slideK, 0f, 9f * Time.deltaTime);
            float hold = (ammo == 0 && !IsReloading) ? 0.9f : 0f;     // slide tertahan di belakang saat magasin kosong
            float k = Mathf.Max(slideK, hold);
            slide.localPosition = slideRest + new Vector3(0f, 0f, -slideTravel * k);
        }

        void AnimateViewModel()
        {
            Vector3 target = Vector3.Lerp(hipPosition, adsPosition, adsT);
            Quaternion rot = Quaternion.Euler(Vector3.Lerp(hipEuler, adsEuler, adsT));
            if (controller != null)
            {
                Vector3 v = controller.HorizontalVelocity;
                float speed = v.magnitude;
                float swayAmp = Mathf.Lerp(1f, 0.15f, adsT);
                float tm = Time.time * (controller.IsSprinting ? 11f : 8f);
                if (speed > 0.2f && controller.IsGrounded)
                {
                    target += new Vector3(Mathf.Cos(tm * 0.5f) * 0.006f, Mathf.Sin(tm) * 0.007f, 0f) * swayAmp;
                    if (controller.IsSprinting) rot = Quaternion.Euler(8f, -14f, -6f) * rot;
                }
                else target += new Vector3(0f, Mathf.Sin(Time.time * 1.4f) * 0.0016f, 0f) * swayAmp;
            }
            if (IsReloading)
            {
                float k = 1f - Mathf.Clamp01((reloadEnd - Time.time) / reloadTime);
                float dip = Mathf.Sin(k * Mathf.PI);
                target += new Vector3(0.02f * dip, -0.16f * dip, 0f);
                rot = Quaternion.Euler(-30f * dip, 0f, 22f * dip) * rot;
            }
            // animasi mengeluarkan senjata (naik dari bawah)
            float e = 1f - Mathf.Clamp01((Time.time - equipStart) / Mathf.Max(0.01f, equipTime));
            e = e * e;
            if (e > 0.001f) { target += new Vector3(0f, -0.3f * e, 0.02f * e); rot = Quaternion.Euler(40f * e, 0f, 0f) * rot; }

            kick *= Mathf.Exp(-14f * Time.deltaTime);
            t.localPosition = Vector3.Lerp(t.localPosition, target + new Vector3(0f, kick.y, -kick.z), 1f - Mathf.Exp(-18f * Time.deltaTime));
            t.localRotation = Quaternion.Slerp(t.localRotation, rot * Quaternion.Euler(-kick.z * 120f, recoilY * 0.2f, -recoilY * 0.35f), 1f - Mathf.Exp(-14f * Time.deltaTime));
        }

        // ---------------- senjata api ----------------
        void TryFire()
        {
            if (IsReloading || Time.time < nextFire) return;
            if (ammo <= 0) { if (reserveAmmo <= 0) DryFire(); else StartReload(); return; }
            nextFire = Time.time + 60f / roundsPerMinute;
            ammo--;
            Fire();
        }

        void Sfx(AudioClip c, float vol, float pitchJitter)
        {
            if (c == null || sfx == null) return;
            sfx.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
            sfx.PlayOneShot(c, vol);
        }

        // klik kosong: peluru di magasin dan cadangan sama-sama habis
        void DryFire()
        {
            if (Time.time < nextDry) return;
            nextDry = Time.time + 0.35f;
            Sfx(emptyClip, emptyVolume, 0f);
        }

        void Fire()
        {
            Camera cam = controller.playerCamera;
            float spread = Mathf.Lerp(hipSpread, adsSpread, adsT);
            if (controller.HorizontalVelocity.magnitude > 0.5f) spread *= 1.6f;
            if (!controller.IsGrounded) spread *= 2.0f;
            if (controller.IsCrouching) spread *= 0.7f;

            Vector3 dir = cam.transform.forward;
            dir = Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), cam.transform.forward) * Quaternion.AngleAxis(UnityEngine.Random.Range(0f, spread), cam.transform.up) * dir;
            Vector3 origin = cam.transform.position;
            Vector3 end = origin + dir * range;
            RaycastHit hit;
            if (Physics.Raycast(origin, dir, out hit, range, hitMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var dmg = hit.collider.GetComponentInParent<IDamageable>();
                if (dmg != null) { dmg.TakeDamage(damage, hit.point, dir); if (OnHit != null) OnHit(); }
                var effect = hit.collider.GetComponentInParent<Outpost.Enemy.EnemyHealth>() != null ? enemyImpactPrefab : impactPrefab;
                if (effect != null)
                {
                    var impact = Instantiate(effect, hit.point + hit.normal * 0.015f, Quaternion.LookRotation(hit.normal));
                    Destroy(impact, 4f);
                }
            }

            float aimK = Mathf.Lerp(1f, 0.55f, adsT);
            if (controller.IsCrouching) aimK *= 0.85f;
            kick += new Vector3(0f, 0.004f, kickBack) * aimK;
            kick.z = Mathf.Min(kick.z, kickBack * 2.5f);
            if (Time.time - lastShot > burstResetDelay) burstShot = 0;
            var step = recoilPattern != null && recoilPattern.Length > 0 ? recoilPattern[burstShot % recoilPattern.Length] : Vector2.up;
            float horizontal = (step.x + UnityEngine.Random.Range(-recoilRandomness, recoilRandomness)) * recoilSide;
            recoilTarget.x = Mathf.Clamp(recoilTarget.x + horizontal * aimK, -maxRecoilYaw, maxRecoilYaw);
            recoilTarget.y = Mathf.Clamp(recoilTarget.y + recoilUp * step.y * aimK, 0f, maxRecoilPitch);
            burstShot++; lastShot = Time.time;
            slideK = 1f;
            Sfx(shootClip, shootVolume, 0.04f);

            if (muzzleFlash != null)
            {
                muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                muzzleFlash.Play(true);
            }
            if (muzzleLight != null) { muzzleLight.enabled = true; flashEnd = Time.time + 0.045f; }
            if (tracer != null && muzzle != null)
            {
                tracer.enabled = true; tracerEnd = Time.time + 0.05f;
                tracer.SetPosition(0, muzzle.position);
                tracer.SetPosition(1, end);
            }
            if (OnFired != null) OnFired();
            if (NoiseEmitted != null) NoiseEmitted(controller.transform.position, noiseRadius);
        }

        public void StartReload()
        {
            if (IsReloading || ammo >= magazineSize || reserveAmmo <= 0) return;
            IsReloading = true; reloadEnd = Time.time + reloadTime;
            Sfx(reloadClip, reloadVolume, 0f);
        }

        void ResetRecoil()
        {
            recoilP = recoilY = 0f; recoilTarget = Vector2.zero; burstShot = 0; lastShot = -999f;
            if (controller != null) controller.SetRecoil(0f, 0f);
        }

        void FinishReload()
        {
            int need = magazineSize - ammo;
            int take = Mathf.Min(need, reserveAmmo);
            ammo += take; reserveAmmo -= take;
            IsReloading = false;
        }

        // ---------------- amunisi ----------------
        /// <summary>Tambah amunisi cadangan; mengembalikan jumlah yang benar-benar masuk.</summary>
        public int AddAmmo(int n)
        {
            EnsureInit();
            if (n <= 0) return 0;
            int before = reserveAmmo;
            reserveAmmo = Mathf.Min(maxReserve, reserveAmmo + n);
            return reserveAmmo - before;
        }

        public void Refill()
        {
            EnsureInit();
            ammo = magazineSize; reserveAmmo = Mathf.Max(reserveAmmo, initialReserve); IsReloading = false;
        }
    }
}
