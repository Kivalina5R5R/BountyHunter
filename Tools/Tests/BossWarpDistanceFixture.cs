using System;
using System.Collections.Generic;
namespace WarpDistanceChecks
{
    struct Vector3
    {
        public float x { get; }
        public float z { get; }
        public Vector3(float x, float y, float z) { this.x = x; this.z = z; }
    }
    struct Vector2
    {
        public float x { get; set; }
        public float y { get; set; }
    }
    static class Mathf
    {
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float Max(float a, float b) => Math.Max(a, b);
    }
    class Settings { public float WarpMinDistance => 5f; public float FleeRadius => 3.2f; }
    class Players
    {
        public List<Vector3> Positions { get; } = new List<Vector3>();
        public bool TryGetNearest(Vector3 from, out Vector3 player, out float distance)
        {
            player = new Vector3(); distance = float.MaxValue;
            foreach (var point in Positions)
            {
                float next = BossBody.FlatDistance(from, point);
                if (next >= distance) continue;
                distance = next; player = point;
            }
            return Positions.Count > 0;
        }
    }
    class BossBody
    {
        public const float Height = 0.04f;
        public Settings Settings { get; } = new Settings();
        public Players Players { get; } = new Players();
        public Vector2 AreaMin => new Vector2();
        public Vector2 AreaMax { get; set; } = new Vector2 { x = 8, y = 6 };
        public Vector3 KeepOnFloor(Vector3 p) => p;
        // Deliberately miss the far edge on every random sample.
        public Vector3 RandomSpot() => new Vector3(1, 0, 1);
        public static float FlatDistance(Vector3 a, Vector3 b) => (float)Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));
    }
    public class Check
    {
        private const int FarSpotTries = 40;
        private const int FloorSamplesPerAxis = 9;
        private const float FarFromHereWeight = 0.3f;
        private readonly BossBody body = new BossBody();
        private readonly float playerClearance = 3.2f;
        /* DESTINATION_METHOD */
        public static void Run()
        {
            var test = new Check();
            var origin = new Vector3(1, 0, 1);
            test.body.Players.Positions.Add(origin);
            if (!test.TryFindFarSpot(origin, out var far) || far.x != 8 || far.z != 6)
                throw new Exception("Missed open far perimeter when random samples stayed close");
            test.body.Players.Positions.Add(far);
            if (!test.TryFindFarSpot(origin, out var alternative)) throw new Exception("Missed alternate far ground");
            foreach (var player in test.body.Players.Positions)
                if (BossBody.FlatDistance(alternative, player) <= 3.2f) throw new Exception("Warp destination is near a teammate");
            test.body.AreaMax = new Vector2 { x = 2, y = 2 };
            if (test.TryFindFarSpot(origin, out var fallback)) throw new Exception("Short warp incorrectly qualified as far");
            if (fallback.x < 0 || fallback.x > 2 || fallback.z < 0 || fallback.z > 2) throw new Exception("Fallback outside floor");
            Console.WriteLine("PASS: production warp selection finds far perimeter, avoids all players, rejects short destinations and retains in-bounds last resort.");
        }
    }
}
