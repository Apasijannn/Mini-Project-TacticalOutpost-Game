using System.Collections.Generic;
using UnityEngine;

namespace Outpost.Player
{
    /// <summary>
    /// Konfigurasi orientasi telapak, posisi titik genggam, pole siku, dan tekukan jari/ibu jari
    /// untuk satu tangan (dalam ruang lokal grip senjata).
    /// </summary>
    public struct HandGripPose
    {
        public Vector3 fingerFwd;       // arah pergelangan -> pangkal jari (ruang lokal grip)
        public Vector3 palmNormal;      // arah permukaan telapak tangan menekan senjata (ruang lokal grip)
        public Vector3 pole;            // arah siku relatif badan (x = keluar/outward, y = atas, z = depan)
        public float gripForwardFrac;   // posisi titik genggam sepanjang Wrist->MiddleFinger1 (0..1)
        public float gripPalmDepth;     // kedalaman titik genggam dari sumbu pergelangan ke permukaan telapak (m)
        public float curlScale;         // pengali tekukan jari (1 = standar)
        public bool triggerIndex;       // true = telunjuk melurus ke pelatuk (tangan kanan)
        public float scapulaProtract;   // + = bahu ikut maju (jangkauan kiri/pistol), - = bahu sedikit mundur (saku popor kanan)
        public Vector3 thumbDir1;       // arah pangkal ibu jari dalam basis tangan (fwd, rad, palm)
        public Vector3 thumbDir2;       // arah ujung ibu jari dalam basis tangan (fwd, rad, palm)

        public static HandGripPose RifleRight() => new HandGripPose
        {
            // Referensi foto 6-8: siku kanan mundur-bawah alami (~35°), punggung tangan miring sejajar pistol grip AK (+14° atas-depan) sehingga lengan bawah -> pergelangan -> tangan mengalir lurus, 3 jari melingkar rapat di depan grip & telunjuk tepat di pelatuk
            fingerFwd = new Vector3(-0.24f, 0.24f, 0.94f),
            palmNormal = new Vector3(-0.96f, -0.05f, -0.25f),
            pole = new Vector3(0.72f, -0.52f, -0.45f),
            gripForwardFrac = 0.56f,
            gripPalmDepth = 0.017f,
            curlScale = 1.32f,
            triggerIndex = true,
            scapulaProtract = -0.08f,
            thumbDir1 = new Vector3(0.72f, 0.22f, 0.66f),
            thumbDir2 = new Vector3(0.32f, -0.10f, 0.94f),
        };

        public static HandGripPose RifleLeft() => new HandGripPose
        {
            // Referensi foto 7: pergelangan di bawah-kiri handguard kayu, buku jari tepat di sisi kanan kayu, 4 jari melingkar rapat naik-masuk memeluk kayu, ibu jari menempel penuh di sisi kiri handguard
            fingerFwd = new Vector3(0.84f, 0.32f, 0.44f),
            palmNormal = new Vector3(-0.27f, 0.95f, -0.16f),
            pole = new Vector3(0.20f, -0.97f, -0.10f),
            gripForwardFrac = 0.72f,
            gripPalmDepth = 0.014f,
            curlScale = 0.88f,
            triggerIndex = false,
            scapulaProtract = 0.42f,
            thumbDir1 = new Vector3(0.80f, 0.56f, 0.22f),
            thumbDir2 = new Vector3(0.68f, 0.72f, -0.14f),
        };

        public static HandGripPose PistolRight() => new HandGripPose
        {
            // Modified Weaver (foto 4-5): tangan kanan (pelatuk) mendorong LURUS ke depan dengan bahu belakang, siku hampir lurus sejajar slide
            fingerFwd = new Vector3(-0.10f, -0.04f, 0.99f),
            palmNormal = new Vector3(-0.98f, 0.04f, -0.16f),
            pole = new Vector3(0.28f, -0.95f, -0.08f),
            gripForwardFrac = 0.56f,
            gripPalmDepth = 0.020f,
            curlScale = 1.12f,
            triggerIndex = true,
            scapulaProtract = 0.14f,
            thumbDir1 = new Vector3(0.86f, 0.24f, 0.45f),
            thumbDir2 = new Vector3(0.92f, 0.10f, 0.36f),
        };

        public static HandGripPose PistolLeft() => new HandGripPose
        {
            // Modified Weaver (foto 4-5): tangan kiri (penahan) menarik dengan siku JELAS MENEKUK ke bawah di bawah lengan kanan yang lurus
            fingerFwd = new Vector3(0.22f, 0.10f, 0.97f),
            palmNormal = new Vector3(0.94f, 0.20f, -0.26f),
            pole = new Vector3(0.16f, -0.98f, -0.08f),
            gripForwardFrac = 0.58f,
            gripPalmDepth = 0.022f,
            curlScale = 1.02f,
            triggerIndex = false,
            scapulaProtract = 0.30f,
            thumbDir1 = new Vector3(0.88f, 0.34f, 0.32f),
            thumbDir2 = new Vector3(0.95f, 0.16f, 0.24f),
        };
    }

    /// <summary>
    /// Solver IK anatomi bersama untuk model Insurgent (dipakai Player FpsBody dan NPC AllyBody):
    /// - Blading tubuh atas (Spine/Chest) + kompensasi Neck/Head agar bahu membentuk saku popor alami
    /// - Protraksi Scapula (tulang selangka) ringan
    /// - Two-Bone IK lengan dengan arah siku keluar yang benar (L = -right, R = +right) & hinge-plane konsisten
    /// - Orientasi telapak penuh 3D + target titik genggam telapak (bukan pergelangan)
    /// - Distribusi twist lengan bawah ke tulang ElbowPart1 agar pergelangan tidak terpelintir (anti candy-wrapper)
    /// - Tekukan jari pada sumbu lokal Y yang benar + telunjuk di pelatuk + pose ibu jari 3 ruas
    /// </summary>
    public sealed class InsurgentRig
    {
        sealed class ArmChain
        {
            public Transform scapula, sh, shPart1, el, elPart1, wr;
            public Quaternion scapulaRest, shRest, elRest, wrRest;
            public float la, lb, outwardSign, palmLen;
            public Vector3 localFwd, localRad, localPalm;
            public Quaternion localHandBasis;
            public Vector3 restBendNormalInSh;
            public Vector3 restPalmInElbow;
            public Transform[] index, middle, ring, pinky, thumb;
            public Quaternion[] indexRest, middleRest, ringRest, pinkyRest, thumbRest;
        }

        sealed class LegChain
        {
            public Transform hip, knee, ankle, toes;
            public Quaternion hipRest, kneeRest, ankleRest;
            public float la, lb, sideSign, ankleH, fwdOff, restLat, restFrw, restToePitch;
        }

        readonly ArmChain armL = new ArmChain(), armR = new ArmChain();
        readonly LegChain legL = new LegChain(), legR = new LegChain();
        Transform rootPelvis, spine1, spine2, chest, neck, head;
        Quaternion rootPelvisRest, spine1Rest, spine2Rest, chestRest, neckRest, headRest;
        Vector3 headRestInBody;
        Transform root, body;
        public bool Ready { get; private set; }

        public static Transform FindDeep(Transform t, string name)
        {
            if (t == null) return null;
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        public bool Init(Transform rootTransform, Transform bodyTransform, float armScale = 1f)
        {
            if (Ready) return true;
            if (rootTransform == null || bodyTransform == null) return false;
            root = rootTransform;
            body = bodyTransform;

            rootPelvis = FindDeep(body, "Root_M");
            spine1 = FindDeep(body, "Spine1_M");
            spine2 = FindDeep(body, "Spine2_M");
            chest = FindDeep(body, "Chest_M");
            neck = FindDeep(body, "Neck_M");
            head = FindDeep(body, "Head_M");
            if (rootPelvis != null) rootPelvisRest = rootPelvis.localRotation;
            if (spine1 != null) spine1Rest = spine1.localRotation;
            if (spine2 != null) spine2Rest = spine2.localRotation;
            if (chest != null) chestRest = chest.localRotation;
            if (neck != null) neckRest = neck.localRotation;
            if (head != null)
            {
                headRest = head.localRotation;
                headRestInBody = body.InverseTransformPoint(head.position);
            }

            // L = sisi kiri (-root.right), R = sisi kanan (+root.right)
            SetupArm(armL, "L", -1f, armScale);
            SetupArm(armR, "R", +1f, armScale);
            SetupLeg(legL, "L", -1f);
            SetupLeg(legR, "R", +1f);
            Ready = armL.sh != null && armR.sh != null;
            return Ready;
        }

        void SetupArm(ArmChain a, string s, float outwardSign, float armScale)
        {
            a.outwardSign = outwardSign;
            a.scapula = FindDeep(body, "Scapula_" + s);
            a.sh = FindDeep(body, "Shoulder_" + s);
            a.shPart1 = FindDeep(body, "ShoulderPart1_" + s);
            a.el = FindDeep(body, "Elbow_" + s);
            a.elPart1 = FindDeep(body, "ElbowPart1_" + s);
            a.wr = FindDeep(body, "Wrist_" + s);
            if (a.sh == null || a.el == null || a.wr == null) { a.sh = null; return; }

            float sc = Mathf.Clamp(armScale, 0.85f, 1.15f);
            a.sh.localScale = Vector3.one * sc;
            a.wr.localScale = Vector3.one / sc;
            if (a.shPart1 != null) a.shPart1.localRotation = Quaternion.identity;
            if (a.elPart1 != null) a.elPart1.localRotation = Quaternion.identity;

            if (a.scapula != null) a.scapulaRest = a.scapula.localRotation;
            a.shRest = a.sh.localRotation;
            a.elRest = a.el.localRotation;
            a.wrRest = a.wr.localRotation;

            a.la = Vector3.Distance(a.sh.position, a.el.position);
            a.lb = Vector3.Distance(a.el.position, a.wr.position);

            var mid1 = FindDeep(body, "MiddleFinger1_" + s);
            var idx1 = FindDeep(body, "IndexFinger1_" + s);
            var pnk1 = FindDeep(body, "PinkyFinger1_" + s);
            if (mid1 != null && idx1 != null && pnk1 != null)
            {
                a.palmLen = Vector3.Distance(a.wr.position, mid1.position);
                Vector3 fwdW = (mid1.position - a.wr.position).normalized;
                Vector3 radW = (idx1.position - pnk1.position).normalized;
                radW = (radW - Vector3.Dot(radW, fwdW) * fwdW).normalized;
                // Kanan (outward > 0): Cross(fwd, rad) = arah dalam telapak; Kiri (outward < 0): Cross(rad, fwd) = arah dalam telapak
                Vector3 palmW = outwardSign > 0f ? Vector3.Cross(fwdW, radW).normalized : Vector3.Cross(radW, fwdW).normalized;

                a.localFwd = a.wr.InverseTransformDirection(fwdW).normalized;
                a.localRad = a.wr.InverseTransformDirection(radW).normalized;
                a.localPalm = a.wr.InverseTransformDirection(palmW).normalized;
                a.localHandBasis = Quaternion.LookRotation(a.localFwd, a.localPalm);
            }
            else
            {
                a.palmLen = 0.095f;
                a.localFwd = outwardSign > 0f ? Vector3.left : Vector3.right;
                a.localRad = outwardSign > 0f ? Vector3.up : Vector3.down;
                a.localPalm = outwardSign > 0f ? Vector3.back : Vector3.forward;
                a.localHandBasis = Quaternion.LookRotation(a.localFwd, a.localPalm);
            }

            Vector3 uRest = (a.el.position - a.sh.position).normalized;
            Vector3 lRest = (a.wr.position - a.el.position).normalized;
            Vector3 nRest = Vector3.Cross(uRest, lRest);
            if (nRest.sqrMagnitude < 1e-4f) nRest = Vector3.Cross(uRest, root.forward);
            a.restBendNormalInSh = Quaternion.Inverse(a.sh.rotation) * nRest.normalized;
            a.restPalmInElbow = Quaternion.Inverse(a.el.rotation) * (a.wr.rotation * a.localPalm);

            LoadFinger(s, "IndexFinger", 3, out a.index, out a.indexRest);
            LoadFinger(s, "MiddleFinger", 3, out a.middle, out a.middleRest);
            LoadFinger(s, "RingFinger", 3, out a.ring, out a.ringRest);
            LoadFinger(s, "PinkyFinger", 3, out a.pinky, out a.pinkyRest);
            LoadFinger(s, "ThumbFinger", 4, out a.thumb, out a.thumbRest);
        }

        void LoadFinger(string side, string prefix, int count, out Transform[] bones, out Quaternion[] rest)
        {
            bones = new Transform[count];
            rest = new Quaternion[count];
            for (int i = 0; i < count; i++)
            {
                bones[i] = FindDeep(body, prefix + (i + 1) + "_" + side);
                if (bones[i] != null) rest[i] = bones[i].localRotation;
            }
        }

        void SetupLeg(LegChain l, string s, float sideSign)
        {
            l.sideSign = sideSign;
            l.hip = FindDeep(body, "Hip_" + s);
            l.knee = FindDeep(body, "Knee_" + s);
            l.ankle = FindDeep(body, "Ankle_" + s);
            l.toes = FindDeep(body, "Toes_" + s);
            if (l.hip == null || l.knee == null || l.ankle == null) { l.hip = null; return; }
            l.hipRest = l.hip.localRotation;
            l.kneeRest = l.knee.localRotation;
            l.ankleRest = l.ankle.localRotation;
            l.la = Vector3.Distance(l.hip.position, l.knee.position);
            l.lb = Vector3.Distance(l.knee.position, l.ankle.position);
            l.ankleH = Mathf.Max(0.02f, Vector3.Dot(l.ankle.position - body.position, Vector3.up));
            l.fwdOff = Vector3.Dot(l.ankle.position - l.hip.position, root.forward);
            l.restLat = Vector3.Dot(l.hip.position - body.position, root.right);
            l.restFrw = Vector3.Dot(l.ankle.position - body.position, root.forward);
            if (l.toes != null)
            {
                Vector3 td = (l.toes.position - l.ankle.position).normalized;
                l.restToePitch = Mathf.Atan2(-Vector3.Dot(td, root.up), Mathf.Max(0.01f, Vector3.Dot(td, root.forward))) * Mathf.Rad2Deg;
            }
            else l.restToePitch = 28f;
        }

        public void ResetAllToRest()
        {
            ResetUpperBody();
            if (legL.hip != null) { legL.hip.localRotation = legL.hipRest; legL.knee.localRotation = legL.kneeRest; legL.ankle.localRotation = legL.ankleRest; }
            if (legR.hip != null) { legR.hip.localRotation = legR.hipRest; legR.knee.localRotation = legR.kneeRest; legR.ankle.localRotation = legR.ankleRest; }
        }

        public void ResetUpperBody()
        {
            if (rootPelvis != null) rootPelvis.localRotation = rootPelvisRest;
            if (spine1 != null) spine1.localRotation = spine1Rest;
            if (spine2 != null) spine2.localRotation = spine2Rest;
            if (chest != null) chest.localRotation = chestRest;
            if (neck != null) neck.localRotation = neckRest;
            if (head != null) head.localRotation = headRest;
            ResetArm(armL);
            ResetArm(armR);
        }

        static void ResetArm(ArmChain a)
        {
            if (a.sh == null) return;
            if (a.scapula != null) a.scapula.localRotation = a.scapulaRest;
            a.sh.localRotation = a.shRest;
            if (a.shPart1 != null) a.shPart1.localRotation = Quaternion.identity;
            a.el.localRotation = a.elRest;
            if (a.elPart1 != null) a.elPart1.localRotation = Quaternion.identity;
            a.wr.localRotation = a.wrRest;
            ResetFinger(a.index, a.indexRest);
            ResetFinger(a.middle, a.middleRest);
            ResetFinger(a.ring, a.ringRest);
            ResetFinger(a.pinky, a.pinkyRest);
            ResetFinger(a.thumb, a.thumbRest);
        }

        static void ResetFinger(Transform[] bones, Quaternion[] rest)
        {
            if (bones == null) return;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null) bones[i].localRotation = rest[i];
        }

        /// <summary>
        /// Postur tubuh atas taktis dunia asli (nose-over-toes):
        /// - Torso condong maju agresif (~13-16°) menahan recoil + blading ringan
        /// - Leher sedikit mengikuti condong ke depan sementara kepala terangkat lurus ke target (dagu sedikit masuk ke bahu)
        /// - Saat ADS rifle, kepala miring ke kanan (cheek weld) menempel popor.
        /// </summary>
        public void ApplyTorsoStance(float bladeYawDeg, float leanPitchDeg, float cheekRollDeg = 0f, float headPitchDeg = 0f)
        {
            Vector3 up = root.up;
            Vector3 right = root.right;
            Vector3 fwd = root.forward;
            float lean = Mathf.Clamp(leanPitchDeg, -10f, 24f);

            // Condong maju mulai dari pinggul (Root_M hip-hinge) hingga dada agar garis tubuh & baju miring utuh seperti foto 2, 5, 7, 8
            if (rootPelvis != null) rootPelvis.rotation = Quaternion.AngleAxis(bladeYawDeg * 0.22f, up) * Quaternion.AngleAxis(lean * 0.40f, right) * rootPelvis.rotation;
            if (spine1 != null) spine1.rotation = Quaternion.AngleAxis(bladeYawDeg * 0.24f, up) * Quaternion.AngleAxis(lean * 0.24f, right) * spine1.rotation;
            if (spine2 != null) spine2.rotation = Quaternion.AngleAxis(bladeYawDeg * 0.26f, up) * Quaternion.AngleAxis(lean * 0.20f, right) * spine2.rotation;
            if (chest != null) chest.rotation = Quaternion.AngleAxis(bladeYawDeg * 0.28f, up) * Quaternion.AngleAxis(lean * 0.16f, right) * chest.rotation;

            // Leher ikut sedikit maju (hanya kompensasi 35%), kepala mengangkat sisa 65% agar mata datar ke depan seperti foto referensi 1-8
            if (neck != null)
                neck.rotation = Quaternion.AngleAxis(-bladeYawDeg * 0.42f, up)
                    * Quaternion.AngleAxis(-lean * 0.35f, right)
                    * Quaternion.AngleAxis(-cheekRollDeg * 0.45f, fwd) * neck.rotation;
            if (head != null)
                head.rotation = Quaternion.AngleAxis(-bladeYawDeg * 0.58f, up)
                    * Quaternion.AngleAxis(-lean * 0.65f + headPitchDeg, right)
                    * Quaternion.AngleAxis(-cheekRollDeg * 0.55f, fwd) * head.rotation;
        }

        /// <summary>Hitung pergeseran kepala akibat torso condong maju agar body bisa dijaga tetap di belakang kamera FPS tanpa clipping dahi.</summary>
        public Vector3 HeadLeanDeltaInBody()
        {
            if (head == null || body == null) return Vector3.zero;
            return body.InverseTransformPoint(head.position) - headRestInBody;
        }

        public void SolveRightArm(Transform grip, HandGripPose pose, Vector3 fallbackTarget)
        {
            SolveArm(armR, grip, pose, fallbackTarget);
        }

        public void SolveLeftArm(Transform grip, HandGripPose pose, Vector3 fallbackTarget)
        {
            SolveArm(armL, grip, pose, fallbackTarget);
        }

        void SolveArm(ArmChain a, Transform grip, HandGripPose pose, Vector3 fallbackTarget)
        {
            if (a.sh == null) return;

            Vector3 wristTarget;
            Quaternion desiredWristWorld;
            bool hasGrip = grip != null;

            if (hasGrip)
            {
                Vector3 fwdW = grip.TransformDirection(pose.fingerFwd.normalized);
                Vector3 palmW = grip.TransformDirection(pose.palmNormal.normalized);
                palmW = palmW - Vector3.Dot(palmW, fwdW) * fwdW;
                if (palmW.sqrMagnitude < 1e-4f) palmW = Vector3.up;
                palmW.Normalize();

                desiredWristWorld = Quaternion.LookRotation(fwdW, palmW) * Quaternion.Inverse(a.localHandBasis);
                Vector3 localGripOffset = a.localFwd * (a.palmLen * pose.gripForwardFrac) + a.localPalm * pose.gripPalmDepth;
                wristTarget = grip.position - desiredWristWorld * localGripOffset;
            }
            else
            {
                wristTarget = fallbackTarget;
                Vector3 fwdW = (root.forward * 0.35f - Vector3.up).normalized;
                Vector3 palmW = (-root.right * a.outwardSign).normalized;
                palmW = (palmW - Vector3.Dot(palmW, fwdW) * fwdW).normalized;
                desiredWristWorld = Quaternion.LookRotation(fwdW, palmW) * Quaternion.Inverse(a.localHandBasis);
            }

            // 1) Protraksi / retraksi Scapula (tangan kiri/pistol sedikit maju; bahu kanan rifle sedikit mundur membuka ruang siku)
            if (a.scapula != null && hasGrip && Mathf.Abs(pose.scapulaProtract) > 0.001f)
            {
                if (pose.scapulaProtract > 0f)
                {
                    Vector3 curShDir = (a.sh.position - a.scapula.position).normalized;
                    Vector3 toTarget = (wristTarget - a.scapula.position).normalized;
                    float ang = Vector3.Angle(curShDir, toTarget);
                    if (ang > 1f)
                    {
                        float protract = Mathf.Min(14f, ang * pose.scapulaProtract);
                        Vector3 axis = Vector3.Cross(curShDir, toTarget);
                        if (axis.sqrMagnitude > 1e-5f)
                            a.scapula.rotation = Quaternion.AngleAxis(protract, axis.normalized) * a.scapula.rotation;
                    }
                }
                else
                {
                    // Retraksi ringan bahu kanan ke belakang (around root.up) untuk saku popor & ruang siku kanan
                    float retractDeg = Mathf.Clamp(-pose.scapulaProtract * 70f, 0f, 10f);
                    a.scapula.rotation = Quaternion.AngleAxis(retractDeg * a.outwardSign, root.up) * a.scapula.rotation;
                }
            }

            // 2) Two-Bone IK (Shoulder -> Elbow -> Wrist) dengan pole siku keluar yang benar
            Vector3 S = a.sh.position;
            Vector3 d = wristTarget - S;
            float dist = Mathf.Clamp(d.magnitude, Mathf.Abs(a.la - a.lb) + 0.03f, a.la + a.lb - 0.008f);
            Vector3 dir = d.sqrMagnitude > 1e-6f ? d.normalized : root.forward;
            float x = (dist * dist + a.la * a.la - a.lb * a.lb) / (2f * dist);
            float h = Mathf.Sqrt(Mathf.Max(0f, a.la * a.la - x * x));

            Vector3 outward = root.right * a.outwardSign;
            Vector3 poleDir = outward * pose.pole.x + root.up * pose.pole.y + root.forward * pose.pole.z;
            Vector3 perp = poleDir - Vector3.Dot(poleDir, dir) * dir;
            if (perp.sqrMagnitude < 1e-5f) perp = -root.up;
            perp.Normalize();

            Vector3 E = S + dir * x + perp * h;
            Vector3 T = S + dir * dist;
            Vector3 targetUpper = (E - S).normalized;
            Vector3 targetLower = (T - E).normalized;

            // Arahkan lengan atas lalu samakan bidang tekuk siku agar bahu & lengan baju tidak terpuntir
            AimBone(a.sh, a.el, targetUpper);
            Vector3 targetNormal = Vector3.Cross(targetUpper, targetLower);
            if (targetNormal.sqrMagnitude > 1e-4f)
            {
                targetNormal.Normalize();
                Vector3 curNormal = a.sh.rotation * a.restBendNormalInSh;
                curNormal = curNormal - Vector3.Dot(curNormal, targetUpper) * targetUpper;
                if (curNormal.sqrMagnitude > 1e-4f)
                {
                    float twist = Vector3.SignedAngle(curNormal.normalized, targetNormal, targetUpper);
                    a.sh.rotation = Quaternion.AngleAxis(twist, targetUpper) * a.sh.rotation;
                }
            }

            // Tekuk siku murni ke arah pergelangan target
            AimBone(a.el, a.wr, targetLower);

            // 3) Terapkan orientasi telapak 3D & distribusikan 60% twist lengan bawah ke ElbowPart1 (anti candy-wrapper)
            a.wr.rotation = desiredWristWorld;
            if (a.elPart1 != null)
            {
                Vector3 forearmAxis = (a.wr.position - a.el.position).normalized;
                Vector3 noTwistPalm = a.el.rotation * a.restPalmInElbow;
                Vector3 curPalm = desiredWristWorld * a.localPalm;
                noTwistPalm -= Vector3.Dot(noTwistPalm, forearmAxis) * forearmAxis;
                curPalm -= Vector3.Dot(curPalm, forearmAxis) * forearmAxis;
                if (noTwistPalm.sqrMagnitude > 1e-4f && curPalm.sqrMagnitude > 1e-4f)
                {
                    float twistDeg = Mathf.Clamp(Vector3.SignedAngle(noTwistPalm.normalized, curPalm.normalized, forearmAxis), -135f, 135f);
                    a.elPart1.rotation = Quaternion.AngleAxis(twistDeg * 0.60f, forearmAxis) * a.elPart1.rotation;
                    a.wr.rotation = desiredWristWorld;
                }
            }

            // 4) Tekukan jari pada sumbu lokal -Y (anatomi rig Insurgent) + telunjuk pelatuk + ibu jari
            PoseFingers(a, pose, hasGrip);
        }

        static void PoseFingers(ArmChain a, HandGripPose pose, bool hasGrip)
        {
            float c = hasGrip ? pose.curlScale : 0.35f;
            // Rapatkan celah jari dari rest pose yang terbuka (Index1.z=-14.4°, Pinky1.z=+9.5°; +Z = arah kelingking, -Z = arah telunjuk)
            float close = hasGrip ? 1f : 0.3f;

            // Jari telunjuk: melingkar pas di dalam pelindung pelatuk (trigger guard) bila triggerIndex aktif
            if (pose.triggerIndex && hasGrip)
                CurlFinger(a.index, a.indexRest, 10f, 38f, 30f, 3f);
            else
                CurlFinger(a.index, a.indexRest, 38f * c, 46f * c, 26f * c, 9f * close);

            CurlFinger(a.middle, a.middleRest, 44f * c, 52f * c, 30f * c, 1f * close);
            CurlFinger(a.ring, a.ringRest, 48f * c, 54f * c, 32f * c, -3f * close);
            CurlFinger(a.pinky, a.pinkyRest, 52f * c, 56f * c, 34f * c, -8f * close);

            // Pose ibu jari berdasarkan basis tangan (fwd, rad, palm) — identik untuk tangan kiri & kanan
            if (a.thumb != null && a.thumb.Length >= 4 && a.thumb[0] != null && a.thumb[1] != null)
            {
                Vector3 hFwd = a.wr.rotation * a.localFwd;
                Vector3 hRad = a.wr.rotation * a.localRad;
                Vector3 hPalm = a.wr.rotation * a.localPalm;

                Vector3 td1 = hasGrip ? pose.thumbDir1 : new Vector3(0.65f, 0.55f, 0.45f);
                Vector3 td2 = hasGrip ? pose.thumbDir2 : new Vector3(0.60f, 0.35f, 0.65f);
                Vector3 dir1W = (hFwd * td1.x + hRad * td1.y + hPalm * td1.z).normalized;
                Vector3 dir2W = (hFwd * td2.x + hRad * td2.y + hPalm * td2.z).normalized;

                AimBone(a.thumb[0], a.thumb[1], dir1W);
                if (a.thumb[2] != null) AimBone(a.thumb[1], a.thumb[2], dir2W);
                if (a.thumb[2] != null && a.thumb[3] != null)
                    AimBone(a.thumb[2], a.thumb[3], (dir2W + hPalm * 0.10f).normalized);
            }
        }

        static void CurlFinger(Transform[] bones, Quaternion[] rest, float d1, float d2, float d3, float adductZ = 0f)
        {
            if (bones == null) return;
            // Rig Insurgent: rotasi lokal -Y menekuk jari masuk ke telapak tangan (baik L maupun R); +Z merapatkan ke arah kelingking.
            if (bones.Length > 0 && bones[0] != null) bones[0].localRotation = rest[0] * Quaternion.Euler(0f, -d1, adductZ);
            if (bones.Length > 1 && bones[1] != null) bones[1].localRotation = rest[1] * Quaternion.Euler(0f, -d2, 0f);
            if (bones.Length > 2 && bones[2] != null) bones[2].localRotation = rest[2] * Quaternion.Euler(0f, -d3, 0f);
        }

        public void PoseLegs(float phase, float amp, float airS, bool sprint, bool combatStance = true)
        {
            if (legL.hip == null || legR.hip == null) return;
            foreach (var lg in new[] { legL, legR })
            {
                lg.hip.localRotation = lg.hipRest;
                lg.knee.localRotation = lg.kneeRest;
                lg.ankle.localRotation = lg.ankleRest;
            }

            float stepLen = sprint ? 0.52f : 0.36f;
            float liftH = sprint ? 0.20f : 0.13f;
            float idleWeight = combatStance ? Mathf.Clamp01(1f - amp * 1.4f) * (1f - airS) : 0f;
            Vector3 fwd = root.forward, right = root.right;
            float bodyLat = Vector3.Dot(body.position - root.position, right);
            float bodyFrw = Vector3.Dot(body.position - root.position, fwd);

            foreach (var lg in new[] { legL, legR })
            {
                Vector3 rel = lg.hip.position - root.position;
                float moveLat = Vector3.Dot(rel, right);
                float moveFrw = Vector3.Dot(rel, fwd) + lg.fwdOff;

                // Combat stance dunia asli (foto 5 & 8): kaki kiri melangkah maju (lutut tekuk atletis),
                // kaki kanan mundur jauh & hampir lurus menahan recoil segaris dengan torso yang condong maju
                float stanceLat = bodyLat * 0.55f + lg.restLat + (lg.sideSign < 0f ? -0.055f : 0.075f);
                float stanceFrw = bodyFrw + lg.restFrw + (lg.sideSign < 0f ? 0.165f : -0.255f);
                float lat = Mathf.Lerp(moveLat, stanceLat, idleWeight);
                float baseFrw = Mathf.Lerp(moveFrw, stanceFrw, idleWeight);

                float ph = phase + (lg.sideSign < 0f ? 0f : Mathf.PI);
                float sn = Mathf.Sin(ph), cs = Mathf.Cos(ph);
                float step = sn * stepLen * amp;
                float lift = Mathf.Max(0f, cs) * liftH * amp;
                lift = Mathf.Lerp(lift, 0.16f, airS);
                step = Mathf.Lerp(step, -lg.sideSign * 0.10f, airS);

                Vector3 target = root.position + right * lat + fwd * (baseFrw + step) + Vector3.up * (lg.ankleH + lift);
                float kneeOut = lg.sideSign > 0f ? (0.18f + 0.16f * idleWeight) : (-0.18f - 0.08f * idleWeight);
                Vector3 pole = fwd * 1f + right * kneeOut;
                TwoBoneSimple(lg.hip, lg.knee, lg.ankle, lg.la, lg.lb, target, pole);

                if (lg.toes != null)
                {
                    float liftN = Mathf.Clamp01(lift / Mathf.Max(0.01f, liftH));
                    float swingPitch = Mathf.Lerp(lg.restToePitch + 18f, lg.restToePitch - 14f, (sn + 1f) * 0.5f);
                    float pitch = Mathf.Lerp(lg.restToePitch, swingPitch, liftN);
                    float footTurn = (lg.sideSign > 0f ? 16f : -5f) * idleWeight;
                    Vector3 footFwd = Quaternion.AngleAxis(footTurn, root.up) * fwd;
                    Vector3 footRight = Vector3.Cross(root.up, footFwd).normalized;
                    Vector3 toeDir = Quaternion.AngleAxis(pitch, footRight) * footFwd;
                    AimBone(lg.ankle, lg.toes, toeDir);
                }
            }
        }

        public string ReachReport(Transform gripL, Transform gripR)
        {
            if (!Ready) return "not ready";
            string r = "";
            foreach (var pair in new[] { new KeyValuePair<ArmChain, Transform>(armL, gripL), new KeyValuePair<ArmChain, Transform>(armR, gripR) })
            {
                var a = pair.Key;
                var g = pair.Value;
                string tag = a == armL ? "L" : "R";
                r += tag + " reach " + (a.la + a.lb).ToString("F3") + " sh " + a.sh.position.ToString("F2");
                if (g != null)
                {
                    Vector3 palmW = a.wr.position + a.wr.rotation * (a.localFwd * (a.palmLen * 0.56f) + a.localPalm * 0.02f);
                    r += " dist " + Vector3.Distance(a.sh.position, g.position).ToString("F3")
                       + " palmMiss " + Vector3.Distance(palmW, g.position).ToString("F3");
                }
                r += " | ";
            }
            return r;
        }

        static void TwoBoneSimple(Transform sh, Transform el, Transform wr, float la, float lb, Vector3 target, Vector3 poleDir)
        {
            Vector3 S = sh.position;
            Vector3 d = target - S;
            float dist = Mathf.Clamp(d.magnitude, Mathf.Abs(la - lb) + 0.02f, la + lb - 0.004f);
            Vector3 dir = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.down;
            float x = (dist * dist + la * la - lb * lb) / (2f * dist);
            float h = Mathf.Sqrt(Mathf.Max(0f, la * la - x * x));
            Vector3 perp = poleDir - Vector3.Dot(poleDir, dir) * dir;
            if (perp.sqrMagnitude < 1e-5f) perp = Vector3.forward;
            perp.Normalize();
            Vector3 E = S + dir * x + perp * h;
            Vector3 T = S + dir * dist;
            AimBone(sh, el, E - S);
            AimBone(el, wr, T - E);
        }

        static void AimBone(Transform bone, Transform child, Vector3 worldDir)
        {
            if (bone == null || child == null || worldDir.sqrMagnitude < 1e-6f) return;
            Vector3 cur = (child.position - bone.position).normalized;
            if (cur.sqrMagnitude < 1e-6f) return;
            bone.rotation = Quaternion.FromToRotation(cur, worldDir.normalized) * bone.rotation;
        }
    }

    /// <summary>
    /// Badan player (model Insurgent) terlihat dari kamera orang pertama dan 360°:
    /// lengan memegang senjata lewat IK dua-tulang dengan orientasi telapak 3D, twist lengan bawah, jari menggenggam, dan kaki berayun saat berjalan.
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
        public float armScale = 1.0f;           // skala lengan alami (1.0 = tanpa pembengkakan)
        public Vector3 handAimLocal = new Vector3(0f, -0.3f, 1f);
        public float rollRight = 0f;
        public float rollLeft = 0f;
        public float curlRight = 60f;
        public float curlLeft = 70f;
        public Vector3 poleOut = new Vector3(0.55f, -0.82f, -0.15f);

        [Header("Kaki")]
        public float stride = 26f;

        readonly InsurgentRig rig = new InsurgentRig();
        float phase, ampS, airS;
        // dipakai builder untuk pratinjau di Edit Mode (-1 = otomatis dari controller/weapon)
        [System.NonSerialized] public float previewAmp = -1f, previewPhase = -1f, previewAir = -1f, previewAds = -1f;

        void Init()
        {
            if (rig.Ready || body == null) return;
            // Pastikan badan di posisi awal sebelum kalibrasi rest pose, lalu inisialisasi rig skala alami.
            rig.Init(transform, body, 1.0f);
        }

        /// <summary>Hitung ulang pose badan. Dipanggil tiap LateUpdate (dan oleh tool pratinjau 360°).</summary>
        public void Pose(float dt)
        {
            Init();
            if (!rig.Ready) return;

            FpsWeapon w = manager != null ? manager.Current : null;
            bool isPistol = w != null && w.kind == WeaponKind.Pistol;
            float adsT = previewAds >= 0f ? Mathf.Clamp01(previewAds) : (w != null ? w.AdsWeight : 0f);

            // Hitung gerak kaki dulu supaya tinggi lutut (combat stance drop) ikut selaras dengan badan
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

            float idleWeight = Mathf.Clamp01(1f - amp * 1.4f) * (1f - airS);
            float eyeDelta = cameraRoot != null ? cameraRoot.localPosition.y - eyeHeight : 0f;

            // True-FPS ADS shoulder weld + aggressive forward combat lean (foto 1-8):
            // 1) Pasang posisi awal & rotasi torso condong maju (~13-16°) dulu
            Vector3 adsShift = isPistol ? Vector3.zero : new Vector3(-0.095f, -0.005f, 0f) * adsT;
            float stanceDropY = -0.052f * idleWeight;
            body.localPosition = bodyOffset + new Vector3(0f, eyeDelta + stanceDropY, 0f) + adsShift;
            body.localRotation = Quaternion.identity;

            rig.ResetUpperBody();

            Transform camT = cameraTransform != null ? cameraTransform : transform;
            Transform gR = w != null ? w.gripRight : null;
            Transform gL = w != null ? w.gripLeft : null;

            float camPitch = camT != null ? Mathf.Asin(Mathf.Clamp(-camT.forward.y, -1f, 1f)) * Mathf.Rad2Deg : 0f;
            // Pistol: Modified Weaver blading ~26° (bahu kanan mundur -> lengan pelatuk lurus tanpa celah; bahu kiri maju -> siku penahan menekuk ke bawah seperti foto 4-5)
            // Rifle: Blading ~22-24° agar bahu kiri menjangkau tengah handguard kayu dengan nyaman & bahu kanan membuka saku popor
            float bladeYaw = isPistol ? Mathf.Lerp(25f, 26f, adsT) : Mathf.Lerp(22f, 24f, adsT);
            float forwardLean = isPistol ? Mathf.Lerp(14f, 17f, adsT) : Mathf.Lerp(15f, 18f, adsT);
            float cheekRoll = isPistol ? 0f : 12f * adsT;
            float headPitch = isPistol ? 2f * adsT : 4f * adsT;
            rig.ApplyTorsoStance(bladeYaw, camPitch * 0.18f + forwardLean, cheekRoll, headPitch);

            // 2) Jangkar kepala tetap aman di belakang kamera FPS (pinggul & kaki mundur membentuk garis diagonal foto 5 & 8, nol clipping dahi)
            Vector3 headDelta = rig.HeadLeanDeltaInBody();
            body.localPosition += new Vector3(0f, -headDelta.y * 0.5f, -headDelta.z);

            HandGripPose poseR = isPistol ? HandGripPose.PistolRight() : HandGripPose.RifleRight();
            HandGripPose poseL = isPistol ? HandGripPose.PistolLeft() : HandGripPose.RifleLeft();

            rig.SolveRightArm(gR, poseR, camT.TransformPoint(new Vector3(0.2f, -0.28f, 0.34f)));
            rig.SolveLeftArm(gL, poseL, transform.position + transform.TransformDirection(new Vector3(-0.22f, 0.95f, 0.14f)));

            rig.PoseLegs(phase, amp, airS, sprint, true);
        }

        /// <summary>Kembalikan seluruh tulang ke rest pose sebelum di-clone oleh AllyBody saat runtime.</summary>
        public void ResetToRestPose()
        {
            if (rig.Ready) rig.ResetAllToRest();
        }

        public string DebugReach()
        {
            Init();
            FpsWeapon w = manager != null ? manager.Current : null;
            return rig.ReachReport(w != null ? w.gripLeft : null, w != null ? w.gripRight : null)
                + "cam " + (cameraTransform != null ? cameraTransform.position.ToString("F2") : "-");
        }

        void LateUpdate() { Pose(Time.deltaTime); }
    }
}
