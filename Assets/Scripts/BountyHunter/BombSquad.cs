using System;
using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Lets the thieves drop bombs: a thief with a player on its tail (within chaseRadius) for
    // chaseSeconds drops a bomb behind it, toward that player, then must wait cooldownSeconds before
    // the next one. Each bomb explodes after fuseSeconds and reports every player inside its blast.
    // Only while armed (BountyRounds arms it from the second round). Console lines start with [BombSquad].
    public sealed class BombSquad : MonoBehaviour
    {
        [SerializeField] private ThiefSpawner thieves;
        [SerializeField] private UWBPlayerSpawner playerSpawner;
        [SerializeField] private Bomb.Art art;

        [Header("When")]
        [SerializeField, Min(0.1f)] private float chaseSeconds = 3f;
        [SerializeField, Min(0.1f)] private float chaseRadius = 2.2f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 10f;

        [Header("Bomb")]
        [SerializeField, Min(0.1f)] private float fuseSeconds = 1f;
        [SerializeField, Min(0.1f)] private float blastRadius = 0.9f;
        // Width of the bomb's body on the floor: about half a thief (thieves are 0.6 tall).
        [SerializeField, Min(0.05f)] private float bombSize = 0.3f;
        // How far behind the thief, toward the chasing player, the bomb lands.
        [SerializeField, Min(0f)] private float dropBehind = 0.35f;

        private readonly Dictionary<ThiefController, float> chasedFor = new Dictionary<ThiefController, float>();
        private readonly Dictionary<ThiefController, float> readyAt = new Dictionary<ThiefController, float>();
        private readonly List<Bomb> bombs = new List<Bomb>();
        private readonly List<UWBPlayerController> caughtInBlast = new List<UWBPlayerController>();
        private SpawnerPlayerLocator players;
        private bool armed;

        // A bomb went off with this player inside its blast; the thief is the one who dropped it.
        public event Action<UWBPlayerController, ThiefController> PlayerBombed;
        // A bomb put down at a spot with its fuse seconds, and a bomb going off at a spot (for sounds).
        public event Action<Vector3, float> BombDropped;
        public event Action<Vector3> BombExploded;

        private void Start()
        {
            players = new SpawnerPlayerLocator(playerSpawner);
        }

        // Starts or stops thieves dropping bombs; the chase and cooldown timers start over.
        public void SetArmed(bool value)
        {
            armed = value;
            chasedFor.Clear();
            readyAt.Clear();
            Debug.Log($"[BombSquad] Bombs {(armed ? "armed" : "off")}", this);
        }

        // Removes every bomb on the floor, lit or exploding, without effects; used between rounds.
        public void ClearBombs()
        {
            foreach (Bomb bomb in bombs)
            {
                if (bomb != null) Destroy(bomb.gameObject);
            }

            bombs.Clear();
        }

        private void Update()
        {
            bombs.RemoveAll(bomb => bomb == null);
            if (!armed || thieves == null || players == null) return;

            foreach (ThiefController thief in thieves.Thieves)
            {
                if (thief == null || thief.IsHidden || thief.State == ThiefState.Caught)
                {
                    if (thief != null) chasedFor[thief] = 0f;
                    continue;
                }

                Vector3 position = thief.transform.position;
                bool chased = players.TryGetNearest(position, out Vector3 chaser, out float distance) && distance <= chaseRadius;
                chasedFor.TryGetValue(thief, out float seconds);
                // A chase survives the player dropping back for a moment.
                seconds = chased ? seconds + Time.deltaTime : Mathf.Max(0f, seconds - Time.deltaTime * 2f);
                chasedFor[thief] = seconds;
                if (seconds < chaseSeconds) continue;
                if (readyAt.TryGetValue(thief, out float ready) && Time.time < ready) continue;

                DropBomb(thief, position, chaser);
            }
        }

        private void DropBomb(ThiefController thief, Vector3 position, Vector3 chaser)
        {
            chasedFor[thief] = 0f;
            readyAt[thief] = Time.time + cooldownSeconds;
            Vector3 behind = chaser - position;
            behind.y = 0f;
            Vector3 spot = position + (behind.sqrMagnitude > 0.0001f ? behind.normalized * dropBehind : Vector3.zero);
            bombs.Add(Bomb.Drop(spot, art, fuseSeconds, blastRadius, bombSize, transform, bomb => OnExploded(bomb, thief)));
            BombDropped?.Invoke(spot, fuseSeconds);
            Debug.Log($"[BombSquad] {thief.name} drops a bomb ({fuseSeconds:0.#}s fuse)", this);
        }

        private void OnExploded(Bomb bomb, ThiefController thief)
        {
            BombExploded?.Invoke(bomb.transform.position);
            players.GetPlayersWithin(bomb.transform.position, bomb.BlastRadius, caughtInBlast);
            foreach (UWBPlayerController player in caughtInBlast)
            {
                Debug.Log($"[BombSquad] Bomb from {(thief != null ? thief.name : "a gone thief")} hits {player.name}", this);
                PlayerBombed?.Invoke(player, thief);
            }
        }
    }
}
