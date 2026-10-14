using System;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // A bush one thief can hide in. When a player stands on it long enough, the thief inside is
    // flushed out. Added at runtime by ThiefSpawner to every bush BushScatter places.
    // UWB positions jitter, so standing is forgiving: once on the bush a player counts as still on
    // it a little past the edge, and a brief slip off keeps the time already stood.
    public sealed class HidingBush : MonoBehaviour
    {
        // Extra distance past the leaf edge that still counts once a player is standing on the bush.
        private const float StayOnMargin = 0.18f;
        // A slip off shorter than this keeps the stand time; after it, the time drains away.
        private const float SlipGraceSeconds = 0.6f;
        private const float DrainRate = 2f;

        private IPlayerLocator players;
        private float radius;
        private float flushSeconds;
        private float standSeconds;
        private float offSeconds;
        private bool playerOnBush;

        public ThiefController Occupant { get; private set; }
        public ThiefController ReservedBy { get; private set; }
        public float Radius => radius;
        public Vector3 Center => transform.position;

        // 0 while a player is off the bush, rising to 1 when the stand time flushes the thief.
        public float FlushProgress => flushSeconds > 0f ? Mathf.Clamp01(standSeconds / flushSeconds) : 0f;

        // Raised when a thief jumps in; the direction points from the bush centre to the side it came from.
        public event Action<HidingBush, Vector3> ThiefEntered;

        // Raised when a thief comes out; the direction points from the bush centre to the side it runs to.
        public event Action<HidingBush, Vector3> ThiefExited;

        public bool PlayerIsStandingOn => playerOnBush;

        // radius is the area of leaves a player must stand inside; flushSeconds is how long they must stay.
        public void Initialize(IPlayerLocator playerLocator, float leafRadius, float secondsToFlush)
        {
            players = playerLocator;
            radius = leafRadius;
            flushSeconds = secondsToFlush;
        }

        // Free for this thief: nobody inside, not claimed by another thief, and no player on it.
        public bool IsFreeFor(ThiefController thief)
        {
            return Occupant == null && (ReservedBy == null || ReservedBy == thief) && !HasPlayerOn(out _);
        }

        public bool HasPlayerOn(out Vector3 playerPosition)
        {
            playerPosition = default;
            return players != null && players.TryGetWithin(Center, radius, out playerPosition);
        }

        public void Reserve(ThiefController thief)
        {
            ReservedBy = thief;
        }

        public void CancelReservation(ThiefController thief)
        {
            if (ReservedBy == thief) ReservedBy = null;
        }

        public void Enter(ThiefController thief, Vector3 fromSide)
        {
            Occupant = thief;
            ReservedBy = null;
            standSeconds = 0f;
            offSeconds = 0f;
            playerOnBush = false;
            ThiefEntered?.Invoke(this, fromSide);
        }

        // The thief comes out toward a side; plays the bush's exit reaction.
        public void Exit(ThiefController thief, Vector3 towardSide)
        {
            Leave(thief);
            ThiefExited?.Invoke(this, towardSide);
        }

        public void Leave(ThiefController thief)
        {
            if (Occupant == thief) Occupant = null;
            CancelReservation(thief);
            standSeconds = 0f;
            playerOnBush = false;
        }

        private void Update()
        {
            if (Occupant == null) return;

            float reach = playerOnBush ? radius + StayOnMargin : radius;
            Vector3 playerPosition = default;
            bool onBush = players != null && players.TryGetWithin(Center, reach, out playerPosition);
            if (!onBush)
            {
                offSeconds += Time.deltaTime;
                if (offSeconds < SlipGraceSeconds) return;

                if (playerOnBush) Debug.Log($"[HidingBush] {name}: player stepped off after {standSeconds:0.0}s, {Occupant.name} stays hidden", this);
                playerOnBush = false;
                standSeconds = Mathf.Max(0f, standSeconds - Time.deltaTime * DrainRate);
                return;
            }

            offSeconds = 0f;
            if (!playerOnBush) Debug.Log($"[HidingBush] {name}: player is standing on the bush where {Occupant.name} hides ({flushSeconds:0.#}s to flush)", this);
            playerOnBush = true;
            standSeconds += Time.deltaTime;
            if (standSeconds < flushSeconds) return;

            ThiefController flushed = Occupant;
            Debug.Log($"[HidingBush] {name}: player stood {flushSeconds:0.#}s, flushing {flushed.name} out", this);
            // The thief picks its escape and leaves through Exit, so the leaves fly its way.
            flushed.FlushOut(playerPosition);
        }

        // Called before the bush is removed for a new layout: whoever hides here has to come out.
        // Not done from OnDestroy, which also runs when the scene closes and must not spawn effects.
        public void EvictOccupant()
        {
            if (Occupant == null) return;

            ThiefController occupant = Occupant;
            occupant.FlushOut(Center);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = Occupant != null ? Color.red : Color.green;
            Gizmos.DrawWireSphere(Center, radius);
        }
#endif
    }
}
