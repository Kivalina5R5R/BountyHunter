using System.Collections;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The whole boss finale (MapSwitcher's finale): phase 1, the vault (BossVault), then phase 2, the
    // chase (BossChase), with the screen fading to black and back between them (until the cut-scene
    // for it is made). Without a vault it is just the chase. Skip cuts the whole finale short.
    public sealed class BossFinale : StageInterlude
    {
        [SerializeField] private BossVault vault;
        [SerializeField] private BossChase chase;
        [SerializeField] private FadeCurtain curtain;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.6f;
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.6f;

        private StageInterlude playing;
        private bool skipping;

        public override RectTransform WallPanel => chase != null ? chase.WallPanel : null;

        public override IEnumerator Prepare()
        {
            if (vault != null) yield return vault.Prepare();
            if (chase != null) yield return chase.Prepare();
        }

        public override void Begin()
        {
            skipping = false;
            playing = vault != null ? vault : chase;
            if (playing != null) playing.Begin();
        }

        public override IEnumerator Run()
        {
            if (playing == null) yield break;

            if (playing == vault)
            {
                yield return vault.Run();
                if (skipping || chase == null) yield break;

                if (curtain != null) yield return curtain.Close(fadeOutSeconds);
                vault.End();
                playing = chase;
                chase.Begin();
                Debug.Log("[BossFinale] Phase 2: the chase", this);
                if (curtain != null) yield return curtain.Open(fadeInSeconds);
            }

            if (!skipping) yield return chase.Run();
        }

        public override void End()
        {
            if (playing != null) playing.End();
            playing = null;
        }

        public override void Skip()
        {
            skipping = true;
            if (playing != null) playing.Skip();
        }
    }
}
