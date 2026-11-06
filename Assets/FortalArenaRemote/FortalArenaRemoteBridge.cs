using UnityEngine;
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

        bool open;

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
            if (supportsLanguage)
            {
                FortalArenaRemote.GetLanguage = () => currentLanguage;
                FortalArenaRemote.SetLanguage = SetLanguage;
            }
        }

        void OnDisable()
        {
            FortalArenaRemote.IsTutorialOpen = FortalArenaRemote.CanOpenTutorial = null;
            FortalArenaRemote.OpenTutorial = FortalArenaRemote.CloseTutorial = null;
            FortalArenaRemote.GetLanguage = null;
            FortalArenaRemote.SetLanguage = null;
        }

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
