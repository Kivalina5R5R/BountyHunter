using UnityEngine;

namespace BountyHunter.Thieves
{
    // The sounds of one character card: a soft pop as the card lands (its SELECT button comes up with
    // it), a warm bop when a player steps on, marimba notes climbing the scale as the ring fills (in
    // step with it: stepping off fades them out, stepping back on picks up at the note the ring is
    // at), and the stamp and a marimba ta-da once picked. With several players picking at once the
    // CardSoundMixer on the cards' parent decides: only the fullest ring's climb sounds, and the short
    // sounds are thinned out and kept from piling up.
    // Sits next to the CharacterCard.
    [RequireComponent(typeof(CharacterCard))]
    public sealed class CharacterCardSounds : MonoBehaviour
    {
        [SerializeField] private AudioClip cardPop;
        [SerializeField] private AudioClip stepOn;
        [SerializeField] private AudioClip loading;
        [SerializeField] private AudioClip selected;
        // The pop and the bop, kept below the ta-da so picking stands out.
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float selectedVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float loadingVolume = 0.45f;
        // How long the ring takes to fill (CharacterSelect.holdSeconds); the climb is made that long.
        [SerializeField, Min(0.1f)] private float fillSeconds = 2f;
        // How quickly the climb fades away when the player steps off.
        [SerializeField, Min(0.01f)] private float stepOffFadeSeconds = 0.12f;

        private CharacterCard card;
        private CardSoundMixer mixer;
        private AudioSource oneShots;
        private AudioSource climb;
        private float lastProgress;

        // True while this card's ring is filling (someone is standing on it, not yet picked).
        public bool IsFilling { get; private set; }

        // How full the ring is, 0..1.
        public float FillProgress => card.Progress;

        private void Awake()
        {
            card = GetComponent<CharacterCard>();
            mixer = FindMixer();
            oneShots = CreateSource();
            climb = CreateSource();
            climb.clip = loading;
        }

        private void OnEnable()
        {
            card.Popped += OnPopped;
            card.SteppedOn += OnSteppedOn;
            card.Stamped += OnStamped;
            if (mixer != null) mixer.Register(this);
        }

        private void OnDisable()
        {
            card.Popped -= OnPopped;
            card.SteppedOn -= OnSteppedOn;
            card.Stamped -= OnStamped;
            if (mixer != null) mixer.Unregister(this);
            IsFilling = false;
            climb.Stop();
        }

        // The climb follows the ring while it fills, and fades out once it stops filling or another
        // card's fuller ring has the climb. Once the card is taken the last note is left to ring.
        private void Update()
        {
            if (loading == null || card.Taken)
            {
                IsFilling = false;
                return;
            }

            float progress = card.Progress;
            IsFilling = progress > lastProgress;
            lastProgress = progress;
            bool sounding = IsFilling && (mixer == null || mixer.MayClimb(this));
            if (!sounding)
            {
                climb.volume = Mathf.MoveTowards(climb.volume, 0f, loadingVolume / stepOffFadeSeconds * Time.deltaTime);
                if (climb.isPlaying && climb.volume <= 0f) climb.Stop();
                return;
            }

            float at = Mathf.Min(progress * fillSeconds, loading.length - 0.01f);
            climb.volume = loadingVolume * (mixer != null ? mixer.ClimbVolumeScale : 1f);
            if (!climb.isPlaying)
            {
                climb.time = at;
                climb.Play();
            }
            else if (Mathf.Abs(climb.time - at) > 0.2f)
            {
                climb.time = at;
            }
        }

        private void OnPopped()
        {
            Play(cardPop);
        }

        private void OnSteppedOn()
        {
            Play(stepOn);
        }

        private void OnStamped()
        {
            if (mixer != null) mixer.Duck();
            Play(selected, selectedVolume);
        }

        private void Play(AudioClip clip)
        {
            Play(clip, volume);
        }

        private void Play(AudioClip clip, float clipVolume)
        {
            if (clip == null) return;
            if (mixer != null) mixer.PlayOneShot(clip, clipVolume);
            else oneShots.PlayOneShot(clip, clipVolume);
        }

        // The mixer shared by all the cards, on their parent; the first card adds it when the scene
        // has none, so every card under that parent shares the same one.
        private CardSoundMixer FindMixer()
        {
            if (transform.parent == null) return null;

            var shared = transform.parent.GetComponent<CardSoundMixer>();
            return shared != null ? shared : transform.parent.gameObject.AddComponent<CardSoundMixer>();
        }

        // A plain 2D source (the floor has no place in the room to sound from).
        private AudioSource CreateSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
