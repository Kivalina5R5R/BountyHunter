using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // A police helicopter flying past in the sky over the boss's rooftop on the wall, behind the
    // buildings: now and then it comes out from behind the buildings on one side, crosses the gap of
    // sky between them at a slightly random height and goes behind the buildings on the other side,
    // the way picked at random each time, after a random wait. It bobs and tilts a little into its
    // flight, its rotor whirls (the blade picture squeezed and stretched, as a turning blade looks from
    // the side) and its searchlight sweeps the city below. It flies in the Boss Wall's Sky, under the
    // buildings in front of the sky there, which hide it as it passes behind them.
    public sealed class BossHelicopter : MonoBehaviour
    {
        // Width as a share of the wall picture (far off, so small).
        private const float WidthShare = 0.045f;
        // Its way across, as shares of the wall picture from the left and from the top: it starts and
        // ends behind the buildings either side of the gap, at a height in this range, climbing or
        // sinking up to Slope on the way.
        private const float FromShare = 0.3f;
        private const float ToShare = 0.735f;
        private static readonly Vector2 HeightShare = new Vector2(0.08f, 0.15f);
        private const float Slope = 0.03f;
        private static readonly Vector2 CrossSeconds = new Vector2(7f, 10f);
        private static readonly Vector2 WaitSeconds = new Vector2(1.5f, 4f);
        private const float BobShare = 0.006f;
        private const float BobSeconds = 2.3f;
        private const float TiltDegrees = 6f;
        // The rotor sits on the mast (shares of the body from its middle) and is this wide against it.
        private static readonly Vector2 RotorAt = new Vector2(-0.14f, 0.47f);
        private const float RotorWidth = 1.05f;
        private const float RotorTurnsPerSecond = 5f;
        // The searchlight hangs from under the nose (shares of the body from its middle), this long
        // against the body width, sweeping this far either way.
        private static readonly Vector2 LampAt = new Vector2(-0.44f, -0.37f);
        private const float BeamLength = 2.6f;
        private const float BeamWidth = 0.9f;
        private const float BeamSweepDegrees = 22f;
        private const float BeamSweepSeconds = 5f;
        private static readonly Color BeamColour = new Color(1f, 0.95f, 0.75f, 0.32f);

        private RectTransform wall;
        private RectTransform craft;
        private RectTransform rotor;
        private RectTransform beam;
        private float elapsed;
        private bool flying;
        // The pass under way: from where to where (shares of the wall), how long, and when it began;
        // between passes it waits out of sight until nextPassAt.
        private Vector2 from;
        private Vector2 to;
        private float crossSeconds;
        private float passStartedAt;
        private float nextPassAt;

        // sky covers the whole wall picture, under the buildings in front; rotorBlades may be null.
        public static BossHelicopter Fly(RectTransform sky, Sprite body, Sprite rotorBlades)
        {
            var root = new GameObject("Boss Helicopter", typeof(RectTransform));
            var helicopter = root.AddComponent<BossHelicopter>();
            helicopter.wall = sky;
            helicopter.craft = (RectTransform)root.transform;
            helicopter.craft.SetParent(helicopter.wall, false);
            helicopter.craft.anchorMin = helicopter.craft.anchorMax = new Vector2(0f, 1f);
            helicopter.craft.pivot = new Vector2(0.5f, 0.5f);

            float width = helicopter.wall.rect.width * WidthShare;
            float height = width * body.rect.height / body.rect.width;
            helicopter.craft.sizeDelta = new Vector2(width, height);

            // The beam first, so the helicopter is drawn over it.
            helicopter.beam = Part("Searchlight", helicopter.craft, BeamSprite(), new Vector2(width * BeamWidth, width * BeamLength), new Vector2(0.5f, 1f));
            helicopter.beam.anchoredPosition = new Vector2(LampAt.x * width, LampAt.y * height);
            helicopter.beam.GetComponent<Image>().color = BeamColour;
            Part("Body", helicopter.craft, body, new Vector2(width, height), new Vector2(0.5f, 0.5f));
            if (rotorBlades != null)
            {
                float rotorWidth = width * RotorWidth;
                float rotorHeight = rotorWidth * rotorBlades.rect.height / rotorBlades.rect.width;
                helicopter.rotor = Part("Rotor", helicopter.craft, rotorBlades, new Vector2(rotorWidth, rotorHeight), new Vector2(0.5f, 0.5f));
                helicopter.rotor.anchoredPosition = new Vector2(RotorAt.x * width, RotorAt.y * height);
            }

            helicopter.nextPassAt = Random.Range(0f, WaitSeconds.y);
            helicopter.SetSeen(false);
            return helicopter;
        }

        private static RectTransform Part(string name, RectTransform parent, Sprite sprite, Vector2 size, Vector2 pivot)
        {
            var part = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)part.transform;
            rect.SetParent(parent, false);
            rect.pivot = pivot;
            rect.sizeDelta = size;
            var image = part.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return rect;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (!flying)
            {
                if (elapsed < nextPassAt) return;
                StartPass();
            }

            float across = (elapsed - passStartedAt) / crossSeconds;
            if (across >= 1f)
            {
                // Behind the buildings on the far side: out of sight until the next pass.
                flying = false;
                SetSeen(false);
                nextPassAt = elapsed + Random.Range(WaitSeconds.x, WaitSeconds.y);
                return;
            }

            Place(across);
        }

        // Its children only (the picture parts), so it keeps running while out of sight.
        private void SetSeen(bool seen)
        {
            foreach (Transform part in craft) part.gameObject.SetActive(seen);
        }

        private void Place(float across)
        {
            float wallWidth = wall.rect.width;
            float wallHeight = wall.rect.height;
            Vector2 at = Vector2.Lerp(from, to, across);
            float bob = Mathf.Sin(elapsed * 2f * Mathf.PI / BobSeconds) * BobShare * wallHeight;
            craft.anchoredPosition = new Vector2(at.x * wallWidth, -at.y * wallHeight + bob);
            // Leans nose-down into its flight and faces it (the picture faces left).
            bool goingRight = to.x > from.x;
            craft.localRotation = Quaternion.Euler(0f, 0f, goingRight ? -TiltDegrees : TiltDegrees);
            craft.localScale = new Vector3(goingRight ? -1f : 1f, 1f, 1f);

            if (rotor != null)
            {
                float turn = Mathf.Cos(elapsed * RotorTurnsPerSecond * 2f * Mathf.PI);
                rotor.localScale = new Vector3(Mathf.Max(0.15f, Mathf.Abs(turn)), 1f, 1f);
            }

            float sweep = Mathf.Sin(elapsed * 2f * Mathf.PI / BeamSweepSeconds) * BeamSweepDegrees;
            beam.localRotation = Quaternion.Euler(0f, 0f, sweep);
        }

        // A new pass one way or the other, at a random height, after the last one's wait.
        private void StartPass()
        {
            bool leftToRight = Random.value < 0.5f;
            float height = Random.Range(HeightShare.x, HeightShare.y);
            float drift = Random.Range(-Slope, Slope);
            from = new Vector2(leftToRight ? FromShare : ToShare, height);
            to = new Vector2(leftToRight ? ToShare : FromShare, Mathf.Clamp(height + drift, HeightShare.x, HeightShare.y));
            crossSeconds = Random.Range(CrossSeconds.x, CrossSeconds.y);
            passStartedAt = elapsed;
            flying = true;
            SetSeen(true);
        }

        // A cone of light, narrow at the lamp (top) and widening and fading downwards, soft at its sides.
        private static Sprite BeamSprite()
        {
            const int width = 64;
            const int height = 256;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Searchlight Beam", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                // 0 at the lamp (top row), 1 at the far end.
                float along = 1f - (y + 0.5f) / height;
                float halfWidth = Mathf.Lerp(0.08f, 0.5f, along);
                float strength = (1f - along) * 0.85f + 0.15f;
                for (int x = 0; x < width; x++)
                {
                    float across = Mathf.Abs((x + 0.5f) / width - 0.5f);
                    float edge = Mathf.Clamp01((halfWidth - across) / (halfWidth * 0.5f));
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(edge * strength * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 1f), 100f);
        }
    }
}
