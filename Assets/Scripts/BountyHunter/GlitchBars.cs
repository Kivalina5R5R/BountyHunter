using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // A broken-signal flicker over a cut-in's background, like a security camera's picture tearing:
    // bursts of thin horizontal bars, white and grey, stacked in a band at a random height, each lit
    // for a moment and gone, now and then a thicker block among them. Faint (barAlpha) so the pieces in
    // front stay clear. Strength (0 to 1) scales it, so it can fade in and out with the background.
    // A handful of bars are kept and reused; on unscaled time, so it plays while the game is frozen.
    [RequireComponent(typeof(RectTransform))]
    public sealed class GlitchBars : MonoBehaviour
    {
        private sealed class Bar
        {
            public Image Picture;
            public float Until;
            public float Alpha;
        }

        // Bands of bars a second, and how many bars in each.
        [SerializeField, Min(0f)] private float burstsPerSecond = 9f;
        [SerializeField] private Vector2Int barsPerBurst = new Vector2Int(3, 9);
        // How long a bar stays lit (seconds).
        [SerializeField] private Vector2 barSeconds = new Vector2(0.05f, 0.16f);
        // Bar sizes in wall units: widths, the thin bars' heights, and the odd block's height.
        [SerializeField] private Vector2 barWidth = new Vector2(30f, 320f);
        [SerializeField] private Vector2 barHeight = new Vector2(1.2f, 5f);
        [SerializeField] private Vector2 blockHeight = new Vector2(10f, 26f);
        [SerializeField, Range(0f, 1f)] private float blockChance = 0.12f;
        // How tall a band of bars is.
        [SerializeField, Min(1f)] private float bandHeight = 40f;
        // How see-through the bars are at their brightest (1 solid).
        [SerializeField, Range(0f, 1f)] private float barAlpha = 0.3f;

        private readonly List<Bar> live = new List<Bar>();
        private readonly Stack<Image> spare = new Stack<Image>();
        private float burstsOwed;

        // 0 shows nothing (no new bars either), 1 the full flicker.
        public float Strength { get; set; } = 1f;

        public void Clear()
        {
            foreach (Bar bar in live) Recycle(bar.Picture);
            live.Clear();
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Bar bar = live[i];
                if (now < bar.Until)
                {
                    SetAlpha(bar.Picture, bar.Alpha * Strength);
                    continue;
                }

                Recycle(bar.Picture);
                live.RemoveAt(i);
            }

            if (Strength <= 0f) return;
            burstsOwed += burstsPerSecond * Time.unscaledDeltaTime;
            while (burstsOwed >= 1f)
            {
                burstsOwed -= 1f;
                Burst(now);
            }
        }

        // A band of bars at a random height, starting about the same place across, as a tear does.
        private void Burst(float now)
        {
            Rect area = ((RectTransform)transform).rect;
            float bandMiddle = Random.Range(area.yMin, area.yMax);
            float left = Random.Range(area.xMin, area.xMax);
            int count = Random.Range(barsPerBurst.x, barsPerBurst.y + 1);
            for (int i = 0; i < count; i++)
            {
                bool block = Random.value < blockChance;
                float width = Random.Range(barWidth.x, barWidth.y) * (block ? 0.6f : 1f);
                float height = block ? Random.Range(blockHeight.x, blockHeight.y) : Random.Range(barHeight.x, barHeight.y);
                float x = left + Random.Range(-0.3f, 1f) * barWidth.y * 0.5f;
                float y = bandMiddle + Random.Range(-0.5f, 0.5f) * bandHeight;

                Image picture = Take();
                RectTransform place = picture.rectTransform;
                place.sizeDelta = new Vector2(width, height);
                place.anchoredPosition = new Vector2(x + width * 0.5f, y);
                // Mostly white, some grey, as the broken lines of a torn picture.
                float shade = Random.value < 0.7f ? 1f : Random.Range(0.45f, 0.8f);
                picture.color = new Color(shade, shade, shade, 0f);
                var bar = new Bar
                {
                    Picture = picture,
                    Until = now + Random.Range(barSeconds.x, barSeconds.y),
                    Alpha = barAlpha * Random.Range(0.5f, 1f),
                };
                SetAlpha(picture, bar.Alpha * Strength);
                live.Add(bar);
            }
        }

        private static void SetAlpha(Graphic picture, float alpha)
        {
            Color colour = picture.color;
            colour.a = alpha;
            picture.color = colour;
        }

        private Image Take()
        {
            if (spare.Count > 0)
            {
                Image reused = spare.Pop();
                reused.gameObject.SetActive(true);
                return reused;
            }

            var picture = new GameObject("Bar", typeof(RectTransform)).AddComponent<Image>();
            picture.raycastTarget = false;
            picture.rectTransform.SetParent(transform, false);
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
