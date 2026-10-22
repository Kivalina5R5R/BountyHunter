using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // A cheer round a player who got the lineup right: paper confetti in the game's colours shoots up
    // in two bursts and drifts back down onto the floor, fluttering and swaying as it falls. The floor
    // is seen from above, so height shows as size: a piece is biggest at the top of its flight and
    // back to its own size when it lands, where it lies a moment and fades. Drawn here, so it needs no
    // art. Lies flat on the floor like the other effects and removes itself when done.
    public sealed class LineupCheer : MonoBehaviour
    {
        // How long it lasts, from the first burst until the last piece has faded.
        public const float Seconds = 2.8f;

        private const float Height = 0.08f;
        private const int Pieces = 40;
        private const float SecondBurstAt = 0.4f;
        // A piece's flight: how fast it shoots up and how slowly it falls (paper drifts down slowly).
        private const float LaunchSpeed = 2.6f;
        private const float Gravity = 4.5f;
        private const float FallSpeed = 0.9f;
        // How much bigger a piece looks per metre of height.
        private const float SizePerMetre = 0.9f;
        // Over the player marker (20), under the money text (22).
        private const int Order = 21;

        private static readonly Color[] Colours =
        {
            new Color(1f, 0.82f, 0.25f, 1f),
            new Color(0.9f, 0.25f, 0.2f, 1f),
            new Color(0.2f, 0.7f, 0.75f, 1f),
            new Color(0.98f, 0.95f, 0.88f, 1f),
            new Color(0.95f, 0.5f, 0.7f, 1f),
        };

        private static Sprite paper;

        private readonly List<Piece> pieces = new List<Piece>();
        private float elapsed;

        private sealed class Piece
        {
            public SpriteRenderer Picture;
            public Vector3 Size;
            // Along the floor: where it heads and how far it spreads, and how it sways while falling.
            public Vector2 Direction;
            public float Spread;
            public float Sway;
            public float Spin;
            public float Delay;
        }

        public static void Play(Vector3 feet, Transform parent)
        {
            var cheer = new GameObject("Lineup Cheer").AddComponent<LineupCheer>();
            cheer.transform.SetParent(parent, true);
            cheer.transform.SetPositionAndRotation(new Vector3(feet.x, Height, feet.z), Quaternion.Euler(90f, 0f, 0f));
            cheer.Build();
        }

        private void Build()
        {
            Vector2 size = Paper.bounds.size;
            for (int i = 0; i < Pieces; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                var picture = new GameObject("Paper", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                picture.transform.SetParent(transform, false);
                picture.sprite = Paper;
                picture.color = Colours[i % Colours.Length];
                picture.sortingOrder = Order;
                pieces.Add(new Piece
                {
                    Picture = picture,
                    Size = new Vector3(0.12f / size.x, 0.065f / size.y, 1f),
                    Direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                    Spread = Random.Range(0.5f, 1.5f),
                    Sway = Random.Range(0.06f, 0.14f),
                    Spin = Random.Range(-300f, 300f),
                    Delay = i % 2 == 0 ? 0f : SecondBurstAt,
                });
            }

            Apply();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= Seconds)
            {
                Destroy(gameObject);
                return;
            }

            Apply();
        }

        // Its own up (y) points toward the wall: the effect is laid out in its local x and y.
        private void Apply()
        {
            float rise = LaunchSpeed / Gravity;
            float top = LaunchSpeed * rise * 0.5f;
            foreach (Piece piece in pieces)
            {
                float time = elapsed - piece.Delay;
                if (time < 0f)
                {
                    SetAlpha(piece.Picture, 0f);
                    continue;
                }

                // Up fast and slowing, then down at paper's slow, steady drift until it lands.
                float height = time < rise
                    ? LaunchSpeed * time - Gravity * time * time * 0.5f
                    : Mathf.Max(0f, top - FallSpeed * (time - rise));
                bool landed = time > rise && height <= 0f;

                // Out along the floor quickly at first, then drifting, swaying side to side as it falls.
                float travel = piece.Spread * (1f - Mathf.Exp(-time * 2.5f));
                Vector2 side = new Vector2(-piece.Direction.y, piece.Direction.x);
                float sway = landed ? 0f : piece.Sway * Mathf.Sin(time * 5f + piece.Spin);
                Transform holder = piece.Picture.transform;
                holder.localPosition = piece.Direction * travel + side * sway;

                // Flutters (turns and flips) in the air and lies still once down.
                float flutter = landed ? 1f : 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(time * 7f + piece.Spin));
                float lift = 1f + SizePerMetre * height;
                holder.localScale = new Vector3(piece.Size.x * lift, piece.Size.y * lift * flutter, 1f);
                if (!landed) holder.localRotation = Quaternion.Euler(0f, 0f, piece.Spin * time);

                float left = 1f - time / (Seconds - piece.Delay);
                SetAlpha(piece.Picture, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(left * 4f)));
            }
        }

        private static void SetAlpha(SpriteRenderer picture, float alpha)
        {
            Color colour = picture.color;
            colour.a = alpha;
            picture.color = colour;
        }

        // A plain white square, tinted per piece: crisp-edged paper.
        private static Sprite Paper => paper != null ? paper : paper = DrawPaper();

        private static Sprite DrawPaper()
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var white = new Color[16];
            for (int i = 0; i < white.Length; i++) white[i] = Color.white;
            texture.SetPixels(white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        }
    }
}
