using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using MiniMapModular;
using Outpost.Enemy;
using Outpost.Final;
using Outpost.Pickups;

namespace Outpost.UI
{
    /// <summary>
    /// Menampilkan ikon sekutu (VIPER, ECHO, NOMAD) di minimap & peta besar (aset Minimap Modular Radar).
    /// Memakai ikon chevron taktis prosedural beresolusi tinggi yang berputar mengikuti arah hadap sekutu,
    /// panah tepi radar bila sekutu di luar jangkauan, dan warna reaktif terhadap status aksi:
    ///   - Aktif / Tempur : Cyan taktis
    ///   - Repair         : Hijau-cyan (sedang memperbaiki generator)
    ///   - Down           : Merah berkedip (tumbang)
    /// </summary>
    public sealed class AllyMinimapBlip : MonoBehaviour
    {
        public Color activeColor = new Color(0.22f, 0.90f, 1.00f, 1f);
        public Color repairColor = new Color(0.25f, 1.00f, 0.65f, 1f);
        public Color downColor = new Color(1.00f, 0.30f, 0.25f, 1f);

        AllyBrain brain;
        Aux_MP aux;
        MinimapBlip template;
        Color currentColor;
        bool built;

        static Texture2D sharedTex;
        static Sprite sharedSprite;
        static readonly FieldInfo AuxImageField = typeof(Aux_MP).GetField("AuxIm", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo AuxArrowGoField = typeof(Aux_MP).GetField("Aux_Icon_A", BindingFlags.Instance | BindingFlags.NonPublic);

        public static AllyMinimapBlip Attach(GameObject allyGo, Color tint)
        {
            if (allyGo == null) return null;
            var blip = allyGo.GetComponent<AllyMinimapBlip>();
            if (blip == null) blip = allyGo.AddComponent<AllyMinimapBlip>();
            blip.activeColor = tint;
            return blip;
        }

        void Awake()
        {
            brain = GetComponent<AllyBrain>();
        }

        void OnEnable()
        {
            TryBuild();
        }

        void LateUpdate()
        {
            if (!built) TryBuild();
            if (aux == null) return;

            bool large = MinimapController.Instance != null && MinimapController.Instance.IsLarge;
            aux.IconSizeMultiplier = large ? 0.36f : 0.44f;
            aux.ArrowSizeMultiplier = large ? 0.34f : 0.46f;

            Color targetColor = ResolveStateColor();
            if (targetColor != currentColor)
            {
                currentColor = targetColor;
                ApplyLiveTint(targetColor);
            }
        }

        void OnDestroy()
        {
            if (aux != null) Destroy(aux);
        }

        Color ResolveStateColor()
        {
            if (brain == null) return activeColor;
            if (brain.Action == AllyAction.Down)
            {
                float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 7f);
                return Color.Lerp(downColor * 0.55f, downColor, pulse);
            }
            if (brain.Action == AllyAction.Repair) return repairColor;
            return activeColor;
        }

        void ApplyLiveTint(Color c)
        {
            if (aux == null) return;
            c.a = 1f;
            aux.Icon_C = c;
            aux.Arrow_C = c;
            if (AuxImageField != null)
            {
                var img = AuxImageField.GetValue(aux) as Image;
                if (img != null) img.color = c;
            }
            if (AuxArrowGoField != null)
            {
                var arrowGo = AuxArrowGoField.GetValue(aux) as GameObject;
                if (arrowGo != null)
                {
                    var ai = arrowGo.GetComponent<Aux_Icon>();
                    if (ai != null && ai.Local_Icon != null) ai.Local_Icon.color = c;
                }
            }
        }

        void TryBuild()
        {
            if (built) return;
            if (GameObject.Find("Cam_Map") == null) return;
            if (template == null) template = FindTemplate();
            if (template == null || template.iconPrefab == null) return;

            EnsureSprite();
            currentColor = ResolveStateColor();

            aux = gameObject.AddComponent<Aux_MP>();
            aux.Icon = template.iconPrefab;
            aux.Icon_Arrow = template.arrowPrefab;
            aux.Arrow_Icon = template.arrowIconPrefab;
            aux.Sprite_Me = sharedSprite;
            aux.Icon_C = currentColor;
            aux.SizeIconAdd = 0.42f;
            aux.Arrow = template.arrowPrefab != null ? 1 : 0;
            aux.Icon_Dir = 0;
            aux.HiddenIcon = 0;
            aux.RotIconMe = 1;                // putar chevron mengikuti arah hadap VIPER
            aux.LayerIconView = 2;            // tampil di atas ikon zombie & pickup
            aux.Sprite_Arrow = template.arrowSprite;
            aux.Arrow_C = currentColor;
            built = true;
        }

        static MinimapBlip FindTemplate()
        {
            var drops = FindFirstObjectByType<DropManager>();
            if (drops != null)
            {
                if (drops.ammoPrefab != null)
                {
                    var b = drops.ammoPrefab.GetComponent<MinimapBlip>();
                    if (b != null && b.iconPrefab != null) return b;
                }
                if (drops.healthPrefab != null)
                {
                    var b = drops.healthPrefab.GetComponent<MinimapBlip>();
                    if (b != null && b.iconPrefab != null) return b;
                }
            }

            var wd = WaveDirector.Instance;
            if (wd != null && wd.catalog != null && wd.catalog.variants != null)
            {
                for (int i = 0; i < wd.catalog.variants.Count; i++)
                {
                    var pref = wd.catalog.variants[i].prefab;
                    if (pref == null) continue;
                    var b = pref.GetComponentInChildren<MinimapBlip>(true);
                    if (b != null && b.iconPrefab != null) return b;
                }
            }

            foreach (var b in FindObjectsByType<MinimapBlip>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b != null && b.iconPrefab != null) return b;

            return null;
        }

        /// <summary>Buat sprite chevron/panah sekutu 128x128 anti-aliased dengan outline gelap agar sangat tajam di minimap.</summary>
        static void EnsureSprite()
        {
            if (sharedSprite != null && sharedTex != null) return;
            const int S = 128;
            sharedTex = new Texture2D(S, S, TextureFormat.RGBA32, false)
            {
                name = "Minimap_AllyViper_Icon",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[S * S];
            Vector2 tip = new Vector2(0f, 0.78f);
            Vector2 wingR = new Vector2(0.62f, -0.58f);
            Vector2 wingL = new Vector2(-0.62f, -0.58f);
            Vector2 notch = new Vector2(0f, -0.22f);

            for (int y = 0; y < S; y++)
            {
                float ny = ((y + 0.5f) / S) * 2f - 1f;
                for (int x = 0; x < S; x++)
                {
                    float nx = ((x + 0.5f) / S) * 2f - 1f;
                    Vector2 p = new Vector2(nx, ny);

                    // Signed distance ke bentuk chevron/panah taktis (union dua segitiga: kiri & kanan)
                    float dLeft = SdTriangle(p, tip, wingL, notch);
                    float dRight = SdTriangle(p, tip, notch, wingR);
                    float dArrow = Mathf.Min(dLeft, dRight);

                    // Ring tipis melingkar di sekeliling chevron supaya langsung terbaca sebagai sekutu (VIPER)
                    float r = p.magnitude;
                    float dRing = Mathf.Abs(r - 0.86f) - 0.045f;
                    float dShape = Mathf.Min(dArrow, dRing);

                    const float aa = 0.032f;
                    float fill = Mathf.Clamp01(0.5f - dShape / aa);
                    float outline = Mathf.Clamp01(0.5f - (dShape - 0.085f) / aa);

                    if (outline <= 0.001f)
                    {
                        pixels[y * S + x] = Color.clear;
                    }
                    else
                    {
                        // Isi putih murni (akan di-tint cyan/hijau/merah oleh Aux_MP), outline gelap untuk kontras
                        Color darkBg = new Color(0.05f, 0.08f, 0.12f, 0.92f);
                        Color col = Color.Lerp(darkBg, Color.white, fill);
                        col.a = Mathf.Max(fill, outline * 0.92f);
                        pixels[y * S + x] = col;
                    }
                }
            }

            sharedTex.SetPixels(pixels);
            sharedTex.Apply();
            sharedSprite = Sprite.Create(sharedTex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        }

        static float SdTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 e0 = b - a, e1 = c - b, e2 = a - c;
            Vector2 v0 = p - a, v1 = p - b, v2 = p - c;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            Vector2 d = Vector2.Min(
                Vector2.Min(new Vector2(Vector2.Dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                            new Vector2(Vector2.Dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
                new Vector2(Vector2.Dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
            return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
        }
    }
}
