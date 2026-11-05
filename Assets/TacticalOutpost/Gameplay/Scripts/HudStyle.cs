using TMPro;
using UnityEngine;

namespace Outpost.UI
{
    public static class HudStyle
    {
        public static Color Ivory => GameMenu.Ivory;
        public static Color Muted => GameMenu.Muted;
        public static Color Amber => GameMenu.Amber;
        public static Color Panel => GameMenu.Panel;
        public static readonly Color Danger = new Color(1f, .22f, .16f);
        static readonly TextAlignmentOptions[] Alignments = {
            TextAlignmentOptions.TopLeft, TextAlignmentOptions.Top, TextAlignmentOptions.TopRight,
            TextAlignmentOptions.MidlineLeft, TextAlignmentOptions.Center, TextAlignmentOptions.MidlineRight,
            TextAlignmentOptions.BottomLeft, TextAlignmentOptions.Bottom, TextAlignmentOptions.BottomRight };

        public static TMP_Text Text(string name, Transform parent, float size, TextAnchor alignment, Vector2 anchor, Vector2 position, Vector2 box, bool mono = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = box;
            var text = go.GetComponent<TextMeshProUGUI>();
            var menu = GameMenu.Instance;
            text.font = menu != null ? mono ? menu.monoFont : menu.headingFont : TMP_Settings.defaultFontAsset;
            text.fontSize = size; text.color = Ivory; text.alignment = Alignments[(int)alignment];
            text.raycastTarget = false; text.richText = true; text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }
        public static UnityEngine.UI.Image Box(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = go.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return image;
        }
    }
}
