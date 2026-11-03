using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Pigeon feathers falling over the wall in the boss's cut-in, on a box stretched over the wall in
    // Assets/Prefabs/Boss/Boss Transition.prefab. Its Feather child is the picture every feather is
    // copied from, switched off, at a feather's usual size (resize it or swap its picture there).
    // While it is falling, feathers keep coming in above the wall at random across it and drift down
    // past the bottom, swaying and turning, cut off at the wall's edges (a RectMask2D on the box), so
    // none shows on another screen. A burst (Burst) throws a handful out from one spot, spreading
    // sideways as they fall from there. Runs on unscaled time, so it plays while the game is frozen.
    public sealed class BossFeatherFall : MonoBehaviour
    {
        [SerializeField] private Image feather;
        [SerializeField, Min(0.1f)] private float perSecond = 5f;
        // Each feather is between these times its picture's size, and takes between these seconds to
        // fall from the top of the wall to the bottom.
        [SerializeField] private Vector2 sizeRange = new Vector2(0.7f, 1.3f);
        [SerializeField] private Vector2 fallSeconds = new Vector2(1.6f, 2.6f);
        // How far it sways either way (wall units) and how many sways a second.
        [SerializeField, Min(0f)] private float sway = 45f;
        [SerializeField, Min(0f)] private float swaysPerSecond = 0.7f;
        // How fast it turns at most (degrees a second), either way.
        [SerializeField, Min(0f)] private float spin = 120f;
        // How far a burst's feathers spread out sideways (wall units) as they fall.
        [SerializeField, Min(0f)] private float burstSpread = 220f;

        private sealed class Falling
        {
            public RectTransform Rect;
            public float Born;
            public float Seconds;
            // Where it starts falling from (null: just above the top), and how far it drifts sideways.
            public float? StartY;
            public float Drift;
            public float X;
            public float Phase;
            public float Turn;
            public float SpinRate;
        }

        private readonly List<Falling> falling = new List<Falling>();
        private bool spawning;
        private float owed;

        // True once every feather has fallen out.
        public bool IsEmpty => falling.Count == 0;

        private void Awake()
        {
            if (feather != null) feather.gameObject.SetActive(false);
            // Only what is on the wall shows: a feather drifting in above it or out past its bottom
            // is cut off at the edge, never showing on the floor's screen next to it.
            if (!TryGetComponent(out RectMask2D _)) gameObject.AddComponent<RectMask2D>();
        }

        // Feathers start coming in over the top of the wall.
        public void StartFalling()
        {
            spawning = true;
            owed = 0f;
        }

        // count feathers thrown out from worldPoint at once, falling from there.
        public void Burst(Vector3 worldPoint, int count)
        {
            if (feather == null) return;

            Vector2 from = ((RectTransform)transform).InverseTransformPoint(worldPoint);
            for (int i = 0; i < count; i++) Spawn(from);
        }

        // No more feathers come in; those in the air fall on out.
        public void StopFalling()
        {
            spawning = false;
        }

        // Takes every feather off the wall at once.
        public void Clear()
        {
            spawning = false;
            foreach (Falling one in falling)
            {
                if (one.Rect != null) Destroy(one.Rect.gameObject);
            }

            falling.Clear();
        }

        private void Update()
        {
            if (feather == null) return;

            if (spawning)
            {
                owed += perSecond * Time.unscaledDeltaTime;
                for (; owed >= 1f; owed -= 1f) Spawn();
            }

            Rect box = ((RectTransform)transform).rect;
            for (int i = falling.Count - 1; i >= 0; i--)
            {
                if (Pose(falling[i], box, Time.unscaledTime)) continue;

                Destroy(falling[i].Rect.gameObject);
                falling.RemoveAt(i);
            }
        }

        // Puts a feather where it is now; false once it has fallen past the bottom.
        private bool Pose(Falling one, Rect box, float now)
        {
            float age = now - one.Born;
            float u = age / one.Seconds;
            if (u >= 1f) return false;

            // From just above the top to just below the bottom, however big and turned it is.
            float margin = Mathf.Max(one.Rect.rect.width, one.Rect.rect.height) * Mathf.Abs(one.Rect.localScale.y);
            float top = box.height * 0.5f + margin;
            float swing = Mathf.Sin(one.Phase + age * swaysPerSecond * Mathf.PI * 2f);
            // A burst's feather flies out sideways fast at first, then drifts.
            float drift = one.Drift * (1f - (1f - u) * (1f - u) * (1f - u));
            float start = one.StartY ?? top;
            one.Rect.anchoredPosition = new Vector2(one.X + drift + sway * swing, Mathf.Lerp(start, -top, u));
            // Leans into each sway, as a falling feather does, and turns slowly on top of that.
            one.Rect.localRotation = Quaternion.Euler(0f, 0f, one.Turn + one.SpinRate * age + 25f * swing);
            return true;
        }

        // A new feather just above the top of the wall.
        private void Spawn()
        {
            Spawn(null);
        }

        // A new feather at from (spreading out sideways from there), or just above the top of the
        // wall without one.
        private void Spawn(Vector2? from)
        {
            Rect box = ((RectTransform)transform).rect;
            Image copy = Instantiate(feather, transform);
            copy.name = "Feather (falling)";
            copy.gameObject.SetActive(true);
            RectTransform rect = copy.rectTransform;
            rect.localScale = feather.rectTransform.localScale * Random.Range(sizeRange.x, sizeRange.y);

            float seconds = Random.Range(fallSeconds.x, fallSeconds.y);
            // From part way down it has less far to fall, so it takes less time.
            if (from.HasValue) seconds *= Mathf.Clamp(0.5f + from.Value.y / Mathf.Max(1f, box.height), 0.3f, 1f);
            var one = new Falling
            {
                Rect = rect,
                Born = Time.unscaledTime,
                Seconds = seconds,
                StartY = from?.y,
                Drift = from.HasValue ? Random.Range(-burstSpread, burstSpread) : 0f,
                X = from?.x ?? Random.Range(-box.width * 0.5f, box.width * 0.5f),
                Phase = Random.Range(0f, Mathf.PI * 2f),
                Turn = Random.Range(0f, 360f),
                SpinRate = Random.Range(-spin, spin),
            };
            falling.Add(one);
            Pose(one, box, Time.unscaledTime);
        }
    }
}
