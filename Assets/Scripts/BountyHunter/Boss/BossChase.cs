using System.Collections;
using System.Collections.Generic;
using Fortal.Players;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The Most Wanted boss chase, played after the last map and before the result (MapSwitcher's
    // finale). First its entrance, entranceSeconds with the wall reading BOSS CHASE!: pigeon feathers
    // blow across the floor and wall (BossFeatherShower). As they go, on the floor a cloud of smoke
    // puffs up and the boss shows in it and steps out (the smoke bomb's arrival), then up to
    // chaseSeconds of chasing. A touch makes the boss warp
    // far away (BossController); every one that counts pays catchReward at once (not the very first,
    // nor one while it is disguised). touchesToCatch of them catch it: the clock stops, it is
    // caught in a scuffle and the game waits outroSeconds before the result. When the time runs out
    // first, it runs off the floor and the result comes once it is gone. The wall shows the heading
    // with the catches so far (headingFormat). Its water (warning circles all over the floor
    // after it shoots) costs shotFine (or what the player has). The passers-by stroll about as in a
    // stage, so it has someone to pass for, and it can duck into the map's hiding spots. It plays on
    // the boss's own rooftop: its floor picture and its wall (the Boss Wall prefab, with a police
    // helicopter flying past behind the buildings), the stage's own wall pieces put away meanwhile.
    // In Simulation mode, for trying the moves out while it runs about: 1 shoots, 2 throws the smoke
    // bomb, 3 puts on a disguise, 4 ducks into the nearest free hiding spot.
    // Console lines start with [BossChase].
    public sealed class BossChase : StageInterlude
    {
        [SerializeField] private UWBPlayerSpawner playerSpawner;
        [SerializeField] private PlayerScoreTag[] scoreTags;
        [SerializeField] private NpcCrowd npcs;
        // The map's hiding spots, which the boss can duck into.
        [SerializeField] private ThiefSpawner hidingSpots;
        // Its footfalls while it runs and the dust it kicks up, as for the thieves; either may be left empty.
        [SerializeField] private GameSfx sounds;
        [SerializeField] private Sprite[] dustSprites;
        // The feathers blowing across the floor and wall in the entrance; leave empty for none.
        [FormerlySerializedAs("rosePetal")]
        [SerializeField] private Sprite feather;

        [Header("Rooftop")]
        // The boss's own map for the chase: its floor picture (put back afterwards), its wall (the Boss
        // Wall prefab in the wall's background, off until the chase; its Sky is where the helicopter
        // flies, under the buildings in front) and the stage's wall pieces put away meanwhile (the
        // board, the posters, the dogs' pictures; the clock and the score tags stay). Any may be empty.
        [SerializeField] private RawImage floorImage;
        [SerializeField] private Texture bossFloor;
        [SerializeField] private GameObject wallPanel;
        [SerializeField] private RectTransform wallSky;
        [SerializeField] private GameObject[] hideDuringChase;
        // Parts switched off rather than hidden, where the object itself must stay (a dog's own
        // picture, whose score tag is a child of it).
        [SerializeField] private Behaviour[] switchOffDuringChase;
        [SerializeField] private Sprite helicopter;
        [SerializeField] private Sprite helicopterRotor;
        // The feathers blow in over this much past the boss's floor (the edges of the floor seen).
        [FormerlySerializedAs("petalFloorMargin")]
        [SerializeField, Min(0f)] private float featherFloorMargin = 0.35f;
        // Shows the heading and the time left on the wall.
        [SerializeField] private StageTimer timer;
        // The scuffle played where the boss is caught at the end.
        [SerializeField] private CatchBrawl.Art catchBrawl;

        [Header("Boss")]
        [SerializeField] private BossSheet walk = new BossSheet(10, 60, new Vector2(0.523f, 0.012f));
        [SerializeField] private BossSheet shoot = new BossSheet(7, 90, new Vector2(0.689f, 0.024f));
        [SerializeField] private BossSheet smokeBomb = new BossSheet(10, 90, new Vector2(0.532f, 0.110f));
        [SerializeField] private BossSheet capeWrap = new BossSheet(7, 90, new Vector2(0.488f, 0.050f));
        // The cape thrown off again, cut twice as finely (30 a second) so it stays smooth played slowly.
        [SerializeField] private BossSheet capeReveal = new BossSheet(6, 30, new Vector2(0.486f, 0.061f));
        [SerializeField] private BossSettings boss = new BossSettings();
        // The floor the boss runs on (x and world z), like the thieves'.
        [SerializeField] private Vector2 areaMin = new Vector2(-5.15f, -2.3f);
        [SerializeField] private Vector2 areaMax = new Vector2(5.15f, 2.3f);

        [Header("Timing (seconds)")]
        [SerializeField, Min(0.5f)] private float entranceSeconds = 4.5f;
        [SerializeField, Min(1f)] private float chaseSeconds = 85.5f;
        [SerializeField, Min(0f)] private float outroSeconds = 5f;
        // How many touches (after the first, which it warps away from) catch it.
        [SerializeField, Min(1)] private int touchesToCatch = 3;
        // {0} catches so far, {1} touchesToCatch.
        [SerializeField] private string headingFormat = "BOSS CHASE! {0}/{1}";
        // Most it waits for the boss to run off the floor before the result comes anyway.
        [SerializeField, Min(0.5f)] private float mostRunOffSeconds = 5f;

        [Header("Money")]
        [SerializeField, Min(0)] private int catchReward = 30;
        [SerializeField, Min(0)] private int shotFine = 5;
        [SerializeField] private Color gainColor = new Color(1f, 0.82f, 0.25f, 1f);
        [SerializeField] private Color damageColor = new Color(1f, 0.22f, 0.16f, 1f);
        [SerializeField] private TMP_FontAsset damageFont;
        [SerializeField] private Material damageMaterial;

        private BossMove walkMove;
        private BossMove shootMove;
        private BossMove smokeMove;
        private BossMove capeMove;
        private BossMove revealMove;
        private BossController chased;
        private SpawnerPlayerLocator players;
        private bool skipping;
        private bool ready;
        private BossFeatherShower wallFeathers;
        private PictureSwap floorSwap;
        private readonly List<GameObject> putAway = new List<GameObject>();
        private readonly List<Behaviour> switchedOff = new List<Behaviour>();
        private BossHelicopter hovering;
        private int touches;
        private ScenePreload.Ticket preparation;

        private void Awake()
        {
            if (wallPanel != null) wallPanel.SetActive(false);
            players = new SpawnerPlayerLocator(playerSpawner);
            preparation = ScenePreload.Enqueue(this, 100, PrepareMoves());
        }

        private IEnumerator PrepareMoves()
        {
            foreach (BossSheet sheet in new[] { walk, smokeBomb, shoot, capeWrap, capeReveal })
            {
                IEnumerator steps = sheet.Preload(boss.PixelsPerUnit);
                while (steps.MoveNext()) yield return null;
            }
        }

        public override IEnumerator Prepare()
        {
            if (preparation == null) preparation = ScenePreload.Enqueue(this, 100, PrepareMoves());
            preparation.Prioritize();
            while (!preparation.Completed) yield return null;
        }

        private void OnDestroy()
        {
            foreach (BossSheet sheet in new[] { walk, smokeBomb, shoot, capeWrap, capeReveal }) sheet?.Release();
        }

        public override void Begin()
        {
            skipping = false;
            touches = 0;
            walkMove ??= new BossMove(walk, boss.PixelsPerUnit);
            shootMove ??= new BossMove(shoot, boss.PixelsPerUnit);
            smokeMove ??= new BossMove(smokeBomb, boss.PixelsPerUnit);
            capeMove ??= new BossMove(capeWrap, boss.PixelsPerUnit);
            revealMove ??= new BossMove(capeReveal, boss.PixelsPerUnit);
            ready = !walkMove.IsEmpty;
            if (!ready)
            {
                Debug.LogError("[BossChase] The boss has no walk sheet, so the chase is skipped: set Walk's texture (Assets/ART/Boss/Sheets/BossWalk.png)", this);
                return;
            }

            chased = BossController.Create(Middle(), transform, boss, walkMove, shootMove, smokeMove, capeMove, revealMove, players, npcs, hidingSpots, areaMin, areaMax);
            if (dustSprites != null && dustSprites.Length > 0) chased.gameObject.AddComponent<DustTrail>().Initialize(dustSprites, chased.Picture);
            if (sounds != null) chased.gameObject.AddComponent<Footsteps>().Initialize(sounds, chased.Picture);
            chased.Touched += OnTouched;
            chased.Shot += OnShot;
            if (timer != null) timer.StartCountdown(Heading(), entranceSeconds + chaseSeconds);
            if (npcs != null) npcs.Begin();
            ShowRooftop();
            Debug.Log("[BossChase] The boss is on the floor", this);
        }

        public override IEnumerator Run()
        {
            if (!ready) yield break;

            float start = Time.time;
            Vector3 middle = Middle();
            Canvas.ForceUpdateCanvases();
            RectTransform wall = wallPanel != null ? wallPanel.transform as RectTransform : null;
            if (feather != null && floorImage != null && floorImage.canvas != null)
                BossFeatherShower.Begin(feather, FeatherFloor(), entranceSeconds, transform, floorImage.canvas.worldCamera);
            if (feather != null && wall != null) wallFeathers = BossFeatherShower.BeginOnWall(feather, wall, entranceSeconds);
            yield return Until(start + entranceSeconds);
            chased.ArriveAt(middle);
            Debug.Log("[BossChase] The feathers settle; the boss appears in a cloud of smoke", this);
            float timeUp = start + entranceSeconds + chaseSeconds;
            while (Time.time < timeUp && !skipping && !Caught) yield return null;

            ClearWater();
            if (Caught)
            {
                yield return PlayCaught();
                yield break;
            }

            chased.RunOff();
            float giveUp = Time.time + mostRunOffSeconds;
            while (!chased.HasLeft && Time.time < giveUp && !skipping) yield return null;
            Debug.Log("[BossChase] The boss got away", this);
        }

        private bool Caught => touches >= touchesToCatch;

        // The clock stops, the boss is caught in a scuffle, then a pause before the result.
        private IEnumerator PlayCaught()
        {
            if (timer != null) timer.Stop();
            chased.Finish();
            CatchBrawl.Play(chased.transform.position, catchBrawl, transform);
            Debug.Log("[BossChase] The boss is caught", this);
            float caughtAt = Time.time;
            yield return Until(caughtAt + CatchBrawl.StampSeconds);
            chased.gameObject.SetActive(false);
            yield return Until(caughtAt + outroSeconds);
        }

        private string Heading()
        {
            return string.Format(headingFormat, Mathf.Min(touches, touchesToCatch), touchesToCatch);
        }

        public override void End()
        {
            StopAllCoroutines();
            ClearWater();
            if (wallFeathers != null) Destroy(wallFeathers.gameObject);
            wallFeathers = null;
            foreach (BossFeatherShower shower in GetComponentsInChildren<BossFeatherShower>()) Destroy(shower.gameObject);
            if (chased != null)
            {
                chased.Touched -= OnTouched;
                chased.Shot -= OnShot;
                Destroy(chased.gameObject);
                chased = null;
            }

            if (timer != null) timer.Stop();
            if (npcs != null) npcs.Clear();
            LeaveRooftop();
        }

        private void Update()
        {
            if (chased == null || Keyboard.current == null || playerSpawner == null || !playerSpawner.IsStandaloneSimulationMode) return;

            if (Keyboard.current.digit1Key.wasPressedThisFrame) TryUse(BossSkill.Shoot);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) TryUse(BossSkill.SmokeBomb);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) TryUse(BossSkill.Disguise);
            if (Keyboard.current.digit4Key.wasPressedThisFrame) TryUse(BossSkill.Hide);
        }

        private void TryUse(BossSkill skill)
        {
            if (chased.TryUse(skill)) Debug.Log($"[BossChase] Key: {skill}", this);
            else Debug.Log($"[BossChase] Key: {skill} cannot be done now (the boss is busy, or there is no passer-by kind to pass for)", this);
        }

        public override void Skip()
        {
            skipping = true;
        }

        // Water still coming down from the boss's last shot goes, so it hits nobody after the chase.
        private void ClearWater()
        {
            foreach (BossWaterVolley volley in GetComponentsInChildren<BossWaterVolley>())
            {
                Destroy(volley.gameObject);
            }
        }

        // A skip ends every wait at once.
        private IEnumerator Until(float time)
        {
            while (Time.time < time && !skipping) yield return null;
        }

        // The boss's floor and wall instead of the last map's (the stage's wall pieces put away), with
        // the helicopter flying past.
        private void ShowRooftop()
        {
            floorSwap = new PictureSwap(floorImage, bossFloor);
            floorSwap.Show();
            putAway.Clear();
            if (hideDuringChase != null)
            {
                foreach (GameObject piece in hideDuringChase)
                {
                    if (piece == null || !piece.activeSelf) continue;
                    piece.SetActive(false);
                    putAway.Add(piece);
                }
            }

            switchedOff.Clear();
            if (switchOffDuringChase != null)
            {
                foreach (Behaviour part in switchOffDuringChase)
                {
                    if (part == null || !part.enabled) continue;
                    part.enabled = false;
                    switchedOff.Add(part);
                }
            }

            if (wallPanel != null) wallPanel.SetActive(true);
            if (helicopter != null && wallSky != null) hovering = BossHelicopter.Fly(wallSky, helicopter, helicopterRotor);
        }

        private void LeaveRooftop()
        {
            floorSwap?.PutBack();
            if (hovering != null) Destroy(hovering.gameObject);
            hovering = null;
            if (wallPanel != null) wallPanel.SetActive(false);
            foreach (GameObject piece in putAway)
            {
                if (piece != null) piece.SetActive(true);
            }

            putAway.Clear();
            foreach (Behaviour part in switchedOff)
            {
                if (part != null) part.enabled = true;
            }

            switchedOff.Clear();
        }

        private Rect FeatherFloor()
        {
            return Rect.MinMaxRect(areaMin.x - featherFloorMargin, areaMin.y - featherFloorMargin, areaMax.x + featherFloorMargin, areaMax.y + featherFloorMargin);
        }

        private Vector3 Middle()
        {
            return new Vector3((areaMin.x + areaMax.x) * 0.5f, 0f, (areaMin.y + areaMax.y) * 0.5f);
        }

        private void OnTouched(UWBPlayerController player)
        {
            touches++;
            if (timer != null) timer.SetHeading(Heading());
            Debug.Log($"[BossChase] Catch {touches}/{touchesToCatch}", this);

            PlayerScoreTag tag = TagOf(player);
            if (tag == null || catchReward <= 0) return;

            tag.SetScore(tag.Score + catchReward);
            FloorDamage.Play(player.transform.position, $"+{catchReward}$", gainColor, damageFont, damageMaterial, transform);
            Debug.Log($"[BossChase] Player {player.PlayerId} touches the boss: +{catchReward}$ (total {tag.Score}$)", this);
        }

        private void OnShot(UWBPlayerController player)
        {
            PlayerScoreTag tag = TagOf(player);
            if (tag == null) return;

            int lost = Mathf.Min(shotFine, tag.Score);
            if (lost <= 0) return;

            tag.SetScore(tag.Score - lost);
            FloorDamage.Play(player.transform.position, $"-{lost}$", damageColor, damageFont, damageMaterial, transform);
            Debug.Log($"[BossChase] Player {player.PlayerId} is hit by the boss's water: -{lost}$ (total {tag.Score}$)", this);
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
    }
}
