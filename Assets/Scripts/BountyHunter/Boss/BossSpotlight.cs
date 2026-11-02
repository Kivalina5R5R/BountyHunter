using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The boss's entrance on the wall, in the way of a fighting game's character spotlight, over the
    // rest of the boss's cut-in (BossTransition) in Assets/Prefabs/Boss/Boss Transition.prefab:
    // PlayIn darkens the wall, white slashes cut across it and a slanted band slides through with a
    // close-up of the boss's eyes panning in it; as the band shoots off (Revealed) the cut-in behind
    // sets its rooftop and boss up, and the dark lifts to show them. PlayOut sweeps slanted bands of
    // colour across the wall; while they cover it (Covered) the cut-in clears away behind them, and
    // they sweep on off it, showing the chase. Runs on unscaled time.
    public sealed class BossSpotlight : MonoBehaviour
    {
        [Header("Pieces")]
        [SerializeField] private Image veil;
        // Thin white bars, laid across the wall at a slant, growing from their left end.
        [SerializeField] private RectTransform[] slashes = new RectTransform[0];
        // The slanted band with the close-up: it slides in from the right and out to the left, the
        // face panning slowly inside it.
        [SerializeField] private RectTransform eyeBand;
        [SerializeField] private RectTransform eyeFace;
        // Slanted bands of colour sweeping from right to left, the widest last.
        [SerializeField] private RectTransform[] sweepBands = new RectTransform[0];

        [Header("Coming in (seconds)")]
        [SerializeField, Min(0.05f)] private float veilSeconds = 0.25f;
        [SerializeField, Min(0f)] private float slashesAfter = 0.2f;
        [SerializeField, Min(0f)] private float slashGap = 0.08f;
        [SerializeField, Min(0.05f)] private float slashSeconds = 0.3f;
        [SerializeField, Min(0f)] private float bandAfter = 0.5f;
        [SerializeField, Min(0.05f)] private float bandInSeconds = 0.22f;
        [SerializeField, Min(0f)] private float bandHoldSeconds = 0.55f;
        [SerializeField, Min(0.05f)] private float bandOutSeconds = 0.15f;
        // The dark lifting off the rooftop once the band has gone.
        [SerializeField, Min(0.05f)] private float revealSeconds = 0.5f;

        [Header("Going out (seconds)")]
        [SerializeField, Min(0.05f)] private float sweepSeconds = 0.7f;
        [SerializeField, Min(0f)] private float sweepGap = 0.07f;

        [Header("Motion (wall units)")]
        [SerializeField, Range(0f, 1f)] private float veilAlpha = 0.85f;
        [SerializeField, Min(0f)] private float facePan = 60f;

        [Header("Sounds")]
        // The slashes, the rooftop showing, and the bands sweeping across.
        [SerializeField] private TransitionSound slashSound = new TransitionSound(0.55f);
        [SerializeField] private TransitionSound revealSound = new TransitionSound(0.7f);
        [SerializeField] private TransitionSound sweepSound = new TransitionSound(0.5f);

        private float wallWidth;
        private Vector2 bandRest;
        private Vector2 faceRest;
        private Vector3[] slashSizes;
        private Vector2[] sweepRests;
        private bool measured;
        private AudioSource voice;

        public bool IsSetUp => veil != null && eyeBand != null;

        // How long PlayIn takes until the band has gone (the rooftop goes up behind the dark), and
        // until the dark has lifted.
        public float RevealedAt => bandAfter + bandInSeconds + bandHoldSeconds + bandOutSeconds;
        public float InSeconds => RevealedAt + revealSeconds;

        // From the start of PlayOut until the bands cover the whole wall.
        public float CoveredAt => sweepSeconds * 0.5f;
        public float OutSeconds => sweepSeconds + sweepGap * Mathf.Max(0, sweepBands.Length - 1);

        // Plays the entrance; revealed runs once as the band has gone, before the dark lifts.
        public IEnumerator PlayIn(System.Action revealed)
        {
            gameObject.SetActive(true);
            Measure();
            HideSweep();
            bool revealedYet = false;
            int slashesHeard = 0;
            float start = Time.unscaledTime;
            while (true)
            {
                float since = Time.unscaledTime - start;
                PoseIn(since);
                if (slashesHeard < slashes.Length && since >= slashesAfter + slashGap * slashesHeard)
                {
                    if (slashesHeard == 0) slashSound.Play(voice);
                    slashesHeard++;
                }

                if (!revealedYet && since >= RevealedAt)
                {
                    revealedYet = true;
                    revealSound.Play(voice);
                    revealed?.Invoke();
                }

                if (since >= InSeconds) break;
                yield return null;
            }

            PoseIn(InSeconds);
        }

        // Sweeps the bands across; covered runs once while they hide the whole wall.
        public IEnumerator PlayOut(System.Action covered)
        {
            gameObject.SetActive(true);
            Measure();
            sweepSound.Play(voice);
            bool coveredYet = false;
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < OutSeconds)
            {
                float since = Time.unscaledTime - start;
                for (int i = 0; i < sweepBands.Length; i++) PoseSweep(i, (since - sweepGap * i) / sweepSeconds);
                if (!coveredYet && since >= CoveredAt + sweepGap * (sweepBands.Length - 1))
                {
                    coveredYet = true;
                    covered?.Invoke();
                }

                yield return null;
            }

            if (!coveredYet) covered?.Invoke();
            HideSweep();
            gameObject.SetActive(false);
        }

        private void Measure()
        {
            if (measured) return;

            measured = true;
            voice = TransitionSound.SourceOn(gameObject);
            wallWidth = ((RectTransform)transform).rect.width;
            bandRest = eyeBand.anchoredPosition;
            if (eyeFace != null) faceRest = eyeFace.anchoredPosition;
            slashSizes = new Vector3[slashes.Length];
            for (int i = 0; i < slashes.Length; i++) slashSizes[i] = slashes[i].localScale;
            sweepRests = new Vector2[sweepBands.Length];
            for (int i = 0; i < sweepBands.Length; i++) sweepRests[i] = sweepBands[i].anchoredPosition;
        }

        private void PoseIn(float since)
        {
            // The wall darkens, and lifts again off the rooftop once the band has gone.
            float lift = Smooth((since - RevealedAt) / revealSeconds);
            SetAlpha(veil, veilAlpha * Smooth(since / veilSeconds) * (1f - lift));

            // Each slash shoots out from its left end, then thins away.
            for (int i = 0; i < slashes.Length; i++)
            {
                float t = (since - slashesAfter - slashGap * i) / slashSeconds;
                bool shown = t > 0f && t < 1f;
                slashes[i].gameObject.SetActive(shown);
                if (!shown) continue;
                float reach = EaseOutCubic(t / 0.4f);
                float thin = 1f - Smooth((t - 0.5f) / 0.5f);
                slashes[i].localScale = new Vector3(slashSizes[i].x * reach, slashSizes[i].y * thin, 1f);
            }

            // The band slides in from the right, holds while the face pans, and shoots off left.
            float bandIn = (since - bandAfter) / bandInSeconds;
            float bandOut = (since - bandAfter - bandInSeconds - bandHoldSeconds) / bandOutSeconds;
            bool bandShown = bandIn > 0f && bandOut < 1f;
            eyeBand.gameObject.SetActive(bandShown);
            if (bandShown)
            {
                float x = wallWidth * (1f - EaseOutCubic(bandIn)) - wallWidth * 1.4f * EaseInCubic(bandOut);
                eyeBand.anchoredPosition = bandRest + Vector2.right * x;
                float pan = Mathf.Clamp01((since - bandAfter) / (bandInSeconds + bandHoldSeconds + bandOutSeconds));
                if (eyeFace != null) eyeFace.anchoredPosition = faceRest + Vector2.right * (facePan * (0.5f - pan));
            }
        }

        // Band i share t of its way across: from off the right edge to off the left.
        private void PoseSweep(int i, float t)
        {
            RectTransform band = sweepBands[i];
            bool shown = t > 0f && t < 1f;
            band.gameObject.SetActive(shown);
            if (!shown) return;

            float reach = wallWidth * 0.5f + band.rect.width;
            float x = Mathf.Lerp(reach, -reach, EaseInOutQuad(t));
            band.anchoredPosition = sweepRests[i] + Vector2.right * x;
        }

        private void HideSweep()
        {
            foreach (RectTransform band in sweepBands) band.gameObject.SetActive(false);
        }

        private static void SetAlpha(Graphic picture, float alpha)
        {
            if (picture == null) return;
            Color colour = picture.color;
            colour.a = alpha;
            picture.color = colour;
        }

        private static float Smooth(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        private static float EaseOutCubic(float u)
        {
            float rest = 1f - Mathf.Clamp01(u);
            return 1f - rest * rest * rest;
        }

        private static float EaseInCubic(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * u;
        }

        private static float EaseInOutQuad(float u)
        {
            u = Mathf.Clamp01(u);
            return u < 0.5f ? 2f * u * u : 1f - 2f * (1f - u) * (1f - u);
        }
    }
}
