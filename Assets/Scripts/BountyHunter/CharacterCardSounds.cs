using UnityEngine;

namespace BountyHunter.Thieves
{
    // The sounds of one character card: a soft pop as the card lands (its SELECT button comes up with
    // it), a warm bop when a player steps on, marimba notes climbing the scale as the ring fills (in
    // step with it: stepping off fades them out, stepping back on picks up at the note the ring is
    // at), and the stamp and a marimba ta-da once picked.
    // Sits next to the CharacterCard.
    [RequireComponent(typeof(CharacterCard))]
    public sealed class CharacterCardSounds : MonoBehaviour
    {
        [SerializeField] private AudioClip cardPop;
        [SerializeField] private AudioClip stepOn;
        [SerializeField] private AudioClip loading;
        [SerializeField] private AudioClip selected;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float loadingVolume = 0.6f;
        // How long the ring takes to fill (CharacterSelect.holdSeconds); the climb is made that long.
        [SerializeField, Min(0.1f)] private float fillSeconds = 2f;
        // How quickly the climb fades away when the player steps off.
        [SerializeField, Min(0.01f)] private float stepOffFadeSeconds = 0.12f;

        private CharacterCard card;
        private AudioSource oneShots;
        private AudioSource climb;
        private float lastProgress;

        private void Awake()
        {
            card = GetComponent<CharacterCard>();
            oneShots = CreateSource();
            climb = CreateSource();
            climb.clip = loading;
        }

        private void OnEnable()
        {
            card.Popped += OnPopped;
            card.SteppedOn += OnSteppedOn;
            card.Stamped += OnStamped;
        }

        private void OnDisable()
        {
            card.Popped -= OnPopped;
            card.SteppedOn -= OnSteppedOn;
            card.Stamped -= OnStamped;
            climb.Stop();
        }

        // The climb follows the ring while it fills, and fades out once it stops filling. Once the
        // card is taken the last note is left to ring.
        private void Update()
        {
            if (loading == null || card.Taken) return;

            float progress = card.Progress;
            bool filling = progress > lastProgress;
            lastProgress = progress;
            if (!filling)
            {
                climb.volume = Mathf.MoveTowards(climb.volume, 0f, loadingVolume / stepOffFadeSeconds * Time.deltaTime);
                if (climb.isPlaying && climb.volume <= 0f) climb.Stop();
                return;
            }

            float at = Mathf.Min(progress * fillSeconds, loading.length - 0.01f);
            climb.volume = loadingVolume;
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
            Play(selected);
        }

        private void Play(AudioClip clip)
        {
            if (clip != null) oneShots.PlayOneShot(clip, volume);
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
