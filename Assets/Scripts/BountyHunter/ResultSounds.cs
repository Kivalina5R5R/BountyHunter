using UnityEngine;

namespace BountyHunter.Thieves
{
    // The result screen's sounds, listening to the ResultScreen: little ticks climbing while the
    // scores count up (Tools/UISounds/make_result_sounds.py makes them; thinned out, as several rows
    // count at once) and the applause (Clapping) as the winner's score lands, started past the
    // recording's silent lead-in so the claps come at once and faded out by applauseEndSeconds,
    // before the recording's second wave of clapping. Both come from the two speakers alike,
    // the ticks down with the music and the applause as loud as the other screens' big moments
    // (about -20 LUFS, a little under them as it lasts so much longer).
    public sealed class ResultSounds : MonoBehaviour
    {
        [SerializeField] private ResultScreen result;

        [Header("Clips")]
        [SerializeField] private AudioClip tick;
        [SerializeField] private AudioClip applause;
        // Where in the applause recording the clapping starts (it opens with a second of silence).
        [SerializeField, Min(0f)] private float applauseStartSeconds = 0.95f;
        // Where in the recording the applause has faded out (its second wave starts just after).
        [SerializeField, Min(0f)] private float applauseEndSeconds = 8f;
        [SerializeField, Min(0.01f)] private float applauseFadeSeconds = 0.4f;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] private float tickVolume = 0.25f;
        [SerializeField, Range(0f, 1f)] private float applauseVolume = 0.85f;

        [Header("Counting")]
        // A tick sooner than this after the last one is skipped.
        [SerializeField, Min(0.01f)] private float tickGapSeconds = 0.07f;
        // The ticks climb from 1 to this pitch as the count goes on.
        [SerializeField, Range(1f, 2f)] private float tickTopPitch = 1.5f;

        private AudioSource tickVoice;
        private AudioSource applauseVoice;
        private float lastTick = float.MinValue;

        private void Awake()
        {
            tickVoice = CreateSource();
            applauseVoice = CreateSource();
            applauseVoice.clip = applause;
            if (result == null || tick == null || applause == null)
            {
                Debug.LogWarning("[ResultSounds] Result, Tick or Applause is not set; the result screen will be quieter", this);
            }
        }

        // Fades the applause out over its last applauseFadeSeconds before applauseEndSeconds.
        private void Update()
        {
            if (!applauseVoice.isPlaying) return;

            float left = applauseEndSeconds - applauseVoice.time;
            applauseVoice.volume = applauseVolume * Mathf.Clamp01(left / applauseFadeSeconds);
            if (left <= 0f) applauseVoice.Stop();
        }

        private void OnEnable()
        {
            if (result == null) return;
            result.ScoreTicked += OnScoreTicked;
            result.ScoreCounted += OnScoreCounted;
        }

        private void OnDisable()
        {
            if (result == null) return;
            result.ScoreTicked -= OnScoreTicked;
            result.ScoreCounted -= OnScoreCounted;
        }

        private void OnScoreTicked(float progress)
        {
            if (tick == null || Time.time - lastTick < tickGapSeconds) return;

            lastTick = Time.time;
            tickVoice.pitch = Mathf.Lerp(1f, tickTopPitch, progress);
            tickVoice.PlayOneShot(tick, tickVolume);
        }

        // place: the row whose score landed (1 is the winner).
        private void OnScoreCounted(int place)
        {
            if (place != 1 || applause == null) return;

            applauseVoice.volume = applauseVolume;
            applauseVoice.time = Mathf.Min(applauseStartSeconds, applause.length - 0.01f);
            applauseVoice.Play();
        }

        // A plain 2D source (the wall has no place in the room to sound from).
        private AudioSource CreateSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
