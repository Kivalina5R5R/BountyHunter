using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Pigeon feathers drifting over the whole floor as the boss makes its entrance, in three layers:
    // small slow ones far down under the characters, middling ones over them and big quicker ones
    // near the camera over everything, so the shower has depth. Feathers keep coming for most of the
    // time, half of them floating in over the top and right edges and half fading in anywhere on the
    // floor, and all drift slowly down to the bottom left on a slant, tumbling as they go (BossBlownFeather).
    // They all fade out together so they are gone at the end (allGoneSeconds), as the boss appears;
    // it removes itself then (IsOver).
    public sealed class BossFeatherShower : MonoBehaviour
    {
        private sealed class Layer
        {
            public readonly float Share;
            public readonly Vector2 Size;
            public readonly Vector2 Speed;
            public readonly int SortingOrder;
            public readonly float Shade;
            // How much it sways and turns, against a middle-layer feather.
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

        // Far (under the characters at 8, a little dim), middle (over them) and near (over
        // everything). Speeds are a gentle drift, as feathers float.
        private static readonly Layer[] Layers =
        {
            new Layer(0.4f, new Vector2(0.1f, 0.15f), new Vector2(0.4f, 0.6f), 7, 0.72f, 0.7f),
            new Layer(0.37f, new Vector2(0.19f, 0.27f), new Vector2(0.6f, 0.9f), 30, 0.9f, 1f),
            new Layer(0.23f, new Vector2(0.38f, 0.54f), new Vector2(0.9f, 1.3f), 32, 1f, 1.2f),
        };

        // Way they drift (x and world z: left and towards the players' side, away from the wall).
        private static readonly Vector3 Wind = new Vector3(-0.8f, 0f, -1f).normalized;
        private const float FeathersPerSecond = 10f;
        // New feathers stop coming this long before the end, and all fade out over this long to it.
        private const float LastSentBeforeEnd = 1f;
        private const float FadeOutSeconds = 0.6f;
        // Share of feathers that float in over an edge (the rest fade in on the floor), and how far out
        // over the top and right edges they start.
        private const float FromEdgeShare = 0.5f;
        private const float Offside = 0.4f;

        private Sprite feather;
        private Camera floorCamera;
        private Rect floor;
        private float allGoneSeconds;
        private float fadeOutAt;
        private float elapsed;
        private float owed;
        private float wallScale;

        // Reuses the floor feather motion in the wall canvas's own plane and clips at its edges.
        public static BossFeatherShower BeginOnWall(Sprite feather, RectTransform wall, float allGoneSeconds)
        {
            var root = new GameObject("Wall Feathers", typeof(RectTransform), typeof(RectMask2D));
            var rect = (RectTransform)root.transform;
            rect.SetParent(wall, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var shower = root.AddComponent<BossFeatherShower>();
            shower.wallScale = Mathf.Max(1f, wall.rect.height) / 5f;
            float width = wall.rect.width / shower.wallScale;
            shower.floor = new Rect(-width * 0.5f, -2.5f, width, 5f);
            shower.feather = feather;
            shower.allGoneSeconds = Mathf.Max(0.5f, allGoneSeconds);
            shower.fadeOutAt = Time.time + shower.allGoneSeconds - FadeOutSeconds;
            return shower;
        }

        // Its time is up and every feather has gone.
        public bool IsOver => elapsed >= allGoneSeconds && transform.childCount == 0;

        // floor is the floor seen (x and world z); every feather has faded out allGoneSeconds from now.
        public static BossFeatherShower Begin(Sprite feather, Rect floor, float allGoneSeconds, Transform parent, Camera floorCamera)
        {
            var shower = new GameObject("Feather Shower").AddComponent<BossFeatherShower>();
            shower.transform.SetParent(parent, false);
            shower.feather = feather;
            shower.floorCamera = floorCamera;
            shower.floor = floor;
            shower.allGoneSeconds = Mathf.Max(0.5f, allGoneSeconds);
            shower.fadeOutAt = Time.time + shower.allGoneSeconds - FadeOutSeconds;
            return shower;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        }

        private void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            foreach (Transform child in transform)
            {
                if (child.TryGetComponent(out SpriteRenderer picture)) picture.forceRenderingOff = camera != floorCamera;
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed <= allGoneSeconds - LastSentBeforeEnd)
            {
                owed += FeathersPerSecond * Time.deltaTime;
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
            BossBlownFeather.Blow(feather, start, way * speed, size, layer.SortingOrder, layer.Shade, layer.Motion, fadeOutAt, floor, transform, wallScale);
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
