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
        Texture2D borderTexture;
        Sprite borderSprite;
        const float BaseRT = 256f;   // ukuran RT asli aset (dasar kalibrasi ikon)

        void Awake() { Instance = this; }
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (borderSprite != null) Destroy(borderSprite);
            if (borderTexture != null) Destroy(borderTexture);
        }

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
            var mapCanvas = map.GetComponentInParent<Canvas>(); mapCanvas.overrideSorting = true; mapCanvas.sortingOrder = 70;
            borderTexture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            borderTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[256 * 256];
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float radius = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(128, 128));
                    pixels[y * 256 + x] = new Color(1, 1, 1, Mathf.Clamp01(1.5f - Mathf.Abs(radius - 126)));
                }
            borderTexture.SetPixels(pixels); borderTexture.Apply();
            borderSprite = Sprite.Create(borderTexture, new Rect(0, 0, 256, 256), new Vector2(.5f, .5f));
            foreach (var image in map.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "BorderLine")
                {
                    image.enabled = true; image.sprite = borderSprite; image.color = HudStyle.Amber;
                    var fund = ms.Fund_Ima.rectTransform;
                    // The vendor mask has 10% transparent padding; fit the ring to its visible circle.
                    image.rectTransform.sizeDelta = fund.rect.size * .9f + Vector2.one * 4;
                    image.rectTransform.pivot = new Vector2(.5f, .5f);
                    image.rectTransform.position = fund.TransformPoint(fund.rect.center);
                    image.raycastTarget = false;
                }
                else if (image.name == "Fund") image.color = HudStyle.Panel;
                else if (image.name == "AngleTextG")
                {
                    image.color = HudStyle.Panel; image.rectTransform.anchoredPosition = new Vector2(0, 150);
                }
                else if (image.name == "Radar") image.color = new Color(HudStyle.Amber.r, HudStyle.Amber.g, HudStyle.Amber.b, .15f);
            }
            foreach (var label in map.GetComponentsInChildren<Text>(true))
            {
                if (GameMenu.Instance != null && GameMenu.Instance.font != null) label.font = GameMenu.Instance.font;
                label.color = HudStyle.Ivory;
            }
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
            Vector2 smallPos = new Vector2(-size.x * 0.5f + margin + 153f * s, size.y * 0.5f - margin - 175f * s);
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
