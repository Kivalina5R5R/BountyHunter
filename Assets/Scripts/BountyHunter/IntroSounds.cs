using UnityEngine;

namespace BountyHunter.Thieves
{
    // The intro's sounds, kept in time with the arrival video: the video's own sound effects with its
    // background music taken out (the car rushing in, the siren, the dogs rolling and jumping; see
    // Tools/IntroSounds/split_original_track.py) start with the video, and the bin crash lands the
    // moment the car hits the bin (IntroBinCrash), which the video has no sound for. The untouched
    // track, music and all, can be played instead (originalVolume above 0, effectsVolume 0).
    // Everything stops with the intro wall when the game starts. Sits on the intro wall.
    public sealed class IntroSounds : MonoBehaviour
    {
        [SerializeField] private WallVideo arrival;
        [SerializeField] private IntroBinCrash binCrash;

        [Header("Video's sound")]
        // Its sound effects without the music, and the whole track as it came.
        [SerializeField] private AudioClip effects;
        [SerializeField, Range(0f, 1f)] private float effectsVolume = 1f;
        [SerializeField] private AudioClip original;
        [SerializeField, Range(0f, 1f)] private float originalVolume;

        [Header("Bin")]
        [SerializeField] private AudioClip crash;
        [SerializeField, Range(0f, 1f)] private float crashVolume = 0.8f;

        private AudioSource effectsSource;
        private AudioSource originalSource;
        private AudioSource oneShots;
        private bool started;

        private void Awake()
        {
            effectsSource = CreateSource();
            originalSource = CreateSource();
            oneShots = CreateSource();
        }

        private void OnEnable()
        {
            if (binCrash != null) binCrash.Crashed += OnCrashed;
        }

        private void OnDisable()
        {
            if (binCrash != null) binCrash.Crashed -= OnCrashed;
        }

        // Starts the tracks with the video, at the point it has reached, so they stay in step.
        private void Update()
        {
            if (started || arrival == null || !arrival.IsPlaying) return;

            started = true;
            float at = (float)arrival.PlaybackTime;
            PlayFrom(effectsSource, effects, effectsVolume, at);
            PlayFrom(originalSource, original, originalVolume, at);
        }

        private static void PlayFrom(AudioSource source, AudioClip clip, float volume, float at)
        {
            if (clip == null || volume <= 0f || at >= clip.length) return;

            source.clip = clip;
            source.volume = volume;
            source.time = at;
            source.Play();
        }

        private void OnCrashed()
        {
            if (crash != null) oneShots.PlayOneShot(crash, crashVolume);
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
