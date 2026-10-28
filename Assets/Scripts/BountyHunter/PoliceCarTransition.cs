using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The way out of a mini game, laid out in Assets/Prefabs/Transition/Police Car Transition.prefab
    // (move, resize or recolour anything there): a police car speeds across the wall once from left to
    // right, its siren flashing red and blue. As a wipe, the mini game is still on the wall ahead of
    // the car and the new map is already behind it. Used to cover the wall instead (Close and Open),
    // the wall fades dark first and the car pulls the dark away behind it.
    public sealed class PoliceCarTransition : WallTransition
    {
        [Header("Pieces")]
        // Fills the wall; while the car passes, its left edge follows the car's back.
        [SerializeField] private Image cover;
        [SerializeField] private RectTransform car;
        // Over the light bar, flashing in turn.
        [SerializeField] private Image redLight;
        [SerializeField] private Image blueLight;

        [Header("Timing (seconds)")]
        [SerializeField, Min(0f)] private float fadeSeconds = 0.45f;
        // The least time the wall stays dark before the car comes.
        [SerializeField, Min(0f)] private float holdSeconds = 0.3f;
        // How long the car takes to cross the wall.
        [SerializeField, Min(0.1f)] private float driveSeconds = 2.1f;
        // How often the siren swaps red and blue.
        [SerializeField, Min(0.02f)] private float sirenSeconds = 0.14f;

        [Header("Motion")]
        // How far into the car the dark's edge sits, as a share of the car's length, so no gap shows.
        [SerializeField, Range(0f, 1f)] private float coverUnderCar = 0.35f;

        private float coverAlpha = -1f;
        private float lightAlpha;
        private float closedAt;

        public override bool IsSetUp => cover != null && car != null;

        public override bool WipesOver => true;

        public override float WipeSeconds => driveSeconds;

        // The car crosses once; leaving is clipped to what is still ahead of the car's back.
        public override IEnumerator Wipe(RectTransform leaving)
        {
            gameObject.SetActive(true);
            Remember();
            cover.gameObject.SetActive(false);
            car.gameObject.SetActive(true);
            RectMask2D clip = leaving != null ? leaving.GetComponent<RectMask2D>() : null;
            bool addedClip = leaving != null && clip == null;
            if (addedClip) clip = leaving.gameObject.AddComponent<RectMask2D>();

            float start = Time.unscaledTime;
            while (true)
            {
                float time = Time.unscaledTime - start;
                float back = Drive(Mathf.Clamp01(time / driveSeconds), time);
                // The old screen shows only from the car's back to the wall's right edge.
                if (clip != null) clip.padding = new Vector4(Mathf.Clamp(back + Wall * 0.5f, 0f, Wall), 0f, 0f, 0f);
                if (time >= driveSeconds) break;
                yield return null;
            }

            if (clip != null) clip.padding = Vector4.zero;
            if (addedClip) Destroy(clip);
            cover.gameObject.SetActive(true);
            gameObject.SetActive(false);
        }

        // Fades the wall to dark, the car not there yet.
        public override IEnumerator Close()
        {
            gameObject.SetActive(true);
            Remember();
            car.gameObject.SetActive(false);
            SetCover(-Wall * 0.5f);
            for (float time = 0f; time < fadeSeconds; time += Time.unscaledDeltaTime)
            {
                SetAlpha(cover, coverAlpha * Smooth(time / fadeSeconds));
                yield return null;
            }

            SetAlpha(cover, coverAlpha);
            closedAt = Time.unscaledTime;
        }

        // The car crosses once, pulling the dark away behind it.
        public override IEnumerator Open()
        {
            while (Time.unscaledTime < closedAt + holdSeconds) yield return null;

            car.gameObject.SetActive(true);
            float start = Time.unscaledTime;
            while (true)
            {
                float time = Time.unscaledTime - start;
                float u = Mathf.Clamp01(time / driveSeconds);
                float back = Drive(u, time);
                SetCover(u >= 1f ? Wall * 0.5f : back);
                if (time >= driveSeconds) break;
                yield return null;
            }

            gameObject.SetActive(false);
        }

        private float Wall => ((RectTransform)transform).rect.width;

        // The pieces' own alphas, which the fades and the siren work from.
        private void Remember()
        {
            if (coverAlpha >= 0f) return;

            coverAlpha = cover.color.a;
            lightAlpha = redLight != null ? redLight.color.a : 1f;
        }

        // Puts the car u of the way across (just off the left edge to just off the right) with its
        // siren on, and returns where its back is (wall units from the middle), a little in under the
        // car so no gap shows there.
        private float Drive(float u, float time)
        {
            float length = car.rect.width * Mathf.Abs(car.localScale.x);
            // A touch faster in the middle than at the edges, so it reads as speeding by.
            float eased = Mathf.Lerp(u, u * u * (3f - 2f * u), 0.35f);
            float x = Mathf.Lerp(-(Wall + length) * 0.5f, (Wall + length) * 0.5f, eased);
            car.anchoredPosition = new Vector2(x, car.anchoredPosition.y);
            Siren(time);
            return u >= 1f ? Wall : x - length * (0.5f - coverUnderCar);
        }

        private void Siren(float time)
        {
            bool red = Mathf.Repeat(time / sirenSeconds, 2f) < 1f;
            SetAlpha(redLight, red ? lightAlpha : 0f);
            SetAlpha(blueLight, red ? 0f : lightAlpha);
        }

        // The dark from left (wall units from the middle) to the wall's right edge.
        private void SetCover(float left)
        {
            float right = Wall * 0.5f;
            left = Mathf.Clamp(left, -right, right);
            RectTransform rect = cover.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(right - left, ((RectTransform)transform).rect.height);
            rect.anchoredPosition = new Vector2((left + right) * 0.5f, 0f);
        }

        private static float Smooth(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        private static void SetAlpha(Graphic picture, float alpha)
        {
            if (picture == null) return;

            Color colour = picture.color;
            colour.a = alpha;
            picture.color = colour;
        }
    }
}
