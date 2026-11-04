using System.Collections;
using TMPro;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Moves one WANTED poster on the cork board. Putting it up pushes a pin in, unrolls the poster
    // down from the pin and lets it swing to rest. Catching its thief slams a CAUGHT stamp on it,
    // then the pin pops out and the poster drops away. Taking it down without a catch (a new stage)
    // skips the stamp. The row closing up glides it, pin and all, to its new spot; a fuller row
    // shrinks it (SetSize), which it grows or shrinks into as it glides.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PosterMotion : MonoBehaviour
    {
        [Header("Pin")]
        // Pin head width and its distance below the poster's top edge, as parts of the poster.
        [SerializeField, Range(0.05f, 0.3f)] private float pinSize = 0.13f;
        [SerializeField, Range(0f, 0.3f)] private float pinInset = 0.06f;

        [Header("Putting up")]
        [SerializeField, Min(0.05f)] private float pinSeconds = 0.16f;
        [SerializeField, Min(0.05f)] private float unrollSeconds = 0.32f;
        // The whole swing from the moment it starts unrolling until it hangs still.
        [SerializeField, Min(0.1f)] private float swingSeconds = 1.1f;
        [SerializeField, Range(0f, 30f)] private float swingDegrees = 8f;
        [SerializeField, Min(0.05f)] private float slideSeconds = 0.45f;

        [Header("Caught")]
        [SerializeField] private string stampText = "CAUGHT";
        [SerializeField] private Color stampColor = new Color(0.76f, 0.09f, 0.07f, 0.9f);
        [SerializeField, Min(0.05f)] private float stampSeconds = 0.12f;
        [SerializeField, Min(0f)] private float stampHoldSeconds = 0.55f;
        [SerializeField, Min(0.1f)] private float dropSeconds = 0.6f;

        private RectTransform rect;
        private CanvasGroup group;
        // Its scale in the scene, the scale it is meant to have now (SetSize), and the one it shows.
        private Vector3 fullScale;
        private Vector3 baseScale;
        private Vector3 shownScale;
        private RectTransform layer;
        private PosterPin pin;
        private PosterStamp stamp;
        private Coroutine playing;
        // Where the poster belongs; one still unrolling or swinging follows it when it changes.
        private Vector2 home;
        private float tilt;

        // The poster's size in the layer's units.
        private Vector2 Size => Vector2.Scale(rect.rect.size, shownScale);

        // From the pin down to the poster's centre, in the layer's units.
        private float PinToCentre => Size.y * (0.5f - pinInset);

        // Call once before anything else; the poster's current scale is taken as its full size.
        // font is used for the CAUGHT stamp.
        public void Initialize(RectTransform effectLayer, TMP_FontAsset font)
        {
            layer = effectLayer;
            rect = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            fullScale = rect.localScale;
            baseScale = fullScale;
            shownScale = fullScale;
            pin = PosterPin.Create(layer, Size.x * pinSize);
            stamp = PosterStamp.Create(rect, stampText, stampColor, font);
        }

        // Its width at full size, in the layer's units.
        public float FullWidth => rect.rect.size.x * fullScale.x;

        // Sizes it to share of its full size: at once when it is not on the wall, else as it next
        // glides (SlideTo) or goes up (Appear).
        public void SetSize(float share)
        {
            baseScale = fullScale * share;
            if (!gameObject.activeInHierarchy) shownScale = baseScale;
        }

        public void HideNow()
        {
            Stop();
            pin.Hide();
            stamp.Hide();
            gameObject.SetActive(false);
        }

        // Pins the poster up at position after delay seconds, hanging turned by tiltDegrees.
        public void Appear(Vector2 position, float tiltDegrees, float delay)
        {
            Stop();
            home = position;
            tilt = tiltDegrees;
            stamp.Hide();
            gameObject.SetActive(true);
            playing = StartCoroutine(AppearRoutine(delay));
        }

        // Glides to position after delay seconds; a poster still swinging settles there instead.
        public void SlideTo(Vector2 position, float delay)
        {
            Vector2 from = home;
            home = position;
            if (playing != null || !gameObject.activeInHierarchy) return;

            playing = StartCoroutine(SlideRoutine(from, delay));
        }

        // Stamps CAUGHT on the poster, then tears it down.
        public void Caught()
        {
            if (!gameObject.activeInHierarchy) return;

            Stop();
            StayWhereItHangs();
            playing = StartCoroutine(CaughtRoutine());
        }

        // Tears the poster down without a stamp.
        public void TakeDown()
        {
            if (!gameObject.activeInHierarchy) return;

            Stop();
            StayWhereItHangs();
            playing = StartCoroutine(DropRoutine());
        }

        // Its home becomes where it hangs now. A slide it was told about may not have started yet
        // (several thieves caught at once: the row closing up for the first moves the home of the
        // next before it is stamped), and it must come down where it is, not jump to that slot.
        private void StayWhereItHangs()
        {
            float unrolled = shownScale.y != 0f ? rect.localScale.y / shownScale.y : 1f;
            float degrees = rect.localEulerAngles.z;
            Vector2 pinPoint = rect.anchoredPosition - Rotate(Vector2.down * (PinToCentre * unrolled), degrees);
            home = pinPoint - Rotate(Vector2.up * PinToCentre, tilt);
        }

        private void Stop()
        {
            if (playing != null) StopCoroutine(playing);
            playing = null;
        }

        private IEnumerator AppearRoutine(float delay)
        {
            shownScale = baseScale;
            float side = Random.value < 0.5f ? -1f : 1f;
            pin.Hide();
            // Waits rolled up at its spot; the row may change (more posters pinned up, one torn
            // down) before it goes up, and it must go up at the size and spot it has by then.
            for (float waited = 0f; waited < delay; waited += Time.deltaTime)
            {
                shownScale = baseScale;
                Hang(PinPoint(), tilt, 0f, 0f);
                yield return null;
            }

            shownScale = baseScale;

            // The pin is pushed in from in front of the board.
            for (float time = 0f; time < pinSeconds; time += Time.deltaTime)
            {
                float u = time / pinSeconds;
                FollowSize();
                pin.Place(PinPoint(), Mathf.Lerp(1.7f, 1f, u * u), Mathf.Clamp01(u * 3f));
                yield return null;
            }

            pin.Place(PinPoint(), 1f, 1f);
            PosterSpecks.Crumbs(layer, PinPoint(), Size.x);

            // Unrolls down from the pin while swinging, and the swing dies out.
            for (float time = 0f; time < swingSeconds; time += Time.deltaTime)
            {
                float unroll = BackOut(Mathf.Clamp01(time / unrollSeconds));
                FollowSize();
                float u = time / swingSeconds;
                float swing = side * swingDegrees * Mathf.Cos(u * Mathf.PI * 3f) * (1f - u) * (1f - u);
                Hang(PinPoint(), tilt + swing, unroll, 1f);
                pin.Place(PinPoint(), 1f, 1f);
                yield return null;
            }

            shownScale = baseScale;
            Hang(PinPoint(), tilt, 1f, 1f);
            pin.Place(PinPoint(), 1f, 1f);
            playing = null;
        }

        // A poster going up while the row grows or shrinks eases to the row's new size as it swings,
        // instead of hanging on at the size it started at (and covering its neighbours).
        private void FollowSize()
        {
            shownScale = Vector3.MoveTowards(shownScale, baseScale, fullScale.magnitude * Time.deltaTime / slideSeconds);
        }

        private IEnumerator SlideRoutine(Vector2 from, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            // Swings a little behind the move, as if carried by its pin.
            Vector2 to = home;
            Vector3 fromScale = shownScale;
            float lean = Mathf.Clamp((from.x - to.x) * 0.05f, -7f, 7f);
            for (float time = 0f; time < slideSeconds; time += Time.deltaTime)
            {
                float u = time / slideSeconds;
                shownScale = Vector3.Lerp(fromScale, baseScale, Mathf.SmoothStep(0f, 1f, u));
                // Toward home as it is now: the row may change again while it glides.
                Vector2 point = PinPoint(Vector2.Lerp(from, home, 1f - Mathf.Pow(1f - u, 3f)));
                Hang(point, tilt + lean * Mathf.Sin(u * Mathf.PI), 1f, 1f);
                pin.Place(point, 1f, 1f);
                yield return null;
            }

            shownScale = baseScale;
            Hang(PinPoint(), tilt, 1f, 1f);
            pin.Place(PinPoint(), 1f, 1f);
            playing = null;
        }

        private IEnumerator CaughtRoutine()
        {
            Vector2 point = PinPoint();

            // The stamp comes down hard from above the board.
            for (float time = 0f; time < stampSeconds; time += Time.deltaTime)
            {
                float u = time / stampSeconds;
                stamp.Pose(Mathf.Lerp(2.4f, 1f, u * u), u);
                yield return null;
            }

            stamp.Pose(1f, 1f);
            PosterSpecks.Ink(layer, layer.InverseTransformPoint(stamp.transform.position), stampColor, Size.x);

            // The poster jolts under the hit.
            const float joltSeconds = 0.18f;
            for (float time = 0f; time < joltSeconds; time += Time.deltaTime)
            {
                float u = time / joltSeconds;
                float jolt = Mathf.Sin(u * Mathf.PI * 3f) * (1f - u);
                Hang(point, tilt + jolt * 2.5f, 1f - Mathf.Abs(jolt) * 0.03f, 1f);
                yield return null;
            }

            Hang(point, tilt, 1f, 1f);
            if (stampHoldSeconds > 0f) yield return new WaitForSeconds(stampHoldSeconds);

            yield return DropRoutine();
        }

        // The pin pops out and flies off; the poster swings round on its corner and drops away.
        private IEnumerator DropRoutine()
        {
            Vector2 point = PinPoint();
            Vector2 centre = rect.anchoredPosition;
            float side = Random.value < 0.5f ? -1f : 1f;
            float fall = Size.y * 1.3f;
            for (float time = 0f; time < dropSeconds; time += Time.deltaTime)
            {
                float u = time / dropSeconds;
                pin.Place(point + new Vector2(side * 0.35f, 0.5f) * (Size.x * u), 1f + 0.3f * u, 1f - u * 2f, side * -200f * u);

                Vector2 position = centre + new Vector2(side * Size.x * 0.25f * u, -fall * u * u);
                float degrees = tilt + side * 28f * u * u;
                float alpha = u < 0.55f ? 1f : 1f - (u - 0.55f) / 0.45f;
                Pose(position, degrees, 1f, alpha);
                yield return null;
            }

            playing = null;
            HideNow();
        }

        private Vector2 PinPoint()
        {
            return PinPoint(home);
        }

        // The pin sits near the top middle of a poster hanging at its tilt with its centre at centre.
        private Vector2 PinPoint(Vector2 centre)
        {
            return centre + Rotate(Vector2.up * PinToCentre, tilt);
        }

        // Hangs the poster from the pin turned by degrees, unrolled to the given part of its height.
        private void Hang(Vector2 pinPoint, float degrees, float unrolled, float alpha)
        {
            Pose(pinPoint + Rotate(Vector2.down * (PinToCentre * unrolled), degrees), degrees, unrolled, alpha);
        }

        private void Pose(Vector2 position, float degrees, float unrolled, float alpha)
        {
            rect.anchoredPosition = position;
            rect.localRotation = Quaternion.Euler(0f, 0f, degrees);
            rect.localScale = new Vector3(shownScale.x, shownScale.y * unrolled, shownScale.z);
            group.alpha = alpha;
        }

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            return Quaternion.Euler(0f, 0f, degrees) * vector;
        }

        // 0 to 1, running a little past 1 before settling, like paper springing open.
        private static float BackOut(float t)
        {
            const float s = 1.2f;
            float u = t - 1f;
            return 1f + u * u * ((s + 1f) * u + s);
        }
    }
}
