using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The boss ducking into one of the map's hiding spots while it is chased, the way a thief does
    // (TryFind picks the spot: not the one it last left, and with a way there that passes no player
    // ahead within routeClearance): it runs for it and jumps in (giving up if a player gets there
    // first), and the spot rustles while it hides. With no player near it sneaks out after a time in
    // sneakOutSeconds; a player near keeps it in longer, but never past hideSeconds; a player who has
    // stood on the spot for flushSeconds drives it out at once. It bursts out away from the nearest
    // player. It cannot be touched while hidden (BossController gives it a head start after).
    public sealed class BossHiding : IBossAction
    {
        // A player counts as on the spot this far past its edge once on it, and the time stood drains
        // this fast when they step off.
        private const float StayOnMargin = 0.18f;
        private const float FlushDrainRate = 2f;
        // Arrived at the spot within this; it holds the spot this much longer than it means to hide,
        // so nothing else takes it on the way.
        private const float AtSpot = 0.15f;
        private const float HoldOnTheWay = 3f;
        private const float ArrivalMarginSeconds = 0.15f;
        // Its picture fades away this fast as it jumps in, instead of blinking out.
        private const float JumpInFadeSeconds = 0.12f;

        private readonly List<Vector3> playerPositions = new List<Vector3>();
        private readonly List<UWBPlayerController> trackedPlayers = new List<UWBPlayerController>();
        private readonly Dictionary<UWBPlayerController, (Vector3 position, float speed, float time)> motion =
            new Dictionary<UWBPlayerController, (Vector3 position, float speed, float time)>();
        private readonly BossBody body;
        private readonly IHidingSpots hidingSpots;
        // The spot it is heading for or hiding in, and how long a player has stood on it.
        private HidingBush hideout;
        private bool hidden;
        private float hiddenFor;
        private float stoodOn;
        private float sneakOutAt;
        // The spot it last came out of; it only goes back after hiding somewhere else.
        private HidingBush lastLeft;

        // hidingSpots may be null: it then never hides.
        public BossHiding(BossBody body, IHidingSpots hidingSpots)
        {
            this.body = body;
            this.hidingSpots = hidingSpots;
        }

        public bool Touchable => !hidden;
        public bool CountsTouches => true;
        public bool HasExited { get; private set; }

        // Track the rendered floor positions, so UWB and simulation use the same speed estimate.
        public void ObservePlayers()
        {
            body.Players.GetPlayersWithin(body.Position, float.MaxValue, trackedPlayers);
            foreach (UWBPlayerController player in trackedPlayers)
            {
                Vector3 position = player.transform.position;
                float speed = body.Settings.RunSpeed;
                if (motion.TryGetValue(player, out var previous))
                {
                    float elapsed = Time.time - previous.time;
                    if (elapsed <= 0f) continue;
                    if (elapsed < 0.5f)
                    {
                        float measured = BossBody.FlatDistance(previous.position, position) / elapsed;
                        speed = Mathf.Lerp(previous.speed, measured, 1f - Mathf.Exp(-elapsed / 0.15f));
                    }
                }
                motion[player] = (position, speed, Time.time);
            }
        }

        private bool CanReachFirst(HidingBush bush, Vector3 position)
        {
            float bossTime = Mathf.Max(0f, BossBody.FlatDistance(position, bush.Center) - AtSpot) / body.Settings.RunSpeed;
            foreach (UWBPlayerController player in trackedPlayers)
            {
                float distance = BossBody.FlatDistance(player.transform.position, bush.Center);
                float clearance = Mathf.Max(bush.Radius + StayOnMargin, body.Settings.TouchRadius);
                if (distance <= clearance) return false;
                float speed = motion.TryGetValue(player, out var sample) ? sample.speed : body.Settings.RunSpeed;
                // Keep some reaction allowance for a currently stationary player starting to chase.
                speed = Mathf.Max(body.Settings.RunSpeed * 0.5f, speed);
                if (bossTime + ArrivalMarginSeconds >= (distance - clearance) / speed) return false;
            }
            return true;
        }

        // Pick at the moment of teleport so a newly occupied bush cannot receive the boss.
        public bool TryWarpIn()
        {
            if (hidingSpots == null) return false;
            HidingBush destination = null;
            float best = float.MinValue;
            foreach (HidingBush bush in hidingSpots.Bushes)
            {
                if (bush == null || !bush.isActiveAndEnabled || bush == lastLeft) continue;
                if (bush.Occupant != null || bush.ReservedBy != null || bush.IsHeldByOther || bush.PlayerIsStandingOn) continue;
                if (body.InCorner(bush.Center)) continue;
                bool nearby = body.Players.TryGetNearest(bush.Center, out _, out float distance);
                if (nearby && distance <= Mathf.Max(body.Settings.HideClearance, bush.Radius + StayOnMargin)) continue;
                float score = nearby ? distance : BossBody.FlatDistance(body.Position, bush.Center);
                if (score <= best) continue;
                best = score;
                destination = bush;
            }
            if (destination == null) return false;
            hideout = destination;
            HasExited = false;
            body.Position = hideout.Center;
            hideout.PopIn(Vector3.zero);
            EnterHideout();
            return true;
        }


        // Compare arrival times instead of rejecting every box near a pursuing player.
        public bool TryFind(float reach, bool aheadOnly = false)
        {
            ObservePlayers();
            hideout = null;
            if (hidingSpots == null) return false;

            Vector3 position = body.Position;
            float best = reach;
            foreach (HidingBush bush in hidingSpots.Bushes)
            {
                if (bush == null || !bush.isActiveAndEnabled) continue;
                if (bush == lastLeft || bush.Occupant != null || bush.ReservedBy != null || bush.IsHeldByOther || bush.PlayerIsStandingOn) continue;

                Vector3 spot = bush.Center;
                Vector3 toward = spot - position;
                toward.y = 0f;
                if (aheadOnly && Vector3.Dot(toward, body.Heading) <= 0f) continue;
                float distance = BossBody.FlatDistance(position, spot);
                if (distance > best || body.InCorner(spot)) continue;
                if (!CanReachFirst(bush, position)) continue;
                if (!SafeRoute(position, spot)) continue;

                best = distance;
                hideout = bush;
            }

            return hideout != null;
        }

        // Heads for the spot TryFind picked.
        public void Begin()
        {
            HasExited = false;
            hidden = false;
            hideout.HoldOther(body.Settings.HideSeconds + HoldOnTheWay);
        }

        public bool Tick()
        {
            if (hideout == null) return false;
            return hidden ? StayHidden() : GoToHideout();
        }

        public void Stop()
        {
            if (hidden) body.Animator.Body.enabled = true;
            hidden = false;
            Release();
        }

        // Runs for the spot and jumps in; gives up if a player gets there first.
        private bool GoToHideout()
        {
            BossSettings settings = body.Settings;
            Vector3 position = body.Position;
            var spot = new Vector3(hideout.Center.x, BossBody.Height, hideout.Center.z);
            ObservePlayers();
            if (!hideout.isActiveAndEnabled || hideout.Occupant != null || hideout.ReservedBy != null ||
                hideout.PlayerIsStandingOn || !SafeRoute(position, spot) || !CanReachFirst(hideout, position))
            {
                Debug.Log("[BossController] A player got to the hiding spot first");
                Release();
                return false;
            }

            Vector3 toward = spot - position;
            toward.y = 0f;
            body.MoveTo(Vector3.MoveTowards(position, spot, settings.RunSpeed * Time.deltaTime));
            if (Time.deltaTime > 0f && BossBody.FlatDistance(position, body.Position) < settings.RunSpeed * Time.deltaTime * 0.1f &&
                BossBody.FlatDistance(body.Position, spot) > AtSpot)
            {
                Release();
                return false;
            }
            body.FaceWhereMoving();
            body.PaceWalk(settings.RunSpeed);
            if (BossBody.FlatDistance(body.Position, spot) > AtSpot) return true;

            hideout.PopIn(toward.sqrMagnitude > 0.0001f ? -toward.normalized : Vector3.right);
            SpriteFadeOut.LeaveBehind(body.Animator.Body, null, 0f, Vector2Int.zero, JumpInFadeSeconds, body.Parent);
            EnterHideout();
            return true;
        }

        private void EnterHideout()
        {
            hideout.HoldOther(body.Settings.HideSeconds);
            body.Animator.Body.enabled = false;
            hidden = true;
            hiddenFor = 0f;
            stoodOn = 0f;
            sneakOutAt = SneakOutTime();
            Debug.Log($"[BossController] Hides in {hideout.name}");
        }

        // Waits in the spot; out when its time is up or a player has stood on it long enough.
        private bool StayHidden()
        {
            BossSettings settings = body.Settings;
            hiddenFor += Time.deltaTime;
            float reach = hideout.Radius + (stoodOn > 0f ? StayOnMargin : 0f);
            bool onIt = body.Players.TryGetWithin(hideout.Center, reach, out _);
            stoodOn = onIt ? stoodOn + Time.deltaTime : Mathf.Max(0f, stoodOn - Time.deltaTime * FlushDrainRate);
            hideout.ShowOtherFlush(stoodOn / settings.FlushSeconds, onIt);

            // A player near keeps it in a little longer, but never past hideSeconds.
            if (body.Players.TryGetWithin(hideout.Center, settings.FleeRadius, out _)) sneakOutAt = Mathf.Max(sneakOutAt, Mathf.Min(settings.HideSeconds, hiddenFor + SneakOutTime()));
            bool flushed = stoodOn >= settings.FlushSeconds;
            bool timeUp = hiddenFor >= Mathf.Min(sneakOutAt, settings.HideSeconds);
            if (!flushed && !timeUp) return true;

            Debug.Log(flushed ? "[BossController] A player drove it out of hiding" : "[BossController] Comes out of hiding");
            Vector3 away = body.Players.TryGetNearest(hideout.Center, out Vector3 player, out _) ? hideout.Center - player : Vector3.left;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.left;
            hideout.PopOut(away);
            lastLeft = hideout;
            Release();
            hidden = false;
            HasExited = true;
            body.Animator.Body.enabled = true;
            body.Heading = away;
            return false;
        }

        private float SneakOutTime()
        {
            Vector2 range = body.Settings.SneakOutSeconds;
            return Random.Range(range.x, Mathf.Max(range.x, range.y));
        }

        // No player ahead on the way passes within routeClearance of it (one behind is left behind).
        private bool SafeRoute(Vector3 from, Vector3 to)
        {
            var start = new Vector2(from.x, from.z);
            var line = new Vector2(to.x, to.z) - start;
            body.Players.GetAll(playerPositions);
            foreach (Vector3 player in playerPositions)
            {
                var point = new Vector2(player.x, player.z);
                if (Vector2.Dot(point - start, line) < 0f) continue;

                float along = line.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(point - start, line) / line.sqrMagnitude) : 0f;
                if (Vector2.Distance(point, start + line * along) < body.Settings.RouteClearance) return false;
            }

            return true;
        }

        private void Release()
        {
            if (hideout != null) hideout.ReleaseOther();
            hideout = null;
        }
    }
}
