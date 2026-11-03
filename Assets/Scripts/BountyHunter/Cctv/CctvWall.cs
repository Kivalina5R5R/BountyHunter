using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Mini game 2 on the wall, laid out in Assets/Prefabs/Cctv/Cctv Wall.prefab (move or resize
    // anything there): a title and a line over a security camera screen with a blinking REC light
    // and the camera's clock, the bins in a row along the
    // bottom of the screen and, on the right, the seconds left to pick a bin. The camera plays a
    // CctvClip: the thieves run in and dive into the bins, then hop from bin to bin, the bag thief
    // with the money bag over its head; a bin bumps as a thief leaves or lands in it, and now and
    // then a banana peel flies out of it onto the screen (CctvBananaPeels). Near the end of the
    // picking two bins at a time swap places (SwapBins, one hopping over, the other ducking under),
    // as the floor's do, each keeping what is inside it. At the reveal every thief
    // peeks out of the bin it ended in, then the others and the bins pop away and the bag thief glides
    // to the middle, bounces up big and is stamped CAUGHT, as in the lineup.
    // Sits on the prefab's root, which stays switched off until shown; it goes over the game wall,
    // just behind the fade curtain. Only the screen, the bins and the thieves are needed.
    public sealed class CctvWall : MonoBehaviour
    {
        private const float PopSeconds = 0.4f;
        private const float BumpSeconds = 0.28f;

        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text line;
        // The 3, 2, 1 in the middle of the screen before the thieves run in, falling in as the
        // floor's countdown at the start of the game does; optional.
        [SerializeField] private TMP_Text countIn;
        [SerializeField, Min(1f)] private float countInDropFrom = 3f;
        [SerializeField, Range(0.05f, 0.6f)] private float countInFallPart = 0.25f;
        [SerializeField, Range(0f, 0.4f)] private float countInSquash = 0.15f;

        [Header("The camera")]
        // Left to right, the same order as the bins on the floor. Each stays where and as big as it
        // is in the prefab, with its own picture; the thieves go in and out at its middle.
        [SerializeField] private Image[] bins;
        // Each thief's picture; its money bag is a child of it, switched on only for the bag thief.
        [SerializeField] private Image[] thieves;
        [SerializeField] private Image[] bags;
        // Where a thief comes from at its entrance (screen units, beside the bins).
        [SerializeField] private Vector2 entrance = new Vector2(-620f, -20f);
        // How far below a bin's middle a thief inside it sits, and how high it jumps over a hop.
        [SerializeField] private float sunkBelow = 25f;
        [SerializeField, Min(0f)] private float hopHeight = 170f;
        // How far a thief rises out of its bin at the reveal.
        [SerializeField] private float peekAbove = 95f;
        // How much a bin squashes when a thief leaves or lands in it.
        [SerializeField, Range(0f, 0.5f)] private float bump = 0.14f;
        // How high one of two swapping bins hops over the other, and how far the other ducks under
        // (screen units).
        [SerializeField, Min(0f)] private float swapHop = 70f;
        [SerializeField, Min(0f)] private float swapDuck = 20f;
        // Both optional: the camera's own blinking REC light and its running clock.
        [SerializeField] private Graphic recLight;
        [SerializeField] private TMP_Text clock;
        // Peels that fly out of the bins over the screen as the thieves land in them; optional.
        [SerializeField] private CctvBananaPeels peels;

        [Header("Picking")]
        [SerializeField] private GameObject countdownGroup;
        [SerializeField] private TMP_Text countdown;
        // {0} the seconds left, {1} its minutes and {2} the seconds past the minute ("{1:00}:{2:00}" reads 00:24).
        [SerializeField] private string countdownFormat = "{0}";
        // The last seconds to pick that count down out loud (CctvSfx).
        [SerializeField, Min(0)] private int hurrySeconds = 5;

        [Header("Reveal")]
        [SerializeField] private RectTransform stamp;
        // Once every thief has peeked out, the other thieves and the bins swell a little and pop away,
        // leaveStagger apart, over leaveSeconds; the bag thief then glides to culpritSpot over
        // glideSeconds and bounces up to culpritSize (times its own size) over bounceSeconds, the same
        // as the lineup's culprit, and CAUGHT slams onto it.
        [SerializeField, Min(0.05f)] private float leaveSeconds = 0.55f;
        [SerializeField, Min(0f)] private float leaveStagger = 0.07f;
        [SerializeField, Min(0.05f)] private float glideSeconds = 0.7f;
        [SerializeField, Min(0.05f)] private float bounceSeconds = 0.5f;
        // Screen units from the middle; low enough that the money bag over its head stays on the screen.
        [SerializeField] private Vector2 culpritSpot = new Vector2(0f, -30f);
        [SerializeField, Min(1f)] private float culpritSize = 2f;
        // Where CAUGHT lands, from the culprit's spot.
        [SerializeField] private Vector2 stampOffset = new Vector2(0f, -40f);

        private Vector2[] binRests;
        // The count-in's own size, read the first time it counts.
        private Vector3? countInSize;
        // Where each bin stands at the start of every game, however they were swapped last time.
        private Vector2[] binHomes;
        private Vector3[] binSizes;
        private Vector3[] thiefSizes;
        // The screen's pieces in their prefab order; the reveal brings the culprit and the stamp to
        // the front, and the next showing puts the thieves back behind the bins they dive into.
        private Transform[] screenOrder;
        private DateTime clockStart;
        private float clockStartedAt = -1f;
        private int clockShownSecond = -1;
        private int shownSeconds = -1;

        // Moments for its sounds (CctvSfx): a thief jumping (into the picture or out of a bin) and
        // landing in a bin (which), a thief peeking out at the reveal, CAUGHT landing, and each of the
        // last hurrySeconds to pick going by (seconds left).
        public event Action ThiefJumped;
        public event Action<int> ThiefLanded;
        public event Action ThiefPeeked;
        public event Action CaughtStamped;
        public event Action<int> HurrySecond;
        // Two bins start swapping places; a number of the count-in falls in (3, 2, 1).
        public event Action BinsSwapping;
        public event Action<int> CountInShown;

        // False until the prefab has its bins and thieves (Tools > Bounty Hunter > Build CCTV Prefabs).
        public bool IsSetUp => bins != null && bins.Length > 0 && thieves != null && thieves.Length > 0
                               && Array.TrueForAll(bins, bin => bin != null) && Array.TrueForAll(thieves, thief => thief != null);

        // True once the prefab is in the monitor design, the one with the banana peels.
        public bool HasBananaPeels => peels != null;

        public int BinCount => bins != null ? bins.Length : 0;
        public int ThiefCount => thieves != null ? thieves.Length : 0;

        private void Awake()
        {
            binRests = new Vector2[bins.Length];
            binSizes = new Vector3[bins.Length];
            for (int i = 0; i < bins.Length; i++)
            {
                if (bins[i] == null) continue;
                binRests[i] = bins[i].rectTransform.anchoredPosition;
                binSizes[i] = bins[i].rectTransform.localScale;
            }

            binHomes = (Vector2[])binRests.Clone();

            thiefSizes = new Vector3[thieves.Length];
            for (int i = 0; i < thieves.Length; i++)
            {
                if (thieves[i] != null) thiefSizes[i] = thieves[i].rectTransform.localScale;
            }

            Transform screen = thieves.Length > 0 && thieves[0] != null ? thieves[0].transform.parent : null;
            screenOrder = new Transform[screen != null ? screen.childCount : 0];
            for (int i = 0; i < screenOrder.Length; i++) screenOrder[i] = screen.GetChild(i);
        }

        private void Update()
        {
            if (recLight != null) recLight.enabled = Mathf.Repeat(Time.time, 1f) < 0.6f;
            if (clock != null && clockStartedAt >= 0f)
            {
                // Only when the shown second changes, so the label is not rebuilt every frame.
                int second = Mathf.FloorToInt(Time.time - clockStartedAt);
                if (second != clockShownSecond)
                {
                    clockShownSecond = second;
                    clock.text = clockStart.AddSeconds(second).ToString("HH:mm:ss");
                }
            }
        }

        // Brings the wall up with its opening words, the bins empty and the camera's clock running.
        public void ShowIntro(string heading, string subline)
        {
            gameObject.SetActive(true);
            StopAllCoroutines();
            SetCountdown(-1);
            if (peels != null) peels.Clear();
            if (stamp != null) stamp.gameObject.SetActive(false);
            foreach (Image thief in thieves) thief.gameObject.SetActive(false);
            for (int i = 0; i < screenOrder.Length; i++) screenOrder[i].SetSiblingIndex(i);
            for (int i = 0; i < bins.Length; i++)
            {
                binRests[i] = binHomes[i];
                bins[i].rectTransform.anchoredPosition = binRests[i];
                bins[i].rectTransform.localScale = binSizes[i];
            }

            ShowCountIn(false);
            clockStart = DateTime.Today.AddHours(23).AddMinutes(UnityEngine.Random.Range(5, 55)).AddSeconds(UnityEngine.Random.Range(0, 60));
            clockStartedAt = Time.time;
            ShowLines(heading, subline);
        }

        public void ShowLines(string heading, string subline)
        {
            if (line != null) line.text = subline;
            if (title == null) return;

            title.text = heading;
            StartCoroutine(Pop(title.rectTransform));
        }

        // Whole seconds left to pick; below zero hides it.
        public void SetCountdown(int seconds)
        {
            bool shown = seconds >= 0;
            if (countdownGroup != null) countdownGroup.SetActive(shown);
            if (shown && countdown != null) countdown.text = string.Format(countdownFormat, seconds, seconds / 60, seconds % 60);
            if (shown && seconds <= hurrySeconds && seconds != shownSeconds) HurrySecond?.Invoke(seconds);
            shownSeconds = seconds;
        }

        // Plays the clip on the camera, thief i shown as bodies[i]; lasts clip.Length.
        public IEnumerator Play(CctvClip clip, IReadOnlyList<Sprite> bodies)
        {
            Dress(clip, bodies);
            List<(float Time, int Bin)> landings = Landings(clip);
            List<float> takeoffs = Takeoffs(clip);
            int nextLanding = 0;
            int nextTakeoff = 0;
            int nextHop = 0;
            float start = Time.time;
            while (Time.time - start < clip.Length)
            {
                float time = Time.time - start;
                ShowFrame(clip, time);
                for (; nextTakeoff < takeoffs.Count && takeoffs[nextTakeoff] <= time; nextTakeoff++) ThiefJumped?.Invoke();
                // As the bag thief jumps, a peel tries to hide the bin it is heading for.
                for (; nextHop < clip.Hops.Count && clip.Hops[nextHop].Start <= time; nextHop++)
                {
                    CctvHop hop = clip.Hops[nextHop];
                    if (peels != null && hop.Thief == clip.BagThief) peels.Block(binRests[hop.To], BinMouth(hop.To));
                }

                for (; nextLanding < landings.Count && landings[nextLanding].Time <= time; nextLanding++)
                {
                    int bin = landings[nextLanding].Bin;
                    ThiefLanded?.Invoke(bin);
                    if (peels != null) peels.MaybeThrow(binRests[bin]);
                }

                yield return null;
            }

            ShowFrame(clip, clip.Length);
        }

        // Counts in from, from - 1, ... 1 in the middle of the screen, each number falling in and fading
        // over stepSeconds, so the players know when the thieves will run in.
        public IEnumerator CountIn(int from, float stepSeconds)
        {
            if (countIn == null || stepSeconds <= 0f) yield break;

            Vector3 size = countInSize ??= countIn.rectTransform.localScale;
            countIn.transform.SetAsLastSibling();
            for (int n = from; n >= 1; n--)
            {
                countIn.text = n.ToString();
                ShowCountIn(true);
                CountInShown?.Invoke(n);
                for (float time = 0f; time < stepSeconds; time += Time.deltaTime)
                {
                    CountdownDrop.Pose(time / stepSeconds, countInFallPart, countInDropFrom, countInSquash, out Vector3 scale, out float alpha);
                    countIn.rectTransform.localScale = Vector3.Scale(size, scale);
                    countIn.alpha = alpha;
                    yield return null;
                }
            }

            ShowCountIn(false);
        }

        private void ShowCountIn(bool shown)
        {
            if (countIn != null) countIn.gameObject.SetActive(shown);
        }

        // Bins a and b swap places over seconds, a hopping over b while b ducks under; each keeps its
        // own place from then on, so the reveal finds the thieves where their bins went.
        public IEnumerator SwapBins(int a, int b, float seconds)
        {
            if (a < 0 || b < 0 || a >= bins.Length || b >= bins.Length || a == b) yield break;

            BinsSwapping?.Invoke();
            RectTransform first = bins[a].rectTransform;
            RectTransform second = bins[b].rectTransform;
            Vector2 from = binRests[a];
            Vector2 to = binRests[b];
            // The one hopping over is drawn in front of the other (and still behind the peels).
            first.SetSiblingIndex(Mathf.Max(first.GetSiblingIndex(), second.GetSiblingIndex()));
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                float u = time / seconds;
                float eased = u * u * (3f - 2f * u);
                float arc = Mathf.Sin(u * Mathf.PI);
                first.anchoredPosition = Vector2.Lerp(from, to, eased) + Vector2.up * (swapHop * arc);
                second.anchoredPosition = Vector2.Lerp(to, from, eased) + Vector2.down * (swapDuck * arc);
                float squash = bump * Mathf.Sin(u * Mathf.PI * 2f);
                first.localScale = Vector3.Scale(binSizes[a], new Vector3(1f - squash, 1f + squash, 1f));
                second.localScale = Vector3.Scale(binSizes[b], new Vector3(1f + squash, 1f - squash, 1f));
                yield return null;
            }

            binRests[a] = to;
            binRests[b] = from;
            first.anchoredPosition = binRests[a];
            second.anchoredPosition = binRests[b];
            first.localScale = binSizes[a];
            second.localScale = binSizes[b];
        }

        // Every thief peeks out of the bin it ended in, one after another; then the others and the bins
        // pop away, the bag thief glides to the middle and bounces up big, and CAUGHT slams onto it.
        public IEnumerator Reveal(CctvClip clip, float stagger)
        {
            yield return PeekOut(clip, stagger);
            int culprit = clip.BagThief;
            if (culprit < 0 || culprit >= clip.Thieves || culprit >= thieves.Length)
            {
                yield return StampBin(clip.Answer);
                yield break;
            }

            yield return new WaitForSeconds(PopSeconds);
            yield return ClearAround(culprit);
            RectTransform caught = thieves[culprit].rectTransform;
            yield return Spotlight(caught, thiefSizes[culprit]);
            if (stamp == null) yield break;

            stamp.anchoredPosition = culpritSpot + stampOffset;
            stamp.SetAsLastSibling();
            yield return Slam(stamp);
            CaughtStamped?.Invoke();
        }

        private IEnumerator PeekOut(CctvClip clip, float stagger)
        {
            SetCountdown(-1);
            if (peels != null) peels.Clear();
            for (int thief = 0; thief < clip.Thieves && thief < thieves.Length; thief++)
            {
                int bin = clip.FinalBins[thief];
                RectTransform picture = thieves[thief].rectTransform;
                picture.gameObject.SetActive(true);
                picture.anchoredPosition = binRests[bin] + new Vector2(0f, peekAbove);
                picture.localRotation = Quaternion.identity;
                StartCoroutine(Pop(picture, PopSeconds, thiefSizes[thief]));
                ThiefPeeked?.Invoke();
                if (stagger > 0f) yield return new WaitForSeconds(stagger);
            }
        }

        // The old ending, kept for a clip without a bag thief: CAUGHT on the answer's bin.
        private IEnumerator StampBin(int bin)
        {
            if (stamp == null) yield break;
            stamp.anchoredPosition = new Vector2(binRests[bin].x, stamp.anchoredPosition.y);
            yield return Slam(stamp);
            CaughtStamped?.Invoke();
        }

        // The other thieves on show, then the bins, swell a little and pop away one after another.
        private IEnumerator ClearAround(int culprit)
        {
            var leaving = new List<RectTransform>();
            var sizes = new List<Vector3>();
            for (int i = 0; i < thieves.Length; i++)
            {
                if (i == culprit || !thieves[i].gameObject.activeSelf) continue;
                leaving.Add(thieves[i].rectTransform);
                sizes.Add(thieves[i].rectTransform.localScale);
            }

            foreach (Image bin in bins)
            {
                leaving.Add(bin.rectTransform);
                sizes.Add(bin.rectTransform.localScale);
            }

            float total = leaveSeconds + leaveStagger * Mathf.Max(0, leaving.Count - 1);
            for (float time = 0f; time < total; time += Time.deltaTime)
            {
                for (int k = 0; k < leaving.Count; k++)
                {
                    float u = Mathf.Clamp01((time - k * leaveStagger) / leaveSeconds);
                    leaving[k].localScale = sizes[k] * LeaveCurve(u);
                }

                yield return null;
            }

            foreach (RectTransform piece in leaving) piece.localScale = Vector3.zero;
        }

        // In front of everything, the culprit glides to its spot and bounces up to its size there.
        private IEnumerator Spotlight(RectTransform caught, Vector3 size)
        {
            caught.SetAsLastSibling();
            caught.localRotation = Quaternion.identity;
            Vector2 from = caught.anchoredPosition;
            for (float time = 0f; time < glideSeconds; time += Time.deltaTime)
            {
                float u = time / glideSeconds;
                caught.anchoredPosition = Vector2.Lerp(from, culpritSpot, u * u * (3f - 2f * u));
                yield return null;
            }

            caught.anchoredPosition = culpritSpot;
            for (float time = 0f; time < bounceSeconds; time += Time.deltaTime)
            {
                caught.localScale = size * Mathf.LerpUnclamped(1f, culpritSize, EaseOutBack(time / bounceSeconds));
                yield return null;
            }

            caught.localScale = size * culpritSize;
        }

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

        public void Hide()
        {
            StopAllCoroutines();
            ShowCountIn(false);
            if (peels != null) peels.Clear();
            clockStartedAt = -1f;
            gameObject.SetActive(false);
        }

        // Every time a thief leaps, running in or hopping out of a bin, in the order they happen.
        private static List<float> Takeoffs(CctvClip clip)
        {
            var takeoffs = new List<float>();
            for (int thief = 0; thief < clip.Thieves; thief++) takeoffs.Add(clip.EnterStart(thief));
            foreach (CctvHop hop in clip.Hops) takeoffs.Add(hop.Start);
            takeoffs.Sort();
            return takeoffs;
        }

        // Every time a thief drops into a bin, dive or hop, in the order they happen.
        // Just over the top of the bin, where a thief diving in shows last.
        private Vector2 BinMouth(int bin)
        {
            RectTransform picture = bins[bin].rectTransform;
            return binRests[bin] + Vector2.up * (picture.rect.height * Mathf.Abs(binSizes[bin].y) * 0.45f);
        }

        private static List<(float Time, int Bin)> Landings(CctvClip clip)
        {
            var landings = new List<(float Time, int Bin)>();
            for (int thief = 0; thief < clip.Thieves; thief++) landings.Add((clip.EnterStart(thief) + clip.EnterSeconds, clip.StartBins[thief]));
            foreach (CctvHop hop in clip.Hops) landings.Add((hop.Start + hop.Seconds, hop.To));
            landings.Sort((a, b) => a.Time.CompareTo(b.Time));
            return landings;
        }

        private void Dress(CctvClip clip, IReadOnlyList<Sprite> bodies)
        {
            for (int i = 0; i < thieves.Length; i++)
            {
                bool used = i < clip.Thieves;
                thieves[i].gameObject.SetActive(false);
                if (!used) continue;

                if (i < bodies.Count && bodies[i] != null) thieves[i].sprite = bodies[i];
                if (bags != null && i < bags.Length && bags[i] != null) bags[i].gameObject.SetActive(i == clip.BagThief);
            }
        }

        // Where everything is at time seconds into the clip.
        private void ShowFrame(CctvClip clip, float time)
        {
            for (int thief = 0; thief < clip.Thieves && thief < thieves.Length; thief++) PlaceThief(clip, thief, time);
            for (int bin = 0; bin < binRests.Length; bin++)
            {
                float squash = bump * BinBump(clip, bin, time);
                bins[bin].rectTransform.localScale = Vector3.Scale(binSizes[bin], new Vector3(1f + squash, 1f - squash, 1f));
            }
        }

        // Out in the open while running in or hopping, hidden inside a bin the rest of the time.
        private void PlaceThief(CctvClip clip, int thief, float time)
        {
            RectTransform picture = thieves[thief].rectTransform;
            float enter = clip.EnterStart(thief);
            if (time >= enter && time <= enter + clip.EnterSeconds)
            {
                Fly(picture, thief, entrance, Sunk(clip.StartBins[thief]), (time - enter) / clip.EnterSeconds);
                return;
            }

            foreach (CctvHop hop in clip.Hops)
            {
                if (hop.Thief != thief || time < hop.Start || time > hop.Start + hop.Seconds) continue;

                Fly(picture, thief, Sunk(hop.From), Sunk(hop.To), (time - hop.Start) / hop.Seconds);
                return;
            }

            picture.gameObject.SetActive(false);
        }

        private void Fly(RectTransform picture, int thief, Vector2 from, Vector2 to, float u)
        {
            picture.gameObject.SetActive(true);
            u = Mathf.Clamp01(u);
            picture.anchoredPosition = Vector2.Lerp(from, to, u) + new Vector2(0f, hopHeight * Mathf.Sin(Mathf.PI * u));
            // Faces the way it goes, leaning into the jump.
            float facing = to.x >= from.x ? 1f : -1f;
            Vector3 size = thiefSizes[thief];
            picture.localScale = new Vector3(Mathf.Abs(size.x) * facing, size.y, size.z);
            picture.localRotation = Quaternion.Euler(0f, 0f, -facing * 12f * Mathf.Cos(Mathf.PI * u));
        }

        private Vector2 Sunk(int bin)
        {
            return binRests[bin] - new Vector2(0f, sunkBelow);
        }

        // 0 to 1: how much bin is bumping now, from thieves leaving or landing in it.
        private static float BinBump(CctvClip clip, int bin, float time)
        {
            float most = 0f;
            for (int thief = 0; thief < clip.Thieves; thief++)
            {
                if (clip.StartBins[thief] == bin) most = Mathf.Max(most, Bump(time - clip.EnterStart(thief) - clip.EnterSeconds));
            }

            foreach (CctvHop hop in clip.Hops)
            {
                if (hop.From == bin) most = Mathf.Max(most, Bump(time - hop.Start));
                if (hop.To == bin) most = Mathf.Max(most, Bump(time - hop.Start - hop.Seconds));
            }

            return most;
        }

        private static float Bump(float since)
        {
            if (since < 0f || since > BumpSeconds) return 0f;
            return Mathf.Sin(Mathf.PI * since / BumpSeconds);
        }

        // Hidden at first, then slams down from big to its own size.
        private static IEnumerator Slam(RectTransform piece)
        {
            piece.gameObject.SetActive(true);
            const float slamSeconds = 0.22f;
            for (float time = 0f; time < slamSeconds; time += Time.deltaTime)
            {
                float u = time / slamSeconds;
                piece.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, u * u);
                yield return null;
            }

            piece.localScale = Vector3.one;
        }

        private static IEnumerator Pop(RectTransform piece, float seconds = PopSeconds)
        {
            return Pop(piece, seconds, Vector3.one);
        }

        private static IEnumerator Pop(RectTransform piece, float seconds, Vector3 size)
        {
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                const float overshoot = 1.70158f;
                float back = time / seconds - 1f;
                piece.localScale = size * (1f + (overshoot + 1f) * back * back * back + overshoot * back * back);
                yield return null;
            }

            piece.localScale = size;
        }
    }
}
