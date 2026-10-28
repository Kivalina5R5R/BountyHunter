using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The boss passing for a passer-by, used when a player closes in: it wraps itself in its cape,
    // the wrapped boss fades out as one of the passer-by kinds on the floor fades in (BossDisguise,
    // over changeSeconds), and it strolls like one for disguiseSeconds; then the passer-by fades back
    // into the wrapped boss and it throws the cape off at revealPace. A touch while it is still
    // wrapping up counts (it is plainly the boss, and the player was quick); once it has changed a
    // touch does not count (BossController has it warp away instead).
    public sealed class BossDisguising : IBossAction
    {
        private enum Step { Wrapping, ChangingIn, Strolling, ChangingOut, Revealing }

        private readonly List<NpcWalker> kinds = new List<NpcWalker>();
        private readonly BossBody body;
        private readonly BossMove cape;
        // The cape thrown off, finely cut; empty to use cape's revealFrames instead.
        private readonly BossMove reveal;
        private readonly NpcCrowd passersBy;
        private readonly BossRun run;
        private Step step;
        private float stepTime;
        private Vector3 strollTarget;
        private bool fleeing;
        private float lastThreatAt;

        public BossDisguising(BossBody body, BossMove cape, BossMove reveal, NpcCrowd passersBy, BossRun run)
        {
            this.body = body;
            this.cape = cape;
            this.reveal = reveal;
            this.passersBy = passersBy;
            this.run = run;
        }

        // There is a cape to wrap in and a passer-by kind to pass for.
        public bool CanDisguise => !cape.IsEmpty && PickKind() != null;
        // Not while it stands changing back and throwing the cape off, so nobody gains by waiting beside it.
        public bool Touchable => step != Step.ChangingOut && step != Step.Revealing;
        public bool CountsTouches => step == Step.Wrapping;
        public bool IsBlocked => step == Step.Strolling && fleeing && run.IsBlocked;

        public void Begin()
        {
            fleeing = false;
            lastThreatAt = float.MinValue;
            Enter(Step.Wrapping);
            body.Animator.Play(cape.Frames, cape.FramesPerSecond, body.Settings.WrapFrames, false);
        }

        public bool Tick()
        {
            stepTime += Time.deltaTime;
            switch (step)
            {
                case Step.Wrapping:
                    if (body.Animator.Done) PutOn();
                    return true;
                case Step.ChangingIn:
                    ChangeIn();
                    return true;
                case Step.Strolling:
                    Stroll();
                    if (stepTime >= body.Settings.DisguiseSeconds) ChangeBack();
                    return true;
                case Step.ChangingOut:
                    ChangeOut();
                    return true;
                default:
                    return !body.Animator.Done;
            }
        }

        public void Stop()
        {
            body.DropDisguise();
        }

        private void Enter(Step next)
        {
            step = next;
            stepTime = 0f;
        }

        private void PutOn()
        {
            NpcWalker kind = PickKind();
            if (kind == null)
            {
                Reveal();
                return;
            }

            Enter(Step.ChangingIn);
            body.Disguise.Wear(kind, body.Settings);
            body.Disguise.SetAlpha(0f);
            Debug.Log($"[BossController] Passing for a {kind.name}");
        }

        // The wrapped boss fades out as the passer-by fades in.
        private void ChangeIn()
        {
            float changed = Mathf.Clamp01(stepTime / body.Settings.ChangeSeconds);
            body.Disguise.SetAlpha(changed);
            body.SetAlpha(1f - changed);
            if (changed < 1f) return;

            body.Animator.Body.enabled = false;
            body.SetAlpha(1f);
            Enter(Step.Strolling);
            strollTarget = body.RandomSpot();
        }

        // Keeps its disguise while fleeing nearby players; only strolls while nobody threatens it.
        private void Stroll()
        {
            Vector3 position = body.Position;
            if (body.Players.TryGetWithin(position, body.Settings.FleeRadius, out _)) lastThreatAt = Time.time;
            bool threatened = Time.time - lastThreatAt < body.Settings.CalmSeconds;
            if (threatened)
            {
                if (!fleeing) run.Begin(0f, false);
                fleeing = true;
                run.Tick(false);
            }
            else
            {
                if (fleeing) strollTarget = body.RandomSpot();
                fleeing = false;
                if (BossBody.FlatDistance(position, strollTarget) < 0.3f) strollTarget = body.RandomSpot();
                Vector3 toward = strollTarget - position;
                toward.y = 0f;
                Vector3 velocity = (toward.normalized + body.PasserPush(position)).normalized * body.Settings.DisguiseSpeed;
                body.Step(velocity);
            }
            Vector3 moved = Time.deltaTime > 0f ? (body.Position - position) / Time.deltaTime : Vector3.zero;
            body.Disguise.Walk(moved);
        }

        // Stops and shows the wrapped boss (the reveal's first picture) under the passer-by, to fade into.
        private void ChangeBack()
        {
            Enter(Step.ChangingOut);
            PlayReveal();
            body.Animator.Paused = true;
            body.Animator.Body.enabled = true;
            body.SetAlpha(0f);
        }

        // The passer-by fades out as the wrapped boss fades in, then it throws the cape off.
        private void ChangeOut()
        {
            float changed = Mathf.Clamp01(stepTime / body.Settings.ChangeSeconds);
            body.Disguise.SetAlpha(1f - changed);
            body.SetAlpha(changed);
            if (changed >= 1f) Reveal();
        }

        private void Reveal()
        {
            body.DropDisguise();
            Enter(Step.Revealing);
            PlayReveal();
        }

        private void PlayReveal()
        {
            if (reveal.IsEmpty) body.Animator.Play(cape.Frames, cape.FramesPerSecond, body.Settings.RevealFrames, false);
            else body.Animator.Play(reveal.Frames, reveal.FramesPerSecond, reveal.All, false);
            body.Animator.Pace = body.Settings.RevealPace;
        }

        // A kind out on the floor now, so there is a real one to mix it up with; any kind otherwise.
        private NpcWalker PickKind()
        {
            kinds.Clear();
            if (passersBy == null) return null;

            foreach (NpcWalker kind in passersBy.KindsOut)
            {
                if (kind != null) kinds.Add(kind);
            }

            if (kinds.Count == 0 && passersBy.Kinds != null)
            {
                foreach (NpcWalker kind in passersBy.Kinds)
                {
                    if (kind != null) kinds.Add(kind);
                }
            }

            return kinds.Count > 0 ? kinds[Random.Range(0, kinds.Count)] : null;
        }
    }
}
