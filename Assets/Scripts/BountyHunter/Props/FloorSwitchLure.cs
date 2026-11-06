using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // What draws the players to a floor switch while it is waiting to be used: a soft glow in the
    // police tape's yellow breathing under the prop, and stars twinkling round it (the sparkle
    // pictures, in the designer's example: each pops up somewhere round the prop's edge, turns a
    // little and shrinks away, drifting up). FloorSwitch turns it on (Inviting) while it is ready and
    // nobody stands on it; it fades away otherwise. Burst throws a ring of stars out, as the prop is
    // used up. Lies flat on the floor; the glow is drawn with the soft dot (PosterSprites).
    public sealed class FloorSwitchLure : MonoBehaviour
    {
        // Under the prop (6), over the hiding spots (5) for the glow; over the prop for the stars.
        private const int GlowOrder = 5;
        private const int StarOrder = 7;
        private const float GlowAlpha = 0.4f;
        private const float BreathsPerSecond = 0.8f;
        private const float StarsPerSecond = 3f;
        private const float StarSeconds = 0.9f;
        private const float BurstSeconds = 0.6f;
        // How long it takes to fade in or out as it is turned on or off.
        private const float FadeSeconds = 0.3f;
        private static readonly Color Yellow = new Color(1f, 0.85f, 0.2f, 1f);
        // A glow picture of its own (UseSteadyGlow): how wide against the prop, and how little it breathes.
        private const float SteadyGlowVsProp = 2.6f;
        private const float SteadyGlowBreath = 0.12f;

        private sealed class Star
        {
            public SpriteRenderer Picture;
            public Vector2 From;
            public Vector2 Drift;
            public float Born;
            public float Seconds;
            public float Size;
            public float Spin;
            public bool Burst;
        }

        private readonly List<Star> stars = new List<Star>();
        private Sprite[] sparkles;
        private SpriteRenderer glow;
        private float width;
        private float shown;
        private float owed;
        // A glow picture of its own stays lit, even with someone on the prop, until the Burst.
        private bool hasSteadyGlow;
        private bool steadyGlowOn;
        private float steadyShown;

        // Whether it is drawing the players in now.
        public bool Inviting { get; set; }

        // Round a prop width metres across, on the switch's root (which lies flat on the floor), its
        // stars drawn from sparkles (a soft dot when there are none).
        public static FloorSwitchLure Create(Transform root, float width, Sprite[] sparkles)
        {
            var holder = new GameObject("Lure");
            holder.transform.SetParent(root, false);
            var lure = holder.AddComponent<FloorSwitchLure>();
            lure.width = width;
            lure.sparkles = sparkles != null && sparkles.Length > 0 ? sparkles : new[] { PosterSprites.Dot };
            lure.glow = lure.Picture("Glow", PosterSprites.Dot, GlowOrder);
            lure.glow.transform.localScale = Vector3.one * (width * 1.9f / SizeOf(PosterSprites.Dot));
            return lure;
        }

        // Draws picture in its own colours under the prop instead of the breathing yellow dot, lit all
        // the time the prop is there.
        public void UseSteadyGlow(Sprite picture)
        {
            glow.sprite = picture;
            glow.transform.localScale = Vector3.one * (width * SteadyGlowVsProp / SizeOf(picture));
            hasSteadyGlow = true;
            steadyGlowOn = true;
        }

        // A ring of stars flying out from the prop and the yellow glow gone at once; a steady glow
        // fades out instead.
        public void Burst(int count)
        {
            Inviting = false;
            shown = 0f;
            steadyGlowOn = false;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
                var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AddStar(outward * width * 0.3f, outward * width * Random.Range(0.7f, 1.1f), BurstSeconds, Random.Range(0.12f, 0.2f) * width / 0.55f, true);
            }
        }

        private void Update()
        {
            shown = Mathf.MoveTowards(shown, Inviting ? 1f : 0f, Time.deltaTime / FadeSeconds);
            float breath = 0.6f + 0.4f * Mathf.Sin(Time.time * BreathsPerSecond * Mathf.PI * 2f);
            if (hasSteadyGlow)
            {
                steadyShown = Mathf.MoveTowards(steadyShown, steadyGlowOn ? 1f : 0f, Time.deltaTime / FadeSeconds);
                glow.color = new Color(1f, 1f, 1f, steadyShown * (1f - SteadyGlowBreath * (1f - breath)));
            }
            else
            {
                Color glowColour = Yellow;
                glowColour.a = GlowAlpha * breath * shown;
                glow.color = glowColour;
            }

            if (Inviting)
            {
                owed += StarsPerSecond * Time.deltaTime;
                while (owed >= 1f)
                {
                    owed -= 1f;
                    AddTwinkle();
                }
            }

            for (int i = stars.Count - 1; i >= 0; i--)
            {
                Star star = stars[i];
                float t = (Time.time - star.Born) / star.Seconds;
                if (t >= 1f)
                {
                    Destroy(star.Picture.gameObject);
                    stars.RemoveAt(i);
                    continue;
                }

                // A burst star flies out fast and slows; a twinkle drifts evenly.
                float travelled = star.Burst ? 1f - (1f - t) * (1f - t) : t;
                Vector2 at = star.From + star.Drift * travelled;
                star.Picture.transform.localPosition = new Vector3(at.x, at.y, 0f);
                star.Picture.transform.localRotation = Quaternion.Euler(0f, 0f, star.Spin * t);
                float size = star.Burst ? 1f - t * 0.6f : Mathf.Sin(t * Mathf.PI);
                star.Picture.transform.localScale = Vector3.one * (star.Size * size / SizeOf(star.Picture.sprite));
                float alpha = star.Burst ? 1f - t * t : Mathf.Max(shown, 0.3f);
                star.Picture.color = new Color(1f, 1f, 1f, alpha);
            }
        }

        // A star somewhere round the prop's edge, drifting a little up the floor (towards the wall).
        private void AddTwinkle()
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float reach = width * Random.Range(0.55f, 0.75f);
            var from = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
            var drift = new Vector2(Random.Range(-0.03f, 0.03f), Random.Range(0.04f, 0.1f));
            AddStar(from, drift, StarSeconds, Random.Range(0.1f, 0.2f) * width / 0.55f, false);
        }

        private void AddStar(Vector2 from, Vector2 drift, float seconds, float size, bool burst)
        {
            Sprite sprite = sparkles[Random.Range(0, sparkles.Length)];
            stars.Add(new Star
            {
                Picture = Picture("Star", sprite, StarOrder),
                From = from,
                Drift = drift,
                Born = Time.time,
                Seconds = seconds,
                Size = size,
                Spin = Random.Range(-90f, 90f),
                Burst = burst,
            });
        }

        private SpriteRenderer Picture(string name, Sprite sprite, int order)
        {
            var piece = new GameObject(name, typeof(SpriteRenderer));
            piece.transform.SetParent(transform, false);
            var picture = piece.GetComponent<SpriteRenderer>();
            picture.sprite = sprite;
            picture.sortingOrder = order;
            picture.color = Color.clear;
            return picture;
        }

        private static float SizeOf(Sprite sprite)
        {
            return sprite != null ? Mathf.Max(0.0001f, sprite.bounds.size.x) : 1f;
        }
    }
}
