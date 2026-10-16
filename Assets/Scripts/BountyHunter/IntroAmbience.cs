using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Keeps the intro wall alive between its big moments: the view drifts and breathes like a
    // hand-held camera, lightning flashes now and then, the street lamp flickers and rain rings ripple
    // on the wet ground. Sits on the intro group and moves it;
    // other effects shake the wall through Shake so the two add up.
    public sealed class IntroAmbience : MonoBehaviour
    {
        [Header("Camera")]
        // How far the view drifts, in the wall's units, and how long each slow sway takes.
        [SerializeField] private Vector2 swayAmount = new Vector2(7f, 3.5f);
        [SerializeField] private Vector2 swaySeconds = new Vector2(11f, 7.5f);
        // Zoomed in a touch so the drift never shows an edge; breathes by zoomBreath around it.
        [SerializeField, Min(1f)] private float zoom = 1.03f;
        [SerializeField, Range(0f, 0.05f)] private float zoomBreath = 0.008f;

        [Header("Lightning")]
        // A sky glow (bright at the top, gone by the middle) so it never glares over the scene.
        [SerializeField] private Graphic lightning;
        [SerializeField] private Vector2 lightningEvery = new Vector2(18f, 35f);
        [SerializeField, Range(0f, 1f)] private float lightningStrength = 0.15f;

        [Header("Street lamp")]
        [SerializeField] private Graphic[] lamps;
        [SerializeField, Range(0f, 1f)] private float lampBrightness = 0.55f;

        [Header("Ripples")]
        // The wall picture; rings appear on its ground band (parts of its height, from the bottom).
        [SerializeField] private RectTransform ground;
        [SerializeField] private Texture ripple;
        [SerializeField] private Vector2 groundBand = new Vector2(0.02f, 0.24f);
        [SerializeField, Min(0f)] private float ripplesPerSecond = 12f;
        [SerializeField] private Vector2 rippleSize = new Vector2(0.02f, 0.06f);

        private sealed class Ring
        {
            public RawImage Picture;
            public float Age;
            public float Life;
            public float Width;
        }

        private readonly List<Ring> rings = new List<Ring>();
        private readonly Stack<Ring> spareRings = new Stack<Ring>();
        private RectTransform view;
        private Vector3 home;
        private float time;
        private float nextLightning;
        private float lightningAge = float.MaxValue;
        private float rippleDebt;
        private float shakeStrength;
        private float shakeLeft;
        private float shakeSeconds;
        private float lampDip;

        private void Awake()
        {
            view = (RectTransform)transform;
            home = view.localPosition;
            nextLightning = Random.Range(lightningEvery.x * 0.4f, lightningEvery.y * 0.6f);
            SetAlpha(lightning, 0f);
        }

        private void OnDisable()
        {
            view.localPosition = home;
            view.localScale = Vector3.one;
        }

        // A jolt of the whole wall, strength in the wall's units, dying away over seconds.
        public void Shake(float strength, float seconds)
        {
            shakeStrength = strength;
            shakeSeconds = Mathf.Max(0.01f, seconds);
            shakeLeft = shakeSeconds;
        }

        private void LateUpdate()
        {
            // Nothing to keep alive once the intro wall is gone.
            if (ground != null && !ground.gameObject.activeInHierarchy) return;

            float dt = Time.deltaTime;
            time += dt;
            MoveCamera(dt);
            UpdateLightning(dt);
            UpdateLamps(dt);
            UpdateRipples(dt);
        }

        private void MoveCamera(float dt)
        {
            var sway = new Vector2(
                Mathf.Sin(time * Mathf.PI * 2f / swaySeconds.x) * swayAmount.x,
                Mathf.Sin(time * Mathf.PI * 2f / swaySeconds.y + 1.3f) * swayAmount.y);

            Vector2 jolt = Vector2.zero;
            if (shakeLeft > 0f)
            {
                shakeLeft -= dt;
                float fade = Mathf.Clamp01(shakeLeft / shakeSeconds);
                jolt = Random.insideUnitCircle * (shakeStrength * fade * fade);
            }

            view.localPosition = home + (Vector3)(sway + jolt);
            view.localScale = Vector3.one * (zoom * (1f + zoomBreath * Mathf.Sin(time * Mathf.PI * 2f / 9f)));
        }

        // A soft glow behind the clouds: rises, eases off, glows once more faintly, fades slowly.
        private void UpdateLightning(float dt)
        {
            if (lightning == null) return;

            nextLightning -= dt;
            if (nextLightning <= 0f)
            {
                lightningAge = 0f;
                nextLightning = Random.Range(lightningEvery.x, lightningEvery.y);
            }

            lightningAge += dt;
            float a = lightningAge;
            float first = Bump(a, 0.12f, 0.35f);
            float second = 0.55f * Bump(a - 0.4f, 0.15f, 1.1f);
            SetAlpha(lightning, Mathf.Max(first, second) * lightningStrength);
        }

        // 0 to 1 over rise seconds (smoothly), back to 0 over fall seconds; 0 outside.
        private static float Bump(float t, float rise, float fall)
        {
            if (t <= 0f || t >= rise + fall) return 0f;
            if (t < rise) return Mathf.SmoothStep(0f, 1f, t / rise);
            return Mathf.SmoothStep(1f, 0f, (t - rise) / fall);
        }

        // Mostly steady with a soft hum, now and then a quick stutter like a failing tube.
        private void UpdateLamps(float dt)
        {
            if (lamps == null) return;

            if (lampDip <= 0f && Random.value < dt * 0.25f) lampDip = Random.Range(0.15f, 0.4f);
            float dip = 1f;
            if (lampDip > 0f)
            {
                lampDip -= dt;
                dip = Mathf.PerlinNoise(time * 40f, 0.5f) < 0.5f ? 0.25f : 1f;
            }

            float hum = 0.92f + 0.08f * Mathf.PerlinNoise(time * 3f, 1.7f);
            foreach (Graphic lamp in lamps) SetAlpha(lamp, lampBrightness * hum * dip);
        }

        private void UpdateRipples(float dt)
        {
            if (ground == null || ripple == null) return;

            rippleDebt += ripplesPerSecond * dt;
            while (rippleDebt >= 1f)
            {
                rippleDebt -= 1f;
                Splash();
            }

            for (int i = rings.Count - 1; i >= 0; i--)
            {
                Ring ring = rings[i];
                ring.Age += dt;
                float t = ring.Age / ring.Life;
                if (t >= 1f)
                {
                    ring.Picture.gameObject.SetActive(false);
                    spareRings.Push(ring);
                    rings.RemoveAt(i);
                    continue;
                }

                float grow = 1f - (1f - t) * (1f - t);
                ring.Picture.rectTransform.sizeDelta = new Vector2(ring.Width, ring.Width * 0.3f) * Mathf.Lerp(0.2f, 1f, grow);
                SetAlpha(ring.Picture, 0.45f * (1f - t));
            }
        }

        // A ring on the wet ground; nearer the bottom (closer to the camera) it is bigger.
        private void Splash()
        {
            Ring ring = spareRings.Count > 0 ? spareRings.Pop() : CreateRing();
            Rect rect = ground.rect;
            float y = Random.Range(groundBand.x, groundBand.y);
            float near = 1f - Mathf.InverseLerp(groundBand.x, groundBand.y, y);
            ring.Width = Mathf.Lerp(rippleSize.x, rippleSize.y, near) * rect.height;
            ring.Age = 0f;
            ring.Life = Random.Range(0.45f, 0.8f);
            ring.Picture.rectTransform.anchoredPosition = new Vector2(Random.value * rect.width, y * rect.height);
            ring.Picture.gameObject.SetActive(true);
            rings.Add(ring);
        }

        private Ring CreateRing()
        {
            var ringObject = new GameObject("Ripple", typeof(RectTransform), typeof(RawImage));
            ringObject.transform.SetParent(ground, false);
            // Behind the cars and the dogs: right after the arrival video.
            Transform video = ground.Find("Video");
            if (video != null) ringObject.transform.SetSiblingIndex(video.GetSiblingIndex() + 1);
            var picture = ringObject.GetComponent<RawImage>();
            picture.texture = ripple;
            picture.raycastTarget = false;
            picture.rectTransform.anchorMin = picture.rectTransform.anchorMax = Vector2.zero;
            return new Ring { Picture = picture };
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
