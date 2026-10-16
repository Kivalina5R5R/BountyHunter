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

            public string Character => character;
            public PlayerScoreTag Tag => tag;
            public GameObject Dog => dog;
        }

        [SerializeField] private Entry[] entries;

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
