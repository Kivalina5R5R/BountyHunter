using System;
using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The water coming down after the boss shoots into the air. At each spot a ring of water eases in
    // and swirls on the floor where it will land, turning and swelling gently, a faint disc filling it
    // from the middle over warningSeconds; just before the end a water streak fades in and shoots down
    // into the middle and flattens into the spot, fading away; once it has mostly gone in it strikes: a
    // burst splashes out from the spot and fades, with the sound. A player inside a circle as it
    // strikes is hit (once a volley, however many circles they stand in). The volley removes itself
    // when it has all faded. Pictures from BossWaterArt; with none it draws a plain ring and leaves
    // the rest out.
    public sealed class BossWaterVolley : MonoBehaviour
    {
        private const float Height = 0.05f;
        private const float PulsesPerSecond = 1.5f;
        private const float PulseGrowth = 0.03f;
        // The ring eases in over this long, from this share of its size.
        private const float RingInSeconds = 0.3f;
        private const float RingInFrom = 0.7f;
        private const float RingDegreesPerSecond = 120f;
        // The ring is drawn a little wider than the circle that hits, so standing on its edge reads as inside.
        private const float RingOverhang = 1.12f;
        // The streak falls this long before it strikes, from this far towards the wall (it reads as
        // from above), this wide and long.
        private const float StreakFallSeconds = 0.45f;
        private const float StreakRise = 2.4f;
        private const float StreakWidth = 0.42f;
        // How much of the fall the streak takes to fade in.
        private const float StreakInShare = 0.3f;
        // The water touches the floor this long before the strike, so the burst comes out of water that
        // has visibly arrived rather than around water still on its way down.
        private const float LandLeadSeconds = 0.14f;
        // On landing the water is gone in this long: it flattens from its full length into a low, wide
        // shape on the strike point, the streak fading across to the fatter splash picture, which fades
        // in at the start and out over the rest. The splash starts this tall, about the streak's length.
        private const float LandSeconds = 0.24f;
        private const float StreakOutSeconds = 0.08f;
        private const float SplashInSeconds = 0.05f;
        private const float SplashOutFrom = 0.3f;
        private const float SplashHeight = 1.3f;
        private const float LandedHeight = 0.08f;
        private const float LandedWidth = 1.5f;
        // The burst and ring start to fade this long after the strike, and take FadeSeconds.
        private const float StrikeSeconds = 0.15f;
        private const float FadeSeconds = 0.6f;
        // The burst fades in and pops out from this share of its size, then keeps spreading a little as it fades.
        private const float ImpactInSeconds = 0.06f;
        private const float ImpactPopSeconds = 0.18f;
        private const float ImpactWidth = 1.5f;
        private const float ImpactFrom = 0.2f;
        private const float ImpactSpread = 1.12f;
        private const float RingOutSpread = 1.15f;
        // The falling water over everything, the splash above the streak it takes over from; the burst
        // over the players' markers (20).
        private const int StreakOrder = 30;
        private const int SplashOrder = 31;
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
            public SpriteRenderer Splash;
            public SpriteRenderer Impact;
        }

        private readonly List<Circle> circles = new List<Circle>();
        private readonly HashSet<UWBPlayerController> hit = new HashSet<UWBPlayerController>();
        private readonly List<UWBPlayerController> near = new List<UWBPlayerController>();
        private SpawnerPlayerLocator players;
        private Action<UWBPlayerController> onHit;
        private Action onStrike;
        private BossWaterArt art;
        private float radius;
        private float warningSeconds;
        private Color color;
        private float elapsed;
        private bool struck;

        public static void Fire(IReadOnlyList<Vector3> spots, float radius, float warningSeconds, Color color, BossWaterArt art, int sortingOrder,
            SpawnerPlayerLocator players, Action<UWBPlayerController> onHit, Action onStrike, Transform parent)
        {
            var volley = new GameObject("Boss Water").AddComponent<BossWaterVolley>();
            volley.transform.SetParent(parent, false);
            volley.players = players;
            volley.onHit = onHit;
            volley.onStrike = onStrike;
            volley.art = art ?? new BossWaterArt();
            volley.radius = radius;
            volley.warningSeconds = Mathf.Max(StreakFallSeconds + LandLeadSeconds, warningSeconds);
            volley.color = color;
            foreach (Vector3 spot in spots) volley.AddCircle(spot, sortingOrder);
        }

        private void AddCircle(Vector3 center, int sortingOrder)
        {
            var circle = new Circle { Center = new Vector3(center.x, Height, center.z), Spin = UnityEngine.Random.Range(0f, 360f) };
            circle.Fill = Piece("Fill", Disc(), sortingOrder, circle.Center, 0f);
            circle.Ring = Piece("Ring", art.Ring != null ? art.Ring : PlainRing(), sortingOrder + 1, circle.Center, radius * 2f * RingOverhang);
            if (art.Streak != null) circle.Streak = Hidden(Piece("Streak", art.Streak, StreakOrder, circle.Center, StreakWidth));
            if (art.Splash != null) circle.Splash = Hidden(Piece("Splash", art.Splash, SplashOrder, circle.Center, 0f));
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
            if (after >= StrikeSeconds + FadeSeconds) Destroy(gameObject);
        }

        private void ShowWarning()
        {
            float filled = Mathf.Clamp01(elapsed / warningSeconds);
            float pulse = Mathf.Sin(elapsed * PulsesPerSecond * 2f * Mathf.PI);
            float sinceLanding = elapsed - warningSeconds + LandLeadSeconds;
            float ringIn = Mathf.SmoothStep(0f, 1f, elapsed / RingInSeconds);
            foreach (Circle circle in circles)
            {
                circle.Fill.color = new Color(color.r, color.g, color.b, color.a * 0.45f);
                SetWidth(circle.Fill, radius * 2f * filled);

                // Spins faster and glows brighter as the strike nears.
                circle.Spin += RingDegreesPerSecond * (1f + filled) * Time.deltaTime;
                circle.Ring.transform.rotation = Flat(circle.Spin);
                circle.Ring.color = art.Ring != null
                    ? new Color(1f, 1f, 1f, ringIn * Mathf.Lerp(0.55f, 1f, filled) * (0.9f + 0.1f * pulse))
                    : new Color(color.r, color.g, color.b, ringIn * Mathf.Clamp01(color.a * 2f));
                SetWidth(circle.Ring, radius * 2f * RingOverhang * Mathf.Lerp(RingInFrom, 1f, ringIn) * (1f + PulseGrowth * pulse));
                if (sinceLanding < 0f) ShowStreak(circle, Mathf.Clamp01(1f + sinceLanding / StreakFallSeconds));
                else ShowLanding(circle, sinceLanding);
            }
        }

        // Shoots down from towards the wall into the middle, speeding up, its head at the strike point.
        private void ShowStreak(Circle circle, float falling)
        {
            if (circle.Streak == null) return;

            circle.Streak.enabled = falling > 0f;
            if (falling <= 0f) return;

            circle.Streak.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, falling / StreakInShare));
            float eased = falling * falling;
            float halfLength = circle.Streak.sprite.bounds.size.y * circle.Streak.transform.localScale.y * 0.5f;
            circle.Streak.transform.position = circle.Center + new Vector3(0f, 0.02f, halfLength + StreakRise * (1f - eased));
        }

        private void Strike()
        {
            struck = true;
            onStrike?.Invoke();
            foreach (Circle circle in circles)
            {
                if (circle.Impact != null)
                {
                    circle.Impact.enabled = true;
                    circle.Impact.transform.rotation = Flat(circle.Spin);
                }

                players.GetPlayersWithin(circle.Center, radius, near);
                foreach (UWBPlayerController player in near)
                {
                    if (hit.Add(player)) onHit?.Invoke(player);
                }
            }
        }

        // Lying from the strike point towards the wall, its bottom edge staying on the point as it
        // flattens. How tall and how wide are shares of its size at this scale.
        private static void ShapeOnPoint(SpriteRenderer piece, Vector3 point, float scale, float tall, float wide)
        {
            piece.transform.localScale = new Vector3(scale * wide, scale * tall, scale);
            piece.transform.position = point + new Vector3(0f, 0.03f, -piece.sprite.bounds.min.y * scale * tall);
        }

        // The falling water flattens into the floor and fades, seconds after its head touched it.
        private static void ShowLanding(Circle circle, float seconds)
        {
            float land = Mathf.Clamp01(seconds / LandSeconds);
            float landed = 1f - (1f - land) * (1f - land);
            float tall = Mathf.Lerp(1f, LandedHeight, landed);
            float wide = Mathf.Lerp(1f, LandedWidth, landed);
            if (circle.Streak != null)
            {
                float streakAlpha = 1f - Mathf.SmoothStep(0f, 1f, seconds / StreakOutSeconds);
                circle.Streak.color = new Color(1f, 1f, 1f, streakAlpha);
                circle.Streak.enabled = streakAlpha > 0f;
                ShapeOnPoint(circle.Streak, circle.Center, StreakWidth / circle.Streak.sprite.bounds.size.x, tall, wide);
            }

            if (circle.Splash != null)
            {
                float fadeIn = Mathf.SmoothStep(0f, 1f, seconds / SplashInSeconds);
                float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, (land - SplashOutFrom) / (1f - SplashOutFrom));
                circle.Splash.color = new Color(1f, 1f, 1f, fadeIn * fadeOut);
                circle.Splash.enabled = land < 1f;
                ShapeOnPoint(circle.Splash, circle.Center, SplashHeight / circle.Splash.sprite.bounds.size.y, tall, wide);
            }
        }

        // The water finishes going in, the burst pops out and spreads, and the burst and ring fade.
        private void ShowStrike(float after)
        {
            float fade = Mathf.SmoothStep(0f, 1f, (after - StrikeSeconds) / FadeSeconds);
            float alpha = 1f - fade;
            float pop = Mathf.Clamp01(after / ImpactPopSeconds);
            float popped = 1f - (1f - pop) * (1f - pop) * (1f - pop);
            foreach (Circle circle in circles)
            {
                circle.Fill.color = new Color(color.r, color.g, color.b, color.a * 0.45f * (1f - Mathf.SmoothStep(0f, 1f, after / StrikeSeconds)));
                circle.Ring.color = new Color(circle.Ring.color.r, circle.Ring.color.g, circle.Ring.color.b, alpha);
                circle.Spin += RingDegreesPerSecond * 2f * Time.deltaTime;
                circle.Ring.transform.rotation = Flat(circle.Spin);
                SetWidth(circle.Ring, radius * 2f * RingOverhang * Mathf.Lerp(1f, RingOutSpread, fade));

                ShowLanding(circle, after + LandLeadSeconds);

                if (circle.Impact == null) continue;

                circle.Impact.color = new Color(1f, 1f, 1f, alpha * Mathf.SmoothStep(0f, 1f, after / ImpactInSeconds));
                SetWidth(circle.Impact, radius * 2f * ImpactWidth * Mathf.Lerp(ImpactFrom, 1f, popped) * Mathf.Lerp(1f, ImpactSpread, fade));
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
