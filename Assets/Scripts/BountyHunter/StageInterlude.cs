using System.Collections;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Something played between two stages (a mini game such as the suspect lineup). MapSwitcher runs
    // it once the stage's clock is out: Begin while the screen is dark, Run with the screen up until
    // it is done, End while the screen is dark again, then the next map starts.
    public abstract class StageInterlude : MonoBehaviour
    {
        // Played over the wall as the game goes into this mini game (its cut-in) and as it leaves it
        // for the next map, instead of the fade to black (the floor still fades); leave empty to fade.
        [SerializeField] private WallTransition entryTransition;
        [SerializeField] private WallTransition exitTransition;

        public WallTransition EntryTransition => entryTransition != null && entryTransition.IsSetUp ? entryTransition : null;
        public WallTransition ExitTransition => exitTransition != null && exitTransition.IsSetUp ? exitTransition : null;

        // Its panel on the wall, for a way out that wipes it off while the next map shows behind it;
        // null when it has none.
        public virtual RectTransform WallPanel => null;

        // Called behind the curtain before Begin when background preparation is still running.
        public virtual IEnumerator Prepare() { yield break; }

        // Sets it up on the floor and the wall, out of sight behind the fade curtain.
        public abstract void Begin();

        // Plays it to the end.
        public abstract IEnumerator Run();

        // Takes it off the floor and the wall again, with the stage's own pieces already back on (the
        // score tags can take what was won).
        public abstract void End();

        // Cuts Run short, all of it.
        public abstract void Skip();

        // N, for trying the game out: on to whatever comes next. All of Run unless it has steps of its own.
        public virtual void SkipStep() => Skip();
    }
}
