using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // Tiny specks on the wall's canvas, kept subtle so they suit the cork board: cork crumbs falling
    // where a pin is pushed in, ink spatter where the CAUGHT stamp lands, and gold sparkles around
    // the leader's star. Lives on the canvas, in its units, and removes itself when done.
    public sealed class PosterSpecks : MonoBehaviour
    {
        private sealed class Speck
        {
            public Image Image;
            public Vector2 Velocity;
            public float Duration;
        }

        private static readonly Color[] CorkColors =
        {
            new Color(0.62f, 0.44f, 0.27f, 1f),
            new Color(0.48f, 0.32f, 0.19f, 1f),
            new Color(0.78f, 0.62f, 0.42f, 1f)
        };

        private const float Gravity = 900f;

        private readonly List<Speck> specks = new List<Speck>();
        private float elapsed;
        private float lifetime;

        // A few crumbs knocked out of the cork, falling from the pin. scale is the poster width.
        public static void Crumbs(RectTransform layer, Vector2 at, float scale)
        {
            PosterSpecks burst = Create(layer, at, "Cork Crumbs");
            for (int i = 0; i < 6; i++)
            {
                var velocity = new Vector2(Random.Range(-1f, 1f) * scale * 0.9f, Random.Range(0.2f, 1.2f) * scale);
                burst.Add(CorkColors[Random.Range(0, CorkColors.Length)], scale * Random.Range(0.018f, 0.035f), velocity, Random.Range(0.35f, 0.55f));
            }
        }

        // Ink flicked out around the stamp in its colour. scale is the poster width.
        public static void Ink(RectTransform layer, Vector2 at, Color color, float scale)
        {
            PosterSpecks burst = Create(layer, at, "Stamp Ink");
            for (int i = 0; i < 10; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * scale * Random.Range(1.2f, 2.4f);
                burst.Add(color, scale * Random.Range(0.012f, 0.03f), velocity, Random.Range(0.18f, 0.3f));
            }
        }

        // Gold sparkles bursting all around, for the leader's star landing. scale is the star's size.
        public static void Sparkle(RectTransform layer, Vector2 at, Color color, float scale)
        {
            PosterSpecks burst = Create(layer, at, "Gold Sparkle");
            for (int i = 0; i < 14; i++)
            {
                float angle = i * Mathf.PI * 2f / 14f + Random.Range(-0.2f, 0.2f);
                Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * scale * Random.Range(1.6f, 3f);
                Color tint = Color.Lerp(color, Color.white, Random.Range(0f, 0.5f));
                burst.Add(tint, scale * Random.Range(0.05f, 0.1f), velocity, Random.Range(0.35f, 0.55f));
            }
        }

        // A couple of small gold specks left behind by the flying star. scale is the star's size.
        public static void Trail(RectTransform layer, Vector2 at, Color color, float scale)
        {
            PosterSpecks burst = Create(layer, at, "Gold Trail");
            for (int i = 0; i < 2; i++)
            {
                Vector2 velocity = Random.insideUnitCircle * scale * 0.4f;
                burst.Add(color, scale * Random.Range(0.04f, 0.07f), velocity, Random.Range(0.3f, 0.45f));
            }
        }

        private static PosterSpecks Create(RectTransform layer, Vector2 at, string burstName)
        {
            var burst = new GameObject(burstName, typeof(RectTransform)).AddComponent<PosterSpecks>();
            var rect = (RectTransform)burst.transform;
            rect.SetParent(layer, false);
            rect.anchoredPosition = at;
            rect.SetAsLastSibling();
            return burst;
        }

        private void Add(Color color, float size, Vector2 velocity, float duration)
        {
            var piece = new GameObject("Speck", typeof(RectTransform), typeof(Image));
            piece.transform.SetParent(transform, false);
            var image = piece.GetComponent<Image>();
            image.sprite = PosterSprites.Dot;
            image.color = color;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            specks.Add(new Speck { Image = image, Velocity = velocity, Duration = duration });
            lifetime = Mathf.Max(lifetime, duration);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            foreach (Speck speck in specks)
            {
                float t = Mathf.Clamp01(elapsed / speck.Duration);
                speck.Image.rectTransform.anchoredPosition = speck.Velocity * elapsed + Vector2.down * (0.5f * Gravity * elapsed * elapsed);
                Color color = speck.Image.color;
                color.a = 1f - t * t;
                speck.Image.color = color;
            }

            if (elapsed >= lifetime) Destroy(gameObject);
        }
    }
}
