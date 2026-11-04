using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // What to do in the vault's blackout, written on the wall so the players know to run to a light
    // switch: a title and a line on a dark torn banner across the lower part of the wall, lettered like
    // the wall's other words. Show springs it up (or, already up, gives it a bounce with the new
    // words); while up, the title gently pulses; Hide shrinks it away.
    public sealed class VaultLightsHint : MonoBehaviour
    {
        // The banner's height against the wall's and where its middle sits (share of the wall's
        // height below the middle); the title's and line's heights and their places on the banner.
        private const float BannerVsWall = 0.34f;
        private const float BannerAt = -0.24f;
        private const float TitleVsWall = 0.12f;
        private const float TitleAt = 0.05f;
        private const float LineVsWall = 0.062f;
        private const float LineAt = -0.06f;
        private const float PopSeconds = 0.4f;
        private const float HideSeconds = 0.3f;
        private const float PulsesPerSecond = 1.2f;
        private const float PulseSize = 0.05f;
        // The tutorial board's title and words colours.
        private static readonly Color TitleColour = new Color(1f, 0.8f, 0.13f, 1f);
        private static readonly Color LineColour = Color.white;

        private RectTransform board;
        private TextMeshProUGUI title;
        private TextMeshProUGUI line;
        private float shownAt = float.MinValue;
        private float hiddenAt = float.MinValue;
        private bool shown;

        // Across the wall (the area it fills), on banner (may be null for none), in font with its material.
        public static VaultLightsHint Create(RectTransform wall, Sprite banner, TMP_FontAsset font, Material fontMaterial)
        {
            var root = new GameObject("Lights Out Hint", typeof(RectTransform));
            var area = (RectTransform)root.transform;
            area.SetParent(wall, false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = Vector2.zero;
            area.offsetMax = Vector2.zero;
            area.SetAsLastSibling();

            var hint = root.AddComponent<VaultLightsHint>();
            float height = wall.rect.height;
            var boardObject = new GameObject("Board", typeof(RectTransform));
            hint.board = (RectTransform)boardObject.transform;
            hint.board.SetParent(area, false);
            hint.board.anchoredPosition = new Vector2(0f, height * BannerAt);
            float bannerHeight = height * BannerVsWall;
            float bannerWidth = banner != null ? bannerHeight * banner.rect.width / Mathf.Max(1f, banner.rect.height) : wall.rect.width * 0.5f;
            hint.board.sizeDelta = new Vector2(bannerWidth, bannerHeight);
            if (banner != null)
            {
                var back = new GameObject("Banner", typeof(RectTransform), typeof(Image));
                var backRect = (RectTransform)back.transform;
                backRect.SetParent(hint.board, false);
                backRect.anchorMin = Vector2.zero;
                backRect.anchorMax = Vector2.one;
                backRect.offsetMin = Vector2.zero;
                backRect.offsetMax = Vector2.zero;
                var picture = back.GetComponent<Image>();
                picture.sprite = banner;
                picture.raycastTarget = false;
            }

            hint.title = Label(hint.board, "Title", font, fontMaterial, TitleColour, 2f, height * TitleVsWall, height * TitleAt);
            hint.line = Label(hint.board, "Line", font, fontMaterial, LineColour, 1f, height * LineVsWall, height * LineAt);
            hint.board.localScale = Vector3.zero;
            return hint;
        }

        public void Show(string titleText, string lineText)
        {
            title.text = titleText;
            line.text = lineText;
            shownAt = Time.time;
            shown = true;
        }

        public void Hide()
        {
            if (!shown) return;

            shown = false;
            hiddenAt = Time.time;
        }

        private void Update()
        {
            if (!shown)
            {
                float gone = Mathf.Clamp01((Time.time - hiddenAt) / HideSeconds);
                board.localScale = Vector3.one * (1f - Mathf.SmoothStep(0f, 1f, gone));
                return;
            }

            float popped = EaseOutBack(Mathf.Clamp01((Time.time - shownAt) / PopSeconds));
            board.localScale = Vector3.one * popped;
            float pulse = 1f + PulseSize * Mathf.Sin((Time.time - shownAt) * PulsesPerSecond * Mathf.PI * 2f);
            title.rectTransform.localScale = Vector3.one * pulse;
        }

        // Runs a little past 1 before settling on it; from 0.3 so a fresh Show bounces rather than vanishing.
        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float back = Mathf.Clamp01(t) - 1f;
            return Mathf.Lerp(0.3f, 1f, 1f + (overshoot + 1f) * back * back * back + overshoot * back * back);
        }

        // One line of words across parent, height tall, its middle at y, letters spacing apart.
        private static TextMeshProUGUI Label(RectTransform parent, string name, TMP_FontAsset font, Material material, Color colour,
            float spacing, float height, float y)
        {
            var piece = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)piece.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(parent.sizeDelta.x * 0.92f, height * 1.3f);
            rect.anchoredPosition = new Vector2(0f, y);
            var label = piece.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            if (material != null) label.fontSharedMaterial = material;
            label.fontSize = height;
            label.color = colour;
            label.characterSpacing = spacing;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = height * 0.5f;
            label.fontSizeMax = height;
            label.raycastTarget = false;
            return label;
        }
    }
}
