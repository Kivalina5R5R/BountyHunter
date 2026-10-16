using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // A big 3, 2, 1 in the middle of the floor before the game starts. Each number falls onto the
    // floor from high up (big, shrinking faster and faster), squashes as it lands, bounces back to its
    // size, holds, then fades; after the last one the given callback runs. The number is a child in
    // the scene, so its font, colour, size and place are set there; this only animates it.
    public sealed class FloorCountdown : MonoBehaviour
    {
        [SerializeField] private TMP_Text number;
        [SerializeField, Min(1)] private int from = 3;
        [SerializeField, Min(0.3f)] private float stepSeconds = 1f;
        // A pause before the 3, so the last pick's stamp and marker finish first.
        [SerializeField, Min(0f)] private float startDelay = 0.7f;
        // How big the number starts, high above the floor, and how much of each step the fall takes.
        [SerializeField, Min(1f)] private float dropFromScale = 3f;
        [SerializeField, Range(0.05f, 0.6f)] private float fallPart = 0.25f;
        // How much it squashes on landing.
        [SerializeField, Range(0f, 0.4f)] private float landSquash = 0.15f;

        private Vector3 numberScale;
        private Coroutine running;

        public bool Running => running != null;

        private void Awake()
        {
            if (number != null) numberScale = number.transform.localScale;
            Show(false);
        }

        // Counts down, then calls done. Does nothing if already counting.
        public void Begin(Action done)
        {
            if (running != null) return;

            running = StartCoroutine(Count(done));
            Debug.Log("[FloorCountdown] Everyone has picked: counting down", this);
        }

        public void Cancel()
        {
            if (running == null) return;

            StopCoroutine(running);
            running = null;
            Show(false);
            Debug.Log("[FloorCountdown] Count stopped: a player still has to pick", this);
        }

        private IEnumerator Count(Action done)
        {
            if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

            for (int n = from; n >= 1; n--)
            {
                if (number != null) number.text = n.ToString();
                Show(true);
                for (float time = 0f; time < stepSeconds; time += Time.deltaTime)
                {
                    Pose(time / stepSeconds);
                    yield return null;
                }
            }

            Show(false);
            running = null;
            done?.Invoke();
        }

        // One number's life, t from 0 to 1: falls in, lands with a squash and bounce, holds, fades.
        private void Pose(float t)
        {
            if (number == null) return;

            const float bounceEnd = 0.5f;
            const float fadeFrom = 0.8f;
            var scale = Vector3.one;
            float alpha = 1f;
            if (t < fallPart)
            {
                // Falling: shrinks faster and faster, fading in from the top of the fall.
                float u = t / fallPart;
                scale *= Mathf.Lerp(dropFromScale, 1f, u * u);
                alpha = Mathf.Clamp01(u * 3f);
            }
            else if (t < bounceEnd)
            {
                // Landed: squashes flat, springs up a little, settles.
                float u = (t - fallPart) / (bounceEnd - fallPart);
                float spring = Mathf.Cos(u * Mathf.PI * 2.5f) * (1f - u) * (1f - u) * landSquash;
                scale = new Vector3(1f + spring, 1f - spring, 1f);
            }
            else if (t > fadeFrom) alpha = 1f - (t - fadeFrom) / (1f - fadeFrom);

            number.transform.localScale = Vector3.Scale(numberScale, scale);
            number.alpha = alpha;
        }

        private void Show(bool visible)
        {
            if (number != null) number.enabled = visible;
        }
    }
}
