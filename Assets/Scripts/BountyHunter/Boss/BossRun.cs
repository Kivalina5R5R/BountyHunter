using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The boss running about, what it does between its other actions, the way a thief does: with a
    // player within fleeRadius it runs, picking a way out (the direction that keeps most room from
    // every player a short run ahead, away from the edges and the corners) and sticking to it for
    // commitSeconds unless a player or the edge is right ahead, so it breaks away instead of running
    // in circles; it calms down only once no player has been that close for calmSeconds, then strolls
    // towards a random spot. It gets up to speed at acceleration, sways side to side, and steers
    // round passers-by. It never ends by itself.
    public sealed class BossRun : IBossAction
    {
        private const float TurnRadiansPerSecond = 6f;
        private const float StrollShare = 0.6f;
        private const float ProgressSeconds = 0.75f;
        // The fan of ways out it weighs, and how far ahead it looks along each.
        private const int Ways = 16;
        private const float LookAhead = 1.2f;
        // A player this close and this straight ahead blocks the way it is sticking to.
        private const float BlockedDistance = 0.8f;
        private const float BlockedAlignment = 0.75f;

        private readonly List<Vector3> playerPositions = new List<Vector3>();
        private readonly BossBody body;
        private Vector3 strollTarget;
        private float runTime;
        private float speed;
        private float lastAlarmAt = float.MinValue;
        private Vector3 committed;
        private float commitUntil;
        private float blockedFor;
        private Vector3 progressOrigin;
        private float progressTime;
        private float expectedProgress;
        private bool circling;
        private float escapeUntil;

        public BossRun(BossBody body)
        {
            this.body = body;
        }

        public bool Touchable => true;
        public bool CountsTouches => true;
        // The controller can choose another action when even the alternate escape cannot move it.
        public bool IsBlocked => Time.time >= escapeUntil && (blockedFor > 0f || circling);

        public void Begin(float escapeSeconds = 0f, bool animateWalk = true)
        {
            escapeUntil = Time.time + escapeSeconds;
            if (escapeSeconds > 0f)
            {
                body.Players.GetAll(playerPositions);
                body.Heading = BestWayOut(body.Position);
                speed = body.Settings.RunSpeed;
                lastAlarmAt = Time.time;
            }
            if (animateWalk) body.PlayWalk();
            commitUntil = 0f;
            blockedFor = 0f;
            progressOrigin = body.Position;
            progressTime = 0f;
            expectedProgress = 0f;
            circling = false;
        }

        public bool Tick()
        {
            return Tick(true);
        }

        // A disguised boss uses the same escape movement while its disguise animates separately.
        public bool Tick(bool animateWalk)
        {
            BossSettings settings = body.Settings;
            Vector3 position = body.Position;
            body.Players.GetAll(playerPositions);
            if (body.Players.TryGetNearest(position, out _, out float nearest) && nearest < settings.FleeRadius) lastAlarmAt = Time.time;
            bool fleeing = Time.time - lastAlarmAt < settings.CalmSeconds;

            if (!fleeing && BossBody.FlatDistance(position, strollTarget) < 0.4f) strollTarget = body.RandomSpot();
            Vector3 wanted = fleeing ? WayOut(position) : (strollTarget - position).normalized;
            wanted += body.CornerPush(position) + body.PasserPush(position);
            if (!fleeing) wanted += body.EdgePush(position);
            if (wanted.sqrMagnitude < 0.0001f) wanted = body.Heading;
            wanted.y = 0f;
            Vector3 heading = Vector3.RotateTowards(body.Heading, wanted.normalized, TurnRadiansPerSecond * Time.deltaTime, 0f);
            body.Heading = heading;

            float wantedSpeed = fleeing ? settings.RunSpeed : settings.RunSpeed * StrollShare;
            speed = Mathf.MoveTowards(speed, wantedSpeed, settings.Acceleration * (fleeing ? 3f : 1f) * Time.deltaTime);
            runTime += Time.deltaTime;
            float wave = 2f * Mathf.PI / settings.WobbleSeconds;
            var side = new Vector3(-heading.z, 0f, heading.x);
            // Sways less the slower it goes, so a stroll does not zigzag wider than it moves on.
            float sway = settings.Wobble * wave * Mathf.Cos(runTime * wave) * speed / settings.RunSpeed;
            Vector3 velocity = heading * speed + side * sway;
            bool avoidPlayers = Time.time >= escapeUntil;
            body.Step(velocity, avoidPlayers);
            if (Time.deltaTime > 0f && (body.Position - position).sqrMagnitude < 0.00000001f)
            {
                // A blocked turn or sway must not keep it pushing into the same player after hiding.
                TryEscapeStep(position, avoidPlayers);
            }

            body.FaceWhereMoving();
            float movedSpeed = Time.deltaTime > 0f ? BossBody.FlatDistance(position, body.Position) / Time.deltaTime : 0f;
            if (Time.deltaTime > 0f)
            {
                blockedFor = speed > 0.05f && movedSpeed < speed * 0.1f ? blockedFor + Time.deltaTime : 0f;
                progressTime += Time.deltaTime;
                expectedProgress += speed * Time.deltaTime;
                if (progressTime >= ProgressSeconds)
                {
                    // Moving back and forth against the edge is still a failed escape.
                    circling = expectedProgress > 0.05f && BossBody.FlatDistance(progressOrigin, body.Position) < expectedProgress * 0.25f;
                    progressOrigin = body.Position;
                    progressTime = 0f;
                    expectedProgress = 0f;
                }
            }
            if (animateWalk) body.PaceWalk(movedSpeed);
            return true;
        }

        public void Stop()
        {
        }

        // Test actual steps too: a good distant destination may still have a blocked first step.
        private void TryEscapeStep(Vector3 position, bool avoidPlayers)
        {
            Vector3 preferred = BestWayOut(position);
            for (int i = 0; i < Ways; i++)
            {
                int offset = i % 2 == 0 ? -i / 2 : (i + 1) / 2;
                Vector3 way = Quaternion.Euler(0f, offset * 360f / Ways, 0f) * preferred;
                body.Step(way * speed, avoidPlayers);
                if (BossBody.FlatDistance(position, body.Position) <= 0f) continue;
                committed = way;
                commitUntil = Time.time + body.Settings.CommitSeconds;
                body.Heading = way;
                return;
            }
        }

        // The way out it is sticking to, chosen again once commitSeconds are up or it is blocked.
        private Vector3 WayOut(Vector3 position)
        {
            if (Time.time < commitUntil && !Blocked(position, committed)) return committed;

            committed = BestWayOut(position);
            commitUntil = Time.time + body.Settings.CommitSeconds;
            return committed;
        }

        // A player right ahead, or the edge straight in front.
        private bool Blocked(Vector3 position, Vector3 way)
        {
            Vector3 ahead = body.KeepOnFloor(position + way * BlockedDistance);
            if (BossBody.FlatDistance(position, ahead) < BlockedDistance * 0.4f) return true;

            foreach (Vector3 player in playerPositions)
            {
                Vector3 toPlayer = player - position;
                toPlayer.y = 0f;
                float distance = toPlayer.magnitude;
                if (distance < 0.0001f) continue;
                // Only one right in the way counts; brushing past a player at the side of a gap is the point.
                if (distance < BlockedDistance && Vector3.Dot(toPlayer / distance, way) > BlockedAlignment) return true;
            }

            return false;
        }

        // Of a fan of ways, the one that keeps the most room from every player a short run ahead, has
        // floor ahead, leans away from the players and the edges, keeps roughly the way it was going
        // and does not lead into a corner.
        private Vector3 BestWayOut(Vector3 position)
        {
            Vector3 push = body.EdgePush(position) * 2f;
            foreach (Vector3 player in playerPositions)
            {
                Vector3 away = position - player;
                away.y = 0f;
                float distance = Mathf.Max(0.1f, away.magnitude);
                push += away / (distance * distance);
            }

            Vector3 preferred = push.sqrMagnitude > 0.0001f ? push.normalized : body.Heading;
            Vector3 best = preferred;
            float bestScore = float.MinValue;
            for (int i = 0; i < Ways; i++)
            {
                Vector3 way = Quaternion.Euler(0f, i * 360f / Ways, 0f) * Vector3.forward;
                Vector3 ahead = body.KeepOnFloor(position + way * LookAhead);
                float room = BossBody.FlatDistance(position, ahead) / LookAhead;
                float closest = 3f;
                foreach (Vector3 player in playerPositions) closest = Mathf.Min(closest, BossBody.FlatDistance(ahead, player));
                float score = closest + room * 1.5f + Vector3.Dot(way, preferred) * 0.6f + Vector3.Dot(way, body.Heading) * 0.3f;
                float clearance = Mathf.Max(body.Settings.RouteClearance, body.Settings.TouchRadius + 0.05f);
                foreach (Vector3 player in playerPositions)
                {
                    float minimum = Mathf.Min(clearance, BossBody.FlatDistance(position, player));
                    if (BossBody.RouteDistance(position, ahead, player) < minimum) score -= 10f;
                }
                Vector3 further = position + way * LookAhead * 2f;
                if (body.InCorner(ahead)) score -= 3f;
                if (body.InCorner(further)) score -= 1.5f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = way;
            }

            return best;
        }
    }
}
