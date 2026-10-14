using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Where the players are on the floor. Thieves and bushes ask this instead of knowing how
    // players are spawned or tracked. Positions are world space; only X and Z are compared.
    public interface IPlayerLocator
    {
        // Finds the closest active player; returns false when no player is on the floor.
        bool TryGetNearest(Vector3 from, out Vector3 position, out float distance);

        // Finds any active player within the radius of the centre.
        bool TryGetWithin(Vector3 center, float radius, out Vector3 position);

        // Fills positions with every active player (clears it first).
        void GetAll(List<Vector3> positions);
    }
}
