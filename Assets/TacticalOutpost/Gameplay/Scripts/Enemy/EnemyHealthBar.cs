using UnityEngine;
using UnityEngine.UI;

namespace Outpost.Enemy
{
    /// <summary>
    /// Bar darah melayang di atas kepala musuh (world-space, selalu menghadap kamera).
    /// Sprite bar diambil dari aset "2D GUI Shooter" (disambungkan oleh builder).
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyHealthBar : MonoBehaviour
    {
        public EnemyHealth health;
        public Sprite barBackground;
        public Sprite barFill;
        public float width = 0.9f;             // meter
        public float heightAboveHead = 0.42f;  // meter (dikali visualScale)
        public float visibleDistance = 30f;
        public bool showName;                  // boss: tampilkan nama di atas bar
        public string label;

        Canvas canvas;
        Image fill, back;
        Text nameText;
        Transform follow;
        float shown;                           // 0..1 fade
        float damagedFlash;
        float lastFraction = 1f;
        Camera cam;

        void Awake()
        {
            if (health == null) health = GetComponent<EnemyHealth>();
            Build();
        }

        void Build()
        {
            const float pxW = 300f, pxH = 24f;
            var go = new GameObject("HealthBar", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 5;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(pxW, pxH);
            go.transform.localScale = Vector3.one * (width / pxW);
            go.layer = 2;   // Ignore Raycast: tidak menghalangi peluru

            back = NewImage("Bg", rt, pxW, pxH, barBackground, new Color(1f, 1f, 1f, 0.9f));
            fill = NewImage("Fill", rt, pxW - 6f, pxH - 8f, barFill != null ? barFill : barBackground, Color.white);
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 1f;

            if (showName)
            {
                var tgo = new GameObject("Name", typeof(RectTransform), typeof(Text));
                tgo.transform.SetParent(rt, false); tgo.layer = 2;
                var trt = (RectTransform)tgo.transform; trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0f); trt.pivot = new Vector2(0.5f, 0f);
                trt.anchoredPosition = new Vector2(0f, pxH + 2f); trt.sizeDelta = new Vector2(pxW * 2f, 40f);
                nameText = tgo.GetComponent<Text>();
                nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                nameText.fontSize = 30; nameText.alignment = TextAnchor.LowerCenter; nameText.color = new Color(1f, 0.82f, 0.4f, 1f);
                nameText.text = string.IsNullOrEmpty(label) ? "BOSS" : label.ToUpper(); nameText.raycastTarget = false;
                nameText.horizontalOverflow = HorizontalWrapMode.Overflow; nameText.verticalOverflow = VerticalWrapMode.Overflow;
                var sh = tgo.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.8f); sh.effectDistance = new Vector2(2f, -2f);
            }
            if (health != null) { follow = health.headBone != null ? health.headBone : transform; lastFraction = health.Fraction; }
            else follow = transform;
        }

        Image NewImage(string n, RectTransform parent, float w, float h, Sprite spr, Color c)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false); go.layer = 2;
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = Vector2.zero;
            var im = go.GetComponent<Image>(); im.sprite = spr; im.color = c; im.raycastTarget = false;
            return im;
        }

        void LateUpdate()
        {
            if (health == null || canvas == null) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            float sc = (health.config != null ? health.config.visualScale : 1f) * health.transform.localScale.y;
            Vector3 head = follow != null ? follow.position : transform.position + Vector3.up * 1.7f * sc;
            canvas.transform.position = new Vector3(head.x, Mathf.Max(head.y, transform.position.y + 1.2f * sc) + heightAboveHead * sc, head.z);
            canvas.transform.rotation = cam.transform.rotation;

            float f = health.Fraction;
            if (f < lastFraction - 0.001f) damagedFlash = 1f;
            lastFraction = f;
            damagedFlash = Mathf.MoveTowards(damagedFlash, 0f, 3f * Time.deltaTime);

            float dist = (cam.transform.position - head).magnitude;
            bool visible = !health.IsDead && dist <= visibleDistance;
            shown = Mathf.MoveTowards(shown, visible ? 1f : 0f, 5f * Time.deltaTime);
            canvas.enabled = shown > 0.01f;
            if (!canvas.enabled) return;

            fill.fillAmount = f;
            Color good = new Color(0.3f, 0.9f, 0.35f), bad = new Color(0.95f, 0.18f, 0.12f);
            Color c = f > 0.5f ? Color.Lerp(new Color(1f, 0.85f, 0.15f), good, (f - 0.5f) * 2f) : Color.Lerp(bad, new Color(1f, 0.85f, 0.15f), f * 2f);
            c = Color.Lerp(c, Color.white, damagedFlash * 0.7f);
            c.a = shown; fill.color = c;
            back.color = new Color(1f, 1f, 1f, 0.9f * shown);
            if (nameText != null) { var nc = nameText.color; nc.a = shown; nameText.color = nc; }
        }
    }
}
