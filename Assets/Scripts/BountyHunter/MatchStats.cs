using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Keeps per-player counts over a game, used only to break ties at the result: catches of the boss
    // (the vault's and the chase's that count, not the chase's decoy), thieves caught (or handed over
    // at a police car), and the total time taken to give the right answer in the mini games (a wrong
    // answer or none counts as the whole time to answer). Any source may be left empty.
    public sealed class MatchStats : MonoBehaviour
    {
        [SerializeField] private BountyRounds rounds;
        [SerializeField] private BossVault vault;
        [SerializeField] private BossChase chase;
        [SerializeField] private LineupPuzzle lineup;
        [SerializeField] private CctvPuzzle cctv;

        private readonly Dictionary<int, int> bossCatches = new Dictionary<int, int>();
        private readonly Dictionary<int, int> thievesCaught = new Dictionary<int, int>();
        private readonly Dictionary<int, float> answerSeconds = new Dictionary<int, float>();

        private void OnEnable()
        {
            if (rounds != null) rounds.ThievesCaught += OnThievesCaught;
            if (vault != null) vault.BossBumped += OnBossCaught;
            if (chase != null) chase.BossCatch += OnBossCaught;
            if (lineup != null) lineup.AnswerTimed += OnAnswerTimed;
            if (cctv != null) cctv.AnswerTimed += OnAnswerTimed;
        }

        private void OnDisable()
        {
            if (rounds != null) rounds.ThievesCaught -= OnThievesCaught;
            if (vault != null) vault.BossBumped -= OnBossCaught;
            if (chase != null) chase.BossCatch -= OnBossCaught;
            if (lineup != null) lineup.AnswerTimed -= OnAnswerTimed;
            if (cctv != null) cctv.AnswerTimed -= OnAnswerTimed;
        }

        // Which of two players with the same money places higher: below 0 when a does, above 0 when b
        // does, 0 when nothing tells them apart. More boss catches first, then more thieves caught,
        // then the faster mini game answers.
        public int CompareTied(int a, int b)
        {
            int byBoss = Count(bossCatches, b).CompareTo(Count(bossCatches, a));
            if (byBoss != 0) return byBoss;

            int byThieves = Count(thievesCaught, b).CompareTo(Count(thievesCaught, a));
            if (byThieves != 0) return byThieves;

            return Seconds(a).CompareTo(Seconds(b));
        }

        private void OnThievesCaught(UWBPlayerController player, int count)
        {
            if (player == null) return;
            thievesCaught[player.PlayerId] = Count(thievesCaught, player.PlayerId) + count;
        }

        private void OnBossCaught(UWBPlayerController player)
        {
            if (player == null) return;
            bossCatches[player.PlayerId] = Count(bossCatches, player.PlayerId) + 1;
        }

        private void OnAnswerTimed(int player, float seconds)
        {
            answerSeconds[player] = Seconds(player) + seconds;
        }

        private float Seconds(int player)
        {
            return answerSeconds.TryGetValue(player, out float seconds) ? seconds : 0f;
        }

        private static int Count(Dictionary<int, int> counts, int player)
        {
            return counts.TryGetValue(player, out int count) ? count : 0;
        }
    }
}
