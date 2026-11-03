using System;
using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // Drops stun guns on the floor during the stages that have them (2 and 3): the first
    // firstDropSeconds into the stage, then every secondsBetweenDrops, a few at a time (a stage in
    // stageDrops, the market, drops fewer, less often), in the open:
    // well apart from each other and the players, and off the hiding spots, the things on the floor a
    // player uses (FloorSwitch, such as the police tape crate) and the mud; of a number of random
    // spots that are clear, the one with the most room round it. A player who steps within pickUpRadius of one picks it up
    // (StunGuns.Arm). The thieves want them gone too: every so often the nearest calm thief within
    // thiefNoticeRadius of a gun sneaks over to it, and a thief that reaches one snatches it away so
    // the police cannot have it (thieves cannot shoot). A thief snatches one at a time: after a
    // snatch it leaves the guns alone for thiefSnatchCooldown seconds. They pop up and bob gently so they are
    // noticed; whatever is left goes when the floor is cleared or the next stage starts.
    public sealed class StunGunPickups : MonoBehaviour
    {
        // A stage that drops guns its own way instead of the usual Drops settings.
        [Serializable]
        private sealed class StageDrops
        {
            [SerializeField] private int stage = 3;
            [SerializeField, Min(1f)] private float secondsBetweenDrops = 25f;
            [SerializeField] private Vector2Int gunsPerDrop = new Vector2Int(1, 1);
            [SerializeField, Min(1)] private int mostGunsWaiting = 2;

            public int Stage => stage;
            public float SecondsBetweenDrops => secondsBetweenDrops;
            public Vector2Int GunsPerDrop => gunsPerDrop;
            public int MostGunsWaiting => mostGunsWaiting;
        }

        [SerializeField] private BountyRounds rounds;
        [SerializeField] private StunGuns guns;
        [SerializeField] private UWBPlayerSpawner players;
        // The thieves, who snatch guns away, and their hiding spots, kept clear of; may be empty.
        [SerializeField, FormerlySerializedAs("hidingSpots")] private ThiefSpawner thieves;
        // The stages (1 first) that have stun guns.
        [SerializeField] private int[] stages = { 2, 3 };

        [Header("Drops")]
        [SerializeField, Min(0f)] private float firstDropSeconds = 6f;
        [SerializeField, Min(1f)] private float secondsBetweenDrops = 15f;
        [SerializeField] private Vector2Int gunsPerDrop = new Vector2Int(2, 3);
        // Never more than this many waiting on the floor.
        [SerializeField, Min(1)] private int mostGunsWaiting = 5;
        [SerializeField, Min(0.1f)] private float pickUpRadius = 0.5f;
        // Stages with fewer drops: the market, where the lucky wheel can hand everyone a gun too.
        [SerializeField] private StageDrops[] stageDrops = { new StageDrops() };

        [Header("Thieves")]
        // A calm thief this close to a gun may go for it, checked this often, at this times its
        // wandering pace.
        [SerializeField, Min(0f)] private float thiefNoticeRadius = 3f;
        [SerializeField, Min(0.1f)] private float thiefLookEverySeconds = 2f;
        [SerializeField, Min(0.1f)] private float thiefGrabSpeedUp = 1.4f;
        [SerializeField, Min(0f)] private float thiefSnatchCooldown = 10f;
        // Where they may land (x and world z), how far from the players and from each other, and how
        // far past the edge of a hiding spot's picture, a floor switch's ring or the mud.
        [SerializeField] private Vector2 areaMin = new Vector2(-4.6f, -1.9f);
        [SerializeField] private Vector2 areaMax = new Vector2(4.6f, 1.9f);
        [SerializeField, Min(0f)] private float clearOfPlayers = 1.5f;
        [SerializeField, Min(0f)] private float apartFromEachOther = 2.2f;
        [SerializeField, Min(0f)] private float clearOfSpots = 0.8f;
        // The park's mud, kept clear of; may be empty.
        [SerializeField] private MudPuddles mud;

        [Header("Look")]
        [SerializeField] private Sprite icon;
        // Big enough to spot from across the floor.
        [SerializeField, Min(0.05f)] private float pickupHeight = 0.45f;

        private const int SpotTries = 40;

        private readonly List<StunGunPickup> waiting = new List<StunGunPickup>();
        private readonly List<Vector3> taken = new List<Vector3>();
        private bool dropping;
        private float nextDropAt;
        // This stage's drops (stageDrops, or the usual settings).
        private float dropEverySeconds;
        private Vector2Int dropCount;
        private int mostWaiting;
        private float nextThiefLookAt;
        // When each thief that has snatched a gun may snatch another.
        private readonly Dictionary<ThiefController, float> thiefReadyAt = new Dictionary<ThiefController, float>();

        // A gun landed on the floor, a player picked one up, and a thief snatched one away.
        // The guns lying on the floor waiting to be picked up.
        public IReadOnlyList<StunGunPickup> Waiting => waiting;

        public event Action<Vector3> Dropped;
        public event Action<UWBPlayerController> PickedUp;
        public event Action<ThiefController> Snatched;

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
            UseDropsOf(stage);
        }

        private void UseDropsOf(int stage)
        {
            dropEverySeconds = secondsBetweenDrops;
            dropCount = gunsPerDrop;
            mostWaiting = mostGunsWaiting;
            if (stageDrops == null) return;

            foreach (StageDrops drops in stageDrops)
            {
                if (drops == null || drops.Stage != stage) continue;

                dropEverySeconds = drops.SecondsBetweenDrops;
                dropCount = drops.GunsPerDrop;
                mostWaiting = drops.MostGunsWaiting;
                return;
            }
        }

        // No more drops, and the guns still waiting are taken away.
        private void Stop()
        {
            dropping = false;
            thiefReadyAt.Clear();
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
                int count = Random.Range(dropCount.x, Mathf.Max(dropCount.x, dropCount.y) + 1);
                for (int i = 0; i < count && waiting.Count < mostWaiting; i++) Drop();
            }

            PickUp();
            SnatchAway();
            if (Time.time < nextThiefLookAt) return;
            nextThiefLookAt = Time.time + thiefLookEverySeconds;
            SendThieves();
        }

        private void Drop()
        {
            if (icon == null || !TryFindSpot(out Vector3 spot)) return;

            waiting.Add(StunGunPickup.Create(transform, icon, pickupHeight, spot));
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

        // A thief in the open within pickUpRadius of a waiting gun snatches it away.
        private void SnatchAway()
        {
            if (thieves == null) return;

            for (int i = waiting.Count - 1; i >= 0; i--)
            {
                StunGunPickup pickup = waiting[i];
                ThiefController thief = NearestThief(pickup.transform.position, pickUpRadius, false);
                if (thief == null) continue;

                waiting.RemoveAt(i);
                pickup.Snatch();
                thiefReadyAt[thief] = Time.time + thiefSnatchCooldown;
                Debug.Log($"[StunGunPickups] {thief.name} snatched a stun gun away", this);
                Snatched?.Invoke(thief);
            }
        }

        // For each waiting gun, the nearest calm thief within thiefNoticeRadius sneaks over to it.
        private void SendThieves()
        {
            if (thieves == null) return;

            foreach (StunGunPickup pickup in waiting)
            {
                Vector3 at = pickup.transform.position;
                ThiefController thief = NearestThief(at, thiefNoticeRadius, true);
                if (thief != null) thief.GoGrab(new Vector3(at.x, thief.transform.position.y, at.z), thiefGrabSpeedUp);
            }
        }

        // The nearest thief out in the open within reach of at and not resting after a snatch (only
        // calm, wandering ones when calmOnly), or null.
        private ThiefController NearestThief(Vector3 at, float reach, bool calmOnly)
        {
            ThiefController nearest = null;
            float nearestDistance = reach;
            foreach (ThiefController thief in thieves.Thieves)
            {
                if (thief == null || !thief.isActiveAndEnabled || thief.IsStunned) continue;
                if (thief.State == ThiefState.Hidden || thief.State == ThiefState.Caught || thief.IsCuffed) continue;
                if (calmOnly && thief.State != ThiefState.Wander) continue;
                if (thiefReadyAt.TryGetValue(thief, out float readyAt) && Time.time < readyAt) continue;

                float distance = Flat(at, thief.transform.position);
                if (distance > nearestDistance) continue;
                nearestDistance = distance;
                nearest = thief;
            }

            return nearest;
        }

        // Of SpotTries random spots, the clear one with the most room round it; false when none is clear.
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

            FloorSwitch[] switches = FindObjectsByType<FloorSwitch>(FindObjectsSortMode.None);
            spot = Vector3.zero;
            float mostRoom = float.MinValue;
            for (int attempt = 0; attempt < SpotTries; attempt++)
            {
                var candidate = new Vector3(Random.Range(areaMin.x, areaMax.x), 0f, Random.Range(areaMin.y, areaMax.y));
                float room = Room(candidate, switches);
                if (room < 0f || room <= mostRoom) continue;

                mostRoom = room;
                spot = candidate;
            }

            return mostRoom >= 0f;
        }

        // How much room the spot has beyond what it must keep clear of (below 0 when it is too close
        // to something): the players, the guns waiting, the hiding spots, the floor switches and the mud.
        private float Room(Vector3 spot, FloorSwitch[] switches)
        {
            float room = float.MaxValue;
            foreach (Vector3 player in taken) room = Mathf.Min(room, Flat(spot, player) - clearOfPlayers);
            foreach (StunGunPickup pickup in waiting) room = Mathf.Min(room, Flat(spot, pickup.transform.position) - apartFromEachOther);

            if (thieves != null)
            {
                foreach (HidingBush bush in thieves.Bushes)
                {
                    if (bush != null) room = Mathf.Min(room, Flat(spot, bush.Center) - bush.Reach - clearOfSpots);
                }
            }

            foreach (FloorSwitch floorSwitch in switches)
            {
                if (floorSwitch != null) room = Mathf.Min(room, Flat(spot, floorSwitch.transform.position) - floorSwitch.Radius - clearOfSpots);
            }

            if (mud != null && InOrNearMud(spot)) room = -1f;
            return room;
        }

        // In the mud, or within clearOfSpots of it on any side.
        private bool InOrNearMud(Vector3 spot)
        {
            if (mud.InMud(spot)) return true;

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                if (mud.InMud(spot + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * clearOfSpots)) return true;
            }

            return false;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }
    }
}
