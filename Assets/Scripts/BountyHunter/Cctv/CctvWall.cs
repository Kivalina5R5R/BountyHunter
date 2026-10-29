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
    // then a banana peel flies out of it onto the screen (CctvBananaPeels). At the reveal every thief
    // peeks out of the bin it ended in and the bag thief's bin is stamped CAUGHT.
    // Sits on the prefab's root, which stays switched off until shown; it goes over the game wall,
    // just behind the fade curtain. Only the screen, the bins and the thieves are needed.
    public sealed class CctvWall : MonoBehaviour
    {
        private const float PopSeconds = 0.4f;
        private const float BumpSeconds = 0.28f;

        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text line;

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

        [Header("Reveal")]
        [SerializeField] private RectTransform stamp;

        private Vector2[] binRests;
        private Vector3[] binSizes;
        private Vector3[] thiefSizes;
        private DateTime clockStart;
        private float clockStartedAt = -1f;

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

            thiefSizes = new Vector3[thieves.Length];
            for (int i = 0; i < thieves.Length; i++)
            {
                if (thieves[i] != null) thiefSizes[i] = thieves[i].rectTransform.localScale;
            }
        }

        private void Update()
        {
            if (recLight != null) recLight.enabled = Mathf.Repeat(Time.time, 1f) < 0.6f;
            if (clock != null && clockStartedAt >= 0f)
            {
                clock.text = clockStart.AddSeconds(Time.time - clockStartedAt).ToString("HH:mm:ss");
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
            for (int i = 0; i < bins.Length; i++)
            {
                bins[i].rectTransform.anchoredPosition = binRests[i];
                bins[i].rectTransform.localScale = binSizes[i];
            }

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
        }

        // Plays the clip on the camera, thief i shown as bodies[i]; lasts clip.Length.
        public IEnumerator Play(CctvClip clip, IReadOnlyList<Sprite> bodies)
        {
            Dress(clip, bodies);
            List<(float Time, int Bin)> landings = Landings(clip);
            int nextLanding = 0;
            float start = Time.time;
            while (Time.time - start < clip.Length)
            {
                float time = Time.time - start;
                ShowFrame(clip, time);
                for (; nextLanding < landings.Count && landings[nextLanding].Time <= time; nextLanding++)
                {
                    if (peels != null) peels.MaybeThrow(binRests[landings[nextLanding].Bin]);
                }

                yield return null;
            }

            ShowFrame(clip, clip.Length);
        }

        // Every thief peeks out of the bin it ended in, one after another, and the bag thief's bin is
        // stamped CAUGHT.
        public IEnumerator Reveal(CctvClip clip, float stagger)
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
                if (stagger > 0f) yield return new WaitForSeconds(stagger);
            }

            if (stamp == null) yield break;
            stamp.anchoredPosition = new Vector2(binRests[clip.Answer].x, stamp.anchoredPosition.y);
            yield return Slam(stamp);
        }

        public void Hide()
        {
            StopAllCoroutines();
            if (peels != null) peels.Clear();
            clockStartedAt = -1f;
            gameObject.SetActive(false);
        }

        // Every time a thief drops into a bin, dive or hop, in the order they happen.
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
