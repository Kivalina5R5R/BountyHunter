using UnityEngine;

namespace BountyHunter.Thieves
{
    // Rose petals drifting over the whole floor as the boss makes its entrance, in three layers:
    // small slow ones far down under the characters, middling ones over them and big quicker ones
    // near the camera over everything, so the shower has depth. Petals keep coming for most of the
    // time, half of them floating in over the top and right edges and half fading in anywhere on the
    // floor, and all drift slowly down to the bottom left on a slant, tumbling as they go (BossPetal).
    // They all fade out together so they are gone at the end (allGoneSeconds), as the boss appears;
    // it removes itself then (IsOver).
    public sealed class BossPetalShower : MonoBehaviour
    {
        private sealed class Layer
        {
            public readonly float Share;
            public readonly Vector2 Size;
            public readonly Vector2 Speed;
            public readonly int SortingOrder;
            public readonly float Shade;
            // How much it sways and turns, against a middle-layer petal.
            public readonly float Motion;

            public Layer(float share, Vector2 size, Vector2 speed, int sortingOrder, float shade, float motion)
            {
                Share = share;
                Size = size;
                Speed = speed;
                SortingOrder = sortingOrder;
                Shade = shade;
                Motion = motion;
            }
        }

        // Far (under the characters at 8, a little dim), middle (over them, under the rose at 31) and
        // near (over everything). Speeds are a gentle drift, as petals float.
        private static readonly Layer[] Layers =
        {
            new Layer(0.4f, new Vector2(0.1f, 0.15f), new Vector2(0.4f, 0.6f), 7, 0.72f, 0.7f),
            new Layer(0.37f, new Vector2(0.19f, 0.27f), new Vector2(0.6f, 0.9f), 30, 0.9f, 1f),
            new Layer(0.23f, new Vector2(0.38f, 0.54f), new Vector2(0.9f, 1.3f), 32, 1f, 1.2f),
        };

        // Way they drift (x and world z: left and towards the players' side, away from the wall).
        private static readonly Vector3 Wind = new Vector3(-0.8f, 0f, -1f).normalized;
        private const float PetalsPerSecond = 18f;
        // New petals stop coming this long before the end, and all fade out over this long to it.
        private const float LastSentBeforeEnd = 1f;
        private const float FadeOutSeconds = 0.6f;
        // Share of petals that float in over an edge (the rest fade in on the floor), and how far out
        // over the top and right edges they start.
        private const float FromEdgeShare = 0.5f;
        private const float Offside = 0.4f;

        private Sprite petal;
        private Rect floor;
        private float allGoneSeconds;
        private float fadeOutAt;
        private float elapsed;
        private float owed;

        // Its time is up and every petal has gone.
        public bool IsOver => elapsed >= allGoneSeconds && transform.childCount == 0;

        // floor is the floor seen (x and world z); every petal has faded out allGoneSeconds from now.
        public static BossPetalShower Begin(Sprite petal, Rect floor, float allGoneSeconds, Transform parent)
        {
            var shower = new GameObject("Rose Petal Shower").AddComponent<BossPetalShower>();
            shower.transform.SetParent(parent, false);
            shower.petal = petal;
            shower.floor = floor;
            shower.allGoneSeconds = Mathf.Max(0.5f, allGoneSeconds);
            shower.fadeOutAt = Time.time + shower.allGoneSeconds - FadeOutSeconds;
            return shower;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed <= allGoneSeconds - LastSentBeforeEnd)
            {
                owed += PetalsPerSecond * Time.deltaTime;
                while (owed >= 1f)
                {
                    owed -= 1f;
                    BlowOne();
                }

                return;
            }

            if (IsOver) Destroy(gameObject);
        }

        private void BlowOne()
        {
            Layer layer = PickLayer();
            Vector3 way = Quaternion.AngleAxis(Random.Range(-12f, 12f), Vector3.up) * Wind;
            float speed = Random.Range(layer.Speed.x, layer.Speed.y);
            Vector3 start = Random.value < FromEdgeShare ? EdgeStart() : new Vector3(Random.Range(floor.xMin, floor.xMax), 0f, Random.Range(floor.yMin, floor.yMax));
            float size = Random.Range(layer.Size.x, layer.Size.y);
            BossPetal.Blow(petal, start, way * speed, size, layer.SortingOrder, layer.Shade, layer.Motion, fadeOutAt, floor, transform);
        }

        // Just over the top or the right edge, in proportion to their lengths.
        private Vector3 EdgeStart()
        {
            float top = floor.width;
            float right = floor.height;
            return Random.value * (top + right) < top
                ? new Vector3(Random.Range(floor.xMin, floor.xMax), 0f, floor.yMax + Offside)
                : new Vector3(floor.xMax + Offside, 0f, Random.Range(floor.yMin, floor.yMax));
        }

        private static Layer PickLayer()
        {
            float pick = Random.value;
            foreach (Layer layer in Layers)
            {
                if (pick < layer.Share) return layer;
                pick -= layer.Share;
            }

            return Layers[Layers.Length - 1];
        }
    }
}
