using System;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // A thief's bomb on the floor: pops down with a bounce, fizzes with its fuse lit over a red
    // danger zone that pulses faster as the fuse burns, squashes, then bursts once into a cartoon
    // explosion that holds still and fades. Calls back once at the moment it explodes. Lies flat on the floor like
    // the other effects, with its top toward the wall.
    public sealed class Bomb : MonoBehaviour
    {
        // The fuse and squash frames are cut from Assets/ART/FX/Bomb/Bomb.mp4; the explosions are
        // drawn in its style without the smoke.
        [Serializable]
        public sealed class Art
        {
            // The lit fuse, looped while it burns.
            [SerializeField] private Sprite[] fuse;
            // The bomb swelling just before it bursts.
            [SerializeField] private Sprite[] squash;
            // Explosion variants; each bomb picks one.
            [SerializeField] private Sprite[] boom;

            public Sprite[] Fuse => fuse;
            public Sprite[] Squash => squash;
            public Sprite[] Boom => boom;
        }

        private const float Height = 0.06f;
        private const float DropSeconds = 0.22f;
        private const float FuseFramesPerSecond = 9f;
        private const float SquashSeconds = 0.22f;
        // The explosion bursts out to full size, holds still, then fades.
        private const float GrowSeconds = 0.12f;
        private const float HoldSeconds = 0.25f;
        private const float FadeSeconds = 0.3f;
        // The burst's spike tips reach the edge of its square art, so at full size it is exactly the
        // blast (the red danger zone).
        private const float BoomWidthVsRadius = 2f;
        // The bomb's round body is this part of a fuse frame's width.
        private const float BodyVsFrame = 0.7f;
        // All over bushes (5) and under thieves (8), so a thief running through a blast stays in
        // sight. The zone is gone by the time the explosion takes its place.
        private const int ZoneOrder = 6;
        private const int BombOrder = 7;
        private const int BoomOrder = 6;
        private static readonly Color ZoneColor = new Color(0.95f, 0.15f, 0.1f, 1f);
        private static readonly Color WarningTint = new Color(1f, 0.55f, 0.5f, 1f);

        private Art art;
        private float fuseSeconds;
        private float blastRadius;
        private float bombScale;
        private float boomScale;
        private SpriteRenderer body;
        private SpriteRenderer zone;
        private SpriteRenderer boom;
        private Action<Bomb> exploded;
        private float elapsed;
        private bool hasExploded;

        public float BlastRadius => blastRadius;

        // Drops a bomb at position that explodes after fuseSeconds, calling onExploded then.
        // bombSize is the width of the bomb's body on the floor. parent only files it in the hierarchy.
        public static Bomb Drop(Vector3 position, Art art, float fuseSeconds, float blastRadius, float bombSize, Transform parent, Action<Bomb> onExploded)
        {
            var bomb = new GameObject("Bomb").AddComponent<Bomb>();
            bomb.transform.SetParent(parent, true);
            bomb.transform.SetPositionAndRotation(new Vector3(position.x, Height, position.z), Quaternion.Euler(90f, 0f, 0f));
            bomb.Build(art, fuseSeconds, blastRadius, bombSize, onExploded);
            return bomb;
        }

        private void Build(Art pieces, float fuse, float radius, float bombSize, Action<Bomb> onExploded)
        {
            art = pieces;
            fuseSeconds = fuse;
            blastRadius = radius;
            exploded = onExploded;

            zone = CreateRenderer("Danger Zone", PosterSprites.Dot, ZoneOrder);
            zone.transform.localScale = Vector3.one * (radius * 2f / PosterSprites.Dot.bounds.size.x);

            Sprite first = FirstOf(art.Fuse);
            body = CreateRenderer("Body", first, BombOrder);
            bombScale = first != null ? bombSize / (first.bounds.size.x * BodyVsFrame) : 1f;

            Sprite burst = RandomOf(art.Boom);
            boom = CreateRenderer("Boom", burst, BoomOrder);
            boomScale = burst != null ? radius * BoomWidthVsRadius / burst.bounds.size.x : 1f;
            boom.transform.localScale = Vector3.one * boomScale;
            // A random turn so no two blasts look the same.
            boom.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            boom.enabled = false;
            UpdateFuse();
        }

        private static Sprite RandomOf(Sprite[] sprites)
        {
            return sprites != null && sprites.Length > 0 ? sprites[UnityEngine.Random.Range(0, sprites.Length)] : null;
        }

        private static Sprite FirstOf(Sprite[] sprites)
        {
            return sprites != null && sprites.Length > 0 ? sprites[0] : null;
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

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (!hasExploded)
            {
                UpdateFuse();
                if (elapsed >= fuseSeconds) Explode();
                return;
            }

            UpdateBoom(elapsed - fuseSeconds);
        }

        private void UpdateFuse()
        {
            float left = fuseSeconds - elapsed;
            Sprite[] squash = art.Squash;
            bool squashing = left <= SquashSeconds && squash != null && squash.Length > 0;
            if (squashing)
            {
                float t = Mathf.Clamp01(1f - left / SquashSeconds);
                body.sprite = squash[Mathf.Min(squash.Length - 1, Mathf.FloorToInt(t * squash.Length))];
            }
            else if (art.Fuse != null && art.Fuse.Length > 0)
            {
                body.sprite = art.Fuse[Mathf.FloorToInt(elapsed * FuseFramesPerSecond) % art.Fuse.Length];
            }

            // Lands with a bounce.
            float drop = Overshoot(elapsed / DropSeconds);
            body.transform.localScale = Vector3.one * (bombScale * drop);

            // Pulses faster and redder as the fuse burns down.
            float burnt = Mathf.Clamp01(elapsed / fuseSeconds);
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * Mathf.Lerp(10f, 34f, burnt));
            body.color = Color.Lerp(Color.white, WarningTint, pulse * burnt);
            Color zoneColor = ZoneColor;
            zoneColor.a = Mathf.Lerp(0.12f, 0.38f, burnt) * (0.75f + 0.25f * pulse) * Mathf.Clamp01(drop);
            zone.color = zoneColor;
        }

        private void Explode()
        {
            hasExploded = true;
            body.enabled = false;
            zone.enabled = false;
            boom.enabled = boom.sprite != null;
            UpdateBoom(0f);
            BlastParticles.Play(transform.position, blastRadius, transform.parent);
            exploded?.Invoke(this);
        }

        // seconds since the explosion started.
        private void UpdateBoom(float seconds)
        {
            if (seconds >= GrowSeconds + HoldSeconds + FadeSeconds)
            {
                Destroy(gameObject);
                return;
            }

            // Bursts out fast to full size, then stays still while it fades.
            float grow = Mathf.Clamp01(seconds / GrowSeconds);
            float scale = Mathf.Lerp(0.2f, 1f, 1f - (1f - grow) * (1f - grow) * (1f - grow));
            float fade = Mathf.Clamp01((seconds - GrowSeconds - HoldSeconds) / FadeSeconds);
            boom.transform.localScale = Vector3.one * (boomScale * scale);
            Color color = boom.color;
            color.a = 1f - fade * fade;
            boom.color = color;
        }

        // 0 to 1 with a springy overshoot past 1 on the way.
        private static float Overshoot(float t)
        {
            t = Mathf.Clamp01(t);
            const float s = 2.2f;
            float u = t - 1f;
            return 1f + u * u * ((s + 1f) * u + s);
        }
    }
}
