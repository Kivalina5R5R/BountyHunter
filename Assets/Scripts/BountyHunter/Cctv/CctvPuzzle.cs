using System;
using System.Collections;
using System.Collections.Generic;
using Fortal.Players;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace BountyHunter.Thieves
{
    // Mini game 2, the security camera, played between stage 2 and stage 3 (MapSwitcher runs it):
    // the wall plays a security camera clip in which three thieves dive into the bins (four in the prefabs)
    // and hop from bin to bin, faster and faster; only one of them carries the money bag (CctvClip).
    // When the clip ends, players have answerSeconds to stand on the bin on the floor they think the
    // bag is in (held holdSeconds, LineupAnswers, the same as the lineup); they may change their mind
    // the same way until picks close. Then the thieves pop out of their bins on the floor and on the
    // wall, the bag thief's bin lights up and is stamped CAUGHT, and those who picked it get rightPay
    // at their feet with a cheer (a wrong pick pays wrongPay, nothing by default, as in the lineup).
    // It ends on GOTCHA! with the thieves up and moves on afterCaughtSeconds after CAUGHT lands, as the
    // lineup does. totalSeconds only sets when picks close at the latest. The clock on the wall is up
    // from the start, holding the clip's length and the time to pick, and counts down from the
    // moment the thieves jump in; picks still get answerSeconds after the clip. With 10 and then 5
    // seconds left (swapAtSecondsLeft, while the players are picking, never during the clip), two
    // bins swap places on the floor and on the camera together, each keeping what is inside it, so
    // the players must follow the bin they want.
    // Console lines start with [CctvPuzzle].
    public sealed class CctvPuzzle : StageInterlude
    {
        [SerializeField] private UWBPlayerSpawner playerSpawner;
        [SerializeField] private PlayerScoreTag[] scoreTags;
        [SerializeField] private CctvFloor floor;
        [SerializeField] private CctvWall wall;
        // The thieves who can be on the camera (three different ones are picked each game).
        [SerializeField] private Sprite[] thiefBodies;

        [Header("The clip")]
        // How many times a thief hops from one bin to another (3 to 9 plays well).
        [SerializeField, Range(1, 12)] private int hops = 7;
        [SerializeField, Min(0.1f)] private float firstHopSeconds = 1.1f;
        // Each hop (and the pause after it) takes this much of the one before.
        [SerializeField, Range(0.5f, 1f)] private float speedUp = 0.85f;
        [SerializeField, Min(0f)] private float hopGapSeconds = 0.25f;
        [SerializeField, Min(0f)] private float enterStagger = 0.35f;
        [SerializeField, Min(0.1f)] private float enterSeconds = 0.8f;
        // Still frames after the last hop, before picks open.
        [SerializeField, Min(0f)] private float endHoldSeconds = 0.6f;

        [Header("Timing (seconds)")]
        // Picks close by totalSeconds - revealSeconds after the screen comes up, however long the clip
        // is: by default 3 s of words, a clip of about 8.4 s and 18 s to pick fit well inside it.
        [SerializeField, Min(10f)] private float totalSeconds = 40f;
        // The opening, before the clip: the camera counts in 3, 2, 1 over it.
        [SerializeField, Min(0f)] private float introSeconds = 3f;
        [SerializeField, Min(1f)] private float answerSeconds = 18f;
        // The clock's starting time (the clip and answerSeconds) is rounded up to a whole number of
        // these, the extra going to picking: 27 shows as 30.
        [SerializeField, Min(1f)] private float clockRoundTo = 10f;
        // Two bins swap places at each of these times before picks close (seconds left on the clock),
        // each swap taking swapSeconds.
        [SerializeField] private float[] swapAtSecondsLeft = { 10f, 5f };
        [SerializeField, Min(0.1f)] private float swapSeconds = 0.8f;
        // Between one thief popping out at the reveal and the next.
        [SerializeField, Min(0f)] private float revealStagger = 0.3f;
        // Picks close at least this long before totalSeconds.
        [FormerlySerializedAs("outroSeconds")]
        [SerializeField, Min(0f)] private float revealSeconds = 4f;
        // From CAUGHT landing on the bag thief until the game moves on (the same as the lineup's).
        [SerializeField, Min(0f)] private float afterCaughtSeconds = 0.5f;

        [Header("Picking")]
        [SerializeField, Min(0.1f)] private float holdSeconds = 1.5f;
        // A player counts as on a ring from enterRadius and off it only past exitRadius (metres).
        [SerializeField, Min(0.1f)] private float enterRadius = 0.6f;
        [SerializeField, Min(0.1f)] private float exitRadius = 0.75f;

        [Header("Money")]
        [SerializeField] private int rightPay = 15;
        // Zero or below: a wrong pick takes this off (0 costs nothing).
        [SerializeField] private int wrongPay = 0;
        [SerializeField] private Color gainColor = new Color(1f, 0.82f, 0.25f, 1f);
        [SerializeField] private Color lossColor = new Color(1f, 0.35f, 0.3f, 1f);
        [SerializeField] private TMP_FontAsset moneyFont;
        [SerializeField] private Material moneyMaterial;

        [Header("Locked in")]
        // Bursts under a player's feet the moment their pick locks in.
        [SerializeField] private string lockedWord = "LOCKED!";
        [SerializeField] private Color lockedColor = new Color(0.35f, 0.8f, 1f, 1f);

        [Header("Words on the wall")]
        [SerializeField] private string watchHeading = "WATCH THE MONEY BAG!";
        [SerializeField] private string watchLine = "WHICH BIN DOES IT END UP IN?";
        [SerializeField] private string askHeading = "WHERE IS THE MONEY BAG?";
        [SerializeField] private string askLine = "STAND ON A BIN TO PICK IT";
        [SerializeField] private string revealHeading = "GOTCHA!";

        private readonly List<(PlayerScoreTag Tag, int Change)> winnings = new List<(PlayerScoreTag Tag, int Change)>();
        private readonly List<Sprite> cast = new List<Sprite>();
        // The camera counts in from this before the thieves run in.
        private const int CountInFrom = 3;
        private readonly System.Random random = new System.Random();
        private CctvClip clip;
        private LineupAnswers answers;
        private bool answersOpen;
        private float answersCloseAt;
        // The clock is counting down: from the thieves jumping in until picks close.
        private bool clockRunning;
        private bool skipping;
        // False when the floor or the wall is not set up; the mini game then lets the game go on.
        private bool ready;

        // Moments for its sounds (CctvSfx): a player steps onto a bin's ring and locks in, picks
        // close, and the right picks are paid (how many).
        public event Action RingSteppedOn;
        public event Action AnswerLocked;
        public event Action AnswersClosed;
        public event Action<int> Settled;

        public override void Begin()
        {
            ready = floor != null && wall != null && floor.IsSetUp && wall.IsSetUp && thiefBodies != null && thiefBodies.Length > 0;
            if (!ready)
            {
                Debug.LogError("[CctvPuzzle] The CCTV prefabs are not set up, so the mini game is skipped: run Tools > Bounty Hunter > Build CCTV Prefabs and save the scene", this);
                return;
            }

            // The bins are as many as the prefabs have; the camera and the floor should have the same.
            int binCount = Mathf.Min(wall.BinCount, floor.BinCount);
            if (wall.BinCount != floor.BinCount) Debug.LogWarning($"[CctvPuzzle] Cctv Wall has {wall.BinCount} bins but Cctv Floor has {floor.BinCount}; only the first {binCount} are used", this);
            int thieves = Mathf.Min(wall.ThiefCount, thiefBodies.Length, binCount - 1);
            var maker = new CctvClipMaker(random, firstHopSeconds, speedUp, hopGapSeconds, enterStagger, enterSeconds, endHoldSeconds);
            clip = maker.Make(binCount, thieves, hops);
            PickCast(thieves);

            floor.Show();
            wall.ShowIntro(watchHeading, watchLine);
            answers = new LineupAnswers(floor.StandPoint, floor.Count, enterRadius, exitRadius, holdSeconds);
            answers.PlayerLocked += OnPlayerPicked;
            answers.PlayerEntered += OnPlayerEntered;
            answersOpen = false;
            clockRunning = false;
            skipping = false;
            Debug.Log($"[CctvPuzzle] {clip.Hops.Count} hops over {clip.Length:0.0}s; the bag ends in bin {clip.Answer + 1}", this);
        }

        public override IEnumerator Run()
        {
            if (!ready) yield break;

            float start = Time.time;
            float ends = start + totalSeconds;
            // The clock is up from the start, holding the clip's length and the time to pick, and runs
            // from the moment the thieves jump in, so it counts the clip down too; picks still get
            // answerSeconds (or a little more, for a round number on the clock) once the clip is over.
            float clockSeconds = Mathf.Ceil((clip.Length + answerSeconds) / clockRoundTo) * clockRoundTo;
            clockSeconds = Mathf.Max(0f, Mathf.Min(clockSeconds, totalSeconds - revealSeconds - introSeconds));
            wall.SetCountdown(Mathf.CeilToInt(clockSeconds));
            // 3, 2, 1 on the screen through the opening, so the players are watching as the thieves run in.
            Coroutine countIn = wall.StartCoroutine(wall.CountIn(CountInFrom, introSeconds / CountInFrom));
            yield return Until(start + introSeconds);
            // A coroutine that ends at once (nothing to count, or skipped already) comes back null.
            if (skipping && countIn != null) wall.StopCoroutine(countIn);

            answersCloseAt = Mathf.Min(Time.time + clockSeconds, ends - revealSeconds);
            clockRunning = true;
            Coroutine playing = wall.StartCoroutine(wall.Play(clip, cast));
            yield return Until(Time.time + clip.Length);
            if (skipping && playing != null) wall.StopCoroutine(playing);

            wall.ShowLines(askHeading, askLine);
            answersOpen = true;
            Coroutine shuffling = StartCoroutine(Shuffle());
            yield return Until(answersCloseAt);
            if (shuffling != null) StopCoroutine(shuffling);
            answersOpen = false;
            clockRunning = false;
            AnswersClosed?.Invoke();
            // Time is up: the clock stays on 0 until the reveal takes it down.
            wall.SetCountdown(0);
            ClearRings();

            wall.ShowLines(revealHeading, string.Empty);
            Coroutine wallReveal = wall.StartCoroutine(wall.Reveal(clip, revealStagger));
            yield return floor.Reveal(clip, cast);
            yield return wallReveal;
            Settle();

            yield return Until(Time.time + afterCaughtSeconds);
        }

        public override void End()
        {
            if (!ready) return;

            // The score tags are back on now, so they count the money up and the leader star follows.
            foreach ((PlayerScoreTag tag, int change) in winnings) tag.SetScore(tag.Score + change);
            winnings.Clear();

            if (answers != null)
            {
                answers.PlayerLocked -= OnPlayerPicked;
                answers.PlayerEntered -= OnPlayerEntered;
            }

            answersOpen = false;
            clockRunning = false;
            StopAllCoroutines();
            floor.Hide();
            wall.Hide();
        }

        public override RectTransform WallPanel => wall != null ? (RectTransform)wall.transform : null;

        public override void Skip()
        {
            skipping = true;
        }

        private void Update()
        {
            if (!answersOpen || answers == null || playerSpawner == null) return;

            // LineupAnswers only takes picks once something is up; here that is the whole time picks are open.
            answers.Track(playerSpawner.SpawnedPlayers, Time.deltaTime, 1);
            for (int ring = 0; ring < floor.Count; ring++) floor.ShowProgress(ring, answers.Fill(ring));
        }

        // At each of swapAtSecondsLeft, two random bins swap places on the floor and on the camera
        // together.
        private IEnumerator Shuffle()
        {
            int count = Mathf.Min(floor.Count, wall.BinCount);
            if (count < 2 || swapAtSecondsLeft == null) yield break;

            var times = new List<float>(swapAtSecondsLeft);
            times.Sort((x, y) => y.CompareTo(x));
            foreach (float secondsLeft in times)
            {
                while (!skipping && Time.time < answersCloseAt - secondsLeft) yield return null;
                if (skipping || Time.time + swapSeconds > answersCloseAt) yield break;

                int a = random.Next(count);
                int b = (a + 1 + random.Next(count - 1)) % count;
                wall.StartCoroutine(wall.SwapBins(a, b, swapSeconds));
                floor.StartCoroutine(floor.SwapBins(a, b, swapSeconds));
            }
        }

        // Different thieves for the clip, in a random order.
        private void PickCast(int count)
        {
            var pool = new List<Sprite>(thiefBodies);
            pool.RemoveAll(body => body == null);
            cast.Clear();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int at = random.Next(pool.Count);
                cast.Add(pool[at]);
                pool.RemoveAt(at);
            }
        }

        // Waits until the time, showing the seconds left to pick while the clock runs; a skip ends
        // every wait at once.
        private IEnumerator Until(float time)
        {
            while (Time.time < time && !skipping)
            {
                if (clockRunning) wall.SetCountdown(Mathf.CeilToInt(Mathf.Max(0f, answersCloseAt - Time.time)));
                yield return null;
            }
        }

        // A bin wobbles as a player steps onto its ring, so they know they are on it.
        private void OnPlayerEntered(UWBPlayerController player, int bin)
        {
            floor.Bounce(bin);
            RingSteppedOn?.Invoke();
        }

        private void OnPlayerPicked(UWBPlayerController player, int bin)
        {
            floor.ShowPick(player.transform);
            floor.Bounce(bin);
            FloorDamage.Play(player.transform.position, lockedWord, lockedColor, moneyFont, moneyMaterial, transform);
            AnswerLocked?.Invoke();
            Debug.Log($"[CctvPuzzle] Player {player.PlayerId} picked bin {bin + 1}", this);
        }

        private void ClearRings()
        {
            for (int ring = 0; ring < floor.Count; ring++) floor.ShowProgress(ring, 0f);
        }

        // Shows what each pick earned or cost at the player's feet now, with a cheer for the right
        // ones; the score tags take it at the end (they are put away during the mini game).
        private void Settle()
        {
            winnings.Clear();
            int rightAnswers = 0;
            foreach (KeyValuePair<int, LineupAnswers.Answer> pair in answers.Locked)
            {
                bool right = pair.Value.Suspect == clip.Answer;
                int change = right ? rightPay : wrongPay;
                Debug.Log($"[CctvPuzzle] Player {pair.Key} {(right ? "was right" : "was wrong")}", this);
                PlayerScoreTag tag = TagOf(pair.Key);
                if (tag != null && change != 0) winnings.Add((tag, change));
                if (!playerSpawner.TryGetPlayer(pair.Key, out UWBPlayerController player) || player == null) continue;

                if (right) rightAnswers++;
                if (right) LineupCheer.Play(player.transform.position, transform);
                if (change > 0) FloorDamage.Play(player.transform.position, $"+{change}$", gainColor, moneyFont, moneyMaterial, transform);
                if (change < 0) FloorDamage.Play(player.transform.position, $"{change}$", lossColor, moneyFont, moneyMaterial, transform);
            }

            Settled?.Invoke(rightAnswers);
        }

        private PlayerScoreTag TagOf(int playerId)
        {
            if (scoreTags == null) return null;
            return Array.Find(scoreTags, tag => tag != null && tag.InPlay && tag.PlayerNumber == playerId);
        }
    }
}
