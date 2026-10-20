using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Plays the intro wall: waits for the scene to settle so the video starts smoothly, plays the
    // patrol car arriving, and the moment everything in it has landed (settledAtSeconds) cross-fades
    // into a living copy built from parts laid exactly over it: the parked car idling on its springs,
    // and the four dogs breathing (their idle videos, made ready a second early so nothing waits).
    // The parts are keyed by the same chroma key as the video, so the colours match and the swap does
    // not show. The logo is not part of the swap: IntroLogo shows it the whole time. The red and blue lights on the cars flash in turn all along.
    // Console lines start with [IntroShow].
    public sealed class IntroShow : MonoBehaviour
    {
        [SerializeField] private WallVideo arrival;
        // The parts that take over when the arrival ends; kept switched off until then.
        [SerializeField] private CanvasGroup idle;
        [SerializeField] private WallVideo[] dogs;
        [SerializeField] private RectTransform car;
        // Glows over the light bars (the parked car and the blurred car in front).
        [SerializeField] private Graphic[] redLights;
        [SerializeField] private Graphic[] blueLights;

        [Header("Timing")]
        // Lets the first heavy frames (loading the wall pictures) pass before the video starts: waits at
        // least this long, then until the frame rate is steady (or the most, whichever comes first).
        [SerializeField, Min(0f)] private float warmUpSeconds = 0.6f;
        [SerializeField, Min(0f)] private float warmUpMostSeconds = 4f;
        // Seconds into the video when everything has landed; from here on only its lights change.
        [SerializeField, Min(0f)] private float settledAtSeconds = 5f;
        // The dogs' videos are made ready this long before, so they can start at once.
        [SerializeField, Min(0f)] private float prepareAheadSeconds = 1f;
        [SerializeField, Min(0.05f)] private float crossFadeSeconds = 0.3f;
        // Gives up waiting for the dogs' videos after this long and fades in anyway.
        [SerializeField, Min(0.1f)] private float dogsTimeoutSeconds = 1f;

        [Header("Idle")]
        // The engine running: a quick small shudder on top of a slow rock on the springs.
        [SerializeField, Min(0f)] private float carBounce = 0.006f;
        [SerializeField, Min(0.1f)] private float carRockSeconds = 1.6f;
        [SerializeField, Min(0.05f)] private float lightFlashSeconds = 0.32f;

        private Graphic arrivalImage;
        private Vector2 carHome;
        private float idleTime;
        private float lightTime;
        private bool preparing;
        private bool takingOver;
        private bool idling;

        // True once the living copy has taken over and the wall is idling.
        public bool IsIdling => idling;
        // Raised once, the moment the wall starts idling.
        public event Action Idling;

        private void Awake()
        {
            if (idle != null)
            {
                idle.alpha = 0f;
                idle.gameObject.SetActive(false);
            }

            if (car != null) carHome = car.anchoredPosition;
            if (arrival == null) return;

            arrivalImage = arrival.GetComponent<Graphic>();
            arrival.Finished += OnArrived;
        }

        private IEnumerator Start()
        {
            if (arrival == null) yield break;

            float started = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(warmUpSeconds);

            // Ten frames in a row at 30 fps or better count as settled.
            int steady = 0;
            while (steady < 10 && Time.realtimeSinceStartup - started < warmUpMostSeconds)
            {
                steady = Time.unscaledDeltaTime < 1f / 30f ? steady + 1 : 0;
                yield return null;
            }

            arrival.Play();
            Debug.Log($"[IntroShow] The patrol car arrives (after {Time.realtimeSinceStartup - started:0.0}s of warm-up)", this);
        }

        private void OnDestroy()
        {
            if (arrival != null) arrival.Finished -= OnArrived;
        }

        // A fallback in case the settle time was set past the end of the video.
        private void OnArrived(WallVideo _)
        {
            BeginTakeOver();
        }

        private void PrepareIdle()
        {
            if (preparing || idle == null) return;

            // Switched on hidden: the dogs' videos get ready without playing yet.
            preparing = true;
            idle.alpha = 0f;
            idle.gameObject.SetActive(true);
        }

        private void BeginTakeOver()
        {
            if (takingOver || idle == null || !isActiveAndEnabled) return;

            takingOver = true;
            Debug.Log($"[IntroShow] Idle takes over at {(arrival != null ? arrival.PlaybackTime : 0d):0.00}s into the video", this);
            PrepareIdle();
            StartCoroutine(TakeOver());
        }

        // Starts the dogs, waits for their first picture, then fades the parts in over the video and
        // the video out.
        private IEnumerator TakeOver()
        {
            if (dogs != null)
            {
                foreach (WallVideo dog in dogs)
                {
                    if (dog != null) dog.Play();
                }
            }

            float waited = 0f;
            while (!DogsReady() && waited < dogsTimeoutSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            // The parts fade in over the video, which stays solid underneath so nothing behind ever
            // shows through mid-fade; it is switched off once the parts fully cover it. They only start
            // moving after that, so nothing is doubled during the fade.
            for (float time = 0f; time < crossFadeSeconds; time += Time.unscaledDeltaTime)
            {
                float u = time / crossFadeSeconds;
                idle.alpha = u * u * (3f - 2f * u);
                yield return null;
            }

            idle.alpha = 1f;
            if (arrivalImage != null) arrivalImage.enabled = false;
            idling = true;
            Idling?.Invoke();
        }

        private bool DogsReady()
        {
            if (dogs == null) return true;
            foreach (WallVideo dog in dogs)
            {
                if (dog != null && dog.Frame == null) return false;
            }

            return true;
        }

        private void Update()
        {
            UpdateLights();
            if (arrival != null && !takingOver && arrival.IsPlaying)
            {
                double played = arrival.PlaybackTime;
                if (played >= settledAtSeconds - prepareAheadSeconds) PrepareIdle();
                if (played >= settledAtSeconds) BeginTakeOver();
            }

            if (!idling) return;

            idleTime += Time.deltaTime;
            // The motion builds up over the first second instead of jumping in.
            float ease = Mathf.SmoothStep(0f, 1f, idleTime / 1f);
            if (car != null)
            {
                float height = car.rect.height;
                float rock = Mathf.Sin(idleTime * Mathf.PI * 2f / carRockSeconds);
                float shudder = Mathf.Sin(idleTime * Mathf.PI * 2f * 7f) * 0.35f;
                car.anchoredPosition = carHome + Vector2.up * (height * carBounce * ease * (rock + shudder));
                car.localScale = new Vector3(1f, 1f + carBounce * 0.6f * ease * rock, 1f);
            }
        }

        // Red and blue take turns, each with a short double blink like a real light bar.
        private void UpdateLights()
        {
            lightTime += Time.deltaTime;
            float phase = Mathf.Repeat(lightTime / lightFlashSeconds, 2f);
            SetLights(redLights, phase < 1f ? Blink(phase) : 0.15f);
            SetLights(blueLights, phase >= 1f ? Blink(phase - 1f) : 0.15f);
        }

        // Bright, a short dip, bright again, then off before the other colour.
        private static float Blink(float t)
        {
            if (t < 0.35f) return 1f;
            if (t < 0.5f) return 0.25f;
            return t < 0.85f ? 1f : 0.15f;
        }

        private static void SetLights(Graphic[] lights, float strength)
        {
            if (lights == null) return;
            foreach (Graphic light in lights)
            {
                if (light == null) continue;
                Color color = light.color;
                color.a = strength;
                light.color = color;
            }
        }
    }
}
