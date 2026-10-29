using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Pigeon feathers falling over the wall in the boss's cut-in, on a box stretched over the wall in
    // Assets/Prefabs/Boss/Boss Transition.prefab. Its Feather child is the picture every feather is
    // copied from, switched off, at a feather's usual size (resize it or swap its picture there).
    // While it is falling, feathers keep coming in above the wall at random across it and drift down
    // past the bottom, swaying and turning. Runs on unscaled time, so it plays while the game is frozen.
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

        private sealed class Falling
        {
            public RectTransform Rect;
            public float Born;
            public float Seconds;
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
        }

        // Feathers start coming in over the top of the wall.
        public void StartFalling()
        {
            spawning = true;
            owed = 0f;
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
            one.Rect.anchoredPosition = new Vector2(one.X + sway * swing, Mathf.Lerp(top, -top, u));
            // Leans into each sway, as a falling feather does, and turns slowly on top of that.
            one.Rect.localRotation = Quaternion.Euler(0f, 0f, one.Turn + one.SpinRate * age + 25f * swing);
            return true;
        }

        // A new feather just above the top of the wall.
        private void Spawn()
        {
            Rect box = ((RectTransform)transform).rect;
            Image copy = Instantiate(feather, transform);
            copy.name = "Feather (falling)";
            copy.gameObject.SetActive(true);
            RectTransform rect = copy.rectTransform;
            rect.localScale = feather.rectTransform.localScale * Random.Range(sizeRange.x, sizeRange.y);

            float seconds = Random.Range(fallSeconds.x, fallSeconds.y);
            var one = new Falling
            {
                Rect = rect,
                Born = Time.unscaledTime,
                Seconds = seconds,
                X = Random.Range(-box.width * 0.5f, box.width * 0.5f),
                Phase = Random.Range(0f, Mathf.PI * 2f),
                Turn = Random.Range(0f, 360f),
                SpinRate = Random.Range(-spin, spin),
            };
            falling.Add(one);
            Pose(one, box, Time.unscaledTime);
        }
    }
}
