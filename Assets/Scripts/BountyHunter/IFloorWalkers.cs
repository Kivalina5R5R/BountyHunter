using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Things walking about on the floor in plain sight (thieves out of hiding, passers-by), so the
    // others can keep from walking over them. Positions are world space; only X and Z matter.
    public interface IFloorWalkers
    {
        // Adds the position of every one in sight to positions, leaving out except (the asker).
        void AddPositions(List<Vector3> positions, Transform except);

        // Adds the spots that are taken without anyone in sight there yet: where one is about to come
        // onto the floor, or is hiding. Nothing else should spawn on them.
        void AddTakenSpots(List<Vector3> spots);
    }
}
