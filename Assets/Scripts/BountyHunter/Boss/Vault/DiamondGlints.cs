using UnityEngine;

namespace BountyHunter.Thieves
{
    // Glints on the vault's diamond, so the prize catches the eye: a few sparkles take turns popping
    // up somewhere on the gem with a little turn, then shrinking back to nothing, each at its own
    // pace. Sits on the diamond's picture (lying flat on the floor); the gem is the upper part of it.
    public sealed class DiamondGlints : MonoBehaviour
    {
        private const int Count = 3;
        private const float GlintSeconds = 0.6f;
        // The pause after a glint before that sparkle pops up again somewhere else.
        private static readonly Vector2 GapSeconds = new Vector2(0.4f, 1.4f);
        // The gem's middle and how far it reaches either way, as shares of the picture (x across, y
        // up), and a glint's width against the picture's.
        private static readonly Vector2 GemCentre = new Vector2(-0.03f, 0.17f);
        private static readonly Vector2 GemReach = new Vector2(0.28f, 0.24f);
        private const float GlintVsPicture = 0.16f;
        private const float TurnDegrees = 45f;
        // Over the diamond (6), under the thieves (8).
        private const int Order = 7;

        private readonly Transform[] glints = new Transform[Count];
        private readonly float[] startsAt = new float[Count];
        private Vector2 pictureSize;
        private float glintScale;

        // Puts glints drawn with glint on diamond; nothing without a glint picture.
        public static void Add(SpriteRenderer diamond, Sprite glint)
        {
            if (diamond == null || diamond.sprite == null || glint == null) return;

            var glints = diamond.gameObject.AddComponent<DiamondGlints>();
            glints.pictureSize = diamond.sprite.bounds.size;
            glints.glintScale = glints.pictureSize.x * GlintVsPicture / Mathf.Max(0.01f, glint.bounds.size.x);
            for (int i = 0; i < Count; i++)
            {
                var piece = new GameObject($"Glint {i + 1}", typeof(SpriteRenderer));
                piece.transform.SetParent(diamond.transform, false);
                var picture = piece.GetComponent<SpriteRenderer>();
                picture.sprite = glint;
                picture.sortingOrder = Order;
                glints.glints[i] = piece.transform;
                // Spread out to start with, so they do not all pop at once.
                glints.startsAt[i] = Time.time + i * (GlintSeconds + GapSeconds.x);
                glints.Place(i);
            }
        }

        private void Update()
        {
            for (int i = 0; i < Count; i++)
            {
                float t = (Time.time - startsAt[i]) / GlintSeconds;
                if (t >= 1f)
                {
                    startsAt[i] = Time.time + Random.Range(GapSeconds.x, GapSeconds.y);
                    Place(i);
                    t = 0f;
                }

                float grown = t > 0f ? Mathf.Sin(t * Mathf.PI) : 0f;
                glints[i].localScale = Vector3.one * (glintScale * grown);
                glints[i].localRotation = Quaternion.Euler(0f, 0f, TurnDegrees * t);
            }
        }

        // Somewhere on the gem, at random.
        private void Place(int index)
        {
            Vector2 spot = Random.insideUnitCircle;
            float x = (GemCentre.x + spot.x * GemReach.x) * pictureSize.x;
            float y = (GemCentre.y + spot.y * GemReach.y) * pictureSize.y;
            glints[index].localPosition = new Vector3(x, y, 0f);
            glints[index].localScale = Vector3.zero;
        }
    }
}
