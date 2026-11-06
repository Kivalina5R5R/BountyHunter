using System.Collections;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The whole boss finale (MapSwitcher's finale): phase 1, the vault (BossVault), then phase 2, the
    // chase (BossChase). Between them the game freezes and phaseChangeClip plays over the wall (the
    // boss making off with the diamond), the screen fading to black over its last moments and back up
    // on the chase. After the chase escapeClip plays the same way, whoever won (in the end the boss
    // always gets away with the diamond), and leaves the screen dark for the result. A clip that
    // cannot be loaded is left out. Without a vault it is just the chase. Skip cuts the whole
    // finale short; SkipStep (N) only moves on a step, so the clips and the chase are still seen:
    // the vault goes to its clip, the chase to its own, and a clip playing to what follows it.
    public sealed class BossFinale : StageInterlude
    {
        [SerializeField] private BossVault vault;
        [SerializeField] private BossChase chase;
        [SerializeField] private FadeCurtain curtain;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.6f;
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.8f;
        private readonly TransitionClip phaseChangeClip = new TransitionClip("BossSteal", 19, 225);
        private readonly TransitionClip escapeClip = new TransitionClip("BossEscape", 37, 434);

        private StageInterlude playing;
        private TransitionClipPlayer clipPlayer;
        private bool skipping;

        public override RectTransform WallPanel => chase != null ? chase.WallPanel : null;

        public override IEnumerator Prepare()
        {
            if (vault != null)
            {
                yield return vault.Prepare();
                yield return phaseChangeClip.Load();
            }

            if (chase != null) yield return chase.Prepare();
        }

        public override void Begin()
        {
            skipping = false;
            playing = vault != null ? vault : chase;
            if (playing == chase) StartCoroutine(escapeClip.Load());
            if (playing != null) playing.Begin();
        }

        public override IEnumerator Run()
        {
            if (playing == null) yield break;

            if (playing == vault)
            {
                yield return vault.Run();
                if (skipping || chase == null) yield break;

                yield return PlayToDark(phaseChangeClip);
                if (skipping) yield break;

                vault.End();
                playing = chase;
                chase.Begin();
                StartCoroutine(escapeClip.Load());
                Debug.Log("[BossFinale] Phase 2: the chase", this);
                if (curtain != null) yield return curtain.Open(fadeInSeconds);
            }

            if (skipping) yield break;

            yield return chase.Run();
            if (!skipping) yield return PlayToDark(escapeClip);
        }

        public override void End()
        {
            StopAllCoroutines();
            if (clipPlayer != null) Time.timeScale = 1f;
            RemoveClipPlayer();
            phaseChangeClip.Unload();
            escapeClip.Unload();
            if (playing != null) playing.End();
            playing = null;
        }

        public override void Skip()
        {
            skipping = true;
            if (clipPlayer != null) clipPlayer.Stop();
            if (playing != null) playing.Skip();
        }

        public override void SkipStep()
        {
            if (clipPlayer != null) clipPlayer.Stop();
            else if (playing != null) playing.Skip();
        }

        // With the game frozen, plays clip over the wall (waiting for it first if it is still loading)
        // and fades to black over its end; without the clip, just the fade. The picture is gone and
        // time runs again by the end, with the screen still dark.
        private IEnumerator PlayToDark(TransitionClip clip)
        {
            while (clip.IsLoading && !skipping) yield return null;

            Coroutine fade = null;
            if (clip.IsLoaded && curtain != null)
            {
                Time.timeScale = 0f;
                clipPlayer = TransitionClipPlayer.Create((RectTransform)curtain.transform.parent, clip.Aspect);
                yield return clipPlayer.Play(clip, fadeOutSeconds, () => fade = StartCoroutine(curtain.Close(fadeOutSeconds)));
            }

            if (fade != null) yield return fade;
            else if (curtain != null && !skipping) yield return curtain.Close(fadeOutSeconds);

            RemoveClipPlayer();
            clip.Unload();
            Time.timeScale = 1f;
        }

        private void RemoveClipPlayer()
        {
            if (clipPlayer != null) Destroy(clipPlayer.gameObject);
            clipPlayer = null;
        }
    }
}
