using UnityEngine;

namespace BountyHunter.Thieves
{
    // The countdown's sounds, the way a race starts: a short beep as each of 3, 2, 1 lands, and a
    // long, brighter beep for GO! A count stopped part way cuts its sound off. Sits next to the
    // FloorCountdown.
    [RequireComponent(typeof(FloorCountdown))]
    public sealed class FloorCountdownSounds : MonoBehaviour
    {
        // The sound for 1, 2, 3, ... in that order.
        [SerializeField] private AudioClip[] numbers;
        [SerializeField] private AudioClip go;
        [SerializeField, Range(0f, 1f)] private float volume = 0.85f;

        private FloorCountdown countdown;
        private AudioSource source;

        private void Awake()
        {
            countdown = GetComponent<FloorCountdown>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            countdown.NumberShown += OnNumberShown;
            countdown.GoShown += OnGoShown;
            countdown.Cancelled += OnCancelled;
        }

        private void OnDisable()
        {
            countdown.NumberShown -= OnNumberShown;
            countdown.GoShown -= OnGoShown;
            countdown.Cancelled -= OnCancelled;
        }

        private void OnNumberShown(int number)
        {
            if (numbers != null && number >= 1 && number <= numbers.Length) Play(numbers[number - 1]);
        }

        private void OnGoShown()
        {
            Play(go);
        }

        private void OnCancelled()
        {
            source.Stop();
        }

        private void Play(AudioClip clip)
        {
            if (clip != null) source.PlayOneShot(clip, volume);
        }
    }
}
