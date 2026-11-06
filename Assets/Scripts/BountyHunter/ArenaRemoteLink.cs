using Fortal.Arena;
using Fortal.Display;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Connects the Fortal web launcher's UDP commands to the HOW TO PLAY board on the wall. The board
    // may open only during the intro, and it drops away when the intro gives way to the game. It adds
    // itself next to the intro as the scene loads, so no scene set-up can be left out of a build.
    // Console lines start with [ArenaRemoteLink].
    public sealed class ArenaRemoteLink : MonoBehaviour
    {
        private TutorialPopup tutorial;
        private IntroSequence intro;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var intro = FindFirstObjectByType<IntroSequence>(FindObjectsInactive.Include);
            var tutorial = FindFirstObjectByType<TutorialPopup>(FindObjectsInactive.Include);
            if (intro == null || tutorial == null)
            {
                Debug.LogWarning("[ArenaRemoteLink] Needs the tutorial and the intro: the launcher cannot open the tutorial");
                return;
            }

            if (!intro.TryGetComponent(out ArenaRemoteLink link)) link = intro.gameObject.AddComponent<ArenaRemoteLink>();
            link.Connect(tutorial, intro);
        }

        private void Connect(TutorialPopup board, IntroSequence introSequence)
        {
            tutorial = board;
            intro = introSequence;
            FortalArenaRemote.IsTutorialOpen = () => tutorial.IsOpen;
            FortalArenaRemote.CanOpenTutorial = () => intro.IsShowing;
            FortalArenaRemote.OpenTutorial = tutorial.Open;
            FortalArenaRemote.CloseTutorial = tutorial.Close;
            intro.Leaving += tutorial.Close;
            Debug.Log("[ArenaRemoteLink] The launcher can open the tutorial during the intro", this);
        }

        private void OnDestroy()
        {
            if (tutorial == null || intro == null) return;

            FortalArenaRemote.IsTutorialOpen = null;
            FortalArenaRemote.CanOpenTutorial = null;
            FortalArenaRemote.OpenTutorial = null;
            FortalArenaRemote.CloseTutorial = null;
            intro.Leaving -= tutorial.Close;
        }
    }
}
