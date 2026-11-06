$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bossFolder = Join-Path $projectRoot 'Assets/Scripts/BountyHunter/Boss'
$controller = Get-Content (Join-Path $bossFolder 'BossController.cs') -Raw
$settings = Get-Content (Join-Path $bossFolder 'BossSettings.cs') -Raw
$scene = Get-Content (Join-Path $projectRoot 'Assets/Scenes/BountyHunter.unity') -Raw

function Read-Method([string] $name, [string] $source = $controller) {
    $match = [regex]::Match($source, "(?:private|public|internal) (?:static )?(?:void|bool|float) $name\([^)]*\)\s*\{")
    if (!$match.Success) { throw "Missing controller method: $name" }
    $start = $match.Index
    $depth = 1
    $end = $match.Index + $match.Length
    while ($depth -gt 0) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    $source.Substring($start, $end - $start)
}

# Execute the production controller methods against deterministic clock, action and player doubles.
# This checks decisions and timing, not Unity rendering, tracking latency or the full movement AI.
$methods = @('Update', 'CheckTouches', 'WarpAway', 'React', 'Use', 'PlayerNearSkillPosition', 'StartRunning') |
    ForEach-Object { Read-Method $_ }
$fixture = Get-Content (Join-Path $PSScriptRoot 'BossBalanceFixture.cs') -Raw
foreach ($field in @('exitGraceSeconds', 'catchWindowSeconds', 'actionGapSeconds', 'touchRadius', 'fleeRadius', 'skillClearance')) {
    $value = [regex]::Match($scene, "(?m)^    ${field}: ([0-9.]+)\r?$")
    if (!$value.Success) { $value = [regex]::Match($settings, "private float $field = ([0-9.]+)f;") }
    if (!$value.Success) { throw "Missing setting: $field" }
    $fixture = $fixture.Replace("__$field", $value.Groups[1].Value + 'f')
}
Add-Type -TypeDefinition ($fixture.Replace('/* CONTROLLER_METHODS */', ($methods -join "`n")))
[BossBalanceFixture]::RunAll()

$smokeSource = Get-Content (Join-Path $bossFolder 'BossSmokeBomb.cs') -Raw
$smokeMethods = (Read-Method 'Throw' $smokeSource) + (Read-Method 'Begin' $smokeSource)
$touchable = [regex]::Match($smokeSource, 'public bool Touchable => [^;]+;').Value
$smokeFixture = @'
#pragma warning disable CS0414
using System;
struct Vector2Int { public int x { get; set; } public int y { get; set; } }
class SmokeSettingsDouble {
    public int VanishAtFrame => 40;
    public float FleeRadius => 3.2f;
    public int WarpFromFrame => 34;
    public Vector2Int SmokeFrames => new Vector2Int { x = 20, y = 76 };
}
class SmokeAnimatorDouble {
    public int Frame { get; set; }
    public void Play(int[] frames, float fps, Vector2Int stretch, bool loop) { Frame = stretch.x; }
}
class SmokeBodyDouble {
    public SmokeSettingsDouble Settings { get; } = new SmokeSettingsDouble();
    public SmokeAnimatorDouble Animator { get; } = new SmokeAnimatorDouble();
}
class SmokeMoveDouble {
    public int[] Frames => new int[90];
    public float FramesPerSecond => 15;
}
public class SmokeWindupCheck {
    private readonly SmokeBodyDouble body = new SmokeBodyDouble();
    private readonly SmokeMoveDouble smoke = new SmokeMoveDouble();
    private bool teleported, betweenClouds, cloudLingering, catchableWindup;
    private bool allowHiding;
    public bool Hid { get; private set; }
    private float playerClearance;
    public static void Run() {
        var check = new SmokeWindupCheck();
        check.Throw(true);
        if (!check.Touchable || check.body.Animator.Frame != 34) throw new Exception("Emergency escape waited through bomb preparation");
        check.body.Animator.Frame = 39;
        if (!check.Touchable) throw new Exception("Catch window ended before smoke cover");
        check.body.Animator.Frame = 40;
        if (check.Touchable) throw new Exception("Catch possible after smoke cover");
        check.Begin(true);
        if (check.Touchable) throw new Exception("Arrival is vulnerable");
        check.Throw();
        if (check.body.Animator.Frame != 20) throw new Exception("Normal smoke throw lost its preparation animation");
        if (check.Touchable) throw new Exception("Normal throw changed immunity");
        check.Throw(true); check.betweenClouds = true;
        if (check.Touchable) throw new Exception("Invisible boss is vulnerable");
        Console.WriteLine("PASS: production smoke Throw/Begin/Touchable, animation starts immediately, catch window closes at cover, normal throw/arrival protected.");
    }
'@
Add-Type -TypeDefinition ($smokeFixture + $touchable + $smokeMethods + '}')
[SmokeWindupCheck]::Run()

$destinationCondition = [regex]::Match($smokeSource, 'if \((allowHiding && \(!far[^\r\n]+)\)').Groups[1].Value.Replace('Random.value', 'roll')
if (!$destinationCondition) { throw 'Missing smoke destination roll' }
$rollFixture = @'
using System;
public static class DestinationRollCheck {
    private static bool Choose(float roll, bool allowHiding, Func<bool> tryHide, bool far = true) {
        return /* CONDITION */;
    }
    public static void Run() {
        int hidden = 0, attempts = 0;
        for (int i = 0; i < 1000; i++)
            if (Choose((i + 0.5f) / 1000, true, () => { attempts++; return true; })) hidden++;
        if (hidden != 300 || attempts != 300) throw new Exception("Destination split differs from 70/30");
        if (!Choose(0.99f, true, () => true, false)) throw new Exception("No far destination failed to force safe hiding fallback");
        if (Choose(0.99f, true, () => false, false)) throw new Exception("Unavailable hiding prevented floor fallback");
        if (Choose(0.99f, false, () => { throw new Exception("Skill prep used fallback hide"); }, false)) throw new Exception("Unexpected hiding");
        if (Choose(0.1f, true, () => false)) throw new Exception("Missing safe bush did not fall back to floor");
        if (Choose(0.1f, false, () => { throw new Exception("Skill preparation tried to hide"); })) throw new Exception("Unexpected hiding");
        if (Choose(0.3f, true, () => true) || Choose(0.1f, true, null)) throw new Exception("Boundary or missing provider failed");
        Console.WriteLine("PASS: destination threshold gives 700 floor / 300 hide over 1000 evenly spaced rolls; unsafe bush falls back; skill preparation stays on floor.");
    }
}
'@
Add-Type -TypeDefinition ($rollFixture.Replace('/* CONDITION */', $destinationCondition))
[DestinationRollCheck]::Run()

$hidingSource = Get-Content (Join-Path $bossFolder 'BossHiding.cs') -Raw
$hidingMethods = (Read-Method 'TryWarpIn' $hidingSource) + (Read-Method 'EnterHideout' $hidingSource) + (Read-Method 'CanReachFirst' $hidingSource)
$hidingFixture = Get-Content (Join-Path $PSScriptRoot 'BossWarpHideFixture.cs') -Raw
Add-Type -TypeDefinition ($hidingFixture.Replace('/* HIDING_METHODS */', $hidingMethods))
[WarpHideChecks.Check]::Run()

$movementSource = Get-Content (Join-Path $bossFolder 'BossBody.cs') -Raw
$movementMethods = (Read-Method 'Step' $movementSource) + (Read-Method 'MoveTo' $movementSource) + (Read-Method 'RouteDistance' $movementSource)
$movementFixture = Get-Content (Join-Path $PSScriptRoot 'BossEscapeMovementFixture.cs') -Raw
Add-Type -TypeDefinition ($movementFixture.Replace('/* MOVEMENT_METHODS */', $movementMethods))
[EscapeMovementChecks.Check]::Run()

$disguiseSource = Get-Content (Join-Path $bossFolder 'BossDisguising.cs') -Raw
$stroll = Read-Method 'Stroll' $disguiseSource
$disguiseFixture = Get-Content (Join-Path $PSScriptRoot 'BossDisguiseEscapeFixture.cs') -Raw
Add-Type -TypeDefinition ($disguiseFixture.Replace('/* STROLL_METHOD */', $stroll))
[DisguiseEscapeChecks.Check]::Run()

$runSource = Get-Content (Join-Path $bossFolder 'BossRun.cs') -Raw
$escape = Read-Method 'TryEscapeStep' $runSource
$routeFixture = Get-Content (Join-Path $PSScriptRoot 'BossEscapeRouteFixture.cs') -Raw
$blocked = [regex]::Match($runSource, 'public bool IsBlocked => [^;]+;').Value
Add-Type -TypeDefinition ($routeFixture.Replace('/* ESCAPE_METHOD */', $escape + $blocked))
[EscapeRouteChecks.Check]::Run()

$destinationMethod = Read-Method 'TryFindFarSpot' $smokeSource
$distanceFixture = Get-Content (Join-Path $PSScriptRoot 'BossWarpDistanceFixture.cs') -Raw
Add-Type -TypeDefinition ($distanceFixture.Replace('/* DESTINATION_METHOD */', $destinationMethod))
[WarpDistanceChecks.Check]::Run()
