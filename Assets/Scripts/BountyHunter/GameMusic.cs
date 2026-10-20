using UnityEngine;

namespace BountyHunter.Thieves
{
    // The background music while the game is played (Cartoon Chase, looping). It fades in as the game
    // starts after the intro (straight away when there is no intro) and fades out as the game ends
    // and the result comes up. Console lines start with [GameMusic].
    public sealed class GameMusic : MonoBehaviour
    {
        [SerializeField] private IntroSequence intro;
        [SerializeField] private ResultSequence result;
        [SerializeField] private AudioClip music;
        [SerializeField, Range(0f, 1f)] private float volume = 0.45f;
        [SerializeField, Min(0.05f)] private float fadeInSeconds = 1f;
        [SerializeField, Min(0.05f)] private float fadeOutSeconds = 1.2f;

        private AudioSource source;
        private float target;
        private float fadeSeconds = 1f;

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
            if (intro != null) intro.GameStarted += FadeIn;
            if (result != null) result.GameEnded += FadeOut;
        }

        private void OnDisable()
        {
            if (intro != null) intro.GameStarted -= FadeIn;
            if (result != null) result.GameEnded -= FadeOut;
        }

        // Without an intro the game is on from the first frame.
        private void Start()
        {
            if (intro == null || !intro.IsShowing) FadeIn();
        }

        private void Update()
        {
            source.volume = Mathf.MoveTowards(source.volume, target, volume / fadeSeconds * Time.deltaTime);
            if (target <= 0f && source.volume <= 0f && source.isPlaying) source.Stop();
        }

        private void FadeIn()
        {
            if (music == null) return;

            target = volume;
            fadeSeconds = fadeInSeconds;
            if (!source.isPlaying) source.Play();
            Debug.Log($"[GameMusic] {music.name} plays", this);
        }

        private void FadeOut()
        {
            target = 0f;
            fadeSeconds = fadeOutSeconds;
        }
    }
}
