using System.Collections.Generic;
using UnityEngine;
using Outpost.Player;

namespace Outpost.Final
{
    /// <summary>
    /// Visual NPC sekutu dari aset yang sudah ada: clone model Insurgent milik player (FpsBody.body) dan view model AK-47 player.
    /// Model Insurgent tidak punya klip animasi, jadi pose dihitung lewat kode (IK dua-tulang), sama seperti FpsBody:
    /// tangan ke grip senjata, kaki berayun sesuai kecepatan agent.
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

        class Limb { public Transform a, b, c, tip; public Quaternion ra, rb, rc; public float la, lb, side; public float ankleH, fwdOff; }
        readonly Limb armL = new Limb(), armR = new Limb(), legL = new Limb(), legR = new Limb();
        readonly List<Transform> fingersL = new List<Transform>(), fingersR = new List<Transform>();
        readonly List<Quaternion> restL = new List<Quaternion>(), restR = new List<Quaternion>();
        // pose tangan diambil dari senjata/badan player supaya pegangan sama persis dengan view model
        Vector3 aimR = new Vector3(0f, -0.3f, 1f), aimL = new Vector3(0f, -0.3f, 1f);
        Vector3 poleR = new Vector3(0.55f, -0.9f, -0.2f), poleL = new Vector3(0.55f, -0.9f, -0.2f);
        float rollR, rollL, curlR = 60f, curlL = 70f;
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

            // clone di bawah holder non-aktif supaya Awake/OnEnable script player tidak jalan
            var holder = new GameObject("clone holder"); holder.SetActive(false);
            body = Instantiate(src.body.gameObject, holder.transform).transform;
            var gun = Instantiate(rifle.gameObject, holder.transform).transform;
            Strip(body.gameObject); Strip(gun.gameObject);

            // geometri sama dengan player: kamera di eyeHeight, badan di bodyOffset, senjata di posisi hip relatif kamera
            aim = new GameObject("Aim").transform;
            aim.SetParent(transform, false); aim.localPosition = new Vector3(0f, src.eyeHeight, -src.bodyOffset.z);
            body.SetParent(transform, false); body.localPosition = Vector3.zero; body.localRotation = Quaternion.identity;
            gun.SetParent(aim, false); gun.localPosition = rifle.hipPosition; gun.localRotation = Quaternion.Euler(rifle.hipEuler);
            if (rifle.customHands)
            {
                aimR = rifle.aimR; aimL = rifle.aimL; rollR = rifle.rollR; rollL = rifle.rollL;
                curlR = rifle.curlR; curlL = rifle.curlL; poleR = rifle.poleR; poleL = rifle.poleL;
            }
            else
            {
                aimR = aimL = src.handAimLocal; rollR = src.rollRight; rollL = src.rollLeft;
                curlR = src.curlRight; curlL = src.curlLeft; poleR = poleL = src.poleOut;
            }
            gripRight = Map(rifle.transform, rifle.gripRight, gun);
            gripLeft = Map(rifle.transform, rifle.gripLeft, gun);
            muzzle = Map(rifle.transform, rifle.muzzle, gun) ?? gun;
            muzzleFlash = rifle.muzzleFlash != null ? Map(rifle.transform, rifle.muzzleFlash.transform, gun)?.GetComponent<ParticleSystem>() : null;
            muzzleLight = rifle.muzzleLight != null ? Map(rifle.transform, rifle.muzzleLight.transform, gun)?.GetComponent<Light>() : null;
            tracer = rifle.tracer != null ? Map(rifle.transform, rifle.tracer.transform, gun)?.GetComponent<LineRenderer>() : null;
            if (tracer != null) { tracer.useWorldSpace = true; tracer.enabled = false; }
            if (muzzleLight != null) muzzleLight.enabled = false;
            Destroy(holder);

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
            Setup();
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

        static Transform Deep(Transform t, string n)
        {
            if (t.name == n) return t;
            foreach (Transform c in t) { var r = Deep(c, n); if (r != null) return r; }
            return null;
        }

        void Setup()
        {
            Chain(armL, "Shoulder_L", "Elbow_L", "Wrist_L", "MiddleFinger1_L", +1f);
            Chain(armR, "Shoulder_R", "Elbow_R", "Wrist_R", "MiddleFinger1_R", -1f);
            Chain(legL, "Hip_L", "Knee_L", "Ankle_L", "Toes_L", +1f);
            Chain(legR, "Hip_R", "Knee_R", "Ankle_R", "Toes_R", -1f);
            foreach (var s in new[] { "L", "R" })
                foreach (var f in new[] { "IndexFinger", "MiddleFinger", "RingFinger", "PinkyFinger" })
                    for (int i = 1; i <= 3; i++)
                    {
                        var t = Deep(body, f + i + "_" + s); if (t == null) continue;
                        (s == "L" ? fingersL : fingersR).Add(t); (s == "L" ? restL : restR).Add(t.localRotation);
                    }
            foreach (var l in new[] { legL, legR })
                if (l.a != null)
                {
                    l.ankleH = Mathf.Max(0.02f, Vector3.Dot(l.c.position - transform.position, Vector3.up));
                    l.fwdOff = Vector3.Dot(l.c.position - l.a.position, transform.forward);
                }
            ready = armL.a != null && armR.a != null;
        }

        void Chain(Limb l, string a, string b, string c, string tip, float side)
        {
            l.a = Deep(body, a); l.b = Deep(body, b); l.c = Deep(body, c); l.tip = Deep(body, tip); l.side = side;
            if (l.a == null || l.b == null || l.c == null) { l.a = null; return; }
            l.ra = l.a.localRotation; l.rb = l.b.localRotation; l.rc = l.c.localRotation;
            l.la = (l.b.position - l.a.position).magnitude; l.lb = (l.c.position - l.b.position).magnitude;
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

        void LateUpdate()
        {
            if (muzzleLight != null && muzzleLight.enabled && Time.time > flashEnd) muzzleLight.enabled = false;
            if (tracer != null && tracer.enabled && Time.time > tracerEnd) tracer.enabled = false;
            if (!ready) return;
            body.localRotation = Quaternion.Slerp(body.localRotation, down ? Quaternion.Euler(-80f, 0f, 0f) : Quaternion.identity, Time.deltaTime * 6f);
            body.localPosition = Vector3.Lerp(body.localPosition, down ? new Vector3(0f, 0.25f, 0f) : Vector3.zero, Time.deltaTime * 6f);
            foreach (var l in new[] { armL, armR }) { l.a.localRotation = l.ra; l.b.localRotation = l.rb; l.c.localRotation = l.rc; }
            if (down) { Curl(fingersL, restL, curlL * 0.5f); Curl(fingersR, restR, curlR * 0.5f); return; }
            Arm(armR, gripRight, aim.TransformPoint(new Vector3(0.2f, -0.28f, 0.34f)), aimR, rollR, poleR);
            Arm(armL, gripLeft, aim.TransformPoint(new Vector3(0.05f, -0.2f, 0.55f)), aimL, rollL, poleL);
            Curl(fingersR, restR, curlR); Curl(fingersL, restL, curlL);
            Legs();
        }

        static void Curl(List<Transform> f, List<Quaternion> rest, float curl)
        {
            for (int i = 0; i < f.Count; i++) f[i].localRotation = rest[i] * Quaternion.Euler(0f, 0f, curl);
        }

        /// <summary>Sama dengan FpsBody.ArmTo: IK ke grip, telapak diarahkan dengan arah grip senjata, lalu diputar (roll).</summary>
        void Arm(Limb l, Transform grip, Vector3 fallback, Vector3 handAim, float roll, Vector3 poleV)
        {
            Vector3 outward = transform.right * l.side;
            Vector3 pole = outward * poleV.x + Vector3.up * poleV.y + transform.forward * poleV.z;
            Vector3 target = grip != null ? grip.position : fallback;
            Vector3 aimDir = grip != null ? grip.TransformDirection(handAim.normalized) : aim.forward;
            TwoBone(l, target, pole);
            if (l.tip == null || aimDir.sqrMagnitude < 0.01f) return;
            AimBone(l.c, l.tip, aimDir);
            if (Mathf.Abs(roll) > 0.01f) l.c.rotation = Quaternion.AngleAxis(roll * l.side, aimDir) * l.c.rotation;
        }

        /// <summary>Jarak bahu → grip dibanding panjang lengan (untuk cek di log).</summary>
        public string ReachReport()
        {
            if (!ready) return "not ready";
            string r = "";
            foreach (var pair in new[] { new KeyValuePair<Limb, Transform>(armL, gripLeft), new KeyValuePair<Limb, Transform>(armR, gripRight) })
                r += (pair.Key == armL ? "L" : "R") + " reach " + (pair.Key.la + pair.Key.lb).ToString("F2")
                    + " need " + (pair.Value != null ? (pair.Value.position - pair.Key.a.position).magnitude.ToString("F2") : "-")
                    + " miss " + (pair.Value != null ? (pair.Value.position - pair.Key.c.position).magnitude.ToString("F3") : "-") + "  ";
            return r;
        }

        void Legs()
        {
            if (legL.a == null || legR.a == null) return;
            foreach (var l in new[] { legL, legR }) { l.a.localRotation = l.ra; l.b.localRotation = l.rb; l.c.localRotation = l.rc; }
            amp = Mathf.MoveTowards(amp, Mathf.Clamp01(speed / 4.2f), Time.deltaTime * 7f);
            phase += speed * Time.deltaTime * 1.55f; if (phase > 628f) phase -= 628f;
            Vector3 fwd = transform.forward, right = transform.right;
            foreach (var l in new[] { legL, legR })
            {
                Vector3 rel = l.a.position - transform.position;
                float lat = Vector3.Dot(rel, right), frw = Vector3.Dot(rel, fwd);
                float ph = phase + (l.side > 0f ? 0f : Mathf.PI);
                float step = Mathf.Sin(ph) * 0.4f * amp, lift = Mathf.Max(0f, Mathf.Cos(ph)) * 0.15f * amp;
                Vector3 target = transform.position + right * lat + fwd * (frw + l.fwdOff + step) + Vector3.up * (l.ankleH + lift);
                TwoBone(l, target, fwd + right * (lat > 0f ? 0.18f : -0.18f));
            }
        }

        static void AimBone(Transform bone, Transform child, Vector3 dir)
        {
            if (bone == null || child == null || dir.sqrMagnitude < 1e-5f) return;
            bone.rotation = Quaternion.FromToRotation((child.position - bone.position).normalized, dir.normalized) * bone.rotation;
        }

        void TwoBone(Limb l, Vector3 target, Vector3 poleDir)
        {
            Vector3 s = l.a.position, d = target - s;
            float dist = Mathf.Clamp(d.magnitude, Mathf.Abs(l.la - l.lb) + 0.02f, l.la + l.lb - 0.004f);
            Vector3 dir = d.sqrMagnitude > 1e-6f ? d.normalized : transform.forward;
            float x = (dist * dist + l.la * l.la - l.lb * l.lb) / (2f * dist);
            float h = Mathf.Sqrt(Mathf.Max(0f, l.la * l.la - x * x));
            Vector3 perp = poleDir - Vector3.Dot(poleDir, dir) * dir;
            if (perp.sqrMagnitude < 1e-5f) perp = Vector3.down;
            perp.Normalize();
            Vector3 e = s + dir * x + perp * h;
            AimBone(l.a, l.b, e - s);
            AimBone(l.b, l.c, s + dir * dist - e);
        }
    }
}
