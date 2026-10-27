using UnityEngine;

namespace BountyHunter.Thieves
{
    // The boss getting away when nobody caught it in time: out of any disguise, straight over the
    // nearer side edge and past it, faster than its run, with nothing in its way and no touching it.
    // Over once it is out of sight.
    public sealed class BossRunOff : IBossAction
    {
        // This far past the side edge it is out of sight; this much faster than its run.
        private const float ExitBeyond = 1.5f;
        private const float ExitSpeedUp = 1.3f;

        private readonly BossBody body;
        private float exitX;

        public BossRunOff(BossBody body)
        {
            this.body = body;
        }

        public bool Touchable => false;
        public bool CountsTouches => false;

        public void Begin()
        {
            body.DropDisguise();
            Vector3 position = body.Position;
            bool leftIsNearer = position.x - body.AreaMin.x < body.FloorMax.x - position.x;
            exitX = leftIsNearer ? body.AreaMin.x - ExitBeyond : body.FloorMax.x + ExitBeyond;
            body.PlayWalk();
        }

        public bool Tick()
        {
            Vector3 from = body.Position;
            float speed = body.Settings.RunSpeed * ExitSpeedUp;
            body.MoveTo(new Vector3(Mathf.MoveTowards(from.x, exitX, speed * Time.deltaTime), 0f, from.z));
            body.FaceWhereMoving();
            body.PaceWalk(speed);
            if (Mathf.Abs(body.Position.x - exitX) > 0.01f) return true;

            body.Animator.Body.enabled = false;
            return false;
        }

        public void Stop()
        {
        }
    }
}
