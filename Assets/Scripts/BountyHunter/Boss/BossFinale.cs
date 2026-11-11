using System.Collections;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The whole boss finale (MapSwitcher's finale): phase 1, the vault (BossVault), then phase 2, the
    // chase (BossChase). Between them the game freezes and phaseChangeClip plays over the wall (the
    // boss making off with the diamond), the screen fading to black over its last moments and back up
    // on the chase. After the chase there are two endings, both loaded while the chase runs: the boss
    // caught (the Good End) plays goodEndClip, the police dogs swarming him, and the boss not caught
    // (the Bad End) plays escapeClip, the boss getting away with the diamond; each plays the same way
    // and leaves the screen dark for the result. A clip that
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
        private readonly TransitionClip phaseChangeClip = new TransitionClip("BossSteal", 19, 225, (99, 113), (222, 224));
        private readonly TransitionClip escapeClip = new TransitionClip("BossEscape", 37, 434, (163, 182), (286, 302));
        // Its white flashes were already taken out of the video it was made from.
        private readonly TransitionClip goodEndClip = new TransitionClip("GoodEnd", 28, 334);

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
            if (playing == chase)
            {
                LoadEndings();
            }

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
                LoadEndings();
                Debug.Log("[BossFinale] Phase 2: the chase", this);
                if (curtain != null) yield return curtain.Open(fadeInSeconds);
            }

            if (skipping) yield break;

            yield return chase.Run();
            if (skipping) yield break;

            // Only the ending that plays stays in memory.
            TransitionClip ending = chase.Caught ? goodEndClip : escapeClip;
            (chase.Caught ? escapeClip : goodEndClip).Unload();
            yield return PlayToDark(ending);
        }

        public override void End()
        {
            StopAllCoroutines();
            if (clipPlayer != null) Time.timeScale = 1f;
            RemoveClipPlayer();
            phaseChangeClip.Unload();
            escapeClip.Unload();
            goodEndClip.Unload();
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

        // Both endings start loading while the chase runs, so either plays at once when it ends.
        private void LoadEndings()
        {
            StartCoroutine(escapeClip.Load());
            StartCoroutine(goodEndClip.Load());
        }

        private void RemoveClipPlayer()
        {
            if (clipPlayer != null) Destroy(clipPlayer.gameObject);
            clipPlayer = null;
        }
    }
}
