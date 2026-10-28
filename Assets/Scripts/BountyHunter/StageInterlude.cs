using System.Collections;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Something played between two stages (a mini game such as the suspect lineup). MapSwitcher runs
    // it once the stage's clock is out: Begin while the screen is dark, Run with the screen up until
    // it is done, End while the screen is dark again, then the next map starts.
    public abstract class StageInterlude : MonoBehaviour
    {
        // Called behind the curtain before Begin when background preparation is still running.
        public virtual IEnumerator Prepare() { yield break; }

        // Sets it up on the floor and the wall, out of sight behind the fade curtain.
        public abstract void Begin();

        // Plays it to the end.
        public abstract IEnumerator Run();

        // Takes it off the floor and the wall again, with the stage's own pieces already back on (the
        // score tags can take what was won).
        public abstract void End();

        // Cuts Run short (N, for trying the game out).
        public abstract void Skip();
    }
}
