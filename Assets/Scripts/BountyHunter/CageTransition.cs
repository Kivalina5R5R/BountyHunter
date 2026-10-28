using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The jail cage that shuts over the wall between the intro and the first map, laid out in
    // Assets/Prefabs/Transition/Cage Transition.prefab (move, resize or recolour anything there):
    // the two halves of a sliding cell door speed in from the sides while the cell goes dark behind
    // them, slam together in the middle (the wall shakes, the doors bounce back a little); it holds
    // while the first map is set up behind it, then the doors slide back out, the dark lifting with
    // them. A padlock (optional, none in the prefab now) drops onto the join and pops off again. Runs on
    // unscaled time. IntroSequence drives it: Close until the wall is covered, Open once the map is up.
    public sealed class CageTransition : WallTransition
    {
        [Header("Pieces")]
        // Holds the doors and the padlock; it shakes as they slam.
        [SerializeField] private RectTransform shaker;
        // Dark behind the bars, so what changes behind the cage does not show through them.
        [SerializeField] private Image shade;
        [SerializeField] private RectTransform leftDoor;
        [SerializeField] private RectTransform rightDoor;
        [SerializeField] private RectTransform padlock;

        [Header("Timing (seconds)")]
        [SerializeField, Min(0.05f)] private float slideSeconds = 0.45f;
        [SerializeField, Min(0.05f)] private float bounceSeconds = 0.14f;
        [SerializeField, Min(0.05f)] private float dropSeconds = 0.4f;
        // The least time the cage stays shut once the padlock has landed.
        [SerializeField, Min(0f)] private float holdSeconds = 0.5f;
        [SerializeField, Min(0.05f)] private float unlockSeconds = 0.2f;
        [SerializeField, Min(0.05f)] private float openSeconds = 0.5f;

        [Header("Motion")]
        // How dark the cell gets behind the bars (its alpha).
        [SerializeField, Range(0f, 1f)] private float shadeAlpha = 1f;
        // How far the doors bounce back after the slam, and how hard the wall shakes (wall units).
        [SerializeField, Min(0f)] private float bounce = 14f;
        [SerializeField, Min(0f)] private float shake = 12f;
        [SerializeField, Min(0.05f)] private float shakeSeconds = 0.25f;
        // How high over its spot the padlock starts its drop.
        [SerializeField, Min(0f)] private float dropHeight = 320f;

        private Vector2 leftRest;
        private Vector2 rightRest;
        private Vector2 padlockRest;
        private Vector3 padlockSize;
        private float shutAt;
        private bool measured;

        // True once the prefab has its doors (Tools > Bounty Hunter > Build Cage Transition).
        public override bool IsSetUp => leftDoor != null && rightDoor != null;

        // Slams the cage shut over the wall and drops the padlock on it.
        public override IEnumerator Close()
        {
            gameObject.SetActive(true);
            Measure();
            float start = Time.unscaledTime;
            float length = slideSeconds + Mathf.Max(dropSeconds, Mathf.Max(bounceSeconds, shakeSeconds));
            while (Time.unscaledTime - start < length)
            {
                PoseClosing(Time.unscaledTime - start);
                yield return null;
            }

            PoseClosing(length);
            shutAt = Time.unscaledTime;
        }

        public override IEnumerator Hold()
        {
            while (Time.unscaledTime < shutAt + holdSeconds) yield return null;
        }

        // The padlock's spring (when there is one) and the doors sliding out.
        public override float OpenSeconds => UnlockDelay + openSeconds;

        // Waits out the rest of the hold, then unlocks the cage and slides it open.
        public override IEnumerator Open()
        {
            yield return Hold();

            float start = Time.unscaledTime;
            float length = OpenSeconds;
            while (Time.unscaledTime - start < length)
            {
                PoseOpening(Time.unscaledTime - start);
                yield return null;
            }

            gameObject.SetActive(false);
        }

        // The doors wait for the padlock to spring open; with no padlock they go at once.
        private float UnlockDelay => padlock != null ? unlockSeconds * 0.5f : 0f;

        private void Measure()
        {
            if (measured) return;

            measured = true;
            leftRest = leftDoor.anchoredPosition;
            rightRest = rightDoor.anchoredPosition;
            if (padlock == null) return;
            padlockRest = padlock.anchoredPosition;
            padlockSize = padlock.localScale;
        }

        private void PoseClosing(float time)
        {
            // The doors speed up all the way in, as a heavy door does, then bounce back off each other.
            float slide = Mathf.Clamp01(time / slideSeconds);
            float since = time - slideSeconds;
            float recoil = since > 0f && since < bounceSeconds ? bounce * Mathf.Sin(Mathf.PI * since / bounceSeconds) : 0f;
            SetDoors(1f - slide * slide * slide, recoil);
            SetShade(shadeAlpha * Smooth(slide));
            Shake(since > 0f && since < shakeSeconds ? shake * (1f - since / shakeSeconds) : 0f);

            if (padlock == null) return;
            padlock.gameObject.SetActive(since > 0f);
            float drop = EaseOutBounce(since / dropSeconds);
            padlock.anchoredPosition = padlockRest + new Vector2(0f, dropHeight * (1f - drop));
            padlock.localScale = padlockSize;
            padlock.localRotation = Quaternion.Euler(0f, 0f, 8f * (1f - drop) * Mathf.Sin(since * 30f));
        }

        private void PoseOpening(float time)
        {
            Shake(0f);
            if (padlock != null)
            {
                // A quick swell as it springs open, then gone.
                float unlock = Mathf.Clamp01(time / unlockSeconds);
                padlock.gameObject.SetActive(unlock < 1f);
                padlock.localScale = padlockSize * (unlock < 0.5f ? 1f + 0.5f * unlock : 1.25f * (1f - (unlock - 0.5f) * 2f));
            }

            float open = Mathf.Clamp01((time - UnlockDelay) / openSeconds);
            float eased = 1f - (1f - open) * (1f - open) * (1f - open);
            SetDoors(eased, 0f);
            SetShade(shadeAlpha * (1f - Smooth(open)));
        }

        // apart: 0 shut, 1 fully off the wall; back: how far each door bounces back from the join.
        private void SetDoors(float apart, float back)
        {
            float off = leftDoor.rect.width;
            leftDoor.anchoredPosition = leftRest - new Vector2(off * apart + back, 0f);
            rightDoor.anchoredPosition = rightRest + new Vector2(rightDoor.rect.width * apart + back, 0f);
        }

        private void SetShade(float alpha)
        {
            if (shade == null) return;

            Color colour = shade.color;
            colour.a = alpha;
            shade.color = colour;
            shade.enabled = alpha > 0f;
        }

        private void Shake(float amount)
        {
            if (shaker == null) return;
            shaker.anchoredPosition = amount > 0f ? Random.insideUnitCircle * amount : Vector2.zero;
        }

        private static float Smooth(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        private static float EaseOutBounce(float u)
        {
            u = Mathf.Clamp01(u);
            const float n = 7.5625f;
            const float d = 2.75f;
            if (u < 1f / d) return n * u * u;
            if (u < 2f / d) return n * (u -= 1.5f / d) * u + 0.75f;
            if (u < 2.5f / d) return n * (u -= 2.25f / d) * u + 0.9375f;
            return n * (u -= 2.625f / d) * u + 0.984375f;
        }
    }
}
