using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The BOUNTY HUNTER logo of the intro, drawn from Logo.png the whole time it shows. The arrival
    // video has its own logo taken out (BountyHunter_Intro_NoLogo.mp4), and this one comes in where
    // that one did, step for step: it crashes in huge, slams down to size, bounces a little and
    // settles, with a burst of gold. It then idles:
    // breathing gently, a band of light sweeping across it now and then, and the sheriff star glinting.
    public sealed class IntroLogo : MonoBehaviour
    {
        [SerializeField] private WallVideo arrival;
        [SerializeField] private RectTransform logo;
        // The light band, a child of the logo clipped to its shape by a Mask.
        [SerializeField] private RectTransform shine;
        [SerializeField] private RectTransform glint;

        [Header("Entrance")]
        // Moves the whole entrance in time; 0 keeps it where the video's logo came in.
        [SerializeField] private float entranceShiftSeconds;

        [Header("Idle")]
        [SerializeField, Range(0f, 0.05f)] private float breathe = 0.012f;
        [SerializeField, Min(0.5f)] private float breatheSeconds = 3.2f;
        [SerializeField, Min(0.5f)] private float shineEverySeconds = 3.6f;
        [SerializeField, Min(0.1f)] private float shineSeconds = 0.85f;
        [SerializeField, Min(0.5f)] private float glintEverySeconds = 2.7f;
        [SerializeField, Min(0.1f)] private float glintSeconds = 0.45f;

        private static readonly Color Gold = new Color(1f, 0.84f, 0.3f, 1f);

        // The original video's logo, measured frame by frame: seconds into the video, size (1 = settled)
        // and how far right of its settled place its centre is (in logo widths). It moves in held
        // steps, about 20 a second, and this one does the same so the entrance keeps the video's feel.
        private static readonly float[] KeySeconds = { 4.6f, 4.65f, 4.7f, 4.733f, 4.783f, 4.817f, 4.833f, 4.85f, 4.883f, 4.9f, 4.917f };
        private static readonly float[] KeyScales = { 2.3f, 1.94f, 1.62f, 1.135f, 0.985f, 0.998f, 1.01f, 1.022f, 1.035f, 1.02f, 1f };
        private static readonly float[] KeyShifts = { 0.075f, 0.066f, 0.037f, 0.021f, 0.004f, 0f, 0f, 0f, 0f, 0f, 0f };
        // The step where it hits the wall.
        private const int LandingKey = 4;

        private Graphic logoPicture;
        private bool appeared;
        private bool idling;
        private float idleTime;

        private void Awake()
        {
            if (logo == null) return;

            logoPicture = logo.GetComponent<Graphic>();
            SetVisible(false);
        }

        private void Update()
        {
            if (logo == null) return;

            if (!appeared && arrival != null && arrival.IsPlaying && arrival.PlaybackTime >= KeySeconds[0] + entranceShiftSeconds)
            {
                appeared = true;
                    StartCoroutine(Entrance());
            }

            if (idling) Idle();
        }

        // Follows the video's clock step by step, so it lands on the same frame as the video's logo would.
        private IEnumerator Entrance()
        {
            SetVisible(true);
            Vector2 home = logo.anchoredPosition;
            float width = logo.rect.width;
            bool landed = false;
            float last = KeySeconds[KeySeconds.Length - 1] + entranceShiftSeconds;
            while (arrival.IsPlaying && arrival.PlaybackTime < last)
            {
                float t = (float)arrival.PlaybackTime - entranceShiftSeconds;
                int key = 0;
                while (key + 1 < KeySeconds.Length && KeySeconds[key + 1] <= t) key++;

                logo.localScale = Vector3.one * KeyScales[key];
                logo.anchoredPosition = home + Vector2.right * (KeyShifts[key] * width);
                if (!landed && key >= LandingKey)
                {
                    landed = true;
                    Slammed();
                }

                yield return null;
            }

            if (!landed) Slammed();
            logo.localScale = Vector3.one;
            logo.anchoredPosition = home;
            idling = true;
        }

        // Gold bursting out around the logo as it lands, and the star catching the light.
        private void Slammed()
        {
            var layer = logo.parent as RectTransform;
            if (layer != null) PosterSpecks.Sparkle(layer, logo.localPosition, Gold, logo.rect.width * 0.5f);
            idleTime = glintEverySeconds - 0.15f;
        }

        private void Idle()
        {
            idleTime += Time.deltaTime;
            logo.localScale = Vector3.one * (1f + breathe * Mathf.Sin(idleTime * Mathf.PI * 2f / breatheSeconds));

            if (shine != null)
            {
                // Sweeps across left to right, then waits for the next pass.
                float t = Mathf.Repeat(idleTime, shineEverySeconds) / shineSeconds;
                bool sweeping = t < 1f;
                shine.gameObject.SetActive(sweeping);
                if (sweeping)
                {
                    float eased = t * t * (3f - 2f * t);
                    float width = logo.rect.width;
                    shine.anchoredPosition = new Vector2(Mathf.Lerp(-0.75f, 0.75f, eased) * width, 0f);
                }
            }

            if (glint != null)
            {
                float t = Mathf.Repeat(idleTime, glintEverySeconds) / glintSeconds;
                bool twinkling = t < 1f;
                glint.gameObject.SetActive(twinkling);
                if (twinkling)
                {
                    glint.localScale = Vector3.one * Mathf.Sin(t * Mathf.PI);
                    glint.localRotation = Quaternion.Euler(0f, 0f, 45f * t);
                }
            }
        }

        private void SetVisible(bool visible)
        {
            if (logoPicture != null) logoPicture.enabled = visible;
            if (shine != null) shine.gameObject.SetActive(false);
            if (glint != null) glint.gameObject.SetActive(false);
        }
    }
}
