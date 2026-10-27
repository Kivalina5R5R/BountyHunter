using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The boss on the floor as its actions move it: where it stands, its picture (BossAnimator) and
    // disguise, and the floor it keeps to. It steps round passers-by and keeps out of the floor's
    // corners (cornerSize), faces the way it really moves (the walk's pictures face right: the cape
    // trails to the left) without flipping back and forth, and paces its walk to the ground it covers.
    public sealed class BossBody
    {
        public const float Height = 0.04f;
        // Turning round: moving at least this fast sideways (metres a second, smoothed), and not
        // again sooner than this; the smoothed movement follows the real one this quickly.
        private const float FaceDeadZone = 0.2f;
        private const float FaceHoldSeconds = 0.25f;
        private const float FaceSmoothing = 10f;

        private readonly List<Vector3> passerPositions = new List<Vector3>();
        private readonly Transform root;
        private readonly NpcCrowd passersBy;
        private float facedAt = -100f;
        // How it has really been moving lately (after edges, corners and passers-by), which it faces by.
        private Vector3 movedSmoothed;

        public BossBody(Transform root, BossAnimator animator, BossDisguise disguise, BossSettings settings, BossMove walk,
            SpawnerPlayerLocator players, NpcCrowd passersBy, Vector2 areaMin, Vector2 areaMax)
        {
            this.root = root;
            this.passersBy = passersBy;
            Animator = animator;
            Disguise = disguise;
            Settings = settings;
            Walk = walk;
            Players = players;
            AreaMin = areaMin;
            FloorMax = areaMax;
            // Its picture stands up from the feet towards the wall (+z): the feet stay its height below
            // the top edge so the head stays on the floor.
            AreaMax = new Vector2(areaMax.x, Mathf.Max(areaMin.y, areaMax.y - HeadRoom(walk)));
        }

        public BossAnimator Animator { get; }
        public BossDisguise Disguise { get; }
        public BossSettings Settings { get; }
        public BossMove Walk { get; }
        public SpawnerPlayerLocator Players { get; }
        // The floor its feet keep to (x and world z); FloorMax is the whole floor's far edge, which
        // AreaMax keeps the boss's height below.
        public Vector2 AreaMin { get; }
        public Vector2 AreaMax { get; }
        public Vector2 FloorMax { get; }
        // Where it means to go, turned gradually by the run.
        public Vector3 Heading { get; set; } = Vector3.left;
        // Where its things are put (water, clouds left behind): beside it, so they stay when it moves.
        public Transform Parent => root.parent;

        public Vector3 Position
        {
            get => root.position;
            set => root.position = new Vector3(value.x, Height, value.z);
        }

        // Moves by velocity for this frame, round any passer-by and inside the floor.
        public void Step(Vector3 velocity)
        {
            FindPassers();
            MoveTo(KeepOnFloor(KeepClearOfPassers(Position + velocity * Time.deltaTime)));
        }

        // Moves straight to next (nothing in the way), keeping track of how it moves.
        public void MoveTo(Vector3 next)
        {
            Vector3 from = Position;
            Position = next;
            if (Time.deltaTime <= 0f) return;

            Vector3 moved = (Position - from) / Time.deltaTime;
            movedSmoothed = Vector3.Lerp(movedSmoothed, moved, 1f - Mathf.Exp(-FaceSmoothing * Time.deltaTime));
        }

        // Faces the way it really moves; turns once it clearly moves the other way and has faced this
        // way a moment, so swaying or being herded up and down does not flip it back and forth.
        public void FaceWhereMoving()
        {
            float horizontal = movedSmoothed.x;
            if (Mathf.Abs(horizontal) < FaceDeadZone) return;

            bool flip = horizontal < 0f;
            if (flip == Animator.Body.flipX || Time.time - facedAt < FaceHoldSeconds) return;

            Animator.Body.flipX = flip;
            facedAt = Time.time;
        }

        // Points the gun that way: the shot's pictures hold the gun out to the left.
        public void AimAt(float horizontal)
        {
            if (Mathf.Abs(horizontal) > 0.01f) Animator.Body.flipX = horizontal > 0f;
        }

        // The walk keeps pace with the ground it covers at speed.
        public void PaceWalk(float speed)
        {
            Animator.Pace = speed / (Settings.MetresPerWalkFrame * Walk.FramesPerSecond);
        }

        public void PlayWalk()
        {
            Animator.Play(Walk.Frames, Walk.FramesPerSecond, Walk.All, true);
        }

        public void SetAlpha(float alpha)
        {
            Animator.Body.color = new Color(1f, 1f, 1f, alpha);
        }

        // Itself again, seen and solid, out of any disguise and still shake.
        public void DropDisguise()
        {
            if (Disguise.IsOn) Disguise.TakeOff();
            Animator.Body.enabled = true;
            Animator.transform.localPosition = Vector3.zero;
            SetAlpha(1f);
        }

        // Grows from nothing edgeMargin from an edge to a full turn at the edge.
        public Vector3 EdgePush(Vector3 position)
        {
            float margin = Mathf.Max(0.01f, Settings.EdgeMargin);
            var push = Vector3.zero;
            push.x += Mathf.Clamp01((AreaMin.x + margin - position.x) / margin);
            push.x -= Mathf.Clamp01((position.x - (AreaMax.x - margin)) / margin);
            push.z += Mathf.Clamp01((AreaMin.y + margin - position.z) / margin);
            push.z -= Mathf.Clamp01((position.z - (AreaMax.y - margin)) / margin);
            return push * 2f;
        }

        // Towards the middle when it gets into a corner square, harder the deeper it is.
        public Vector3 CornerPush(Vector3 position)
        {
            float corner = CornerSize;
            float intoX = Mathf.Max(AreaMin.x + corner - position.x, position.x - (AreaMax.x - corner));
            float intoZ = Mathf.Max(AreaMin.y + corner - position.z, position.z - (AreaMax.y - corner));
            if (intoX <= 0f || intoZ <= 0f) return Vector3.zero;

            var middle = new Vector3((AreaMin.x + AreaMax.x) * 0.5f - position.x, 0f, (AreaMin.y + AreaMax.y) * 0.5f - position.z);
            return middle.normalized * (2f * Mathf.Clamp01(Mathf.Min(intoX, intoZ) / corner) + 0.5f);
        }

        // Away from passers-by within passerClearance (the nearer, the harder), so it goes round them.
        public Vector3 PasserPush(Vector3 position)
        {
            FindPassers();
            var push = Vector3.zero;
            float clearance = Settings.PasserClearance;
            foreach (Vector3 passer in passerPositions)
            {
                var from = new Vector3(position.x - passer.x, 0f, position.z - passer.z);
                float distance = from.magnitude;
                if (distance >= clearance || distance < 0.001f) continue;
                push += from / distance * (1f - distance / clearance) * 2f;
            }

            return push;
        }

        // Inside the floor with its corners rounded off (cornerSize): a spot in a corner square is
        // moved onto the arc round that corner.
        public Vector3 KeepOnFloor(Vector3 spot)
        {
            float corner = CornerSize;
            var inner = new Vector2(Mathf.Clamp(spot.x, AreaMin.x + corner, AreaMax.x - corner), Mathf.Clamp(spot.z, AreaMin.y + corner, AreaMax.y - corner));
            var flat = new Vector2(Mathf.Clamp(spot.x, AreaMin.x, AreaMax.x), Mathf.Clamp(spot.z, AreaMin.y, AreaMax.y));
            bool inCornerSquare = flat.x != inner.x && flat.y != inner.y;
            if (inCornerSquare && (flat - inner).magnitude > corner) flat = inner + (flat - inner).normalized * corner;
            return new Vector3(flat.x, Height, flat.y);
        }

        // In one of the corner squares it keeps out of.
        public bool InCorner(Vector3 spot)
        {
            float corner = CornerSize;
            bool side = spot.x < AreaMin.x + corner || spot.x > AreaMax.x - corner;
            bool end = spot.z < AreaMin.y + corner || spot.z > AreaMax.y - corner;
            return side && end;
        }

        // A random spot on the floor, edgeMargin in from the edges and out of the corners.
        public Vector3 RandomSpot()
        {
            float margin = Settings.EdgeMargin;
            return KeepOnFloor(new Vector3(
                Random.Range(AreaMin.x + margin, AreaMax.x - margin),
                Height,
                Random.Range(AreaMin.y + margin, AreaMax.y - margin)));
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            return SpawnerPlayerLocator.FlatDistance(a, b);
        }

        private float CornerSize => Mathf.Min(Settings.CornerSize, (AreaMax.x - AreaMin.x) * 0.5f, (AreaMax.y - AreaMin.y) * 0.5f);

        private void FindPassers()
        {
            passerPositions.Clear();
            if (passersBy != null) passersBy.AddPositions(passerPositions, null);
        }

        // Out to passerGap from any passer-by it would come too close to.
        private Vector3 KeepClearOfPassers(Vector3 spot)
        {
            float gap = Settings.PasserGap;
            foreach (Vector3 passer in passerPositions)
            {
                var from = new Vector3(spot.x - passer.x, 0f, spot.z - passer.z);
                float distance = from.magnitude;
                if (distance >= gap) continue;
                Vector3 away = distance > 0.001f ? from / distance : -Heading;
                spot = new Vector3(passer.x + away.x * gap, spot.y, passer.z + away.z * gap);
            }

            return spot;
        }

        // How far the walk reaches above the feet (the tallest of its frames).
        private static float HeadRoom(BossMove move)
        {
            float top = 0f;
            foreach (Sprite frame in move.Frames) top = Mathf.Max(top, frame.bounds.max.y);
            return top;
        }
    }
}
