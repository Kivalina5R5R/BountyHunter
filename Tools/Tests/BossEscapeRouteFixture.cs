using System;
namespace EscapeRouteChecks
{
    struct Vector3
    {
        public float x { get; set; }
        public float z { get; set; }
        public static Vector3 operator *(Vector3 a, float scale) => new Vector3 { x = a.x * scale, z = a.z * scale };
    }
    struct Quaternion
    {
        private float radians;
        public static Quaternion Euler(float x, float y, float z) => new Quaternion { radians = y * (float)Math.PI / 180f };
        public static Vector3 operator *(Quaternion q, Vector3 v) => new Vector3
        {
            x = v.x * (float)Math.Cos(q.radians) + v.z * (float)Math.Sin(q.radians),
            z = v.z * (float)Math.Cos(q.radians) - v.x * (float)Math.Sin(q.radians)
        };
    }
    static class Time
    {
        public static float deltaTime { get; set; }
        public static float time => 10f;
    }
    class Settings { public float CommitSeconds => 0.4f; }
    class BossBody
    {
        public Vector3 Position { get; set; }
        public Vector3 Heading { get; set; }
        public Settings Settings { get; } = new Settings();
        public Func<Vector3, bool> CanMove { get; set; }
        public int Attempts { get; private set; }
        public bool AvoidedPlayers { get; private set; } = true;
        public void Step(Vector3 velocity, bool avoidPlayers)
        {
            Attempts++;
            AvoidedPlayers &= avoidPlayers;
            if (!CanMove(velocity)) return;
            Position = new Vector3 { x = Position.x + velocity.x * Time.deltaTime, z = Position.z + velocity.z * Time.deltaTime };
        }
        public static float FlatDistance(Vector3 a, Vector3 b) => (float)Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));
    }
    public class Check
    {
        private const int Ways = 16;
        private readonly BossBody body = new BossBody();
        private readonly float speed = 1.4f;
        private Vector3 committed;
        private float commitUntil;
        private float escapeUntil;
        private float blockedFor;
        private bool circling;
        private Vector3 BestWayOut(Vector3 position) => new Vector3 { z = 1 };
        /* ESCAPE_METHOD */
        public static void Run()
        {
            foreach (int fps in new[] { 30, 60, 120 })
            {
                Time.deltaTime = 1f / fps;
                var immediate = new Check();
                immediate.blockedFor = Time.deltaTime;
                if (!immediate.IsBlocked) throw new Exception("Failed escape waits before smoke");
                immediate.escapeUntil = Time.time + 1f;
                if (immediate.IsBlocked) throw new Exception("Immediate smoke interrupted protected hide exit");
                immediate.escapeUntil = Time.time;
                if (!immediate.IsBlocked) throw new Exception("Protected exit expiry delayed smoke");
                immediate.blockedFor = 0f;
                immediate.circling = true;
                if (!immediate.IsBlocked) throw new Exception("Circling no longer triggers escape");
                var open = new Check();
                open.body.CanMove = v => v.x > 1f;
                open.TryEscapeStep(new Vector3(), true);
                if (open.body.Position.x <= 0 || open.body.Attempts <= 1) throw new Exception("Blocked preferred direction did not try the open side in the same frame");
                if (Math.Abs(BossBody.FlatDistance(new Vector3(), open.body.Position) - open.speed / fps) > 0.00001f) throw new Exception("Retries changed movement speed");
                if (!open.body.AvoidedPlayers || open.commitUntil <= Time.time || open.committed.x <= 0) throw new Exception("Escape lost collision protection or heading commitment");
                var trapped = new Check();
                trapped.body.CanMove = v => false;
                trapped.TryEscapeStep(new Vector3(), true);
                if (trapped.body.Attempts != Ways || BossBody.FlatDistance(new Vector3(), trapped.body.Position) != 0) throw new Exception("Surrounded escape crossed a blocked route or failed to check all directions");
                var protectedExit = new Check();
                protectedExit.body.CanMove = v => true;
                protectedExit.TryEscapeStep(new Vector3(), false);
                if (protectedExit.body.AvoidedPlayers) throw new Exception("Protected hide exit lost its movement exemption");
            }
            Console.WriteLine("PASS: production escape retries at 30/60/120 FPS; blocked preferred route, open side, fully surrounded, unchanged speed and protected exit.");
        }
    }
}
