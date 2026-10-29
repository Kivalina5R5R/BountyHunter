using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The red dots that pop up at random over the blue streak in the boss's cut-in, on a box the same
    // place and size as the streak in Assets/Prefabs/Boss/Boss Transition.prefab. Its Dot child is the
    // picture every dot is copied from, switched off, at a dot's biggest size (recolour it there).
    // While it is popping, dots keep appearing somewhere in the box, only in the part of it the
    // streak has reached so far from the side it comes in from (SetReach), each popping up, staying a
    // moment and popping away.
    // Runs on unscaled time, so it plays while the game is frozen.
    public sealed class BossDotPops : MonoBehaviour
    {
        [SerializeField] private Image dot;
        [SerializeField, Min(0.1f)] private float perSecond = 10f;
        // Each dot is between these times its picture's size.
        [SerializeField] private Vector2 sizeRange = new Vector2(0.2f, 1f);
        // How far up and down the box dots appear, as a share of its height from its middle.
        [SerializeField, Range(0f, 0.5f)] private float band = 0.35f;
        [SerializeField, Min(0.01f)] private float popInSeconds = 0.18f;
        // How long a dot stays, picked between these at random.
        [SerializeField] private Vector2 staySeconds = new Vector2(0.4f, 1.2f);
        [SerializeField, Min(0.01f)] private float popOutSeconds = 0.15f;
        // The streak comes in from the right (off: from the left).
        [SerializeField] private bool fromRight = true;

        private sealed class Popping
        {
            public RectTransform Rect;
            public Vector3 Size;
            public float Born;
            public float LeavesAt;
        }

        private readonly List<Popping> dots = new List<Popping>();
        private bool spawning;
        private float owed;
        private float reach = 1f;

        private void Awake()
        {
            if (dot != null) dot.gameObject.SetActive(false);
        }

        public void StartPopping()
        {
            spawning = true;
            owed = 0f;
        }

        // 0 to 1: how far across the box, from the side the streak comes in from, new dots may appear.
        public void SetReach(float share)
        {
            reach = Mathf.Clamp01(share);
        }

        // No more dots come, and those up pop away now.
        public void PopAll()
        {
            spawning = false;
            float now = Time.unscaledTime;
            foreach (Popping one in dots) one.LeavesAt = Mathf.Min(one.LeavesAt, now);
        }

        // Takes every dot off at once.
        public void Clear()
        {
            spawning = false;
            foreach (Popping one in dots)
            {
                if (one.Rect != null) Destroy(one.Rect.gameObject);
            }

            dots.Clear();
        }

        private void Update()
        {
            if (dot == null) return;

            if (spawning && reach > 0f)
            {
                owed += perSecond * Time.unscaledDeltaTime;
                for (; owed >= 1f; owed -= 1f) Spawn();
            }

            float now = Time.unscaledTime;
            for (int i = dots.Count - 1; i >= 0; i--)
            {
                Popping one = dots[i];
                float leaving = (now - one.LeavesAt) / popOutSeconds;
                if (leaving >= 1f)
                {
                    Destroy(one.Rect.gameObject);
                    dots.RemoveAt(i);
                    continue;
                }

                float size = leaving > 0f ? 1f - leaving : EaseOutBack((now - one.Born) / popInSeconds);
                one.Rect.localScale = one.Size * size;
            }
        }

        private void Spawn()
        {
            Rect box = ((RectTransform)transform).rect;
            Image copy = Instantiate(dot, transform);
            copy.name = "Dot (popping)";
            copy.gameObject.SetActive(true);
            RectTransform rect = copy.rectTransform;
            float across = Random.Range(0f, box.width * reach);
            float x = fromRight ? box.width * 0.5f - across : across - box.width * 0.5f;
            rect.anchoredPosition = new Vector2(x, Random.Range(-band, band) * box.height);
            rect.localScale = Vector3.zero;

            float now = Time.unscaledTime;
            dots.Add(new Popping
            {
                Rect = rect,
                Size = dot.rectTransform.localScale * Random.Range(sizeRange.x, sizeRange.y),
                Born = now,
                LeavesAt = now + popInSeconds + Random.Range(staySeconds.x, staySeconds.y),
            });
        }

        private static float EaseOutBack(float u)
        {
            const float overshoot = 1.70158f;
            float back = Mathf.Clamp01(u) - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }
    }
}
