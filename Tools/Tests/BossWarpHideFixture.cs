#pragma warning disable CS0649, CS0414
using System;
using System.Collections.Generic;
namespace WarpHideChecks
{
    struct Vector3
    {
        public float X { get; set; }
        public static Vector3 zero => new Vector3();
    }
    static class Mathf { public static float Max(float a, float b) => Math.Max(a, b); }
    static class Debug { public static void Log(string text) { } }
    class Settings { public float HideClearance => 1.5f; public float HideSeconds => 5f; public float RunSpeed => 1.4f; public float TouchRadius => 0.6f; }
    class UWBPlayerController { public PlayerTransform transform { get; } = new PlayerTransform(); }
    class PlayerTransform { public Vector3 position { get; set; } }
    class Picture { public bool enabled { get; set; } = true; }
    class Animator { public Picture Body { get; } = new Picture(); }
    class Players
    {
        public float? Position { get; set; }
        public bool TryGetNearest(Vector3 point, out Vector3 nearest, out float distance)
        {
            nearest = new Vector3 { X = Position ?? 0 };
            distance = Position.HasValue ? Math.Abs(point.X - Position.Value) : float.MaxValue;
            return Position.HasValue;
        }
    }
    class BossBody
    {
        public Vector3 Position { get; set; }
        public Settings Settings { get; } = new Settings();
        public Players Players { get; } = new Players();
        public Animator Animator { get; } = new Animator();
        public bool InCorner(Vector3 point) => point.X > 9;
        public static float FlatDistance(Vector3 a, Vector3 b) => Math.Abs(a.X - b.X);
    }
    class HidingBush
    {
        public bool isActiveAndEnabled { get; set; } = true;
        public string name => "Test bush";
        public object Occupant { get; set; }
        public object ReservedBy { get; set; }
        public bool IsHeldByOther { get; set; }
        public bool PlayerIsStandingOn { get; set; }
        public Vector3 Center { get; set; }
        public float Radius => 0.6f;
        public int Entries { get; private set; }
        public void PopIn(Vector3 side) { Entries++; }
        public void HoldOther(float seconds) { IsHeldByOther = true; }
    }
    class Spots { public List<HidingBush> Bushes { get; } = new List<HidingBush>(); }
    public class Check
    {
        private const float StayOnMargin = 0.18f;
        private const float AtSpot = 0.15f;
        private const float ArrivalMarginSeconds = 0.15f;
        private readonly List<UWBPlayerController> trackedPlayers = new List<UWBPlayerController>();
        private readonly Dictionary<UWBPlayerController, (Vector3 position, float speed, float time)> motion =
            new Dictionary<UWBPlayerController, (Vector3 position, float speed, float time)>();
        private readonly BossBody body = new BossBody();
        private readonly Spots hidingSpots = new Spots();
        private HidingBush hideout, lastLeft;
        private bool hidden;
        private float hiddenFor, stoodOn, sneakOutAt;
        public bool HasExited { get; private set; }
        private float SneakOutTime() => 3;
        /* HIDING_METHODS */
        private HidingBush Add(float x)
        {
            var bush = new HidingBush { Center = new Vector3 { X = x } };
            hidingSpots.Bushes.Add(bush);
            return bush;
        }
        private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        public static void Run()
        {
            var free = new Check(); var target = free.Add(5);
            Require(free.TryWarpIn() && free.hidden && free.body.Position.X == 5, "Did not enter free bush");
            Require(!free.body.Animator.Body.enabled && target.IsHeldByOther && target.Entries == 1, "Hidden state/hold/visual not initialized");
            Require(free.hiddenFor == 0 && free.stoodOn == 0 && free.sneakOutAt == 3, "Hidden timers not reset");
            foreach (string reason in new[] { "occupied", "reserved", "held", "stoodOn", "inactive", "lastLeft", "corner", "playerArrived" })
            {
                var test = new Check(); var bush = test.Add(reason == "corner" ? 10 : 5);
                switch (reason)
                {
                    case "occupied": bush.Occupant = new object(); break;
                    case "reserved": bush.ReservedBy = new object(); break;
                    case "held": bush.IsHeldByOther = true; break;
                    case "stoodOn": bush.PlayerIsStandingOn = true; break;
                    case "inactive": bush.isActiveAndEnabled = false; break;
                    case "lastLeft": test.lastLeft = bush; break;
                    case "playerArrived": test.body.Players.Position = 5; break;
                }
                Require(!test.TryWarpIn() && !test.hidden && test.body.Position.X == 0, "Invalid destination accepted: " + reason);
            }
            var selection = new Check(); selection.Add(1); selection.Add(7); selection.body.Players.Position = 0;
            Require(selection.TryWarpIn() && selection.body.Position.X == 7, "Did not pick safe bush away from player");
            var race = new Check(); var closeBox = race.Add(0.2f);
            var pursuer = new UWBPlayerController(); pursuer.transform.position = new Vector3 { X = -1 };
            race.trackedPlayers.Add(pursuer);
            race.motion[pursuer] = (pursuer.transform.position, 0.8f, 0);
            Require(race.CanReachFirst(closeBox, Vector3.zero), "Slow chaser incorrectly blocks nearby box");
            race.motion[pursuer] = (pursuer.transform.position, 4f, 0);
            Require(!race.CanReachFirst(closeBox, Vector3.zero), "Faster chaser can intercept but box accepted");
            race.motion[pursuer] = (pursuer.transform.position, 0f, 0);
            Require(race.CanReachFirst(closeBox, Vector3.zero), "Stationary player incorrectly blocks reachable box");
            var guard = new UWBPlayerController(); guard.transform.position = closeBox.Center; race.trackedPlayers.Add(guard);
            Require(!race.CanReachFirst(closeBox, Vector3.zero), "Second player guarding box ignored");
            race.trackedPlayers.Remove(guard); pursuer.transform.position = new Vector3 { X = 0.9f };
            Require(!race.CanReachFirst(closeBox, Vector3.zero), "Player on box margin ignored");
            Console.WriteLine("PASS: production arrival-time check, slow/fast/stationary pursuers and teammate guarding destination.");
            Console.WriteLine("PASS: production warp-to-hide selection, occupancy, reservations, player arrival, last bush, corners, hidden state and timers.");
        }
    }
}
