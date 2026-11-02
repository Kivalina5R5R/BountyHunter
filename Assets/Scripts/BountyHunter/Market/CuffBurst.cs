using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The burst round a thief the moment it is handcuffed (PoliceCuffs), so the player sees at once
    // the catch took: a bright flash, a ring shooting out and sparks flying off, all gone in about
    // half a second. Lies flat on the floor; drawn with the soft dot and ring (PosterSprites).
    public sealed class CuffBurst : MonoBehaviour
    {
        // Over the thieves (8), under the handcuffs (9).
        private const int Order = 8;
        private const float Height = 0.04f;
        private const float Seconds = 0.5f;
        private const int SparkCount = 10;

        private sealed class Spark
        {
            public SpriteRenderer Picture;
            public Vector2 Direction;
            public float Reach;
            public float Size;
        }

        private readonly List<Spark> sparks = new List<Spark>();
        private SpriteRenderer flash;
        private SpriteRenderer ring;
        private float width;
        private Color colour;
        private float bornAt;

        // A burst width metres across at the feet of a thief, in colour (the flash is white).
        public static void Play(Vector3 at, float width, Color colour, Transform parent)
        {
            var holder = new GameObject("Cuff Burst");
            holder.transform.SetParent(parent, false);
            holder.transform.SetPositionAndRotation(new Vector3(at.x, Height, at.z), Quaternion.Euler(90f, 0f, 0f));
            holder.AddComponent<CuffBurst>().Build(width, colour);
        }

        private void Build(float burstWidth, Color burstColour)
        {
            width = burstWidth;
            colour = burstColour;
            bornAt = Time.time;
            flash = Picture("Flash", PosterSprites.Dot);
            ring = Picture("Ring", PosterSprites.Ring);
            for (int i = 0; i < SparkCount; i++)
            {
                float angle = (i + Random.value * 0.6f) / SparkCount * Mathf.PI * 2f;
                sparks.Add(new Spark
                {
                    Picture = Picture("Spark", PosterSprites.Dot),
                    Direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                    Reach = width * Random.Range(0.4f, 0.6f),
                    Size = width * Random.Range(0.06f, 0.1f),
                });
            }

            Show(0f);
        }

        private void Update()
        {
            float t = (Time.time - bornAt) / Seconds;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            Show(t);
        }

        private void Show(float t)
        {
            float outward = 1f - (1f - t) * (1f - t);
            float fade = 1f - t;

            // The flash is gone in the first third.
            float flashLeft = Mathf.Clamp01(1f - t * 3f);
            Size(flash, width * (0.5f + 0.4f * outward));
            flash.color = new Color(1f, 1f, 1f, 0.9f * flashLeft);

            Size(ring, width * (0.3f + 0.7f * outward));
            Color ringColour = colour;
            ringColour.a = fade;
            ring.color = ringColour;

            foreach (Spark spark in sparks)
            {
                Vector2 at = spark.Direction * (spark.Reach * outward);
                spark.Picture.transform.localPosition = new Vector3(at.x, at.y, 0f);
                Size(spark.Picture, spark.Size * (1f - t * 0.7f));
                Color sparkColour = Color.Lerp(Color.white, colour, t);
                sparkColour.a = fade;
                spark.Picture.color = sparkColour;
            }
        }

        private static void Size(SpriteRenderer picture, float metres)
        {
            picture.transform.localScale = Vector3.one * (metres / Mathf.Max(0.0001f, picture.sprite.bounds.size.x));
        }

        private SpriteRenderer Picture(string name, Sprite sprite)
        {
            var piece = new GameObject(name, typeof(SpriteRenderer));
            piece.transform.SetParent(transform, false);
            var picture = piece.GetComponent<SpriteRenderer>();
            picture.sprite = sprite;
            picture.sortingOrder = Order;
            return picture;
        }
    }
}
