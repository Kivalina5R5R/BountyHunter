using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // A red rubber-stamp mark ("CAUGHT" in a box) slammed across a WANTED poster's picture when
    // its thief is caught. Built on the poster itself so it turns and fades with it.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PosterStamp : MonoBehaviour
    {
        // Picture centre and stamp size as parts of the poster's own width and height.
        private const float PictureHeight = 0.12f;
        private const float FontSizeVsWidth = 0.2f;
        private const float BorderVsWidth = 0.018f;
        private const float Angle = -14f;

        private RectTransform rect;
        private CanvasGroup group;

        public static PosterStamp Create(RectTransform poster, string text, Color color, TMP_FontAsset font)
        {
            Vector2 size = poster.rect.size;
            var stampObject = new GameObject("Stamp", typeof(RectTransform), typeof(CanvasGroup));
            stampObject.transform.SetParent(poster, false);
            var stamp = stampObject.AddComponent<PosterStamp>();
            stamp.rect = (RectTransform)stampObject.transform;
            stamp.group = stampObject.GetComponent<CanvasGroup>();
            stamp.group.blocksRaycasts = false;
            stamp.rect.anchoredPosition = new Vector2(0f, size.y * PictureHeight);
            stamp.rect.localRotation = Quaternion.Euler(0f, 0f, Angle);

            float fontSize = size.x * FontSizeVsWidth;
            TextMeshProUGUI label = AddLabel(stamp.rect, text, color, font, fontSize);
            float border = size.x * BorderVsWidth;
            var box = new Vector2(label.preferredWidth + fontSize * 0.5f, fontSize * 1.15f);
            AddBox(stamp.rect, box, border, color);
            stampObject.SetActive(false);
            return stamp;
        }

        private static TextMeshProUGUI AddLabel(RectTransform parent, string text, Color color, TMP_FontAsset font, float fontSize)
        {
            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.characterSpacing = 8f;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.rectTransform.sizeDelta = new Vector2(label.preferredWidth, fontSize * 1.2f);
            return label;
        }

        // A hollow rectangle drawn as four bars.
        private static void AddBox(RectTransform parent, Vector2 size, float thickness, Color color)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            AddBar(parent, new Vector2(0f, halfHeight), new Vector2(size.x + thickness, thickness), color);
            AddBar(parent, new Vector2(0f, -halfHeight), new Vector2(size.x + thickness, thickness), color);
            AddBar(parent, new Vector2(-halfWidth, 0f), new Vector2(thickness, size.y + thickness), color);
            AddBar(parent, new Vector2(halfWidth, 0f), new Vector2(thickness, size.y + thickness), color);
        }

        private static void AddBar(RectTransform parent, Vector2 position, Vector2 size, Color color)
        {
            var barObject = new GameObject("Border", typeof(RectTransform), typeof(Image));
            barObject.transform.SetParent(parent, false);
            var bar = barObject.GetComponent<Image>();
            bar.color = color;
            bar.raycastTarget = false;
            bar.rectTransform.anchoredPosition = position;
            bar.rectTransform.sizeDelta = size;
        }

        public void Pose(float scale, float alpha)
        {
            gameObject.SetActive(true);
            rect.localScale = Vector3.one * scale;
            group.alpha = alpha;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
