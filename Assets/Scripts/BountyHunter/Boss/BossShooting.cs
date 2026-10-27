using System;
using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // The boss's water gun, used when players huddle together (Huddled): it turns to the nearest
    // player and fires once, standing; the moment the water leaves the gun it comes down all over the
    // floor in warning circles (BossWaterVolley), the first on each huddled player if they are still
    // together, and a player in a circle as it lands is hit (once a volley). Then it lowers the gun.
    public sealed class BossShooting : IBossAction
    {
        private const int RainSpotTries = 30;

        private readonly List<Vector3> playerPositions = new List<Vector3>();
        private readonly List<Vector3> rainSpots = new List<Vector3>();
        private readonly BossBody body;
        private readonly BossMove shoot;
        private readonly Action<UWBPlayerController> hit;
        private bool released;
        // The shot is out and the gun is coming down.
        private bool lowering;

        // hit is told each player the water hits.
        public BossShooting(BossBody body, BossMove shoot, Action<UWBPlayerController> hit)
        {
            this.body = body;
            this.shoot = shoot;
            this.hit = hit;
        }

        public bool CanShoot => !shoot.IsEmpty;
        // Not while it stands shooting, so nobody gains by waiting beside it.
        public bool Touchable => false;
        public bool CountsTouches => true;

        // Two players standing within huddleRadius of each other.
        public bool Huddled()
        {
            return Huddled(out _, out _);
        }

        // The first two players found standing within huddleRadius of each other.
        private bool Huddled(out Vector3 first, out Vector3 second)
        {
            body.Players.GetAll(playerPositions);
            for (int i = 0; i < playerPositions.Count; i++)
            {
                for (int j = i + 1; j < playerPositions.Count; j++)
                {
                    if (BossBody.FlatDistance(playerPositions[i], playerPositions[j]) > body.Settings.HuddleRadius) continue;
                    first = playerPositions[i];
                    second = playerPositions[j];
                    return true;
                }
            }

            first = Vector3.zero;
            second = Vector3.zero;
            return false;
        }

        public void Begin()
        {
            released = false;
            lowering = false;
            Vector3 position = body.Position;
            if (body.Players.TryGetNearest(position, out Vector3 target, out _)) body.AimAt(target.x - position.x);
            body.Animator.Play(shoot.Frames, shoot.FramesPerSecond, body.Settings.FiringFrames, false);
        }

        public bool Tick()
        {
            if (!released && !lowering && body.Animator.Frame >= body.Settings.ReleaseFrame) Release();
            if (!body.Animator.Done) return true;
            if (lowering) return false;

            lowering = true;
            body.Animator.Play(shoot.Frames, shoot.FramesPerSecond, body.Settings.LowerGunFrames, false);
            return true;
        }

        public void Stop()
        {
        }

        // The water leaves the gun: the circles show where it will come down.
        private void Release()
        {
            released = true;
            BossSettings settings = body.Settings;
            PickRainSpots();
            BossWaterVolley.Fire(rainSpots, settings.RainRadius, settings.RainWarningSeconds, settings.RainColor, settings.Water, settings.SortingOrder - 3,
                body.Players, hit, body.Parent);
        }

        // rainCircles spots on the floor (one on each huddled player, or one for both when they stand
        // that close), kept apart so the circles do not overlap when there is room.
        private void PickRainSpots()
        {
            rainSpots.Clear();
            float radius = body.Settings.RainRadius;
            if (Huddled(out Vector3 first, out Vector3 second))
            {
                rainSpots.Add(first);
                if (BossBody.FlatDistance(first, second) > radius) rainSpots.Add(second);
            }

            float spacing = radius * 2f + 0.2f;
            while (rainSpots.Count < body.Settings.RainCircles)
            {
                Vector3 best = RainSpot(radius);
                for (int attempt = 0; attempt < RainSpotTries; attempt++)
                {
                    Vector3 spot = RainSpot(radius);
                    if (!ClearOfOthers(spot, spacing)) continue;
                    best = spot;
                    break;
                }

                rainSpots.Add(best);
            }
        }

        // Anywhere on the whole floor (up to its far edge, not the boss's lowered one).
        private Vector3 RainSpot(float radius)
        {
            return new Vector3(
                Random.Range(body.AreaMin.x + radius, body.FloorMax.x - radius),
                0f,
                Random.Range(body.AreaMin.y + radius, body.FloorMax.y - radius));
        }

        private bool ClearOfOthers(Vector3 spot, float spacing)
        {
            foreach (Vector3 other in rainSpots)
            {
                if (BossBody.FlatDistance(spot, other) < spacing) return false;
            }

            return true;
        }
    }
}
