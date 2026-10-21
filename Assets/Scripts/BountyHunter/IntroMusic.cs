using UnityEngine;
using UnityEngine.InputSystem;

namespace BountyHunter.Thieves
{
    // The intro screen's music. It starts once the patrol car has knocked the bin over and the bin
    // has landed (IntroBinCrash.Crashed plus startAfterCrashSeconds), and loops until the intro gives
    // way to the game, when it fades out. P swaps between song and otherSong with a cross-fade, and
    // swaps back on the next press; the choice is remembered (PlayerPrefs), in the editor and in a
    // build alike, so the intro starts with whichever song was last chosen. Console lines start with
    // [IntroMusic].
    public sealed class IntroMusic : MonoBehaviour
    {
        private const string OtherSongKey = "BountyHunter.IntroOtherSong";

        [SerializeField] private IntroSequence intro;
        [SerializeField] private IntroBinCrash binCrash;
        // The intro's own music, and the one P swaps to.
        [SerializeField] private AudioClip song;
        [SerializeField] private AudioClip otherSong;
        // From the car hitting the bin until the music starts (the bin's flight and landing).
        [SerializeField, Min(0f)] private float startAfterCrashSeconds = 0.85f;
        // Each song's own volume, so a louder recording (the OST) does not jump out when swapped to.
        [SerializeField, Range(0f, 1f)] private float volume = 0.4f;
        [SerializeField, Range(0f, 1f)] private float otherSongVolume = 0.16f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.6f;

        private readonly AudioSource[] sources = new AudioSource[2];
        private readonly float[] targets = new float[2];
        private int playing;
        private bool started;

        private static bool OtherChosen
        {
            get => PlayerPrefs.GetInt(OtherSongKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(OtherSongKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private AudioClip ChosenSong => OtherChosen && otherSong != null ? otherSong : song;

        private bool IntroShowing => intro != null && intro.IsShowing;

        private void Awake()
        {
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.volume = 0f;
                sources[i] = source;
            }
        }

        private void OnEnable()
        {
            if (intro != null) intro.Leaving += FadeOut;
            if (binCrash != null) binCrash.Crashed += OnCrashed;
        }

        private void OnDisable()
        {
            if (intro != null) intro.Leaving -= FadeOut;
            if (binCrash != null) binCrash.Crashed -= OnCrashed;
            CancelInvoke(nameof(StartMusic));
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame) Swap();

            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                source.volume = Mathf.MoveTowards(source.volume, targets[i], Mathf.Max(volume, otherSongVolume) / fadeSeconds * Time.deltaTime);
                if (targets[i] <= 0f && source.volume <= 0f && source.isPlaying) source.Stop();
            }
        }

        private void OnCrashed()
        {
            CancelInvoke(nameof(StartMusic));
            Invoke(nameof(StartMusic), startAfterCrashSeconds);
        }

        private void StartMusic()
        {
            if (!IntroShowing) return;

            started = true;
            FadeTo(ChosenSong);
        }

        // Swaps to the other song (and remembers it); while the music is on it cross-fades over.
        private void Swap()
        {
            OtherChosen = !OtherChosen;
            AudioClip next = ChosenSong;
            Debug.Log($"[IntroMusic] Intro music is now {(next != null ? next.name : "none")}", this);
            if (started && IntroShowing) FadeTo(next);
        }

        // Fades the song now playing out and clip in from its start on the other source.
        private void FadeTo(AudioClip clip)
        {
            if (clip == null) return;

            targets[playing] = 0f;
            playing = 1 - playing;
            AudioSource source = sources[playing];
            source.Stop();
            source.clip = clip;
            source.volume = 0f;
            source.Play();
            targets[playing] = clip == otherSong ? otherSongVolume : volume;
        }

        private void FadeOut()
        {
            CancelInvoke(nameof(StartMusic));
            started = false;
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i] = 0f;
            }
        }
    }
}
