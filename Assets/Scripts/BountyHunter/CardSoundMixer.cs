using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Keeps the character cards' sounds from clashing when several players pick at once. Only one
    // marimba climb plays at a time: the card whose ring is fullest (it only hands over when another
    // card is clearly ahead, so the tune does not flip between cards); the others stay quiet. The
    // short sounds all go through here: the same sound started again within repeatGapSeconds is
    // skipped, sounds starting close together are turned down so the pile-up is not louder, and the
    // ta-da (Duck) turns the climb down for a moment so it stands out.
    // Sits on the parent of the cards; CharacterCardSounds finds it there (or adds it).
    public sealed class CardSoundMixer : MonoBehaviour
    {
        // The same sound started again sooner than this is skipped.
        [SerializeField, Min(0f)] private float repeatGapSeconds = 0.1f;
        // Sounds started within this long of each other count as playing together.
        [SerializeField, Min(0.01f)] private float crowdSeconds = 0.3f;
        // Another card must be this much fuller (0..1 of the ring) to take the climb over.
        [SerializeField, Range(0f, 0.5f)] private float handOverLead = 0.1f;
        // While the ta-da rings the climb drops to this share of its volume, for duckSeconds.
        [SerializeField, Range(0f, 1f)] private float duckedClimb = 0.3f;
        [SerializeField, Min(0f)] private float duckSeconds = 1.2f;

        private readonly List<CharacterCardSounds> cards = new List<CharacterCardSounds>();
        private readonly Dictionary<AudioClip, float> lastStarted = new Dictionary<AudioClip, float>();
        private readonly List<float> recentStarts = new List<float>();
        private CharacterCardSounds leader;
        private AudioSource oneShots;
        private float duckUntil = float.MinValue;

        // 1 normally, lower while the ta-da is ringing.
        public float ClimbVolumeScale => Time.time < duckUntil ? duckedClimb : 1f;

        private void Awake()
        {
            oneShots = gameObject.AddComponent<AudioSource>();
            oneShots.playOnAwake = false;
            oneShots.spatialBlend = 0f;
        }

        public void Register(CharacterCardSounds card)
        {
            if (!cards.Contains(card)) cards.Add(card);
        }

        public void Unregister(CharacterCardSounds card)
        {
            cards.Remove(card);
            if (leader == card) leader = null;
        }

        // True when this card's climb may sound now: it is the filling card with the fullest ring.
        public bool MayClimb(CharacterCardSounds card)
        {
            if (leader != null && !leader.IsFilling) leader = null;

            foreach (CharacterCardSounds other in cards)
            {
                if (other == null || !other.IsFilling || other == leader) continue;
                if (leader == null || other.FillProgress > leader.FillProgress + handOverLead) leader = other;
            }

            return card == leader;
        }

        // Plays a short sound unless the same one just started; quieter when others started with it.
        public void PlayOneShot(AudioClip clip, float volume)
        {
            if (clip == null) return;

            float now = Time.time;
            if (lastStarted.TryGetValue(clip, out float last) && now - last < repeatGapSeconds) return;
            lastStarted[clip] = now;

            recentStarts.RemoveAll(started => now - started > crowdSeconds);
            float crowd = 1f / Mathf.Sqrt(recentStarts.Count + 1);
            recentStarts.Add(now);
            oneShots.PlayOneShot(clip, volume * crowd);
        }

        // Turns the climb down while a big sound (the ta-da) rings.
        public void Duck()
        {
            duckUntil = Time.time + duckSeconds;
        }
    }
}
