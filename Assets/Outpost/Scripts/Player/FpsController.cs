using UnityEngine;
using UnityEngine.InputSystem;

namespace Outpost.Player
{
    /// <summary>Gerak first person: WASD, mouse look, sprint (Shift), jongkok (Ctrl/C), lompat (Space), head bob.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class FpsController : MonoBehaviour
    {
        [Header("Referensi")]
        public Transform cameraRoot;        // pivot kepala (yang diputar pitch)
        public Camera playerCamera;

        [Header("Gerak")]
        public float walkSpeed = 4.2f;
        public float sprintSpeed = 6.6f;
        public float crouchSpeed = 2.2f;
        public float acceleration = 14f;
        public float jumpHeight = 1.05f;
        public float gravity = -22f;

        [Header("Suara langkah (isi AudioClip di Inspector)")]
        public AudioClip walkClip;              // dilooping selama berjalan
        public AudioClip runClip;               // dilooping selama sprint
        [Range(0f, 1f)] public float walkVolume = 0.5f;
        [Range(0f, 1f)] public float runVolume = 0.7f;
        [Range(0f, 1f)] public float crouchVolume = 0.25f;

        [Header("Kamera")]
        public float mouseSensitivity = 0.11f;   // derajat per piksel
        public float maxPitch = 85f;
        public float baseFov = 70f;
        public float standEyeHeight = 1.62f;
        public float crouchEyeHeight = 1.05f;
        public float standHeight = 1.8f;
        public float crouchHeight = 1.2f;
        public bool headBob = true;

        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsGrounded { get { return cc != null && cc.isGrounded; } }
        public Vector3 HorizontalVelocity { get { return new Vector3(velocity.x, 0f, velocity.z); } }
        public float Pitch { get { return pitch; } }
        public bool InputEnabled = true;

        /// <summary>Tambahan pitch sementara dari recoil (derajat, positif = kamera naik).</summary>
        [HideInInspector] public float recoilPitch;
        [HideInInspector] public float recoilYaw;
        [HideInInspector] public float fovMultiplier = 1f;   // dipakai ADS

        CharacterController cc;
        AudioSource stepSrc;
        Vector3 velocity;
        float pitch;
        float bobTimer;
        float eyeHeight;
        float fovCurrent;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            if (cameraRoot == null && playerCamera != null) cameraRoot = playerCamera.transform.parent;
            eyeHeight = standEyeHeight;
            fovCurrent = baseFov;
            stepSrc = gameObject.AddComponent<AudioSource>();
            stepSrc.playOnAwake = false; stepSrc.loop = true; stepSrc.spatialBlend = 0f;
        }

        void Start() { LockCursor(true); }

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) LockCursor(false);
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked && InputEnabled) LockCursor(true);

            bool active = InputEnabled && Cursor.lockState == CursorLockMode.Locked;
            Look(active ? mouse : null);
            Move(active ? kb : null);
            UpdateFootsteps(active);
            UpdateCamera();
        }

        void Look(Mouse mouse)
        {
            if (mouse == null) return;
            Vector2 d = mouse.delta.ReadValue() * mouseSensitivity * fovMultiplier;
            transform.Rotate(0f, d.x, 0f, Space.World);
            pitch = Mathf.Clamp(pitch - d.y, -maxPitch, maxPitch);
        }

        void Move(Keyboard kb)
        {
            Vector2 input = Vector2.zero;
            bool wantSprint = false, wantCrouch = false, jump = false;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
                wantSprint = kb.leftShiftKey.isPressed;
                wantCrouch = kb.leftCtrlKey.isPressed || kb.cKey.isPressed;
                jump = kb.spaceKey.wasPressedThisFrame;
            }
            input = Vector2.ClampMagnitude(input, 1f);

            // jongkok (tidak boleh berdiri bila ada langit-langit)
            if (wantCrouch) IsCrouching = true;
            else if (IsCrouching && CanStand()) IsCrouching = false;

            IsSprinting = wantSprint && !IsCrouching && input.y > 0.1f;
            float target = IsCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);

            Vector3 wish = (transform.right * input.x + transform.forward * input.y) * target;
            Vector3 h = Vector3.MoveTowards(new Vector3(velocity.x, 0, velocity.z), wish, acceleration * Time.deltaTime);
            velocity.x = h.x; velocity.z = h.z;

            if (cc.isGrounded && velocity.y < 0f) velocity.y = -2f;
            if (jump && cc.isGrounded && !IsCrouching) velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            velocity.y += gravity * Time.deltaTime;

            float targetH = IsCrouching ? crouchHeight : standHeight;
            cc.height = Mathf.MoveTowards(cc.height, targetH, 6f * Time.deltaTime);
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);

            cc.Move(velocity * Time.deltaTime);
        }

        // suara langkah: loop jalan / lari selama player bergerak di tanah; berhenti saat diam / di udara / mati
        void UpdateFootsteps(bool active)
        {
            if (stepSrc == null) return;
            bool moving = active && cc.isGrounded && HorizontalVelocity.magnitude > 0.5f;
            AudioClip want = !moving ? null : (IsSprinting ? runClip : walkClip);
            if (want == null) { if (stepSrc.isPlaying) stepSrc.Stop(); return; }
            if (stepSrc.clip != want) { stepSrc.clip = want; stepSrc.Play(); }
            else if (!stepSrc.isPlaying) stepSrc.Play();
            stepSrc.volume = IsCrouching ? crouchVolume : (IsSprinting ? runVolume : walkVolume);
            stepSrc.pitch = IsCrouching ? 0.85f : 1f;
        }

        bool CanStand()
        {
            Vector3 p = transform.position;
            float r = cc.radius * 0.95f;
            return !Physics.CheckCapsule(p + Vector3.up * (crouchHeight - r + 0.05f), p + Vector3.up * (standHeight - r), r, ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore);
        }

        void UpdateCamera()
        {
            if (cameraRoot == null) return;
            float targetEye = IsCrouching ? crouchEyeHeight : standEyeHeight;
            eyeHeight = Mathf.Lerp(eyeHeight, targetEye, 10f * Time.deltaTime);

            Vector3 bob = Vector3.zero;
            float speed = HorizontalVelocity.magnitude;
            if (headBob && cc.isGrounded && speed > 0.2f)
            {
                bobTimer += Time.deltaTime * (IsSprinting ? 11f : IsCrouching ? 6f : 8f);
                float amp = IsSprinting ? 0.045f : 0.025f;
                bob = new Vector3(Mathf.Cos(bobTimer * 0.5f) * amp * 0.6f, Mathf.Sin(bobTimer) * amp, 0f);
            }
            else bobTimer = 0f;

            cameraRoot.localPosition = new Vector3(0f, eyeHeight, 0f) + bob;
            cameraRoot.localRotation = Quaternion.Euler(Mathf.Clamp(pitch - recoilPitch, -89f, 89f), recoilYaw, 0f);

            if (playerCamera != null)
            {
                float f = baseFov * fovMultiplier * (IsSprinting ? 1.06f : 1f);
                fovCurrent = Mathf.Lerp(fovCurrent, f, 10f * Time.deltaTime);
                playerCamera.fieldOfView = fovCurrent;
            }
        }

        /// <summary>Pindahkan player ke posisi/arah tertentu (dipakai respawn).</summary>
        public void Teleport(Vector3 pos, float yaw)
        {
            cc.enabled = false;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            velocity = Vector3.zero; pitch = 0f;
            cc.enabled = true;
        }
    }
}
