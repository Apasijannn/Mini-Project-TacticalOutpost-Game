using System.Collections.Generic;
using UnityEngine;

namespace Outpost.Player
{
    /// <summary>
    /// Badan player (model Insurgent) terlihat dari kamera orang pertama: lengan memegang senjata lewat IK dua-tulang
    /// (grip kanan/kiri tiap senjata), jari menggenggam, dan kaki berayun saat berjalan.
    /// Model Insurgent tidak punya klip animasi, jadi geraknya dihitung lewat kode.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class FpsBody : MonoBehaviour
    {
        public Transform body;                  // instance model Insurgent (anak dari root player)
        public Transform cameraTransform;
        public WeaponManager manager;
        public FpsController controller;
        public Transform cameraRoot;

        [Header("Penempatan badan (relatif root player)")]
        public Vector3 bodyOffset = new Vector3(0f, 0f, -0.12f);
        public float eyeHeight = 1.62f;

        [Header("Tangan")]
        public float armScale = 1.25f;          // lengan sedikit lebih panjang agar bisa menjangkau handguard; telapak diskalakan balik
        public Vector3 handAimLocal = new Vector3(0f, -0.3f, 1f);   // arah telapak (di ruang grip)
        public float rollRight = 0f;
        public float rollLeft = 0f;
        public float curlRight = 60f;
        public float curlLeft = 70f;
        public Vector3 poleOut = new Vector3(0.55f, -0.9f, -0.2f);  // petunjuk arah siku (x = keluar, y = bawah, z = depan)

        [Header("Kaki")]
        public float stride = 26f;

        class Arm
        {
            public Transform sh, el, wr, aim;
            public List<Transform> fingers = new List<Transform>();
            public List<Quaternion> fingerRest = new List<Quaternion>();
            public Quaternion shRest, elRest, wrRest;
            public float a, b, side;
        }
        class Leg
        {
            public Transform hip, knee, ankle, toes;
            public Quaternion hipRest, kneeRest, ankleRest;
            public float a, b, side, ankleH, fwdOff;
        }

        Arm L = new Arm(), R = new Arm();
        Leg legL = new Leg(), legR = new Leg();
        float phase, ampS, airS;
        bool ready;
        // dipakai builder untuk pratinjau di Edit Mode (-1 = otomatis dari controller)
        [System.NonSerialized] public float previewAmp = -1f, previewPhase = -1f, previewAir = -1f;

        static Transform FindDeep(Transform t, string n)
        {
            if (t.name == n) return t;
            foreach (Transform c in t) { var r = FindDeep(c, n); if (r != null) return r; }
            return null;
        }

        void Init()
        {
            if (ready || body == null) return;
            SetupArm(L, "L", +1f); SetupArm(R, "R", -1f);
            SetupLeg(legL, "L", +1f); SetupLeg(legR, "R", -1f);
            ready = L.sh != null && R.sh != null;
        }

        void SetupLeg(Leg l, string s, float sgn)
        {
            l.side = sgn;
            l.hip = FindDeep(body, "Hip_" + s); l.knee = FindDeep(body, "Knee_" + s); l.ankle = FindDeep(body, "Ankle_" + s); l.toes = FindDeep(body, "Toes_" + s);
            if (l.hip == null || l.knee == null || l.ankle == null) { l.hip = null; return; }
            l.hipRest = l.hip.localRotation; l.kneeRest = l.knee.localRotation; l.ankleRest = l.ankle.localRotation;
            l.a = (l.knee.position - l.hip.position).magnitude; l.b = (l.ankle.position - l.knee.position).magnitude;
            Transform root = transform;
            l.ankleH = Mathf.Max(0.02f, Vector3.Dot(l.ankle.position - root.position, Vector3.up));
            l.fwdOff = Vector3.Dot(l.ankle.position - l.hip.position, root.forward);
        }

        void SetupArm(Arm a, string s, float sgn)
        {
            a.side = sgn;
            a.sh = FindDeep(body, "Shoulder_" + s); a.el = FindDeep(body, "Elbow_" + s); a.wr = FindDeep(body, "Wrist_" + s);
            if (a.sh == null || a.el == null || a.wr == null) return;
            a.sh.localScale = Vector3.one * armScale;
            a.wr.localScale = Vector3.one / armScale;
            a.shRest = a.sh.localRotation; a.elRest = a.el.localRotation; a.wrRest = a.wr.localRotation;
            a.a = (a.el.position - a.sh.position).magnitude; a.b = (a.wr.position - a.el.position).magnitude;
            a.aim = FindDeep(body, "MiddleFinger1_" + s);
            foreach (var f in new[] { "IndexFinger", "MiddleFinger", "RingFinger", "PinkyFinger" })
                for (int i = 1; i <= 3; i++) { var t = FindDeep(body, f + i + "_" + s); if (t != null) { a.fingers.Add(t); a.fingerRest.Add(t.localRotation); } }
        }

        static void AimBone(Transform bone, Transform child, Vector3 worldDir)
        {
            if (bone == null || child == null) return;
            Vector3 cur = (child.position - bone.position).normalized;
            bone.rotation = Quaternion.FromToRotation(cur, worldDir.normalized) * bone.rotation;
        }

        void TwoBone(Arm a, Vector3 target, Vector3 poleDir) { TwoBoneIK(a.sh, a.el, a.wr, a.a, a.b, target, poleDir); }

        void TwoBoneIK(Transform sh, Transform el, Transform wr, float la, float lb, Vector3 target, Vector3 poleDir)
        {
            Vector3 S = sh.position;
            Vector3 d = target - S;
            float dist = Mathf.Clamp(d.magnitude, Mathf.Abs(la - lb) + 0.02f, la + lb - 0.004f);
            Vector3 dir = d.sqrMagnitude > 1e-6f ? d.normalized : transform.forward;
            float x = (dist * dist + la * la - lb * lb) / (2f * dist);
            float h = Mathf.Sqrt(Mathf.Max(0f, la * la - x * x));
            Vector3 perp = poleDir - Vector3.Dot(poleDir, dir) * dir;
            if (perp.sqrMagnitude < 1e-5f) perp = Vector3.down;
            perp.Normalize();
            Vector3 E = S + dir * x + perp * h;
            Vector3 T = S + dir * dist;
            AimBone(sh, el, E - S);
            AimBone(el, wr, T - E);
        }

        /// <summary>Hitung ulang pose badan. Dipanggil tiap LateUpdate (dan oleh builder untuk render pratinjau).</summary>
        public void Pose(float dt)
        {
            Init();
            if (!ready) return;
            // posisi badan: kepala sedikit di belakang kamera, mengikuti tinggi mata (jongkok)
            float eyeDelta = cameraRoot != null ? cameraRoot.localPosition.y - eyeHeight : 0f;
            body.localPosition = bodyOffset + new Vector3(0f, eyeDelta, 0f);
            body.localRotation = Quaternion.identity;
            Transform root = transform;

            // reset ke pose istirahat
            foreach (var a in new[] { L, R })
            {
                a.sh.localRotation = a.shRest; a.el.localRotation = a.elRest; a.wr.localRotation = a.wrRest;
                for (int i = 0; i < a.fingers.Count; i++) a.fingers[i].localRotation = a.fingerRest[i];
            }

            FpsWeapon w = manager != null ? manager.Current : null;
            Transform camT = cameraTransform != null ? cameraTransform : root;
            Transform gR = w != null ? w.gripRight : null, gL = w != null ? w.gripLeft : null;

            if (w != null && w.customHands)
            {
                ArmTo(R, gR, camT, new Vector3(0.2f, -0.28f, 0.34f), w.rollR, w.curlR, w.aimR, w.poleR);
                ArmTo(L, gL, camT, Vector3.zero, w.rollL, w.curlL, w.aimL, w.poleL);
            }
            else
            {
                ArmTo(R, gR, camT, new Vector3(0.2f, -0.28f, 0.34f), rollRight, curlRight, handAimLocal, poleOut);
                ArmTo(L, gL, camT, Vector3.zero, rollLeft, curlLeft, handAimLocal, poleOut);
            }

            PoseLegs(dt);
        }

        void PoseLegs(float dt)
        {
            Transform root = transform;
            if (legL.hip == null || legR.hip == null) return;
            foreach (var lg in new[] { legL, legR }) { lg.hip.localRotation = lg.hipRest; lg.knee.localRotation = lg.kneeRest; lg.ankle.localRotation = lg.ankleRest; }

            float speed = controller != null ? controller.HorizontalVelocity.magnitude : 0f;
            bool grounded = controller == null || controller.IsGrounded;
            bool sprint = controller != null && controller.IsSprinting;
            float ampT = Mathf.Clamp01(speed / 4.2f);
            float airT = grounded ? 0f : 1f;
            if (previewAmp >= 0f) { ampT = previewAmp; airT = previewAir >= 0f ? previewAir : 0f; }
            if (Application.isPlaying) { ampS = Mathf.MoveTowards(ampS, ampT, dt * 7f); airS = Mathf.MoveTowards(airS, airT, dt * 4f); }
            else { ampS = ampT; airS = airT; }
            float amp = ampS * (1f - airS * 0.6f);
            if (previewPhase >= 0f) phase = previewPhase; else phase += speed * dt * 1.55f;
            if (phase > 628f) phase -= 628f;

            float stepLen = sprint ? 0.52f : 0.36f;
            float liftH = sprint ? 0.20f : 0.13f;
            Vector3 fwd = root.forward, right = root.right;
            foreach (var lg in new[] { legL, legR })
            {
                Vector3 rel = lg.hip.position - root.position;
                float lat = Vector3.Dot(rel, right), frw = Vector3.Dot(rel, fwd);
                float ph = phase + (lg.side > 0f ? 0f : Mathf.PI);
                float sn = Mathf.Sin(ph), cs = Mathf.Cos(ph);
                float step = sn * stepLen * amp;
                float lift = Mathf.Max(0f, cs) * liftH * amp;
                // di udara: kaki sedikit terlipat dan menggantung
                lift = Mathf.Lerp(lift, 0.16f, airS);
                step = Mathf.Lerp(step, lg.side * 0.10f, airS);
                Vector3 target = root.position + right * lat + fwd * (frw + lg.fwdOff + step) + Vector3.up * (lg.ankleH + lift);
                Vector3 pole = fwd * 1f + right * (lat > 0f ? 0.18f : -0.18f);
                TwoBoneIK(lg.hip, lg.knee, lg.ankle, lg.a, lg.b, target, pole);
                if (lg.toes != null)
                {
                    float liftN = Mathf.Clamp01(lift / Mathf.Max(0.01f, liftH));
                    float swingPitch = Mathf.Lerp(52f, 8f, (sn + 1f) * 0.5f);      // ujung kaki turun saat lepas tanah, naik saat menapak
                    float pitch = Mathf.Lerp(28f, swingPitch, liftN);
                    Vector3 toeDir = Quaternion.AngleAxis(pitch, right) * fwd;     // putar ke bawah (sumbu kanan)
                    AimBone(lg.ankle, lg.toes, toeDir);
                }
            }
        }

        void ArmTo(Arm a, Transform grip, Transform camT, Vector3 fallbackCamLocal, float roll, float curl, Vector3 handAim, Vector3 poleV)
        {
            Transform root = transform;
            Vector3 outward = root.right * a.side;
            Vector3 pole = outward * poleV.x + Vector3.up * poleV.y + root.forward * poleV.z;
            Vector3 target, aimDir;
            if (grip != null)
            {
                target = grip.position;
                aimDir = grip.TransformDirection(handAim.normalized);
            }
            else if (fallbackCamLocal != Vector3.zero)
            {
                target = camT.TransformPoint(fallbackCamLocal); aimDir = root.forward;
            }
            else
            {
                // tangan bebas: menggantung santai di samping badan
                target = a.sh.position + root.TransformDirection(new Vector3(a.side * 0.10f, -(a.a + a.b) * 0.86f, 0.16f));
                aimDir = (root.forward * 0.4f - Vector3.up).normalized; curl *= 0.7f;
            }
            TwoBone(a, target, pole);
            if (a.aim != null && aimDir.sqrMagnitude > 0.01f)
            {
                AimBone(a.wr, a.aim, aimDir);
                if (Mathf.Abs(roll) > 0.01f) a.wr.rotation = Quaternion.AngleAxis(roll * a.side, aimDir) * a.wr.rotation;
            }
            for (int i = 0; i < a.fingers.Count; i++)
                a.fingers[i].localRotation = a.fingerRest[i] * Quaternion.Euler(0f, 0f, curl);
        }

        public string DebugReach()
        {
            Init();
            FpsWeapon w = manager != null ? manager.Current : null;
            string r = "";
            foreach (var a in new[] { L, R })
            {
                Transform g = w != null ? (a.side > 0 ? w.gripLeft : w.gripRight) : null;
                r += (a.side > 0 ? "L" : "R") + " reach " + (a.a + a.b).ToString("F3") + " sh " + a.sh.position.ToString("F2") + (g != null ? " dist " + (g.position - a.sh.position).magnitude.ToString("F3") + " grip " + g.position.ToString("F2") : "") + " | ";
            }
            return r + "cam " + (cameraTransform != null ? cameraTransform.position.ToString("F2") : "-");
        }

        void LateUpdate() { Pose(Time.deltaTime); }
    }
}
