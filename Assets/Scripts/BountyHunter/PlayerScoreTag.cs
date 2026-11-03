using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // One player's tag on the wall: "PLAYER 3" in the player's colour on the black tab and the
    // score on the paper below. Display only; whatever keeps score calls SetScore. In Play a new
    // score counts up (or down) like a rolling counter, a "+N$" (or "-N$") floats up from it and the
    // number pops in the gain or loss colour (the same for every player) at the end.
    // While the dogs' pictures are away (the boss chase) the character's face shows before the name
    // (ShowFace), so everyone still knows whose tag it is.
    // Changes made in the Inspector show straight away, outside Play too.
    public sealed class PlayerScoreTag : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text score;
        [SerializeField, Min(1)] private int playerNumber = 1;
        [SerializeField] private Color playerColor = new Color(1f, 0.35f, 0.3f, 1f);
        // {0} is the player number.
        [SerializeField] private string labelFormat = "PLAYER {0}";
        [SerializeField, Min(0)] private int startingScore;

        [Header("Score change")]
        [SerializeField, Min(0.05f)] private float countSeconds = 0.7f;
        // Most steps the count takes; bigger jumps count in larger steps.
        [SerializeField, Min(1)] private int maxCountSteps = 12;
        [SerializeField, Min(0.02f)] private float stepRollSeconds = 0.09f;
        [SerializeField, Range(0f, 1f)] private float popSwell = 0.3f;
        [SerializeField, Min(0.05f)] private float popSeconds = 0.55f;
        // Warm bounty gold, readable on the paper.
        [SerializeField] private Color flashColor = new Color(0.86f, 0.47f, 0.05f, 1f);
        // {0} is the points gained; empty for no floating text.
        [SerializeField] private string gainFormat = "+{0}$";
        // Used instead when points are lost (a bomb).
        [SerializeField] private Color lossColor = new Color(0.78f, 0.1f, 0.08f, 1f);
        [SerializeField] private string lossFormat = "-{0}$";

        [Header("Face")]
        // The character's face before the name, off until ShowFace; may be empty.
        [SerializeField] private Image face;
        // The name's left and right edges (this tag's units) while the face shows, shrinking to fit.
        [SerializeField] private Vector2 nameBesideFace = new Vector2(-222f, 78f);
        [SerializeField, Min(1f)] private float smallestNameSize = 44f;

        [Header("Leader badge")]
        // Where the leader's star (LeaderStar) sits, in this tag's own units: centred on the tag's ragged
        // left edge and big enough that its arms hide the whole edge, from under the name tab to the bottom.
        [SerializeField] private Vector2 badgeSpot = new Vector2(-362f, -30f);
        [SerializeField, Min(1f)] private float badgeSize = 440f;
        private int currentScore;
        private int shownScore;
        private Coroutine counting;
        private Color changeColor;
        private Vector2 namePosition;
        private Vector2 nameSize;
        private bool nameAutoSized;
        private float nameFontSize;
        private bool faceShown;
        // Set by PlaceBadgeOn: where the star goes for now instead of badgeSpot.
        private RectTransform badgePlace;

        public int PlayerNumber => playerNumber;
        public int Score => currentScore;

        // Raised in Play whenever SetScore changes the score.
        public event Action<PlayerScoreTag> ScoreChanged;

        // Where the leader's star goes on this tag, in world space, and its width there.
        public void GetBadge(out Vector3 worldPosition, out float worldSize)
        {
            if (badgePlace != null)
            {
                worldPosition = badgePlace.TransformPoint(badgePlace.rect.center);
                worldSize = badgePlace.rect.width * badgePlace.lossyScale.x;
                return;
            }

            worldPosition = transform.TransformPoint(badgeSpot);
            worldSize = badgeSize * transform.lossyScale.x;
        }

        // The star goes on place (its middle, its width the star's size) until this is called again
        // with null, which puts it back at badgeSpot.
        public void PlaceBadgeOn(RectTransform place)
        {
            badgePlace = place;
        }

        private void Awake()
        {
            currentScore = startingScore;
            Refresh();
        }

        // The character's face for ShowFace.
        public void SetFace(Sprite picture)
        {
            if (face != null && picture != null) face.sprite = picture;
        }

        // Shows the character's face before the name (the name moves over to make room), or hides it.
        public void ShowFace(bool show)
        {
            if (face == null || label == null || show == faceShown) return;

            faceShown = show;
            face.gameObject.SetActive(show);
            RectTransform name = label.rectTransform;
            if (show)
            {
                namePosition = name.anchoredPosition;
                nameSize = name.sizeDelta;
                nameAutoSized = label.enableAutoSizing;
                nameFontSize = label.fontSize;
                float width = nameBesideFace.y - nameBesideFace.x;
                name.anchoredPosition = new Vector2(nameBesideFace.x + width * 0.5f, namePosition.y);
                name.sizeDelta = new Vector2(width, nameSize.y);
                label.fontSizeMax = nameFontSize;
                label.fontSizeMin = Mathf.Min(smallestNameSize, nameFontSize);
                label.enableAutoSizing = true;
                return;
            }

            name.anchoredPosition = namePosition;
            name.sizeDelta = nameSize;
            label.enableAutoSizing = nameAutoSized;
            label.fontSize = nameFontSize;
        }

        public void SetPlayer(int number, Color color)
        {
            playerNumber = Mathf.Max(1, number);
            playerColor = color;
            Refresh();
        }

        // False once the tag is taken off the wall because nobody plays its character.
        public bool InPlay { get; private set; } = true;

        // Gives the tag to this player, keeping its own colour.
        public void AssignPlayer(int number)
        {
            InPlay = true;
            gameObject.SetActive(true);
            SetPlayer(number, playerColor);
        }

        // Nobody plays this character: the tag leaves the wall and scores for no one.
        public void Unassign()
        {
            InPlay = false;
            gameObject.SetActive(false);
        }

        public void SetScore(int value)
        {
            int gained = Mathf.Max(0, value) - currentScore;
            currentScore = Mathf.Max(0, value);
            if (!Application.isPlaying || score == null || !isActiveAndEnabled)
            {
                Refresh();
                return;
            }

            if (gained == 0) return;

            ScoreChanged?.Invoke(this);

            changeColor = gained > 0 ? flashColor : lossColor;
            string format = gained > 0 ? gainFormat : lossFormat;
            if (!string.IsNullOrEmpty(format)) ScorePopup.Play(score, string.Format(format, Mathf.Abs(gained)), changeColor);
            if (counting != null) StopCoroutine(counting);
            counting = StartCoroutine(CountRoutine());
        }

        private IEnumerator CountRoutine()
        {
            RollingLabel roller = RollingLabel.On(score);
            int distance = Mathf.Abs(currentScore - shownScore);
            int stepSize = Mathf.Max(1, Mathf.CeilToInt(distance / (float)maxCountSteps));
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / (float)stepSize));
            float interval = countSeconds / steps;
            while (shownScore != currentScore)
            {
                shownScore = (int)Mathf.MoveTowards(shownScore, currentScore, stepSize);
                roller.Roll(shownScore.ToString(), Mathf.Min(stepRollSeconds, interval));
                yield return new WaitForSeconds(interval);
            }

            roller.Pop(popSwell, changeColor, popSeconds);
            counting = null;
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

            // A count in progress keeps the number it is showing.
            if (counting != null) return;

            shownScore = currentScore;
            if (score != null) score.text = currentScore.ToString();
        }
    }
}
