using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fortal.Display
{
    // The kiosk's remote for the HOW TO PLAY board on the wall (TutorialPopup): a small board on the
    // dimmed kiosk telling the players to look at the wall, which page it is on, and BACK and NEXT.
    // BACK reads CLOSE on the first page and NEXT reads the last page's word on the last. Touching the
    // dimmed kiosk round it asks to close. TutorialPopup shows it, moves it and answers its buttons.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class TutorialRemote : MonoBehaviour
    {
        [SerializeField] private RectTransform board;
        // Says which page the wall is on.
        [SerializeField] private TMP_Text counter;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text backLabel;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text nextLabel;
        // The dimmed kiosk round the board; touching it asks to close.
        [SerializeField] private Button backdrop;

        [Header("Words")]
        [SerializeField] private string nextWord = "NEXT";
        [SerializeField] private string backWord = "BACK";
        [SerializeField] private string closeWord = "CLOSE";
        [SerializeField] private string lastWord = "LET'S GO!";
        // {0} is the page on show and {1} how many there are.
        [SerializeField] private string counterWords = "PAGE {0} / {1}";

        // How far the board rises from as it springs up.
        [SerializeField, Min(0f)] private float openRise = 240f;

        private CanvasGroup group;
        private float boardRest;

        public event Action BackPressed;
        public event Action NextPressed;
        public event Action ClosePressed;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (backButton != null) backButton.onClick.AddListener(() => BackPressed?.Invoke());
            if (nextButton != null) nextButton.onClick.AddListener(() => NextPressed?.Invoke());
            if (backdrop != null) backdrop.onClick.AddListener(() => ClosePressed?.Invoke());
            if (board != null) boardRest = board.anchoredPosition.y;
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

        // Faded to alpha; it takes touches while touchable and swallows them until it has faded out.
        public void SetShown(float alpha, bool touchable)
        {
            group.alpha = alpha;
            group.interactable = touchable;
            group.blocksRaycasts = touchable || alpha > 0f;
        }

        // At this scale and tilt, lowered by this share of openRise.
        public void PlaceBoard(float scale, float lowered, float tilt)
        {
            if (board == null) return;
            board.localScale = Vector3.one * scale;
            board.anchoredPosition = new Vector2(board.anchoredPosition.x, boardRest - lowered * openRise);
            board.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        // Shows page index (0 first) of count.
        public void ShowPage(int index, int count)
        {
            if (counter != null) counter.text = string.Format(counterWords, index + 1, count);
            if (backLabel != null) backLabel.text = index == 0 ? closeWord : backWord;
            if (nextLabel != null) nextLabel.text = index == count - 1 ? lastWord : nextWord;
        }
    }
}
