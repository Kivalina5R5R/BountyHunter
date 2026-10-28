using System;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // How the boss moves and acts, tuned in BossChase's Inspector. Distances are metres on the floor,
    // frame numbers count from 0 in the move's sheet (15 a second).
    [Serializable]
    public sealed class BossSettings
    {
        [Header("Look")]
        // Pixels of the sheets per metre on the floor: 340 makes the boss about 1 m tall.
        [SerializeField, Min(1f)] private float pixelsPerUnit = 340f;
        // Over the passers-by (8, and 9 while their pictures cross-fade), so one never hides it.
        [SerializeField] private int bodySortingOrder = 10;

        [Header("Running away")]
        [SerializeField, Min(0.1f)] private float runSpeed = 1.4f;
        // How far one frame of the walk carries it (metres). The walk plays as fast as it moves, so
        // the feet keep up with the floor; at 0.06 a run of 1.4 m/s plays the walk at about 23 frames
        // a second instead of its own 15.
        [SerializeField, Min(0.005f)] private float metresPerWalkFrame = 0.06f;
        // Sways this far either side of its way at full run (less when slower), once every wobbleSeconds.
        [SerializeField, Min(0f)] private float wobble = 0.1f;
        [SerializeField, Min(0.1f)] private float wobbleSeconds = 1.4f;
        // Runs from every player closer than this; with none that close it strolls about.
        [SerializeField, Min(0.5f)] private float fleeRadius = 3.2f;
        // Starts turning away from an edge this far from it, so it does not run into a corner.
        [SerializeField, Min(0f)] private float edgeMargin = 0.9f;
        // Like a thief: running in the open it picks a way out and sticks to it this long (unless a
        // player or the edge is right ahead), so it breaks away instead of running in circles; it
        // gets up to speed at this rate; and it only calms down once no player has been within
        // fleeRadius for calmSeconds.
        [SerializeField, Min(0f)] private float commitSeconds = 1f;
        [SerializeField, Min(0.1f)] private float acceleration = 2.5f;
        [SerializeField, Min(0f)] private float calmSeconds = 1.2f;
        // It never goes into a corner of the floor: each corner is rounded off this far (metres).
        [SerializeField, Min(0f)] private float cornerSize = 1.6f;

        // Steers round passers-by closer than passerClearance and never comes nearer than passerGap.
        [SerializeField, Min(0f)] private float passerClearance = 1.1f;
        [SerializeField, Min(0f)] private float passerGap = 0.6f;

        [Header("Being touched")]
        // A touch makes it warp away (the smoke bomb) to a spot at least warpMinDistance from where
        // it was, as far from the players as it can find.
        [SerializeField, Min(0.1f)] private float touchRadius = 0.6f;
        [SerializeField, Min(0f)] private float warpMinDistance = 5f;

        [Header("When to do what")]
        // Warp before shooting or disguising only when a player is within this distance.
        [SerializeField, Min(0.1f)] private float skillClearance = 1.5f;
        // Shoots when two players stand this close together (water for the group), at most once every
        // shootEverySeconds.
        [SerializeField, Min(0.1f)] private float huddleRadius = 2f;
        [SerializeField, Min(0f)] private float shootEverySeconds = 7f;
        // Puts on a disguise when the nearest player comes within this range (near, but not on top of
        // it, so it has time to wrap up), at most once every disguiseEverySeconds.
        [SerializeField] private Vector2 disguiseWhenPlayerAt = new Vector2(1.3f, 3f);
        [SerializeField, Min(0f)] private float disguiseEverySeconds = 10f;
        // Ducks into a free hiding spot within hideReach that it reaches well before any player, with
        // no player within hideClearance of it, while being chased, at most once every hideEverySeconds.
        // It stays hidden at most hideSeconds; a player standing on the spot for flushSeconds drives it out.
        [SerializeField, Min(0f)] private float hideReach = 3.5f;
        [SerializeField, Min(0f)] private float hideClearance = 1.5f;
        [SerializeField, Min(0f)] private float hideEverySeconds = 10f;
        [SerializeField, Min(0.5f)] private float hideSeconds = 5f;
        [SerializeField, Min(0.1f)] private float flushSeconds = 1f;
        // Like a thief: with no player near it sneaks out after a time in this range (a player near
        // keeps it in longer, up to hideSeconds); its way to the spot keeps routeClearance from every
        // player ahead; and it does not go back into the spot it last left. For exitGraceSeconds after
        // coming out, and after anything it does standing still (a shot, a smoke bomb, throwing its
        // cape off), it runs and cannot be caught, a fair head start.
        [SerializeField] private Vector2 sneakOutSeconds = new Vector2(3f, 5f);
        [SerializeField, Min(0f)] private float routeClearance = 0.9f;
        [SerializeField, Min(0f)] private float exitGraceSeconds = 1f;
        // Normal running stays catchable this long before another skill; a trapped boss exposes its smoke windup instead.
        [SerializeField, Min(0f)] private float catchWindowSeconds = 1f;

        [Header("Moves")]
        // Besides the moves above, now and then (a time in this range, counted while it runs) it
        // throws the smoke bomb anyway.
        [SerializeField] private Vector2 moveEverySeconds = new Vector2(8f, 12f);
        // After any move it runs at least this long before the next, so the players get a real
        // chance at it between moves.
        [SerializeField, Min(0f)] private float actionGapSeconds = 1.5f;

        [Header("Shoot")]
        // The stretch of Shoot from the gun already raised to the end of its first shot (the video
        // fires three times), and the stretch played after it to lower the gun. As the water leaves the
        // gun (releaseFrame) it comes down in rainCircles circles this wide (radius) at random spots,
        // landing rainWarningSeconds after they show.
        [SerializeField] private Vector2Int firingFrames = new Vector2Int(8, 25);
        // The water comes down from the moment this frame shows (the water leaving the gun).
        [SerializeField, Min(0)] private int releaseFrame = 17;
        [SerializeField] private Vector2Int lowerGunFrames = new Vector2Int(55, 64);
        [SerializeField, Min(1)] private int rainCircles = 7;
        [SerializeField, Min(0.1f)] private float rainRadius = 0.7f;
        [SerializeField, Min(0.1f)] private float rainWarningSeconds = 1.6f;
        [SerializeField] private Color rainColor = new Color(0.25f, 0.7f, 1f, 0.45f);
        [SerializeField] private BossWaterArt water = new BossWaterArt();

        [Header("Smoke bomb")]
        // The stretch of SmokeBomb played. It throws the bomb down and the cloud covers it; the cloud
        // sits there until vanishAtFrame, when the cloud is left fading where it was
        // (originCloudFrames) and, once it is gone, the boss jumps to a spot far from the players.
        // There the cloud picks up from arriveFromFrame (already puffed up) and billows a while before
        // it clears to show the boss (the cloud frames up to 66, then the boss coming out of it). It cannot be touched
        // all the while.
        [SerializeField] private Vector2Int smokeFrames = new Vector2Int(20, 76);
        [SerializeField, Min(0)] private int vanishAtFrame = 40;
        [SerializeField, Min(0)] private int arriveFromFrame = 54;
        // From steppedOutFrame (the boss coming out of the cloud) a copy of the cloud alone
        // (lingeringCloudFrame) stays over it and fades over lingeringCloudFadeSeconds.
        [SerializeField, Min(0)] private int steppedOutFrame = 66;
        [SerializeField, Min(0)] private int lingeringCloudFrame = 55;
        [SerializeField, Min(0.05f)] private float lingeringCloudFadeSeconds = 0.9f;
        // Where the smoke bomb starts when it warps away from the first touch (the bomb going down).
        [SerializeField, Min(0)] private int warpFromFrame = 34;
        // The cloud left where it vanished billows through these frames (cloud only), then fades this
        // fast; only once it is gone does the new cloud puff up where the boss comes out.
        [SerializeField] private Vector2Int originCloudFrames = new Vector2Int(40, 52);
        [SerializeField, Min(0.05f)] private float originCloudFadeSeconds = 0.25f;

        [Header("Disguise")]
        // The stretch of CapeWrap played to wrap up, and the one played to throw the cape off, at
        // revealPace of its own rate (0.8: a little slower, so the cape's swirl shows).
        // Turning from the wrapped boss into the passer-by and back cross-fades over changeSeconds.
        [SerializeField] private Vector2Int wrapFrames = new Vector2Int(10, 31);
        [SerializeField] private Vector2Int revealFrames = new Vector2Int(72, 86);
        [SerializeField, Range(0.2f, 1f)] private float revealPace = 0.8f;
        [SerializeField, Min(0.05f)] private float changeSeconds = 0.6f;
        [SerializeField, Min(1f)] private float disguiseSeconds = 8f;
        // Strolls like a passer-by while disguised.
        [SerializeField, Min(0.1f)] private float disguiseSpeed = 0.6f;
        // The give-away: its top hat stays on, this wide, and it flickers in this colour now and then.
        [SerializeField] private Sprite hat;
        [SerializeField, Min(0.05f)] private float hatWidth = 0.32f;
        [SerializeField, Min(0.2f)] private float shimmerEverySeconds = 2.2f;
        [SerializeField] private Color shimmerColor = new Color(0.55f, 0.75f, 1f, 1f);

        public float PixelsPerUnit => pixelsPerUnit;
        public int SortingOrder => bodySortingOrder;
        public float RunSpeed => runSpeed;
        public float MetresPerWalkFrame => metresPerWalkFrame;
        public float Wobble => wobble;
        public float WobbleSeconds => wobbleSeconds;
        public float FleeRadius => fleeRadius;
        public float SkillClearance => Mathf.Max(touchRadius, skillClearance);
        public float EdgeMargin => edgeMargin;
        public float CommitSeconds => commitSeconds;
        public float Acceleration => acceleration;
        public float CalmSeconds => calmSeconds;
        public float CornerSize => cornerSize;
        public float TouchRadius => touchRadius;
        public float WarpMinDistance => warpMinDistance;
        public float HuddleRadius => huddleRadius;
        public float ShootEverySeconds => shootEverySeconds;
        public Vector2 DisguiseWhenPlayerAt => disguiseWhenPlayerAt;
        public float DisguiseEverySeconds => disguiseEverySeconds;
        public float HideReach => hideReach;
        public float HideClearance => hideClearance;
        public float HideEverySeconds => hideEverySeconds;
        public float HideSeconds => hideSeconds;
        public float FlushSeconds => flushSeconds;
        public Vector2 SneakOutSeconds => sneakOutSeconds;
        public float RouteClearance => routeClearance;
        public float ExitGraceSeconds => exitGraceSeconds;
        public float CatchWindowSeconds => catchWindowSeconds;
        public Vector2 MoveEverySeconds => moveEverySeconds;
        public float ActionGapSeconds => actionGapSeconds;
        public Vector2Int FiringFrames => firingFrames;
        public int ReleaseFrame => releaseFrame;
        public Vector2Int LowerGunFrames => lowerGunFrames;
        public int RainCircles => rainCircles;
        public float RainRadius => rainRadius;
        public float RainWarningSeconds => rainWarningSeconds;
        public Color RainColor => rainColor;
        public BossWaterArt Water => water;
        public Vector2Int SmokeFrames => smokeFrames;
        public int VanishAtFrame => vanishAtFrame;
        public int ArriveFrame => arriveFromFrame;
        public int SteppedOutFrame => steppedOutFrame;
        public int LingeringCloudFrame => lingeringCloudFrame;
        public float LingeringCloudFadeSeconds => lingeringCloudFadeSeconds;
        public int WarpFromFrame => warpFromFrame;
        public Vector2Int OriginCloudFrames => originCloudFrames;
        public float OriginCloudFadeSeconds => originCloudFadeSeconds;
        public float PasserClearance => passerClearance;
        public float PasserGap => passerGap;
        public Vector2Int WrapFrames => wrapFrames;
        public Vector2Int RevealFrames => revealFrames;
        public float RevealPace => revealPace;
        public float ChangeSeconds => changeSeconds;
        public float DisguiseSeconds => disguiseSeconds;
        public float DisguiseSpeed => disguiseSpeed;
        public Sprite Hat => hat;
        public float HatWidth => hatWidth;
        public float ShimmerEverySeconds => shimmerEverySeconds;
        public Color ShimmerColor => shimmerColor;
    }
}
