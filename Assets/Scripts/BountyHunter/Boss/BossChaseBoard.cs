using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The boss chase's board on the wall, laid out in Assets/Prefabs/Boss/Boss Wall.prefab (move,
    // resize or restyle anything there): a dark brush board reading BOSS CHASE! with the catches so
    // far (2/3) and a pair of handcuffs for each catch needed, grey until it is made. It springs up as
    // the chase starts. Each catch snaps the next handcuffs shut: they spin in blue with a burst of
    // stars and sparks and the count bumps. While it waits, the handcuffs caught rock gently and the
    // next empty pair beats, so everyone sees what is left. BossChase drives it. Runs on unscaled time.
    public sealed class BossChaseBoard : MonoBehaviour
    {
        [SerializeField] private RectTransform board;
        [SerializeField] private TMP_Text count;
        // {0} catches so far, {1} catches needed.
        [SerializeField] private string countFormat = "{0}/{1}";
        // Each pair of handcuffs once caught, over its grey empty one; one per catch needed.
        [SerializeField] private RectTransform[] caughtCuffs = new RectTransform[0];
        [SerializeField] private RectTransform[] emptyCuffs = new RectTransform[0];
        // The stars and sparks bursting off a pair as it snaps shut; may be empty.
        [SerializeField] private CutInParticles particles;
        [SerializeField] private Color[] burstColours =
        {
            new Color(0.25f, 0.55f, 1f, 1f), Color.white, new Color(1f, 0.85f, 0.2f, 1f),
        };

        [Header("Motion")]
        [SerializeField, Min(0.05f)] private float showSeconds = 0.45f;
        [SerializeField, Min(0.05f)] private float snapSeconds = 0.45f;
        [SerializeField, Min(0.05f)] private float bumpSeconds = 0.35f;
        [SerializeField, Range(0f, 30f)] private float rockDegrees = 6f;
        [SerializeField, Range(0f, 0.5f)] private float beat = 0.12f;

        private Vector3 boardSize;
        private Vector3[] caughtSizes;
        private Vector3[] emptySizes;
        private Vector3 countSize;
        private bool measured;
        private int caught;
        private int needed;
        private float shownAt = float.NegativeInfinity;
        private float snappedAt = float.NegativeInfinity;
        private int snapping = -1;

        // Puts the board up for a chase needing catches, caughtSoFar of them made already.
        public void Show(int caughtSoFar, int catches)
        {
            gameObject.SetActive(true);
            Measure();
            needed = catches;
            caught = Mathf.Clamp(caughtSoFar, 0, catches);
            snapping = -1;
            shownAt = Time.unscaledTime;
            WriteCount();
        }

        // One more catch: the next handcuffs snap shut.
        public void SetCaught(int caughtSoFar)
        {
            int now = Mathf.Clamp(caughtSoFar, 0, needed);
            if (now <= caught) return;

            caught = now;
            snapping = caught - 1;
            snappedAt = Time.unscaledTime;
            WriteCount();
            if (particles != null && snapping < caughtCuffs.Length && caughtCuffs[snapping] != null)
            {
                Vector2 at = particles.FromWorld(caughtCuffs[snapping].position);
                particles.Ring(at, 160f, 0.4f, Color.white);
                particles.Burst(at, 22, 420f, 9f, 0.7f, burstColours, 0.4f, 300f);
            }
        }

        private void Measure()
        {
            if (measured) return;

            measured = true;
            if (board != null) boardSize = board.localScale;
            if (count != null) countSize = count.rectTransform.localScale;
            caughtSizes = Sizes(caughtCuffs);
            emptySizes = Sizes(emptyCuffs);
        }

        private void Update()
        {
            if (!measured) return;

            float now = Time.unscaledTime;
            if (board != null) board.localScale = boardSize * EaseOutBack((now - shownAt) / showSeconds);

            float bump = Mathf.Clamp01((now - snappedAt) / bumpSeconds);
            if (count != null) count.rectTransform.localScale = countSize * (1f + 0.4f * Mathf.Sin(bump * Mathf.PI) * (1f - bump));

            for (int i = 0; i < caughtCuffs.Length; i++)
            {
                RectTransform cuffs = caughtCuffs[i];
                if (cuffs == null) continue;

                bool isCaught = i < caught;
                cuffs.gameObject.SetActive(isCaught);
                if (!isCaught) continue;

                if (i == snapping && now - snappedAt < snapSeconds)
                {
                    // Spins in from nothing, past its size, and settles.
                    float u = (now - snappedAt) / snapSeconds;
                    cuffs.localScale = caughtSizes[i] * EaseOutBack(u);
                    cuffs.localRotation = Quaternion.Euler(0f, 0f, 360f * (1f - EaseOutCubic(u)));
                    continue;
                }

                cuffs.localScale = caughtSizes[i];
                cuffs.localRotation = Quaternion.Euler(0f, 0f, rockDegrees * Mathf.Sin(now * 2.2f + i * 1.3f));
            }

            // The next pair still to catch beats; the rest wait still.
            for (int i = 0; i < emptyCuffs.Length; i++)
            {
                if (emptyCuffs[i] == null) continue;
                float beating = i == caught ? 1f + beat * Mathf.Max(0f, Mathf.Sin(now * Mathf.PI * 2f * 1.2f)) : 1f;
                emptyCuffs[i].localScale = emptySizes[i] * beating;
            }
        }

        private void WriteCount()
        {
            if (count != null) count.text = string.Format(countFormat, caught, needed);
        }

        private static Vector3[] Sizes(RectTransform[] pieces)
        {
            var sizes = new Vector3[pieces.Length];
            for (int i = 0; i < pieces.Length; i++) sizes[i] = pieces[i] != null ? pieces[i].localScale : Vector3.one;
            return sizes;
        }

        private static float EaseOutCubic(float u)
        {
            float rest = 1f - Mathf.Clamp01(u);
            return 1f - rest * rest * rest;
        }

        // Runs a little past 1 before settling on it; nothing before it starts.
        private static float EaseOutBack(float u)
        {
            if (u <= 0f) return 0f;
            const float overshoot = 1.70158f;
            float back = Mathf.Clamp01(u) - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }
    }
}
