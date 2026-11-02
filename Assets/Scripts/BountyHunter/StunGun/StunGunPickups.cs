using System;
using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // Drops stun guns on the floor during the stages that have them (2 and 3): the first
    // firstDropSeconds into the stage, then every dropEverySeconds, a few at a time, at spots clear
    // of the players and the hiding spots. A player who steps within pickUpRadius of one picks it up
    // (StunGuns.Arm). They pop up and bob gently so they are noticed; whatever is left goes when the
    // floor is cleared or the next stage starts.
    public sealed class StunGunPickups : MonoBehaviour
    {
        [SerializeField] private BountyRounds rounds;
        [SerializeField] private StunGuns guns;
        [SerializeField] private UWBPlayerSpawner players;
        // Its hiding spots, to keep the guns off them; may be empty.
        [SerializeField] private ThiefSpawner hidingSpots;
        // The stages (1 first) that have stun guns.
        [SerializeField] private int[] stages = { 2, 3 };

        [Header("Drops")]
        [SerializeField, Min(0f)] private float firstDropSeconds = 6f;
        [SerializeField, Min(1f)] private float dropEverySeconds = 20f;
        [SerializeField] private Vector2Int perDrop = new Vector2Int(1, 2);
        // Never more than this many waiting on the floor.
        [SerializeField, Min(1)] private int mostOnFloor = 3;
        [SerializeField, Min(0.1f)] private float pickUpRadius = 0.5f;
        // Where they may land (x and world z), and how far from the players, hiding spots and each other.
        [SerializeField] private Vector2 areaMin = new Vector2(-4.6f, -1.9f);
        [SerializeField] private Vector2 areaMax = new Vector2(4.6f, 1.9f);
        [SerializeField, Min(0f)] private float clearOfPlayers = 1.5f;
        [SerializeField, Min(0f)] private float clearOfSpots = 0.8f;

        [Header("Look")]
        [SerializeField] private Sprite icon;
        // Big enough to spot from across the floor.
        [SerializeField, Min(0.05f)] private float pickupWidth = 0.6f;

        private const int SpotTries = 30;

        private readonly List<StunGunPickup> waiting = new List<StunGunPickup>();
        private readonly List<Vector3> taken = new List<Vector3>();
        private bool dropping;
        private float nextDropAt;

        // A gun landed on the floor, and a player picked one up.
        public event Action<Vector3> Dropped;
        public event Action<UWBPlayerController> PickedUp;

        private void OnEnable()
        {
            if (rounds == null) return;
            rounds.StageStarted += OnStageStarted;
            rounds.FloorCleared += Stop;
        }

        private void OnDisable()
        {
            if (rounds != null)
            {
                rounds.StageStarted -= OnStageStarted;
                rounds.FloorCleared -= Stop;
            }

            Stop();
        }

        private void OnStageStarted(int stage)
        {
            Stop();
            if (stages == null || Array.IndexOf(stages, stage) < 0) return;

            dropping = true;
            nextDropAt = Time.time + firstDropSeconds;
        }

        // No more drops, and the guns still waiting are taken away.
        private void Stop()
        {
            dropping = false;
            foreach (StunGunPickup pickup in waiting)
            {
                if (pickup != null) Destroy(pickup.gameObject);
            }

            waiting.Clear();
        }

        private void Update()
        {
            waiting.RemoveAll(pickup => pickup == null);
            if (!dropping) return;

            if (Time.time >= nextDropAt)
            {
                nextDropAt = Time.time + dropEverySeconds;
                int count = Random.Range(perDrop.x, Mathf.Max(perDrop.x, perDrop.y) + 1);
                for (int i = 0; i < count && waiting.Count < mostOnFloor; i++) Drop();
            }

            PickUp();
        }

        private void Drop()
        {
            if (icon == null || !TryFindSpot(out Vector3 spot)) return;

            waiting.Add(StunGunPickup.Create(transform, icon, pickupWidth, spot));
            Dropped?.Invoke(spot);
        }

        // The first player standing within pickUpRadius of a waiting gun takes it.
        private void PickUp()
        {
            if (players == null) return;

            for (int i = waiting.Count - 1; i >= 0; i--)
            {
                StunGunPickup pickup = waiting[i];
                Vector3 at = pickup.transform.position;
                foreach (UWBPlayerController player in players.SpawnedPlayers)
                {
                    if (player == null || !player.isActiveAndEnabled) continue;
                    Vector3 offset = player.transform.position - at;
                    if (new Vector2(offset.x, offset.z).magnitude > pickUpRadius) continue;

                    waiting.RemoveAt(i);
                    pickup.Collect();
                    if (guns != null) guns.Arm(player);
                    PickedUp?.Invoke(player);
                    break;
                }
            }
        }

        // A random spot clear of the players, the hiding spots and the guns already waiting.
        private bool TryFindSpot(out Vector3 spot)
        {
            taken.Clear();
            if (players != null)
            {
                foreach (UWBPlayerController player in players.SpawnedPlayers)
                {
                    if (player != null && player.isActiveAndEnabled) taken.Add(player.transform.position);
                }
            }

            for (int attempt = 0; attempt < SpotTries; attempt++)
            {
                spot = new Vector3(Random.Range(areaMin.x, areaMax.x), 0f, Random.Range(areaMin.y, areaMax.y));
                if (IsClear(spot)) return true;
            }

            spot = Vector3.zero;
            return false;
        }

        private bool IsClear(Vector3 spot)
        {
            foreach (Vector3 player in taken)
            {
                if (Flat(spot, player) < clearOfPlayers) return false;
            }

            foreach (StunGunPickup pickup in waiting)
            {
                if (Flat(spot, pickup.transform.position) < clearOfPlayers) return false;
            }

            if (hidingSpots == null) return true;
            foreach (HidingBush bush in hidingSpots.Bushes)
            {
                if (bush != null && Flat(spot, bush.Center) < bush.Radius + clearOfSpots) return false;
            }

            return true;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }
    }
}
