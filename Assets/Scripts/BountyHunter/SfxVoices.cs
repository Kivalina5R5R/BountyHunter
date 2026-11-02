using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // A few voices for a mini game's sounds (LineupSfx, CctvSfx): each sound plays on the next free
    // voice (or the one that started longest ago when all are busy), from both speakers alike. The same
    // clip starting again within sameSoundGapSeconds (several things happening at once) plays once,
    // so it does not double up into one loud, phasing blur.
    public sealed class SfxVoices
    {
        private readonly List<AudioSource> sources = new List<AudioSource>();
        private readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();
        private readonly float sameSoundGapSeconds;
        private int nextVoice;

        public SfxVoices(Transform owner, int count, float sameSoundGapSeconds)
        {
            this.sameSoundGapSeconds = sameSoundGapSeconds;
            for (int i = 0; i < Mathf.Max(1, count); i++)
            {
                var holder = new GameObject($"Voice {i + 1}");
                holder.transform.SetParent(owner, false);
                var source = holder.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                sources.Add(source);
            }
        }

        // pitch also changes how long it lasts (2 is an octave up and twice as fast).
        public void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            if (clip == null) return;
            if (lastPlayed.TryGetValue(clip, out float at) && Time.unscaledTime - at < sameSoundGapSeconds) return;

            lastPlayed[clip] = Time.unscaledTime;
            AudioSource source = sources.Find(candidate => !candidate.isPlaying);
            if (source == null)
            {
                source = sources[nextVoice];
                nextVoice = (nextVoice + 1) % sources.Count;
                source.Stop();
            }

            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }
    }
}
