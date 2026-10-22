using UnityEngine;

namespace BountyHunter.Thieves
{
    // The background music while the game is played (Cartoon Chase, looping). It comes in the moment
    // the intro starts giving way to the game (as GO lands and the wall fades through black),
    // crossing the intro's music as that fades out, so the energy carries over with no gap. It starts
    // as loud as the intro's music (startVolume), holds there for holdSeconds and then settles over
    // settleSeconds to its playing level (volume), under the sound effects (about 10 dB below a
    // catch) so the plays stand out once the first round is under way. Straight away at the playing
    // level when there is no intro. It fades out as the game ends and the result comes up. Console
    // lines start with [GameMusic].
    public sealed class GameMusic : MonoBehaviour
    {
        [SerializeField] private IntroSequence intro;
        [SerializeField] private ResultSequence result;
        [SerializeField] private AudioClip music;
        // Coming out of the intro: matched to the intro's music, so the switch does not drop.
        [SerializeField, Range(0f, 1f)] private float startVolume = 0.4f;
        // While the game is played.
        [SerializeField, Range(0f, 1f)] private float volume = 0.25f;
        // About as long as the intro's music takes to fade out, so the two cross.
        [SerializeField, Min(0.05f)] private float fadeInSeconds = 0.5f;
        [SerializeField, Min(0f)] private float holdSeconds = 3f;
        [SerializeField, Min(0.05f)] private float settleSeconds = 5f;
        [SerializeField, Min(0.05f)] private float fadeOutSeconds = 1.2f;

        private AudioSource source;
        private float target;
        // Volume change per second toward target.
        private float rate = 1f;
        private float settleAt = float.MaxValue;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.clip = music;
        }

        private void OnEnable()
        {
            if (intro != null) intro.Leaving += ComeIn;
            if (result != null) result.GameEnded += FadeOut;
        }

        private void OnDisable()
        {
            if (intro != null) intro.Leaving -= ComeIn;
            if (result != null) result.GameEnded -= FadeOut;
        }

        // Without an intro the game is on from the first frame.
        private void Start()
        {
            if (intro == null || !intro.IsShowing) FadeTo(volume, fadeInSeconds);
        }

        private void Update()
        {
            if (Time.time >= settleAt)
            {
                settleAt = float.MaxValue;
                FadeTo(volume, settleSeconds);
            }

            source.volume = Mathf.MoveTowards(source.volume, target, rate * Time.deltaTime);
            if (target <= 0f && source.volume <= 0f && source.isPlaying) source.Stop();
        }

        // Out of the intro: in at the intro music's level, settling down to the playing level later.
        private void ComeIn()
        {
            FadeTo(startVolume, fadeInSeconds);
            settleAt = Time.time + holdSeconds;
        }

        private void FadeOut()
        {
            settleAt = float.MaxValue;
            FadeTo(0f, fadeOutSeconds);
        }

        // Moves the volume to level over seconds, starting the music if it is not playing yet.
        private void FadeTo(float level, float seconds)
        {
            if (music == null) return;

            target = level;
            rate = Mathf.Max(Mathf.Abs(level - source.volume), 0.01f) / seconds;
            if (level <= 0f || source.isPlaying) return;

            source.Play();
            Debug.Log($"[GameMusic] {music.name} plays", this);
        }
    }
}
