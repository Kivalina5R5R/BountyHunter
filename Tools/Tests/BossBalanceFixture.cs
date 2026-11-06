#pragma warning disable CS0649, CS0414
using System;
using System.Collections.Generic;
using Vector3 = System.Single;

enum BossSkill { Shoot, SmokeBomb, Disguise, Hide }
struct Vector2 { public float x { get; set; } public float y { get; set; } }
static class Time { public static float time { get; set; } public static float deltaTime { get; set; } }
static class Mathf { public static float Max(float a, float b) => Math.Max(a, b); }
static class Random { public static float Range(float a, float b) => (a + b) / 2; }
static class Debug { public static void Log(string message, object owner) { } }
class UWBPlayerController
{
    public int PlayerId { get; set; }
    public float Position { get; set; }
    public bool Active { get; set; } = true;
}
class BossSettings
{
    public float ExitGraceSeconds => __exitGraceSeconds;
    public float CatchWindowSeconds => __catchWindowSeconds;
    public float ActionGapSeconds => __actionGapSeconds;
    public float TouchRadius => __touchRadius;
    public float FleeRadius => __fleeRadius;
    public float SkillClearance => Math.Max(TouchRadius, __skillClearance);
    public float RunSpeed => 1.4f;
    public float RouteClearance => 0.9f;
    public float ShootEverySeconds => 7;
    public float DisguiseEverySeconds => 10;
    public float HideEverySeconds => 10;
    public float HideReach => 3.5f;
    public Vector2 DisguiseWhenPlayerAt => new Vector2 { x = 1.3f, y = 3f };
    public Vector2 MoveEverySeconds => new Vector2 { x = 8, y = 12 };
}
class Players
{
    public List<UWBPlayerController> All { get; } = new List<UWBPlayerController>();
    public bool TryGetNearest(float from, out float position, out float distance)
    {
        position = 0; distance = float.MaxValue;
        foreach (var player in All)
            if (player.Active && Math.Abs(player.Position - from) < distance)
            { position = player.Position; distance = Math.Abs(position - from); }
        return distance != float.MaxValue;
    }
    public bool TryGetWithin(float from, float radius, out float position)
        => TryGetNearest(from, out position, out float distance) && distance <= radius;
    public void GetPlayersWithin(float from, float radius, List<UWBPlayerController> result)
    {
        result.Clear();
        foreach (var player in All)
            if (player.Active && Math.Abs(player.Position - from) <= radius) result.Add(player);
    }
}
class Body
{
    public float Position { get; set; }
    public Players Players { get; } = new Players();
    public BossSettings Settings { get; } = new BossSettings();
    public void DropDisguise() { }
}
static class BossBody { public static float FlatDistance(float a, float b) => Math.Abs(a - b); }
interface IBossAction { bool Tick(); void Stop(); bool Touchable { get; } bool CountsTouches { get; } }
class ActionDouble : IBossAction
{
    public bool Touchable { get; set; } = true;
    public bool CountsTouches { get; set; } = true;
    public bool Done { get; set; }
    public bool IsEmpty { get; set; }
    public bool IsBlocked { get; set; }
    public bool CanShoot { get; set; }
    public bool CanDisguise { get; set; }
    public bool Grouped { get; set; }
    public bool HideAvailable { get; set; }
    public bool HasExited { get; set; }
    public bool Hid { get; set; }
    public bool AllowHiding { get; private set; } = true;
    public float EscapeSeconds { get; private set; }
    public int Warps { get; private set; }
    public int Starts { get; private set; }
    public Action OnTick { get; set; }
    public bool Tick() { OnTick?.Invoke(); return !Done; }
    public void Stop() { }
    public void Begin(float escapeSeconds = 0f) { EscapeSeconds = escapeSeconds; Done = false; IsBlocked = false; Starts++; }
    public void Throw(bool catchableWindup = false) { Touchable = catchableWindup; Begin(); }
    public void Warp(float clearance, bool allowHiding = true) { AllowHiding = allowHiding; Touchable = false; Warps++; Begin(); }
    public bool Huddled() => Grouped;
    public bool TryFind(float reach, bool aheadOnly = false) => HideAvailable;
    public void ObservePlayers() { }
}
public class BossBalanceFixture
{
    private readonly Body body = new Body();
    private readonly ActionDouble run = new ActionDouble();
    private readonly ActionDouble shooting = new ActionDouble { Touchable = false };
    private readonly ActionDouble smokeBomb = new ActionDouble { Touchable = false };
    private readonly ActionDouble disguising = new ActionDouble();
    private readonly ActionDouble hiding = new ActionDouble { Touchable = false };
    private readonly ActionDouble runOff = new ActionDouble { Touchable = false };
    private readonly List<UWBPlayerController> nearPlayers = new List<UWBPlayerController>();
    private IBossAction doing;
    private BossSkill? skillAfterWarp;
    private float nextSmokeAt = 100, shootReadyAt, disguiseReadyAt, hideReadyAt, actionReadyAt, catchableAt;
    private bool hideEscapePending;
    private bool firstTouchDodged = true, disguisedLast, HasLeft;
    private event Action<UWBPlayerController> Touched;
    private int catches;
    private BossBalanceFixture() { doing = run; Touched += player => catches++; }
    private void AddPlayer(float position) { body.Players.All.Add(new UWBPlayerController { Position = position }); }
    private static void Require(bool passed, string message) { if (!passed) throw new Exception(message); }
    private void EndAction(ActionDouble action) { doing = action; action.HasExited = action == hiding; action.Done = true; Update(); }

    /* CONTROLLER_METHODS */

    public static void RunAll()
    {
        foreach (int fps in new[] { 30, 60, 120 })
        {
            Time.deltaTime = 1f / fps;
            Time.time = 0;
            var trapped = new BossBalanceFixture(); trapped.AddPlayer(0.8f);
            trapped.EndAction(trapped.shooting);
            float escapeAt = trapped.actionReadyAt;
            Require(escapeAt >= trapped.catchableAt + trapped.body.Settings.CatchWindowSeconds, "Catch window not guaranteed");
            trapped.run.IsBlocked = true;
            Time.time = Time.deltaTime; trapped.Update();
            Require(trapped.doing == trapped.smokeBomb && trapped.smokeBomb.Starts == 1 && Time.time < escapeAt, "Blocked boss waited instead of starting throw");
            Require(trapped.smokeBomb.Touchable, "Trapped smoke windup offers no catch opportunity");
            trapped.body.Players.All[0].Position = 0.5f;
            Time.time += Time.deltaTime; trapped.Update();
            Require(trapped.catches == 0, "Starting smoke erased exit protection");
            Time.time = trapped.catchableAt + Time.deltaTime; trapped.Update();
            Require(trapped.catches == 1 && !trapped.smokeBomb.Touchable, "Catch during throw did not become protected warp");

            Time.time = 0;
            var running = new BossBalanceFixture(); running.EndAction(running.shooting);
            for (int frame = 1; frame * Time.deltaTime < running.actionReadyAt; frame++)
            { Time.time = frame * Time.deltaTime; running.Update(); }
            Require(running.doing == running.run, "Normal running window removed");

            Time.time = 0;
            var abandonedBox = new BossBalanceFixture(); abandonedBox.AddPlayer(0.8f);
            abandonedBox.doing = abandonedBox.hiding;
            abandonedBox.hiding.Done = true;
            abandonedBox.run.OnTick = () => abandonedBox.body.Position -= 1.4f * Time.deltaTime;
            abandonedBox.Update();
            Require(abandonedBox.doing == abandonedBox.run && abandonedBox.body.Position < 0, "Abandoned hiding approach did not flee in the same frame");
            Require(!abandonedBox.hideEscapePending && abandonedBox.run.EscapeSeconds == 0, "Abandoned approach was treated as a hidden exit");

            Time.time = 0;
            var team = new BossBalanceFixture(); team.AddPlayer(-1); team.AddPlayer(1);
            team.run.IsBlocked = true; team.EndAction(team.hiding);
            Require(team.catchableAt == team.body.Settings.ExitGraceSeconds, "Hide exit does not use the thief-style head start");
            Require(team.run.EscapeSeconds == team.body.Settings.ExitGraceSeconds, "Hide exit did not start protected running");
            team.body.Players.All[1].Position = 0.2f;
            team.run.OnTick = () => team.body.Position += 1.4f * Time.deltaTime;
            for (int frame = 1; frame <= fps + 1; frame++) { Time.time = frame * Time.deltaTime; team.Update(); }
            Require(!team.hideEscapePending, "Actual run did not open catches");
            team.body.Players.All[1].Position = team.body.Position + 0.5f;
            Time.time += Time.deltaTime; team.Update();
            Require(team.catches == 1, "Teammate cannot intercept after hide exit");

            Time.time = 0;
            var boxed = new BossBalanceFixture(); boxed.AddPlayer(0); boxed.EndAction(boxed.hiding);
            boxed.run.IsBlocked = true; Time.time = 0.3f; boxed.Update();
            Require(boxed.doing == boxed.run && boxed.smokeBomb.Starts == 0 && boxed.catches == 0, "Hide exit skipped its first second of running");
            boxed.body.Players.All[0].Position = 0.8f;
            Time.time = 1.01f; boxed.Update();
            Require(boxed.doing == boxed.smokeBomb, "Still blocked after head start did not escape");
            boxed.smokeBomb.Hid = true; boxed.smokeBomb.Done = true; boxed.Update();
            Require(boxed.doing == boxed.hiding && !boxed.hideEscapePending, "Smoke did not hand control to hiding");

            Time.time = 0;
            var camper = new BossBalanceFixture(); camper.AddPlayer(0); camper.EndAction(camper.hiding);
            camper.run.OnTick = () => camper.body.Position += 1.4f * Time.deltaTime;
            for (int frame = 1; frame <= fps; frame++) { Time.time = frame * Time.deltaTime; camper.Update(); }
            Require(camper.catches == 0, "A stationary solo camper catches the normal hide escape");

            var hit = new BossBalanceFixture(); hit.AddPlayer(0.59f);
            hit.run.OnTick = () => hit.body.Position = -10;
            hit.Update(); Require(hit.catches == 1 && hit.body.Position == 0, "Movement stole an existing touch");
            var outside = new BossBalanceFixture(); outside.AddPlayer(0.61f); outside.Update();
            Require(outside.catches == 0, "Catch radius expanded");
            Console.WriteLine($"PASS {fps} FPS: immediate trapped throw, catch during windup, normal running window, teammate interception, touch priority.");
        }
        foreach (string state in new[] { "shoot", "smoke", "hidden", "runoff", "disguised", "wrapping", "running", "approachingHide" })
        {
            Time.time = 20;
            var h = new BossBalanceFixture(); h.AddPlayer(0.2f);
            switch (state)
            {
                case "shoot": h.doing = h.shooting; break;
                case "smoke": h.doing = h.smokeBomb; break;
                case "hidden": h.doing = h.hiding; break;
                case "runoff": h.doing = h.runOff; break;
                case "disguised": h.doing = h.disguising; h.disguising.CountsTouches = false; break;
                case "wrapping": h.doing = h.disguising; break;
                case "approachingHide": h.doing = h.hiding; h.hiding.Touchable = true; break;
            }
            h.Update();
            bool scores = state == "wrapping" || state == "running" || state == "approachingHide";
            Require(h.catches == (scores ? 1 : 0), "Wrong catch eligibility: " + state);
        }
        var first = new BossBalanceFixture(); first.firstTouchDodged = false; first.AddPlayer(0);
        first.Update(); Require(first.catches == 0 && first.smokeBomb.Warps == 1, "First-touch dodge changed");
        first.doing = first.run; first.Update(); Require(first.catches == 1, "Second touch cannot score");
        var winner = new BossBalanceFixture(); winner.AddPlayer(0); winner.Touched += player => winner.doing = null;
        winner.Update(); Require(winner.catches == 1 && winner.doing == null && winner.smokeBomb.Warps == 0, "Winning catch restarted boss");
        var pending = new BossBalanceFixture(); pending.AddPlayer(1); pending.Use(BossSkill.Shoot);
        Require(pending.doing == pending.smokeBomb && pending.shooting.Starts == 0, "Skill did not warp first");
        Require(!pending.smokeBomb.AllowHiding, "Queued shot was redirected into hiding");
        pending.body.Players.All[0].Position = 10; pending.smokeBomb.Done = true; pending.Update();
        Require(pending.doing == pending.shooting && pending.shooting.Starts == 1, "Skill did not resume after warp");
        foreach (BossSkill skill in new[] { BossSkill.Shoot, BossSkill.Disguise })
        {
            var far = new BossBalanceFixture(); far.AddPlayer(2.5f); far.Use(skill);
            Require(far.smokeBomb.Warps == 0 && (skill == BossSkill.Shoot ? far.shooting.Starts : far.disguising.Starts) == 1, "Distant skill unnecessarily warped");
            var near = new BossBalanceFixture(); near.AddPlayer(1); near.Use(skill);
            Require(near.smokeBomb.Warps == 1 && near.shooting.Starts + near.disguising.Starts == 0, "Close skill skipped required warp");
            var boundary = new BossBalanceFixture(); boundary.AddPlayer(boundary.body.Settings.SkillClearance); boundary.Use(skill);
            Require(boundary.smokeBomb.Warps == 1, "Clearance boundary is unsafe");
            var empty = new BossBalanceFixture(); empty.Use(skill);
            Require(empty.smokeBomb.Warps == 0, "Empty arena caused unnecessary warp");
            var secondPlayer = new BossBalanceFixture(); secondPlayer.AddPlayer(3); secondPlayer.AddPlayer(1); secondPlayer.Use(skill);
            Require(secondPlayer.smokeBomb.Warps == 1, "Nearby second player ignored");
        }
        var disguiseAtRange = new BossBalanceFixture(); disguiseAtRange.AddPlayer(2.5f); disguiseAtRange.disguising.CanDisguise = true;
        disguiseAtRange.Update();
        Require(disguiseAtRange.doing == disguiseAtRange.disguising && disguiseAtRange.smokeBomb.Warps == 0, "Automatic disguise always warps");
        Console.WriteLine("PASS: shoot/disguise direct at safe range, warp near or at boundary, check all players, automatic disguise without warp.");
        var cover = new BossBalanceFixture(); cover.AddPlayer(1); cover.hiding.HideAvailable = true;
        cover.shooting.CanShoot = true; cover.shooting.Grouped = true; cover.Update();
        Require(cover.doing == cover.hiding && cover.shooting.Starts == 0, "Safe forward box did not take priority");
        var cooldown = new BossBalanceFixture(); cooldown.AddPlayer(1); cooldown.hiding.HideAvailable = true;
        cooldown.hideReadyAt = Time.time + 10; cooldown.Update();
        Require(cooldown.hiding.Starts == 0, "Forward hiding bypassed its cooldown");
        var blockedDisguise = new BossBalanceFixture(); blockedDisguise.doing = blockedDisguise.disguising;
        blockedDisguise.disguising.IsBlocked = true; blockedDisguise.Update();
        Require(blockedDisguise.doing == blockedDisguise.smokeBomb && blockedDisguise.smokeBomb.Warps == 1, "Blocked disguise did not escape");
        Console.WriteLine("PASS: eligibility matrix, first-touch dodge, winning catch, warp-before-skill sequence.");
        Console.WriteLine("Scope: actual controller methods, deterministic action/player doubles; not a full Unity playtest or measured win rate.");
    }
}
