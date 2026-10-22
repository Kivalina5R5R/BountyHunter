using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The suspect lineup on the wall, laid out in Assets/Prefabs/Lineup/Lineup Wall.prefab (move or
    // resize anything there): a title and a line under it, the witness (the bunny officer) on the
    // left, the clues coming in one after another as cards in the middle (the feature's picture, a
    // tick or a cross, and a word or two) and, on the right, the seconds left to lock an answer in.
    // At the reveal the clue cards go and the culprit comes up in the middle, stamped CAUGHT, with
    // what it carries on a strip under it that fits however many things that is.
    // Sits on the prefab's root, which stays switched off until shown; it goes over the game wall,
    // just behind the fade curtain. Only the clue cards are needed: the title, the line, the
    // countdown and each card's icon, badge and label may be taken out of the prefab.
    public sealed class LineupWall : MonoBehaviour
    {
        private const float PopSeconds = 0.4f;
        // The culprit comes in slower than the cards, so its arrival reads.
        private const float CulpritPopSeconds = 0.6f;

        // One clue card's pieces.
        [Serializable]
        private sealed class Card
        {
            [SerializeField] private RectTransform root;
            [SerializeField] private Image icon;
            [SerializeField] private Image badge;
            [SerializeField] private TMP_Text label;

            public RectTransform Root => root;
            public Image Icon => icon;
            public Image Badge => badge;
            public TMP_Text Label => label;
        }

        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text line;
        // Left to right, in the order the clues come.
        [SerializeField] private Card[] cards;
        [SerializeField] private GameObject countdownGroup;
        [SerializeField] private TMP_Text countdown;
        [SerializeField] private Sprite yes;
        [SerializeField] private Sprite no;

        [Header("The culprit, at the reveal")]
        [SerializeField] private RectTransform culprit;
        [SerializeField] private Image culpritPicture;
        [SerializeField] private RectTransform culpritStamp;
        // The strip under the culprit, sized to what it carries.
        [SerializeField] private RectTransform culpritStrip;
        // What it carries, left to right on the strip; slots left over are hidden.
        [SerializeField] private Image[] culpritItems;
        // Between the things on the strip, and round them to the strip's edge (wall units).
        [SerializeField, Min(0f)] private float culpritItemGap = 12f;
        [SerializeField, Min(0f)] private float culpritStripPadding = 10f;

        // Each clue card floats gently up and down this far (wall units), to its own beat; what is on
        // it rides along. Its tick or cross also swells by badgePulse of its size now and then.
        [SerializeField, Min(0f)] private float cardFloat = 5f;
        [SerializeField, Range(0f, 0.5f)] private float badgePulse = 0.15f;

        private Vector2[] cardRests;
        private Vector3[] badgeSizes;

        // False until the prefab has its clue cards (Tools > Bounty Hunter > Build Lineup Prefabs).
        public bool IsSetUp => cards != null && cards.Length > 0 && Array.TrueForAll(cards, card => card != null && card.Root != null);

        // Where each card sits in the prefab, before it floats about.
        private void Awake()
        {
            if (cards == null) return;

            cardRests = new Vector2[cards.Length];
            badgeSizes = new Vector3[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i]?.Root != null) cardRests[i] = cards[i].Root.anchoredPosition;
                if (cards[i]?.Badge != null) badgeSizes[i] = cards[i].Badge.rectTransform.localScale;
            }
        }

        private void Update()
        {
            if (cardRests == null) return;

            float time = Time.time;
            for (int i = 0; i < cards.Length; i++)
            {
                Card card = cards[i];
                if (card?.Root == null || !card.Root.gameObject.activeSelf) continue;

                float beat = time + i * 1.3f;
                card.Root.anchoredPosition = cardRests[i] + new Vector2(0f, cardFloat * Mathf.Sin(beat * 1.6f));
                if (card.Badge == null) continue;

                // A quick swell every couple of seconds rather than a constant throb; the badge sits on
                // the card, so it floats along with it.
                float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(beat * 2.6f)), 8f);
                card.Badge.rectTransform.localScale = badgeSizes[i] * (1f + badgePulse * pulse);
            }
        }

        // Brings the panel up with its opening words and no clues yet.
        public void ShowIntro(string heading, string subline)
        {
            gameObject.SetActive(true);
            StopAllCoroutines();
            foreach (Card card in cards) card.Root.gameObject.SetActive(false);
            SetCountdown(-1);
            if (culprit != null) culprit.gameObject.SetActive(false);
            ShowLines(heading, subline);
        }

        public void ShowLines(string heading, string subline)
        {
            if (line != null) line.text = subline;
            if (title == null) return;

            title.text = heading;
            StartCoroutine(Pop(title.rectTransform));
        }

        // Clue number slot (0 first): its picture, a tick when the culprit has it or a cross when not.
        public void ShowClue(int slot, Sprite icon, bool has, string label)
        {
            if (slot < 0 || slot >= cards.Length) return;

            Card card = cards[slot];
            if (card.Icon != null)
            {
                card.Icon.sprite = icon;
                card.Icon.enabled = icon != null;
            }

            if (card.Badge != null) card.Badge.sprite = has ? yes : no;
            if (card.Label != null) card.Label.text = label;
            card.Root.gameObject.SetActive(true);
            StartCoroutine(Pop(card.Root));
        }

        // Empties the title and the line under it.
        public void ClearLines()
        {
            if (title != null) title.text = string.Empty;
            if (line != null) line.text = string.Empty;
        }

        // Whole seconds left to lock in; below zero hides it.
        public void SetCountdown(int seconds)
        {
            bool shown = seconds >= 0;
            if (countdownGroup != null) countdownGroup.SetActive(shown);
            if (shown && countdown != null) countdown.text = seconds.ToString();
        }

        // True once the prefab has its culprit panel (made before it, it has none).
        public bool HasCulpritPanel => culprit != null && culpritPicture != null;

        // The clue cards go, the culprit pops up with what it carries, and the CAUGHT stamp slams onto it.
        public void ShowCulprit(Sprite picture, IReadOnlyList<Sprite> carried)
        {
            if (culprit == null) return;

            SetCountdown(-1);
            foreach (Card card in cards)
            {
                if (card?.Root != null) card.Root.gameObject.SetActive(false);
            }

            if (culpritPicture != null) culpritPicture.sprite = picture;
            LayOutCulpritItems(carried);

            culprit.gameObject.SetActive(true);
            StartCoroutine(Pop(culprit, CulpritPopSeconds));
            if (culpritStamp != null) StartCoroutine(Slam(culpritStamp));
        }

        public void Hide()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        // The things it carries side by side, centred on the strip, which is just wide enough for them.
        private void LayOutCulpritItems(IReadOnlyList<Sprite> carried)
        {
            var used = new List<Image>();
            for (int i = 0; culpritItems != null && i < culpritItems.Length; i++)
            {
                Image item = culpritItems[i];
                if (item == null) continue;

                bool shown = i < carried.Count && carried[i] != null;
                item.gameObject.SetActive(shown);
                if (!shown) continue;

                item.sprite = carried[i];
                used.Add(item);
            }

            if (culpritStrip != null) culpritStrip.gameObject.SetActive(used.Count > 0);
            if (used.Count == 0) return;

            float size = used[0].rectTransform.sizeDelta.x;
            float width = used.Count * size + (used.Count - 1) * culpritItemGap;
            float y = culpritStrip != null ? culpritStrip.anchoredPosition.y : used[0].rectTransform.anchoredPosition.y;
            float centre = culpritStrip != null ? culpritStrip.anchoredPosition.x : 0f;
            for (int i = 0; i < used.Count; i++)
            {
                used[i].rectTransform.anchoredPosition = new Vector2(centre - width * 0.5f + size * 0.5f + i * (size + culpritItemGap), y);
            }

            if (culpritStrip != null) culpritStrip.sizeDelta = new Vector2(width + culpritStripPadding * 2f, culpritStrip.sizeDelta.y);
        }

        // Hidden at first, then slams down from big to its own size as the culprit settles.
        private static IEnumerator Slam(RectTransform stamp)
        {
            stamp.gameObject.SetActive(false);
            yield return new WaitForSeconds(CulpritPopSeconds * 0.8f);
            stamp.gameObject.SetActive(true);
            const float slamSeconds = 0.22f;
            for (float time = 0f; time < slamSeconds; time += Time.deltaTime)
            {
                float u = time / slamSeconds;
                stamp.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, u * u);
                yield return null;
            }

            stamp.localScale = Vector3.one;
        }

        private static IEnumerator Pop(RectTransform piece, float seconds = PopSeconds)
        {
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                const float overshoot = 1.70158f;
                float back = time / seconds - 1f;
                piece.localScale = Vector3.one * (1f + (overshoot + 1f) * back * back * back + overshoot * back * back);
                yield return null;
            }

            piece.localScale = Vector3.one;
        }
    }
}
