using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using MiniMapModular;

namespace Outpost.UI
{
    /// <summary>
    /// Mengatur minimap: kecil di pojok kiri atas, tekan M untuk memperbesar menjadi peta seluruh outpost.
    /// Memakai kamera dan UI dari aset Minimap Modular Radar (Cam_Map + Map_Status).
    /// </summary>
    public class MinimapController : MonoBehaviour
    {
        public static MinimapController Instance { get; private set; }
        public MP mp;
        public Custom_MP ms;
        public RectTransform holder;          // area penuh layar tempat Map_Status berada
        public RectTransform map;             // Map_Status
        public Camera mapCam;
        public Transform player;
        public Image dim;                     // latar gelap saat peta besar

        [Header("Kecil (pojok kiri atas)")]
        public float smallScale = 0.9f;
        public float smallCamSize = 27f;
        public float margin = 34f;

        [Header("Besar (tekan M)")]
        public float largeScale = 2.55f;
        public float largeCamSize = 64f;
        public Vector3 mapCenter = new Vector3(12f, 0f, -1.2f);

        [Header("Tampilan peta")]
        public float sunBoost = 2.4f;       // penerangan ekstra hanya saat kamera peta merender (aspal gelap jadi terbaca)
        public float cameraHeight = 40f;

        public bool IsLarge { get; private set; }
        float k;                 // 0 = kecil, 1 = besar
        float refreshTimer;
        bool prevFog;
        Light sun; float prevSun;
        const float BaseRT = 256f;   // ukuran RT asli aset (dasar kalibrasi ikon)

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public static bool CloseLargeMap()
        {
            if (Instance == null || !Instance.IsLarge) return false;
            Instance.IsLarge = false;
            return true;
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBegin;
            RenderPipelineManager.endCameraRendering += OnEnd;
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBegin;
            RenderPipelineManager.endCameraRendering -= OnEnd;
        }
        void OnBegin(ScriptableRenderContext c, Camera cam)
        {
            if (cam != mapCam) return;
            prevFog = RenderSettings.fog; RenderSettings.fog = false;
            if (sun != null) { prevSun = sun.intensity; sun.intensity = prevSun * sunBoost; }
        }
        void OnEnd(ScriptableRenderContext c, Camera cam)
        {
            if (cam != mapCam) return;
            RenderSettings.fog = prevFog;
            if (sun != null) sun.intensity = prevSun;
        }

        void Start()
        {
            sun = RenderSettings.sun;
            if (sun == null) foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
            if (mapCam != null) mapCam.enabled = false;   // dirender manual (~20x per detik) supaya hemat
            Apply(0f, true);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked) IsLarge = !IsLarge;
            float target = IsLarge ? 1f : 0f;
            k = Mathf.MoveTowards(k, target, 4.5f * Time.unscaledDeltaTime);
            Apply(Mathf.SmoothStep(0f, 1f, k), false);

            refreshTimer -= Time.unscaledDeltaTime;
            if (mapCam != null && refreshTimer <= 0f) { refreshTimer = 0.05f; mapCam.Render(); }
        }

        void Apply(float e, bool force)
        {
            if (map == null || holder == null) return;
            float s = Mathf.Lerp(smallScale, largeScale, e);
            map.localScale = new Vector3(s, s, 1f);

            Vector2 size = holder.rect.size;
            float half = 150f * s;
            Vector2 smallPos = new Vector2(-size.x * 0.5f + margin + half, size.y * 0.5f - margin - half);
            map.anchoredPosition = Vector2.Lerp(smallPos, Vector2.zero, e);

            if (dim != null) dim.color = new Color(0f, 0f, 0f, 0.42f * e);

            if (mapCam != null && mp != null)
            {
                float cs = Mathf.Lerp(smallCamSize, largeCamSize, e);
                mapCam.orthographicSize = cs;
                mp.Min_Dis_Icon = cs * 0.9f;          // ikon di luar jarak ini jadi panah tepi
                mp.CamSize = cs;
                if (player != null)
                {
                    // di mode besar kamera bergeser ke tengah outpost (MP mengikuti player + PosyMeLocal)
                    Vector3 toCenter = (mapCenter - player.position) * e;
                    mp.PosyMeLocal = new Vector3(toCenter.x, cameraHeight, toCenter.z);
                }
            }
        }

        void LateUpdate()
        {
            // ikon player (+ sapuan radar + kerucut arah) mengikuti posisi asli player di peta
            if (mapCam == null || ms == null || ms.IconPlayerR == null || player == null) return;
            Vector3 sp = mapCam.WorldToScreenPoint(player.position);
            float rtW = mapCam.pixelWidth, rtH = mapCam.pixelHeight;
            if (rtW < 1f) return;
            float f = 300f / rtW;
            ms.IconPlayerR.anchoredPosition = new Vector2((sp.x - rtW * 0.5f) * f, (sp.y - rtH * 0.5f) * f);
        }
    }
}
