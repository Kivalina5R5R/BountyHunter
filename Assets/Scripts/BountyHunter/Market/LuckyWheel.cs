using System;
using System.Collections;
using Fortal.Players;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // What the lucky wheel can land on, in the order of its segments (clockwise from the top).
    public enum LuckyWheelResult
    {
        // The thieves run at slowFactor of their speed for effectSeconds.
        SlowThieves,
        // Every wanted thief's bounty goes up by bountyRaise.
        BountiesUp,
        // Every bomb on the floor goes, and none is dropped for effectSeconds.
        NoBombs,
        // The bad one: the thieves run at fastFactor of their speed for effectSeconds.
        FastThieves
    }

    // The market's lucky wheel (stage 3): a small wheel lies in the middle of the floor (the market's
    // hiding spots keep off it: MapSwitcher's Keep Clear). A player who stands on it for holdSeconds
    // sets it off: it goes from the floor in a burst of stars and spins big on the wall instead,
    // stopping on one result picked by weights (LuckyWheelSpin), which then hits the whole floor at
    // once. Hiding spots creeping away with a thief inside steer round its spot all stage, even
    // while it is away. cooldownSeconds after the
    // spin it springs up again in the same spot. Gone when the floor is cleared.
    public sealed class LuckyWheel : MonoBehaviour
    {
        [SerializeField] private BountyRounds rounds;
        [SerializeField] private UWBPlayerSpawner players;
        // The thieves (sped up or slowed) and their hiding spots (kept clear of).
        [SerializeField] private ThiefSpawner thieves;
        [SerializeField] private BombSquad bombs;
        [SerializeField] private int stage = 3;
        // Where the wheel lies (x and world z).
        [SerializeField] private Vector2 spot = Vector2.zero;
        // The game wall's area the spin fills.
        [SerializeField] private RectTransform wall;

        [Header("Spinning")]
        [SerializeField, Min(0.1f)] private float radius = 0.6f;
        [SerializeField, Min(0.1f)] private float holdSeconds = 1.5f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 20f;
        [SerializeField, Min(0.5f)] private float spinSeconds = 2.5f;
        // How likely each result is, in LuckyWheelResult order.
        [SerializeField] private float[] weights = { 3f, 3f, 2f, 2f };

        [Header("Results")]
        [SerializeField, Min(0f)] private float effectSeconds = 6f;
        [SerializeField, Range(0.1f, 1f)] private float slowFactor = 0.5f;
        [SerializeField, Range(1f, 2.5f)] private float fastFactor = 1.4f;
        [SerializeField, Min(0)] private int bountyRaise = 5;

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
        // The wheel on the floor, small enough to sit inside the ring that fills as a player stands on it.
        [SerializeField, Min(0.2f)] private float wheelWidth = 0.75f;
        // The stars twinkling round it while it waits, and the ring filling round it as a player
        // stands on it: the players' own select ring, drawn with the radial fill material.
        [SerializeField] private Sprite[] sparkles = new Sprite[0];
        [SerializeField] private Sprite selectRing;
        [SerializeField] private Material radialFill;
        [SerializeField] private Color selectRingTint = Color.white;

        private static readonly int ResultCount = Enum.GetValues(typeof(LuckyWheelResult)).Length;

        private FloorSwitch waiting;
        private Coroutine comingBack;
        private float effectUntil;
        private float speedFactor = 1f;
        private bool bombsHeld;

        // How far the wheel and its ring reach from its centre.
        private float Reach => Mathf.Max(radius, wheelWidth * 0.5f);

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

            // The wheel always comes back to the same spot, so the hiding spots keep off it all stage.
            KeepBushesOff(new Vector3(spot.x, 0f, spot.y));
            PutWheelDown();
        }

        // Speeds the thieves up or slows them down while a result lasts, and lets the bombs back after.
        private void Update()
        {
            bool lasting = Time.time < effectUntil;
            if (!lasting && bombsHeld) ReleaseBombs();
            if (!lasting && speedFactor == 1f) return;

            if (!lasting) speedFactor = 1f;
            SetThiefSpeed(speedFactor);
        }

        private void PutWheelDown()
        {
            var at = new Vector3(spot.x, 0f, spot.y);
            waiting = FloorSwitch.Create(transform, "Lucky Wheel", at, wheel, wheelWidth, null, selectRing, radialFill, selectRingTint, radius,
                holdSeconds, 0f, players, sparkles);
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
            comingBack = StartCoroutine(BringWheelBack());
        }

        private void Apply(LuckyWheelResult result, UWBPlayerController player)
        {
            switch (result)
            {
                case LuckyWheelResult.SlowThieves:
                    StartSpeed(slowFactor);
                    break;
                case LuckyWheelResult.FastThieves:
                    StartSpeed(fastFactor);
                    break;
                case LuckyWheelResult.BountiesUp:
                    if (rounds != null) rounds.RaiseEveryBounty(bountyRaise);
                    break;
                case LuckyWheelResult.NoBombs:
                    HoldBombs();
                    break;
            }

            Debug.Log($"[LuckyWheel] The wheel lands on {result}", this);
            Landed?.Invoke(player, result);
        }

        private void StartSpeed(float factor)
        {
            speedFactor = factor;
            effectUntil = Time.time + effectSeconds;
        }

        private void HoldBombs()
        {
            if (bombs == null) return;

            bombs.ClearBombs();
            bombs.SetArmed(false);
            bombsHeld = true;
            effectUntil = Mathf.Max(effectUntil, Time.time + effectSeconds);
        }

        private void ReleaseBombs()
        {
            bombsHeld = false;
            if (bombs != null) bombs.SetArmed(true);
        }

        private void SetThiefSpeed(float factor)
        {
            if (thieves == null) return;

            foreach (ThiefController thief in thieves.Thieves)
            {
                if (thief != null) thief.SpeedFactor = factor;
            }
        }

        private IEnumerator BringWheelBack()
        {
            yield return new WaitForSeconds(spinSeconds + cooldownSeconds);
            comingBack = null;
            PutWheelDown();
            Debug.Log("[LuckyWheel] A new lucky wheel is down", this);
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
        private void Clear()
        {
            ForgetBushSpots();
            if (comingBack != null) StopCoroutine(comingBack);
            comingBack = null;
            if (waiting != null)
            {
                waiting.Triggered -= OnTriggered;
                Destroy(waiting.gameObject);
            }

            waiting = null;
            if (wall != null)
            {
                foreach (LuckyWheelSpin spin in wall.GetComponentsInChildren<LuckyWheelSpin>()) Destroy(spin.gameObject);
            }

            if (speedFactor != 1f) SetThiefSpeed(1f);
            speedFactor = 1f;
            effectUntil = 0f;
            bombsHeld = false;
        }

        // The hiding spots that creep about steer round the wheel and its ring.
        private void KeepBushesOff(Vector3 at)
        {
            if (thieves == null) return;

            foreach (HidingBush bush in thieves.Bushes)
            {
                if (bush != null && bush.TryGetComponent(out BushWalker walker)) walker.KeepClearOf(at, Reach);
            }
        }

        private void ForgetBushSpots()
        {
            if (thieves == null) return;

            foreach (HidingBush bush in thieves.Bushes)
            {
                if (bush != null && bush.TryGetComponent(out BushWalker walker)) walker.ForgetSpots();
            }
        }
    }
}
