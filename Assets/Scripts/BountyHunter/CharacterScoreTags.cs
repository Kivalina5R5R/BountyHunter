using System;
using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Which score tag on the wall belongs to which character. When the game starts each tag is given
    // to the player who picked its character, so that player's points land under the character they
    // play; a character nobody picked leaves the wall, dog and tag both. Console lines start with
    // [CharacterScoreTags].
    public sealed class CharacterScoreTags : MonoBehaviour
    {
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private string character;
            [SerializeField] private PlayerScoreTag tag;
            // The dog on the wall the tag belongs to.
            [SerializeField] private GameObject dog;
            // The character's face, shown on the tag while the dog's picture is away.
            [SerializeField] private Sprite face;

            public string Character => character;
            public PlayerScoreTag Tag => tag;
            public GameObject Dog => dog;
            public Sprite Face => face;
        }

        [SerializeField] private Entry[] entries;
        // Breaks ties in the standings; may be empty, when tied characters keep the order of the entries.
        [SerializeField] private MatchStats tieBreaks;

        private void Awake()
        {
            if (entries == null) return;
            foreach (Entry entry in entries)
            {
                if (entry != null && entry.Tag != null) entry.Tag.SetFace(entry.Face);
            }
        }

        // chosen: player id to the character they picked. With no picks at all the tags keep their numbers.
        public void Assign(IReadOnlyDictionary<int, string> chosen)
        {
            if (entries == null || chosen == null || chosen.Count == 0) return;

            foreach (Entry entry in entries)
            {
                if (entry == null || entry.Tag == null) continue;

                bool played = TryGetPlayer(chosen, entry.Character, out int player);
                if (entry.Dog != null) entry.Dog.SetActive(played);
                if (played)
                {
                    entry.Tag.AssignPlayer(player);
                    Debug.Log($"[CharacterScoreTags] {entry.Character}'s tag shows player {player}", this);
                }
                else
                {
                    entry.Tag.Unassign();
                    Debug.Log($"[CharacterScoreTags] Nobody plays {entry.Character}; it leaves the wall", this);
                }
            }
        }

        // The characters still in play with their scores, best first. Equal money is settled by
        // tieBreaks (boss catches, then thieves caught, then mini game answer speed), so only one
        // stands on the winner's bench; a tie even then keeps the order of the entries.
        public List<ResultStanding> Standings()
        {
            var ranked = new List<ResultStanding>();
            if (entries == null) return ranked;

            var players = new Dictionary<string, int>();
            foreach (Entry entry in entries)
            {
                if (entry == null || entry.Tag == null || !entry.Tag.InPlay) continue;

                var standing = new ResultStanding(entry.Character, entry.Tag.Score);
                players[entry.Character] = entry.Tag.PlayerNumber;
                // An insertion sort that keeps entry order among those it cannot tell apart.
                int at = ranked.FindIndex(other => Compare(standing, other, players) < 0);
                ranked.Insert(at < 0 ? ranked.Count : at, standing);
            }

            return ranked;
        }

        // Below 0 when a places above b.
        private int Compare(ResultStanding a, ResultStanding b, IReadOnlyDictionary<string, int> players)
        {
            int byMoney = b.Score.CompareTo(a.Score);
            if (byMoney != 0 || tieBreaks == null) return byMoney;

            return tieBreaks.CompareTied(players[a.Character], players[b.Character]);
        }

        private static bool TryGetPlayer(IReadOnlyDictionary<int, string> chosen, string character, out int player)
        {
            foreach (KeyValuePair<int, string> pick in chosen)
            {
                if (pick.Value != character) continue;
                player = pick.Key;
                return true;
            }

            player = 0;
            return false;
        }
    }
}
