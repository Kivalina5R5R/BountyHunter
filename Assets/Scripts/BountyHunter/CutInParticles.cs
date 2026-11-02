using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The bits flying about a cut-in on the wall, as UI pictures over it: sparks and confetti that
    // shoot out, slow down, fall and fade (Burst), a shockwave ring that grows and fades (Ring), and
    // stars that twinkle in place (Twinkle). Dots and rings are drawn in code (PosterSprites); stars use
    // the star picture when there is one. Positions are in this layer's own units (the wall's). A
    // handful of pictures are kept and reused; on unscaled time, so it plays while the game is frozen.
    [RequireComponent(typeof(RectTransform))]
    public sealed class CutInParticles : MonoBehaviour
    {
        public enum Look { Dot, Star, Ring }

        private sealed class Bit
        {
            public Image Picture;
            public Look Look;
            public Vector2 Velocity;
            public float Gravity;
            public float Spin;
            public float Born;
            public float Life;
            public float Size;
            public Color Colour;
            // A dot stretched along its way while it moves fast.
            public bool Streaks;
            // A star growing and shrinking in place.
            public bool Twinkles;
        }

        [SerializeField] private Sprite star;
        // Never more than this many at once; the oldest makes way.
        [SerializeField, Min(8)] private int most = 220;
        // How quickly flying bits slow down (share of their speed kept each second).
        [SerializeField, Range(0.01f, 1f)] private float keepSpeed = 0.08f;

        private readonly List<Bit> live = new List<Bit>();
        private readonly Stack<Image> spare = new Stack<Image>();

        // count bits flying out from at in every direction (or within spread degrees of heading), at
        // up to speed, size across, living up to life seconds, coloured from colours; starShare of them
        // stars, the rest dots streaking along their way. gravity pulls them down.
        public void Burst(Vector2 at, int count, float speed, float size, float life, Color[] colours, float starShare = 0f,
            float gravity = 0f, float heading = 0f, float spread = 360f)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (heading + Random.Range(-spread, spread) * 0.5f) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                bool isStar = Random.value < starShare;
                Add(new Bit
                {
                    Look = isStar ? Look.Star : Look.Dot,
                    Velocity = direction * (speed * Random.Range(0.35f, 1f)),
                    Gravity = gravity,
                    Spin = isStar ? Random.Range(-540f, 540f) : 0f,
                    Life = life * Random.Range(0.6f, 1f),
                    Size = size * Random.Range(0.6f, 1.25f) * (isStar ? 1.6f : 1f),
                    Colour = colours[Random.Range(0, colours.Length)],
                    Streaks = !isStar,
                }, at);
            }
        }

        // A thin ring growing from at to size across over life seconds, fading.
        public void Ring(Vector2 at, float size, float life, Color colour)
        {
            Add(new Bit { Look = Look.Ring, Life = life, Size = size, Colour = colour }, at);
        }

        // A star at at that grows, spins a little and shrinks away.
        public void Twinkle(Vector2 at, float size, float life, Color colour)
        {
            Add(new Bit { Look = Look.Star, Life = life, Size = size, Colour = colour, Spin = Random.Range(-90f, 90f), Twinkles = true }, at);
        }

        // Where a point on the wall lies in this layer.
        public Vector2 FromWorld(Vector3 world)
        {
            return transform.InverseTransformPoint(world);
        }

        public void Clear()
        {
            foreach (Bit bit in live) Recycle(bit.Picture);
            live.Clear();
        }

        private void Add(Bit bit, Vector2 at)
        {
            if (live.Count >= most)
            {
                Recycle(live[0].Picture);
                live.RemoveAt(0);
            }

            bit.Picture = Take();
            bit.Picture.sprite = bit.Look == Look.Ring ? PosterSprites.Ring : bit.Look == Look.Star && star != null ? star : PosterSprites.Dot;
            bit.Picture.rectTransform.anchoredPosition = at;
            bit.Born = Time.unscaledTime;
            live.Add(bit);
            Pose(bit, 0f);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            float slow = Mathf.Pow(keepSpeed, dt);
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Bit bit = live[i];
                float t = (now - bit.Born) / bit.Life;
                if (t >= 1f)
                {
                    Recycle(bit.Picture);
                    live.RemoveAt(i);
                    continue;
                }

                bit.Velocity = bit.Velocity * slow + Vector2.down * (bit.Gravity * dt);
                bit.Picture.rectTransform.anchoredPosition += bit.Velocity * dt;
                Pose(bit, t);
            }
        }

        private void Pose(Bit bit, float t)
        {
            RectTransform place = bit.Picture.rectTransform;
            Color colour = bit.Colour;
            switch (bit.Look)
            {
                case Look.Ring:
                    float grown = 1f - (1f - t) * (1f - t);
                    place.sizeDelta = Vector2.one * (bit.Size * Mathf.Lerp(0.2f, 1f, grown));
                    colour.a *= 1f - t;
                    break;
                case Look.Star when bit.Twinkles:
                    // Twinkling in place: up and back down.
                    place.sizeDelta = Vector2.one * (bit.Size * Mathf.Sin(t * Mathf.PI));
                    place.localRotation = Quaternion.Euler(0f, 0f, bit.Spin * t);
                    break;
                default:
                    float size = bit.Size * (1f - t * t);
                    float speed = bit.Velocity.magnitude;
                    float stretch = bit.Streaks ? 1f + Mathf.Min(3f, speed / 300f) : 1f;
                    place.sizeDelta = new Vector2(size * stretch, size);
                    float angle = bit.Streaks && speed > 1f ? Mathf.Atan2(bit.Velocity.y, bit.Velocity.x) * Mathf.Rad2Deg : bit.Spin * t;
                    place.localRotation = Quaternion.Euler(0f, 0f, angle);
                    colour.a *= Mathf.Clamp01((1f - t) * 2.5f);
                    break;
            }

            bit.Picture.color = colour;
        }

        private Image Take()
        {
            Image picture;
            if (spare.Count > 0)
            {
                picture = spare.Pop();
                picture.gameObject.SetActive(true);
            }
            else
            {
                picture = new GameObject("Bit", typeof(RectTransform)).AddComponent<Image>();
                picture.raycastTarget = false;
                picture.rectTransform.SetParent(transform, false);
            }

            picture.rectTransform.localRotation = Quaternion.identity;
            return picture;
        }

        private void Recycle(Image picture)
        {
            if (picture == null) return;
            picture.gameObject.SetActive(false);
            spare.Push(picture);
        }

        private void OnDisable()
        {
            Clear();
        }
    }
}
