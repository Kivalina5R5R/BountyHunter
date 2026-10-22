using UnityEngine;

namespace BountyHunter.Thieves
{
    // The result screen's background music (Result_BGM, cut by Tools/MusicLoops/make_result_bgm_loop.py
    // into an intro and an 8-bar loop). It starts the moment the game ends, as the wall fades through
    // black to the result, fading in while the game's music (GameMusic) fades out, so the two cross
    // over the transition. The intro plays once and the loop follows it to the sample (both are
    // scheduled on the audio clock), then repeats seamlessly for as long as the result shows. Its
    // level sits between the game's music and the intro's (about -26 LUFS): the result has no game
    // sounds over it, like the intro, yet coming in from the game's music it does not jump. Console
    // lines start with [ResultMusic].
    public sealed class ResultMusic : MonoBehaviour
    {
        // A moment for the audio system to have both clips ready before the intro starts.
        private const double ScheduleAheadSeconds = 0.1;

        [SerializeField] private ResultSequence result;
        [SerializeField] private AudioClip intro;
        [SerializeField] private AudioClip loop;
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
        // As long as GameMusic takes to fade out, so the two cross.
        [SerializeField, Min(0.05f)] private float fadeInSeconds = 1.2f;

        private AudioSource introSource;
        private AudioSource loopSource;
        private float level;
        private float target;
        private bool started;

        private void Awake()
        {
            introSource = CreateSource(intro, false);
            loopSource = CreateSource(loop, true);
            if (result == null || loop == null)
            {
                Debug.LogWarning("[ResultMusic] Result or Loop is not set; the result screen will have no music", this);
            }
        }

        private void OnEnable()
        {
            if (result != null) result.GameEnded += StartMusic;
        }

        private void OnDisable()
        {
            if (result != null) result.GameEnded -= StartMusic;
        }

        private void Update()
        {
            level = Mathf.MoveTowards(level, target, volume / fadeInSeconds * Time.deltaTime);
            introSource.volume = level;
            loopSource.volume = level;
        }

        private void StartMusic()
        {
            if (started || loop == null) return;

            started = true;
            target = volume;
            level = 0f;
            double at = AudioSettings.dspTime + ScheduleAheadSeconds;
            if (intro != null)
            {
                introSource.PlayScheduled(at);
                at += (double)intro.samples / intro.frequency;
            }

            loopSource.PlayScheduled(at);
            Debug.Log($"[ResultMusic] {(intro != null ? intro.name + " then " : "")}{loop.name} plays", this);
        }

        private AudioSource CreateSource(AudioClip clip, bool looping)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = looping;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.clip = clip;
            return source;
        }
    }
}
