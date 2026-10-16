using TMPro;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Shows on the floor that a player was hurt: a red shock ring bursts out under their feet and a
    // damage number ("-7$") pops up there, shakes, then drifts toward the wall and fades. Lies flat
    // on the floor like the other effects, with its top toward the wall, and removes itself when done.
    public sealed class FloorDamage : MonoBehaviour
    {
        private const float Height = 0.09f;
        private const float Seconds = 1.2f;
        private const float PopSeconds = 0.22f;
        private const float ShakeSeconds = 0.3f;
        private const float RingSeconds = 0.4f;
        // Text height and drift, in world units (a player marker is 0.665 wide).
        private const float FontSize = 4.2f;
        private const float Drift = 0.55f;
        // Starts this far toward the wall from the feet so the marker does not cover it.
        private const float StartOffset = 0.35f;
        private const float RingSize = 1.5f;
        // The ring under the player marker (20), the number over it.
        private const int RingOrder = 19;
        private const int TextOrder = 22;

        private TextMeshPro text;
        private SpriteRenderer ring;
        private float elapsed;

        // font and material may be null for TextMesh Pro's defaults.
        public static void Play(Vector3 feet, string message, Color color, TMP_FontAsset font, Material material, Transform parent)
        {
            var damage = new GameObject("Floor Damage").AddComponent<FloorDamage>();
            damage.transform.SetParent(parent, true);
            damage.transform.SetPositionAndRotation(new Vector3(feet.x, Height, feet.z), Quaternion.Euler(90f, 0f, 0f));
            damage.Build(message, color, font, material);
        }

        private void Build(string message, Color color, TMP_FontAsset font, Material material)
        {
            var ringObject = new GameObject("Shock Ring", typeof(SpriteRenderer));
            ringObject.transform.SetParent(transform, false);
            ring = ringObject.GetComponent<SpriteRenderer>();
            ring.sprite = PosterSprites.Dot;
            ring.color = color;
            ring.sortingOrder = RingOrder;

            var textObject = new GameObject("Number", typeof(RectTransform));
            textObject.transform.SetParent(transform, false);
            text = textObject.AddComponent<TextMeshPro>();
            if (font != null) text.font = font;
            if (material != null) text.fontSharedMaterial = material;
            text.text = message;
            text.fontSize = FontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(3f, 1f);
            text.renderer.sortingOrder = TextOrder;
            Apply();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= Seconds)
            {
                Destroy(gameObject);
                return;
            }

            Apply();
        }

        // The object is turned flat, so its local up (y) points toward the wall.
        private void Apply()
        {
            float u = elapsed / Seconds;
            float shake = elapsed < ShakeSeconds ? Mathf.Sin(elapsed * 70f) * 0.05f * (1f - elapsed / ShakeSeconds) : 0f;
            float rise = 1f - (1f - u) * (1f - u);
            text.rectTransform.localPosition = new Vector3(shake, StartOffset + Drift * rise, 0f);
            text.rectTransform.localScale = Vector3.one * Overshoot(elapsed / PopSeconds);
            text.alpha = u < 0.6f ? 1f : 1f - (u - 0.6f) / 0.4f;

            float burst = Mathf.Clamp01(elapsed / RingSeconds);
            ring.enabled = burst < 1f;
            ring.transform.localScale = Vector3.one * (Mathf.Lerp(0.3f, RingSize, 1f - (1f - burst) * (1f - burst)) / PosterSprites.Dot.bounds.size.x);
            Color ringColor = ring.color;
            ringColor.a = 0.6f * (1f - burst);
            ring.color = ringColor;
        }

        // 0 to 1 with a springy overshoot past 1 on the way.
        private static float Overshoot(float t)
        {
            t = Mathf.Clamp01(t);
            const float s = 2.6f;
            float u = t - 1f;
            return 1f + u * u * ((s + 1f) * u + s);
        }
    }
}
