using System;
using System.Collections;
using System.Collections.Generic;
using Fortal.Display;
using Fortal.Players;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // What the lucky wheel can land on, in the order of its segments (clockwise from the top).
    public enum LuckyWheelResult
    {
        // extraSpots more hiding spots spring up for extraSpotSeconds, sparkling so they stand out
        // (ExtraSpotMark), then shrink away (good for the thieves).
        MoreHiding,
        // The loudspeaker: more thieves join the round until mostThieves are wanted, and
        // extraPassersBy more passers-by come in.
        CallOut,
        // Every player on the floor gets a stun gun.
        StunGunsForAll,
        // The police cars drive from the left and right ends to the bottom and top (or back).
        MoveCars
    }

    // The market's lucky wheel (stage 3): a mystery box lies in the middle of the floor, its panels
    // running through the rainbow (the market's hiding spots keep off it: MapSwitcher's Keep Clear).
    // A player who stands on it for holdSeconds sets it off: it goes from the floor in a burst of
    // stars and a wheel spins big on the wall instead, stopping on one result picked by weights
    // (LuckyWheelSpin), which then happens on the floor. Hiding spots creeping away with a thief
    // inside steer round its spot all stage, even while it is away. cooldownSeconds after the spin
    // it springs up again in the same spot. Gone when the floor is cleared.
    public sealed class LuckyWheel : MonoBehaviour
    {
        [SerializeField] private BountyRounds rounds;
        [SerializeField] private UWBPlayerSpawner players;
        // The thieves and their hiding spots, and what lays the hiding spots out.
        [SerializeField] private ThiefSpawner thieves;
        [SerializeField] private BushScatter hidingSpots;
        [SerializeField] private NpcCrowd passersBy;
        [SerializeField] private StunGuns stunGuns;
        [SerializeField] private PoliceCuffs cuffs;
        [SerializeField] private int stage = 3;
        // Where the box lies (x and world z).
        [SerializeField] private Vector2 spot = Vector2.zero;
        // The game wall's area the spin fills.
        [SerializeField] private RectTransform wall;

        [Header("Spinning")]
        [SerializeField, Min(0.1f)] private float radius = 0.6f;
        [SerializeField, Min(0.1f)] private float holdSeconds = 1.5f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 20f;
        [SerializeField, Min(0.5f)] private float spinSeconds = 2.5f;
        // How likely each result is, in LuckyWheelResult order.
        [SerializeField] private float[] weights = { 1f, 1f, 1f, 1f };

        [Header("Results")]
        [SerializeField, Min(1)] private int extraSpots = 2;
        [SerializeField, Min(1f)] private float extraSpotSeconds = 20f;
        [SerializeField, Min(1)] private int mostThieves = 5;
        [SerializeField, Min(0)] private int extraPassersBy = 2;

        [Header("Look")]
        // The wheel with its result icons on its segments (Tools/MarketArt), the pointer over it while
        // it spins on the wall, and each result's icon in LuckyWheelResult order, popped up as it lands.
        [SerializeField] private Sprite wheel;
        [SerializeField] private Sprite pointer;
        [SerializeField] private Sprite[] resultIcons = new Sprite[0];
        // Each segment's colour, in LuckyWheelResult order, for the glow as it lands.
        [SerializeField] private Color[] resultColours =
        {
            new Color(0.4f, 0.85f, 0.35f, 1f),
            new Color(1f, 0.82f, 0.2f, 1f),
            new Color(0.3f, 0.6f, 1f, 1f),
            new Color(1f, 0.3f, 0.25f, 1f),
        };
        // What lies on the floor: the mystery box (the wheel when empty), drawn with the rainbow
        // material (Sprite Rainbow) so its panels run through the rainbow, small enough to sit inside
        // the ring that fills as a player stands on it.
        [SerializeField] private Sprite mysteryBox;
        [SerializeField] private Material rainbow;
        [SerializeField, Min(0.2f)] private float wheelWidth = 0.55f;
        // The stars twinkling round it while it waits, and the ring filling round it as a player
        // stands on it: the players' own select ring, drawn with the radial fill material.
        [SerializeField] private Sprite[] sparkles = new Sprite[0];
        [SerializeField] private Sprite selectRing;
        [SerializeField] private Material radialFill;
        [SerializeField] private Color selectRingTint = Color.white;

        private static readonly int ResultCount = Enum.GetValues(typeof(LuckyWheelResult)).Length;

        private readonly List<Coroutine> extraSpotTimers = new List<Coroutine>();
        private readonly List<ExtraSpotMark> extraSpotMarks = new List<ExtraSpotMark>();
        private FloorSwitch waiting;
        private Coroutine comingBack;

        // How far the box and its ring reach from its centre.
        private float Reach => Mathf.Max(radius, wheelWidth * 0.5f);

        private Vector3 Spot => new Vector3(spot.x, 0f, spot.y);

        // A player set the wheel spinning and it landed on this.
        public event Action<UWBPlayerController, LuckyWheelResult> Landed;

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

            // The box always comes back to the same spot, so the hiding spots keep off it all stage.
            KeepBushesOff();
            PutBoxDown();
        }

        private void PutBoxDown()
        {
            Sprite onFloor = mysteryBox != null ? mysteryBox : wheel;
            waiting = FloorSwitch.Create(transform, "Lucky Wheel", Spot, onFloor, wheelWidth, null, selectRing, radialFill, selectRingTint, radius,
                holdSeconds, 0f, players, sparkles);
            if (rainbow != null && mysteryBox != null) waiting.UsePropMaterial(rainbow);
            waiting.Triggered += OnTriggered;
        }

        private void OnTriggered(FloorSwitch tripped, UWBPlayerController player)
        {
            tripped.Triggered -= OnTriggered;
            tripped.Vanish();
            waiting = null;

            var result = (LuckyWheelResult)PickResult();
            int index = (int)result;
            Sprite icon = resultIcons != null && index < resultIcons.Length ? resultIcons[index] : null;
            Color colour = resultColours != null && index < resultColours.Length ? resultColours[index] : Color.white;
            Debug.Log($"[LuckyWheel] Player {(player != null ? player.PlayerId : 0)} spins the wheel: {result}", this);
            // Without a wall to spin on, it lands at once.
            if (wall != null) LuckyWheelSpin.Play(wall, wheel, pointer, icon, index, ResultCount, spinSeconds, colour, () => Apply(result, player));
            else Apply(result, player);
            comingBack = StartCoroutine(BringBoxBack());
        }

        private void Apply(LuckyWheelResult result, UWBPlayerController player)
        {
            switch (result)
            {
                case LuckyWheelResult.MoreHiding:
                    AddHidingSpots();
                    break;
                case LuckyWheelResult.CallOut:
                    if (rounds != null) rounds.CallOutThieves(mostThieves);
                    if (passersBy != null) passersBy.CallOut(extraPassersBy);
                    break;
                case LuckyWheelResult.StunGunsForAll:
                    ArmEveryone();
                    break;
                case LuckyWheelResult.MoveCars:
                    if (cuffs != null) cuffs.MoveCars();
                    break;
            }

            Debug.Log($"[LuckyWheel] The wheel lands on {result}", this);
            Landed?.Invoke(player, result);
        }

        // More hiding spots spring up, keeping off the box and the cars, and go again after a while.
        private void AddHidingSpots()
        {
            if (hidingSpots == null) return;

            IReadOnlyList<ScatteredBush> added = hidingSpots.AddMore(extraSpots);
            var marks = new List<ExtraSpotMark>();
            foreach (ScatteredBush bush in added)
            {
                if (bush.Transform == null) continue;

                ExtraSpotMark mark = ExtraSpotMark.Create(transform, bush.Transform, bush.Reach * 1.5f, sparkles);
                marks.Add(mark);
                extraSpotMarks.Add(mark);
                if (!bush.Transform.TryGetComponent(out BushWalker walker)) continue;

                walker.KeepClearOf(Spot, Reach);
                if (cuffs != null) cuffs.KeepClearOfCars(walker);
            }

            Debug.Log($"[LuckyWheel] {added.Count} of {extraSpots} more hiding spots for {extraSpotSeconds}s", this);
            extraSpotTimers.Add(StartCoroutine(TakeAwayLater(added, marks)));
        }

        private IEnumerator TakeAwayLater(IReadOnlyList<ScatteredBush> added, List<ExtraSpotMark> marks)
        {
            yield return new WaitForSeconds(extraSpotSeconds);
            foreach (ExtraSpotMark mark in marks)
            {
                if (mark != null) mark.Finish();
                extraSpotMarks.Remove(mark);
            }

            foreach (ScatteredBush bush in added)
            {
                if (bush.Transform != null) hidingSpots.Remove(bush.Transform);
            }
        }

        private void ArmEveryone()
        {
            if (stunGuns == null || players == null) return;

            foreach (UWBPlayerController player in players.SpawnedPlayers)
            {
                if (player != null && player.isActiveAndEnabled) stunGuns.Arm(player);
            }
        }

        private IEnumerator BringBoxBack()
        {
            yield return new WaitForSeconds(spinSeconds + cooldownSeconds);
            comingBack = null;
            PutBoxDown();
            Debug.Log("[LuckyWheel] The mystery box is back", this);
        }

        // A result index by the weights (all alike when none are set).
        private int PickResult()
        {
            float total = 0f;
            for (int i = 0; i < ResultCount; i++) total += Weight(i);
            if (total <= 0f) return Random.Range(0, ResultCount);

            float roll = Random.value * total;
            for (int i = 0; i < ResultCount; i++)
            {
                roll -= Weight(i);
                if (roll < 0f) return i;
            }

            return ResultCount - 1;
        }

        private float Weight(int index)
        {
            return weights != null && index < weights.Length ? Mathf.Max(0f, weights[index]) : 1f;
        }

        // Everything the wheel set going stops; the floor is being cleared or another stage starts.
        // Extra hiding spots still up go with the next layout, and so does what the hiding spots keep
        // clear of (each stage lays new ones; the cars' spots must not be forgotten here either).
        private void Clear()
        {
            if (comingBack != null) StopCoroutine(comingBack);
            comingBack = null;
            foreach (Coroutine timer in extraSpotTimers)
            {
                if (timer != null) StopCoroutine(timer);
            }

            extraSpotTimers.Clear();
            foreach (ExtraSpotMark mark in extraSpotMarks)
            {
                if (mark != null) Destroy(mark.gameObject);
            }

            extraSpotMarks.Clear();
            if (waiting != null)
            {
                waiting.Triggered -= OnTriggered;
                Destroy(waiting.gameObject);
            }

            waiting = null;
            if (wall == null) return;

            foreach (LuckyWheelSpin spin in wall.GetComponentsInChildren<LuckyWheelSpin>()) Destroy(spin.gameObject);
        }

        // The hiding spots that creep about steer round the box and its ring.
        private void KeepBushesOff()
        {
            if (thieves == null) return;

            foreach (HidingBush bush in thieves.Bushes)
            {
                if (bush != null && bush.TryGetComponent(out BushWalker walker)) walker.KeepClearOf(Spot, Reach);
            }
        }
    }
}
