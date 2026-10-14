using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Picks a bush for a fleeing thief, and lists the bushes for a wandering one to lurk near.
    public interface IHidingSpots
    {
        IReadOnlyList<HidingBush> Bushes { get; }

        // Returns a free bush the thief reaches before every player, along a path that keeps away
        // from all players, or null when none is safe. excluded is skipped (the bush it last left).
        HidingBush FindHidingBush(ThiefController thief, IReadOnlyList<Vector3> playerPositions, HidingBush excluded);
    }
}
