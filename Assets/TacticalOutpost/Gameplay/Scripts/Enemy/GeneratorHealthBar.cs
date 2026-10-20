using UnityEngine;
using UnityEngine.UI;

namespace Outpost.Enemy
{
    /// <summary>
    /// Bar darah BESAR melayang di atas generator (world-space, selalu menghadap kamera, terlihat dari jauh).
    /// Menempel di objek yang sama dengan GeneratorHealth. Ukuran & tinggi bisa diatur di Inspector.
    /// Posisi otomatis mengikuti bagian paling atas collider generator, jadi tetap pas walau ukuran generator diubah.
    /// </summary>
    [DisallowMultipleComponent]
    public class GeneratorHealthBar : MonoBehaviour
    {
        public GeneratorHealth generator;
        public Sprite barBackground;
        public Sprite barFill;
        public float width = 12f;              // lebar bar (meter)
        public float heightAboveTop = 2.0f;    // jarak bar di atas sisi atas generator (meter)
        public string label = "GENERATOR";

        Canvas canvas;
        Image fill, back;
        Text nameText, hpText;
        float damagedFlash, lastFraction = 1f;
        Camera cam;

        const float PxW = 600f, PxH = 54f;

        void Awake()
        {
            if (generator == null) generator = GetComponent<GeneratorHealth>();
            Build();
        }

        void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }

        void Build()
        {
            var go = new GameObject("GeneratorHealthBar", typeof(RectTransform), typeof(Canvas));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 10;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(PxW, PxH);
            go.transform.localScale = Vector3.one * (width / PxW);
            go.layer = 2;   // Ignore Raycast: tidak menghalangi peluru

            back = NewImage("Bg", rt, PxW, PxH, barBackground, new Color(1f, 1f, 1f, 0.95f));
            fill = NewImage("Fill", rt, PxW - 10f, PxH - 14f, barFill != null ? barFill : barBackground, Color.white);
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 1f;

            nameText = NewText("Name", rt, 56, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, PxH + 4f), new Vector2(PxW * 2f, 70f));
            nameText.text = label; nameText.color = new Color(0.55f, 0.92f, 1f, 1f);
            hpText = NewText("HP", rt, 36, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PxW, PxH));
            hpText.color = Color.white;
            if (generator != null) lastFraction = generator.Fraction;
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

        Text NewText(string n, RectTransform parent, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false); go.layer = 2;
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = box;
            var tx = go.GetComponent<Text>();
            tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tx.fontSize = size; tx.alignment = align; tx.raycastTarget = false;
            tx.horizontalOverflow = HorizontalWrapMode.Overflow; tx.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = go.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.85f); sh.effectDistance = new Vector2(3f, -3f);
            return tx;
        }

        void LateUpdate()
        {
            if (generator == null || canvas == null) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            // posisi: tepat di atas sisi atas collider generator
            Vector3 c = generator.transform.position; float top = c.y + 2f;
            if (generator.Body != null) { var b = generator.Body.bounds; c = b.center; top = b.max.y; }
            canvas.transform.position = new Vector3(c.x, top + heightAboveTop, c.z);
            canvas.transform.rotation = cam.transform.rotation;

            float f = generator.Fraction;
            if (f < lastFraction - 0.001f) damagedFlash = 1f;
            lastFraction = f;
            damagedFlash = Mathf.MoveTowards(damagedFlash, 0f, 2.5f * Time.deltaTime);

            fill.fillAmount = f;
            Color good = new Color(0.3f, 0.9f, 0.35f), bad = new Color(0.95f, 0.18f, 0.12f);
            Color col = f > 0.5f ? Color.Lerp(new Color(1f, 0.85f, 0.15f), good, (f - 0.5f) * 2f) : Color.Lerp(bad, new Color(1f, 0.85f, 0.15f), f * 2f);
            if (f < 0.25f && f > 0f) col = Color.Lerp(col, Color.white, 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.time * 9f)));   // berkedip saat kritis
            fill.color = Color.Lerp(col, Color.white, damagedFlash * 0.7f);
            hpText.text = Mathf.CeilToInt(generator.Health) + " / " + Mathf.CeilToInt(generator.maxHealth);
            nameText.text = generator.IsAlive ? label : label + " OFFLINE";
        }
    }
}
