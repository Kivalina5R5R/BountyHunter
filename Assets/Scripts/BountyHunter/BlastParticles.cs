using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // The bits flying out of a bomb blast, around the explosion art: a quick shockwave ring, hot
    // spark streaks and dark debris, and the floor under the blast darkening (see-through, never
    // solid black) for a while before it clears. All sized from the blast radius. Lies flat on the floor like the other effects
    // and removes itself when the last piece is gone.
    public sealed class BlastParticles : MonoBehaviour
    {
        private const float Height = 0.07f;
        // The darkened floor comes in fast, stays, then clears slowly.
        private const float ScorchSeconds = 3.2f;
        private const float ScorchAlpha = 0.42f;
        // The scorch over the floor and under bushes (5); the rest over the explosion (6) and under
        // thieves (8), so they never hide a thief.
        private const int ScorchOrder = 4;
        private const int SparkOrder = 7;

        private static readonly Color[] SparkColors =
        {
            new Color(1f, 0.95f, 0.7f, 1f),
            new Color(1f, 0.75f, 0.25f, 1f),
            new Color(1f, 0.45f, 0.15f, 1f)
        };
        private static readonly Color DebrisColor = new Color(0.2f, 0.17f, 0.16f, 1f);
        private static readonly Color RingColor = new Color(1f, 0.9f, 0.7f, 1f);
        private static readonly Color ScorchColor = new Color(0.08f, 0.06f, 0.05f, 1f);

        private enum Motion
        {
            // Shoots out and slows down, shrinking; stretched along its path.
            Streak,
            // Shoots out and slows down, keeping its size.
            Chunk,
            // Grows from the centre and fades.
            Ring,
            // Stays put: darkens quickly, holds, then clears slowly.
            Scorch
        }

        private sealed class Piece
        {
            public SpriteRenderer Renderer;
            public Motion Motion;
            public float Start;
            public float Duration;
            public Vector2 From;
            public Vector2 To;
            public float Size;
            public float Spin;
            public float Alpha;
        }

        private readonly List<Piece> pieces = new List<Piece>();
        private float elapsed;
        private float lifetime;

        // Plays the particles of a blast of radius at position.
        public static void Play(Vector3 position, float radius, Transform parent)
        {
            var blast = new GameObject("Blast Particles").AddComponent<BlastParticles>();
            blast.transform.SetParent(parent, true);
            blast.transform.SetPositionAndRotation(new Vector3(position.x, Height, position.z), Quaternion.Euler(90f, 0f, 0f));
            blast.Build(radius);
        }

        private void Build(float radius)
        {
            // The soft dot is solid to about half its width, so this darkens the whole blast area.
            Add(PosterSprites.Dot, ScorchColor, ScorchOrder, Motion.Scorch, 0f, ScorchSeconds, Vector2.zero, Vector2.zero, radius * 2.6f, 0f, ScorchAlpha);
            Add(PosterSprites.Ring, RingColor, SparkOrder, Motion.Ring, 0f, 0.35f, Vector2.zero, Vector2.zero, radius * 2.6f, 0f, 0.9f);

            for (int i = 0; i < 20; i++)
            {
                Vector2 direction = Direction(Random.Range(0f, 360f));
                Color color = SparkColors[Random.Range(0, SparkColors.Length)];
                Add(PosterSprites.Dot, color, SparkOrder, Motion.Streak, Random.Range(0f, 0.05f), Random.Range(0.35f, 0.6f),
                    direction * radius * 0.2f, direction * radius * Random.Range(1.2f, 1.9f), radius * Random.Range(0.07f, 0.12f), 0f, 1f);
            }

            for (int i = 0; i < 9; i++)
            {
                Vector2 direction = Direction(Random.Range(0f, 360f));
                Add(PosterSprites.Dot, DebrisColor, SparkOrder, Motion.Chunk, Random.Range(0f, 0.04f), Random.Range(0.5f, 0.8f),
                    direction * radius * 0.2f, direction * radius * Random.Range(1f, 1.5f), radius * Random.Range(0.05f, 0.09f), 0f, 1f);
            }
        }

        private void Add(Sprite sprite, Color color, int order, Motion motion, float start, float duration, Vector2 from, Vector2 to, float size, float spin, float alpha)
        {
            var pieceObject = new GameObject(motion.ToString(), typeof(SpriteRenderer));
            pieceObject.transform.SetParent(transform, false);
            var spriteRenderer = pieceObject.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = order;
            var piece = new Piece
            {
                Renderer = spriteRenderer,
                Motion = motion,
                Start = start,
                Duration = duration,
                From = from,
                To = to,
                Size = size / Mathf.Max(0.0001f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y)),
                Spin = spin,
                Alpha = alpha
            };
            pieces.Add(piece);
            lifetime = Mathf.Max(lifetime, start + duration);
            Apply(piece, -1f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            foreach (Piece piece in pieces)
            {
                Apply(piece, (elapsed - piece.Start) / piece.Duration);
            }

            if (elapsed >= lifetime) Destroy(gameObject);
        }

        // t below 0 is not started yet, above 1 is finished; both are hidden.
        private static void Apply(Piece piece, float t)
        {
            bool visible = t >= 0f && t <= 1f;
            piece.Renderer.enabled = visible;
            if (!visible) return;

            Transform pieceTransform = piece.Renderer.transform;
            // Fast out, slowing down, as if through air.
            float travelled = 1f - Mathf.Pow(1f - t, 3f);
            Vector2 position = Vector2.Lerp(piece.From, piece.To, travelled);
            var scale = new Vector3(piece.Size, piece.Size, 1f);
            float degrees = piece.Spin * t;
            float alpha = piece.Alpha;
            switch (piece.Motion)
            {
                case Motion.Streak:
                    // Long while fast, a dot as it stops.
                    Vector2 path = piece.To - piece.From;
                    degrees = Mathf.Atan2(path.y, path.x) * Mathf.Rad2Deg;
                    scale.x *= Mathf.Lerp(3.5f, 1f, travelled) * (1f - t * 0.6f);
                    scale.y *= 1f - t * 0.6f;
                    alpha *= 1f - t * t;
                    break;
                case Motion.Chunk:
                    alpha *= t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                    break;
                case Motion.Ring:
                    position = Vector2.zero;
                    scale *= Mathf.Lerp(0.15f, 1f, travelled);
                    alpha *= 1f - t;
                    break;
                case Motion.Scorch:
                    position = Vector2.zero;
                    alpha *= t < 0.04f ? t / 0.04f : t < 0.5f ? 1f : Mathf.SmoothStep(1f, 0f, (t - 0.5f) / 0.5f);
                    break;
            }

            pieceTransform.localPosition = new Vector3(position.x, position.y, 0f);
            pieceTransform.localRotation = Quaternion.Euler(0f, 0f, degrees);
            pieceTransform.localScale = scale;
            Color color = piece.Renderer.color;
            color.a = alpha;
            piece.Renderer.color = color;
        }

        // A direction on the floor at angleDegrees, where 90 points toward the wall.
        private static Vector2 Direction(float angleDegrees)
        {
            float angle = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }
}
