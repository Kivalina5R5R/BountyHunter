using System;
using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // A passer-by on the floor (the sloth officer): it strolls from spot to spot at its own slow pace
    // and stops for a while now and then. It neither runs from the players nor hides, but it never
    // walks into one either: it only sets off along paths that keep clear of them, and stops and
    // picks another way when one steps in front of it. So a bump is always the player walking into
    // it, an innocent officer grabbed by mistake: it raises Bumped once and
    // NpcCrowd takes it off the floor for a while. It faces the way it walks (mirrored only); the
    // frames are played by ThiefFrameAnimation on its body.
    public sealed class NpcWalker : MonoBehaviour
    {
        [SerializeField] private string displayName = "Sloth";
        [SerializeField] private SpriteRenderer body;
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
        [SerializeField, Min(0f)] private float pathClearance = 1f;
        // A player this close ahead of it while it walks makes it stop and choose another way.
        [SerializeField, Min(0f)] private float yieldDistance = 0.9f;
        // How long it stands still after giving way, and when no clear path is found.
        [SerializeField, Min(0f)] private float yieldPauseSeconds = 0.6f;

        [Header("Bumping")]
        // A player whose centre comes this close has bumped into it, from the moment it walks in
        // (NpcCrowd only sends it in where no player is near).
        [SerializeField, Min(0.1f)] private float bumpDistance = 0.45f;

        private readonly List<Vector3> playerPositions = new List<Vector3>();
        private IPlayerLocator players;
        private Vector2 areaMin;
        private Vector2 areaMax;
        private Vector3 goal;
        private Vector3 velocity;
        private float restUntil;
        private bool walking;
        private bool entering;
        private bool bumped;

        public string DisplayName => displayName;

        public event Action<NpcWalker> Bumped;

        // Gives it the players to watch for and the floor area to stroll in (x and world z).
        public void Initialize(IPlayerLocator playerLocator, Vector2 moveAreaMin, Vector2 moveAreaMax)
        {
            players = playerLocator;
            areaMin = moveAreaMin;
            areaMax = moveAreaMax;
        }

        // Starts outside the floor and walks in toward inside. As soon as it is enterDepth past the edge
        // it is free, like a thief, and picks its own way at once. It can be bumped all along.
        public void WalkIn(Vector3 outside, Vector3 inside)
        {
            transform.position = outside;
            goal = inside;
            velocity = Vector3.zero;
            entering = true;
            walking = true;
            bumped = false;
        }

        private void Update()
        {
            if (bumped) return;

            if (players != null &&
                players.TryGetNearest(transform.position, out _, out float distance) && distance <= bumpDistance)
            {
                bumped = true;
                Bumped?.Invoke(this);
                return;
            }

            if (walking) Walk();
            else if (Time.time >= restUntil) StartTrip();
            else Slow();
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

            // Eases off over the last stretch instead of stopping dead.
            float speed = walkSpeed * Mathf.Clamp01(toGoal.magnitude / 0.3f + 0.3f);
            Steer(toGoal.normalized * speed);
        }

        private void Slow()
        {
            Steer(Vector3.zero);
        }

        private void Steer(Vector3 wanted)
        {
            velocity = Vector3.MoveTowards(velocity, wanted, acceleration * Time.deltaTime);
            Vector3 next = transform.position + velocity * Time.deltaTime;
            // Never off the floor once in (its trips aim further in, edgeMargin).
            if (!entering) next = Clamped(next, 0f);
            transform.position = next;
            Face(velocity.x);
        }

        // Somewhere tripDistance away inside the floor whose path keeps clearest of the players;
        // when none keeps pathClearance away it waits a moment and tries again.
        private void StartTrip()
        {
            if (players != null) players.GetAll(playerPositions);
            else playerPositions.Clear();

            Vector3 from = transform.position;
            Vector3 best = from;
            float bestClearance = float.MinValue;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
                float length = UnityEngine.Random.Range(tripDistance.x, tripDistance.y);
                Vector3 candidate = Clamped(from + new Vector3(direction.x, 0f, direction.y) * length, edgeMargin);
                if (FlatDistance(from, candidate) < tripDistance.x * 0.5f) continue;

                float clearance = PathClearance(from, candidate);
                if (clearance <= bestClearance) continue;
                bestClearance = clearance;
                best = candidate;
            }

            if (bestClearance < pathClearance)
            {
                restUntil = Time.time + yieldPauseSeconds;
                return;
            }

            goal = best;
            walking = true;
        }

        // The closest any player is to the straight path from a to b (flat).
        private float PathClearance(Vector3 a, Vector3 b)
        {
            float clearance = float.MaxValue;
            Vector2 start = new Vector2(a.x, a.z);
            Vector2 end = new Vector2(b.x, b.z);
            Vector2 path = end - start;
            foreach (Vector3 player in playerPositions)
            {
                Vector2 point = new Vector2(player.x, player.z);
                float t = path.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - start, path) / path.sqrMagnitude) : 0f;
                clearance = Mathf.Min(clearance, Vector2.Distance(point, start + path * t));
            }

            return clearance;
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
