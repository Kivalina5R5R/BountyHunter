using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BountyHunter.Thieves
{
    // Ends the game when the last map's clock runs out or N is pressed on it (both through
    // MapSwitcher), or at once with R for trying the screen out; never during the intro. The rounds stop, the wall
    // fades through black, the game's own pieces on the wall (the wanted board, posters, clock, dogs
    // and their tags) step aside and the result screen comes up over the station with the standings. Console lines start with [ResultSequence].
    public sealed class ResultSequence : MonoBehaviour
    {
        [SerializeField] private StageTimer timer;
        [SerializeField] private BountyRounds rounds;
        [SerializeField] private CharacterScoreTags scoreTags;
        [SerializeField] private ResultScreen result;
        // Switched off while the result shows; the station behind them stays.
        [SerializeField] private GameObject[] hideOnResult;
        [SerializeField] private FadeCurtain curtain;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.45f;
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.6f;

        private bool ended;

        public bool Ended => ended;

        // Raised the moment the game ends, as the wall starts fading to the result.
        public event Action GameEnded;

        private void Awake()
        {
            if (result != null) result.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) TryEndGame();
        }

        // Ends the game and brings up the result; false when it is already over or not under way yet.
        public bool TryEndGame()
        {
            // Only once the game is under way: the clock lives on the game wall.
            if (ended || (timer != null && !timer.isActiveAndEnabled)) return false;

            ended = true;
            GameEnded?.Invoke();
            if (rounds != null) rounds.Stop();
            StartCoroutine(ShowResult());
            return true;
        }

        private IEnumerator ShowResult()
        {
            if (curtain != null) yield return curtain.Close(fadeOutSeconds);

            if (hideOnResult != null)
            {
                foreach (GameObject piece in hideOnResult)
                {
                    if (piece != null) piece.SetActive(false);
                }
            }

            if (result != null && scoreTags != null) result.Show(scoreTags.Standings());
            Debug.Log("[ResultSequence] Results are up", this);
            if (curtain != null) yield return curtain.Open(fadeInSeconds);
        }
    }
}
