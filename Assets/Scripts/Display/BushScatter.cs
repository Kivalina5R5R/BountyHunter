using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Fortal.Display
{
    // One bush on the floor: its transform and the radius of the leaves a player can stand inside.
    public readonly struct ScatteredBush
    {
        public ScatteredBush(Transform transform, float radius)
        {
            Transform = transform;
            Radius = radius;
        }

        public Transform Transform { get; }

        // Half of the narrowest side of the visible leaves.
        public float Radius { get; }
    }

    // Scatters bush sprites flat on the floor at random spots when Play starts. Bushes keep a
    // minimum distance from each other and stay inside the area. Use a seed above zero for the
    // same layout every run, or 0 for a new layout each time. Runs before default scripts so other
    // systems can use the bushes from their own Start.
    [DefaultExecutionOrder(-50)]
    public sealed class BushScatter : MonoBehaviour
    {
        [SerializeField] private Sprite[] bushSprites;

        [Header("Amount and Size")]
        // Each Play picks a bush count in this range (both ends included).
        [SerializeField] private Vector2Int countRange = new Vector2Int(5, 6);
        // Width of the player marker in world units (Tag Circle 1.44 x player scale 0.462).
        [SerializeField, Min(0.01f)] private float playerWidth = 0.665f;
        // Bush size as a multiple of the player width, measured on the bush's NARROWEST side of
        // visible leaves, picked at random in this range. Every bush is at least the smaller value
        // in every direction, so it always shows a little past a player standing on it; the largest
        // is twice the smallest.
        [SerializeField] private Vector2 sizeVsPlayer = new Vector2(1.15f, 2.3f);
        // Extra gap between the edges of neighbouring bushes.
        [SerializeField, Min(0f)] private float edgeGap = 0.1f;

        [Header("Area (world X/Z)")]
        // Bush centres; Vector2.y is world Z. Defaults keep whole bushes inside the 11 x 5.26 floor.
        [SerializeField] private Vector2 areaMin = new Vector2(-4.5f, -1.65f);
        [SerializeField] private Vector2 areaMax = new Vector2(4.5f, 1.65f);

        [Header("Look")]
        // Each bush turns by a random angle up to this many degrees either way (180 = any angle).
        [SerializeField, Range(0f, 180f)] private float maxTiltDegrees = 180f;
        [SerializeField] private bool randomFlip = true;
        [SerializeField] private int sortingOrder = 5;
        [SerializeField] private float height = 0.02f;
        [SerializeField, Min(0)] private int seed;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<ScatteredBush> bushes = new List<ScatteredBush>();

        public IReadOnlyList<ScatteredBush> Bushes => bushes;

        // The rectangle bush centres stay in (Vector2.y is world Z); moving bushes should keep to it too.
        public Vector2 AreaMin => areaMin;
        public Vector2 AreaMax => areaMax;

        // Raised after every new layout, including Scatter Again.
        public event Action Scattered;

        // Raised just before the current bushes are removed for a new layout, while they still exist.
        public event Action Clearing;

        private void Start()
        {
            Scatter();
        }

        // Replaces the current bushes with a new random layout.
        [ContextMenu("Scatter Again (Play Mode)")]
        public void Scatter()
        {
            Clear();
            if (bushSprites == null || bushSprites.Length == 0) return;

            Random.State previousState = Random.state;
            if (seed > 0) Random.InitState(seed);

            int count = Random.Range(Mathf.Max(0, countRange.x), Mathf.Max(countRange.x, countRange.y) + 1);
            var placed = new List<(Vector2 position, float reach)>();
            int attempts = count * 300;
            while (placed.Count < count && attempts-- > 0)
            {
                Sprite sprite = bushSprites[Random.Range(0, bushSprites.Length)];
                if (sprite == null) continue;

                Vector2 leaves = VisibleSize(sprite);
                float scale = Random.Range(MinimumSize, MaximumSize) / Mathf.Max(0.0001f, Mathf.Min(leaves.x, leaves.y));
                // Half of the longest side covers the bush at any tilt.
                float reach = Mathf.Max(leaves.x, leaves.y) * scale * 0.5f;
                var position = new Vector2(Random.Range(areaMin.x, areaMax.x), Random.Range(areaMin.y, areaMax.y));
                if (TooClose(position, reach, placed)) continue;

                placed.Add((position, reach));
                Transform bush = SpawnBush(sprite, position, scale, placed.Count);
                bushes.Add(new ScatteredBush(bush, Mathf.Min(leaves.x, leaves.y) * scale * 0.5f));
            }

            if (seed > 0) Random.state = previousState;
            Scattered?.Invoke();
        }

        // Never lets a bush shrink below the player's size, whatever the Inspector says.
        private float MinimumSize => playerWidth * Mathf.Max(1.05f, sizeVsPlayer.x);
        private float MaximumSize => Mathf.Max(MinimumSize, playerWidth * sizeVsPlayer.y);

        // Keeps centres at least the two bushes' reach apart, so neighbours never overlap.
        private bool TooClose(Vector2 position, float reach, List<(Vector2 position, float reach)> placed)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                float spacing = placed[i].reach + reach + edgeGap;
                if ((placed[i].position - position).sqrMagnitude < spacing * spacing) return true;
            }

            return false;
        }

        private Transform SpawnBush(Sprite sprite, Vector2 position, float scale, int number)
        {
            var bush = new GameObject($"Bush{number:00}", typeof(SpriteRenderer));
            bush.transform.SetParent(transform, false);
            bush.transform.position = new Vector3(position.x, height, position.y);
            float spin = Random.Range(-maxTiltDegrees, maxTiltDegrees);
            bush.transform.rotation = Quaternion.Euler(90f, spin, 0f);
            bush.transform.localScale = Vector3.one * scale;

            var spriteRenderer = bush.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.flipX = randomFlip && Random.value < 0.5f;
            spawned.Add(bush);
            return bush.transform;
        }

        // Width and height of the drawn leaves from the sprite's tight outline, ignoring transparent
        // padding. The images differ (1254 x 1254 squares and a 1920 x 1080 canvas), so each is measured.
        private static Vector2 VisibleSize(Sprite sprite)
        {
            Vector2[] vertices = sprite.vertices;
            if (vertices == null || vertices.Length == 0) return sprite.bounds.size;

            Vector2 min = vertices[0];
            Vector2 max = vertices[0];
            foreach (Vector2 vertex in vertices)
            {
                min = Vector2.Min(min, vertex);
                max = Vector2.Max(max, vertex);
            }

            return max - min;
        }

        private void Clear()
        {
            if (spawned.Count > 0) Clearing?.Invoke();
            foreach (GameObject bush in spawned)
            {
                if (bush != null) Destroy(bush);
            }

            spawned.Clear();
            bushes.Clear();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.3f, 1f);
            var center = new Vector3((areaMin.x + areaMax.x) * 0.5f, height, (areaMin.y + areaMax.y) * 0.5f);
            Gizmos.DrawWireCube(center, new Vector3(areaMax.x - areaMin.x, 0f, areaMax.y - areaMin.y));
        }
#endif
    }
}
