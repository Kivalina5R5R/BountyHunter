using System;
using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The water coming down after the boss shoots into the air. At each spot a glowing ring swirls on
    // the floor where it will land, turning and pulsing, a faint disc filling it from the middle over
    // warningSeconds; just before the end a water streak shoots down into the middle, then it strikes:
    // lightning flickers on the spot and a burst of light splashes out. A player inside a circle as it
    // strikes is hit (once a volley, however many circles they stand in). The volley removes itself
    // when it has all faded. Pictures from BossWaterArt; with none it draws a plain ring and leaves
    // the rest out.
    public sealed class BossWaterVolley : MonoBehaviour
    {
        private const float Height = 0.05f;
        private const float PulsesPerSecond = 2.5f;
        private const float PulseGrowth = 0.05f;
        private const float RingDegreesPerSecond = 120f;
        // The ring is drawn a little wider than the circle that hits, so standing on its edge reads as inside.
        private const float RingOverhang = 1.12f;
        // The streak falls this long before it strikes, from this far towards the wall (it reads as
        // from above), this wide and long.
        private const float StreakFallSeconds = 0.4f;
        private const float StreakRise = 2.4f;
        private const float StreakWidth = 0.42f;
        // The bolts stand this tall from the strike towards the wall, flickering for boltSeconds.
        private const float BoltHeight = 2.1f;
        private const float BoltSeconds = 0.35f;
        private const float BoltFlickerSeconds = 0.05f;
        private const float ImpactPopSeconds = 0.1f;
        private const float ImpactWidth = 1.5f;
        private const float FadeSeconds = 0.5f;
        // The streak and bolts over everything; the burst over the players' markers (20).
        private const int StreakOrder = 30;
        private const int ImpactOrder = 23;
        private const int TextureSize = 128;
        private const float RingShare = 0.08f;

        private static Sprite disc;
        private static Sprite plainRing;

        private sealed class Circle
        {
            public Vector3 Center;
            public float Spin;
            public SpriteRenderer Fill;
            public SpriteRenderer Ring;
            public SpriteRenderer Streak;
            public SpriteRenderer Bolt;
            public SpriteRenderer Impact;
        }

        private readonly List<Circle> circles = new List<Circle>();
        private readonly HashSet<UWBPlayerController> hit = new HashSet<UWBPlayerController>();
        private readonly List<UWBPlayerController> near = new List<UWBPlayerController>();
        private SpawnerPlayerLocator players;
        private Action<UWBPlayerController> onHit;
        private BossWaterArt art;
        private float radius;
        private float warningSeconds;
        private Color color;
        private float elapsed;
        private bool struck;

        public static void Fire(IReadOnlyList<Vector3> spots, float radius, float warningSeconds, Color color, BossWaterArt art, int sortingOrder,
            SpawnerPlayerLocator players, Action<UWBPlayerController> onHit, Transform parent)
        {
            var volley = new GameObject("Boss Water").AddComponent<BossWaterVolley>();
            volley.transform.SetParent(parent, false);
            volley.players = players;
            volley.onHit = onHit;
            volley.art = art ?? new BossWaterArt();
            volley.radius = radius;
            volley.warningSeconds = Mathf.Max(StreakFallSeconds, warningSeconds);
            volley.color = color;
            foreach (Vector3 spot in spots) volley.AddCircle(spot, sortingOrder);
        }

        private void AddCircle(Vector3 center, int sortingOrder)
        {
            var circle = new Circle { Center = new Vector3(center.x, Height, center.z), Spin = UnityEngine.Random.Range(0f, 360f) };
            circle.Fill = Piece("Fill", Disc(), sortingOrder, circle.Center, 0f);
            circle.Ring = Piece("Ring", art.Ring != null ? art.Ring : PlainRing(), sortingOrder + 1, circle.Center, radius * 2f * RingOverhang);
            if (art.Streak != null) circle.Streak = Hidden(Piece("Streak", art.Streak, StreakOrder, circle.Center, StreakWidth));
            if (art.Bolts.Length > 0) circle.Bolt = Hidden(Piece("Lightning", art.Bolts[0], StreakOrder, circle.Center, 0f));
            if (art.Impact != null) circle.Impact = Hidden(Piece("Burst", art.Impact, ImpactOrder, circle.Center, 0f));
            circles.Add(circle);
        }

        private static SpriteRenderer Hidden(SpriteRenderer piece)
        {
            piece.enabled = false;
            return piece;
        }

        // A flat picture on the floor (its top towards the wall), width metres wide.
        private SpriteRenderer Piece(string name, Sprite sprite, int sortingOrder, Vector3 at, float width)
        {
            var piece = new GameObject(name).AddComponent<SpriteRenderer>();
            piece.transform.SetParent(transform, false);
            piece.transform.SetPositionAndRotation(at, Flat(0f));
            piece.sprite = sprite;
            piece.sortingOrder = sortingOrder;
            SetWidth(piece, width);
            return piece;
        }

        // Lying on the floor, turned by degrees about the up axis.
        private static Quaternion Flat(float degrees)
        {
            return Quaternion.AngleAxis(degrees, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
        }

        private static void SetWidth(SpriteRenderer piece, float width)
        {
            float native = piece.sprite.bounds.size.x;
            piece.transform.localScale = Vector3.one * (native > 0f ? width / native : 0f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (!struck)
            {
                ShowWarning();
                if (elapsed >= warningSeconds) Strike();
                return;
            }

            float after = elapsed - warningSeconds;
            ShowStrike(after);
            if (after >= BoltSeconds + FadeSeconds) Destroy(gameObject);
        }

        private void ShowWarning()
        {
            float filled = Mathf.Clamp01(elapsed / warningSeconds);
            float pulse = Mathf.Sin(elapsed * PulsesPerSecond * 2f * Mathf.PI);
            float falling = Mathf.Clamp01(1f - (warningSeconds - elapsed) / StreakFallSeconds);
            foreach (Circle circle in circles)
            {
                circle.Fill.color = new Color(color.r, color.g, color.b, color.a * 0.45f);
                SetWidth(circle.Fill, radius * 2f * filled);

                // Spins faster and glows brighter as the strike nears.
                circle.Spin += RingDegreesPerSecond * (1f + filled) * Time.deltaTime;
                circle.Ring.transform.rotation = Flat(circle.Spin);
                circle.Ring.color = art.Ring != null
                    ? new Color(1f, 1f, 1f, Mathf.Lerp(0.55f, 1f, filled) * (0.85f + 0.15f * pulse))
                    : new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * 2f));
                SetWidth(circle.Ring, radius * 2f * RingOverhang * (1f + PulseGrowth * pulse));
                ShowStreak(circle, falling);
            }
        }

        // Shoots down from towards the wall into the middle, speeding up, its head at the strike point.
        private void ShowStreak(Circle circle, float falling)
        {
            if (circle.Streak == null) return;

            circle.Streak.enabled = falling > 0f;
            if (falling <= 0f) return;

            float eased = falling * falling;
            float halfLength = circle.Streak.sprite.bounds.size.y * circle.Streak.transform.localScale.y * 0.5f;
            circle.Streak.transform.position = circle.Center + new Vector3(0f, 0.02f, halfLength + StreakRise * (1f - eased));
        }

        private void Strike()
        {
            struck = true;
            foreach (Circle circle in circles)
            {
                if (circle.Streak != null) circle.Streak.enabled = false;
                if (circle.Bolt != null)
                {
                    circle.Bolt.enabled = true;
                    PlaceBolt(circle, art.Bolts[0]);
                }

                if (circle.Impact != null) circle.Impact.enabled = true;

                players.GetPlayersWithin(circle.Center, radius, near);
                foreach (UWBPlayerController player in near)
                {
                    if (hit.Add(player)) onHit?.Invoke(player);
                }
            }
        }

        // Standing up from the strike point towards the wall, BoltHeight tall.
        private void PlaceBolt(Circle circle, Sprite bolt)
        {
            circle.Bolt.sprite = bolt;
            float native = bolt.bounds.size.y;
            float scale = native > 0f ? BoltHeight / native : 0f;
            circle.Bolt.transform.localScale = Vector3.one * scale;
            circle.Bolt.transform.position = circle.Center + new Vector3(0f, 0.03f, -bolt.bounds.min.y * scale);
        }

        // The lightning flickers between its pictures, then everything fades; the burst pops out first.
        private void ShowStrike(float after)
        {
            float fade = Mathf.Clamp01((after - BoltSeconds) / FadeSeconds);
            float alpha = 1f - fade * fade;
            float pop = Mathf.Clamp01(after / ImpactPopSeconds);
            int flicker = Mathf.FloorToInt(after / BoltFlickerSeconds);
            foreach (Circle circle in circles)
            {
                circle.Fill.color = new Color(color.r, color.g, color.b, color.a * 0.45f * (1f - Mathf.Clamp01(after / BoltSeconds)));
                circle.Ring.color = new Color(circle.Ring.color.r, circle.Ring.color.g, circle.Ring.color.b, alpha * 0.8f);
                circle.Spin += RingDegreesPerSecond * 2f * Time.deltaTime;
                circle.Ring.transform.rotation = Flat(circle.Spin);

                if (circle.Bolt != null)
                {
                    Sprite[] bolts = art.Bolts;
                    PlaceBolt(circle, bolts[flicker % bolts.Length]);
                    // Flashes on and off while it crackles, then fades out with the rest.
                    bool lit = after >= BoltSeconds || flicker % 3 != 2;
                    circle.Bolt.color = new Color(1f, 1f, 1f, lit ? alpha : 0.25f * alpha);
                    circle.Bolt.flipX = flicker % 2 == 1;
                }

                if (circle.Impact == null) continue;

                circle.Impact.color = new Color(1f, 1f, 1f, alpha);
                SetWidth(circle.Impact, radius * 2f * ImpactWidth * Mathf.Lerp(0.4f, 1f, 1f - (1f - pop) * (1f - pop)));
            }
        }

        // A soft-edged white disc one unit across.
        private static Sprite Disc()
        {
            if (disc == null) disc = MakeSprite("Boss Water Disc", distance => Mathf.Clamp01((1f - distance) * TextureSize * 0.5f));
            return disc;
        }

        // A white ring one unit across, RingShare of the radius thick (when no ring picture is set).
        private static Sprite PlainRing()
        {
            if (plainRing == null) plainRing = MakeSprite("Boss Water Ring", distance => Mathf.Clamp01((RingShare - Mathf.Abs(distance - (1f - RingShare))) * TextureSize * 0.5f));
            return plainRing;
        }

        // alphaAt gets the distance from the middle, 0 there and 1 at the edge.
        private static Sprite MakeSprite(string name, Func<float, float> alphaAt)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[TextureSize * TextureSize];
            float half = TextureSize * 0.5f;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alphaAt(distance) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
        }
    }
}
