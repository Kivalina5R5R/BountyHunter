using UnityEngine;
using UnityEngine.InputSystem;

namespace BountyHunter.Thieves
{
    // The OST on the intro screen, switched on and off with P. The choice is remembered (PlayerPrefs),
    // in the editor and in a build alike: left on, the song plays every time the intro shows, until
    // P turns it off again. It fades in and out when switched, and fades out as the intro gives way
    // to the game. Console lines start with [IntroMusic].
    public sealed class IntroMusic : MonoBehaviour
    {
        private const string OnKey = "BountyHunter.IntroOST";

        [SerializeField] private IntroSequence intro;
        [SerializeField] private AudioClip song;
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.6f;

        private AudioSource source;
        private float target;

        private static bool On
        {
            get => PlayerPrefs.GetInt(OnKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(OnKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private bool IntroShowing => intro != null && intro.IsShowing;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.clip = song;
        }

        private void OnEnable()
        {
            if (intro != null) intro.Leaving += FadeOut;
        }

        private void OnDisable()
        {
            if (intro != null) intro.Leaving -= FadeOut;
        }

        private void Start()
        {
            if (On && IntroShowing) FadeIn();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame) Toggle();

            source.volume = Mathf.MoveTowards(source.volume, target, volume / fadeSeconds * Time.deltaTime);
            if (target <= 0f && source.volume <= 0f && source.isPlaying) source.Stop();
        }

        private void Toggle()
        {
            On = !On;
            Debug.Log($"[IntroMusic] OST {(On ? "on: it plays on the intro screen from now on" : "off")}", this);
            if (!IntroShowing) return;

            if (On) FadeIn();
            else FadeOut();
        }

        private void FadeIn()
        {
            if (song == null) return;

            target = volume;
            if (!source.isPlaying) source.Play();
        }

        private void FadeOut()
        {
            target = 0f;
        }
    }
}
