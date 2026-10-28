using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The suspect lineup on the wall, laid out in Assets/Prefabs/Lineup/Lineup Wall.prefab (move or
    // resize anything there): the title on a black tape at the top, the clues coming in one after
    // another on a cream panel under it (the feature's picture with a tick or a cross), the time left
    // to lock an answer in, and a WANTED poster showing what a right answer pays now. Along the
    // bottom the six suspects stand in the floor's order, each over a cream plate showing what it
    // carries (the floor shows the suspects only). At the reveal the others leave the row, the culprit
    // leaves its plate, glides to its spot and bounces up big, standing whole, and is stamped CAUGHT
    // (RevealCulprit). The poster keeps its question mark.
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

        // One suspect in the row along the bottom: its picture and the things it carries, left to
        // right on its plate (slots left over are hidden).
        [Serializable]
        private sealed class SuspectSlot
        {
            [SerializeField] private RectTransform root;
            [SerializeField] private Image body;
            [SerializeField] private Image[] items;

            public RectTransform Root => root;
            public Image Body => body;
            public Image[] Items => items;
        }

        // How one species' picture sits in its slot in the row: its picture, a size multiplier and a
        // nudge (wall units) from where the slot's body sits in the prefab. Species without an entry
        // show at 1 and no nudge.
        [Serializable]
        private sealed class SpeciesFit
        {
            [SerializeField] private string species = "Cat";
            [SerializeField] private Sprite body;
            [SerializeField, Min(0.1f)] private float scale = 1f;
            [SerializeField] private Vector2 offset;

            public string Species => species;
            public Sprite Body => body;
            public float Scale => scale;
            public Vector2 Offset => offset;
        }

        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text line;
        // Left to right, in the order the clues come.
        [SerializeField] private Card[] cards;
        [SerializeField] private GameObject countdownGroup;
        [SerializeField] private TMP_Text countdown;
        // {0} the seconds left, {1} its minutes and {2} the seconds past the minute ("{1:00}:{2:00}" reads 00:24).
        [SerializeField] private string countdownFormat = "{0}";

        [Header("The suspects, along the bottom")]
        // Left to right, in the floor's order.
        [SerializeField] private SuspectSlot[] lineup;
        [SerializeField, Min(0f)] private float lineupItemGap = 6f;
        // Each species' size and nudge in the row, so every picture can be lined up by hand.
        [SerializeField] private SpeciesFit[] speciesFits;
        // At the reveal (RevealCulprit): the others pop away over leaveSeconds, leaveStagger apart, then
        // the culprit's plate and items go as it glides to culpritSpot over glideSeconds, and it bounces
        // up to culpritSize over bounceSeconds.
        [SerializeField, Min(0.05f)] private float leaveSeconds = 0.55f;
        [SerializeField, Min(0f)] private float leaveStagger = 0.07f;
        [SerializeField, Min(0.05f)] private float glideSeconds = 0.7f;
        [SerializeField, Min(0.05f)] private float bounceSeconds = 0.5f;
        // Where the culprit's slot ends up (wall units from the middle) and how big (its slot's scale):
        // standing whole, big, just left of the middle in front of the clue panel.
        [SerializeField] private Vector2 culpritSpot = new Vector2(-52f, 8f);
        [SerializeField, Min(1f)] private float culpritSize = 2.45f;
        // Slams onto the culprit once it has grown, this far from its spot (wall units); hidden until then.
        [SerializeField] private RectTransform caughtStamp;
        [SerializeField] private Vector2 caughtStampOffset = new Vector2(0f, -20f);

        [Header("Idle motion in the row")]
        // Each suspect breathes (stretching up from its feet by this share) and rocks this many
        // degrees either way, and the things on its plate bob this far (wall units), each slot to
        // its own beat.
        [SerializeField, Range(0f, 0.1f)] private float breathe = 0.025f;
        [SerializeField, Range(0f, 10f)] private float rock = 2f;
        [SerializeField, Min(0f)] private float itemBob = 2.5f;

        [Header("Entrance in the row")]
        // A suspect's entrance (IntroduceSuspect): its plate pops up, it drops onto it from dropHeight
        // as a black silhouette, is lit up after silhouetteSeconds, then what it carries pops up on
        // the plate one after another.
        [SerializeField, Min(0f)] private float dropHeight = 80f;
        [SerializeField, Min(0.05f)] private float dropSeconds = 0.5f;
        [SerializeField, Min(0f)] private float silhouetteSeconds = 0.45f;
        [SerializeField, Min(0.05f)] private float litSeconds = 0.4f;
        [SerializeField, Min(0f)] private float itemGap = 0.12f;

        [Header("Hurry")]
        // In the last hurrySeconds the time turns hurryColour and pops each second.
        [SerializeField, Min(0)] private int hurrySeconds = 5;
        [SerializeField] private Color hurryColour = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private Sprite yes;
        [SerializeField] private Sprite no;

        [Header("What a right answer pays now")]
        // Under the clue cards while answers are open: how much a right answer given now pays.
        [SerializeField] private RectTransform rewardPanel;
        [SerializeField] private TMP_Text rewardText;
        // {0} is the amount.
        [SerializeField] private string rewardFormat = "RIGHT ANSWER NOW: +{0}$";

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
        // Where each slot's body sits in the prefab, which the species' nudge is measured from.
        private Vector2[] bodyRests;
        // Where each slot sits in the prefab; the reveal moves the culprit's, the next game puts it back.
        private Vector2[] slotRests;
        // Where each body and item sits and how big, once the row is filled, for the idle motion.
        private Vector2[] bodyPlaces;
        private Vector3[] bodySizes;
        private Vector2[][] itemPlaces;
        private Color countdownColour;
        private int shownSeconds = -1;
        // Slots whose entrance is over, which the idle motion may move.
        private bool[] settled;
        private Vector3[] badgeSizes;

        // False until the prefab has its clue cards (Tools > Bounty Hunter > Build Lineup Prefabs).
        public bool IsSetUp => cards != null && cards.Length > 0 && Array.TrueForAll(cards, card => card != null && card.Root != null);

        // Where each card sits in the prefab, before it floats about.
        private void Awake()
        {
            if (countdown != null) countdownColour = countdown.color;
            if (lineup != null)
            {
                bodyPlaces = new Vector2[lineup.Length];
                bodySizes = new Vector3[lineup.Length];
                itemPlaces = new Vector2[lineup.Length][];
                bodyRests = new Vector2[lineup.Length];
                slotRests = new Vector2[lineup.Length];
                for (int i = 0; i < lineup.Length; i++)
                {
                    if (lineup[i]?.Root != null) slotRests[i] = lineup[i].Root.anchoredPosition;
                    if (lineup[i]?.Body != null) bodyRests[i] = lineup[i].Body.rectTransform.anchoredPosition;
                }
            }

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
            MoveRow();
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
            ShowReward(-1);
            if (culprit != null) culprit.gameObject.SetActive(false);
            if (caughtStamp != null) caughtStamp.gameObject.SetActive(false);
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

        // True once the prefab has its reward panel (made before it, it has none).
        public bool HasRewardPanel => rewardPanel != null && rewardText != null;

        // What a right answer given now pays; it pops each time it changes. Below zero hides it.
        public void ShowReward(int amount)
        {
            if (rewardPanel == null) return;

            bool shown = amount >= 0;
            if (!shown)
            {
                rewardPanel.gameObject.SetActive(false);
                return;
            }

            if (rewardText != null) rewardText.text = string.Format(rewardFormat, amount);
            rewardPanel.gameObject.SetActive(true);
            StartCoroutine(Pop(rewardPanel));
        }

        // Whole seconds left to lock in; below zero hides it.
        public void SetCountdown(int seconds)
        {
            bool shown = seconds >= 0;
            if (countdownGroup != null) countdownGroup.SetActive(shown);
            if (!shown || countdown == null)
            {
                shownSeconds = -1;
                return;
            }

            countdown.text = string.Format(countdownFormat, seconds, seconds / 60, seconds % 60);
            bool hurry = seconds <= hurrySeconds;
            countdown.color = hurry ? hurryColour : countdownColour;
            if (hurry && seconds != shownSeconds) StartCoroutine(Pop(countdown.rectTransform, 0.25f));
            shownSeconds = seconds;
        }

        // The suspects in the row breathe and rock, and what they carry bobs, each slot to its own beat.
        private void MoveRow()
        {
            if (lineup == null || bodySizes == null) return;

            float time = Time.time;
            for (int i = 0; i < lineup.Length; i++)
            {
                SuspectSlot slot = lineup[i];
                if (slot?.Root == null || !slot.Root.gameObject.activeSelf || settled == null || !settled[i]) continue;

                float beat = time + i * 1.1f;
                if (slot.Body != null)
                {
                    // Stretches up from its feet: the middle rises by half of what it grows.
                    RectTransform body = slot.Body.rectTransform;
                    float stretch = breathe * Mathf.Sin(beat * 2.2f);
                    body.localScale = Vector3.Scale(bodySizes[i], new Vector3(1f, 1f + stretch, 1f));
                    body.anchoredPosition = bodyPlaces[i] + new Vector2(0f, body.rect.height * bodySizes[i].y * stretch * 0.5f);
                    body.localRotation = Quaternion.Euler(0f, 0f, rock * Mathf.Sin(beat * 1.3f));
                }

                Vector2[] places = itemPlaces[i];
                for (int k = 0; slot.Items != null && places != null && k < slot.Items.Length && k < places.Length; k++)
                {
                    Image item = slot.Items[k];
                    if (item == null || !item.gameObject.activeSelf) continue;
                    item.rectTransform.anchoredPosition = places[k] + new Vector2(0f, itemBob * Mathf.Sin(beat * 1.8f + k * 0.8f));
                }
            }
        }

        // True once the prefab has its culprit panel (made before it, it has none).
        public bool HasCulpritPanel => culprit != null && culpritPicture != null;

        // True once the prefab has the row of suspects along the bottom (made before it, it has none).
        public bool HasLineupRow => lineup != null && lineup.Length > 0;

        // True once the row has its per-species sizes (made before them, it has none).
        public bool HasSpeciesFits => speciesFits != null && speciesFits.Length > 0;

        // Puts the suspects in the row, left to right: bodies[i] carrying carried(i). Slots past the
        // last suspect are hidden.
        public void ShowSuspects(IReadOnlyList<Sprite> bodies, Func<int, IReadOnlyList<Sprite>> carried)
        {
            if (lineup == null) return;

            for (int i = 0; i < lineup.Length; i++)
            {
                SuspectSlot slot = lineup[i];
                if (slot?.Root == null) continue;

                bool used = i < bodies.Count;
                slot.Root.gameObject.SetActive(used);
                if (!used) continue;

                slot.Root.localScale = Vector3.one;
                slot.Root.anchoredPosition = slotRests[i];
                // The reveal took the plate away from the last culprit.
                Transform plate = slot.Root.Find("Plate");
                if (plate != null) plate.localScale = Vector3.one;
                if (slot.Body != null)
                {
                    slot.Body.sprite = bodies[i];
                    SetAlpha(slot.Body, 1f);
                    SpeciesFit fit = speciesFits != null ? Array.Find(speciesFits, entry => entry != null && entry.Body == bodies[i]) : null;
                    slot.Body.rectTransform.localScale = Vector3.one * (fit != null ? fit.Scale : 1f);
                    slot.Body.rectTransform.anchoredPosition = bodyRests[i] + (fit != null ? fit.Offset : Vector2.zero);
                    bodyPlaces[i] = slot.Body.rectTransform.anchoredPosition;
                    bodySizes[i] = slot.Body.rectTransform.localScale;
                }

                LayOutItems(slot, carried(i));
                itemPlaces[i] = Array.ConvertAll(slot.Items ?? Array.Empty<Image>(), item => item != null ? item.rectTransform.anchoredPosition : Vector2.zero);
                // Off the wall until it is introduced.
                slot.Root.localScale = Vector3.zero;
            }

            settled = new bool[lineup.Length];
        }

        // How long one suspect's entrance takes, until the last thing it carries has popped up.
        public float EntranceSeconds => Mathf.Max(dropSeconds, silhouetteSeconds) + litSeconds + itemGap * 3f + 0.4f;

        // Brings slot index in: its plate pops up, the suspect drops onto it as a silhouette and is lit
        // up, then what it carries pops up.
        public void IntroduceSuspect(int index)
        {
            if (lineup == null || index < 0 || index >= lineup.Length || lineup[index]?.Root == null) return;
            if (!lineup[index].Root.gameObject.activeSelf) return;
            StartCoroutine(Entrance(index));
        }

        private IEnumerator Entrance(int index)
        {
            SuspectSlot slot = lineup[index];
            var shown = new List<Image>();
            foreach (Image item in slot.Items ?? Array.Empty<Image>())
            {
                if (item == null || !item.gameObject.activeSelf) continue;
                item.rectTransform.localScale = Vector3.zero;
                shown.Add(item);
            }

            StartCoroutine(Pop(slot.Root));
            if (slot.Body != null)
            {
                // Drops in, bouncing on the plate, a black silhouette.
                RectTransform body = slot.Body.rectTransform;
                SetLit(slot.Body, 0f);
                for (float time = 0f; time < dropSeconds; time += Time.deltaTime)
                {
                    body.anchoredPosition = bodyPlaces[index] + new Vector2(0f, dropHeight * (1f - EaseOutBounce(time / dropSeconds)));
                    yield return null;
                }

                body.anchoredPosition = bodyPlaces[index];
                yield return Wait(Mathf.Max(0f, silhouetteSeconds - dropSeconds));

                // The light comes on: its colours fade in as it swells a little.
                for (float time = 0f; time < litSeconds; time += Time.deltaTime)
                {
                    float u = time / litSeconds;
                    SetLit(slot.Body, u);
                    body.localScale = bodySizes[index] * (1f + 0.12f * Mathf.Sin(Mathf.PI * u));
                    yield return null;
                }

                SetLit(slot.Body, 1f);
                body.localScale = bodySizes[index];
            }

            foreach (Image item in shown)
            {
                StartCoroutine(Pop(item.rectTransform));
                yield return Wait(itemGap);
            }

            settled[index] = true;
        }

        // 0 a black silhouette, 1 the picture's own colours (its fade is kept).
        private static void SetLit(Image picture, float lit)
        {
            float shade = Mathf.Clamp01(lit);
            picture.color = new Color(shade, shade, shade, picture.color.a);
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float time = 0f; time < seconds; time += Time.deltaTime) yield return null;
        }

        private static float EaseOutBounce(float u)
        {
            u = Mathf.Clamp01(u);
            const float n = 7.5625f;
            const float d = 2.75f;
            if (u < 1f / d) return n * u * u;
            if (u < 2f / d) return n * (u -= 1.5f / d) * u + 0.75f;
            if (u < 2.5f / d) return n * (u -= 2.25f / d) * u + 0.9375f;
            return n * (u -= 2.625f / d) * u + 0.984375f;
        }

        // The reveal in the row: the others swell a little and pop away one after another, the culprit
        // loses its plate, glides to culpritSpot and bounces up to culpritSize, standing whole.
        public IEnumerator RevealCulprit(int index)
        {
            if (lineup == null || index < 0 || index >= lineup.Length || lineup[index]?.Root == null) yield break;

            var leaving = new List<RectTransform>();
            for (int i = 0; i < lineup.Length; i++)
            {
                if (i != index && lineup[i]?.Root != null && lineup[i].Root.gameObject.activeSelf) leaving.Add(lineup[i].Root);
            }

            float total = leaveSeconds + leaveStagger * Mathf.Max(0, leaving.Count - 1);
            for (float time = 0f; time < total; time += Time.deltaTime)
            {
                for (int k = 0; k < leaving.Count; k++)
                {
                    float u = Mathf.Clamp01((time - k * leaveStagger) / leaveSeconds);
                    leaving[k].localScale = Vector3.one * LeaveCurve(u);
                }

                yield return null;
            }

            foreach (RectTransform slot in leaving) slot.gameObject.SetActive(false);

            // The culprit's plate and what it carries go too, so it stands there whole; then it glides to
            // its spot and bounces up to its size there.
            SuspectSlot culpritSlot = lineup[index];
            RectTransform caught = culpritSlot.Root;
            var under = new List<Transform>();
            Transform plate = caught.Find("Plate");
            if (plate != null) under.Add(plate);
            foreach (Image item in culpritSlot.Items ?? Array.Empty<Image>())
            {
                if (item != null && item.gameObject.activeSelf) under.Add(item.transform);
            }

            Vector2 from = caught.anchoredPosition;
            for (float time = 0f; time < glideSeconds; time += Time.deltaTime)
            {
                float u = time / glideSeconds;
                caught.anchoredPosition = Vector2.Lerp(from, culpritSpot, u * u * (3f - 2f * u));
                foreach (Transform piece in under) piece.localScale = Vector3.one * LeaveCurve(Mathf.Clamp01(time / leaveSeconds));
                yield return null;
            }

            caught.anchoredPosition = culpritSpot;
            foreach (Transform piece in under) piece.localScale = Vector3.zero;

            for (float time = 0f; time < bounceSeconds; time += Time.deltaTime)
            {
                caught.localScale = Vector3.one * Mathf.LerpUnclamped(1f, culpritSize, EaseOutBack(time / bounceSeconds));
                yield return null;
            }

            caught.localScale = Vector3.one * culpritSize;
            if (caughtStamp == null) yield break;

            caughtStamp.anchoredPosition = culpritSpot + caughtStampOffset;
            caughtStamp.SetAsLastSibling();
            yield return Slam(caughtStamp);
        }

        // True once the prefab has the CAUGHT stamp that goes on the culprit (made before it, it has none).
        public bool HasCaughtStamp => caughtStamp != null;

        private static float EaseOutBack(float u)
        {
            const float overshoot = 2.2f;
            float back = Mathf.Clamp01(u) - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }

        // 1 at the start, up to 1.12 a third of the way, then down to nothing.
        private static float LeaveCurve(float u)
        {
            const float peak = 0.3f;
            if (u < peak) return 1f + 0.12f * Mathf.Sin(u / peak * Mathf.PI * 0.5f);
            float down = (u - peak) / (1f - peak);
            return 1.12f * (1f - down * down);
        }

        // The culprit pops up with what it carries, and the CAUGHT stamp slams onto it; the clues and
        // the clock (on 00:00) stay up, so they can be checked against it.
        public void ShowCulprit(Sprite picture, IReadOnlyList<Sprite> carried)
        {
            if (culprit == null) return;

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

        // What the suspect carries side by side, centred where its plate's items sit.
        private void LayOutItems(SuspectSlot slot, IReadOnlyList<Sprite> carried)
        {
            if (slot.Items == null) return;

            var used = new List<Image>();
            for (int i = 0; i < slot.Items.Length; i++)
            {
                Image item = slot.Items[i];
                if (item == null) continue;

                bool shown = i < carried.Count && carried[i] != null;
                item.gameObject.SetActive(shown);
                if (!shown) continue;

                item.sprite = carried[i];
                SetAlpha(item, 1f);
                used.Add(item);
            }

            if (used.Count == 0) return;
            float size = used[0].rectTransform.sizeDelta.x;
            float width = used.Count * size + (used.Count - 1) * lineupItemGap;
            float y = used[0].rectTransform.anchoredPosition.y;
            for (int i = 0; i < used.Count; i++)
            {
                used[i].rectTransform.anchoredPosition = new Vector2(-width * 0.5f + size * 0.5f + i * (size + lineupItemGap), y);
            }
        }

        private static void SetAlpha(Graphic picture, float alpha)
        {
            Color colour = picture.color;
            colour.a = alpha;
            picture.color = colour;
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

        // Grows from nothing past size and settles at it.
        private static IEnumerator Pop(RectTransform piece, float seconds = PopSeconds, float size = 1f)
        {
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                const float overshoot = 1.70158f;
                float back = time / seconds - 1f;
                piece.localScale = Vector3.one * size * (1f + (overshoot + 1f) * back * back * back + overshoot * back * back);
                yield return null;
            }

            piece.localScale = Vector3.one * size;
        }
    }
}
