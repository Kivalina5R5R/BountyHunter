using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Lets a bush with a thief inside creep away from an approaching player. It is slower than a
    // walking player, and slower still while someone stands on it, so a player can follow it and
    // keep standing on it long enough to flush the thief. It stays on the floor and never walks into
    // another bush. Added by ThiefSpawner.
    [RequireComponent(typeof(HidingBush))]
    public sealed class BushWalker : MonoBehaviour
    {
        // A player closer than this (from the bush centre) makes the bush creep away.
        [SerializeField, Min(0.1f)] private float noticeRadius = 1.8f;
        [SerializeField, Min(0f)] private float creepSpeed = 0.35f;
        // Speed while a player is standing on it; low enough to keep up with on foot.
        [SerializeField, Min(0f)] private float stoodOnSpeed = 0.15f;
        [SerializeField, Min(0.1f)] private float acceleration = 1.2f;
        // Extra room kept between this bush's leaves and a neighbour's.
        [SerializeField, Min(0f)] private float bushGap = 0.1f;

        private HidingBush bush;
        private IPlayerLocator players;
        private IReadOnlyList<HidingBush> neighbours;
        private Vector2 areaMin;
        private Vector2 areaMax;
        private Vector3 velocity;
        private Vector3 lastAway = Vector3.right;

        // area is the floor rectangle the bush centre must stay in (Vector2.y is world Z).
        public void Initialize(IPlayerLocator playerLocator, IReadOnlyList<HidingBush> allBushes, Vector2 moveAreaMin, Vector2 moveAreaMax)
        {
            bush = GetComponent<HidingBush>();
            players = playerLocator;
            neighbours = allBushes;
            areaMin = moveAreaMin;
            areaMax = moveAreaMax;
        }

        public bool IsMoving => velocity.sqrMagnitude > 0.0001f;

        private void Update()
        {
            if (bush == null || players == null) return;

            Vector3 desired = DesiredVelocity();
            velocity = Vector3.MoveTowards(velocity, desired, acceleration * Time.deltaTime);
            if (!IsMoving) return;

            Vector3 next = transform.position + velocity * Time.deltaTime;
            next.x = Mathf.Clamp(next.x, areaMin.x, areaMax.x);
            next.z = Mathf.Clamp(next.z, areaMin.y, areaMax.y);
            if (Blocked(next))
            {
                velocity = Vector3.zero;
                return;
            }

            transform.position = next;
        }

        private Vector3 DesiredVelocity()
        {
            if (bush.Occupant == null) return Vector3.zero;
            if (!players.TryGetNearest(bush.Center, out Vector3 player, out float distance) || distance > noticeRadius) return Vector3.zero;

            Vector3 away = bush.Center - player;
            away.y = 0f;
            // A player right on the centre gives no direction; keep creeping the way it was going.
            if (away.sqrMagnitude > 0.0025f) lastAway = away.normalized;

            float speed = bush.PlayerIsStandingOn ? stoodOnSpeed : creepSpeed;
            return SlideAlongEdges(lastAway) * speed;
        }

        // At the floor edge the blocked part of the direction is dropped, so it slides along the edge.
        private Vector3 SlideAlongEdges(Vector3 direction)
        {
            Vector3 position = transform.position;
            if ((position.x <= areaMin.x && direction.x < 0f) || (position.x >= areaMax.x && direction.x > 0f)) direction.x = 0f;
            if ((position.z <= areaMin.y && direction.z < 0f) || (position.z >= areaMax.y && direction.z > 0f)) direction.z = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        }

        // True when the step would push this bush's leaves into a neighbour that it is not already moving away from.
        private bool Blocked(Vector3 next)
        {
            if (neighbours == null) return false;

            foreach (HidingBush other in neighbours)
            {
                if (other == null || other == bush) continue;

                float minimum = (bush.Radius + other.Radius) * 1.3f + bushGap;
                float nextDistance = FlatDistance(next, other.Center);
                if (nextDistance < minimum && nextDistance < FlatDistance(transform.position, other.Center)) return true;
            }

            return false;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return Mathf.Sqrt(x * x + z * z);
        }
    }
}
