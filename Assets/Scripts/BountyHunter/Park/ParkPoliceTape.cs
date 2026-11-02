using System;
using System.Collections;
using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // The park's police tape (stage 2): one box of tape (the tape item) lies somewhere on the floor, at
    // a random spot clear of the hiding spots' pictures and the mud; a hiding spot creeping away with
    // a thief inside steers round it (BushWalker.KeepClearOf) rather than going over it. A player who
    // stands on it for holdSeconds opens it and tapes off a few hiding spots (tapedSpots, picked at
    // random) for lockSeconds: a thief hiding in one comes out, and no thief may hide there until the
    // tape comes off. The opened box vanishes in a burst of stars, and cooldownSeconds later a new
    // one springs up at another random spot, ready to open again. It closes part of the map's cover for a while, so the players choose when:
    // before the thieves hide, or to drive out the ones already in. Gone when the floor is cleared.
    public sealed class ParkPoliceTape : MonoBehaviour
    {
        [SerializeField] private BountyRounds rounds;
        [SerializeField] private UWBPlayerSpawner players;
        // Its hiding spots (and the thieves in them).
        [SerializeField] private ThiefSpawner hidingSpots;
        [SerializeField] private int stage = 2;
        // Where the crate may lie (x and world z), and how far its ring keeps from the hiding spots'
        // pictures.
        [SerializeField] private Vector2 areaMin = new Vector2(-3.8f, -1.5f);
        [SerializeField] private Vector2 areaMax = new Vector2(3.8f, 1.5f);
        [SerializeField, Min(0f)] private float clearOfSpots = 0.4f;
        // The stage's mud, which the box keeps out of, and the stun guns lying about, which it keeps
        // clearOfGuns metres from (past its own reach); either may be empty.
        [SerializeField] private MudPuddles mud;
        [SerializeField] private StunGunPickups guns;
        [SerializeField, Min(0f)] private float clearOfGuns = 0.4f;

        [Header("Taping off")]
        [SerializeField, Min(0.1f)] private float radius = 0.6f;
        [SerializeField, Min(0.1f)] private float holdSeconds = 1.5f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 15f;
        [SerializeField] private Vector2Int tapedSpots = new Vector2Int(2, 3);
        [SerializeField, Min(1f)] private float lockSeconds = 10f;

        [Header("Look")]
        // The tape box, and the stars twinkling round it while it waits to be opened.
        [SerializeField] private Sprite tapeItem;
        [SerializeField, Min(0.1f)] private float crateWidth = 0.55f;
        [SerializeField] private Sprite[] sparkles = new Sprite[0];
        // The strips of KEEP OUT tape across a taped-off hiding spot (BushTape): the strip picture
        // (its torn ends are its borders, its middle repeats), the tape's width on the floor
        // (metres, the same on every spot), how far each strip hangs past the leaves' edge, and the
        // spot size (metres across its leaves' narrow side) under which it gets three strips, not four.
        [SerializeField] private Sprite tapeStrip;
        [SerializeField, Min(0.02f)] private float tapeWidth = 0.16f;
        [SerializeField, Min(0f)] private float tapeOverhang = 0.08f;
        [SerializeField, Min(0f)] private float smallSpotSize = 1f;
        // The ring round the crate: the players' own green select ring (Assets/ART/Player/Floor/
        // Load.png) filling round as a player stands on it, with no ring behind it.
        [SerializeField] private Sprite selectRing;
        [SerializeField] private Material radialFill;
        // Tints the ring; white keeps the select ring's own green.
        [SerializeField] private Color selectRingTint = Color.white;

        private const int SpotTries = 60;

        // How far the crate and its ring reach from the crate's centre.
        private float CrateReach => Mathf.Max(radius, crateWidth * 0.5f);

        private readonly List<Vector3> spots = new List<Vector3>();
        private readonly List<HidingBush> choices = new List<HidingBush>();
        private FloorSwitch crateSwitch;
        private Coroutine comingBack;

        // Where the crate lies this stage (empty in other stages).
        public IReadOnlyList<Vector3> Spots => spots;

        // A player taped off some hiding spots (which).
        public event Action<UWBPlayerController, IReadOnlyList<HidingBush>> Taped;

        private void OnEnable()
        {
            if (rounds == null) return;
            rounds.StageStarted += OnStageStarted;
            rounds.FloorCleared += Clear;
        }

        private void OnDisable()
        {
            if (rounds != null)
            {
                rounds.StageStarted -= OnStageStarted;
                rounds.FloorCleared -= Clear;
            }

            Clear();
        }

        private void OnStageStarted(int started)
        {
            Clear();
            if (started != stage) return;

            PutBoxDown();
        }

        // A new box at a random clear spot.
        private void PutBoxDown()
        {
            Vector3 at = RandomClearSpot();
            spots.Add(at);
            crateSwitch = FloorSwitch.Create(transform, "Police Tape Box", at, tapeItem, crateWidth, null, selectRing, radialFill, selectRingTint, radius,
                holdSeconds, 0f, players, sparkles);
            crateSwitch.Triggered += OnTriggered;
            KeepBushesOff(at);
        }

        // The opened box goes; a new one comes after the cooldown.
        private void TakeBoxAway()
        {
            if (crateSwitch != null)
            {
                crateSwitch.Triggered -= OnTriggered;
                crateSwitch.Vanish();
            }

            crateSwitch = null;
            spots.Clear();
            ForgetBushSpots();
            comingBack = StartCoroutine(BringBoxBack());
        }

        private IEnumerator BringBoxBack()
        {
            yield return new WaitForSeconds(cooldownSeconds);
            comingBack = null;
            PutBoxDown();
            Debug.Log("[ParkPoliceTape] A new tape box is down", this);
        }

        // The hiding spots that creep about steer round the crate and its ring.
        private void KeepBushesOff(Vector3 at)
        {
            if (hidingSpots == null) return;

            foreach (HidingBush bush in hidingSpots.Bushes)
            {
                if (bush != null && bush.TryGetComponent(out BushWalker walker)) walker.KeepClearOf(at, CrateReach);
            }
        }

        private void ForgetBushSpots()
        {
            if (hidingSpots == null) return;

            foreach (HidingBush bush in hidingSpots.Bushes)
            {
                if (bush != null && bush.TryGetComponent(out BushWalker walker)) walker.ForgetSpots();
            }
        }

        private void Clear()
        {
            ForgetBushSpots();
            if (comingBack != null) StopCoroutine(comingBack);
            comingBack = null;

            if (crateSwitch != null)
            {
                crateSwitch.Triggered -= OnTriggered;
                Destroy(crateSwitch.gameObject);
            }

            crateSwitch = null;
            spots.Clear();
        }

        // Tapes off a few hiding spots at random, not ones already taped.
        private void OnTriggered(FloorSwitch tripped, UWBPlayerController player)
        {
            TakeBoxAway();
            if (hidingSpots == null) return;

            choices.Clear();
            foreach (HidingBush bush in hidingSpots.Bushes)
            {
                if (bush != null && !bush.IsLocked) choices.Add(bush);
            }

            int count = Mathf.Min(choices.Count, Random.Range(tapedSpots.x, Mathf.Max(tapedSpots.x, tapedSpots.y) + 1));
            var taped = new List<HidingBush>();
            for (int i = 0; i < count; i++)
            {
                int pick = Random.Range(i, choices.Count);
                (choices[i], choices[pick]) = (choices[pick], choices[i]);
                HidingBush bush = choices[i];
                bush.Lock(lockSeconds);
                BushTape.Play(bush, tapeStrip, tapeWidth, tapeOverhang, smallSpotSize, lockSeconds);
                taped.Add(bush);
            }

            Debug.Log($"[ParkPoliceTape] Player {(player != null ? player.PlayerId : 0)} tapes off {taped.Count} hiding spots for {lockSeconds:0}s", this);
            Taped?.Invoke(player, taped);
        }

        // A random spot clear of the hiding spots (or, when none is found, the tried spot with the
        // most room).
        private Vector3 RandomClearSpot()
        {
            Vector3 roomiest = new Vector3((areaMin.x + areaMax.x) * 0.5f, 0f, (areaMin.y + areaMax.y) * 0.5f);
            float mostRoom = float.MinValue;
            for (int attempt = 0; attempt < SpotTries; attempt++)
            {
                var spot = new Vector3(Random.Range(areaMin.x, areaMax.x), 0f, Random.Range(areaMin.y, areaMax.y));
                if (IsClear(spot)) return spot;

                float room = Room(spot);
                if (room <= mostRoom) continue;
                mostRoom = room;
                roomiest = spot;
            }

            return roomiest;
        }

        // How far the crate's reach is from the nearest hiding spot's picture or stun gun (below 0 when
        // they overlap).
        private float Room(Vector3 spot)
        {
            float room = float.MaxValue;
            if (hidingSpots == null) return room;

            foreach (HidingBush bush in hidingSpots.Bushes)
            {
                if (bush != null) room = Mathf.Min(room, Flat(spot, bush.Center) - bush.Reach - CrateReach);
            }

            if (guns != null)
            {
                foreach (StunGunPickup gun in guns.Waiting)
                {
                    if (gun != null) room = Mathf.Min(room, Flat(spot, gun.transform.position) - CrateReach - clearOfGuns);
                }
            }

            if (mud != null && mud.InMud(spot)) room = Mathf.Min(room, -1f);
            return room;
        }

        private bool IsClear(Vector3 spot)
        {
            return Room(spot) >= clearOfSpots;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }
    }
}
