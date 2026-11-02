using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The sparks of a stun gun's bolt where it leaves the gun and where it hits: a bright flash, a
    // quick ring of electricity, and sparks shooting out, stretched along their way, slowing,
    // shrinking and fading; a few fall short and wink out. All in the bolt's colour round a white-hot
    // middle and scaled by strength (1 where it hits). Lies flat on the floor like the other effects
    // and removes itself when the last piece is gone.
    public sealed class StunSparks : MonoBehaviour
    {
        // Over the bolt (29 to 31) and the thieves (8).
        private const int Order = 32;
        // Sizes and reach (metres) at strength 1.
        private const float FlashSize = 0.55f;
        private const float RingSize = 0.9f;
        private const int SparkCount = 14;
        private const float SparkReach = 0.75f;
        private const float SparkWidth = 0.05f;
        private static readonly Color HotColour = new Color(1f, 0.99f, 0.88f, 1f);

        private enum Motion
        {
            // Swells a little and fades fast.
            Flash,
            // Grows from the middle and fades.
            Ring,
            // Shoots out and slows, stretched along its way, shrinking and fading.
            Spark
        }

        private sealed class Piece
        {
            public SpriteRenderer Renderer;
            public Motion Motion;
            public float Start;
            public float Duration;
            public Vector2 Reach;
            public float Size;
            public Color Tint;
        }

        private readonly List<Piece> pieces = new List<Piece>();
        private float elapsed;
        private float lifetime;

        public static void Play(Vector3 at, Color colour, float strength, Transform parent)
        {
            var sparks = new GameObject("Stun Sparks").AddComponent<StunSparks>();
            sparks.transform.SetParent(parent, true);
            sparks.transform.SetPositionAndRotation(at, Quaternion.Euler(90f, 0f, 0f));
            sparks.Build(colour, strength);
        }

        private void Build(Color colour, float strength)
        {
            Color glow = colour;
            glow.a = 0.75f;
            Add(PosterSprites.Dot, Motion.Flash, 0f, 0.16f, Vector2.zero, FlashSize * strength, HotColour);
            Add(PosterSprites.Dot, Motion.Flash, 0f, 0.24f, Vector2.zero, FlashSize * 1.8f * strength, glow);
            Add(PosterSprites.Ring, Motion.Ring, 0f, 0.28f, Vector2.zero, RingSize * strength, colour);

            int count = Mathf.Max(4, Mathf.RoundToInt(SparkCount * strength));
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                // Most fly far and fast; a few fall short and wink out.
                bool fallsShort = Random.value < 0.3f;
                float reach = SparkReach * strength * (fallsShort ? Random.Range(0.2f, 0.45f) : Random.Range(0.6f, 1.2f));
                Color tint = Random.value < 0.4f ? HotColour : colour;
                Add(PosterSprites.Dot, Motion.Spark, Random.Range(0f, 0.04f), fallsShort ? Random.Range(0.12f, 0.2f) : Random.Range(0.25f, 0.4f),
                    direction * reach, SparkWidth * Random.Range(0.7f, 1.3f) * Mathf.Sqrt(strength), tint);
            }
        }

        private void Add(Sprite sprite, Motion motion, float start, float duration, Vector2 reach, float size, Color tint)
        {
            var pieceObject = new GameObject(motion.ToString(), typeof(SpriteRenderer));
            pieceObject.transform.SetParent(transform, false);
            var renderer = pieceObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = Order;
            var piece = new Piece
            {
                Renderer = renderer,
                Motion = motion,
                Start = start,
                Duration = duration,
                Reach = reach,
                Size = size / Mathf.Max(0.0001f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y)),
                Tint = tint,
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
            bool shown = t >= 0f && t < 1f;
            piece.Renderer.enabled = shown;
            if (!shown) return;

            Transform place = piece.Renderer.transform;
            Color tint = piece.Tint;
            float fade = 1f - t;
            switch (piece.Motion)
            {
                case Motion.Flash:
                    place.localScale = Vector3.one * (piece.Size * (0.7f + 0.5f * Mathf.Sqrt(t)));
                    tint.a *= fade * fade;
                    break;
                case Motion.Ring:
                    place.localScale = Vector3.one * (piece.Size * (0.25f + 0.75f * (1f - fade * fade)));
                    tint.a *= fade;
                    break;
                case Motion.Spark:
                    float travelled = 1f - fade * fade * fade;
                    float speed = 3f * fade * fade;
                    Vector2 at = piece.Reach * travelled;
                    place.localPosition = new Vector3(at.x, at.y, 0f);
                    float angle = Mathf.Atan2(piece.Reach.y, piece.Reach.x) * Mathf.Rad2Deg;
                    place.localRotation = Quaternion.Euler(0f, 0f, angle);
                    float width = piece.Size * Mathf.Lerp(0.3f, 1f, fade);
                    place.localScale = new Vector3(width * (1f + 3.5f * speed), width, 1f);
                    tint.a *= Mathf.Clamp01(fade * 1.5f);
                    break;
            }

            piece.Renderer.color = tint;
        }
    }
}
