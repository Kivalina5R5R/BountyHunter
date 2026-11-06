using System;
namespace DisguiseEscapeChecks
{
    struct Vector3
    {
        public float x { get; set; }
        public float y { get; set; }
        public static Vector3 zero => new Vector3();
        public Vector3 normalized => new Vector3 { x = Math.Sign(x) };
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3 { x = a.x + b.x };
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3 { x = a.x - b.x };
        public static Vector3 operator *(Vector3 a, float b) => new Vector3 { x = a.x * b };
        public static Vector3 operator /(Vector3 a, float b) => new Vector3 { x = a.x / b };
    }
    static class Time { public static float time { get; set; } public static float deltaTime { get; set; } }
    class Settings { public float FleeRadius => 3.2f; public float CalmSeconds => 1.2f; public float DisguiseSpeed => 0.6f; }
    class Players
    {
        public bool Near { get; set; }
        public bool TryGetWithin(Vector3 from, float radius, out Vector3 player) { player = Vector3.zero; return Near; }
    }
    class Disguise
    {
        public Vector3 Velocity { get; private set; }
        public void Walk(Vector3 velocity) { Velocity = velocity; }
    }
    class BossBody
    {
        public Vector3 Position { get; set; }
        public Settings Settings { get; } = new Settings();
        public Players Players { get; } = new Players();
        public Disguise Disguise { get; } = new Disguise();
        public Vector3 RandomSpot() => new Vector3 { x = 10 };
        public Vector3 PasserPush(Vector3 position) => Vector3.zero;
        public void Step(Vector3 velocity) { Position += velocity * Time.deltaTime; }
        public static float FlatDistance(Vector3 a, Vector3 b) => Math.Abs(a.x - b.x);
    }
    class RunDouble
    {
        private readonly BossBody body;
        public RunDouble(BossBody body) { this.body = body; }
        public int Begins { get; private set; }
        public int Ticks { get; private set; }
        public bool Blocked { get; set; }
        public void Begin(float seconds, bool animateWalk) { if (animateWalk) throw new Exception("Disguise overwritten"); Begins++; }
        public void Tick(bool animateWalk)
        {
            if (animateWalk) throw new Exception("Hidden boss animation advanced");
            Ticks++;
            if (!Blocked) body.Position += new Vector3 { x = -1.4f * Time.deltaTime };
        }
    }
    public class Check
    {
        private readonly BossBody body = new BossBody();
        private readonly RunDouble run;
        private Vector3 strollTarget;
        private bool fleeing;
        private float lastThreatAt = float.MinValue;
        private Check() { run = new RunDouble(body); }
        /* STROLL_METHOD */
        public static void Run()
        {
            foreach (int fps in new[] { 30, 60, 120 })
            {
                Time.time = 0; Time.deltaTime = 1f / fps;
                var boss = new Check(); boss.body.Players.Near = true;
                for (int i = 0; i < fps; i++) { Time.time += Time.deltaTime; boss.Stroll(); }
                if (boss.body.Position.x > -1.3f || boss.run.Begins != 1 || boss.run.Ticks != fps) throw new Exception("Disguise did not continuously flee");
                if (Math.Abs(boss.body.Disguise.Velocity.x + 1.4f) > 0.001f) throw new Exception("Disguise animation ignores movement");
                boss.run.Blocked = true; boss.Stroll();
                if (boss.body.Disguise.Velocity.x != 0) throw new Exception("Disguise runs in place while blocked");
                boss.body.Players.Near = false; Time.time += 0.5f; boss.Stroll();
                if (!boss.fleeing) throw new Exception("Escape stops on brief tracking gap");
                Time.time += 1.3f; boss.Stroll();
                if (boss.fleeing || boss.body.Disguise.Velocity.x <= 0) throw new Exception("Safe disguise failed to resume strolling");
            }
            Console.WriteLine("PASS: production disguise Stroll at 30/60/120 FPS uses continuous escape, preserves disguise, animates actual motion, tolerates brief tracking gaps.");
        }
    }
}
