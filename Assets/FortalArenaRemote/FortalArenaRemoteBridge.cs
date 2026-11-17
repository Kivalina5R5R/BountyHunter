using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;

namespace Fortal.Arena
{
    // Connects the game to the web launcher with no code. Put it on any object in the scene that has the tutorial.
    //   Simple: drag the tutorial's root object into Tutorial Root. It is switched on to open and off to close.
    //   Your own flow: leave Tutorial Root empty, connect On Open / On Close, and call MarkTutorialClosed() when it ends.
    [AddComponentMenu("Fortal/Arena Remote Bridge")]
    public sealed class FortalArenaRemoteBridge : MonoBehaviour
    {
        [Header("Tutorial")]
        [Tooltip("The object that is the tutorial. Active = open. Empty: use the events below instead.")]
        public GameObject tutorialRoot;
        [Tooltip("The tutorial may open only while one of these is active (your menu screen). Empty: it may always open.")]
        public GameObject[] allowedWhileActive;
        public UnityEvent onOpenTutorial = new UnityEvent();
        public UnityEvent onCloseTutorial = new UnityEvent();

        [Header("Language (only for a game with Thai and English)")]
        public bool supportsLanguage;
        public string currentLanguage = "th";
        public UnityEvent onThai = new UnityEvent();
        public UnityEvent onEnglish = new UnityEvent();

        [Header("Music / effects volume from the launcher (kit 3.0)")]
        [Tooltip("Your Audio Mixer. Its exposed parameters below get the launcher's music / effects levels (in dB). Empty: not used.")]
        public AudioMixer mixer;
        [Tooltip("Exposed parameter of the music group (right-click Volume > Expose, then rename). Empty: not used.")]
        public string musicParameter = "MusicVolume";
        [Tooltip("Exposed parameter of the effects group. Empty: not used.")]
        public string sfxParameter = "SfxVolume";
        [Tooltip("No mixer: music sources. Their volume set in the Inspector is 100%. Only for sources whose volume the game never changes.")]
        public AudioSource[] musicSources = new AudioSource[0];
        [Tooltip("No mixer: effect sources, as above.")]
        public AudioSource[] sfxSources = new AudioSource[0];
        [Tooltip("Your own code: gets the music level 0..1 on every change.")]
        public FloatEvent onMusicVolume = new FloatEvent();
        [Tooltip("Your own code: gets the effects level 0..1 on every change.")]
        public FloatEvent onSfxVolume = new FloatEvent();

        [Serializable] public class FloatEvent : UnityEvent<float> { }

        bool open;
        float[] musicBase, sfxBase;

        // For Tutorial Root left empty: call these if the tutorial opens or ends by itself (a Skip button, the last page).
        public void MarkTutorialOpen() { open = true; }
        public void MarkTutorialClosed() { open = false; }
        // For a language button inside the game, so the launcher's page follows it.
        public void MarkLanguage(string language) { currentLanguage = language; }

        void OnEnable()
        {
            FortalArenaRemote.IsTutorialOpen = () => tutorialRoot != null ? tutorialRoot.activeInHierarchy : open;
            FortalArenaRemote.CanOpenTutorial = Allowed;
            FortalArenaRemote.OpenTutorial = Open;
            FortalArenaRemote.CloseTutorial = Close;
            musicBase = BaseVolumes(musicSources, musicBase);
            sfxBase = BaseVolumes(sfxSources, sfxBase);
            FortalArenaSettings.Changed += ApplyVolumes;
            ApplyVolumes();
            if (supportsLanguage)
            {
                FortalArenaRemote.GetLanguage = () => currentLanguage;
                FortalArenaRemote.SetLanguage = SetLanguage;
            }
        }

        void OnDisable()
        {
            FortalArenaSettings.Changed -= ApplyVolumes;
            FortalArenaRemote.IsTutorialOpen = FortalArenaRemote.CanOpenTutorial = null;
            FortalArenaRemote.OpenTutorial = FortalArenaRemote.CloseTutorial = null;
            FortalArenaRemote.GetLanguage = null;
            FortalArenaRemote.SetLanguage = null;
        }

        // The Inspector volume of each source is its 100%; read once, so later changes multiply the same base.
        static float[] BaseVolumes(AudioSource[] sources, float[] known)
        {
            if (known != null && known.Length == sources.Length) return known;
            var volumes = new float[sources.Length];
            for (int i = 0; i < sources.Length; i++) volumes[i] = sources[i] != null ? sources[i].volume : 1f;
            return volumes;
        }

        void ApplyVolumes()
        {
            float music = FortalArenaSettings.Music, sfx = FortalArenaSettings.Sfx;
            if (mixer != null)
            {
                if (!string.IsNullOrEmpty(musicParameter) && !mixer.SetFloat(musicParameter, Decibels(music)))
                    Debug.LogWarning("[FortalArenaRemote] The mixer has no exposed parameter '" + musicParameter + "'");
                if (!string.IsNullOrEmpty(sfxParameter) && !mixer.SetFloat(sfxParameter, Decibels(sfx)))
                    Debug.LogWarning("[FortalArenaRemote] The mixer has no exposed parameter '" + sfxParameter + "'");
            }
            for (int i = 0; i < musicSources.Length; i++) if (musicSources[i] != null) musicSources[i].volume = musicBase[i] * music;
            for (int i = 0; i < sfxSources.Length; i++) if (sfxSources[i] != null) sfxSources[i].volume = sfxBase[i] * sfx;
            onMusicVolume.Invoke(music);
            onSfxVolume.Invoke(sfx);
        }

        // 0..1 to the mixer's dB: 1 = 0 dB, 0 = -80 dB (silent)
        static float Decibels(float level) { return level <= .0001f ? -80f : Mathf.Log10(level) * 20f; }

        bool Allowed()
        {
            if (allowedWhileActive == null || allowedWhileActive.Length == 0) return true;
            foreach (var screen in allowedWhileActive) if (screen != null && screen.activeInHierarchy) return true;
            return false;
        }

        void Open()
        {
            open = true;
            if (tutorialRoot != null) tutorialRoot.SetActive(true);
            onOpenTutorial.Invoke();
        }

        void Close()
        {
            open = false;
            if (tutorialRoot != null) tutorialRoot.SetActive(false);
            onCloseTutorial.Invoke();
        }

        void SetLanguage(string language)
        {
            if (language == currentLanguage) return;
            currentLanguage = language;
            (language == "en" ? onEnglish : onThai).Invoke();
        }
    }
}
