using UnityEngine;
using UnityEngine.Serialization;

namespace BountyHunter.Thieves
{
    // The suspect lineup's sounds (mini game 1; Tools/LineupSounds/make_lineup_sounds.py makes them
    // into Assets/Audio/Lineup, the ring step and the coin are the select and gameplay's own). Like GameSfx
    // it listens to the game rather than the game calling it: each pair of suspects dropping onto
    // their plates (a bass note and a snap, a step higher each pair), each clue card going up,
    // players stepping onto a ring and locking in, the last seconds pulsing and a falling chime as
    // answers close, then nothing more until the CAUGHT stamp lands on the culprit, and a confetti
    // pop with the bounty's chime when someone got it right. The same sound starting several times at once (two
    // suspects coming in together, players locking in together) plays once. Volumes are in the
    // gameplay's tiers: the stamp and the cheer loudest, the clues and locking in
    // next, the entrances and steps under them. LineupSfxAutoWire adds it to the lineup and fills it
    // in when the editor loads.
    public sealed class LineupSfx : MonoBehaviour
    {
        [Header("Listens to")]
        [SerializeField] private LineupPuzzle puzzle;
        [SerializeField] private LineupWall wall;

        [Header("Suspects coming in")]
        [SerializeField] private AudioClip drop;
        [SerializeField, Range(0f, 1f)] private float entranceVolume = 0.55f;
        // The suspects come in two by two; each pair's notes are played this many semitones up, so the
        // pairs climb like a walking bass line.
        [SerializeField] private int[] pairSemitones = { 0, 3, 5, 7, 10, 12 };

        [Header("Clues and answers")]
        [SerializeField] private AudioClip clue;
        [SerializeField, Range(0f, 1f)] private float clueVolume = 0.6f;
        [SerializeField] private AudioClip stepOn;
        [SerializeField, Range(0f, 1f)] private float stepOnVolume = 0.4f;
        [SerializeField] private AudioClip locked;
        [SerializeField, Range(0f, 1f)] private float lockedVolume = 0.6f;

        [Header("Time running out")]
        [SerializeField] private AudioClip tick;
        // The last three seconds.
        [SerializeField] private AudioClip finalTick;
        [SerializeField] private AudioClip timeUp;
        [SerializeField, Range(0f, 1f)] private float timeVolume = 0.75f;

        [Header("The reveal")]
        [SerializeField] private AudioClip stamp;
        [SerializeField] private AudioClip cheer;
        [SerializeField] private AudioClip coin;
        [SerializeField, Range(0f, 1f)] private float revealVolume = 0.7f;

        // The same clip starting again within this long (several at once) is not played again.
        [SerializeField, Min(0f)] private float sameSoundGapSeconds = 0.1f;
        [FormerlySerializedAs("voices")]
        [SerializeField, Range(1, 8)] private int voiceCount = 5;

        private SfxVoices voices;

        private void Awake()
        {
            voices = new SfxVoices(transform, voiceCount, sameSoundGapSeconds);
        }

        private void OnEnable()
        {
            if (puzzle != null)
            {
                puzzle.SuspectIntroduced += OnSuspectIntroduced;
                puzzle.ClueShown += OnClueShown;
                puzzle.RingSteppedOn += OnRingSteppedOn;
                puzzle.AnswerLocked += OnAnswerLocked;
                puzzle.AnswersClosed += OnAnswersClosed;
                puzzle.Settled += OnSettled;
            }

            if (wall != null)
            {
                wall.CaughtStamped += OnCaughtStamped;
                wall.HurrySecond += OnHurrySecond;
            }
        }

        private void OnDisable()
        {
            if (puzzle != null)
            {
                puzzle.SuspectIntroduced -= OnSuspectIntroduced;
                puzzle.ClueShown -= OnClueShown;
                puzzle.RingSteppedOn -= OnRingSteppedOn;
                puzzle.AnswerLocked -= OnAnswerLocked;
                puzzle.AnswersClosed -= OnAnswersClosed;
                puzzle.Settled -= OnSettled;
            }

            if (wall != null)
            {
                wall.CaughtStamped -= OnCaughtStamped;
                wall.HurrySecond -= OnHurrySecond;
            }
        }

        private void OnSuspectIntroduced(int suspect) => Play(drop, entranceVolume, PairPitch(suspect));

        private float PairPitch(int suspect)
        {
            if (pairSemitones == null || pairSemitones.Length == 0) return 1f;

            int step = pairSemitones[Mathf.Min(suspect / 2, pairSemitones.Length - 1)];
            return Mathf.Pow(2f, step / 12f);
        }

        private void OnClueShown(int clueIndex) => Play(clue, clueVolume);

        private void OnRingSteppedOn() => Play(stepOn, stepOnVolume);

        private void OnAnswerLocked() => Play(locked, lockedVolume);

        // The clock's last seconds; 0 is time up, which the time-up chime covers.
        private void OnHurrySecond(int secondsLeft)
        {
            if (secondsLeft <= 0) return;
            Play(secondsLeft <= 3 ? finalTick : tick, timeVolume);
        }

        private void OnAnswersClosed() => Play(timeUp, timeVolume);

        private void OnCaughtStamped() => Play(stamp, revealVolume);

        // A confetti pop and the bounty's chime when anyone got it right.
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
