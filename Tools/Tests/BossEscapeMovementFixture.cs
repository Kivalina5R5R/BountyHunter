using System;
using System.Collections.Generic;
namespace EscapeMovementChecks
{
    public struct Vector3
    {
        public float x { get; set; }
        public float z { get; set; }
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3 { x = a.x - b.x, z = a.z - b.z };
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3 { x = a.x + b.x, z = a.z + b.z };
        public static Vector3 operator *(Vector3 a, float b) => new Vector3 { x = a.x * b, z = a.z * b };
        public static Vector3 operator /(Vector3 a, float b) => a * (1 / b);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * t;
    }
    struct Vector2
    {
        private readonly float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float sqrMagnitude => x * x + y * y;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float b) => new Vector2(a.x * b, a.y * b);
    }
    static class Time { public static float deltaTime => 0.1f; }
    static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Clamp01(float x) => Math.Max(0, Math.Min(1, x));
        public static float Exp(float x) => (float)Math.Exp(x);
    }
    class SettingsDouble { public float RouteClearance => 0.9f; public float TouchRadius => 0.6f; }
    class PlayersDouble
    {
        public void GetAll(List<Vector3> result) { result.Clear(); result.Add(new Vector3 { x = 0.1f }); }
    }
    public class Check
    {
        private const float FaceSmoothing = 10;
        private readonly List<Vector3> playerPositions = new List<Vector3>();
        private PlayersDouble Players { get; } = new PlayersDouble();
        private SettingsDouble Settings { get; } = new SettingsDouble();
        private Vector3 Position { get; set; }
        private Vector3 movedSmoothed;
        private void FindPassers() { }
        private Vector3 KeepClearOfPassers(Vector3 point) => point;
        private Vector3 KeepOnFloor(Vector3 point) => new Vector3 { x = Math.Max(-1, Math.Min(1, point.x)), z = point.z };
        private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
        /* MOVEMENT_METHODS */
        public static void Run()
        {
            var boss = new Check();
            boss.Step(new Vector3 { x = 0.5f });
            if (boss.Position.x != 0) throw new Exception("Normal movement crossed into a player");
            boss.Step(new Vector3 { x = 0.5f }, false);
            if (boss.Position.x <= 0) throw new Exception("Protected hide exit was cancelled by overlap");
            boss.Position = new Vector3 { x = 0.1f };
            boss.Step(new Vector3 { x = -1 }, false);
            if (boss.Position.x >= 0.1f) throw new Exception("Exact player overlap prevented escape");
            boss.Position = new Vector3();
            boss.Step(new Vector3 { x = -1 });
            if (boss.Position.x >= 0) throw new Exception("Normal safe escape was blocked");
            boss.Step(new Vector3 { x = 100 }, false);
            if (boss.Position.x > 1) throw new Exception("Protected exit left floor bounds");
            Console.WriteLine("PASS: production Step/MoveTo/RouteDistance; protected overlap can move, normal player collision and floor clamp remain active.");
        }
    }
}
