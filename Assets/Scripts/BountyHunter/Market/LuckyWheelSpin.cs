using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The lucky wheel spinning up on the wall once a player has set it off on the floor (LuckyWheel):
    // the wheel springs up in the middle of the wall (left as it is, not dimmed), whirls round and
    // slows to a stop with the won segment under the pointer and thumps. Then the wheel fades away
    // while the won result's icon pops up big in its place in a glow of the segment's colour (Landed
    // is called then), with what it is written under it on a dark torn banner: its title and a line
    // saying what it does, lettered like the wall's other words (the outlined font, the tutorial's
    // colours). It holds a moment and it all shrinks away. The wheel picture has its segments laid
    // clockwise round its middle, the first one centred at the top, each segmentCount-th of the way
    // round; its icons reach out past the disc, whose rim is RimVsWheel of the way out, where the
    // pointer sits.
    public sealed class LuckyWheelSpin : MonoBehaviour
    {
        private const int Turns = 5;
        private const float AppearSeconds = 0.35f;
        // Long enough after landing to read what was won.
        private const float HoldSeconds = 2.8f;
        private const float GoneSeconds = 0.35f;
        private const float PopSeconds = 0.4f;
        private const float ThumpSeconds = 0.3f;
        // The wheel fades away between these times after landing, while the icon pops up.
        private const float WheelFadeFrom = 0.15f;
        private const float WheelFadeTo = 0.5f;
        // The words come in between these times after landing, rising this much of the wall's height.
        private const float CaptionFrom = 0.25f;
        private const float CaptionTo = 0.6f;
        private const float CaptionRise = 0.04f;
        // The disc's height against the wall's (the icons reaching out past it come on top), the
        // pointer's and the glow's against the disc's, and how much it swells as it thumps to a stop.
        private const float WheelVsWall = 0.86f;
        private const float PointerVsWheel = 0.2f;
        private const float RimVsWheel = 0.89f;
        private const float ThumpSize = 0.12f;
        private const float GlowVsWheel = 1.25f;
        // The won icon: its longest side and how far above the middle it sits, against the wall's
        // height. The banner, the title and the line on it: their heights and where their middles sit.
        private const float IconVsWall = 0.5f;
        private const float IconRaise = 0.1f;
        private const float BannerVsWall = 0.34f;
        private const float BannerAt = -0.31f;
        private const float TitleVsWall = 0.12f;
        private const float TitleAt = -0.26f;
        private const float LineVsWall = 0.065f;
        private const float LineAt = -0.37f;
        // The tutorial board's title and words colours.
        private static readonly Color TitleColour = Color.white;
        private static readonly Color LineColour = new Color(0.82f, 0.86f, 1f, 1f);

        private Image wheel;
        private Image pointer;
        private RectTransform icon;
        private CanvasGroup caption;
        private Vector2 captionHome;
        private float wallHeight;
        private Image glow;
        private float spinSeconds;
        private float endAngle;
        private float bornAt;
        private Action landed;
        private bool hasLanded;

        // On the wall (wall, the area it fills), stopping on segment (0 centred at the top, then
        // clockwise) of segmentCount, then showing iconPicture big in colour with title and line (what
        // it does) under it on banner, lettered in font with fontMaterial, and calling landed.
        public static void Play(RectTransform wall, Sprite wheelPicture, Sprite pointerPicture, Sprite iconPicture,
            int segment, int segmentCount, float spinSeconds, Color colour, string title, string line, Sprite banner,
            TMP_FontAsset font, Material fontMaterial, Action landed)
        {
            var root = new GameObject("Lucky Wheel Spin", typeof(RectTransform));
            var area = (RectTransform)root.transform;
            area.SetParent(wall, false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = Vector2.zero;
            area.offsetMax = Vector2.zero;
            area.SetAsLastSibling();

            var spin = root.AddComponent<LuckyWheelSpin>();
            spin.spinSeconds = Mathf.Max(0.1f, spinSeconds);
            spin.landed = landed;
            spin.bornAt = Time.time;
            spin.wallHeight = wall.rect.height;
            // Turning the wheel by a segment's clockwise angle brings it to the top, under the pointer.
            float segmentAngle = 360f / Mathf.Max(1, segmentCount);
            spin.endAngle = Turns * 360f + segmentAngle * segment;

            // size is the disc's; the picture is that much bigger again for the icons past its rim.
            float size = spin.wallHeight * WheelVsWall;
            float pictureSize = size / RimVsWheel;
            spin.glow = Picture(area, "Glow", PosterSprites.Dot, new Vector2(size * GlowVsWheel, size * GlowVsWheel));
            Color glowColour = colour;
            glowColour.a = 0f;
            spin.glow.color = glowColour;
            spin.wheel = Picture(area, "Wheel", wheelPicture, new Vector2(pictureSize, pictureSize));
            spin.pointer = Picture(area, "Pointer", pointerPicture, Fit(pointerPicture, size * PointerVsWheel));
            spin.pointer.rectTransform.anchoredPosition = new Vector2(0f, size * 0.5f);

            spin.icon = Picture(area, "Won", iconPicture, Fit(iconPicture, spin.wallHeight * IconVsWall)).rectTransform;
            spin.icon.anchoredPosition = new Vector2(0f, spin.wallHeight * IconRaise);
            spin.icon.gameObject.SetActive(false);
            spin.glow.rectTransform.anchoredPosition = spin.icon.anchoredPosition;

            var words = new GameObject("Caption", typeof(RectTransform), typeof(CanvasGroup));
            var wordsRect = (RectTransform)words.transform;
            wordsRect.SetParent(area, false);
            wordsRect.sizeDelta = new Vector2(wall.rect.width, spin.wallHeight);
            spin.caption = words.GetComponent<CanvasGroup>();
            spin.caption.alpha = 0f;
            spin.captionHome = wordsRect.anchoredPosition;
            if (banner != null)
            {
                float bannerHeight = spin.wallHeight * BannerVsWall;
                Image back = Picture(wordsRect, "Banner", banner, new Vector2(bannerHeight * banner.rect.width / Mathf.Max(1f, banner.rect.height), bannerHeight));
                back.rectTransform.anchoredPosition = new Vector2(0f, spin.wallHeight * BannerAt);
            }

            Label(wordsRect, "Title", title, font, fontMaterial, TitleColour, 2f, spin.wallHeight * TitleVsWall, spin.wallHeight * TitleAt);
            Label(wordsRect, "Line", line, font, fontMaterial, LineColour, 1f, spin.wallHeight * LineVsWall, spin.wallHeight * LineAt);
            spin.Show();
        }

        private void Update()
        {
            Show();
        }

        private void Show()
        {
            float elapsed = Time.time - bornAt;
            float spun = Mathf.Clamp01(elapsed / spinSeconds);
            // Fast at first, easing into its stop.
            float turned = 1f - Mathf.Pow(1f - spun, 3f);
            wheel.rectTransform.localRotation = Quaternion.Euler(0f, 0f, endAngle * turned);
            if (spun >= 1f && !hasLanded) Land();

            float afterLanding = elapsed - spinSeconds;
            float gone = Mathf.Clamp01((afterLanding - HoldSeconds) / GoneSeconds);
            if (gone >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            float appear = EaseOutBack(Mathf.Clamp01(elapsed / AppearSeconds));
            float shrink = 1f - Mathf.SmoothStep(0f, 1f, gone);
            float thump = hasLanded && afterLanding < ThumpSeconds ? ThumpSize * Mathf.Sin(afterLanding / ThumpSeconds * Mathf.PI) : 0f;
            // After the thump the wheel and its pointer fade away behind the icon.
            float wheelShown = hasLanded ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(WheelFadeFrom, WheelFadeTo, afterLanding)) : 1f;
            wheel.rectTransform.localScale = Vector3.one * (appear * shrink * (1f + thump));
            wheel.color = new Color(1f, 1f, 1f, wheelShown);
            // The pointer ticks as segments pass under it, less as the wheel slows.
            float tick = spun < 1f ? 12f * (1f - spun) * Mathf.Sin(endAngle * turned / 90f * Mathf.PI * 2f) : 0f;
            pointer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tick);
            pointer.rectTransform.localScale = Vector3.one * (appear * shrink);
            pointer.color = new Color(1f, 1f, 1f, wheelShown);
            if (!hasLanded) return;

            // The icon springs up big in a glow, then settles and goes with the rest.
            float pop = EaseOutBack(Mathf.Clamp01(afterLanding / PopSeconds));
            icon.localScale = Vector3.one * (Mathf.LerpUnclamped(0.2f, 1f, pop) * shrink);
            Color glowColour = glow.color;
            glowColour.a = 0.8f * Mathf.Clamp01(afterLanding / PopSeconds) * shrink;
            glow.color = glowColour;
            glow.rectTransform.localScale = Vector3.one * (0.9f + 0.1f * Mathf.Sin(afterLanding * 6f));

            // The words rise into place under it.
            float words = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CaptionFrom, CaptionTo, afterLanding));
            caption.alpha = words * shrink;
            ((RectTransform)caption.transform).anchoredPosition = captionHome + Vector2.down * (wallHeight * CaptionRise * (1f - words));
        }

        private void Land()
        {
            hasLanded = true;
            icon.gameObject.SetActive(true);
            landed?.Invoke();
        }

        // Runs a little past 1 before settling on it.
        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float back = Mathf.Clamp01(t) - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }

        // One line of words across parent, height tall, its middle at y, letters spacing apart.
        private static void Label(RectTransform parent, string name, string text, TMP_FontAsset font, Material material, Color colour,
            float spacing, float height, float y)
        {
            if (string.IsNullOrEmpty(text)) return;

            var piece = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)piece.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(parent.sizeDelta.x, height * 1.3f);
            rect.anchoredPosition = new Vector2(0f, y);
            var label = piece.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            if (material != null) label.fontSharedMaterial = material;
            label.text = text;
            label.fontSize = height;
            label.color = colour;
            label.characterSpacing = spacing;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
        }

        // A sprite's size with its longer side this long.
        private static Vector2 Fit(Sprite sprite, float longest)
        {
            if (sprite == null) return new Vector2(longest, longest);

            Vector2 size = sprite.rect.size;
            float scale = longest / Mathf.Max(1f, Mathf.Max(size.x, size.y));
            return size * scale;
        }

        private static Image Picture(RectTransform parent, string name, Sprite sprite, Vector2 size)
        {
            var piece = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)piece.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            var picture = piece.GetComponent<Image>();
            picture.sprite = sprite;
            picture.raycastTarget = false;
            return picture;
        }
    }
}
