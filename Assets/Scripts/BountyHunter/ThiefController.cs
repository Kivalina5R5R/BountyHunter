using System;
using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    public enum ThiefState
    {
        Wander,
        Flee,
        Hidden,
        Caught
    }

    // One thief on the floor. While calm it sneaks from spot to spot, preferring places beside a
    // bush and away from players, walking on curved paths and pausing between trips.
    // When a player comes close it runs to a safe bush and hides. A player who stands on that bush long enough flushes it out; a player who overlaps it
    // while it is in the open catches it and it disappears. Each thief type is a prefab with its
    // own sprite and tuning, so new thieves only need a new prefab in ThiefSpawner.
    public sealed class ThiefController : MonoBehaviour
    {
        [SerializeField] private string typeName = "Chicken";
        [SerializeField] private SpriteRenderer body;
        // Which way the art faces; the sprite is mirrored to face its running direction.
        [SerializeField] private bool artFacesRight;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float wanderSpeed = 0.6f;
        [SerializeField, Min(0.1f)] private float fleeSpeed = 1.5f;
        [SerializeField, Min(0.1f)] private float acceleration = 2.5f;
        // How fast it can change direction; lower values give wider curves.
        [SerializeField, Min(10f)] private float wanderTurnDegrees = 200f;
        [SerializeField, Min(10f)] private float fleeTurnDegrees = 720f;

        [Header("Wandering")]
        [SerializeField] private Vector2 wanderPauseSeconds = new Vector2(1.5f, 3f);
        // How far each trip goes from where it stands.
        [SerializeField] private Vector2 tripDistance = new Vector2(1.2f, 3f);
        // Gap kept from the leaves of a bush when lurking beside it.
        [SerializeField, Min(0f)] private float lurkGap = 0.3f;

        [Header("Fleeing")]
        // How often a fleeing thief rethinks where to run, in seconds.
        [SerializeField, Min(0.05f)] private float replanSeconds = 0.2f;
        // Players closer than this push the running thief sideways around them.
        [SerializeField, Min(0.1f)] private float avoidRadius = 1.3f;
        // Distance from the floor edge where the thief starts turning away from it.
        [SerializeField, Min(0.05f)] private float edgeMargin = 0.6f;
        // Corners are dead ends where a thief is easily caught: spots this close to one count as
        // dangerous, both when fleeing and when picking where to wander.
        [SerializeField, Min(0.1f)] private float cornerRadius = 1.8f;
        // A fleeing thief only calms down once no player has been within calmRadius for calmSeconds,
        // so it does not stop and start again every time a player steps in and out of fleeRadius.
        [SerializeField, Min(0.1f)] private float calmRadius = 3.2f;
        [SerializeField, Min(0f)] private float calmSeconds = 1.2f;

        [Header("Crowd")]
        // Thieves in the open are never closer than this (centre to centre), about one body width,
        // so they never walk over each other.
        [SerializeField, Min(0.1f)] private float personalSpace = 0.7f;
        // Thieves try to stay at least this far from each other: wanderers steer clear and pick spots
        // away from the others, fleeing thieves mind it less, and one running to a bush hardly at all.
        [SerializeField, Min(0.1f)] private float comfortDistance = 1.4f;

        [Header("Senses")]
        // A player closer than this makes the thief run.
        [SerializeField, Min(0.1f)] private float fleeRadius = 2.2f;
        // A player whose centre comes this close to the thief's centre catches it.
        [SerializeField, Min(0.05f)] private float catchDistance = 0.45f;
        // A caught thief stays this long, until the fight cloud has popped up over it, then vanishes.
        [SerializeField, Min(0f)] private float vanishAfterCatchSeconds = 0.12f;

        [Header("Hiding")]
        // While hidden with no player near, the thief sneaks out after a random time in this range.
        [SerializeField] private Vector2 sneakOutSeconds = new Vector2(3f, 5f);
        // The thief never stays in a bush longer than this; it runs out even with a player near.
        [SerializeField, Min(0.5f)] private float maxHideSeconds = 5f;
        // For this long after coming out of a bush (flushed, sneaking out or time up) the thief
        // cannot be caught, so it gets a fair head start.
        [SerializeField, Min(0f)] private float exitGraceSeconds = 1f;

        private IPlayerLocator players;
        private IHidingSpots hidingSpots;
        private IThiefCrowd crowd;
        private Vector2 areaMin;
        private Vector2 areaMax;
        private ThiefState state = ThiefState.Wander;
        private Vector3 target;
        private bool hasTarget;
        private float waitUntil;
        private HidingBush targetBush;
        private HidingBush hiddenIn;
        // The bush it last came out of; it may only go back after hiding somewhere else.
        private HidingBush lastLeftBush;
        private readonly List<Vector3> playerPositions = new List<Vector3>();
        private float nextReplanAt;
        private float sneakOutAt;
        private float catchableAt;
        private float mustLeaveAt;
        private Vector3 lastThreat;
        private Vector3 lastMoveDirection = Vector3.back;
        private Vector3 velocity;
        private float tripSpeed;
        private float lastAlarmAt = float.MinValue;
        // Walking in from off the floor at the start of a round; it moves straight in, unclamped.
        private bool entering;
        private float enterAt;
        private Vector3 entryPoint;

        public ThiefState State => state;
        public string TypeName => typeName;
        public SpriteRenderer Body => body;
        public bool IsHidden => state == ThiefState.Hidden;

        // While hidden: seconds until it sneaks out on its own (it waits longer while a player is near).
        public float SecondsUntilSneakOut => state == ThiefState.Hidden ? Mathf.Max(0f, sneakOutAt - Time.time) : float.MaxValue;

        public event Action<ThiefController> Caught;

        // Gives the thief its players, bushes and the floor area it may move in (Vector2.y is world Z).
        public void Initialize(IPlayerLocator playerLocator, IHidingSpots spots, IThiefCrowd thieves, Vector2 moveAreaMin, Vector2 moveAreaMax)
        {
            players = playerLocator;
            hidingSpots = spots;
            crowd = thieves;
            areaMin = moveAreaMin;
            areaMax = moveAreaMax;
            BeginWander();
        }

        // Starts the round unseen inside the bush and sneaks out after seconds (a player nearby
        // makes it wait a little longer). Call right after Initialize.
        public void StartHiddenIn(HidingBush bush, float seconds)
        {
            entering = false;
            bush.Occupy(this);
            hiddenIn = bush;
            state = ThiefState.Hidden;
            sneakOutAt = Time.time + seconds;
            mustLeaveAt = sneakOutAt + maxHideSeconds;
            SetVisible(false);
        }

        // Waits unseen off the floor until WalkIn or StartHiddenIn says how it comes in.
        public void WaitOffFloor()
        {
            entering = true;
            enterAt = float.MaxValue;
            SetVisible(false);
        }

        // Starts from outside (off the floor) after seconds and hurries in toward inside; the moment it
        // is over the floor edge its own mind takes over (wandering, or running from a player near),
        // and it cannot be caught for exitGraceSeconds, the same head start as coming out of a bush.
        public void WalkIn(Vector3 outside, Vector3 inside, float seconds)
        {
            entering = true;
            enterAt = Time.time + seconds;
            transform.position = new Vector3(outside.x, transform.position.y, outside.z);
            entryPoint = new Vector3(inside.x, transform.position.y, inside.z);
            SetVisible(false);
        }

        // Called by a bush when a player stood on it long enough; runs away from that player.
        public void FlushOut(Vector3 threat)
        {
            if (state == ThiefState.Caught) return;

            HidingBush bush = hiddenIn;
            hiddenIn = null;
            SetVisible(true);
            Debug.Log($"[Thief] {name} flushed out of the bush, running", this);
            Flee(threat, true);
            ExitBush(bush);
        }

        private void Update()
        {
            if (players == null || state == ThiefState.Caught) return;
            if (entering)
            {
                UpdateWalkIn();
                return;
            }

            bool threatened = players.TryGetNearest(transform.position, out Vector3 threat, out float threatDistance) &&
                threatDistance <= fleeRadius;

            if (state == ThiefState.Hidden)
            {
                // The bush may creep away with the thief inside.
                Vector3 bushCenter = hiddenIn.Center;
                transform.position = new Vector3(bushCenter.x, transform.position.y, bushCenter.z);
                if (threatened) lastThreat = threat;
                UpdateHidden(threatened);
                return;
            }

            if (threatDistance <= catchDistance && Time.time >= catchableAt)
            {
                Catch();
                return;
            }

            players.GetAll(playerPositions);
            if (threatDistance <= calmRadius) lastAlarmAt = Time.time;
            bool calm = Time.time - lastAlarmAt >= calmSeconds;
            if (threatened && (state != ThiefState.Flee || Time.time >= nextReplanAt || !TargetStillSafe())) Flee(threat, false);
            else if (calm && state == ThiefState.Flee && targetBush == null) BeginWander();

            // While running in the open, the run direction is steered every frame.
            if (state == ThiefState.Flee && targetBush == null) SetTarget(transform.position + EscapeDirection() * 1.5f);
            Move();
            KeepApart();
        }

        private void UpdateWalkIn()
        {
            if (Time.time < enterAt) return;

            SetVisible(true);
            Vector3 position = transform.position;
            // Hurries in, between a stroll and a dash, so it does not dawdle where it cannot be seen.
            Vector3 next = Vector3.MoveTowards(position, entryPoint, Mathf.Lerp(wanderSpeed, fleeSpeed, 0.5f) * Time.deltaTime);
            Face(next.x - position.x);
            if ((next - position).sqrMagnitude > 0.000001f) lastMoveDirection = (next - position).normalized;
            transform.position = next;

            // Over the edge: it decides for itself from here, running if a player is near.
            bool onFloor = next.x >= areaMin.x && next.x <= areaMax.x && next.z >= areaMin.y && next.z <= areaMax.y;
            if (!onFloor && (entryPoint - next).sqrMagnitude > 0.0001f) return;

            FinishWalkIn();
            if (players.TryGetNearest(next, out Vector3 threat, out float distance) && distance <= fleeRadius)
            {
                Debug.Log($"[Thief] {name} came onto the floor next to a player, running", this);
                players.GetAll(playerPositions);
                Flee(threat, true);
                return;
            }

            Debug.Log($"[Thief] {name} came onto the floor", this);
            BeginWander();
            // Keeps its pace and heading into a trip of its own instead of stopping at the edge.
            velocity = lastMoveDirection * Mathf.Lerp(wanderSpeed, fleeSpeed, 0.5f);
            StartTrip();
        }

        private void FinishWalkIn()
        {
            entering = false;
            catchableAt = Time.time + exitGraceSeconds;
        }

        private void UpdateHidden(bool threatened)
        {
            if (Time.time >= mustLeaveAt)
            {
                LeaveAfterTimeLimit(threatened);
                return;
            }

            if (threatened)
            {
                // A player nearby keeps it hiding a little longer, but never past maxHideSeconds.
                sneakOutAt = Mathf.Min(mustLeaveAt, Time.time + RandomIn(sneakOutSeconds));
                return;
            }

            if (Time.time < sneakOutAt) return;

            Debug.Log($"[Thief] {name} sneaks out of {hiddenIn.name}", this);
            HidingBush bush = hiddenIn;
            hiddenIn = null;
            SetVisible(true);
            BeginWander();
            // Heads somewhere right away so the bush knows which side it leaves from.
            StartTrip();
            ExitBush(bush);
        }

        // Time is up: walks out calmly, or runs from a nearby player without coming back to this bush.
        private void LeaveAfterTimeLimit(bool threatened)
        {
            HidingBush bush = hiddenIn;
            Debug.Log($"[Thief] {name} has hidden for {maxHideSeconds:0.#}s and must leave {bush.name}", this);
            hiddenIn = null;
            SetVisible(true);
            if (threatened)
            {
                players.GetAll(playerPositions);
                Flee(lastThreat, true);
            }
            else
            {
                BeginWander();
                StartTrip();
            }

            ExitBush(bush);
        }

        // Runs to a safe bush, or away from every player when no bush is safe. Called again every
        // replanSeconds, so the plan follows the players as they move.
        private void Flee(Vector3 threat, bool flushed)
        {
            nextReplanAt = Time.time + replanSeconds;
            if (playerPositions.Count == 0) players.GetAll(playerPositions);
            HidingBush bush = hidingSpots?.FindHidingBush(this, playerPositions, lastLeftBush);
            if (bush != targetBush) ReleaseTargetBush();

            bool wasFleeing = state == ThiefState.Flee;
            state = ThiefState.Flee;
            if (bush != null)
            {
                targetBush = bush;
                bool newBush = targetBush != bush;
                targetBush = bush;
                bush.Reserve(this);
                SetTarget(bush.Center);
                if (!wasFleeing || flushed || newBush) Debug.Log($"[Thief] {name} runs to hide in {bush.name}", this);
                return;
            }

            if (!wasFleeing || flushed) Debug.Log($"[Thief] {name} has no safe bush, running away from {playerPositions.Count} player(s)", this);
            SetTarget(transform.position + EscapeDirection() * 1.5f);
        }

        // A bush stops being safe once a player stands on it or another thief takes it.
        private bool TargetStillSafe()
        {
            return targetBush == null || targetBush.IsFreeFor(this);
        }

        private void Move()
        {
            float dt = Time.deltaTime;
            if (!hasTarget)
            {
                velocity = Vector3.MoveTowards(velocity, Vector3.zero, acceleration * dt);
                if (state == ThiefState.Wander) Idle();
                return;
            }

            bool fleeing = state == ThiefState.Flee;
            Vector3 position = transform.position;
            Vector3 toTarget = target - position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance <= 0.06f)
            {
                Arrive();
                return;
            }

            // Calm trips slow down into their stop; flights dash straight in.
            float speed = fleeing ? fleeSpeed : tripSpeed * Mathf.Clamp(distance / 0.5f, 0.3f, 1f);
            // Turning gets sharper near the goal so it never circles around it.
            float turn = (fleeing ? fleeTurnDegrees : wanderTurnDegrees) * (1f + 3f * (1f - Mathf.Clamp01(distance / 0.6f)));
            Vector3 heading = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : lastMoveDirection;
            Vector3 wanted = toTarget / distance;
            if (fleeing) wanted = AvoidPlayers(position, wanted);
            // Steering clear fades out on the last stretch, so it can still reach its goal.
            wanted = AvoidThieves(position, wanted, CrowdWeight() * Mathf.Clamp01(distance / 0.5f));
            heading = Vector3.RotateTowards(heading, wanted, turn * Mathf.Deg2Rad * dt, 0f);
            float currentSpeed = Mathf.MoveTowards(velocity.magnitude, speed, acceleration * (fleeing ? 3f : 1f) * dt);
            velocity = heading * currentSpeed;

            Vector3 step = velocity * dt;
            Vector3 next = step.magnitude >= distance ? new Vector3(target.x, position.y, target.z) : ClampToArea(position + step);
            Face(next.x - position.x);
            if ((next - position).sqrMagnitude > 0.000001f) lastMoveDirection = (next - position).normalized;
            transform.position = next;
        }

        private void Arrive()
        {
            hasTarget = false;
            // A flee without a bush is re-planned from the player position on the next Update.
            if (targetBush != null)
            {
                Hide(targetBush);
                return;
            }

            if (state != ThiefState.Wander) return;
            velocity = Vector3.zero;
            BeginPause();
        }

        // Stands still between trips, then sets off again.
        private void Idle()
        {
            if (Time.time >= waitUntil) StartTrip();
        }

        private void BeginPause()
        {
            waitUntil = Time.time + RandomIn(wanderPauseSeconds);
        }

        private void StartTrip()
        {
            tripSpeed = wanderSpeed * UnityEngine.Random.Range(0.8f, 1.2f);
            SetTarget(PickWanderGoal());
        }

        // Scores a handful of candidate spots and walks to the best: beside a bush (cover), away
        // from players, a sensible distance away, and roughly ahead so it does not zigzag.
        private Vector3 PickWanderGoal()
        {
            Vector3 position = transform.position;
            Vector3 best = position;
            float bestScore = float.MinValue;
            for (int i = 0; i < 12; i++)
            {
                Vector3 candidate = ClampToArea(i % 2 == 0 ? LurkSpot() : NearbySpot(position));
                Vector3 offset = candidate - position;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance < tripDistance.x * 0.6f) continue;

                float score = players.TryGetNearest(candidate, out _, out float playerDistance) ? Mathf.Min(playerDistance, 4f) : 4f;
                if (NextToBush(candidate)) score += 1.2f;
                score -= Mathf.Abs(distance - (tripDistance.x + tripDistance.y) * 0.5f) * 0.4f;
                score += Vector3.Dot(offset / Mathf.Max(distance, 0.0001f), lastMoveDirection) * 0.5f;
                score += UnityEngine.Random.Range(0f, 0.6f);
                // Spots another thief is standing on or heading to are crowded.
                if (Crowded(candidate)) score -= 3f;
                score -= CornerCloseness(candidate) * 3f;

                if (score <= bestScore) continue;
                bestScore = score;
                best = candidate;
            }

            return bestScore > float.MinValue ? best : ClampToArea(NearbySpot(position));
        }

        // A spot just outside the leaves of a random bush.
        private Vector3 LurkSpot()
        {
            if (hidingSpots == null || hidingSpots.Bushes.Count == 0) return NearbySpot(transform.position);

            HidingBush bush = hidingSpots.Bushes[UnityEngine.Random.Range(0, hidingSpots.Bushes.Count)];
            if (bush == null) return NearbySpot(transform.position);

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            var around = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            return bush.Center + around * (bush.Radius * 1.3f + lurkGap);
        }

        private Vector3 NearbySpot(Vector3 from)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            return from + direction * RandomIn(tripDistance);
        }

        private bool NextToBush(Vector3 point)
        {
            if (hidingSpots == null) return false;

            foreach (HidingBush bush in hidingSpots.Bushes)
            {
                if (bush == null) continue;
                if (SpawnerPlayerLocator.FlatDistance(point, bush.Center) < bush.Radius * 1.3f + lurkGap + 0.4f) return true;
            }

            return false;
        }

        private void Hide(HidingBush bush)
        {
            targetBush = null;
            if (!bush.IsFreeFor(this))
            {
                bush.CancelReservation(this);
                BeginWander();
                return;
            }

            // It ran in along its last step, so it entered from the opposite side.
            bush.Enter(this, -lastMoveDirection);
            // Hiding somewhere new makes the previous bush allowed again.
            if (lastLeftBush != bush) lastLeftBush = null;
            hiddenIn = bush;
            state = ThiefState.Hidden;
            mustLeaveAt = Time.time + maxHideSeconds;
            sneakOutAt = Mathf.Min(mustLeaveAt, Time.time + RandomIn(sneakOutSeconds));
            SetVisible(false);
            Debug.Log($"[Thief] {name} is hiding in {bush.name}", this);
        }

        private void Catch()
        {
            state = ThiefState.Caught;
            ReleaseTargetBush();
            Debug.Log($"[Thief] {name} ({typeName}) was CAUGHT at {transform.position.x:0.00}, {transform.position.z:0.00}", this);
            Caught?.Invoke(this);
            Destroy(gameObject, vanishAfterCatchSeconds);
        }

        private void BeginWander()
        {
            ReleaseTargetBush();
            state = ThiefState.Wander;
            hasTarget = false;
            BeginPause();
        }

        private void ReleaseTargetBush()
        {
            if (targetBush != null) targetBush.CancelReservation(this);
            targetBush = null;
        }

        private void SetTarget(Vector3 point)
        {
            target = ClampToArea(point);
            hasTarget = true;
        }

        // Best direction to run in the open: away from every player (closer players count more),
        // and away from the floor edges so it is not cornered. Of a fan of directions, the one
        // that keeps the most distance from all players a short run ahead wins.
        private Vector3 EscapeDirection()
        {
            Vector3 position = transform.position;
            Vector3 push = Vector3.zero;
            foreach (Vector3 player in playerPositions)
            {
                Vector3 away = position - player;
                away.y = 0f;
                float distance = Mathf.Max(0.1f, away.magnitude);
                push += away / (distance * distance);
            }

            push += EdgePush(position) * 2f;
            Vector3 preferred = push.sqrMagnitude > 0.0001f ? push.normalized : lastMoveDirection;

            Vector3 best = preferred;
            float bestScore = float.MinValue;
            for (int i = 0; i < 16; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                Vector3 ahead = ClampToArea(position + direction * 1.2f);
                // Directions blocked by an edge go nowhere and score low.
                float room = Vector3.Distance(position, ahead) / 1.2f;
                float closest = float.MaxValue;
                foreach (Vector3 player in playerPositions) closest = Mathf.Min(closest, SpawnerPlayerLocator.FlatDistance(ahead, player));
                float score = Mathf.Min(closest, 3f) + room * 1.5f + Vector3.Dot(direction, preferred) * 0.6f + Vector3.Dot(direction, lastMoveDirection) * 0.3f;
                // Running into another thief is worth avoiding, but much less than running into a player.
                score -= Crowding(ahead) * 0.8f;
                // A corner traps it, so heading toward one is bad even when it is still a little way off.
                Vector3 further = ClampToArea(position + direction * 2.4f);
                score -= CornerCloseness(ahead) * 3f + CornerCloseness(further) * 1.5f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = direction;
            }

            return best;
        }

        // 0 when the point is at least cornerRadius from every floor corner, up to 1 in a corner.
        private float CornerCloseness(Vector3 point)
        {
            float x = Mathf.Min(point.x - areaMin.x, areaMax.x - point.x);
            float z = Mathf.Min(point.z - areaMin.y, areaMax.y - point.z);
            float distance = Mathf.Sqrt(x * x + z * z);
            return Mathf.Clamp01(1f - distance / cornerRadius);
        }

        // Points inward when close to a floor edge, stronger the closer it gets.
        private Vector3 EdgePush(Vector3 position)
        {
            Vector3 push = Vector3.zero;
            push.x += Mathf.Clamp01(1f - (position.x - areaMin.x) / edgeMargin) - Mathf.Clamp01(1f - (areaMax.x - position.x) / edgeMargin);
            push.z += Mathf.Clamp01(1f - (position.z - areaMin.y) / edgeMargin) - Mathf.Clamp01(1f - (areaMax.y - position.z) / edgeMargin);
            return push;
        }

        // While fleeing, players near the path bend it sideways so the thief runs around them.
        private Vector3 AvoidPlayers(Vector3 position, Vector3 heading)
        {
            Vector3 bend = Vector3.zero;
            foreach (Vector3 player in playerPositions)
            {
                Vector3 away = position - player;
                away.y = 0f;
                float distance = away.magnitude;
                if (distance >= avoidRadius || distance < 0.0001f) continue;
                bend += away / distance * (1f - distance / avoidRadius);
            }

            return (heading + bend * 2f).normalized;
        }

        // How much keeping away from other thieves matters right now: fully while wandering, less while
        // fleeing in the open, and little when running for a bush, where passing close may be needed.
        private float CrowdWeight()
        {
            if (state == ThiefState.Wander) return 1.2f;
            return targetBush != null ? 0.3f : 0.7f;
        }

        // Bends a heading away from other thieves in the open inside comfortDistance, most for those
        // ahead; one straight ahead is passed on the side it is already leaning to.
        private Vector3 AvoidThieves(Vector3 position, Vector3 heading, float weight)
        {
            if (crowd == null || weight <= 0f) return heading;

            var right = new Vector3(heading.z, 0f, -heading.x);
            Vector3 bend = Vector3.zero;
            foreach (ThiefController other in crowd.Thieves)
            {
                if (!InTheOpen(other)) continue;

                Vector3 away = position - other.transform.position;
                away.y = 0f;
                float distance = away.magnitude;
                if (distance >= comfortDistance || distance < 0.0001f) continue;

                away /= distance;
                float closeness = 1f - distance / comfortDistance;
                float ahead = Mathf.Clamp01(Vector3.Dot(heading, -away));
                bend += away * closeness * (0.4f + ahead);
                bend += right * (Vector3.Dot(right, away) >= 0f ? 1f : -1f) * closeness * ahead;
            }

            if (bend.sqrMagnitude < 0.000001f) return heading;
            return (heading + bend * weight).normalized;
        }

        // 0 when no other thief in the open is within comfortDistance of the point, up to 1 on top of one.
        private float Crowding(Vector3 point)
        {
            if (crowd == null) return 0f;

            float crowding = 0f;
            foreach (ThiefController other in crowd.Thieves)
            {
                if (!InTheOpen(other)) continue;
                float distance = SpawnerPlayerLocator.FlatDistance(point, other.transform.position);
                crowding = Mathf.Max(crowding, 1f - distance / comfortDistance);
            }

            return crowding;
        }

        private bool InTheOpen(ThiefController other)
        {
            return other != null && other != this && !other.IsHidden && other.State != ThiefState.Caught;
        }

        // Moves this thief out of any other thief in the open closer than personalSpace, at once
        // rather than gradually, so no speed can carry it through. Each takes half of the overlap
        // and both do this, so they part evenly; hidden thieves do not count (they are in a bush).
        private void KeepApart()
        {
            if (crowd == null || state == ThiefState.Caught) return;

            Vector3 position = transform.position;
            Vector3 push = Vector3.zero;
            foreach (ThiefController other in crowd.Thieves)
            {
                if (other == null || other == this || other.IsHidden || other.State == ThiefState.Caught) continue;

                Vector3 away = position - other.transform.position;
                away.y = 0f;
                float distance = away.magnitude;
                if (distance >= personalSpace) continue;

                // Exactly on top of each other: split by name order so they move opposite ways.
                if (distance < 0.0001f) away = string.CompareOrdinal(name, other.name) < 0 ? Vector3.left : Vector3.right;
                else away /= distance;
                push += away * ((personalSpace - distance) * 0.5f);
            }

            if (push.sqrMagnitude < 0.000001f) return;
            transform.position = ClampToArea(position + push);
        }

        // True when another thief in the open stands near the point or is walking to it.
        private bool Crowded(Vector3 point)
        {
            if (crowd == null) return false;

            foreach (ThiefController other in crowd.Thieves)
            {
                if (other == null || other == this || other.IsHidden) continue;
                if (SpawnerPlayerLocator.FlatDistance(point, other.transform.position) < comfortDistance) return true;
                if (other.hasTarget && SpawnerPlayerLocator.FlatDistance(point, other.target) < comfortDistance) return true;
            }

            return false;
        }

        private Vector3 ClampToArea(Vector3 point)
        {
            return new Vector3(
                Mathf.Clamp(point.x, areaMin.x, areaMax.x),
                transform.position.y,
                Mathf.Clamp(point.z, areaMin.y, areaMax.y));
        }

        private void Face(float horizontalDirection)
        {
            if (body == null || Mathf.Abs(horizontalDirection) < 0.0001f) return;
            body.flipX = horizontalDirection > 0f != artFacesRight;
        }

        private void SetVisible(bool visible)
        {
            if (body != null) body.enabled = visible;
        }

        private void ExitBush(HidingBush bush)
        {
            if (bush == null) return;

            // It may not hide here again until it has hidden in another bush.
            lastLeftBush = bush;
            catchableAt = Time.time + exitGraceSeconds;
            Vector3 toward = hasTarget ? target - transform.position : lastMoveDirection;
            toward.y = 0f;
            bush.Exit(this, toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector3.zero);
        }

        private static float RandomIn(Vector2 range)
        {
            return UnityEngine.Random.Range(range.x, Mathf.Max(range.x, range.y));
        }

        private void OnDestroy()
        {
            ReleaseTargetBush();
            if (hiddenIn != null) hiddenIn.Leave(this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 1f);
            Gizmos.DrawWireSphere(transform.position, fleeRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, catchDistance);
        }
#endif
    }
}
