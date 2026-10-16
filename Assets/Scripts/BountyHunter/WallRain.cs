using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Rain falling across the intro wall: a few hundred streaks at a slight slant, each at its own
    // depth, so near ones are long, wide, quick and bright and far ones short, thin, slow and faint.
    // All the streaks are one mesh, rebuilt each frame, so the whole shower is a single draw.
    // Stretch it over the wall; sizes and speeds are in parts of its height.
    public sealed class WallRain : MaskableGraphic
    {
        [SerializeField] private Texture streak;
        [SerializeField, Range(10, 900)] private int count = 480;
        // Fall speed in wall heights per second, far to near.
        [SerializeField] private Vector2 speed = new Vector2(1.1f, 2.4f);
        // Streak length and width in wall heights, far to near.
        [SerializeField] private Vector2 length = new Vector2(0.045f, 0.13f);
        [SerializeField] private Vector2 width = new Vector2(0.0018f, 0.0045f);
        [SerializeField] private Vector2 brightness = new Vector2(0.3f, 0.7f);
        // Lean of the rain, in degrees from straight down (positive drifts right).
        [SerializeField, Range(-30f, 30f)] private float slant = 8f;

        private struct Drop
        {
            public Vector2 Position;
            public float Depth;
        }

        private Drop[] drops;

        public override Texture mainTexture => streak != null ? streak : s_WhiteTexture;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Scatter();
        }

        // Spreads the drops over the whole wall, so it is already raining on the first frame.
        private void Scatter()
        {
            drops = new Drop[count];
            for (int i = 0; i < drops.Length; i++)
            {
                drops[i] = new Drop { Position = new Vector2(Random.value, Random.value), Depth = Random.value };
            }
        }

        private void Update()
        {
            if (drops == null || !Application.isPlaying) return;

            Rect rect = rectTransform.rect;
            float aspect = rect.height / Mathf.Max(1f, rect.width);
            Vector2 down = Direction();
            float dt = Time.deltaTime;
            for (int i = 0; i < drops.Length; i++)
            {
                Drop drop = drops[i];
                float fall = Mathf.Lerp(speed.x, speed.y, drop.Depth) * dt;
                drop.Position += new Vector2(down.x * fall * aspect, down.y * fall);
                if (drop.Position.y < -0.15f)
                {
                    // Back to the top at a new spot and depth.
                    drop.Position = new Vector2(Random.value, 1.05f + Random.value * 0.1f);
                    drop.Depth = Random.value;
                }

                drop.Position.x = Mathf.Repeat(drop.Position.x, 1f);
                drops[i] = drop;
            }

            SetVerticesDirty();
        }

        // Straight down, leaning by slant.
        private Vector2 Direction()
        {
            float angle = slant * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (drops == null) return;

            Rect rect = rectTransform.rect;
            float height = rect.height;
            Vector2 down = Direction();
            var side = new Vector2(-down.y, down.x);
            foreach (Drop drop in drops)
            {
                float d = drop.Depth;
                Vector2 tip = new Vector2(rect.xMin + drop.Position.x * rect.width, rect.yMin + drop.Position.y * height);
                Vector2 along = -down * (Mathf.Lerp(length.x, length.y, d) * height);
                Vector2 across = side * (Mathf.Lerp(width.x, width.y, d) * height * 0.5f);
                Color32 tint = color;
                tint.a = (byte)(color.a * Mathf.Lerp(brightness.x, brightness.y, d) * 255f);

                int start = mesh.currentVertCount;
                // The texture's bright end (v = 0 at the bottom) is the falling tip.
                mesh.AddVert(tip - across, tint, new Vector2(0f, 0f));
                mesh.AddVert(tip + across, tint, new Vector2(1f, 0f));
                mesh.AddVert(tip + along + across, tint, new Vector2(1f, 1f));
                mesh.AddVert(tip + along - across, tint, new Vector2(0f, 1f));
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
