using System.Collections.Generic;
using Fortal.Players;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace BountyHunter.Thieves
{
    // Runs the rounds of a stage: round 1 puts up one WANTED poster, round 2 two, round 3 three, and
    // every round after that three again, for as long as the stage's clock runs (StageTimer; when it
    // runs out MapSwitcher moves on to the next map). Each round spawns exactly one thief per poster. The player who catches a thief earns the bounty on
    // its poster and the poster is stamped CAUGHT and torn down. Catching every thief starts the
    // next round: the stage title rolls to the new number first, then the posters go up.
    // From stage 1-2 on the thieves drop bombs (BombSquad): a bombed player loses a random fine in bombPenalty
    // (or what they have) and the thief's bounty goes up by the same. Passers-by (NpcCrowd, the sloth)
    // stroll the floor from the first round on; a player who grabs one pays npcFine (or what they
    // have). MapSwitcher starts each new stage.
    // Console lines start with [BountyRounds].
    [DefaultExecutionOrder(20)]
    public sealed class BountyRounds : MonoBehaviour
    {
        [SerializeField] private ThiefSpawner spawner;
        [SerializeField] private WantedBoard board;
        [SerializeField] private StageTimer timer;
        [SerializeField] private PlayerScoreTag[] scoreTags;
        [SerializeField] private BombSquad bombs;
        [SerializeField] private NpcCrowd npcs;
        // What grabbing a passer-by costs, shown at the player's feet like a bomb's fine.
        [SerializeField, Min(0)] private int npcFine = 5;
        // Inclusive range of the random fine for being caught in a blast.
        [SerializeField] private Vector2Int bombPenalty = new Vector2Int(5, 10);
        // The "-7$" shown on the floor at a bombed player's feet.
        [SerializeField] private string damageFormat = "-{0}$";
        [SerializeField] private Color damageColor = new Color(1f, 0.22f, 0.16f, 1f);
        [SerializeField] private TMP_FontAsset damageFont;
        // An outlined material of damageFont keeps the number readable on the busy floor.
        [SerializeField] private Material damageMaterial;
        // Round 1 puts up one poster, round 2 two, and so on up to this many; every round after that
        // puts up this many again, until the stage's clock runs out (MapSwitcher then moves on).
        [FormerlySerializedAs("roundsPerStage")]
        [SerializeField, Min(1)] private int mostPosters = 3;
        // The posters go up this long after the stage title has finished rolling to the new number.
        [SerializeField, Min(0f)] private float postersAfterTitle = 0.2f;
        // Each thief comes out of hiding (or walks onto the floor) this long after its poster starts going up.
        [SerializeField, Min(0f)] private float thiefAfterPoster = 0.5f;
        // Pause after the last catch of a round, so the brawl and the poster tear-down play out first.
        [SerializeField, Min(0f)] private float nextRoundDelay = 1.8f;

        private readonly Dictionary<ThiefController, WantedPoster> wanted = new Dictionary<ThiefController, WantedPoster>();
        private int stage = 1;
        private int round;
        // Set by IntroSequence: nothing starts until it calls Begin.
        private bool waitingForIntro;
        // Set once the game is over: no more rounds, catches or fines.
        private bool stopped;

        private void Start()
        {
            if (spawner != null) spawner.ThiefCaught += OnThiefCaught;
            if (bombs != null) bombs.PlayerBombed += OnPlayerBombed;
            if (npcs != null) npcs.NpcBumped += OnNpcBumped;
            if (round == 0 && !waitingForIntro) StartStage(stage);
        }

        // Keeps the first round from starting until Begin; called by the intro before Start.
        public void WaitForIntro()
        {
            waitingForIntro = true;
        }

        // Starts the first round once the intro is over.
        public void Begin()
        {
            if (!waitingForIntro) return;

            waitingForIntro = false;
            StartStage(stage);
        }

        private void OnDestroy()
        {
            if (spawner != null) spawner.ThiefCaught -= OnThiefCaught;
            if (bombs != null) bombs.PlayerBombed -= OnPlayerBombed;
            if (npcs != null) npcs.NpcBumped -= OnNpcBumped;
        }

        // Starts the stage over from round 1, with any thieves still out removed.
        public void StartStage(int stageNumber)
        {
            if (stopped) return;

            stage = stageNumber;
            // A map switch during the intro only picks the stage the game will start on.
            if (waitingForIntro) return;

            CancelInvoke(nameof(NextRound));
            // A new stage (a new map) sends the passers-by in afresh, over an edge or out of a hiding
            // spot of the new layout, so the change of stage shows; StartRound's Begin brings them.
            if (npcs != null) npcs.Clear();
            StartRound(1);
        }

        // Ends the game: the thieves and bombs go and nothing starts again.
        public void Stop()
        {
            if (stopped) return;

            stopped = true;
            CancelInvoke(nameof(NextRound));
            wanted.Clear();
            if (spawner != null) spawner.RemoveThieves();
            if (bombs != null)
            {
                bombs.ClearBombs();
                bombs.SetArmed(false);
            }

            if (npcs != null) npcs.Clear();

            Debug.Log("[BountyRounds] Game over", this);
        }

        private void StartRound(int roundNumber)
        {
            round = roundNumber;
            wanted.Clear();
            if (spawner != null) spawner.RemoveThieves();
            if (bombs != null)
            {
                bombs.ClearBombs();
                // Stage 1-1 is the warm-up without bombs.
                bombs.SetArmed(stage > 1 || round > 1);
            }

            if (timer != null) timer.StartStage(stage, round);
            if (board == null || spawner == null)
            {
                if (npcs != null) npcs.Begin();
                return;
            }

            float postersDelay = timer != null ? timer.TitleRollSeconds + postersAfterTitle : 0f;
            IReadOnlyList<WantedPoster> posters = board.PutUp(Mathf.Min(round, mostPosters), postersDelay);
            for (int i = 0; i < posters.Count; i++)
            {
                float entrance = postersDelay + i * board.PinStagger + thiefAfterPoster;
                wanted[spawner.SpawnThief(posters[i].Thief, entrance)] = posters[i];
            }

            // After the thieves have picked where they come in, so the passers-by keep clear of it.
            if (npcs != null) npcs.Begin();

            Debug.Log($"[BountyRounds] Stage {stage}-{round}: {wanted.Count} wanted", this);
        }

        private void OnThiefCaught(ThiefController thief, UWBPlayerController catcher)
        {
            if (!wanted.TryGetValue(thief, out WantedPoster poster)) return;

            wanted.Remove(thief);
            Pay(catcher, poster.Bounty);
            if (board != null) board.MarkCaught(poster);
            if (wanted.Count > 0) return;

            // Every cleared round brings the next, for as long as the stage's clock runs.
            Invoke(nameof(NextRound), nextRoundDelay);
        }

        private void NextRound()
        {
            StartRound(round + 1);
        }

        private void Pay(UWBPlayerController catcher, int bounty)
        {
            PlayerScoreTag tag = TagOf(catcher);
            if (tag == null) return;

            tag.SetScore(tag.Score + bounty);
            Debug.Log($"[BountyRounds] Player {catcher.PlayerId} earns {bounty}$ (total {tag.Score}$)", this);
        }

        // The bombed player pays a random fine (or what they have) and the bomber's bounty grows by what was paid.
        private void OnPlayerBombed(UWBPlayerController player, ThiefController bomber)
        {
            if (stopped) return;

            PlayerScoreTag tag = TagOf(player);
            if (tag == null) return;

            int fine = Random.Range(bombPenalty.x, Mathf.Max(bombPenalty.x, bombPenalty.y) + 1);
            int lost = Mathf.Min(fine, tag.Score);
            if (lost <= 0) return;

            tag.SetScore(tag.Score - lost);
            FloorDamage.Play(player.transform.position, string.Format(damageFormat, lost), damageColor, damageFont, damageMaterial, transform);
            if (wanted.TryGetValue(bomber, out WantedPoster poster)) poster.RaiseBounty(lost);
            Debug.Log($"[BountyRounds] Player {player.PlayerId} loses {lost}$ to a bomb (total {tag.Score}$)", this);
        }

        // Grabbing a passer-by instead of a thief costs npcFine (or what the player has).
        private void OnNpcBumped(NpcWalker npc, UWBPlayerController player)
        {
            if (stopped) return;

            PlayerScoreTag tag = TagOf(player);
            if (tag == null) return;

            int lost = Mathf.Min(npcFine, tag.Score);
            if (lost <= 0) return;

            tag.SetScore(tag.Score - lost);
            FloorDamage.Play(player.transform.position, string.Format(damageFormat, lost), damageColor, damageFont, damageMaterial, transform);
            Debug.Log($"[BountyRounds] Player {player.PlayerId} loses {lost}$ for grabbing {npc.DisplayName} (total {tag.Score}$)", this);
        }

        private PlayerScoreTag TagOf(UWBPlayerController player)
        {
            if (player == null || scoreTags == null) return null;

            foreach (PlayerScoreTag tag in scoreTags)
            {
                if (tag != null && tag.InPlay && tag.PlayerNumber == player.PlayerId) return tag;
            }

            return null;
        }
    }
}
