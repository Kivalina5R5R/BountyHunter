using System.Collections;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Something that covers the wall between two screens instead of the fade to black: Close until
    // the wall is covered (what is behind it can then change), Open to show the wall again. Runs on
    // unscaled time; the floor fades dark under it meanwhile (MapSwitcher, IntroSequence). One that
    // wipes instead (WipesOver) shows the new screen straight away under the old one and clears the
    // old one off the wall bit by bit (Wipe).
    public abstract class WallTransition : MonoBehaviour
    {
        // False when its prefab is missing pieces; the caller then fades instead.
        public abstract bool IsSetUp { get; }

        // True when it plays as a wipe over the leaving screen rather than covering the wall.
        public virtual bool WipesOver => false;

        public abstract IEnumerator Close();

        public abstract IEnumerator Open();

        // Waits out the least time the wall stays covered, so the floor can start showing again
        // exactly as Open starts; Open waits for it too when not called first.
        public virtual IEnumerator Hold()
        {
            yield break;
        }

        // How long Open takes once the hold is over, so the floor can come back in step with it.
        public virtual float OpenSeconds => 0f;

        // How long Wipe takes, so the floor can fade in step with it.
        public virtual float WipeSeconds => 0f;

        // Clears leaving (the old screen's panel, drawn over the new one) off the wall; the caller
        // switches it off afterwards.
        public virtual IEnumerator Wipe(RectTransform leaving)
        {
            yield break;
        }
    }
}
