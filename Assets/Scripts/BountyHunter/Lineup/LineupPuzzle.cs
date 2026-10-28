using System;
using System.Collections;
using System.Collections.Generic;
using Fortal.Players;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace BountyHunter.Thieves
{
    // Mini game 1, the suspect lineup, played between stage 1 and stage 2 (MapSwitcher runs it):
    // six suspects stand in a row on the floor and the witness on the wall gives four clues, the
    // first once the suspects have come in pair by pair (the clock waits for them) and then one
    // every clueGapSeconds. A player locks in the
    // suspect they think did it by standing on its ring (LineupAnswers), and may change their mind
    // the same way until answers close; the answer pays by how many clues were up when it was last
    // given; it is a bonus, so a wrong answer or none costs nothing. Then the culprit is shown and stamped
    // CAUGHT, the money shows at each player's feet with a cheer for those who got it right, and
    // afterCaughtSeconds later the game goes on to the next stage. It always lasts the same
    // time, however fast the players answer. Console lines start with [LineupPuzzle].
    public sealed class LineupPuzzle : StageInterlude
    {
        [SerializeField] private UWBPlayerSpawner playerSpawner;
        [SerializeField] private PlayerScoreTag[] scoreTags;
        [SerializeField] private LineupFloor floor;
        [SerializeField] private LineupWall wall;
        // The species that can stand in the lineup, with their art.
        [SerializeField] private LineupLook[] looks;
        [SerializeField] private LineupFeatureArt[] features;

        [Header("Timing (seconds)")]
        // The time to answer is extraAnswerSeconds + clueGapSeconds for each clue after the first +
        // lastClueSeconds: 3 + 3 x 9 + 10 = 40 seconds on the clock.
        // The suspects come in pair by pair (each on the wall and on the floor together) this far
        // apart, and the first clue comes this long after the last one has finished coming in,
        // before the clock starts.
        [SerializeField, Min(0f)] private float suspectGapSeconds = 0.65f;
        [SerializeField, Min(0f)] private float afterLineupSeconds = 1f;
        // Added to the time to answer, on top of the clues' own.
        [FormerlySerializedAs("introSeconds")]
        [SerializeField, Min(0f)] private float extraAnswerSeconds = 3f;
        [SerializeField, Min(0.5f)] private float clueGapSeconds = 9f;
        // From the last clue until answers close.
        [SerializeField, Min(0f)] private float lastClueSeconds = 10f;
        // From CAUGHT landing on the culprit until the game moves on.
        [SerializeField, Min(0f)] private float afterCaughtSeconds = 0.5f;

        [Header("Locking in")]
        [SerializeField, Min(0.1f)] private float holdSeconds = 1.5f;
        // A player counts as on a ring from enterRadius and off it only past exitRadius (metres).
        [SerializeField, Min(0.1f)] private float enterRadius = 0.6f;
        [SerializeField, Min(0.1f)] private float exitRadius = 0.75f;

        [Header("Money")]
        // What a right answer pays, by how many clue cards were up when it was last given: 1 card 20$,
        // 2 cards 15$, 3 cards 10$, all 4 cards 5$.
        [SerializeField] private int[] payByCardsUp = { 20, 15, 10, 5 };
        [SerializeField] private Color gainColor = new Color(1f, 0.82f, 0.25f, 1f);
        [SerializeField] private TMP_FontAsset moneyFont;
        [SerializeField] private Material moneyMaterial;

        [Header("Locked in")]
        // Bursts under a player's feet the moment their answer locks in (it says nothing of which).
        [SerializeField] private string lockedWord = "LOCKED!";
        [SerializeField] private Color lockedColor = new Color(0.35f, 0.8f, 1f, 1f);

        [Header("Words on the wall")]
        [SerializeField] private string askHeading = "WHO IS THE BIG BOSS?";
        [SerializeField] private string askLine = "STAND ON A SUSPECT TO LOCK IN - THE SOONER, THE MORE $";

        private readonly List<LineupSpecies> species = new List<LineupSpecies>();
        // What each answer earned or cost, settled at the reveal and handed to the score tags at the end.
        private readonly List<(PlayerScoreTag Tag, int Change)> winnings = new List<(PlayerScoreTag Tag, int Change)>();
        private readonly System.Random random = new System.Random();
        private LineupCaseGenerator maker;
        private LineupCase lineup;
        private LineupAnswers answers;
        private int cluesShown;
        private bool answersOpen;
        // The rings follow the players from the suspects' entrance until answers close, so someone
        // who walks onto a ring before the first clue does not have to step off it again; nobody can
        // lock in before the first clue (cluesShown is 0).
        private bool watching;
        private float answersCloseAt;
        private bool skipping;
        // False when the floor or the wall is not set up; the mini game then lets the game go on.
        private bool ready;

        // Moments for its sounds: a clue goes up, a player locks in, the culprit is shown.
        public event Action<int> SuspectIntroduced;
        public event Action<int> ClueShown;
        public event Action AnswerLocked;
        public event Action Revealed;

        private void Awake()
        {
            foreach (LineupLook look in looks) species.Add(look.ToSpecies());
            maker = new LineupCaseGenerator(species, random);
        }

        public override void Begin()
        {
            ready = floor != null && wall != null && floor.IsSetUp && wall.IsSetUp;
            if (!ready)
            {
                Debug.LogError("[LineupPuzzle] The lineup prefabs are not set up, so the mini game is skipped: run Tools > Bounty Hunter > Build Lineup Prefabs and save the scene", this);
                return;
            }

            lineup = maker.Make();
            floor.Show(lineup, looks);
            wall.ShowIntro(askHeading, askLine);
            var bodies = new List<Sprite>(lineup.Suspects.Count);
            foreach (LineupSuspect suspect in lineup.Suspects) bodies.Add(looks[suspect.Species].Body);
            wall.ShowSuspects(bodies, floor.CarriedBy);
            answers = new LineupAnswers(floor.StandPoint, floor.Count, enterRadius, exitRadius, holdSeconds);
            answers.PlayerLocked += OnPlayerLocked;
            answers.PlayerEntered += OnPlayerEntered;
            cluesShown = 0;
            answersOpen = false;
            skipping = false;
            Debug.Log($"[LineupPuzzle] The culprit is the {lineup.SpeciesOf(lineup.Culprit).Name}, in place {lineup.Culprit + 1}", this);
        }

        public override IEnumerator Run()
        {
            if (!ready) yield break;

            // The suspects come in pair by pair, wall and floor together, while the clock waits on
            // the full time and nobody can answer yet.
            watching = true;
            float answerSeconds = extraAnswerSeconds + (lineup.Clues.Count - 1) * clueGapSeconds + lastClueSeconds;
            wall.SetCountdown(Mathf.CeilToInt(answerSeconds));
            for (int i = 0; i < floor.Count; i++)
            {
                if (i > 0) yield return Until(Time.time + suspectGapSeconds);
                floor.Introduce(i);
                wall.IntroduceSuspect(i);
                SuspectIntroduced?.Invoke(i);
            }

            yield return Until(Time.time + Mathf.Max(wall.EntranceSeconds, floor.EntranceSeconds) + afterLineupSeconds);

            // The first clue is up the moment the lineup is in, and the clock and answers start with it.
            float start = Time.time;
            float closes = start + answerSeconds;
            answersCloseAt = closes;
            answersOpen = true;
            ShowClue(0);
            for (int i = 1; i < lineup.Clues.Count; i++)
            {
                yield return Until(Mathf.Min(start + i * clueGapSeconds, closes));
                ShowClue(i);
            }

            yield return Until(closes);
            answersOpen = false;
            watching = false;
            // Time is up: the clock stays on 00:00 until the game moves on.
            wall.SetCountdown(0);
            wall.ShowReward(-1);
            ClearRings();

            // The reveal: the floor's lineup shrinks away while on the wall the others leave the row
            // and the culprit glides to the middle and bounces bigger; then it fills the WANTED poster,
            // then those who got it right cheer. The title and the clues stay up.
            Revealed?.Invoke();
            // The floor and the wall's row play the reveal together; then the poster gets the culprit.
            Coroutine wallReveal = wall.StartCoroutine(wall.RevealCulprit(lineup.Culprit));
            yield return floor.Reveal();
            yield return wallReveal;

            // The money and the cheer come as CAUGHT lands, and the game moves on a moment later.
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
                answers.PlayerLocked -= OnPlayerLocked;
                answers.PlayerEntered -= OnPlayerEntered;
            }

            answersOpen = false;
            watching = false;
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
            if (!watching || answers == null || playerSpawner == null) return;

            answers.Track(playerSpawner.SpawnedPlayers, Time.deltaTime, cluesShown);
            for (int ring = 0; ring < floor.Count; ring++)
            {
                floor.ShowProgress(ring, answers.Fill(ring));
            }
        }

        private void ShowClue(int index)
        {
            LineupClue clue = lineup.Clues[index];
            LineupFeatureArt art = ArtFor(clue.Feature);
            wall.ShowClue(index, art?.Icon, clue.Has, art != null ? art.Label : clue.Feature.ToString().ToUpperInvariant());
            cluesShown = index + 1;
            wall.ShowReward(Reward(cluesShown));
            ClueShown?.Invoke(index);
        }

        // Waits until the time, showing the seconds left to answer while answers are open; a skip
        // ends every wait at once.
        private IEnumerator Until(float time)
        {
            while (Time.time < time && !skipping)
            {
                if (answersOpen) wall.SetCountdown(Mathf.CeilToInt(answersCloseAt - Time.time));
                yield return null;
            }
        }

        // A suspect bounces as a player steps onto its ring, so they know they are on it; not while the
        // suspects are still coming in (a bounce would bring one in early).
        private void OnPlayerEntered(UWBPlayerController player, int suspect)
        {
            if (answersOpen) floor.Bounce(suspect);
        }

        private void OnPlayerLocked(UWBPlayerController player, int suspect)
        {
            floor.ShowPick(player.transform);
            floor.Bounce(suspect);
            FloorDamage.Play(player.transform.position, lockedWord, lockedColor, moneyFont, moneyMaterial, transform);
            AnswerLocked?.Invoke();
            Debug.Log($"[LineupPuzzle] Player {player.PlayerId} picked the {lineup.SpeciesOf(suspect).Name} after {cluesShown} clue(s)", this);
        }

        private void ClearRings()
        {
            for (int ring = 0; ring < floor.Count; ring++) floor.ShowProgress(ring, 0f);
        }

        // Right answers earn a bonus by how early they were last given (a wrong one costs nothing). It
        // shows at the player's feet now with a cheer; the score tags take it at the end (they are put
        // away during the mini game).
        private void Settle()
        {
            winnings.Clear();
            foreach (KeyValuePair<int, LineupAnswers.Answer> pair in answers.Locked)
            {
                bool right = pair.Value.Suspect == lineup.Culprit;
                Debug.Log($"[LineupPuzzle] Player {pair.Key} {(right ? "was right" : "was wrong")}", this);
                if (!right) continue;

                int bonus = Reward(pair.Value.CluesSeen);
                PlayerScoreTag tag = TagOf(pair.Key);
                if (tag != null && bonus > 0) winnings.Add((tag, bonus));
                if (!playerSpawner.TryGetPlayer(pair.Key, out UWBPlayerController player) || player == null) continue;

                LineupCheer.Play(player.transform.position, transform);
                if (bonus > 0) FloorDamage.Play(player.transform.position, $"+{bonus}$", gainColor, moneyFont, moneyMaterial, transform);
            }

        }

        private int Reward(int cluesSeen)
        {
            if (payByCardsUp == null || payByCardsUp.Length == 0) return 0;
            return payByCardsUp[Mathf.Clamp(cluesSeen - 1, 0, payByCardsUp.Length - 1)];
        }

        private LineupFeatureArt ArtFor(LineupFeature feature)
        {
            return Array.Find(features, art => art != null && art.Feature == feature);
        }

        private PlayerScoreTag TagOf(int playerId)
        {
            if (scoreTags == null) return null;
            return Array.Find(scoreTags, tag => tag != null && tag.InPlay && tag.PlayerNumber == playerId);
        }
    }
}
