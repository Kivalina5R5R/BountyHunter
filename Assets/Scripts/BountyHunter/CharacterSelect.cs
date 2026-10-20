using System;
using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Character picking on the intro floor: a player who stands on a free card for holdSeconds takes
    // that character, and their floor marker turns into that character's ring. Stepping off before
    // then lets the progress drain away. Each player takes one character and each character goes to
    // one player. Console lines start with [CharacterSelect].
    public sealed class CharacterSelect : MonoBehaviour
    {
        // The floor marker ring shown for a character once picked.
        [Serializable]
        private sealed class MarkerArt
        {
            [SerializeField] private string character;
            [SerializeField] private Sprite marker;

            public string Character => character;
            public Sprite Marker => marker;
        }

        [SerializeField] private UWBPlayerSpawner playerSpawner;
        [SerializeField] private CharacterCard[] cards;
        [SerializeField] private MarkerArt[] markers;
        // How close to the card's centre a player must stand.
        [SerializeField, Min(0.1f)] private float standRadius = 0.9f;
        [SerializeField, Min(0.1f)] private float holdSeconds = 2f;
        // How long a half-filled ring takes to empty again once the player steps off.
        [SerializeField, Min(0.05f)] private float drainSeconds = 0.6f;
        // The gap between one card coming out and the next when they are revealed.
        [SerializeField, Min(0f)] private float revealStaggerSeconds = 0.18f;

        private readonly Dictionary<int, string> chosen = new Dictionary<int, string>();
        private readonly List<UWBPlayerController> standing = new List<UWBPlayerController>();
        private float[] progress;
        private SpawnerPlayerLocator players;
        private bool waitingForReveal;

        // Player id to the character they picked.
        public IReadOnlyDictionary<int, string> Chosen => chosen;

        // True when at least one player is on the floor and every one of them has picked (or every
        // card is taken, so those left cannot pick anyway).
        public bool EveryoneChosen
        {
            get
            {
                if (playerSpawner == null) return false;

                int present = 0;
                foreach (UWBPlayerController player in playerSpawner.SpawnedPlayers)
                {
                    if (player == null || !player.isActiveAndEnabled) continue;
                    present++;
                    if (!chosen.ContainsKey(player.PlayerId) && !AllCardsTaken) return false;
                }

                return present > 0;
            }
        }

        private bool AllCardsTaken
        {
            get
            {
                if (cards == null) return true;
                foreach (CharacterCard card in cards)
                {
                    if (card != null && !card.Taken) return false;
                }

                return true;
            }
        }
        public event Action<int, string> CharacterChosen;

        private void Start()
        {
            players = new SpawnerPlayerLocator(playerSpawner);
            progress = new float[cards != null ? cards.Length : 0];
            if (!waitingForReveal || cards == null) return;

            foreach (CharacterCard card in cards)
            {
                if (card != null) card.Hide();
            }
        }

        // Keeps the cards hidden from the start until Reveal is called.
        public void WaitForReveal()
        {
            waitingForReveal = true;
        }

        // Brings the hidden cards out one after another; they can be picked once each has landed.
        public void Reveal()
        {
            if (!waitingForReveal) return;

            waitingForReveal = false;
            if (cards == null) return;
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null) cards[i].Reveal(i * revealStaggerSeconds);
            }

            Debug.Log("[CharacterSelect] Cards are out", this);
        }

        private void Update()
        {
            if (players == null || cards == null) return;

            for (int i = 0; i < cards.Length; i++)
            {
                CharacterCard card = cards[i];
                if (card == null || card.Taken || !card.Ready) continue;

                UWBPlayerController picker = FreePlayerOn(card);
                progress[i] = picker != null
                    ? progress[i] + Time.deltaTime / holdSeconds
                    : Mathf.Max(0f, progress[i] - Time.deltaTime / drainSeconds);
                card.ShowProgress(progress[i]);
                if (picker == null || progress[i] < 1f) continue;

                chosen[picker.PlayerId] = card.Character;
                card.Take();
                CharacterMarker.Show(picker, MarkerFor(card.Character));
                Debug.Log($"[CharacterSelect] Player {picker.PlayerId} is {card.Character}", this);
                CharacterChosen?.Invoke(picker.PlayerId, card.Character);
            }
        }

        private Sprite MarkerFor(string character)
        {
            if (markers == null) return null;
            foreach (MarkerArt art in markers)
            {
                if (art != null && art.Character == character) return art.Marker;
            }

            return null;
        }

        // A player on the card who has not picked a character yet, or null.
        private UWBPlayerController FreePlayerOn(CharacterCard card)
        {
            players.GetPlayersWithin(card.StandPoint, standRadius, standing);
            foreach (UWBPlayerController player in standing)
            {
                if (!chosen.ContainsKey(player.PlayerId)) return player;
            }

            return null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (cards == null) return;
            Gizmos.color = new Color(0.4f, 1f, 0.4f, 1f);
            foreach (CharacterCard card in cards)
            {
                if (card != null) Gizmos.DrawWireSphere(card.StandPoint, standRadius);
            }
        }
#endif
    }
}
