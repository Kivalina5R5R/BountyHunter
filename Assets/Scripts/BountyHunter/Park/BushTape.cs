using Fortal.Display;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The police tape over a taped-off hiding spot: a few strips of KEEP OUT tape slapped across its
    // leaves one after another (three on a small spot, four on a bigger one), crossing each other at odd angles, which stay while it is locked and
    // fade away as the lock ends. The tape is always the same width on the floor whatever the size of
    // the spot; each strip is only as long as its line across the spot's drawn leaves (the art has
    // empty margins, and some bushes are long ovals), plus a little hanging over their edge, its
    // middle repeating the KEEP OUT print as often as that takes (the strip picture's ends are its
    // borders, its middle tiles). Drawn over the spot. Sits on the hiding spot, so it follows it as it
    // creeps and goes with it when the layout changes. Removes itself.
    public sealed class BushTape : MonoBehaviour
    {
        private const float SlapSeconds = 0.18f;
        private const float SlapEverySeconds = 0.07f;
        private const float FadeSeconds = 0.5f;

        // Each strip: its slant (degrees, before the spot's own random turn) and how far its line runs
        // from the leaves' middle (shares of their half size, across the strip), as in the example
        // drawing: on a bigger spot two crossing near the middle and two further out; on a small one
        // three spread round it.
        private static readonly Vector2[] FewStrips =
        {
            new Vector2(-20f, -0.3f),
            new Vector2(50f, 0.3f),
            new Vector2(100f, 0f),
        };

        private static readonly Vector2[] ManyStrips =
        {
            new Vector2(-20f, -0.15f),
            new Vector2(86f, -0.38f),
            new Vector2(-58f, 0.42f),
            new Vector2(48f, 0.08f),
        };

        private SpriteRenderer[] pictures;
        private float[] lengths;
        private float width;
        private float seconds;
        private float age;

        // Over the hiding spot's leaves, strips of tape (a picture whose ends are its borders) width
        // metres wide hanging overhang metres past the leaves' edge, for seconds; fewer of them when
        // the leaves' narrow side is under smallSpot metres.
        public static void Play(HidingBush bush, Sprite strip, float width, float overhang, float smallSpot, float seconds)
        {
            if (strip == null) return;

            BushTape old = bush.GetComponentInChildren<BushTape>();
            if (old != null) Destroy(old.gameObject);

            var holder = new GameObject("Police Tape");
            holder.transform.SetParent(bush.transform, false);
            var effect = holder.AddComponent<BushTape>();
            effect.seconds = seconds;
            effect.Lay(bush.GetComponent<SpriteRenderer>(), strip, width, overhang, smallSpot);
            effect.LateUpdate();
        }

        // In the spot's own units, each strip on its line across the leaves' oval.
        private void Lay(SpriteRenderer spot, Sprite strip, float worldWidth, float worldOverhang, float smallSpot)
        {
            // Metres to the spot's own units (it is scaled to its size).
            float perMetre = 1f / Mathf.Max(0.0001f, transform.lossyScale.x);
            width = worldWidth * perMetre;
            float overhang = worldOverhang * perMetre;

            Rect leaves = spot != null && spot.sprite != null ? SpriteExtents.Visible(spot.sprite) : new Rect(-0.5f, -0.5f, 1f, 1f);
            Vector2 middle = leaves.center;
            if (spot != null && spot.flipX) middle.x = -middle.x;
            if (spot != null && spot.flipY) middle.y = -middle.y;
            Vector2 half = leaves.size * 0.5f;
            int order = spot != null ? spot.sortingOrder + 1 : 0;
            float turn = Random.Range(0f, 360f);
            Vector2[] strips = Mathf.Min(leaves.width, leaves.height) / perMetre < smallSpot ? FewStrips : ManyStrips;

            pictures = new SpriteRenderer[strips.Length];
            lengths = new float[strips.Length];
            for (int i = 0; i < strips.Length; i++)
            {
                float angle = (strips[i].x + turn) * Mathf.Deg2Rad;
                var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var across = new Vector2(-along.y, along.x);
                // How far out the line runs, as a share of the oval's half size that way.
                float reach = Mathf.Sqrt(Sq(across.x * half.x) + Sq(across.y * half.y));
                Vector2 through = middle + across * (strips[i].y * reach);
                Chord(through - middle, along, half, out float from, out float to);

                var part = new GameObject($"Strip {i + 1}", typeof(SpriteRenderer));
                part.transform.SetParent(transform, false);
                part.transform.localPosition = through + along * ((from + to) * 0.5f);
                part.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
                SpriteRenderer picture = part.GetComponent<SpriteRenderer>();
                picture.sprite = strip;
                picture.drawMode = SpriteDrawMode.Tiled;
                picture.tileMode = SpriteTileMode.Continuous;
                // Alternate layers, so the strips cross over and under each other.
                picture.sortingOrder = order + i % 2;
                pictures[i] = picture;
                lengths[i] = Mathf.Max(width * 2f, to - from + overhang * 2f);
            }
        }

        // Where a line through the oval (start: from the oval's middle) along direction meets its
        // edge, as distances along it; a line missing it gets a short stretch round its start.
        private static void Chord(Vector2 start, Vector2 direction, Vector2 half, out float from, out float to)
        {
            float a = Sq(direction.x / half.x) + Sq(direction.y / half.y);
            float b = 2f * (start.x * direction.x / Sq(half.x) + start.y * direction.y / Sq(half.y));
            float c = Sq(start.x / half.x) + Sq(start.y / half.y) - 1f;
            float inside = b * b - 4f * a * c;
            if (inside <= 0f)
            {
                from = -0.1f;
                to = 0.1f;
                return;
            }

            float root = Mathf.Sqrt(inside);
            from = (-b - root) / (2f * a);
            to = (-b + root) / (2f * a);
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= seconds)
            {
                Destroy(gameObject);
                return;
            }

            float alpha = Mathf.Clamp01((seconds - age) / FadeSeconds);
            for (int i = 0; i < pictures.Length; i++)
            {
                // Each strip slaps down in turn: it runs out to its length, a touch past and back.
                float slap = Mathf.Clamp01((age - i * SlapEverySeconds) / SlapSeconds);
                float grow = slap <= 0f ? 0f : EaseOutBack(slap);
                SpriteRenderer picture = pictures[i];
                picture.enabled = slap > 0f;
                picture.size = new Vector2(Mathf.Max(0.001f, lengths[i] * grow), width) / StripScale(picture.sprite);
                picture.transform.localScale = Vector3.one * StripScale(picture.sprite);
                picture.color = new Color(1f, 1f, 1f, alpha);
            }
        }

        // The strip's picture scaled so its height is the tape's width.
        private float StripScale(Sprite sprite)
        {
            return width / Mathf.Max(0.0001f, sprite.bounds.size.y);
        }

        private static float Sq(float value)
        {
            return value * value;
        }

        // Runs a little past 1 before settling on it.
        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float back = t - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }
    }
}
