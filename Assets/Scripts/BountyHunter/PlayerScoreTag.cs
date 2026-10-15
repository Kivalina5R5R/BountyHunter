using TMPro;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // One player's tag on the wall: "PLAYER 3" in the player's colour on the black tab and the
    // score on the paper below. Display only; whatever keeps score calls SetScore. Changes made in
    // the Inspector show straight away, outside Play too.
    public sealed class PlayerScoreTag : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text score;
        [SerializeField, Min(1)] private int playerNumber = 1;
        [SerializeField] private Color playerColor = new Color(1f, 0.35f, 0.3f, 1f);
        // {0} is the player number.
        [SerializeField] private string labelFormat = "PLAYER {0}";
        [SerializeField, Min(0)] private int startingScore;

        private int currentScore;

        public int PlayerNumber => playerNumber;
        public int Score => currentScore;

        private void Awake()
        {
            currentScore = startingScore;
            Refresh();
        }

        public void SetPlayer(int number, Color color)
        {
            playerNumber = Mathf.Max(1, number);
            playerColor = color;
            Refresh();
        }

        public void SetScore(int value)
        {
            currentScore = Mathf.Max(0, value);
            Refresh();
        }

        private void OnValidate()
        {
            // In Play the live score stays; outside Play the tag previews the starting score.
            if (!Application.isPlaying) currentScore = startingScore;
            Refresh();
        }

        private void Refresh()
        {
            if (label != null)
            {
                label.text = string.Format(labelFormat, playerNumber);
                label.color = playerColor;
            }

            if (score != null) score.text = currentScore.ToString();
        }
    }
}
