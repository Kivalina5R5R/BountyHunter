using System;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The lucky wheel spinning up on the wall once a player has set it off on the floor (LuckyWheel):
    // the wall dims, the wheel springs up in its middle, whirls round and slows to a stop with the won
    // segment under the pointer, then thumps and glows in the segment's colour while the won result's
    // icon pops up big in front of it (Landed is called then), holds a moment and it all shrinks away. The
    // wheel picture has its segments laid clockwise from the top, each segmentCount-th of the way
    // round (Tools/MarketArt).
    public sealed class LuckyWheelSpin : MonoBehaviour
    {
        private const int Turns = 5;
        private const float AppearSeconds = 0.35f;
        private const float HoldSeconds = 1.6f;
        private const float GoneSeconds = 0.35f;
        private const float PopSeconds = 0.4f;
        private const float ThumpSeconds = 0.3f;
        private const float DimAlpha = 0.55f;
        // The wheel's height against the wall's, and the icon's against the wheel's.
        private const float WheelVsWall = 0.86f;
        private const float IconVsWheel = 0.7f;

        private Image dim;
        private RectTransform wheel;
        private RectTransform pointer;
        private RectTransform icon;
        private Image glow;
        private float spinSeconds;
        private float endAngle;
        private float bornAt;
        private Action landed;
        private bool hasLanded;

        // On the wall (wall, the area it fills), stopping on segment (0 at the top, then clockwise)
        // of segmentCount, showing iconPicture in colour as it lands and calling landed then.
        public static void Play(RectTransform wall, Sprite wheelPicture, Sprite pointerPicture, Sprite iconPicture,
            int segment, int segmentCount, float spinSeconds, Color colour, Action landed)
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
            // Turning the wheel by a segment's clockwise angle brings it to the top, under the pointer.
            float segmentAngle = 360f / Mathf.Max(1, segmentCount);
            spin.endAngle = Turns * 360f + segmentAngle * (segment + 0.5f);

            float size = wall.rect.height * WheelVsWall;
            spin.dim = Picture(area, "Dim", null, Vector2.zero);
            RectTransform dimRect = spin.dim.rectTransform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.sizeDelta = Vector2.zero;
            spin.dim.color = Color.clear;

            spin.glow = Picture(area, "Glow", PosterSprites.Dot, new Vector2(size * 1.25f, size * 1.25f));
            Color glowColour = colour;
            glowColour.a = 0f;
            spin.glow.color = glowColour;
            spin.wheel = Picture(area, "Wheel", wheelPicture, new Vector2(size, size)).rectTransform;
            spin.pointer = Picture(area, "Pointer", pointerPicture, Fit(pointerPicture, size * 0.2f)).rectTransform;
            spin.pointer.anchoredPosition = new Vector2(0f, size * 0.5f);
            spin.icon = Picture(area, "Won", iconPicture, Fit(iconPicture, size * IconVsWheel)).rectTransform;
            spin.icon.gameObject.SetActive(false);
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
            wheel.localRotation = Quaternion.Euler(0f, 0f, endAngle * turned);
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
            float thump = hasLanded && afterLanding < ThumpSeconds ? 0.12f * Mathf.Sin(afterLanding / ThumpSeconds * Mathf.PI) : 0f;
            wheel.localScale = Vector3.one * (appear * shrink * (1f + thump));
            // The pointer ticks as segments pass under it, less as the wheel slows.
            float tick = spun < 1f ? 12f * (1f - spun) * Mathf.Sin(endAngle * turned / 90f * Mathf.PI * 2f) : 0f;
            pointer.localRotation = Quaternion.Euler(0f, 0f, tick);
            pointer.localScale = Vector3.one * (appear * shrink);
            dim.color = new Color(0f, 0f, 0f, DimAlpha * Mathf.Clamp01(elapsed / AppearSeconds) * shrink);
            if (!hasLanded) return;

            // The icon springs up big in front of the wheel in a glow, then settles and goes with it.
            float pop = EaseOutBack(Mathf.Clamp01(afterLanding / PopSeconds));
            icon.localScale = Vector3.one * (Mathf.LerpUnclamped(0.2f, 1f, pop) * shrink);
            Color glowColour = glow.color;
            glowColour.a = 0.8f * Mathf.Clamp01(afterLanding / PopSeconds) * shrink;
            glow.color = glowColour;
            glow.rectTransform.localScale = Vector3.one * (0.9f + 0.1f * Mathf.Sin(afterLanding * 6f));
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
