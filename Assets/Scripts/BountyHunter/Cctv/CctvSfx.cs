using UnityEngine;

namespace BountyHunter.Thieves
{
    // The security camera's sounds (mini game 2). Its own (Tools/CctvSounds/make_cctv_sounds.py,
    // Assets/Audio/Cctv): a thief leaping (running in, or out of a bin) and landing in a bin (each bin
    // a note of its own, so the hops can be heard as well as seen), a banana peel flapping out (kept quiet) and
    // splatting on the screen, and the thieves
    // popping up out of their bins at the reveal. Shared with the lineup, where the moment is the same:
    // stepping onto a ring and locking in, the last seconds pulsing, the time-up chime, the CAUGHT
    // stamp, and the confetti pop with the bounty's chime when someone picked right. Like GameSfx it
    // listens to the game rather than the game calling it. CctvSfxAutoWire adds it to the mini game
    // and fills it in when the editor loads.
    public sealed class CctvSfx : MonoBehaviour
    {
        [Header("Listens to")]
        [SerializeField] private CctvPuzzle puzzle;
        [SerializeField] private CctvWall wall;
        [SerializeField] private CctvBananaPeels peels;

        [Header("The clip")]
        [SerializeField] private AudioClip jump;
        [SerializeField] private AudioClip land;
        [SerializeField, Range(0f, 1f)] private float thiefVolume = 0.5f;
        // Each bin's landing, left to right, this many semitones from the clip's own pitch.
        [SerializeField] private int[] binSemitones = { -3, 0, 4, 7 };

        [Header("Banana peels")]
        [SerializeField] private AudioClip peelThrow;
        // The flight is kept well under everything else.
        [SerializeField, Range(0f, 1f)] private float peelThrowVolume = 0.25f;
        [SerializeField] private AudioClip peelSplat;
        [SerializeField, Range(0f, 1f)] private float peelVolume = 0.55f;

        [Header("Picking")]
        [SerializeField] private AudioClip stepOn;
        [SerializeField, Range(0f, 1f)] private float stepOnVolume = 0.4f;
        [SerializeField] private AudioClip locked;
        [SerializeField, Range(0f, 1f)] private float lockedVolume = 0.6f;
        [SerializeField] private AudioClip tick;
        // The last three seconds.
        [SerializeField] private AudioClip finalTick;
        [SerializeField] private AudioClip timeUp;
        [SerializeField, Range(0f, 1f)] private float timeVolume = 0.75f;

        [Header("The reveal")]
        [SerializeField] private AudioClip peek;
        [SerializeField, Range(0f, 1f)] private float peekVolume = 0.55f;
        [SerializeField] private AudioClip stamp;
        [SerializeField] private AudioClip cheer;
        [SerializeField] private AudioClip coin;
        [SerializeField, Range(0f, 1f)] private float revealVolume = 0.7f;

        // The same clip starting again within this long (several at once) is not played again.
        [SerializeField, Min(0f)] private float sameSoundGapSeconds = 0.06f;
        [SerializeField, Range(1, 8)] private int voiceCount = 6;

        private SfxVoices voices;

        private void Awake()
        {
            voices = new SfxVoices(transform, voiceCount, sameSoundGapSeconds);
        }

        private void OnEnable()
        {
            if (puzzle != null)
            {
                puzzle.RingSteppedOn += OnRingSteppedOn;
                puzzle.AnswerLocked += OnAnswerLocked;
                puzzle.AnswersClosed += OnAnswersClosed;
                puzzle.Settled += OnSettled;
            }

            if (wall != null)
            {
                wall.ThiefJumped += OnThiefJumped;
                wall.ThiefLanded += OnThiefLanded;
                wall.ThiefPeeked += OnThiefPeeked;
                wall.CaughtStamped += OnCaughtStamped;
                wall.HurrySecond += OnHurrySecond;
            }

            if (peels != null)
            {
                peels.Thrown += OnPeelThrown;
                peels.Splatted += OnPeelSplatted;
            }
        }

        private void OnDisable()
        {
            if (puzzle != null)
            {
                puzzle.RingSteppedOn -= OnRingSteppedOn;
                puzzle.AnswerLocked -= OnAnswerLocked;
                puzzle.AnswersClosed -= OnAnswersClosed;
                puzzle.Settled -= OnSettled;
            }

            if (wall != null)
            {
                wall.ThiefJumped -= OnThiefJumped;
                wall.ThiefLanded -= OnThiefLanded;
                wall.ThiefPeeked -= OnThiefPeeked;
                wall.CaughtStamped -= OnCaughtStamped;
                wall.HurrySecond -= OnHurrySecond;
            }

            if (peels != null)
            {
                peels.Thrown -= OnPeelThrown;
                peels.Splatted -= OnPeelSplatted;
            }
        }

        private void OnThiefJumped() => Play(jump, thiefVolume);

        private void OnThiefLanded(int bin)
        {
            float pitch = 1f;
            if (binSemitones != null && binSemitones.Length > 0)
            {
                pitch = Mathf.Pow(2f, binSemitones[Mathf.Clamp(bin, 0, binSemitones.Length - 1)] / 12f);
            }

            Play(land, thiefVolume, pitch);
        }

        private void OnPeelThrown() => Play(peelThrow, peelThrowVolume);

        private void OnPeelSplatted() => Play(peelSplat, peelVolume);

        private void OnRingSteppedOn() => Play(stepOn, stepOnVolume);

        private void OnAnswerLocked() => Play(locked, lockedVolume);

        // The clock's last seconds; 0 is time up, which the time-up chime covers.
        private void OnHurrySecond(int secondsLeft)
        {
            if (secondsLeft <= 0) return;
            Play(secondsLeft <= 3 ? finalTick : tick, timeVolume);
        }

        private void OnAnswersClosed() => Play(timeUp, timeVolume);

        private void OnThiefPeeked() => Play(peek, peekVolume);

        private void OnCaughtStamped() => Play(stamp, revealVolume);

        // A confetti pop and the bounty's chime when anyone picked right.
        private void OnSettled(int rightAnswers)
        {
            if (rightAnswers <= 0) return;
            Play(cheer, revealVolume);
            Play(coin, revealVolume);
        }

        private void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            voices?.Play(clip, volume, pitch);
        }
    }
}
