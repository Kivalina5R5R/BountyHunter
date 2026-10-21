using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Fortal.Display
{
    // One bush on the floor: its transform, the radius of the leaves a player can stand inside and
    // how far the bush reaches from its centre at any turn.
    public readonly struct ScatteredBush
    {
        public ScatteredBush(Transform transform, float radius, float reach)
        {
            Transform = transform;
            Radius = radius;
            Reach = reach;
        }

        public Transform Transform { get; }

        // Half of the narrowest side of the visible leaves.
        public float Radius { get; }

        // Half of the longest side, which covers the bush at any tilt.
        public float Reach { get; }
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
        // No picture (bush, bin, crate, ...) is laid out more times than this in one layout; with too
        // few pictures for the count, the layout simply has fewer spots.
        [SerializeField, Min(1)] private int maxSamePicture = 2;
        // Width of the player marker in world units (Tag Circle 1.44 x player scale 0.462).
        [SerializeField, Min(0.01f)] private float playerWidth = 0.665f;
        // Bush size as a multiple of the player width, measured on the bush's NARROWEST side of
        // visible leaves, picked at random in this range. Every bush is at least the smaller value
        // in every direction, so it always shows a little past a player standing on it; the largest
        // is twice the smallest.
        [SerializeField] private Vector2 sizeVsPlayer = new Vector2(1.15f, 2.3f);
        // Extra gap between the edges of neighbouring bushes; never closer than this.
        [SerializeField, Min(0f)] private float edgeGap = 0.1f;
        // The gap each bush tries for between its edge and its neighbours'. Of a handful of random
        // spots it takes one this clear, or else the clearest one, so bushes spread over the floor.
        [SerializeField, Min(0f)] private float preferredGap = 1.3f;
        [SerializeField, Min(1)] private int spotsPerBush = 24;

        [Header("Area (world X/Z)")]
        // Bush centres; Vector2.y is world Z. Defaults keep whole bushes inside the 11 x 5.26 floor.
        [SerializeField] private Vector2 areaMin = new Vector2(-4.5f, -1.65f);
        [SerializeField] private Vector2 areaMax = new Vector2(4.5f, 1.65f);
        // The floor, centred on the arena. Every bush keeps its whole size plus Edge Margin inside it.
        [SerializeField] private Vector2 floorSize = new Vector2(11f, 5.26f);
        [SerializeField, Min(0f)] private float edgeMargin = 0.35f;

        [Header("Look")]
        // Each bush turns by a random angle up to this many degrees either way (180 = any angle).
        [SerializeField, Range(0f, 180f)] private float maxTiltDegrees = 180f;
        [SerializeField] private bool randomFlip = true;
        [SerializeField] private int sortingOrder = 5;
        // Drop shadow under each bush, in world X/Z (Vector2.y is world Z; negative is toward the
        // bottom of the floor). A clear colour means no shadow.
        [SerializeField] private Vector2 shadowOffset = new Vector2(0.07f, -0.09f);
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.35f);
        [SerializeField] private float height = 0.02f;
        [SerializeField, Min(0)] private int seed;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<ScatteredBush> bushes = new List<ScatteredBush>();

        public IReadOnlyList<ScatteredBush> Bushes => bushes;

        // Raised after every new layout, including Scatter Again.
        public event Action Scattered;

        // Raised just before the current bushes are removed for a new layout, while they still exist.
        public event Action Clearing;

        private void Start()
        {
            Scatter();
        }

        // Swaps the art used for the next layout (bushes on one map, bins on another). Upright art
        // such as bins wants no tilt; top-down bushes can turn any way. sizeRange is the same as
        // Size Vs Player. Call Scatter to apply.
        public void UseSprites(Sprite[] sprites, float tiltDegrees, Vector2 sizeRange)
        {
            bushSprites = sprites;
            maxTiltDegrees = Mathf.Clamp(tiltDegrees, 0f, 180f);
            sizeVsPlayer = sizeRange;
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
            var timesUsed = new Dictionary<Sprite, int>();
            int attempts = count * 300;
            while (placed.Count < count && attempts-- > 0)
            {
                Sprite sprite = PickSprite(timesUsed);
                if (sprite == null) break;

                Vector2 leaves = VisibleSize(sprite);
                float scale = Random.Range(MinimumSize, MaximumSize) / Mathf.Max(0.0001f, Mathf.Min(leaves.x, leaves.y));
                // Half of the longest side covers the bush at any tilt.
                float reach = Mathf.Max(leaves.x, leaves.y) * scale * 0.5f;
                Vector2 position = PickSpot(reach, placed, out float clearance);
                if (clearance < edgeGap) continue;

                placed.Add((position, reach));
                timesUsed[sprite] = timesUsed.TryGetValue(sprite, out int used) ? used + 1 : 1;
                Transform bush = SpawnBush(sprite, position, scale, placed.Count);
                bushes.Add(new ScatteredBush(bush, Mathf.Min(leaves.x, leaves.y) * scale * 0.5f, reach));
            }

            if (seed > 0) Random.state = previousState;
            Scattered?.Invoke();
        }

        // Where the centre of a bush reaching this far may go: inside the bush area, and far enough in
        // that the whole bush stays Edge Margin away from the floor's edges. Moving bushes use it too.
        public void CentreLimits(float reach, out Vector2 min, out Vector2 max)
        {
            Vector2 inside = floorSize * 0.5f - Vector2.one * (reach + edgeMargin);
            min = Vector2.Max(areaMin, -inside);
            max = Vector2.Min(areaMax, inside);
            // A bush too big for the room left sits on the middle line instead.
            if (min.x > max.x) min.x = max.x = (min.x + max.x) * 0.5f;
            if (min.y > max.y) min.y = max.y = (min.y + max.y) * 0.5f;
        }

        // A random picture laid out fewer than maxSamePicture times so far; null when every one is used up.
        private Sprite PickSprite(Dictionary<Sprite, int> timesUsed)
        {
            var allowed = new List<Sprite>();
            foreach (Sprite sprite in bushSprites)
            {
                if (sprite == null) continue;
                if (timesUsed.TryGetValue(sprite, out int used) && used >= maxSamePicture) continue;
                allowed.Add(sprite);
            }

            return allowed.Count > 0 ? allowed[Random.Range(0, allowed.Count)] : null;
        }

        // Never lets a bush shrink below the player's size, whatever the Inspector says.
        private float MinimumSize => playerWidth * Mathf.Max(1.05f, sizeVsPlayer.x);
        private float MaximumSize => Mathf.Max(MinimumSize, playerWidth * sizeVsPlayer.y);

        // Tries random spots for a bush reaching this far: the first with at least Preferred Gap
        // around it wins, otherwise the clearest. clearance is the gap left to the nearest bush edge.
        private Vector2 PickSpot(float reach, List<(Vector2 position, float reach)> placed, out float clearance)
        {
            CentreLimits(reach, out Vector2 low, out Vector2 high);
            Vector2 best = Vector2.zero;
            clearance = float.MinValue;
            for (int i = 0; i < spotsPerBush; i++)
            {
                var spot = new Vector2(Random.Range(low.x, high.x), Random.Range(low.y, high.y));
                float gap = Clearance(spot, reach, placed);
                if (gap <= clearance) continue;

                best = spot;
                clearance = gap;
                if (clearance >= preferredGap) break;
            }

            return best;
        }

        // Gap between this bush's edge and the nearest placed bush's edge; negative when they overlap.
        private static float Clearance(Vector2 position, float reach, List<(Vector2 position, float reach)> placed)
        {
            float clearance = float.MaxValue;
            for (int i = 0; i < placed.Count; i++)
            {
                clearance = Mathf.Min(clearance, Vector2.Distance(placed[i].position, position) - placed[i].reach - reach);
            }

            return clearance;
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
            if (shadowColor.a > 0f) BushShadow.Add(spriteRenderer, shadowOffset, shadowColor);
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
