using System;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // One sound a wall transition plays at a moment of its own (the cage slamming, the police car
    // passing, the boss striking in), and how loud. Tools/TransitionSounds/make_transition_sounds.py
    // makes them into Assets/Audio/Transition; each transition's prefab is given them when the editor
    // loads (TransitionSoundsAutoWire). Leave the clip empty for silence.
    [Serializable]
    public sealed class TransitionSound
    {
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume;

        public TransitionSound(float volume)
        {
            this.volume = volume;
        }

        public void Play(AudioSource source)
        {
            if (clip != null && source != null) source.PlayOneShot(clip, volume);
        }

        // The transition's own voice: from both speakers alike, and it plays while the game is frozen
        // (the cut-ins run with time stopped).
        public static AudioSource SourceOn(GameObject holder)
        {
            if (!holder.TryGetComponent(out AudioSource source)) source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }
    }
}
