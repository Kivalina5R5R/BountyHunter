using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // The cartoon scuffle played where a thief is caught: a fight cloud pops up over it and boils,
    // paws and handcuffs poke out of it, stars and POW / BAM words pop, small puffs fly off,
    // then the cloud bursts into puffs and fades. A finale (left empty for the thieves) slams down
    // in the middle as it bursts, like a stamp, and stays a moment. Lies flat on the floor like the
    // other effects, with its top toward the wall, so the words read upright on the floor display.
    public sealed class CatchBrawl : MonoBehaviour
    {
        // The pieces the scuffle is built from; any group left empty is simply skipped.
        [Serializable]
        public sealed class Art
        {
            // Variations of the same cloud; swapping them makes it boil.
            [SerializeField] private Sprite[] clouds;
            [SerializeField] private Sprite[] puffs;
            // Paws and handcuffs poking out of the cloud.
            [SerializeField] private Sprite[] limbs;
            [SerializeField] private Sprite[] stars;
            [SerializeField] private Sprite[] words;
            [SerializeField] private Sprite[] swooshes;
            // Slams down as a stamp when the cloud bursts (one picked); empty for none.
            [SerializeField] private Sprite[] finale;

            public Sprite[] Clouds => clouds;
            public Sprite[] Puffs => puffs;
            public Sprite[] Limbs => limbs;
            public Sprite[] Stars => stars;
            public Sprite[] Words => words;
            public Sprite[] Swooshes => swooshes;
            public Sprite[] Finale => finale;
        }

        private const float Height = 0.08f;
        // Cloud width in world units (a thief is 0.6 tall, a player marker 0.665 wide).
        private const float CloudSize = 1.3f;
        private const float PopSeconds = 0.18f;
        private const float BoilUntil = 1.05f;
        private const float BurstSeconds = 0.35f;
        private const float BoilFramesPerSecond = 14f;
        // Limbs sit under the cloud, everything else on top. Above thieves (8) and leaves (9),
        // below player markers (20), so the players can still see where they stand.
        private const int LimbOrder = 13;
        private const int CloudOrder = 14;
        private const int TopOrder = 16;

        private enum Motion
        {
            // Pops up with an overshoot, holds, then shrinks away.
            Pop,
            // Pushes out of the cloud edge and back in.
            Poke,
            // Flies out and fades.
            Fly,
            // Slams down from big, holds, then fades.
            Stamp
        }

        private sealed class Piece
        {
            public SpriteRenderer Renderer;
            public Motion Motion;
            public float Start;
            public float Duration;
            public Vector3 From;
            public Vector3 To;
            public float Size;
            public float Yaw;
            public float Spin;
        }

        private readonly List<Piece> pieces = new List<Piece>();
        private Art art;
        private SpriteRenderer cloud;
        private float cloudScale;
        private float cloudYaw;
        private float elapsed;
        private int cloudFrame;
        // When the last piece is done.
        private float lifetime;

        // Plays the scuffle centred on position. parent only files it in the hierarchy.
        public static void Play(Vector3 position, Art pieces, Transform parent)
        {
            if (pieces == null || !HasAny(pieces.Clouds)) return;

            var brawl = new GameObject("Catch Brawl").AddComponent<CatchBrawl>();
            brawl.transform.SetParent(parent, true);
            brawl.transform.position = new Vector3(position.x, Height, position.z);
            brawl.Build(pieces);
        }

        private static bool HasAny(Sprite[] sprites)
        {
            return sprites != null && sprites.Length > 0;
        }

        private void Build(Art pieces)
        {
            art = pieces;
            Sprite first = art.Clouds[0];
            cloud = CreateRenderer("Cloud", first, CloudOrder);
            cloudScale = CloudSize / Mathf.Max(0.0001f, first.bounds.size.x);
            cloudYaw = Random.Range(-6f, 6f);

            AddLimbs();
            AddStars();
            AddWords();
            AddSwooshes();
            AddSidePuffs();
            AddFinalPuffs();
            AddFinale();
            lifetime = BoilUntil + BurstSeconds + 0.2f;
            foreach (Piece piece in this.pieces)
            {
                lifetime = Mathf.Max(lifetime, piece.Start + piece.Duration);
            }

            ApplyCloud();
        }

        // Three to five pokes at spread-out angles, overlapping so the fight never pauses.
        private void AddLimbs()
        {
            if (!HasAny(art.Limbs)) return;

            int count = Random.Range(3, 6);
            float angle = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                angle += Random.Range(80f, 150f);
                Vector3 direction = Direction(angle);
                AddPiece(art.Limbs, Motion.Poke, LimbOrder, 0.32f, PopSeconds + i * 0.17f, 0.32f,
                    direction * CloudSize * 0.3f, direction * CloudSize * 0.52f, angle, 0f);
            }
        }

        private void AddStars()
        {
            if (!HasAny(art.Stars)) return;

            for (int i = 0; i < 4; i++)
            {
                Vector3 spot = Direction(Random.Range(0f, 360f)) * CloudSize * Random.Range(0.15f, 0.45f);
                AddPiece(art.Stars, Motion.Pop, TopOrder, Random.Range(0.13f, 0.2f), PopSeconds + Random.Range(0f, 0.7f), 0.35f,
                    spot, spot, Random.Range(-20f, 20f), Random.Range(-360f, 360f));
            }
        }

        // Two different words, one after the other, on opposite sides of the cloud.
        private void AddWords()
        {
            if (!HasAny(art.Words)) return;

            int first = Random.Range(0, art.Words.Length);
            float side = Random.value < 0.5f ? -1f : 1f;
            for (int i = 0; i < Mathf.Min(2, art.Words.Length); i++)
            {
                Sprite word = art.Words[(first + i) % art.Words.Length];
                var spot = new Vector3(side * CloudSize * Random.Range(0.12f, 0.25f), 0f, CloudSize * Random.Range(-0.2f, 0.2f));
                AddPiece(new[] { word }, Motion.Pop, TopOrder + 1, CloudSize * 0.62f, PopSeconds + 0.05f + i * 0.4f, 0.5f,
                    spot, spot, Random.Range(-12f, 12f), 0f);
                side = -side;
            }
        }

        private void AddSwooshes()
        {
            if (!HasAny(art.Swooshes)) return;

            for (int i = 0; i < 3; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector3 spot = Direction(angle) * CloudSize * 0.62f;
                // The arc follows the cloud's edge.
                AddPiece(art.Swooshes, Motion.Pop, CloudOrder, 0.36f, PopSeconds + Random.Range(0f, 0.6f), 0.3f,
                    spot, spot, angle - 90f, Random.Range(-90f, 90f));
            }
        }

        // Little puffs shooting off the sides while the fight goes on.
        private void AddSidePuffs()
        {
            if (!HasAny(art.Puffs)) return;

            for (int i = 0; i < 3; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector3 direction = Direction(angle);
                AddPiece(art.Puffs, Motion.Fly, CloudOrder, 0.26f, PopSeconds + Random.Range(0.1f, 0.7f), 0.45f,
                    direction * CloudSize * 0.42f, direction * CloudSize * 0.8f, Random.Range(0f, 360f), Random.Range(-60f, 60f));
            }
        }

        // The cloud bursts into puffs all around when the fight ends.
        private void AddFinalPuffs()
        {
            if (!HasAny(art.Puffs)) return;

            int count = 7;
            for (int i = 0; i < count; i++)
            {
                float angle = i * 360f / count + Random.Range(-15f, 15f);
                Vector3 direction = Direction(angle);
                AddPiece(art.Puffs, Motion.Fly, TopOrder, Random.Range(0.3f, 0.42f), BoilUntil - 0.05f, BurstSeconds + 0.15f,
                    direction * CloudSize * 0.2f, direction * CloudSize * Random.Range(0.6f, 0.8f), Random.Range(0f, 360f), Random.Range(-90f, 90f));
            }
        }

        // The stamp lands just before the cloud bursts, so it is seen through the burst.
        private void AddFinale()
        {
            if (!HasAny(art.Finale)) return;

            AddPiece(art.Finale, Motion.Stamp, TopOrder + 2, CloudSize * 0.7f, BoilUntil - 0.1f, 0.85f,
                Vector3.zero, Vector3.zero, Random.Range(-10f, 10f), 0f);
        }

        private void AddPiece(Sprite[] sprites, Motion motion, int order, float size, float start, float duration, Vector3 from, Vector3 to, float yaw, float spin)
        {
            Sprite sprite = sprites[Random.Range(0, sprites.Length)];
            if (sprite == null) return;

            var piece = new Piece
            {
                Renderer = CreateRenderer(sprite.name, sprite, order),
                Motion = motion,
                Start = start,
                Duration = duration,
                From = from,
                To = to,
                Size = size / Mathf.Max(0.0001f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y)),
                Yaw = yaw,
                Spin = spin
            };
            pieces.Add(piece);
            Apply(piece, -1f);
        }

        private SpriteRenderer CreateRenderer(string pieceName, Sprite sprite, int order)
        {
            var piece = new GameObject(pieceName, typeof(SpriteRenderer));
            piece.transform.SetParent(transform, false);
            var spriteRenderer = piece.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = order;
            return spriteRenderer;
        }

        // A direction on the floor at angleDegrees, where 90 points toward the wall (screen up).
        private static Vector3 Direction(float angleDegrees)
        {
            float angle = angleDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            ApplyCloud();
            foreach (Piece piece in pieces)
            {
                Apply(piece, (elapsed - piece.Start) / piece.Duration);
            }

            if (elapsed >= lifetime) Destroy(gameObject);
        }

        private void ApplyCloud()
        {
            float scale;
            float alpha = 1f;
            if (elapsed < PopSeconds)
            {
                scale = Overshoot(elapsed / PopSeconds);
            }
            else if (elapsed < BoilUntil)
            {
                // Boiling: the outline swaps between variations and the whole cloud jostles.
                int frame = Mathf.FloorToInt(elapsed * BoilFramesPerSecond);
                if (frame != cloudFrame)
                {
                    cloudFrame = frame;
                    cloud.sprite = art.Clouds[Random.Range(0, art.Clouds.Length)];
                    cloud.flipX = Random.value < 0.5f;
                    cloud.transform.localPosition = new Vector3(Random.Range(-0.03f, 0.03f), 0f, Random.Range(-0.03f, 0.03f));
                }

                scale = 1f + Mathf.Sin(elapsed * 31f) * 0.04f;
            }
            else
            {
                // Bursts: swells a little and fades away.
                float t = Mathf.Clamp01((elapsed - BoilUntil) / BurstSeconds);
                scale = 1f + 0.2f * t;
                alpha = 1f - t * t;
            }

            cloud.transform.localRotation = Quaternion.Euler(90f, cloudYaw, 0f);
            cloud.transform.localScale = Vector3.one * (cloudScale * scale);
            SetAlpha(cloud, alpha);
        }

        // t below 0 is not started yet, above 1 is finished; both are hidden.
        private static void Apply(Piece piece, float t)
        {
            bool visible = t >= 0f && t <= 1f;
            piece.Renderer.enabled = visible;
            if (!visible) return;

            Transform pieceTransform = piece.Renderer.transform;
            float scale = 1f;
            float alpha = 1f;
            Vector3 position = piece.From;
            switch (piece.Motion)
            {
                case Motion.Pop:
                    scale = t < 0.25f ? Overshoot(t / 0.25f) : t > 0.75f ? (1f - t) / 0.25f : 1f;
                    break;
                case Motion.Poke:
                    // Out fast, a short hold, then back in.
                    position = Vector3.Lerp(piece.From, piece.To, Mathf.Sin(Mathf.Min(1f, t * 1.15f) * Mathf.PI));
                    break;
                case Motion.Fly:
                    position = Vector3.Lerp(piece.From, piece.To, 1f - (1f - t) * (1f - t));
                    scale = Mathf.Lerp(0.6f, 1.15f, t);
                    alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f;
                    break;
                case Motion.Stamp:
                    // Down hard from twice its size in the first fifth, a little bounce, then fades at the end.
                    float slam = Mathf.Clamp01(t / 0.2f);
                    scale = slam < 1f ? Mathf.Lerp(2.2f, 1f, slam * slam) : 1f + Mathf.Sin(Mathf.Clamp01((t - 0.2f) / 0.15f) * Mathf.PI) * 0.06f;
                    alpha = t < 0.2f ? slam : t > 0.75f ? (1f - t) / 0.25f : 1f;
                    break;
            }

            pieceTransform.localPosition = position;
            // Limbs point away from the centre; their art points up (toward the wall) at yaw 0.
            float yaw = piece.Motion == Motion.Poke ? 90f - piece.Yaw : piece.Yaw + piece.Spin * t;
            pieceTransform.localRotation = Quaternion.Euler(90f, yaw, 0f);
            pieceTransform.localScale = Vector3.one * (piece.Size * scale);
            SetAlpha(piece.Renderer, alpha);
        }

        // 0 to 1 with a springy overshoot past 1 on the way.
        private static float Overshoot(float t)
        {
            t = Mathf.Clamp01(t);
            const float s = 2.2f;
            float u = t - 1f;
            return 1f + u * u * ((s + 1f) * u + s);
        }

        private static void SetAlpha(SpriteRenderer spriteRenderer, float alpha)
        {
            Color color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }
    }
}
