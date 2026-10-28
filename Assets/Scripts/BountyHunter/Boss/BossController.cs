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
        private BossSkill? skillAfterWarp;
        private float nextSmokeAt;
        private float shootReadyAt;
        private float disguiseReadyAt;
        private float hideReadyAt;
        private bool firstTouchDodged;
        // It runs about until this before it may do anything else (actionGapSeconds after a move).
        private float actionReadyAt;
        // It runs off with a head start after any action: nobody can catch it before this.
        private float catchableAt;
        private bool hideEscapePending;
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
            boss.disguising = new BossDisguising(body, cape, reveal, passersBy, boss.run);
            boss.hiding = new BossHiding(body, hidingSpots);
            boss.smokeBomb = new BossSmokeBomb(body, smoke, boss.hiding.TryWarpIn);
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
            skillAfterWarp = null;
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
            skillAfterWarp = null;
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

            return Use(skill);
        }

        private void Update()
        {
            if (doing == null) return;
            hiding.ObservePlayers();
            // Credit a player who already reached it before this frame's escape movement.
            if (CheckTouches()) return;

            if (!doing.Tick())
            {
                if (doing == smokeBomb && smokeBomb.Hid)
                {
                    hideEscapePending = false;
                    doing = hiding;
                    hideReadyAt = Time.time + body.Settings.HideEverySeconds;
                    return;
                }
                if (doing == smokeBomb && skillAfterWarp.HasValue)
                {
                    BossSkill skill = skillAfterWarp.Value;
                    skillAfterWarp = null;
                    if (!PlayerNearSkillPosition())
                    {
                        Use(skill);
                        return;
                    }
                }

                if (doing == runOff)
                {
                    doing = null;
                    HasLeft = true;
                    return;
                }

                float grace = body.Settings.ExitGraceSeconds;
                hideEscapePending = doing == hiding && hiding.HasExited;
                StartRunning();
                run.Tick();
                catchableAt = Time.time + grace;
                actionReadyAt = Mathf.Max(Time.time + body.Settings.ActionGapSeconds, catchableAt + body.Settings.CatchWindowSeconds);
            }

            if (hideEscapePending && doing == run && Time.time >= catchableAt)
            {
                hideEscapePending = false;
            }

            if (CheckTouches()) return;
            if (doing == disguising && disguising.IsBlocked)
            {
                WarpAway();
                return;
            }
            if (doing != run) return;

            // Keep acting when trapped; the throw itself provides the catch opportunity.
            if (!hideEscapePending && run.IsBlocked)
            {
                if (!smokeBomb.IsEmpty)
                {
                    run.Stop();
                    smokeBomb.Throw(!hideEscapePending);
                    doing = smokeBomb;
                }
                return;
            }

            if (!hideEscapePending && Time.time >= actionReadyAt && !React() && !smokeBomb.IsEmpty && Time.time >= nextSmokeAt)
                Use(BossSkill.SmokeBomb);
        }

        private void OnDestroy()
        {
            doing?.Stop();
        }

        // Standing still on its first walk picture, doing nothing.
        private void Stand()
        {
            skillAfterWarp = null;
            doing = null;
            body.Animator.Body.enabled = true;
            body.Animator.Play(body.Walk.Frames, body.Walk.FramesPerSecond, Vector2Int.zero, false);
        }

        private void StartRunning()
        {
            run.Begin(hideEscapePending ? body.Settings.ExitGraceSeconds : 0f);
            doing = run;
            Vector2 every = body.Settings.MoveEverySeconds;
            nextSmokeAt = Time.time + Random.Range(every.x, Mathf.Max(every.x, every.y));
        }

        // Answers what the players are doing, the first that fits; false when none does.
        private bool React()
        {
            BossSettings settings = body.Settings;
            float now = Time.time;
            bool nearby = body.Players.TryGetNearest(body.Position, out _, out float nearest);
            if (now >= hideReadyAt && nearby && nearest < settings.FleeRadius && hiding.TryFind(settings.HideReach, true))
                return Use(BossSkill.Hide);

            if (now >= shootReadyAt && shooting.CanShoot && shooting.Huddled())
            {
                return Use(BossSkill.Shoot);
            }

            Vector2 closingIn = settings.DisguiseWhenPlayerAt;
            if (!disguisedLast && now >= disguiseReadyAt && nearby && nearest >= closingIn.x && nearest <= closingIn.y && disguising.CanDisguise)
            {
                return Use(BossSkill.Disguise);
            }

            if (now >= hideReadyAt && nearby && nearest < settings.FleeRadius && hiding.TryFind(settings.HideReach))
            {
                Use(BossSkill.Hide);
                return true;
            }

            return false;
        }

        private bool PlayerNearSkillPosition()
        {
            return body.Players.TryGetWithin(body.Position, body.Settings.SkillClearance, out _);
        }

        private bool Use(BossSkill skill)
        {
            BossSettings settings = body.Settings;
            if ((skill == BossSkill.Shoot || skill == BossSkill.Disguise) && PlayerNearSkillPosition())
            {
                if (smokeBomb.IsEmpty) return false;
                doing?.Stop();
                skillAfterWarp = skill;
                smokeBomb.Warp(settings.FleeRadius, false);
                doing = smokeBomb;
                return true;
            }

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

            return true;
        }

        // A player within touchRadius: it warps away; the touch counts (Touched) unless it was the
        // first of the chase or it was in disguise.
        private bool CheckTouches()
        {
            if (doing == null || hideEscapePending || !doing.Touchable || Time.time < catchableAt) return false;

            body.Players.GetPlayersWithin(body.Position, body.Settings.TouchRadius, nearPlayers);
            if (nearPlayers.Count == 0) return false;

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
                IBossAction touchedAction = doing;
                Touched?.Invoke(player);
                // The winning touch can finish the chase synchronously.
                if (doing != touchedAction) return true;
            }

            WarpAway();
            return true;
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

            smokeBomb.Warp(body.Settings.FleeRadius);
            doing = smokeBomb;
        }
    }
}
