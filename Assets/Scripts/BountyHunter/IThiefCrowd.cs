using System.Collections.Generic;

namespace BountyHunter.Thieves
{
    // The thieves on the floor, so each one can keep its distance from the others.
    public interface IThiefCrowd
    {
        IReadOnlyList<ThiefController> Thieves { get; }
    }
}
