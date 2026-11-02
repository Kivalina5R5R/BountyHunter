using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // A big 3, 2, 1, GO! in the middle of the floor before the game starts. Each falls onto the floor
    // from high up (big, shrinking faster and faster), squashes as it lands, bounces back to its size,
    // holds, then fades; after the GO the given callback runs. The number is a child in
    // the scene, so its font, colour, size and place are set there; this only animates it.
    public sealed class FloorCountdown : MonoBehaviour
    {
        [SerializeField] private TMP_Text number;
        [SerializeField, Min(1)] private int from = 3;
        [SerializeField, Min(0.3f)] private float stepSeconds = 1f;
        // Shown after the 1, the same way; leave empty for none.
        [SerializeField] private string goText = "GO!";
        // A pause after everyone has picked, before the 3 comes in.
        [SerializeField, Min(0f)] private float startDelay = 1f;
        // How big the number starts, high above the floor, and how much of each step the fall takes.
        [SerializeField, Min(1f)] private float dropFromScale = 3f;
        [SerializeField, Range(0.05f, 0.6f)] private float fallPart = 0.25f;
        // How much it squashes on landing.
        [SerializeField, Range(0f, 0.4f)] private float landSquash = 0.15f;

        private Vector3 numberScale;
        private Coroutine running;

        public bool Running => running != null;

        // For its sounds (FloorCountdownSounds): a number starts falling (3, 2, 1), the GO does, or
        // the count was stopped part way.
        public event Action<int> NumberShown;
        public event Action GoShown;
        public event Action Cancelled;

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
            Cancelled?.Invoke();
            Debug.Log("[FloorCountdown] Count stopped: a player still has to pick", this);
        }

        private IEnumerator Count(Action done)
        {
            if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

            for (int n = from; n >= 1; n--)
            {
                NumberShown?.Invoke(n);
                yield return Drop(n.ToString());
            }

            if (!string.IsNullOrEmpty(goText))
            {
                GoShown?.Invoke();
                yield return Drop(goText);
            }

            Show(false);
            running = null;
            done?.Invoke();
        }

        // One step: the text falls in, lands, holds and fades over stepSeconds.
        private IEnumerator Drop(string text)
        {
            if (number != null) number.text = text;
            Show(true);
            for (float time = 0f; time < stepSeconds; time += Time.deltaTime)
            {
                Pose(time / stepSeconds);
                yield return null;
            }
        }

        // One number's life, t from 0 to 1: falls in, lands with a squash and bounce, holds, fades.
        private void Pose(float t)
        {
            if (number == null) return;

            CountdownDrop.Pose(t, fallPart, dropFromScale, landSquash, out Vector3 scale, out float alpha);
            number.transform.localScale = Vector3.Scale(numberScale, scale);
            number.alpha = alpha;
        }

        private void Show(bool visible)
        {
            if (number != null) number.enabled = visible;
        }
    }
}
