using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // A short spray of leaves thrown out of a bush on one side (where a thief jumps in or runs out).
    // Seen from above, each leaf flies up toward the camera (it grows), falls back (it shrinks),
    // lands with a small bounce just past the bush and fades on the ground. Every leaf gets its own
    // random start, angle, distance, size, spin, drift and delay, so no two bursts look alike.
    public sealed class LeafBurst : MonoBehaviour
    {
        private const float Height = 0.06f;
        // Above bushes (5) and thieves (8), below player markers (20).
        private const int SortingOrder = 9;
        // Half-width of the spray around the side direction.
        private const float SpreadDegrees = 55f;

        private readonly List<Leaf> leaves = new List<Leaf>();
        private float elapsed;

        private sealed class Leaf
        {
            public SpriteRenderer Renderer;
            public Vector3 Start;
            public Vector3 End;
            public Vector3 Drift;
            public float Delay;
            public float Duration;
            public float StartAngle;
            public float Spin;
            public float Size;
            public float AirShare;
            public float Lift;
        }

        // Throws count leaves from a bush of the given leaf radius toward side (world X/Z).
        // A zero side throws them all around. parent only files the burst in the hierarchy
        // (the bush's folder); world placement is unchanged.
        public static void Play(Vector3 center, float radius, Sprite[] sprites, int count, Vector3 side, Transform parent)
        {
            if (sprites == null || sprites.Length == 0 || count <= 0) return;

            var burst = new GameObject("Leaf Burst").AddComponent<LeafBurst>();
            burst.transform.SetParent(parent, true);
            burst.transform.position = new Vector3(center.x, Height, center.z);
            side.y = 0f;
            bool aimed = side.sqrMagnitude > 0.0001f;
            float sideAngle = aimed ? Mathf.Atan2(side.z, side.x) * Mathf.Rad2Deg : 0f;
            for (int i = 0; i < count; i++)
            {
                Sprite sprite = sprites[Random.Range(0, sprites.Length)];
                if (sprite == null) continue;

                float angle = aimed ? sideAngle + RandomSpread() : Random.Range(0f, 360f);
                burst.AddLeaf(sprite, radius, angle);
            }
        }

        // Leaves bunch toward the middle of the spray and thin out at its edges.
        private static float RandomSpread()
        {
            return (Random.value + Random.value - 1f) * SpreadDegrees;
        }

        private void AddLeaf(Sprite sprite, float radius, float angleDegrees)
        {
            float angle = angleDegrees * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var sideways = new Vector3(-direction.z, 0f, direction.x);

            var leafObject = new GameObject("Leaf", typeof(SpriteRenderer));
            leafObject.transform.SetParent(transform, false);
            var leafRenderer = leafObject.GetComponent<SpriteRenderer>();
            leafRenderer.sprite = sprite;
            leafRenderer.sortingOrder = SortingOrder;
            leafRenderer.flipX = Random.value < 0.5f;

            var leaf = new Leaf
            {
                Renderer = leafRenderer,
                // Leaves come from inside the leaf mass, not exactly on its edge.
                Start = direction * radius * Random.Range(0.35f, 0.85f),
                // Lands just past the leaves of the bush, not far across the floor.
                End = direction * (radius + Random.Range(0.03f, 0.28f)),
                // A gentle sideways curve, as if caught by air.
                Drift = sideways * Random.Range(-0.1f, 0.1f),
                Delay = Random.Range(0f, 0.12f),
                Duration = Random.Range(0.9f, 1.4f),
                // Part of the life spent in the air; the rest lies on the ground and fades.
                AirShare = Random.Range(0.4f, 0.55f),
                // How much bigger it looks at the top of its flight.
                Lift = Random.Range(0.5f, 0.9f),
                StartAngle = Random.Range(0f, 360f),
                Spin = Random.Range(-600f, 600f),
                Size = Random.Range(0.1f, 0.22f) / Mathf.Max(0.0001f, sprite.bounds.size.x)
            };
            leaves.Add(leaf);
            Apply(leaf, 0f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            bool anyAlive = false;
            foreach (Leaf leaf in leaves)
            {
                float t = Mathf.Clamp01((elapsed - leaf.Delay) / leaf.Duration);
                Apply(leaf, t);
                if (t < 1f) anyAlive = true;
            }

            if (!anyAlive) Destroy(gameObject);
        }

        private static void Apply(Leaf leaf, float t)
        {
            Transform leafTransform = leaf.Renderer.transform;
            Color color = leaf.Renderer.color;

            if (t < leaf.AirShare)
            {
                // In the air: moves out with a slight curve; height rises then falls, shown as size.
                float air = t / leaf.AirShare;
                float travel = 1f - (1f - air) * (1f - air);
                float height = Mathf.Sin(air * Mathf.PI);
                leafTransform.localPosition = Vector3.Lerp(leaf.Start, leaf.End, travel) + leaf.Drift * height;
                leafTransform.localRotation = Quaternion.Euler(90f, leaf.StartAngle + leaf.Spin * travel, 0f);
                // Grows out of the bush instead of popping into view.
                float appear = Mathf.Clamp01(air / 0.2f);
                leafTransform.localScale = Vector3.one * (leaf.Size * (1f + leaf.Lift * height) * appear);
                color.a = 1f;
            }
            else
            {
                // On the ground: a small settle bounce, then it fades where it landed.
                float ground = (t - leaf.AirShare) / (1f - leaf.AirShare);
                float bounce = Mathf.Sin(Mathf.Clamp01(ground / 0.25f) * Mathf.PI) * 0.12f;
                leafTransform.localPosition = leaf.End;
                leafTransform.localRotation = Quaternion.Euler(90f, leaf.StartAngle + leaf.Spin, 0f);
                leafTransform.localScale = Vector3.one * (leaf.Size * (1f + bounce));
                color.a = ground < 0.4f ? 1f : 1f - (ground - 0.4f) / 0.6f;
            }

            leaf.Renderer.color = color;
        }
    }
}
