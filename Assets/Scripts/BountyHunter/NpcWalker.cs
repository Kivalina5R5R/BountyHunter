using System;
using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // A passer-by on the floor (the sloth officer): it strolls from spot to spot at its own slow pace
    // and stops for a while now and then. It neither runs from the players nor hides, but it never
    // walks into one either: it only sets off along paths that keep clear of them, and stops and
    // picks another way when one steps in front of it. It keeps off the hiding spots and out of the
    // way of a thief running to hide, and does not stand about right next to others. So a bump is
    // always the player walking into it, an innocent officer grabbed by mistake: it raises Bumped
    // once and NpcCrowd hides it in the scuffle cloud; after that it is back on the same spot in
    // its angry act (GetAngry) before it strolls on. Leave sends it off the floor over the closest
    // edge it can reach without walking into a player (it can still be bumped on the way); Left
    // says when it is out of sight. It faces the way it walks (mirrored only); the frames are played
    // by ThiefFrameAnimation on its body.
    public sealed class NpcWalker : MonoBehaviour
    {
        [SerializeField] private string displayName = "Sloth";
        [SerializeField] private SpriteRenderer body;
        // The body's frames; only needed for a hopper (Pig, Raccoon), whose steps follow its hops.
        [SerializeField] private ThiefFrameAnimation frames;
        [SerializeField] private bool artFacesRight;

        [Header("Strolling")]
        [SerializeField, Min(0.05f)] private float walkSpeed = 0.3f;
        [SerializeField, Min(0.1f)] private float acceleration = 1.2f;
        [SerializeField] private Vector2 pauseSeconds = new Vector2(1.5f, 4f);
        [SerializeField] private Vector2 tripDistance = new Vector2(1f, 3f);
        // Strolls aim for spots this far inside the floor's edges.
        [SerializeField, Min(0f)] private float edgeMargin = 0.6f;
        // Walking in from outside, it is free to stroll once this far past the edge; 0 frees it the
        // moment it steps over the edge (its first trip still heads edgeMargin in).
        [SerializeField, Min(0f)] private float enterDepth = 0f;

        // Paths it sets off on keep at least this far from every player.
        [SerializeField, Min(0f)] private float pathClearance = 0.65f;
        // A player this close ahead of it while it walks makes it stop and choose another way.
        [SerializeField, Min(0f)] private float yieldDistance = 0.7f;
        // How long it stands still after giving way, and when no clear path is found.
        [SerializeField, Min(0f)] private float yieldPauseSeconds = 0.6f;

        // Thieves and other passers-by closer than this are stepped away from (they step aside too).
        [SerializeField, Min(0.1f)] private float personalSpace = 0.7f;
        // While walking it steers round thieves and other passers-by closer than this.
        [SerializeField, Min(0.1f)] private float avoidDistance = 1.3f;
        // Trips aim for spots up to this far from the thieves and other passers-by (further counts the
        // same), so the floor stays evenly spread instead of everyone gathering in one place.
        [SerializeField, Min(0.1f)] private float spreadDistance = 2.5f;
        // Standing still, it walks on when a thief or another passer-by is closer than this, and trips
        // avoid ending this close to one.
        [SerializeField, Min(0f)] private float restSpacing = 1.3f;

        [Header("Hiding spots")]
        // Trips never end this close to a hiding spot's leaves, and when a thief runs for a spot it
        // gets out of the way (off the spot and off the thief's line to it) at once.
        [SerializeField, Min(0f)] private float bushClearance = 0.6f;
        // How often it looks around for a thief to make way for or a neighbour standing too close.
        [SerializeField, Min(0.05f)] private float lookAroundSeconds = 0.25f;

        [Header("Bumping")]
        // A player whose centre comes this close has bumped into it, from the moment it walks in
        // (NpcCrowd only sends it in where no player is near).
        [SerializeField, Min(0.1f)] private float bumpDistance = 0.45f;
        // After its angry act it cannot be bumped again for this long, so a player still standing
        // there is not fined twice; it walks off first.
        [SerializeField, Min(0f)] private float bumpGraceSeconds = 1.5f;

        [Header("Leaving")]
        // It is gone once this far past an edge, out of sight.
        [SerializeField, Min(0.1f)] private float goneOutside = 0.45f;

        private readonly List<Vector3> playerPositions = new List<Vector3>();
        private readonly List<Vector3> walkerPositions = new List<Vector3>();
        private IReadOnlyList<IFloorWalkers> others;
        private IPlayerLocator players;
        private IHidingSpots hidingSpots;
        private float nextLookAroundAt;
        private Vector2 areaMin;
        private Vector2 areaMax;
        private Vector3 goal;
        private Vector3 velocity;
        private float restUntil;
        private bool walking;
        private bool entering;
        private bool leaving;
        private bool bumped;
        // Until when it stands in its angry act, and until when it cannot be bumped.
        private float angryUntil = float.MinValue;
        private float safeUntil = float.MinValue;
        // When the angry act starts after GetAngry, or float.MaxValue when none is waiting.
        private float angerStartsAt = float.MaxValue;

        public string DisplayName => displayName;
        // Which way its pictures look when not flipped (the boss copies it to pass for one).
        public bool ArtFacesRight => artFacesRight;
        // Still walking in from outside the floor.
        public bool IsComingIn => entering;

        public event Action<NpcWalker> Bumped;
        // Raised once it has walked off the floor after Leave.
        public event Action<NpcWalker> Left;

        // Gives it the players to watch for, the others on the floor to keep from walking over (thieves,
        // passers-by; entries may be null), the hiding spots to keep clear of (may be null) and the
        // floor area to stroll in (x and world z).
        public void Initialize(IPlayerLocator playerLocator, IReadOnlyList<IFloorWalkers> floorWalkers, IHidingSpots spots, Vector2 moveAreaMin, Vector2 moveAreaMax)
        {
            players = playerLocator;
            others = floorWalkers;
            hidingSpots = spots;
            areaMin = moveAreaMin;
            areaMax = moveAreaMax;
            // Its picture stands up from the feet towards the wall (+z), so the feet stay its height
            // below the top edge and the head does not stick out over the floor.
            areaMax.y = Mathf.Max(areaMin.y, moveAreaMax.y - HeadRoom());
        }

        // How far its picture reaches above its feet (its tight outline, so empty margins do not count).
        private float HeadRoom()
        {
            if (body == null || body.sprite == null) return 0f;

            float top = 0f;
            foreach (Vector2 point in body.sprite.vertices) top = Mathf.Max(top, point.y);
            return top * body.transform.lossyScale.y;
        }

        // Starts outside the floor and walks in toward inside. As soon as it is enterDepth past the edge
        // it is free, like a thief, and picks its own way at once. It can be bumped all along.
        public void WalkIn(Vector3 outside, Vector3 inside)
        {
            transform.position = outside;
            goal = inside;
            velocity = Vector3.zero;
            entering = true;
            leaving = false;
            walking = true;
            bumped = false;
        }

        // Appears at point already on the floor (stepping out of a hiding spot) and strolls off at once.
        // It never goes back into hiding.
        public void StepOutAt(Vector3 point)
        {
            transform.position = point;
            velocity = Vector3.zero;
            entering = false;
            leaving = false;
            walking = false;
            bumped = false;
            restUntil = 0f;
            StartTrip();
        }

        // Heads off the floor: at once, or after the angry act or scuffle it is in now.
        public void Leave()
        {
            if (leaving) return;

            leaving = true;
            if (bumped || Time.time < angryUntil || angerStartsAt != float.MaxValue) return;
            StartTrip();
        }

        // Back on its spot near the end of the scuffle: stands still for delaySeconds (while the X
        // lands on it), then plays its angry act (its frames' angryFrames) and strolls on. It cannot be
        // bumped from now until bumpGraceSeconds after the act.
        public void GetAngry(float delaySeconds)
        {
            bumped = false;
            walking = false;
            entering = false;
            velocity = Vector3.zero;
            if (frames != null) frames.Stand();
            angerStartsAt = Time.time + Mathf.Max(0f, delaySeconds);
            angryUntil = float.MaxValue;
            safeUntil = float.MaxValue;
        }

        private void Update()
        {
            if (Time.time >= angerStartsAt) StartAngryAct();
            if (bumped || Time.time < angryUntil) return;

            if (Time.time >= safeUntil && players != null &&
                players.TryGetNearest(transform.position, out _, out float distance) && distance <= bumpDistance)
            {
                bumped = true;
                Bumped?.Invoke(this);
                return;
            }

            if (leaving && OutsideBy() >= goneOutside)
            {
                bumped = true;
                Left?.Invoke(this);
                return;
            }

            if (LookAround()) StartTrip();
            else if (walking) Walk();
            else if (Time.time >= restUntil) StartTrip();
            else Slow();
            KeepApart();
        }

        private void StartAngryAct()
        {
            angerStartsAt = float.MaxValue;
            float act = frames != null ? frames.PlayAngry() : 0f;
            angryUntil = Time.time + act;
            restUntil = angryUntil;
            safeUntil = angryUntil + bumpGraceSeconds;
        }

        // Every lookAroundSeconds (once on the floor): true when it stands where a thief is running
        // to hide, or its trip ends there, or (standing still) a thief or another passer-by is too close.
        private bool LookAround()
        {
            if (entering || Time.time < nextLookAroundAt) return false;
            nextLookAroundAt = Time.time + lookAroundSeconds;

            if (InWayOfHidingThief(transform.position)) return true;
            if (walking) return InWayOfHidingThief(goal);

            GatherWalkers();
            return NearestWalker(transform.position) < restSpacing;
        }

        // True when point is on a hiding spot a thief is running for, or on its line to that spot.
        private bool InWayOfHidingThief(Vector3 point)
        {
            if (hidingSpots == null) return false;

            Vector2 at = new Vector2(point.x, point.z);
            foreach (HidingBush bush in hidingSpots.Bushes)
            {
                if (bush == null || bush.ReservedBy == null) continue;

                Vector3 thief = bush.ReservedBy.transform.position;
                Vector2 start = new Vector2(thief.x, thief.z);
                Vector2 end = new Vector2(bush.Center.x, bush.Center.z);
                if (DistanceToSegment(at, start, end) < bush.Radius + bushClearance) return true;
            }

            return false;
        }

        // True when point is closer than bushClearance to the leaves of any hiding spot.
        private bool NearHidingSpot(Vector3 point)
        {
            if (hidingSpots == null) return false;

            foreach (HidingBush bush in hidingSpots.Bushes)
            {
                if (bush != null && FlatDistance(point, bush.Center) < bush.Radius + bushClearance) return true;
            }

            return false;
        }

        // Moves out of any thief or passer-by closer than personalSpace by half the overlap (they move
        // the other half), so they never walk over each other.
        private void KeepApart()
        {
            if (others == null) return;

            GatherWalkers();
            Vector3 position = transform.position;
            Vector3 push = Vector3.zero;
            foreach (Vector3 other in walkerPositions)
            {
                Vector3 away = position - other;
                away.y = 0f;
                float distance = away.magnitude;
                if (distance >= personalSpace) continue;

                // Exactly on top of each other: pick a side from its name so the two split apart.
                away = distance < 0.0001f ? (name.GetHashCode() % 2 == 0 ? Vector3.left : Vector3.right) : away / distance;
                push += away * ((personalSpace - distance) * 0.5f);
            }

            if (push.sqrMagnitude < 0.000001f) return;
            Vector3 next = position + push;
            transform.position = entering || leaving ? next : Clamped(next, 0f);
        }

        private void Walk()
        {
            if (entering && Clamped(transform.position, enterDepth) == transform.position)
            {
                entering = false;
                walking = false;
                StartTrip();
                return;
            }

            Vector3 toGoal = goal - transform.position;
            toGoal.y = 0f;
            if (toGoal.magnitude < 0.08f)
            {
                // On its way out it keeps going until it is out of sight.
                if (leaving)
                {
                    StartTrip();
                    return;
                }

                walking = false;
                restUntil = Time.time + UnityEngine.Random.Range(pauseSeconds.x, pauseSeconds.y);
                return;
            }

            // Gives way to a player in front rather than walking into them (not while coming in from
            // outside, where it cannot be bumped anyway).
            if (!entering && PlayerAhead(toGoal.normalized))
            {
                walking = false;
                restUntil = Time.time + yieldPauseSeconds;
                return;
            }

            // Eases off over the last stretch instead of stopping dead, and walks round thieves and
            // other passers-by ahead (fading out near the goal so it still gets there).
            float speed = walkSpeed * Mathf.Clamp01(toGoal.magnitude / 0.3f + 0.3f);
            Vector3 heading = AvoidWalkers(toGoal.normalized, Mathf.Clamp01(toGoal.magnitude / 0.5f));
            Steer(heading * speed);
        }

        // Bends a heading away from thieves and passers-by within avoidDistance, most for those ahead;
        // one straight ahead is passed on the side it is already leaning to.
        private Vector3 AvoidWalkers(Vector3 heading, float weight)
        {
            if (weight <= 0f) return heading;

            GatherWalkers();
            Vector3 position = transform.position;
            var right = new Vector3(heading.z, 0f, -heading.x);
            Vector3 bend = Vector3.zero;
            foreach (Vector3 other in walkerPositions)
            {
                Vector3 away = position - other;
                away.y = 0f;
                float distance = away.magnitude;
                if (distance >= avoidDistance || distance < 0.0001f) continue;

                away /= distance;
                float closeness = 1f - distance / avoidDistance;
                float ahead = Mathf.Clamp01(Vector3.Dot(heading, -away));
                bend += away * closeness * (0.4f + ahead);
                bend += right * (Vector3.Dot(right, away) >= 0f ? 1f : -1f) * closeness * ahead;
            }

            if (bend.sqrMagnitude < 0.000001f) return heading;
            return (heading + bend * (1.2f * weight)).normalized;
        }

        private void Slow()
        {
            Steer(Vector3.zero);
        }

        private void Steer(Vector3 wanted)
        {
            velocity = Vector3.MoveTowards(velocity, wanted, acceleration * Time.deltaTime);
            Vector3 next = transform.position + velocity * (Stride() * Time.deltaTime);
            // Never off the floor once in (its trips aim further in, edgeMargin), until it leaves.
            if (!entering && !leaving) next = Clamped(next, 0f);
            transform.position = next;
            Face(velocity.x);
        }

        // How much of its velocity it moves at this moment: always all of it when walking; for a
        // hopper only while in the air (its frames say when), after telling them it wants to go.
        private float Stride()
        {
            if (frames == null || !frames.Hops) return 1f;

            frames.MoveIntent = velocity.magnitude;
            return frames.StrideNow;
        }

        // Somewhere tripDistance away inside the floor whose path keeps pathClearance from the players,
        // preferring spots away from the thieves and other passers-by and out from the edges (not simply
        // the spot furthest from the players, which sent every passer-by to the same far side). When no
        // path is clear it waits a moment and tries again.
        private void StartTrip()
        {
            if (players != null) players.GetAll(playerPositions);
            else playerPositions.Clear();
            GatherWalkers();
            if (leaving)
            {
                StartExit();
                return;
            }

            Vector3 from = transform.position;
            Vector3 best = from;
            float bestScore = float.MinValue;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
                float length = UnityEngine.Random.Range(tripDistance.x, tripDistance.y);
                Vector3 candidate = Clamped(from + new Vector3(direction.x, 0f, direction.y) * length, edgeMargin);
                if (FlatDistance(from, candidate) < tripDistance.x * 0.5f) continue;
                if (PathClearance(from, candidate) < pathClearance) continue;
                if (NearHidingSpot(candidate) || InWayOfHidingThief(candidate)) continue;

                float nearestWalker = NearestWalker(candidate);
                float score = Mathf.Min(nearestWalker, spreadDistance)
                    + Mathf.Min(EdgeDistance(candidate), 1.5f) * 0.5f
                    + UnityEngine.Random.Range(0f, 0.6f);
                if (nearestWalker < restSpacing) score -= 3f;
                // Paths that cut right past a thief or passer-by are a last resort.
                if (WalkerPathClearance(from, candidate) < personalSpace) score -= 2f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = candidate;
            }

            if (bestScore == float.MinValue)
            {
                walking = false;
                restUntil = Time.time + yieldPauseSeconds;
                return;
            }

            goal = best;
            walking = true;
        }

        // Off the floor over the closest edge whose way out keeps pathClearance from the players (the
        // closest of any when none does), a little past where it is out of sight.
        private void StartExit()
        {
            Vector3 from = transform.position;
            Vector3 best = from;
            float bestScore = float.MinValue;
            for (int side = 0; side < 4; side++)
            {
                for (int step = -1; step <= 1; step++)
                {
                    Vector3 candidate = ExitSpot(from, side, step * 1.2f);
                    float score = -FlatDistance(from, candidate) + UnityEngine.Random.Range(0f, 0.3f);
                    if (PathClearance(from, candidate) < pathClearance) score -= 100f;
                    if (score <= bestScore) continue;
                    bestScore = score;
                    best = candidate;
                }
            }

            goal = best;
            walking = true;
        }

        // A spot just past one side of the floor (0 left, 1 right, 2 near, 3 far), across from
        // point and moved along that side by shift.
        private Vector3 ExitSpot(Vector3 point, int side, float shift)
        {
            float past = goneOutside + 0.3f;
            float x = Mathf.Clamp(point.x + shift, areaMin.x, areaMax.x);
            float z = Mathf.Clamp(point.z + shift, areaMin.y, areaMax.y);
            switch (side)
            {
                case 0: return new Vector3(areaMin.x - past, point.y, z);
                case 1: return new Vector3(areaMax.x + past, point.y, z);
                case 2: return new Vector3(x, point.y, areaMin.y - past);
                default: return new Vector3(x, point.y, areaMax.y + past);
            }
        }

        // How far past the floor's edges it stands (0 or less while on the floor).
        private float OutsideBy()
        {
            Vector3 position = transform.position;
            return Mathf.Max(
                Mathf.Max(areaMin.x - position.x, position.x - areaMax.x),
                Mathf.Max(areaMin.y - position.z, position.z - areaMax.y));
        }

        // Fills walkerPositions with the thieves and other passers-by in sight.
        private void GatherWalkers()
        {
            walkerPositions.Clear();
            if (others == null) return;

            foreach (IFloorWalkers group in others)
            {
                if (group != null) group.AddPositions(walkerPositions, transform);
            }
        }

        // Flat distance from point to the closest thief or other passer-by; very far when there are none.
        private float NearestWalker(Vector3 point)
        {
            float nearest = float.MaxValue;
            foreach (Vector3 other in walkerPositions)
            {
                nearest = Mathf.Min(nearest, FlatDistance(point, other));
            }

            return nearest;
        }

        // Flat distance from point to the closest floor edge.
        private float EdgeDistance(Vector3 point)
        {
            return Mathf.Min(
                Mathf.Min(point.x - areaMin.x, areaMax.x - point.x),
                Mathf.Min(point.z - areaMin.y, areaMax.y - point.z));
        }

        // The closest any player is to the straight path from a to b (flat).
        private float PathClearance(Vector3 a, Vector3 b)
        {
            float clearance = float.MaxValue;
            Vector2 start = new Vector2(a.x, a.z);
            Vector2 end = new Vector2(b.x, b.z);
            foreach (Vector3 player in playerPositions)
            {
                clearance = Mathf.Min(clearance, DistanceToSegment(new Vector2(player.x, player.z), start, end));
            }

            return clearance;
        }

        // The closest any thief or passer-by in walkerPositions is to the straight path from a to b,
        // leaving out ones right beside its start (it is walking away from those).
        private float WalkerPathClearance(Vector3 a, Vector3 b)
        {
            float clearance = float.MaxValue;
            Vector2 start = new Vector2(a.x, a.z);
            Vector2 end = new Vector2(b.x, b.z);
            foreach (Vector3 walker in walkerPositions)
            {
                Vector2 point = new Vector2(walker.x, walker.z);
                if (Vector2.Distance(point, start) < personalSpace) continue;
                clearance = Mathf.Min(clearance, DistanceToSegment(point, start, end));
            }

            return clearance;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float t = segment.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude) : 0f;
            return Vector2.Distance(point, start + segment * t);
        }

        // A player within yieldDistance in the half in front of where it is heading.
        private bool PlayerAhead(Vector3 heading)
        {
            if (players == null) return false;

            players.GetAll(playerPositions);
            Vector3 position = transform.position;
            foreach (Vector3 player in playerPositions)
            {
                Vector3 toPlayer = player - position;
                toPlayer.y = 0f;
                if (toPlayer.magnitude <= yieldDistance && Vector3.Dot(toPlayer.normalized, heading) > 0.2f) return true;
            }

            return false;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // The position kept at least margin inside the floor's edges.
        private Vector3 Clamped(Vector3 position, float margin)
        {
            position.x = Mathf.Clamp(position.x, areaMin.x + margin, areaMax.x - margin);
            position.z = Mathf.Clamp(position.z, areaMin.y + margin, areaMax.y - margin);
            return position;
        }

        private void Face(float dx)
        {
            if (body == null || Mathf.Abs(dx) < 0.02f) return;
            body.flipX = dx > 0f != artFacesRight;
        }
    }
}
