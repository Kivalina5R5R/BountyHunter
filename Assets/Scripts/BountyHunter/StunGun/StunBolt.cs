using UnityEngine;

namespace BountyHunter.Thieves
{
    // A stun gun's bolt: a streak of electricity from the shooter to what it hits, lying just over the
    // floor in the look of the lightning bolt icons: a white-hot core in a dark outline over a soft
    // glow in the bolt's colour. It shoots out from the shooter to the target in a blink (sparks
    // bursting at both ends, StunSparks), then writhes (smooth bends moving along it, snapping to a
    // new shape every few hundredths of a second so it crackles), flickers and fades, with a couple
    // of short forks branching off it. Each line is a row of soft dots stretched along its path and
    // overlapping, so it bends smoothly with no corners. Removes itself.
    public sealed class StunBolt : MonoBehaviour
    {
        private const float Height = 0.08f;
        private const float Seconds = 0.38f;
        // From the shooter to the target.
        private const float GrowSeconds = 0.07f;
        private const float RejagSeconds = 0.08f;
        // Pieces along the bolt per metre (between MinPieces and MaxPieces), and bends per metre.
        private const float PiecesPerMetre = 12f;
        private const int MinPieces = 10;
        private const int MaxPieces = 48;
        private const float BendsPerMetre = 2.2f;
        // How far the bolt strays either side of the straight line at its middle (metres), and how
        // fast its bends move along.
        private const float Jag = 0.13f;
        private const float Writhe = 6f;
        // Each dot reaches this much past its stretch of the path, so the dots overlap smoothly.
        private const float Overlap = 1.9f;
        private const float GlowWidth = 0.34f;
        private const float OutlineWidth = 0.11f;
        private const float CoreWidth = 0.055f;
        private const int Forks = 2;
        // The glow, the outline and the core, over everything else on the floor.
        private const int GlowOrder = 29;
        private const int OutlineOrder = 30;
        private const int CoreOrder = 31;
        private static readonly Color OutlineColour = new Color(0.22f, 0.1f, 0.04f, 1f);
        private static readonly Color CoreColour = new Color(1f, 0.99f, 0.88f, 1f);

        // One line of the bolt: its dots in each layer and the path they follow (floor metres from
        // the shooter, x across and y up the floor).
        private sealed class Strand
        {
            public Vector2[] Path;
            public SpriteRenderer[] Glow;
            public SpriteRenderer[] Outline;
            public SpriteRenderer[] Core;
            public float Seed;
            // Its width as a share of the main bolt's.
            public float Thickness;
        }

        private Strand main;
        private Strand[] forks;
        // Where along the bolt each fork starts (0 to 1) and where it reaches to from there.
        private float[] forkAt;
        private Vector2[] forkReach;
        private Vector2 target;
        private Color colour;
        private float dotSize;
        private float age;
        private float rejagAt;
        private bool sparked;

        public static void Play(Vector3 from, Vector3 to, Color colour, Transform parent)
        {
            var holder = new GameObject("Stun Bolt");
            holder.transform.SetParent(parent, true);
            // Local x is the floor's x and local y the floor's z.
            holder.transform.SetPositionAndRotation(new Vector3(from.x, Height, from.z), Quaternion.Euler(90f, 0f, 0f));
            var bolt = holder.AddComponent<StunBolt>();
            bolt.target = new Vector2(to.x - from.x, to.z - from.z);
            bolt.colour = colour;
            bolt.Build();
            StunSparks.Play(new Vector3(from.x, Height, from.z), colour, 0.5f, parent);
        }

        private void Build()
        {
            Sprite dot = PosterSprites.Dot;
            dotSize = Mathf.Max(0.0001f, dot.bounds.size.x);
            float length = target.magnitude;
            int pieces = Mathf.Clamp(Mathf.CeilToInt(length * PiecesPerMetre), MinPieces, MaxPieces);
            main = MakeStrand(pieces, 1f, true);

            forks = new Strand[Forks];
            forkAt = new float[Forks];
            forkReach = new Vector2[Forks];
            Vector2 along = target.normalized;
            for (int i = 0; i < Forks; i++)
            {
                forks[i] = MakeStrand(Mathf.Max(4, pieces / 4), 0.55f, false);
                forkAt[i] = Random.Range(0.3f, 0.7f);
                float turn = Random.Range(28f, 55f) * (i % 2 == 0 ? 1f : -1f) * Mathf.Deg2Rad;
                Vector2 heading = new Vector2(along.x * Mathf.Cos(turn) - along.y * Mathf.Sin(turn), along.x * Mathf.Sin(turn) + along.y * Mathf.Cos(turn));
                forkReach[i] = heading * (length * Random.Range(0.16f, 0.28f));
            }

            Reseed();
            Draw();
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= Seconds)
            {
                Destroy(gameObject);
                return;
            }

            if (age >= rejagAt) Reseed();
            Draw();
        }

        private void Draw()
        {
            float grown = Mathf.Clamp01(age / GrowSeconds);
            grown = 1f - (1f - grown) * (1f - grown);
            float fading = Mathf.Clamp01((age - GrowSeconds) / (Seconds - GrowSeconds));
            float alpha = 1f - fading * fading * fading;
            float flicker = Random.Range(0.85f, 1.12f);

            if (grown >= 1f && !sparked)
            {
                sparked = true;
                Vector3 hit = transform.TransformPoint(new Vector3(target.x, target.y, 0f));
                StunSparks.Play(hit, colour, 1f, transform.parent);
            }

            Trace(main.Path, Vector2.zero, target * grown, main.Seed, Jag * Mathf.Lerp(0.4f, 1f, grown));
            Show(main, alpha, flicker, 1f);

            for (int i = 0; i < Forks; i++)
            {
                Strand fork = forks[i];
                if (grown < 1f)
                {
                    Hide(fork);
                    continue;
                }

                Vector2 start = PointAlong(main.Path, forkAt[i]);
                Trace(fork.Path, start, start + forkReach[i], fork.Seed, Jag * 0.5f);
                Show(fork, alpha * 0.9f, flicker, 0f);
            }
        }

        // A new shape for every strand.
        private void Reseed()
        {
            rejagAt = age + RejagSeconds;
            main.Seed = Random.Range(0f, 100f);
            foreach (Strand fork in forks) fork.Seed = Random.Range(0f, 100f);
        }

        // Smooth bends between start and end, still at both ends and straying most in the middle.
        private void Trace(Vector2[] path, Vector2 start, Vector2 end, float seed, float jag)
        {
            Vector2 line = end - start;
            Vector2 across = new Vector2(-line.y, line.x).normalized;
            float bends = Mathf.Max(1f, line.magnitude * BendsPerMetre);
            float time = age * Writhe;
            for (int i = 0; i < path.Length; i++)
            {
                float t = i / (float)(path.Length - 1);
                float broad = Mathf.PerlinNoise(seed + t * bends, time) * 2f - 1f;
                float fine = Mathf.PerlinNoise(seed + 37f + t * bends * 2.6f, time * 1.6f) * 2f - 1f;
                float stray = (broad * 0.8f + fine * 0.3f) * jag * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.7f);
                path[i] = start + line * t + across * stray;
            }
        }

        private static Vector2 PointAlong(Vector2[] path, float t)
        {
            float at = t * (path.Length - 1);
            int below = Mathf.Min(path.Length - 2, Mathf.FloorToInt(at));
            return Vector2.Lerp(path[below], path[below + 1], at - below);
        }

        // Lays the strand's dots along its path: each stretched over its stretch of the path, thin at
        // the start (or at both ends of the main bolt), at this alpha. endWidth is its width at the
        // far end as a share of its full width.
        private void Show(Strand strand, float alpha, float flicker, float endWidth)
        {
            Color glow = colour;
            glow.a = 0.4f * alpha;
            Color outline = OutlineColour;
            outline.a = 0.85f * alpha;
            Color core = Color.Lerp(CoreColour, colour, 0.2f);
            core.a = alpha;

            int count = strand.Path.Length - 1;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = strand.Path[i];
                Vector2 b = strand.Path[i + 1];
                float t = (i + 0.5f) / count;
                float width = strand.Thickness * flicker * Taper(t, endWidth);
                float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                float length = Vector2.Distance(a, b) * Overlap;
                Place(strand.Glow[i], a, b, angle, length + GlowWidth * width * 0.5f, GlowWidth * width, glow);
                Place(strand.Outline[i], a, b, angle, length + OutlineWidth * width * 0.3f, OutlineWidth * width, outline);
                Place(strand.Core[i], a, b, angle, length, CoreWidth * width, core);
            }
        }

        // Thin at the shooter, full along the middle and narrowing to endWidth at the far end.
        private static float Taper(float t, float endWidth)
        {
            float start = Mathf.SmoothStep(0.35f, 1f, Mathf.Clamp01(t / 0.15f));
            float end = Mathf.Lerp(1f, endWidth, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, t)));
            return start * end;
        }

        private void Place(SpriteRenderer piece, Vector2 a, Vector2 b, float angle, float length, float width, Color tint)
        {
            if (piece == null) return;
            piece.enabled = true;
            Transform place = piece.transform;
            Vector2 middle = (a + b) * 0.5f;
            place.localPosition = new Vector3(middle.x, middle.y, 0f);
            place.localRotation = Quaternion.Euler(0f, 0f, angle);
            place.localScale = new Vector3(Mathf.Max(width, length) / dotSize, width / dotSize, 1f);
            piece.color = tint;
        }

        private static void Hide(Strand strand)
        {
            foreach (SpriteRenderer piece in strand.Glow) if (piece != null) piece.enabled = false;
            foreach (SpriteRenderer piece in strand.Outline) piece.enabled = false;
            foreach (SpriteRenderer piece in strand.Core) piece.enabled = false;
        }

        // A strand of pieces dots; forks have no glow of their own (the main bolt's lights them).
        private Strand MakeStrand(int pieces, float thickness, bool glows)
        {
            var strand = new Strand
            {
                Path = new Vector2[pieces + 1],
                Glow = new SpriteRenderer[pieces],
                Outline = new SpriteRenderer[pieces],
                Core = new SpriteRenderer[pieces],
                Thickness = thickness,
            };
            for (int i = 0; i < pieces; i++)
            {
                if (glows) strand.Glow[i] = Dot("Glow", GlowOrder);
                strand.Outline[i] = Dot("Outline", OutlineOrder);
                strand.Core[i] = Dot("Core", CoreOrder);
            }

            return strand;
        }

        private SpriteRenderer Dot(string name, int order)
        {
            var piece = new GameObject(name, typeof(SpriteRenderer));
            piece.transform.SetParent(transform, false);
            var renderer = piece.GetComponent<SpriteRenderer>();
            renderer.sprite = PosterSprites.Dot;
            renderer.sortingOrder = order;
            renderer.enabled = false;
            return renderer;
        }
    }
}
