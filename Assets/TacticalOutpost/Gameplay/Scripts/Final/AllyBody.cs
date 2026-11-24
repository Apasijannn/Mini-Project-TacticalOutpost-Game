using System.Collections.Generic;
using UnityEngine;
using Outpost.Player;

namespace Outpost.Final
{
    /// <summary>
    /// Visual NPC sekutu dari aset yang sudah ada: clone model Insurgent milik player (FpsBody.body) dan view model AK-47 player.
    /// Memakai solver IK anatomi yang sama dengan FpsBody (InsurgentRig): blading bahu, siku keluar alami,
    /// twist lengan bawah (ElbowPart1), telapak 3D menempel grip/handguard, jari & ibu jari, serta ayunan kaki.
    /// </summary>
    [DefaultExecutionOrder(600)]
    public sealed class AllyBody : MonoBehaviour
    {
        public Transform body, aim, gripRight, gripLeft, muzzle;
        public ParticleSystem muzzleFlash;
        public Light muzzleLight;
        public LineRenderer tracer;
        public float speed;            // diisi AllyBrain
        public bool down;              // tumbang

        readonly InsurgentRig rig = new InsurgentRig();
        float phase, amp, flashEnd, tracerEnd;
        bool ready;

        /// <summary>Bangun visual dari player. Mengembalikan false bila model player tidak ditemukan.</summary>
        public bool Build(PlayerHealth player, Color armband)
        {
            var src = player != null ? player.GetComponentInChildren<FpsBody>(true) : null;
            var manager = player != null ? player.GetComponentInChildren<WeaponManager>(true) : null;
            FpsWeapon rifle = null;
            if (manager != null && manager.weapons != null)
                foreach (var w in manager.weapons) if (w != null && w.kind == WeaponKind.Rifle) { rifle = w; break; }
            if (src == null || src.body == null || rifle == null) return false;

            // Kembalikan badan player ke rest pose sesaat sebelum clone agar AllyBody menangkap rest pose murni.
            src.ResetToRestPose();
            // clone di bawah holder non-aktif supaya Awake/OnEnable script player tidak jalan
            var holder = new GameObject("clone holder"); holder.SetActive(false);
            body = Instantiate(src.body.gameObject, holder.transform).transform;
            var gun = Instantiate(rifle.gameObject, holder.transform).transform;
            src.Pose(0f);
            Strip(body.gameObject); Strip(gun.gameObject);

            // pastikan skala lengan clone bersih (1.0) kalau FpsBody sumber sempat ter-scale
            foreach (var s in new[] { "L", "R" })
            {
                var sh = InsurgentRig.FindDeep(body, "Shoulder_" + s);
                var wr = InsurgentRig.FindDeep(body, "Wrist_" + s);
                if (sh != null) sh.localScale = Vector3.one;
                if (wr != null) wr.localScale = Vector3.one;
            }

            // geometri sama dengan player: kamera di eyeHeight, badan di bodyOffset, senjata di posisi hip relatif kamera
            aim = new GameObject("Aim").transform;
            aim.SetParent(transform, false); aim.localPosition = new Vector3(0f, src.eyeHeight, -src.bodyOffset.z);
            body.SetParent(transform, false); body.localPosition = Vector3.zero; body.localRotation = Quaternion.identity;
            gun.SetParent(aim, false); gun.localPosition = rifle.hipPosition; gun.localRotation = Quaternion.Euler(rifle.hipEuler);
            body.gameObject.SetActive(true);
            gun.gameObject.SetActive(true);

            gripRight = Map(rifle.transform, rifle.gripRight, gun);
            gripLeft = Map(rifle.transform, rifle.gripLeft, gun);
            muzzle = Map(rifle.transform, rifle.muzzle, gun) ?? gun;
            muzzleFlash = rifle.muzzleFlash != null ? Map(rifle.transform, rifle.muzzleFlash.transform, gun)?.GetComponent<ParticleSystem>() : null;
            muzzleLight = rifle.muzzleLight != null ? Map(rifle.transform, rifle.muzzleLight.transform, gun)?.GetComponent<Light>() : null;
            tracer = rifle.tracer != null ? Map(rifle.transform, rifle.tracer.transform, gun)?.GetComponent<LineRenderer>() : null;
            if (tracer != null) { tracer.useWorldSpace = true; tracer.enabled = false; }
            if (muzzleLight != null) muzzleLight.enabled = false;
            if (Application.isPlaying) Destroy(holder); else DestroyImmediate(holder);

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true;
                r.gameObject.layer = 0;
            }
            // pita lengan berwarna supaya sekutu mudah dibedakan dari zombie
            var mpb = new MaterialPropertyBlock();
            foreach (var r in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.GetPropertyBlock(mpb); mpb.SetColor("_BaseColor", Color.Lerp(Color.white, armband, 0.25f)); r.SetPropertyBlock(mpb);
            }

            ready = rig.Init(transform, body, 1.0f);
            return ready;
        }

        static void Strip(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Animator>(true)) DestroyImmediate(c);
        }

        static Transform Map(Transform srcRoot, Transform srcChild, Transform dstRoot)
        {
            if (srcChild == null) return null;
            if (srcChild == srcRoot) return dstRoot;
            var path = new List<string>();
            for (var t = srcChild; t != null && t != srcRoot; t = t.parent) path.Insert(0, t.name);
            return dstRoot.Find(string.Join("/", path.ToArray()));
        }

        /// <summary>Arahkan senjata ke titik dunia (pitch dibatasi).</summary>
        public void AimAt(Vector3 point)
        {
            if (aim == null) return;
            Vector3 d = point - aim.position;
            Vector3 local = transform.InverseTransformDirection(d);
            float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -35f, 35f);
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -25f, 25f);
            aim.localRotation = Quaternion.Slerp(aim.localRotation, Quaternion.Euler(pitch, yaw, 0f), 0.35f);
        }

        public void RelaxAim() { if (aim != null) aim.localRotation = Quaternion.Slerp(aim.localRotation, Quaternion.Euler(12f, 0f, 0f), 0.1f); }

        public void Flash(Vector3 end)
        {
            if (muzzleFlash != null) muzzleFlash.Play(true);
            if (muzzleLight != null) { muzzleLight.enabled = true; flashEnd = Time.time + 0.05f; }
            if (tracer != null && muzzle != null) { tracer.enabled = true; tracer.SetPosition(0, muzzle.position); tracer.SetPosition(1, end); tracerEnd = Time.time + 0.05f; }
        }

        /// <summary>Hitung ulang pose secara sinkron (dipakai LateUpdate dan pratinjau Editor).</summary>
        public void PoseNow(float dt)
        {
            if (!ready) return;
            amp = dt > 0f ? Mathf.MoveTowards(amp, Mathf.Clamp01(speed / 4.2f), dt * 7f) : 0f;
            phase += speed * dt * 1.55f; if (phase > 628f) phase -= 628f;
            float idleWeight = Mathf.Clamp01(1f - amp * 1.4f);

            float blend = dt > 0f ? Mathf.Clamp01(dt * 6f) : 1f;
            body.localRotation = Quaternion.Slerp(body.localRotation, down ? Quaternion.Euler(-80f, 0f, 0f) : Quaternion.identity, blend);
            Vector3 standPos = new Vector3(0f, -0.052f * idleWeight, 0f);
            body.localPosition = Vector3.Lerp(body.localPosition, down ? new Vector3(0f, 0.25f, 0f) : standPos, blend);

            rig.ResetUpperBody();
            if (down) return;

            float aimPitch = aim != null ? Mathf.Asin(Mathf.Clamp(-aim.forward.y, -1f, 1f)) * Mathf.Rad2Deg : 0f;
            rig.ApplyTorsoStance(22f, aimPitch * 0.18f + 16f, 0f, 0f);
            Vector3 headDelta = rig.HeadLeanDeltaInBody();
            body.localPosition += new Vector3(0f, -headDelta.y * 0.5f, -headDelta.z);
            rig.SolveRightArm(gripRight, HandGripPose.RifleRight(), aim != null ? aim.TransformPoint(new Vector3(0.2f, -0.28f, 0.34f)) : transform.position + transform.forward * 0.4f + Vector3.up * 1.3f);
            rig.SolveLeftArm(gripLeft, HandGripPose.RifleLeft(), aim != null ? aim.TransformPoint(new Vector3(0.05f, -0.2f, 0.55f)) : transform.position + transform.forward * 0.55f + Vector3.up * 1.3f);

            rig.PoseLegs(phase, amp, 0f, false, true);
        }

        void LateUpdate()
        {
            if (muzzleLight != null && muzzleLight.enabled && Time.time > flashEnd) muzzleLight.enabled = false;
            if (tracer != null && tracer.enabled && Time.time > tracerEnd) tracer.enabled = false;
            PoseNow(Time.deltaTime);
        }

        /// <summary>Jarak bahu → grip dibanding panjang lengan (untuk cek di log).</summary>
        public string ReachReport() { return rig.ReachReport(gripLeft, gripRight); }
    }
}
