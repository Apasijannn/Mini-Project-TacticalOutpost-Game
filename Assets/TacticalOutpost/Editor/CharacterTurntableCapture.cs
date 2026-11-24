using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Outpost.Final;
using Outpost.Player;

namespace Outpost.EditorTools
{
    /// <summary>
    /// Tool inspeksi 360° model karakter (Insurgent) saat memegang semua senjata (AK-47, M1911, dst.) dalam pose Hip & ADS,
    /// sekaligus pratinjau kamera First-Person (FPS) yang akurat.
    /// Berjalan di PreviewScene terisolasi sehingga tidak mengubah atau mengotori scene yang sedang terbuka.
    ///
    /// Menu:
    ///   - Tactical Outpost / Character / 360 Weapon Inspector      (window interaktif drag-to-orbit di Editor)
    ///   - Tactical Outpost / Character / Capture 360 All Weapons   (render contact sheet + 12 frame 360° + FPS view ke Docs/CharacterPreview)
    /// CLI:
    ///   -executeMethod Outpost.EditorTools.CharacterTurntableCapture.CaptureBatch
    ///   -executeMethod Outpost.EditorTools.CharacterTurntableCapture.CaptureBatchBefore
    /// </summary>
    public static class CharacterTurntableCapture
    {
        public const string IndustrialPlayerPrefab = "Assets/TacticalOutpost/Gameplay/Prefabs/Adapted/Player_Industrial.prefab";
        public const string FallbackPlayerPrefab = "Assets/TacticalOutpost/Gameplay/Prefabs/Player_FPS.prefab";
        public const string OutputRelativeDir = "Docs/CharacterPreview";

        static readonly PropertyInfo CurrentIndexProp = typeof(WeaponManager).GetProperty(
            "CurrentIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        internal sealed class TurntableStage : IDisposable
        {
            public Scene scene;
            public GameObject player;
            public FpsBody body;
            public WeaponManager weaponManager;
            public Camera camera;
            public GameObject cameraGo;
            public readonly List<FpsWeapon> weapons = new List<FpsWeapon>();
            readonly List<Material> ownedMaterials = new List<Material>();
            readonly Dictionary<Renderer, ShadowCastingMode> origShadows = new Dictionary<Renderer, ShadowCastingMode>();
            public string restDump = "";

            public static TurntableStage Create()
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IndustrialPlayerPrefab);
                if (prefab == null) prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FallbackPlayerPrefab);
                if (prefab == null) throw new FileNotFoundException("Prefab player tidak ditemukan: " + IndustrialPlayerPrefab);

                var stage = new TurntableStage { scene = EditorSceneManager.NewPreviewScene() };
                try
                {
                    stage.SetupStudio();
                    stage.SetupPlayer(prefab);
                    return stage;
                }
                catch
                {
                    stage.Dispose();
                    throw;
                }
            }

            void SetupStudio()
            {
                // 3-point + bounce lighting agar seluruh sisi 360° dan detail tangan/senjata terlihat jelas.
                AddLight("Studio_Key", LightType.Directional, new Vector3(38f, -145f, 0f), Vector3.zero, new Color(1f, 0.97f, 0.93f), 1.6f, 0f);
                AddLight("Studio_FillL", LightType.Point, Vector3.zero, new Vector3(-2.6f, 2.0f, 2.2f), new Color(0.80f, 0.89f, 1f), 12f, 12f);
                AddLight("Studio_RimBack", LightType.Point, Vector3.zero, new Vector3(1.8f, 2.4f, -2.6f), new Color(0.94f, 0.96f, 1f), 14f, 12f);
                AddLight("Studio_BackLeft", LightType.Point, Vector3.zero, new Vector3(-2.2f, 1.4f, -2.2f), new Color(0.86f, 0.91f, 0.98f), 10f, 12f);

                // Piringan lantai studio sebagai referensi pijakan kaki & orientasi depan (+Z).
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                floor.name = "Studio_Floor";
                floor.transform.SetPositionAndRotation(new Vector3(0f, -0.025f, 0f), Quaternion.identity);
                floor.transform.localScale = new Vector3(2.4f, 0.025f, 2.4f);
                var col = floor.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
                var floorShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (floorShader != null)
                {
                    var mat = new Material(floorShader) { hideFlags = HideFlags.HideAndDontSave };
                    mat.color = new Color(0.22f, 0.24f, 0.28f, 1f);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.22f, 0.24f, 0.28f, 1f));
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
                    ownedMaterials.Add(mat);
                    floor.GetComponent<Renderer>().sharedMaterial = mat;
                }
                SceneManager.MoveGameObjectToScene(floor, scene);

                cameraGo = new GameObject("Studio_Camera");
                camera = cameraGo.AddComponent<Camera>();
                camera.scene = scene;
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 50f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.13f, 0.15f, 0.19f, 1f);
                camera.cullingMask = ~0;
                camera.allowHDR = true;
                camera.allowMSAA = false;
                var urp = cameraGo.AddComponent<UniversalAdditionalCameraData>();
                urp.renderPostProcessing = false;
                urp.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                SceneManager.MoveGameObjectToScene(cameraGo, scene);
            }

            void AddLight(string name, LightType type, Vector3 euler, Vector3 pos, Color color, float intensity, float range)
            {
                var go = new GameObject(name);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
                var l = go.AddComponent<Light>();
                l.type = type;
                l.color = color;
                l.intensity = intensity;
                if (range > 0f) l.range = range;
                l.shadows = LightShadows.None;
                l.cullingMask = ~0;
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            void SetupPlayer(GameObject prefab)
            {
                player = UnityEngine.Object.Instantiate(prefab);
                player.name = "Character_Turntable_Instance";
                player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                SceneManager.MoveGameObjectToScene(player, scene);

                foreach (var c in player.GetComponentsInChildren<Camera>(true)) c.enabled = false;
                foreach (var a in player.GetComponentsInChildren<AudioListener>(true)) a.enabled = false;
                foreach (var l in player.GetComponentsInChildren<Light>(true)) l.enabled = false;
                foreach (var lr in player.GetComponentsInChildren<LineRenderer>(true)) lr.enabled = false;
                foreach (var ps in player.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var r = ps.GetComponent<Renderer>();
                    if (r != null) r.enabled = false;
                }

                foreach (var r in player.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer || r is LineRenderer) continue;
                    origShadows[r] = r.shadowCastingMode;
                    r.enabled = true;
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.receiveShadows = true;
                    if (r is SkinnedMeshRenderer smr)
                    {
                        smr.updateWhenOffscreen = true;
                        smr.forceMatrixRecalculationPerRender = true;
                    }
                }

                body = player.GetComponentInChildren<FpsBody>(true);
                weaponManager = player.GetComponentInChildren<WeaponManager>(true);
                if (body == null || weaponManager == null || weaponManager.weapons == null || weaponManager.weapons.Length == 0)
                    throw new InvalidOperationException("FpsBody atau WeaponManager.weapons tidak ditemukan pada prefab player.");

                body.previewAmp = 0f;
                body.previewPhase = 0f;
                body.previewAir = 0f;

                weapons.Clear();
                foreach (var w in weaponManager.weapons)
                    if (w != null) weapons.Add(w);

                // Rekam geometri tulang & senjata SEBELUM FpsBody.Init() memodifikasi skala/rotasi tulang.
                restDump = BuildRestGeometryDump();
            }

            public void SetThirdPersonRenderMode(bool thirdPerson)
            {
                foreach (var kv in origShadows)
                {
                    if (kv.Key == null) continue;
                    kv.Key.shadowCastingMode = thirdPerson ? ShadowCastingMode.On : kv.Value;
                }
            }

            string BuildRestGeometryDump()
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== REST POSE BONE GEOMETRY (before FpsBody.Init) ===");
                string[] bones =
                {
                    "Root_M", "Spine1_M", "Spine2_M", "Chest_M", "Neck_M", "Head_M",
                    "Scapula_R", "Shoulder_R", "ShoulderPart1_R", "Elbow_R", "ElbowPart1_R", "Wrist_R",
                    "ThumbFinger1_R", "ThumbFinger2_R", "ThumbFinger3_R",
                    "IndexFinger1_R", "IndexFinger2_R", "IndexFinger3_R",
                    "MiddleFinger1_R", "MiddleFinger2_R", "MiddleFinger3_R",
                    "RingFinger1_R", "PinkyFinger1_R",
                    "Scapula_L", "Shoulder_L", "ShoulderPart1_L", "Elbow_L", "ElbowPart1_L", "Wrist_L",
                    "ThumbFinger1_L", "ThumbFinger2_L", "ThumbFinger3_L",
                    "IndexFinger1_L", "IndexFinger2_L", "IndexFinger3_L",
                    "MiddleFinger1_L", "MiddleFinger2_L", "MiddleFinger3_L",
                    "RingFinger1_L", "PinkyFinger1_L"
                };
                foreach (var bn in bones)
                {
                    var t = FindDeep(body.body, bn);
                    if (t == null) continue;
                    sb.AppendLine($"  {bn,-16} worldPos={t.position:F4}  right={t.right:F3}  up={t.up:F3}  fwd={t.forward:F3}  localEuler={t.localEulerAngles:F1}");
                }
                foreach (var side in new[] { "R", "L" })
                {
                    var wr = FindDeep(body.body, "Wrist_" + side);
                    var mid = FindDeep(body.body, "MiddleFinger1_" + side);
                    var idx = FindDeep(body.body, "IndexFinger1_" + side);
                    var pnk = FindDeep(body.body, "PinkyFinger1_" + side);
                    var th1 = FindDeep(body.body, "ThumbFinger1_" + side);
                    var th2 = FindDeep(body.body, "ThumbFinger2_" + side);
                    if (wr != null && mid != null && idx != null && pnk != null)
                    {
                        Vector3 fingerFwdW = (mid.position - wr.position).normalized;
                        Vector3 radialW = (idx.position - pnk.position).normalized; // ke sisi telunjuk/ibu jari
                        Vector3 crossW = Vector3.Cross(fingerFwdW, radialW).normalized;
                        Vector3 fingerFwdL = wr.InverseTransformDirection(fingerFwdW);
                        Vector3 radialL = wr.InverseTransformDirection(radialW);
                        Vector3 crossL = wr.InverseTransformDirection(crossW);
                        sb.AppendLine($"  [Hand_{side} Basis in World] fingerFwd={fingerFwdW:F3} radial(Pnk->Idx)={radialW:F3} cross(fwd x rad)={crossW:F3}");
                        sb.AppendLine($"  [Hand_{side} Basis in WristLocal] fingerFwd={fingerFwdL:F3} radial={radialL:F3} cross={crossL:F3}");
                        if (th1 != null && th2 != null)
                            sb.AppendLine($"  [Thumb_{side} World] pos={th1.position:F4} dir={(th2.position - th1.position).normalized:F3} wristToThumbLocal={wr.InverseTransformPoint(th1.position):F4}");
                        // Cek efek rotasi +Y vs +Z pada MiddleFinger2 terhadap arah telapak
                        var m2 = FindDeep(body.body, "MiddleFinger2_" + side);
                        var m3 = FindDeep(body.body, "MiddleFinger3_" + side);
                        if (m2 != null && m3 != null)
                        {
                            Vector3 fDir = (m3.position - m2.position).normalized;
                            Vector3 rotY30 = (Quaternion.AngleAxis(30f, m2.up) * fDir) - fDir;
                            Vector3 rotZ30 = (Quaternion.AngleAxis(30f, m2.forward) * fDir) - fDir;
                            sb.AppendLine($"  [Middle2_{side} Curl Test] +30Y deltaW={rotY30:F3} (dot cross={Vector3.Dot(rotY30.normalized, crossW):F2}, dot rad={Vector3.Dot(rotY30.normalized, radialW):F2}) | +30Z deltaW={rotZ30:F3} (dot cross={Vector3.Dot(rotZ30.normalized, crossW):F2}, dot rad={Vector3.Dot(rotZ30.normalized, radialW):F2})");
                        }
                    }
                }

                sb.AppendLine();
                sb.AppendLine("=== WEAPON LOCAL BOUNDS & GRIP POINTS ===");
                foreach (var w in weapons)
                {
                    bool wasActive = w.gameObject.activeSelf;
                    w.gameObject.SetActive(true);
                    w.transform.localPosition = Vector3.zero;
                    w.transform.localRotation = Quaternion.identity;
                    Bounds localB = new Bounds();
                    bool hasB = false;
                    foreach (var mf in w.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf.sharedMesh == null) continue;
                        var b = mf.sharedMesh.bounds;
                        for (int ix = -1; ix <= 1; ix += 2)
                            for (int iy = -1; iy <= 1; iy += 2)
                                for (int iz = -1; iz <= 1; iz += 2)
                                {
                                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3(ix, iy, iz));
                                    Vector3 wp = mf.transform.TransformPoint(corner);
                                    Vector3 lp = w.transform.InverseTransformPoint(wp);
                                    if (!hasB) { localB = new Bounds(lp, Vector3.zero); hasB = true; }
                                    else localB.Encapsulate(lp);
                                }
                    }
                    sb.AppendLine($"  [{w.displayName}] kind={w.kind} hipPos={w.hipPosition:F3} hipEuler={w.hipEuler:F1} adsPos={w.adsPosition:F3} adsEuler={w.adsEuler:F1}");
                    if (hasB) sb.AppendLine($"    MeshBounds(local): min={localB.min:F3} max={localB.max:F3} size={localB.size:F3}");
                    if (w.gripRight != null) sb.AppendLine($"    GripR(local): {w.transform.InverseTransformPoint(w.gripRight.position):F3}");
                    if (w.gripLeft != null) sb.AppendLine($"    GripL(local): {w.transform.InverseTransformPoint(w.gripLeft.position):F3}");
                    if (w.muzzle != null) sb.AppendLine($"    Muzzle(local): {w.transform.InverseTransformPoint(w.muzzle.position):F3}");
                    sb.AppendLine($"    customHands={w.customHands} aimR={w.aimR:F2} aimL={w.aimL:F2} rollR={w.rollR:F1} rollL={w.rollL:F1} curlR={w.curlR:F1} curlL={w.curlL:F1}");
                    w.gameObject.SetActive(wasActive);
                }
                return sb.ToString();
            }

            public void ApplyPose(int weaponIndex, bool ads)
            {
                weaponIndex = Mathf.Clamp(weaponIndex, 0, weapons.Count - 1);
                for (int i = 0; i < weapons.Count; i++)
                    weapons[i].gameObject.SetActive(i == weaponIndex);

                CurrentIndexProp?.SetValue(weaponManager, weaponIndex);

                var w = weapons[weaponIndex];
                w.transform.localPosition = ads ? w.adsPosition : w.hipPosition;
                w.transform.localRotation = Quaternion.Euler(ads ? w.adsEuler : w.hipEuler);

                if (body.cameraRoot != null)
                {
                    body.cameraRoot.localPosition = new Vector3(0f, body.eyeHeight, 0f);
                    body.cameraRoot.localRotation = Quaternion.identity;
                }
                if (body.cameraTransform != null)
                {
                    body.cameraTransform.localPosition = Vector3.zero;
                    body.cameraTransform.localRotation = Quaternion.identity;
                }

                body.previewAds = ads ? 1f : 0f;
                body.Pose(0f);
            }

            public void AimCamera(float yawDeg, float pitchDeg, float distance, Vector3 target, float fov = 40f, float nearClip = 0.05f)
            {
                camera.fieldOfView = fov;
                camera.nearClipPlane = nearClip;
                float yaw = yawDeg * Mathf.Deg2Rad;
                float pitch = pitchDeg * Mathf.Deg2Rad;
                float horiz = Mathf.Cos(pitch) * distance;
                Vector3 offset = new Vector3(Mathf.Sin(yaw) * horiz, Mathf.Sin(pitch) * distance, Mathf.Cos(yaw) * horiz);
                cameraGo.transform.SetPositionAndRotation(target + offset, Quaternion.LookRotation(target - (target + offset), Vector3.up));
            }

            public void AimAsFpsCamera(int weaponIndex, bool ads)
            {
                var w = weapons[weaponIndex];
                camera.fieldOfView = ads ? 70f * w.adsFov : 70f;
                camera.nearClipPlane = 0.03f;
                Transform camT = body.cameraTransform != null ? body.cameraTransform : body.cameraRoot;
                cameraGo.transform.SetPositionAndRotation(camT.position, camT.rotation);
            }

            public void RenderTo(RenderTexture rt)
            {
                var request = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                    RenderPipeline.SubmitRenderRequest(camera, request);
                else
                {
                    camera.targetTexture = rt;
                    camera.Render();
                    camera.targetTexture = null;
                }
            }

            public Texture2D CaptureTexture(int width, int height)
            {
                var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
                rt.Create();
                try
                {
                    RenderTo(rt);
                    var rb = AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32);
                    rb.WaitForCompletion();
                    if (rb.hasError) throw new InvalidOperationException("AsyncGPUReadback gagal saat render karakter 360°.");
                    var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    tex.SetPixelData(rb.GetData<byte>(), 0);
                    tex.Apply(false, false);
                    return tex;
                }
                finally
                {
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }
            }

            public string BuildPoseDiagnostics(int weaponIndex, bool ads)
            {
                var w = weapons[weaponIndex];
                var sb = new StringBuilder();
                sb.AppendLine($"[{w.displayName} - {(ads ? "ADS" : "HIP")}] kind={w.kind}, customHands={w.customHands}");
                sb.AppendLine($"  Weapon localPos={w.transform.localPosition:F3}, localEuler={w.transform.localEulerAngles:F1}");
                sb.AppendLine($"  {body.DebugReach()}");
                var wrL = FindDeep(body.body, "Wrist_L");
                var wrR = FindDeep(body.body, "Wrist_R");
                var midL = FindDeep(body.body, "MiddleFinger1_L");
                var midR = FindDeep(body.body, "MiddleFinger1_R");
                var elL = FindDeep(body.body, "Elbow_L");
                var elR = FindDeep(body.body, "Elbow_R");
                if (wrL != null && w.gripLeft != null)
                {
                    Vector3 palmL = midL != null ? Vector3.Lerp(wrL.position, midL.position, 0.55f) : wrL.position;
                    sb.AppendLine($"  L: wristMiss={Vector3.Distance(wrL.position, w.gripLeft.position):F4}m  palmMiss={Vector3.Distance(palmL, w.gripLeft.position):F4}m  elbowPos={elL?.position:F3}");
                }
                if (wrR != null && w.gripRight != null)
                {
                    Vector3 palmR = midR != null ? Vector3.Lerp(wrR.position, midR.position, 0.55f) : wrR.position;
                    sb.AppendLine($"  R: wristMiss={Vector3.Distance(wrR.position, w.gripRight.position):F4}m  palmMiss={Vector3.Distance(palmR, w.gripRight.position):F4}m  elbowPos={elR?.position:F3}");
                }
                return sb.ToString();
            }

            static Transform FindDeep(Transform t, string name)
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

            public void Dispose()
            {
                foreach (var m in ownedMaterials)
                    if (m != null) UnityEngine.Object.DestroyImmediate(m);
                ownedMaterials.Clear();
                if (scene.IsValid())
                    EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [MenuItem("Tactical Outpost/Character/Capture 360 All Weapons")]
        public static void CaptureAllMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[CHARACTER 360] Keluar dari Play Mode terlebih dahulu.");
                return;
            }
            string outDir = CaptureAll(OutputRelativeDir);
            Debug.Log("[CHARACTER 360] Selesai! Hasil tersimpan di: " + outDir);
        }

        /// <summary>Entry point CLI batchmode: -executeMethod Outpost.EditorTools.CharacterTurntableCapture.CaptureBatch</summary>
        public static void CaptureBatch() { RunBatch(OutputRelativeDir); }

        /// <summary>Entry point CLI batchmode baseline: -executeMethod Outpost.EditorTools.CharacterTurntableCapture.CaptureBatchBefore</summary>
        public static void CaptureBatchBefore() { RunBatch(OutputRelativeDir + "/before"); }

        /// <summary>Kalibrasi GripR/GripL, posisi senjata, dan armScale pada prefab player lalu render 360°.</summary>
        public static void CalibrateAndCaptureBatch()
        {
            try
            {
                CalibratePlayerPrefabs();
                string outDir = CaptureAll(OutputRelativeDir);
                Debug.Log("[CHARACTER 360] Calibrate + Capture selesai: " + outDir);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tactical Outpost/Character/Calibrate Weapon Grips on Prefabs")]
        public static void CalibratePlayerPrefabs()
        {
            ApplyPrefabCalibration(FallbackPlayerPrefab);
            ApplyPrefabCalibration(IndustrialPlayerPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CHARACTER 360] Kalibrasi prefab Player_FPS & Player_Industrial selesai.");
        }

        static void ApplyPrefabCalibration(string prefabPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return;
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var body = root.GetComponentInChildren<FpsBody>(true);
                if (body != null)
                {
                    body.bodyOffset = new Vector3(0f, 0f, -0.12f);
                    body.armScale = 1.0f;
                    body.poleOut = new Vector3(0.55f, -0.82f, -0.15f);
                }

                foreach (var w in root.GetComponentsInChildren<FpsWeapon>(true))
                {
                    if (w.kind == WeaponKind.Rifle)
                    {
                        w.hipPosition = new Vector3(0.125f, -0.150f, 0.170f);
                        w.hipEuler = new Vector3(0f, -3f, 0f);
                        w.adsPosition = new Vector3(0.000f, -0.115f, 0.175f);
                        w.adsEuler = Vector3.zero;
                        if (w.gripRight != null)
                        {
                            // Tepat melingkar penuh pada pistol grip cokelat AK-47 agar 3 jari memeluk bagian depan grip & telunjuk tepat di pelatuk
                            w.gripRight.localPosition = new Vector3(0.013f, -0.058f, 0.008f);
                            w.gripRight.localRotation = Quaternion.identity;
                        }
                        if (w.gripLeft != null)
                        {
                            // Tepat di tengah wooden lower handguard agar buku jari tepat di sisi kanan kayu & ibu jari menempel sisi kiri kayu (foto 7)
                            w.gripLeft.localPosition = new Vector3(0.012f, 0.018f, 0.245f);
                            w.gripLeft.localRotation = Quaternion.identity;
                        }
                    }
                    else if (w.kind == WeaponKind.Pistol)
                    {
                        w.hipPosition = new Vector3(0.045f, -0.095f, 0.350f);
                        w.hipEuler = new Vector3(1f, -2f, 0f);
                        w.adsPosition = new Vector3(0.000f, -0.030f, 0.355f);
                        w.adsEuler = Vector3.zero;
                        if (w.gripRight != null)
                        {
                            w.gripRight.localPosition = new Vector3(0.004f, -0.070f, -0.010f);
                            w.gripRight.localRotation = Quaternion.identity;
                        }
                        if (w.gripLeft != null)
                        {
                            w.gripLeft.localPosition = new Vector3(-0.014f, -0.086f, 0.020f);
                            w.gripLeft.localRotation = Quaternion.identity;
                        }
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void RunBatch(string relativeDir)
        {
            try
            {
                string outDir = CaptureAll(relativeDir);
                Debug.Log("[CHARACTER 360] Batch selesai: " + outDir);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        public static string CaptureAll(string relativeDir = OutputRelativeDir)
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativeDir));
            string framesDir = Path.Combine(outDir, "frames");
            Directory.CreateDirectory(outDir);
            Directory.CreateDirectory(framesDir);

            var report = new StringBuilder();
            report.AppendLine("# Character 360° Weapon Inspection Report");
            report.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("Angle convention: 0°=Front, 90°=Char Right, 180°=Back, 270°=Char Left");
            report.AppendLine();

            using (var stage = TurntableStage.Create())
            {
                report.AppendLine(stage.restDump);
                report.AppendLine();

                int[] fullSheetAngles = { 0, 45, 90, 135, 180, 225, 270, 315 };
                int[] closeAngles = { 0, 45, 90, 270 };
                Vector3 fullTarget = new Vector3(0f, 0.94f, 0.08f);
                Vector3 closeTarget = new Vector3(0f, 1.34f, 0.22f);

                var fpsTiles = new List<Texture2D>();
                try
                {
                    for (int wi = 0; wi < stage.weapons.Count; wi++)
                    {
                        string safeName = Sanitize(stage.weapons[wi].displayName);
                        foreach (bool ads in new[] { false, true })
                        {
                            string stance = ads ? "ADS" : "Hip";
                            stage.SetThirdPersonRenderMode(true);
                            stage.ApplyPose(wi, ads);
                            report.AppendLine(stage.BuildPoseDiagnostics(wi, ads));

                            // 1) 12 frame turntable 360° (setiap 30°)
                            for (int deg = 0; deg < 360; deg += 30)
                            {
                                stage.AimCamera(deg, 5f, 2.85f, fullTarget);
                                var tex = stage.CaptureTexture(540, 640);
                                try
                                {
                                    File.WriteAllBytes(Path.Combine(framesDir, $"{safeName}_{stance}_{deg:000}.png"), tex.EncodeToPNG());
                                }
                                finally { UnityEngine.Object.DestroyImmediate(tex); }
                            }

                            // 2) Contact sheet 8 sudut full-body (4x2)
                            string fullSheetPath = Path.Combine(outDir, $"sheet_{safeName}_{stance}_full360.png");
                            SaveContactSheet(stage, fullSheetAngles, 4, 2, 480, 560, 5f, 2.85f, fullTarget, fullSheetPath);
                            report.AppendLine("  Full 360 sheet: " + Path.GetFileName(fullSheetPath));

                            // 3) Contact sheet 4 sudut close-up tangan & senjata (4x1)
                            string handsSheetPath = Path.Combine(outDir, $"sheet_{safeName}_{stance}_hands.png");
                            SaveContactSheet(stage, closeAngles, 4, 1, 512, 512, 8f, 1.15f, closeTarget, handsSheetPath);
                            report.AppendLine("  Hands close-up: " + Path.GetFileName(handsSheetPath));

                            // 4) Tangkap sudut kamera First-Person (FPS) dengan ShadowsOnly asli (kepala disembunyikan seperti in-game)
                            stage.SetThirdPersonRenderMode(false);
                            stage.AimAsFpsCamera(wi, ads);
                            fpsTiles.Add(stage.CaptureTexture(640, 360));
                            report.AppendLine();
                        }
                    }

                    if (fpsTiles.Count > 0)
                    {
                        int cols = 2;
                        int rows = Mathf.CeilToInt(fpsTiles.Count / (float)cols);
                        SaveTextureGrid(fpsTiles, cols, rows, 640, 360, Path.Combine(outDir, "sheet_FPS_All.png"));
                        report.AppendLine("FPS view sheet: sheet_FPS_All.png (order: AK47 Hip, AK47 ADS, M1911 Hip, M1911 ADS)");
                    }

                    // 5) Tangkap Ally VIPER (AllyBody) 4 sudut untuk memverifikasi paritas IK sekutu
                    var ph = stage.player.GetComponentInChildren<PlayerHealth>(true);
                    if (ph != null)
                    {
                        var allyGo = new GameObject("Ally_Preview_Instance");
                        SceneManager.MoveGameObjectToScene(allyGo, stage.scene);
                        var ally = allyGo.AddComponent<AllyBody>();
                        if (ally.Build(ph, new Color(0.25f, 0.72f, 1f)))
                        {
                            stage.player.SetActive(false);
                            foreach (var smr in allyGo.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                            {
                                smr.updateWhenOffscreen = true;
                                smr.forceMatrixRecalculationPerRender = true;
                            }
                            ally.PoseNow(0f);
                            report.AppendLine();
                            report.AppendLine("[Ally VIPER - AK-47] " + ally.ReachReport());
                            string allySheetPath = Path.Combine(outDir, "sheet_AllyVIPER_AK47.png");
                            SaveContactSheet(stage, closeAngles, 4, 1, 512, 560, 6f, 2.35f, new Vector3(0f, 1.05f, 0.08f), allySheetPath);
                            report.AppendLine("  Ally VIPER sheet: " + Path.GetFileName(allySheetPath));
                            stage.player.SetActive(true);
                        }
                        UnityEngine.Object.DestroyImmediate(allyGo);
                    }
                }
                finally
                {
                    foreach (var t in fpsTiles) if (t != null) UnityEngine.Object.DestroyImmediate(t);
                }
            }

            File.WriteAllText(Path.Combine(outDir, "report.txt"), report.ToString(), Encoding.UTF8);
            return outDir;
        }

        static void SaveContactSheet(
            TurntableStage stage, int[] angles, int cols, int rows, int cellW, int cellH,
            float pitchDeg, float distance, Vector3 target, string outputPath)
        {
            var tiles = new List<Texture2D>(angles.Length);
            try
            {
                for (int idx = 0; idx < angles.Length && idx < cols * rows; idx++)
                {
                    stage.AimCamera(angles[idx], pitchDeg, distance, target);
                    tiles.Add(stage.CaptureTexture(cellW, cellH));
                }
                SaveTextureGrid(tiles, cols, rows, cellW, cellH, outputPath);
            }
            finally
            {
                foreach (var t in tiles) if (t != null) UnityEngine.Object.DestroyImmediate(t);
            }
        }

        static void SaveTextureGrid(List<Texture2D> tiles, int cols, int rows, int cellW, int cellH, string outputPath)
        {
            int pad = 4;
            int totalW = cols * cellW + (cols + 1) * pad;
            int totalH = rows * cellH + (rows + 1) * pad;
            var sheet = new Texture2D(totalW, totalH, TextureFormat.RGBA32, false);
            try
            {
                var bg = new Color32[totalW * totalH];
                var borderColor = new Color32(20, 24, 30, 255);
                for (int i = 0; i < bg.Length; i++) bg[i] = borderColor;
                sheet.SetPixels32(bg);

                for (int idx = 0; idx < tiles.Count && idx < cols * rows; idx++)
                {
                    int col = idx % cols;
                    int row = idx / cols;
                    int x = pad + col * (cellW + pad);
                    int y = pad + (rows - 1 - row) * (cellH + pad);
                    sheet.SetPixels32(x, y, cellW, cellH, tiles[idx].GetPixels32());
                }
                sheet.Apply(false, false);
                File.WriteAllBytes(outputPath, sheet.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(sheet); }
        }

        static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Weapon";
            var sb = new StringBuilder();
            foreach (char c in name)
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.Length > 0 ? sb.ToString() : "Weapon";
        }
    }

    /// <summary>
    /// Window interaktif di Unity Editor untuk memutar karakter 360° secara real-time saat memegang semua senjata.
    /// </summary>
    public sealed class Character360Window : EditorWindow
    {
        CharacterTurntableCapture.TurntableStage stage;
        RenderTexture previewRt;
        int weaponIndex;
        bool ads;
        bool closeUp;
        bool fpsView;
        bool autoSpin;
        float yaw = 35f;
        float pitch = 6f;
        float zoom = 1f;
        double lastTime;
        string reachInfo = "";

        [MenuItem("Tactical Outpost/Character/360 Weapon Inspector")]
        public static void Open()
        {
            var win = GetWindow<Character360Window>("Character 360°");
            win.minSize = new Vector2(560f, 640f);
            win.Show();
        }

        void OnEnable()
        {
            lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Cleanup();
        }

        void Cleanup()
        {
            if (previewRt != null) { previewRt.Release(); DestroyImmediate(previewRt); previewRt = null; }
            if (stage != null) { stage.Dispose(); stage = null; }
        }

        void EnsureStage()
        {
            if (stage != null && stage.scene.IsValid()) return;
            Cleanup();
            stage = CharacterTurntableCapture.TurntableStage.Create();
            ApplyAndRefreshReach();
        }

        void ApplyAndRefreshReach()
        {
            if (stage == null) return;
            stage.ApplyPose(weaponIndex, ads);
            reachInfo = stage.BuildPoseDiagnostics(weaponIndex, ads).Trim();
        }

        void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Clamp((float)(now - lastTime), 0f, 0.1f);
            lastTime = now;
            if (autoSpin && !fpsView)
            {
                yaw = (yaw + dt * 45f) % 360f;
                Repaint();
            }
        }

        void OnGUI()
        {
            try { EnsureStage(); }
            catch (Exception ex)
            {
                EditorGUILayout.HelpBox(ex.Message, MessageType.Error);
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var names = new string[stage.weapons.Count];
                for (int i = 0; i < stage.weapons.Count; i++) names[i] = stage.weapons[i].displayName;
                int nextWeapon = EditorGUILayout.Popup(weaponIndex, names, EditorStyles.toolbarPopup, GUILayout.Width(120f));
                bool nextAds = GUILayout.Toggle(ads, ads ? "ADS" : "Hip", EditorStyles.toolbarButton, GUILayout.Width(50f));
                fpsView = GUILayout.Toggle(fpsView, "FPS Cam", EditorStyles.toolbarButton, GUILayout.Width(65f));
                if (!fpsView)
                {
                    closeUp = GUILayout.Toggle(closeUp, closeUp ? "Hands" : "Full Body", EditorStyles.toolbarButton, GUILayout.Width(80f));
                    autoSpin = GUILayout.Toggle(autoSpin, "Auto 360°", EditorStyles.toolbarButton, GUILayout.Width(72f));
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Capture 360° PNGs", EditorStyles.toolbarButton, GUILayout.Width(125f)))
                {
                    string dir = CharacterTurntableCapture.CaptureAll();
                    EditorUtility.RevealInFinder(dir);
                }
                if (nextWeapon != weaponIndex || nextAds != ads)
                {
                    weaponIndex = nextWeapon;
                    ads = nextAds;
                    ApplyAndRefreshReach();
                }
            }

            if (!fpsView)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Angle:", GUILayout.Width(45f));
                    foreach (var preset in new[] { ("Front 0°", 0f), ("3/4 R 45°", 45f), ("Right 90°", 90f), ("Back 180°", 180f), ("Left 270°", 270f), ("3/4 L 315°", 315f) })
                    {
                        if (GUILayout.Button(preset.Item1, EditorStyles.miniButton))
                        {
                            autoSpin = false;
                            yaw = preset.Item2;
                        }
                    }
                }
            }

            Rect rect = GUILayoutUtility.GetRect(10f, 10000f, 380f, 10000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (!fpsView) HandleDrag(rect);

            if (Event.current.type == EventType.Repaint && rect.width > 16f && rect.height > 16f)
            {
                float ppp = EditorGUIUtility.pixelsPerPoint;
                int rw = Mathf.Clamp(Mathf.RoundToInt(rect.width * ppp), 256, 1440);
                int rh = Mathf.Clamp(Mathf.RoundToInt(rect.height * ppp), 256, 1440);
                if (previewRt == null || previewRt.width != rw || previewRt.height != rh)
                {
                    if (previewRt != null) { previewRt.Release(); DestroyImmediate(previewRt); }
                    previewRt = new RenderTexture(rw, rh, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
                    previewRt.Create();
                }

                stage.SetThirdPersonRenderMode(!fpsView);
                if (fpsView)
                    stage.AimAsFpsCamera(weaponIndex, ads);
                else
                {
                    Vector3 target = closeUp ? new Vector3(0f, 1.34f, 0.22f) : new Vector3(0f, 0.94f, 0.08f);
                    float dist = (closeUp ? 1.15f : 2.85f) / Mathf.Clamp(zoom, 0.5f, 2.5f);
                    stage.AimCamera(yaw, pitch, dist, target);
                }
                stage.RenderTo(previewRt);
                GUI.DrawTexture(rect, previewRt, ScaleMode.ScaleToFit, false);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!fpsView)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        yaw = EditorGUILayout.Slider("Yaw 360°", yaw, 0f, 360f);
                        pitch = EditorGUILayout.Slider("Pitch", pitch, -30f, 45f, GUILayout.Width(210f));
                        zoom = EditorGUILayout.Slider("Zoom", zoom, 0.6f, 2.2f, GUILayout.Width(190f));
                    }
                }
                EditorGUILayout.LabelField(reachInfo, EditorStyles.wordWrappedMiniLabel, GUILayout.MinHeight(54f));
            }
        }

        void HandleDrag(Rect rect)
        {
            var e = Event.current;
            if (!rect.Contains(e.mousePosition)) return;
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                autoSpin = false;
                yaw = (yaw + e.delta.x * 0.7f) % 360f;
                if (yaw < 0f) yaw += 360f;
                pitch = Mathf.Clamp(pitch + e.delta.y * 0.4f, -30f, 45f);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                zoom = Mathf.Clamp(zoom - e.delta.y * 0.05f, 0.6f, 2.2f);
                e.Use();
                Repaint();
            }
        }
    }
}
