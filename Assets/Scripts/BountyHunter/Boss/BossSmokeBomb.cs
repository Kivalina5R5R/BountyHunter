using System;
using UnityEngine;
using Random = UnityEngine.Random;

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
        private const int FloorSamplesPerAxis = 9;
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
        private float playerClearance;
        private bool catchableWindup;
        private readonly Func<bool> tryHide;
        private bool allowHiding;
        public bool Hid { get; private set; }

        public BossSmokeBomb(BossBody body, BossMove smoke, Func<bool> tryHide)
        {
            this.body = body;
            this.smoke = smoke;
            this.tryHide = tryHide;
        }

        public bool IsEmpty => smoke.IsEmpty;
        public bool CountsTouches => true;

        public bool Touchable => catchableWindup && !teleported && !betweenClouds && body.Animator.Frame < body.Settings.VanishAtFrame;

        // A trapped boss starts throwing immediately but can be caught before the cloud covers it.
        public void Throw(bool catchableWindup = false)
        {
            Begin(false);
            this.catchableWindup = catchableWindup;
            playerClearance = body.Settings.FleeRadius;
            // An emergency escape skips taking the bomb out and starts at the throw itself.
            Vector2Int frames = body.Settings.SmokeFrames;
            if (catchableWindup) frames.x = body.Settings.WarpFromFrame;
            body.Animator.Play(smoke.Frames, smoke.FramesPerSecond, frames, false);
        }

        // Skill preparation can require extra room from every player at the destination.
        public void Warp(float playerClearance = 0f, bool allowHiding = true)
        {
            body.DropDisguise();
            Begin(false);
            this.playerClearance = playerClearance;
            this.allowHiding = allowHiding;
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
                bool far = TryFindFarSpot(warpedFrom, out Vector3 destination);
                // Keep the usual split when far ground exists; otherwise prefer a safe hideout.
                if (allowHiding && (!far || Random.value < 0.3f) && tryHide != null && tryHide())
                {
                    Hid = true;
                    return false;
                }
                body.Position = destination;
                body.Animator.Body.enabled = true;
                PlayArrival();
            }

            if (teleported && !cloudLingering && body.Animator.Frame >= settings.SteppedOutFrame)
            {
                cloudLingering = true;
                Vector2Int cloud = new Vector2Int(settings.LingeringCloudFrame, settings.LingeringCloudFrame);
                SpriteFadeOut.LeaveBehind(body.Animator.Body, smoke.Frames, smoke.FramesPerSecond, cloud, settings.LingeringCloudFadeSeconds, body.Parent, 1, true);
            }

            return !(teleported && body.Animator.Done);
        }

        public void Stop()
        {
            betweenClouds = false;
        }

        private void Begin(bool arrived)
        {
            Hid = false;
            allowHiding = !arrived;
            catchableWindup = false;
            playerClearance = 0f;
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
            SpriteFadeOut.LeaveBehind(body.Animator.Body, smoke.Frames, smoke.FramesPerSecond, cloud, settings.OriginCloudFadeSeconds, body.Parent, fadeWhilePlaying: true);
            arriveAt = Time.time + Mathf.Max(0, cloud.y - cloud.x + 1) / smoke.FramesPerSecond + settings.OriginCloudFadeSeconds;
            body.Animator.Body.enabled = false;
            body.Animator.Paused = true;
        }

        private void PlayArrival()
        {
            body.Animator.Play(smoke.Frames, smoke.FramesPerSecond, new Vector2Int(body.Settings.ArriveFrame, body.Settings.SmokeFrames.y), false);
        }

        // Include the perimeter so random interior samples cannot miss the open far side.
        // Even when no far spot qualifies, return the safest sampled ground as a last resort.
        private bool TryFindFarSpot(Vector3 from, out Vector3 best)
        {
            best = from;
            float bestScore = float.MinValue;
            bool foundFar = false;
            int gridSamples = FloorSamplesPerAxis * FloorSamplesPerAxis;
            for (int i = 0; i < gridSamples + FarSpotTries; i++)
            {
                Vector3 spot;
                if (i < gridSamples)
                {
                    float x = (i % FloorSamplesPerAxis) / (float)(FloorSamplesPerAxis - 1);
                    float z = (i / FloorSamplesPerAxis) / (float)(FloorSamplesPerAxis - 1);
                    spot = body.KeepOnFloor(new Vector3(
                        Mathf.Lerp(body.AreaMin.x, body.AreaMax.x, x), BossBody.Height,
                        Mathf.Lerp(body.AreaMin.y, body.AreaMax.y, z)));
                }
                else spot = body.RandomSpot();
                float fromHere = BossBody.FlatDistance(spot, from);
                float fromPlayers = body.Players.TryGetNearest(spot, out _, out float nearest) ? nearest : body.Settings.WarpMinDistance;
                bool isFar = fromHere >= body.Settings.WarpMinDistance && fromPlayers > Mathf.Max(playerClearance, body.Settings.FleeRadius);
                float score = fromPlayers + fromHere * FarFromHereWeight;
                if (foundFar && !isFar) continue;
                if (isFar == foundFar && score <= bestScore) continue;
                best = spot;
                bestScore = score;
                foundFar = isFar;
            }

            return foundFar;
        }
    }
}
