using System;
using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // The Most Wanted boss on the floor: it decides what the boss does and keeps the actions going,
    // one at a time (each in its own class, moving the boss through BossBody). It runs about (BossRun)
    // and reacts to the players, the first that fits:
    // - BossShooting when players huddle together (at most once every shootEverySeconds);
    // - BossDisguising when a player closes in (disguiseWhenPlayerAt; once every disguiseEverySeconds,
    //   and only once in a row: after a disguise it must shoot or throw the smoke bomb before the next);
    // - BossHiding while chased, with a free hiding spot near that it reaches first (once every
    //   hideEverySeconds);
    // and now and then (moveEverySeconds) throws the smoke bomb anyway (BossSmokeBomb).
    // It cannot be touched while it stands doing something (shooting, a smoke bomb, throwing its cape
    // off) or hides, and like a thief coming out of a hiding spot it then runs off with a head start
    // (exitGraceSeconds) before it can be caught, so nobody gains by waiting beside it.
    // A player who touches it makes it warp away at once (BossSmokeBomb.Warp); Touched says who,
    // except for the very first touch of the chase and a touch while it is disguised, which count
    // for nothing. Its pictures lie flat on the floor like the thieves'.
    public sealed class BossController : MonoBehaviour
    {
        private readonly List<UWBPlayerController> nearPlayers = new List<UWBPlayerController>();

        private BossBody body;
        private BossRun run;
        private BossShooting shooting;
        private BossSmokeBomb smokeBomb;
        private BossDisguising disguising;
        private BossHiding hiding;
        private BossRunOff runOff;
        // What it is doing now; null while it stands still (before its entrance, after the chase).
        private IBossAction doing;
        private float nextSmokeAt;
        private float shootReadyAt;
        private float disguiseReadyAt;
        private float hideReadyAt;
        private bool firstTouchDodged;
        // It runs about until this before it may do anything else (actionGapSeconds after a move).
        private float actionReadyAt;
        // It runs off with a head start after any action: nobody can catch it before this.
        private float catchableAt;
        // It has disguised itself and done nothing else since: it must shoot or throw the smoke bomb
        // before it may disguise itself again (hiding does not count).
        private bool disguisedLast;

        // A player caught the boss (touched it, and it counts); it warps away.
        public event Action<UWBPlayerController> Touched;
        // A player was hit by its water.
        public event Action<UWBPlayerController> Shot;

        // Gone off the floor after RunOff.
        public bool HasLeft { get; private set; }

        // Its picture (off while it is out of sight: hidden, in a disguise, between clouds).
        public SpriteRenderer Picture => body.Animator.Body;

        // Makes the boss standing at position, still and out of sight until ArriveAt. hidingSpots may
        // be null (it then never hides).
        public static BossController Create(Vector3 position, Transform parent, BossSettings settings, BossMove walk, BossMove shoot, BossMove smoke, BossMove cape,
            BossMove reveal, SpawnerPlayerLocator players, NpcCrowd passersBy, IHidingSpots hidingSpots, Vector2 areaMin, Vector2 areaMax)
        {
            var root = new GameObject("Boss");
            root.transform.SetParent(parent, false);

            var bodyObject = new GameObject("Body");
            bodyObject.transform.SetParent(root.transform, false);
            bodyObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            bodyObject.AddComponent<SpriteRenderer>().sortingOrder = settings.SortingOrder;

            var boss = root.AddComponent<BossController>();
            var animator = bodyObject.AddComponent<BossAnimator>();
            var body = new BossBody(root.transform, animator, BossDisguise.Create(root.transform), settings, walk, players, passersBy, areaMin, areaMax);
            boss.body = body;
            boss.run = new BossRun(body);
            boss.shooting = new BossShooting(body, shoot, player => boss.Shot?.Invoke(player));
            boss.smokeBomb = new BossSmokeBomb(body, smoke);
            boss.disguising = new BossDisguising(body, cape, reveal, passersBy);
            boss.hiding = new BossHiding(body, hidingSpots);
            boss.runOff = new BossRunOff(body);
            body.Position = position;
            boss.Stand();
            animator.Body.enabled = false;
            return boss;
        }

        // Appears at position the way it comes out of a smoke bomb (a cloud puffs up, it shows in it
        // and steps out, untouchable until it is out), then runs about: the start of the chase.
        public void ArriveAt(Vector3 position)
        {
            if (smokeBomb.IsEmpty)
            {
                body.Position = position;
                body.Animator.Body.enabled = true;
                StartRunning();
                return;
            }

            smokeBomb.ArriveAt(position);
            doing = smokeBomb;
        }

        // Runs off the floor (nobody caught it in time); HasLeft once it is out of sight.
        public void RunOff()
        {
            doing?.Stop();
            runOff.Begin();
            doing = runOff;
            Debug.Log("[BossController] Nobody caught it: it runs off the floor", this);
        }

        // Stops it where it is for good, out of any disguise (the chase is over).
        public void Finish()
        {
            doing?.Stop();
            body.DropDisguise();
            Stand();
        }

        // Does a move now, for trying them out (BossChase's number keys); only while it runs about.
        // False when it is busy or the move cannot be done.
        public bool TryUse(BossSkill skill)
        {
            if (doing != run) return false;
            switch (skill)
            {
                case BossSkill.Shoot:
                    if (!shooting.CanShoot) return false;
                    break;
                case BossSkill.SmokeBomb:
                    if (smokeBomb.IsEmpty) return false;
                    break;
                case BossSkill.Disguise:
                    if (!disguising.CanDisguise) return false;
                    break;
                case BossSkill.Hide:
                    if (!hiding.TryFind(float.MaxValue)) return false;
                    break;
            }

            Use(skill);
            return true;
        }

        private void Update()
        {
            if (doing == null) return;

            if (!doing.Tick())
            {
                if (doing == runOff)
                {
                    doing = null;
                    HasLeft = true;
                    return;
                }

                StartRunning();
                catchableAt = Time.time + body.Settings.ExitGraceSeconds;
                actionReadyAt = Time.time + Mathf.Max(body.Settings.ActionGapSeconds, body.Settings.ExitGraceSeconds);
            }

            if (doing == run && Time.time >= actionReadyAt && !React() && Time.time >= nextSmokeAt && !smokeBomb.IsEmpty) Use(BossSkill.SmokeBomb);
            CheckTouches();
        }

        private void OnDestroy()
        {
            doing?.Stop();
        }

        // Standing still on its first walk picture, doing nothing.
        private void Stand()
        {
            doing = null;
            body.Animator.Body.enabled = true;
            body.Animator.Play(body.Walk.Frames, body.Walk.FramesPerSecond, Vector2Int.zero, false);
        }

        private void StartRunning()
        {
            run.Begin();
            doing = run;
            Vector2 every = body.Settings.MoveEverySeconds;
            nextSmokeAt = Time.time + Random.Range(every.x, Mathf.Max(every.x, every.y));
        }

        // Answers what the players are doing, the first that fits; false when none does.
        private bool React()
        {
            BossSettings settings = body.Settings;
            float now = Time.time;
            if (now >= shootReadyAt && shooting.CanShoot && shooting.Huddled())
            {
                Use(BossSkill.Shoot);
                return true;
            }

            bool nearby = body.Players.TryGetNearest(body.Position, out _, out float nearest);
            Vector2 closingIn = settings.DisguiseWhenPlayerAt;
            if (!disguisedLast && now >= disguiseReadyAt && nearby && nearest >= closingIn.x && nearest <= closingIn.y && disguising.CanDisguise)
            {
                Use(BossSkill.Disguise);
                return true;
            }

            if (now >= hideReadyAt && nearby && nearest < settings.FleeRadius && hiding.TryFind(settings.HideReach))
            {
                Use(BossSkill.Hide);
                return true;
            }

            return false;
        }

        private void Use(BossSkill skill)
        {
            BossSettings settings = body.Settings;
            Debug.Log($"[BossController] {skill}", this);
            if (skill == BossSkill.Disguise) disguisedLast = true;
            else if (skill != BossSkill.Hide) disguisedLast = false;
            switch (skill)
            {
                case BossSkill.Shoot:
                    shootReadyAt = Time.time + settings.ShootEverySeconds;
                    shooting.Begin();
                    doing = shooting;
                    break;
                case BossSkill.SmokeBomb:
                    smokeBomb.Throw();
                    doing = smokeBomb;
                    break;
                case BossSkill.Disguise:
                    disguiseReadyAt = Time.time + settings.DisguiseEverySeconds;
                    disguising.Begin();
                    doing = disguising;
                    break;
                case BossSkill.Hide:
                    hideReadyAt = Time.time + settings.HideEverySeconds;
                    hiding.Begin();
                    doing = hiding;
                    break;
            }
        }

        // A player within touchRadius: it warps away; the touch counts (Touched) unless it was the
        // first of the chase or it was in disguise.
        private void CheckTouches()
        {
            if (doing == null || !doing.Touchable || Time.time < catchableAt) return;

            body.Players.GetPlayersWithin(body.Position, body.Settings.TouchRadius, nearPlayers);
            if (nearPlayers.Count == 0) return;

            UWBPlayerController player = nearPlayers[0];
            if (!doing.CountsTouches)
            {
                Debug.Log($"[BossController] Player {player.PlayerId} touched it in disguise: it warps away, that does not count", this);
            }
            else if (!firstTouchDodged)
            {
                firstTouchDodged = true;
                Debug.Log($"[BossController] Player {player.PlayerId} touched it first: it warps away, nobody is paid", this);
            }
            else
            {
                Touched?.Invoke(player);
            }

            WarpAway();
        }

        private void WarpAway()
        {
            doing?.Stop();
            if (smokeBomb.IsEmpty)
            {
                body.DropDisguise();
                StartRunning();
                return;
            }

            smokeBomb.Warp();
            doing = smokeBomb;
        }
    }
}
