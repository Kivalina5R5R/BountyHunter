using System;
using System.Collections;
using System.Collections.Generic;
using Fortal.Players;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // Boss phase 1, the vault (before the chase, BossFinale): the diamond sits on its pedestal in the
    // middle of the floor and the boss (VaultBoss, the chase's chameleon) tries to stand on it long
    // enough to steal it while the players keep it off, for as long as the chase lasts. The boss on the diamond
    // (within stealRadius) fills the steal ring round it over stealSeconds; off it the ring drains
    // over drainSeconds rather than starting over. A full ring steals the diamond: every player pays
    // stolenFine (or what they have). Holding out to the end secures it: every player earns
    // securedReward. Either way the chase follows at once. The boss comes in at a random spot along
    // the floor's edge (as far from the players as it can) and makes for the diamond. A player who
    // catches it (touches it) earns bumpReward, and it warps in a puff of smoke to another random
    // spot along the edge and comes back in another look than before: as one of the passers-by or
    // thieves (any kind, out on the floor or not) for disguiseSeconds, or as itself. In a blackout
    // it cannot be caught at all: the lights must go back on first.
    // Whether the boss can be seen: never inside its smoke; always for revealSeconds after it trips
    // a laser and always within alwaysSeenNear of the diamond; otherwise while the lights are on,
    // always as it first comes in, but sneaking (after a catch or its smoke bomb) only with a player
    // within sneakSeenWithin; in a blackout only in a player's glow or under a switch's lamp. Its skills: a
    // smoke bomb (smokeRadius, smokeSeconds) when a player comes within smokeWhenPlayerWithin, the
    // first after smokeFirstAt and then every smokeEvery; and the blackout, the first at
    // blackoutFirstAt for blackoutSeconds, then every blackoutEverySeconds from the last. In a
    // blackout two light switches stand in opposite corners (switchSpots); a player who stands on
    // one for switchHoldSeconds switches it on; with fewer than bothSwitchesFromPlayers players one
    // switch puts the lights on, with that many or more both must be on (a switch on stays dimmed).
    // Lasers pass across the floor from edge to edge, one pass at a time, and are gone in a
    // blackout (VaultLasers); the boss touching one shakes it,
    // and sets the alarm off (it is seen for revealSeconds, the wall's edge glows red once) at most once
    // every alarmCooldownSeconds. A few passers-by stroll about to pass for (NpcCrowd; grabbing one
    // costs as in a stage). It has its own floor and wall pictures (black where one is missing).
    // Console lines start with [BossVault].
    public sealed class BossVault : StageInterlude
    {
        // The diamond and its ring lie on the floor under the characters (thieves 8, players 20), who
        // walk over it; the dark sheet and the lasers go over the characters.
        private const int RingOrder = 5;
        private const int DiamondOrder = 6;
        private const int DarkOrder = 24;
        private const int LaserOrder = 25;
        private const int SmokeOrder = 21;
        // The alarm glow: how far it reaches in from the wall's edge, how long its one swell and
        // fade lasts (shorter than alarmCooldownSeconds, so glows never overlap) and how strong it gets.
        private const float AlarmGlowWidth = 70f;
        private const float AlarmGlowSeconds = 1.2f;
        private const float AlarmGlowPeak = 0.75f;
        private const int AlarmGlowPixels = 64;

        [SerializeField] private UWBPlayerSpawner playerSpawner;
        [SerializeField] private PlayerScoreTag[] scoreTags;
        [SerializeField] private NpcCrowd npcs;
        // The thieves the boss can pass for, besides every passer-by kind (prefabs; none need be out).
        [SerializeField] private ThiefController[] thiefKinds = new ThiefController[0];
        // The market's hiding spots, put away while the vault plays (there are none in a vault).
        [SerializeField] private ThiefSpawner hidingSpots;
        [SerializeField] private StageTimer timer;
        // The chase that follows: the boss's look and settings, and how the wall is cleared.
        [SerializeField] private BossChase chase;

        [Header("Room")]
        [SerializeField] private RawImage floorImage;
        // Where the wall's background pictures are: the vault's wall picture covers them while it plays.
        [SerializeField] private RectTransform wallBackground;
        // The vault's floor and wall (Assets/ART/BG/MapBoss/P1); either may be empty, for black.
        [SerializeField] private Texture floorPicture;
        [SerializeField] private Sprite wallPicture;
        [SerializeField] private Vector2 areaMin = new Vector2(-5.15f, -2.3f);
        [SerializeField] private Vector2 areaMax = new Vector2(5.15f, 2.3f);

        [Header("Diamond")]
        [SerializeField] private Sprite diamondPedestal;
        [SerializeField, Min(0.2f)] private float diamondWidth = 0.9f;
        // The ring round the pedestal filling as the boss steals: the players' select ring, drawn with
        // the radial fill material.
        [SerializeField] private Sprite stealRing;
        [SerializeField] private Material radialFill;
        [SerializeField] private Color stealColour = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField, Min(0.1f)] private float stealRadius = 0.45f;
        [SerializeField, Min(0.5f)] private float stealSeconds = 4f;
        [SerializeField, Min(1f)] private float drainSeconds = 24f;

        [Header("Boss")]
        // Passers-by stroll at 0.3, so a disguised boss walks only a little faster.
        [SerializeField] private VaultBoss.Speeds bossSpeeds = new VaultBoss.Speeds
        {
            Approach = 0.7f, Sneak = 0.5f, Disguised = 0.35f, BumpRadius = 0.6f,
        };
        [SerializeField, Min(0f)] private float arriveAfterSeconds = 1.5f;
        [SerializeField, Min(0f)] private float sneakSeenWithin = 1.8f;
        [SerializeField, Min(0f)] private float alwaysSeenNear = 1.2f;
        // Where along the edge it comes in and warps to: this far in from the edge, and at least
        // edgeClearOfPlayers from every player when the floor allows.
        [SerializeField, Min(0f)] private float edgeInset = 0.5f;
        [SerializeField, Min(0f)] private float edgeClearOfPlayers = 2.5f;
        [SerializeField, Min(1f)] private float disguiseSeconds = 8f;
        // The puff of smoke it warps in and out of.
        [SerializeField, Min(0.1f)] private float warpPuffRadius = 0.7f;
        [SerializeField, Min(0.1f)] private float warpPuffSeconds = 0.6f;
        // How long it is gone after a catch before it comes back at another spot.
        [SerializeField, Min(0f)] private float warpDelaySeconds = 0.5f;

        [Header("Skills")]
        [SerializeField, Min(0f)] private float smokeFirstAt = 7f;
        [SerializeField] private Vector2 smokeEvery = new Vector2(9f, 13f);
        [SerializeField, Min(0f)] private float smokeWhenPlayerWithin = 2.5f;
        [SerializeField, Min(0.2f)] private float smokeRadius = 1.15f;
        [SerializeField, Min(0.5f)] private float smokeSeconds = 6f;
        [SerializeField, Min(0f)] private float blackoutFirstAt = 16f;
        [SerializeField, Min(1f)] private float blackoutSeconds = 8f;
        [SerializeField, Min(1f)] private float blackoutEverySeconds = 26f;

        [Header("Blackout")]
        // The dark sheet's material (Assets/ART/Vault/VaultDarkness.mat, the Vault Darkness shader).
        [SerializeField] private Material darknessMaterial;
        // Round each player the dark lifts a little (flashlightLift of it), enough to see themselves,
        // and the boss can be seen there; round each light switch it lifts fully, out to lampRadius.
        // Both fade softly at their edges.
        [SerializeField, Min(0.2f)] private float flashlightRadius = 0.9f;
        [SerializeField, Range(0f, 1f)] private float flashlightLift = 0.35f;
        [SerializeField, Min(0.2f)] private float lampRadius = 1.1f;
        [SerializeField, Range(0.5f, 1f)] private float darkAlpha = 0.94f;
        [SerializeField] private Vector2[] switchSpots = { new Vector2(-4.6f, -1.75f), new Vector2(4.6f, 1.75f) };
        [SerializeField, Min(0.1f)] private float switchRadius = 0.6f;
        [SerializeField, Min(0.1f)] private float switchHoldSeconds = 1f;
        // From this many players on, both switches must be on to put the lights back.
        [SerializeField, Min(1)] private int bothSwitchesFromPlayers = 3;
        [SerializeField] private Sprite lightSwitch;
        [SerializeField, Min(0.1f)] private float switchWidth = 0.55f;
        [SerializeField] private Sprite[] sparkles = new Sprite[0];

        [Header("Lasers")]
        // Two upright beams passing left or right, one lying beam passing up or down: each crosses
        // the floor once and is gone for a while, at its own times so they seldom come together.
        // None shows in a blackout.
        [SerializeField] private VaultLasers.Beam[] laserPasses =
        {
            new VaultLasers.Beam(true, 5f, 13f, 3f),
            new VaultLasers.Beam(true, 6f, 17f, 8f),
            new VaultLasers.Beam(false, 4f, 21f, 5f),
        };
        [SerializeField, Min(0.01f)] private float laserWidth = 0.07f;
        [SerializeField, Min(0.01f)] private float laserReach = 0.18f;
        [SerializeField] private Color laserColour = new Color(1f, 0.15f, 0.2f, 1f);
        [SerializeField, Min(0f)] private float revealSeconds = 2f;
        [SerializeField, Min(0f)] private float alarmCooldownSeconds = 1.5f;
        [SerializeField] private Color alarmColour = new Color(1f, 0.1f, 0.1f, 1f);

        [Header("Money")]
        [SerializeField, Min(0)] private int bumpReward = 5;
        [SerializeField, Min(0)] private int securedReward = 10;
        [SerializeField, Min(0)] private int stolenFine = 10;
        [SerializeField] private Color gainColor = new Color(1f, 0.82f, 0.25f, 1f);
        [SerializeField] private Color damageColor = new Color(1f, 0.22f, 0.16f, 1f);
        [SerializeField] private TMP_FontAsset damageFont;
        [SerializeField] private Material damageMaterial;

        [Header("Wall")]
        // The clock's heading all through the vault (the steal ring on the floor shows how far the
        // boss has got).
        [SerializeField] private string phaseHeading = "BOSS PHASE 1";

        private static readonly int FillId = Shader.PropertyToID("_Fill");

        private readonly List<GameObject> built = new List<GameObject>();
        private readonly List<GameObject> hiddenSpots = new List<GameObject>();
        private readonly List<VaultSmoke> smokes = new List<VaultSmoke>();
        private readonly List<FloorSwitch> switches = new List<FloorSwitch>();
        private readonly List<Image> wallFrame = new List<Image>();
        private readonly List<Vector3> passerPositions = new List<Vector3>();
        private SpawnerPlayerLocator players;
        private BossStaging staging;
        private PictureSwap floorSwap;
        private Image wallSheet;
        private VaultBoss boss;
        private VaultLasers laserBeams;
        private VaultDarkness darkness;
        // Switches this blackout still needs on before the lights come back.
        private int switchesNeeded;
        private SpriteRenderer ring;
        private MaterialPropertyBlock ringBlock;
        private float startedAt;
        private float stolen;
        private bool skipping;
        private bool ready;
        private float nextSmokeAt;
        private float nextBlackoutAt;
        private float lightsBackAt;
        private float revealedUntil;
        private float alarmReadyAt;
        private float alarmAt = float.MinValue;
        private int touchingBeam = -1;

        // The vault ended: true when the boss stole the diamond.
        public event Action<bool> Ended;
        // A player bumped the boss; the alarm went off; the lights went out or came back on.
        public event Action<UWBPlayerController> BossBumped;
        public event Action AlarmRang;
        public event Action<bool> LightsChanged;

        private Vector3 Diamond => new Vector3((areaMin.x + areaMax.x) * 0.5f, 0f, (areaMin.y + areaMax.y) * 0.5f);

        private float Elapsed => Time.time - startedAt;

        private void Awake()
        {
            players = new SpawnerPlayerLocator(playerSpawner);
        }

        // The chase's boss sheets are loaded first.
        public override IEnumerator Prepare()
        {
            if (chase != null) yield return chase.Prepare();
        }

        public override void Begin()
        {
            skipping = false;
            stolen = 0f;
            touchingBeam = -1;
            revealedUntil = 0f;
            alarmReadyAt = 0f;
            startedAt = Time.time + arriveAfterSeconds;
            nextSmokeAt = startedAt + smokeFirstAt;
            nextBlackoutAt = startedAt + blackoutFirstAt;
            lightsBackAt = float.MaxValue;
            ready = chase != null;
            if (!ready)
            {
                Debug.LogError("[BossVault] No boss chase set to borrow the boss from, so the vault is skipped", this);
                return;
            }

            ShowRoom();
            BuildDiamond();
            laserBeams = VaultLasers.Create(transform, areaMin, areaMax, laserPasses, laserWidth, laserColour, LaserOrder);
            built.Add(laserBeams.gameObject);
            darkness = VaultDarkness.Create(transform, areaMax - areaMin, DarkOrder, darknessMaterial, playerSpawner, flashlightRadius, flashlightLift, lampRadius,
                darkAlpha);
            built.Add(darkness.gameObject);
            PutBossDown();
            if (npcs != null) npcs.Begin();
            if (timer != null) timer.StartCountdown(phaseHeading, chase.Seconds);
            Debug.Log("[BossVault] The vault is open", this);
        }

        public override IEnumerator Run()
        {
            if (!ready) yield break;

            while (Time.time < startedAt && !skipping) yield return null;
            if (boss != null) boss.Arrive();

            // As long as the chase, counted from the vault opening (the boss comes in a moment after).
            float over = startedAt - arriveAfterSeconds + chase.Seconds;
            while (Time.time < over && !skipping && stolen < 1f)
            {
                Play();
                yield return null;
            }

            Finish(stolen >= 1f);
        }

        public override void End()
        {
            StopAllCoroutines();
            if (boss != null) Destroy(boss.gameObject);
            boss = null;
            foreach (FloorSwitch lights in switches)
            {
                if (lights != null) Destroy(lights.gameObject);
            }

            switches.Clear();
            foreach (VaultSmoke smoke in smokes)
            {
                if (smoke != null) Destroy(smoke.gameObject);
            }

            smokes.Clear();
            foreach (GameObject piece in built)
            {
                if (piece != null) Destroy(piece);
            }

            built.Clear();
            wallFrame.Clear();
            laserBeams = null;
            darkness = null;
            ring = null;
            if (timer != null) timer.Stop();
            if (npcs != null)
            {
                npcs.StopAvoiding(boss);
                npcs.Clear();
            }

            LeaveRoom();
        }

        public override void Skip()
        {
            skipping = true;
        }

        // One frame of the vault.
        private void Play()
        {
            smokes.RemoveAll(smoke => smoke == null);
            UseSkills();
            Blackout();
            Lasers();
            Steal();
            boss.Seen = CanSee();
            boss.Catchable = darkness == null || !darkness.Dark;
            ShowWallFrame();
        }

        private bool CanSee()
        {
            Vector3 at = boss.Position;
            foreach (VaultSmoke smoke in smokes)
            {
                if (smoke.Covers(at)) return false;
            }

            if (Time.time < revealedUntil || Flat(at, Diamond) <= alwaysSeenNear) return true;
            if (darkness != null && darkness.Dark) return darkness.Lit(at);
            if (!boss.Sneaking) return true;

            return players.TryGetNearest(at, out _, out float nearest) && nearest <= sneakSeenWithin;
        }

        // The smoke bomb, when a player comes close (not while it is disguised).
        private void UseSkills()
        {
            if (boss.Disguised || Time.time < nextSmokeAt) return;
            if (!players.TryGetNearest(boss.Position, out _, out float nearest) || nearest > smokeWhenPlayerWithin) return;

            nextSmokeAt = Time.time + Random.Range(smokeEvery.x, Mathf.Max(smokeEvery.x, smokeEvery.y));
            smokes.Add(VaultSmoke.Throw(transform, boss.Position, smokeRadius, smokeSeconds, SmokeOrder));
            boss.Sneak();
            Debug.Log("[BossVault] The boss throws a smoke bomb", this);
        }

        // Another look than was (null being itself): any passer-by or thief kind, or itself, at
        // random, all alike.
        private Component NextLook(Component was)
        {
            var looks = new List<Component> { null };
            if (npcs != null && npcs.Kinds != null)
            {
                foreach (NpcWalker kind in npcs.Kinds)
                {
                    if (kind != null) looks.Add(kind);
                }
            }

            if (thiefKinds != null)
            {
                foreach (ThiefController kind in thiefKinds)
                {
                    if (kind != null) looks.Add(kind);
                }
            }

            // Itself counts as a look too: as itself before, it now passes for someone.
            looks.Remove(was);
            return looks.Count > 0 ? looks[Random.Range(0, looks.Count)] : null;
        }

        private static bool FacesRight(Component kind)
        {
            if (kind is NpcWalker passerBy) return passerBy.ArtFacesRight;
            if (kind is ThiefController thief) return thief.ArtFacesRight;
            return false;
        }

        private void Blackout()
        {
            if (darkness == null) return;

            if (!darkness.Dark && Time.time >= nextBlackoutAt)
            {
                darkness.SetDark(true);
                laserBeams.SetShown(false);
                lightsBackAt = Time.time + blackoutSeconds;
                nextBlackoutAt = Time.time + blackoutEverySeconds;
                PutSwitchesDown();
                LightsChanged?.Invoke(false);
                Debug.Log("[BossVault] The boss cuts the lights", this);
                return;
            }

            if (darkness.Dark && Time.time >= lightsBackAt) LightsOn(null);
        }

        private void LightsOn(UWBPlayerController byPlayer)
        {
            if (darkness == null || !darkness.Dark) return;

            darkness.SetDark(false);
            darkness.SetLamps(Array.Empty<Vector3>());
            if (laserBeams != null) laserBeams.SetShown(true);
            lightsBackAt = float.MaxValue;
            foreach (FloorSwitch lights in switches)
            {
                if (lights == null) continue;
                lights.Triggered -= OnSwitched;
                lights.Vanish();
            }

            switches.Clear();
            LightsChanged?.Invoke(true);
            string who = byPlayer != null ? $"Player {byPlayer.PlayerId} puts" : "Back come";
            Debug.Log($"[BossVault] {who} the lights on", this);
        }

        private void PutSwitchesDown()
        {
            var spots = new List<Vector3>();
            switchesNeeded = ActivePlayers() >= bothSwitchesFromPlayers ? switchSpots.Length : Mathf.Min(1, switchSpots.Length);
            foreach (Vector2 spot in switchSpots)
            {
                var at = new Vector3(spot.x, 0f, spot.y);
                spots.Add(at);
                FloorSwitch lights = FloorSwitch.Create(transform, "Light Switch", at, lightSwitch, switchWidth, null, stealRing, radialFill, Color.white, switchRadius,
                    switchHoldSeconds, blackoutSeconds, playerSpawner, sparkles);
                lights.Triggered += OnSwitched;
                switches.Add(lights);
            }

            darkness.SetLamps(spots);
        }

        // A switch on rests (dimmed) for the rest of the blackout, so each counts once.
        private void OnSwitched(FloorSwitch lights, UWBPlayerController player)
        {
            switchesNeeded--;
            if (switchesNeeded <= 0)
            {
                LightsOn(player);
                return;
            }

            Debug.Log($"[BossVault] Player {player?.PlayerId} switches one on, {switchesNeeded} to go", this);
        }

        private int ActivePlayers()
        {
            if (playerSpawner == null) return 0;

            int count = 0;
            foreach (UWBPlayerController player in playerSpawner.SpawnedPlayers)
            {
                if (player != null && player.isActiveAndEnabled) count++;
            }

            return count;
        }

        // The boss touching a beam shakes it every time, and sets the alarm off unless it just rang.
        private void Lasers()
        {
            int beam = laserBeams.HitBy(boss.Position, laserReach, out float along);
            if (beam >= 0 && beam != touchingBeam)
            {
                laserBeams.Shake(beam, along);
                if (Time.time >= alarmReadyAt)
                {
                    alarmReadyAt = Time.time + alarmCooldownSeconds;
                    revealedUntil = Time.time + revealSeconds;
                    alarmAt = Time.time;
                    AlarmRang?.Invoke();
                    Debug.Log("[BossVault] The boss trips a laser: ALARM", this);
                }
            }

            touchingBeam = beam;
        }

        private void Steal()
        {
            bool onDiamond = Flat(boss.Position, Diamond) <= stealRadius;
            stolen = onDiamond ? stolen + Time.deltaTime / stealSeconds : Mathf.Max(0f, stolen - Time.deltaTime / drainSeconds);
            ShowRing();
        }

        // Everyone's money settles the outcome, and the chase follows at once.
        private void Finish(bool diamondStolen)
        {
            if (boss != null) boss.Freeze();
            if (darkness != null) darkness.SetDark(false);
            if (timer != null) timer.Stop();
            if (!skipping) PayEveryone(diamondStolen ? -stolenFine : securedReward);
            string outcome = diamondStolen ? "The diamond is stolen" : "The vault is secured";
            Debug.Log($"[BossVault] {outcome}", this);
            Ended?.Invoke(diamondStolen);
        }

        private void PayEveryone(int amount)
        {
            if (scoreTags == null || amount == 0) return;

            foreach (PlayerScoreTag tag in scoreTags)
            {
                if (tag == null || !tag.InPlay) continue;

                int change = amount > 0 ? amount : -Mathf.Min(-amount, tag.Score);
                if (change == 0) continue;
                tag.SetScore(tag.Score + change);
            }
        }

        // Caught: the catcher is paid, and the boss vanishes in a puff, then warpDelaySeconds later
        // comes back in another puff at another spot along the edge, disguised.
        private void OnBumped(UWBPlayerController player)
        {
            BossBumped?.Invoke(player);
            Pay(player, bumpReward);

            smokes.Add(VaultSmoke.Throw(transform, boss.Position, warpPuffRadius, warpPuffSeconds, SmokeOrder));
            Component was = boss.Look;
            boss.Vanish();
            StartCoroutine(ComeBack(was));
        }

        private IEnumerator ComeBack(Component was)
        {
            yield return new WaitForSeconds(warpDelaySeconds);
            if (boss == null) yield break;

            Vector3 to = EdgeSpot();
            smokes.Add(VaultSmoke.Throw(transform, to, warpPuffRadius, warpPuffSeconds, SmokeOrder));
            boss.WarpTo(to);
            Component kind = NextLook(was);
            if (kind != null) boss.Disguise(kind, FacesRight(kind), disguiseSeconds);
            else boss.BeItself();
            string look = kind != null ? $"passing for a {kind.name}" : "as itself";
            Debug.Log($"[BossVault] The boss warps to ({to.x:0.0}, {to.z:0.0}), {look}", this);
        }

        private void Pay(UWBPlayerController player, int amount)
        {
            PlayerScoreTag tag = TagOf(player);
            if (tag == null || amount <= 0) return;

            tag.SetScore(tag.Score + amount);
            FloorDamage.Play(player.transform.position, $"+{amount}$", gainColor, damageFont, damageMaterial, transform);
        }

        // A random spot along the floor's edge (edgeInset in), clear of the players by
        // edgeClearOfPlayers and of the passers-by; of the spots tried, the one furthest from the
        // players when none is.
        private Vector3 EdgeSpot()
        {
            Vector3 best = Diamond;
            float bestRoom = float.MinValue;
            passerPositions.Clear();
            if (npcs != null) npcs.AddPositions(passerPositions, null);
            for (int i = 0; i < 40; i++)
            {
                Vector3 spot = RandomEdgePoint();
                if (NearPasser(spot)) continue;

                float room = players.TryGetNearest(spot, out _, out float nearest) ? nearest : float.MaxValue;
                if (room >= edgeClearOfPlayers) return spot;
                if (room <= bestRoom) continue;
                bestRoom = room;
                best = spot;
            }

            return best;
        }

        private bool NearPasser(Vector3 spot)
        {
            float gap = chase.Settings.PasserClearance;
            foreach (Vector3 passer in passerPositions)
            {
                if (Flat(spot, passer) < gap) return true;
            }

            return false;
        }

        // A point along the edge of the floor (edgeInset in), every part of the edge alike.
        private Vector3 RandomEdgePoint()
        {
            Vector2 min = areaMin + Vector2.one * edgeInset;
            Vector2 max = areaMax - Vector2.one * edgeInset;
            float width = max.x - min.x;
            float depth = max.y - min.y;
            float along = Random.value * 2f * (width + depth);
            if (along < width) return new Vector3(min.x + along, 0f, min.y);
            along -= width;
            if (along < depth) return new Vector3(max.x, 0f, min.y + along);
            along -= depth;
            if (along < width) return new Vector3(max.x - along, 0f, max.y);
            along -= width;
            return new Vector3(min.x, 0f, max.y - along);
        }

        private PlayerScoreTag TagOf(UWBPlayerController player)
        {
            if (player == null || scoreTags == null) return null;

            foreach (PlayerScoreTag tag in scoreTags)
            {
                if (tag != null && tag.InPlay && tag.PlayerNumber == player.PlayerId) return tag;
            }

            return null;
        }

        // The boss comes in at a random spot along the edge, clear of the players.
        private void PutBossDown()
        {
            chase.GetMoves(out BossMove walk, out _, out _, out _);
            Vector3 start = EdgeSpot();
            boss = VaultBoss.Create(transform, start, Diamond, chase.Settings, walk, players, npcs, areaMin, areaMax, bossSpeeds);
            boss.Bumped += OnBumped;
            if (npcs != null) npcs.AlsoAvoid(boss);
        }

        private void BuildDiamond()
        {
            Vector3 at = Diamond;
            ring = Flat("Steal Ring", stealRing, RingOrder, at, stealRadius * 2f + 0.5f);
            if (ring != null)
            {
                if (radialFill != null) ring.sharedMaterial = radialFill;
                ring.color = stealColour;
                ringBlock = new MaterialPropertyBlock();
                ShowRing();
            }

            Flat("Diamond", diamondPedestal, DiamondOrder, at, diamondWidth);
        }

        private void ShowRing()
        {
            if (ring == null || ringBlock == null) return;

            ring.enabled = stolen > 0f;
            ring.GetPropertyBlock(ringBlock);
            ringBlock.SetFloat(FillId, Mathf.Clamp01(stolen));
            ring.SetPropertyBlock(ringBlock);
        }

        // A picture lying flat on the floor at, width metres across; null without a sprite.
        private SpriteRenderer Flat(string name, Sprite sprite, int order, Vector3 at, float width)
        {
            if (sprite == null) return null;

            var piece = new GameObject(name, typeof(SpriteRenderer));
            piece.transform.SetParent(transform, false);
            piece.transform.SetPositionAndRotation(new Vector3(at.x, 0.04f, at.z), Quaternion.Euler(90f, 0f, 0f));
            piece.transform.localScale = Vector3.one * (width / Mathf.Max(0.0001f, sprite.bounds.size.x));
            var picture = piece.GetComponent<SpriteRenderer>();
            picture.sprite = sprite;
            picture.sortingOrder = order;
            built.Add(piece);
            return picture;
        }

        // The vault's floor and wall pictures (black where one is missing), the stage's wall pieces
        // and the hiding spots away; the wall gets its red alarm glow (hidden until the alarm rings).
        private void ShowRoom()
        {
            staging = chase.NewStaging();
            staging.Show();
            floorSwap = new PictureSwap(floorImage, floorPicture != null ? floorPicture : Texture2D.blackTexture);
            floorSwap.Show();
            if (wallBackground != null)
            {
                wallSheet = WallPiece("Vault Wall", wallPicture != null ? Color.white : Color.black, Vector2.zero, Vector2.one, Vector2.zero);
                wallSheet.sprite = wallPicture;
                // One sheet over the whole wall, its glow sliced so it keeps one width along every edge.
                Image glow = WallPiece("Alarm Glow", Color.clear, Vector2.zero, Vector2.one, Vector2.zero);
                glow.sprite = AlarmGlowSprite();
                glow.type = Image.Type.Sliced;
                glow.fillCenter = true;
                glow.pixelsPerUnitMultiplier = (AlarmGlowPixels / 2f) / AlarmGlowWidth;
                wallFrame.Add(glow);
            }

            hiddenSpots.Clear();
            if (hidingSpots == null) return;

            foreach (HidingBush bush in hidingSpots.Bushes)
            {
                if (bush == null || !bush.gameObject.activeSelf) continue;
                bush.gameObject.SetActive(false);
                hiddenSpots.Add(bush.gameObject);
            }
        }

        private void LeaveRoom()
        {
            floorSwap?.PutBack();
            floorSwap = null;
            if (wallSheet != null) Destroy(wallSheet.gameObject);
            wallSheet = null;
            staging?.PutBack();
            staging = null;
            foreach (GameObject spot in hiddenSpots)
            {
                if (spot != null) spot.SetActive(true);
            }

            hiddenSpots.Clear();
        }

        // The wall's edge glows red once after the alarm rings: it swells quickly, then fades gently,
        // with no flashing.
        private void ShowWallFrame()
        {
            float t = (Time.time - alarmAt) / AlarmGlowSeconds;
            float swell = t < 0.2f ? Mathf.SmoothStep(0f, 1f, t / 0.2f) : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.2f) / 0.8f);
            Color colour = alarmColour;
            colour.a = t >= 0f && t < 1f ? alarmColour.a * AlarmGlowPeak * swell : 0f;
            foreach (Image edge in wallFrame) edge.color = colour;
        }

        // Drawn once and shared: white, strongest at the border and fading to nothing inwards, with
        // a clear middle that the sliced image stretches over the wall.
        private static Sprite alarmGlowSprite;

        private static Sprite AlarmGlowSprite()
        {
            if (alarmGlowSprite != null) return alarmGlowSprite;

            const int size = AlarmGlowPixels;
            const float reach = size / 2f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Alarm Glow", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fromEdge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y)) + 0.5f;
                    float strength = 1f - Mathf.Clamp01(fromEdge / reach);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(strength * strength * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            float border = reach - 1f;
            alarmGlowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            return alarmGlowSprite;
        }

        // A plain sheet on the wall's background, anchored between min and max, size sizeDelta: over
        // the stage's wall pictures but under the clock when the clock is on the same background.
        private Image WallPiece(string name, Color colour, Vector2 min, Vector2 max, Vector2 size)
        {
            var piece = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)piece.transform;
            rect.SetParent(wallBackground, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(min.x == max.x ? min.x : 0.5f, min.y == max.y ? min.y : 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            rect.SetAsLastSibling();
            Transform clock = ClockOnWall();
            if (clock != null) rect.SetSiblingIndex(clock.GetSiblingIndex());
            var image = piece.GetComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            built.Add(piece);
            return image;
        }

        // The child of the wall's background that holds the clock, or null when it is elsewhere.
        private Transform ClockOnWall()
        {
            if (timer == null) return null;

            Transform holder = timer.transform;
            while (holder != null && holder.parent != wallBackground) holder = holder.parent;
            return holder;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }
    }
}
