using UnityEngine;

namespace BountyHunter.Thieves
{
    // The boss's smoke bomb, three ways in:
    // - Throw: it takes the bomb out, throws it down and the cloud covers it; the cloud sits there
    //   until vanishAtFrame.
    // - Warp: the same from the bomb going down (warpFromFrame); what a touch sets off.
    // - Arrive: straight to coming out of a cloud at a set spot (its entrance).
    // Thrown or warped, the cloud is left where it was and clears (originCloudFrames, then a fade)
    // before a new cloud puffs up at a spot at least warpMinDistance away, as far from the players as
    // it can find; there it shows in the cloud and steps out while a copy of the cloud stays over it
    // and thins away slowly. It cannot be touched all the while (so nobody gains by waiting for it).
    public sealed class BossSmokeBomb : IBossAction
    {
        private const int FarSpotTries = 40;
        // How much the spot's distance from where it was counts against its distance from the players.
        private const float FarFromHereWeight = 0.3f;

        private readonly BossBody body;
        private readonly BossMove smoke;
        // Out of the cloud elsewhere (or arriving): the second half.
        private bool teleported;
        // Gone from where it was, waiting for the cloud left there to clear until arriveAt.
        private bool betweenClouds;
        private float arriveAt;
        private bool cloudLingering;
        private Vector3 warpedFrom;

        public BossSmokeBomb(BossBody body, BossMove smoke)
        {
            this.body = body;
            this.smoke = smoke;
        }

        public bool IsEmpty => smoke.IsEmpty;
        public bool CountsTouches => true;

        public bool Touchable => false;

        public void Throw()
        {
            Begin(false);
            body.Animator.Play(smoke.Frames, smoke.FramesPerSecond, body.Settings.SmokeFrames, false);
        }

        public void Warp()
        {
            body.DropDisguise();
            Begin(false);
            body.Animator.Play(smoke.Frames, smoke.FramesPerSecond, new Vector2Int(body.Settings.WarpFromFrame, body.Settings.SmokeFrames.y), false);
        }

        public void ArriveAt(Vector3 position)
        {
            body.Position = position;
            Begin(true);
            body.Animator.Body.enabled = true;
            PlayArrival();
        }

        public bool Tick()
        {
            BossSettings settings = body.Settings;
            if (!teleported && !betweenClouds && body.Animator.Frame >= settings.VanishAtFrame) LeaveCloudBehind();

            if (betweenClouds && Time.time >= arriveAt)
            {
                betweenClouds = false;
                teleported = true;
                body.Position = FarSpot(warpedFrom);
                body.Animator.Body.enabled = true;
                PlayArrival();
            }

            if (teleported && !cloudLingering && body.Animator.Frame >= settings.SteppedOutFrame)
            {
                cloudLingering = true;
                Vector2Int cloud = new Vector2Int(settings.LingeringCloudFrame, settings.LingeringCloudFrame);
                SpriteFadeOut.LeaveBehind(body.Animator.Body, smoke.Frames, smoke.FramesPerSecond, cloud, settings.LingeringCloudFadeSeconds, body.Parent, 1);
            }

            return !(teleported && body.Animator.Done);
        }

        public void Stop()
        {
            betweenClouds = false;
        }

        private void Begin(bool arrived)
        {
            teleported = arrived;
            betweenClouds = false;
            cloudLingering = false;
        }

        // The cloud stays where it was and clears; it is gone from sight until then.
        private void LeaveCloudBehind()
        {
            BossSettings settings = body.Settings;
            betweenClouds = true;
            warpedFrom = body.Position;
            Vector2Int cloud = settings.OriginCloudFrames;
            SpriteFadeOut.LeaveBehind(body.Animator.Body, smoke.Frames, smoke.FramesPerSecond, cloud, settings.OriginCloudFadeSeconds, body.Parent);
            arriveAt = Time.time + Mathf.Max(0, cloud.y - cloud.x + 1) / smoke.FramesPerSecond + settings.OriginCloudFadeSeconds;
            body.Animator.Body.enabled = false;
            body.Animator.Paused = true;
        }

        private void PlayArrival()
        {
            body.Animator.Play(smoke.Frames, smoke.FramesPerSecond, new Vector2Int(body.Settings.ArriveFrame, body.Settings.SmokeFrames.y), false);
        }

        // Of a few random spots at least warpMinDistance from where it was (when the floor has any),
        // the one farthest from the players, a little further from where it was breaking ties.
        private Vector3 FarSpot(Vector3 from)
        {
            Vector3 best = body.RandomSpot();
            float bestScore = float.MinValue;
            for (int i = 0; i < FarSpotTries; i++)
            {
                Vector3 spot = body.RandomSpot();
                float fromHere = BossBody.FlatDistance(spot, from);
                float fromPlayers = body.Players.TryGetNearest(spot, out _, out float nearest) ? nearest : 10f;
                float score = fromPlayers + fromHere * FarFromHereWeight - (fromHere < body.Settings.WarpMinDistance ? 100f : 0f);
                if (score <= bestScore) continue;
                best = spot;
                bestScore = score;
            }

            return best;
        }
    }
}
