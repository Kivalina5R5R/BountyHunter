using System;
using System.Collections;
using System.Collections.Generic;
using Fortal.Displays;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Teaches the game on the wall, with no buttons, in two layers:
    // - how to play: the HOW TO PLAY board (howToPlayBoard), opened only from the kiosk (the Fortal
    //   launcher's Tutorial button, during the intro); the first rest takes it down if still up;
    // - each stage's and boss phase's own mechanics: a rest of briefSeconds before it (Brief), once
    //   the stage is in sight, a card fading in (and out at the end, over fadeSeconds) in the middle
    //   of the wall with only that part's new mechanics and a countdown, while the
    //   clock waits and no thief or boss is out (whoever calls Brief holds those back until it ends).
    //   Nothing stays up while the part plays; the wall is left to the posters, the clock and the scores.
    // The cards are in the game's language (GameLanguage, TutorialPage): English first; B or the
    // kiosk's TH/EN button swaps every line in place to Thai and back. They are built at run
    // time on their own canvas over the wall, under the cut-ins and the fade curtain. Thai lines use
    // thaiFont (made into a dynamic TMP font when the game starts), English lines englishFont.
    public sealed class WallTutorial : MonoBehaviour
    {
        // Over the wall's pictures, under the mini game cut-ins and boss clips (90) and the curtain (100).
        private const int SortingOrder = 80;

        [SerializeField] private RectTransform wall;
        // The HOW TO PLAY board on the wall (pages that turn by themselves), opened from the kiosk;
        // a rest closes it if it is still up. May be empty.
        [FormerlySerializedAs("launcherBoard")]
        [SerializeField] private Fortal.Displays.TutorialPopup howToPlayBoard;
        [SerializeField] private Font thaiFont;
        [SerializeField] private TMP_FontAsset englishFont;
        [SerializeField, Min(0f)] private float briefSeconds = 10f;
        // The card fades in as the rest starts and out as it ends, over this long each.
        [SerializeField, Min(0f)] private float fadeSeconds = 0.45f;
        [SerializeField] private TutorialPage[] pages = TutorialPage.Defaults();

        [Header("Look (wall canvas units)")]
        [SerializeField] private Color panelColour = new Color(0.05f, 0.07f, 0.16f, 0.86f);
        [SerializeField] private Color headingColour = new Color(1f, 0.82f, 0.25f, 1f);
        [SerializeField] private Color textColour = Color.white;
        [SerializeField] private Color countdownColour = new Color(0.78f, 0.86f, 1f, 1f);
        [SerializeField] private Vector2 cardSize = new Vector2(860f, 330f);
        [SerializeField, Min(4f)] private float textSize = 26f;
        // {0} the whole seconds left of the rest.
        [SerializeField] private string englishCountdown = "STARTS IN {0}";
        [SerializeField] private string thaiCountdown = "เริ่มใน {0}";

        private TMP_FontAsset thaiAsset;
        private RectTransform layer;
        private CanvasGroup layerFade;
        private GameObject card;
        // The card up now, so a change of language can build it again in the other one.
        private TutorialPage cardPage;
        private TMP_Text countdown;
        private int countdownShown = -1;

        // "en" or "th": the language the cards are shown in.
        public string Language => GameLanguage.Code;

        private static bool IsThai => GameLanguage.IsThai;

        public float BriefSeconds => briefSeconds;

        private void Awake()
        {
            thaiAsset = GameLanguage.ThaiFontAsset(thaiFont, englishFont);
            GameLanguage.Changed += OnLanguageChanged;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame) GameLanguage.Toggle();
        }

        private void OnDestroy()
        {
            GameLanguage.Changed -= OnLanguageChanged;
        }

        // Sets the game's language ("th" or "en"); the cards follow it through GameLanguage.Changed.
        public void SetLanguage(string language)
        {
            GameLanguage.Set(language);
        }

        // Rebuilds a card that is up in the new language.
        private void OnLanguageChanged()
        {
            if (cardPage != null) BuildCard(cardPage);
        }

        // The rest before part: its mechanics in the middle of the wall with a countdown for
        // briefSeconds (cut short when skip says so); nothing stays up once it plays.
        public IEnumerator Brief(TutorialPart part, Func<bool> skip)
        {
            if (howToPlayBoard != null && howToPlayBoard.IsOpen) howToPlayBoard.Close();
            TutorialPage page = PageOf(part);
            if (page == null || wall == null) yield break;

            BuildCard(page);
            layerFade.alpha = 0f;
            float started = Time.time;
            float ends = started + briefSeconds;
            while (Time.time < ends && (skip == null || !skip()))
            {
                layerFade.alpha = fadeSeconds > 0f ? Mathf.Clamp01((Time.time - started) / fadeSeconds) : 1f;
                int left = Mathf.CeilToInt(ends - Time.time);
                if (left != countdownShown)
                {
                    countdownShown = left;
                    countdown.text = string.Format(IsThai ? thaiCountdown : englishCountdown, left);
                }

                yield return null;
            }

            float from = layerFade.alpha;
            float fadeEnds = Time.time + fadeSeconds;
            while (Time.time < fadeEnds && (skip == null || !skip()))
            {
                layerFade.alpha = from * Mathf.Clamp01((fadeEnds - Time.time) / Mathf.Max(0.01f, fadeSeconds));
                yield return null;
            }

            RemoveCard();
        }

        // Takes a rest card cut short down.
        public void Clear()
        {
            RemoveCard();
        }

        private TutorialPage PageOf(TutorialPart part)
        {
            if (pages == null) return null;
            return Array.Find(pages, page => page != null && page.Part == part);
        }

        private TMP_FontAsset Font => IsThai && thaiAsset != null ? thaiAsset : englishFont;

        private RectTransform Layer()
        {
            if (layer != null) return layer;

            var root = new GameObject("Wall Tutorial", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            root.layer = wall.gameObject.layer;
            layer = (RectTransform)root.transform;
            layer.SetParent(wall, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = Vector2.zero;
            layer.offsetMax = Vector2.zero;
            var canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            layerFade = root.GetComponent<CanvasGroup>();
            layerFade.blocksRaycasts = false;
            layerFade.interactable = false;
            return layer;
        }

        // The rest card in the middle: heading, the mechanics, the countdown (ticked down by Brief).
        private void BuildCard(TutorialPage page)
        {
            RemoveCard();
            cardPage = page;
            countdownShown = -1;

            var box = new GameObject("Brief Card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            box.layer = wall.gameObject.layer;
            var rect = (RectTransform)box.transform;
            rect.SetParent(Layer(), false);
            rect.sizeDelta = cardSize;
            var back = box.GetComponent<Image>();
            back.color = panelColour;
            back.raycastTarget = false;
            var list = box.GetComponent<VerticalLayoutGroup>();
            list.padding = new RectOffset(28, 28, 18, 18);
            list.spacing = 8f;
            list.childAlignment = TextAnchor.MiddleCenter;
            list.childControlWidth = true;
            list.childControlHeight = true;
            list.childForceExpandHeight = false;
            card = box;

            Label(rect, IsThai ? page.ThaiHeading : page.EnglishHeading, textSize * 1.35f, headingColour, TextAlignmentOptions.Center);
            foreach (TutorialLine line in page.Lines) Rule(rect, line);
            countdown = Label(rect, string.Empty, textSize * 0.9f, countdownColour, TextAlignmentOptions.Center);
        }

        // One mechanic: its icon (if any) beside its line in the language shown.
        private void Rule(RectTransform parent, TutorialLine line)
        {
            var row = new GameObject("Rule", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.layer = parent.gameObject.layer;
            row.transform.SetParent(parent, false);
            var across = row.GetComponent<HorizontalLayoutGroup>();
            across.spacing = textSize * 0.5f;
            across.childAlignment = TextAnchor.MiddleLeft;
            across.childControlWidth = true;
            across.childControlHeight = true;
            across.childForceExpandWidth = false;
            across.childForceExpandHeight = false;

            if (line.Icon != null)
            {
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                icon.layer = row.layer;
                icon.transform.SetParent(row.transform, false);
                var picture = icon.GetComponent<Image>();
                picture.sprite = line.Icon;
                picture.preserveAspect = true;
                picture.raycastTarget = false;
                var size = icon.GetComponent<LayoutElement>();
                size.preferredWidth = textSize * 2.2f;
                size.preferredHeight = textSize * 2.2f;
            }

            TMP_Text words = Label((RectTransform)row.transform, IsThai ? line.Thai : line.English, textSize, textColour, TextAlignmentOptions.Left);
            words.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private TMP_Text Label(RectTransform parent, string text, float size, Color colour, TextAlignmentOptions alignment)
        {
            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.layer = parent.gameObject.layer;
            label.transform.SetParent(parent, false);
            var words = label.GetComponent<TextMeshProUGUI>();
            if (Font != null) words.font = Font;
            words.text = text;
            words.fontSize = size;
            words.color = colour;
            words.alignment = alignment;
            words.textWrappingMode = TextWrappingModes.Normal;
            words.raycastTarget = false;
            return words;
        }

        private void RemoveCard()
        {
            if (card != null)
            {
                // Hidden at once: Destroy only takes it away at the end of the frame.
                card.SetActive(false);
                Destroy(card);
            }

            card = null;
            cardPage = null;
            countdown = null;
        }
    }
}
