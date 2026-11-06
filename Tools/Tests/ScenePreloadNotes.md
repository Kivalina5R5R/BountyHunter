# Staged startup preparation

Code inspection found a single enabled build scene (`BountyHunter.unity`) and no runtime `SceneManager.LoadScene` calls in `Assets/Scripts`. This change targets initialization and first-use work, not disk-loading time of the scene's serialized dependencies. No timing improvement has been measured on the target device.

## Changes

- `WallVideo` previously prepared its primary and seamless-loop decoder together in `Awake`. Decoder preparation now uses a shared priority queue: non-looping arrival first, visible looping videos next, spare decoders afterwards. A requested playback promotes its existing preparation. Preparation waits have an eight-second bound so a failed decoder cannot monopolize the queue forever. A loop can use ordinary looping until its spare is ready. Disabling a wall cancels queued work; re-enabling checks the actual decoder readiness before resuming.
- `IntroSequence` prewarms the shared procedural dot, pin and ring before their first gameplay use. Their pixel equations are unchanged, but 21,504 individual `SetPixel` calls become three `SetPixels` calls; generated textures release their CPU pixel copies after upload.
- `BossChase` prepares its five sheets in the background, in order: walk, smoke, shoot, wrap, reveal. `BossSheet` creates at most eight sprites in each queue step and shares completed/partial work with early consumers. `MapSwitcher` waits behind the curtain before starting an interlude or finale; an early jump promotes the existing job rather than rebuilding the sheets. Generated sprites are destroyed with the chase owner.
- `ScenePreload` runs one small iterator step per rendered frame on the main thread, not on a worker thread. `BountyHunter.PreloadStep` is a Profiler marker. This bounds the number of operations, not the time of an individual Unity/native operation.

## Automated checks

```powershell
pwsh -NoProfile -File Tools/Tests/Test-ScenePreload.ps1
pwsh -NoProfile -File Tools/Tests/Test-BossBalance.ps1
dotnet build Assembly-CSharp.csproj --no-restore -v quiet -p:CustomAfterMicrosoftCommonTargets=D:\Game\Bounty_Hunter\Tools\Tests\IncludeScenePreload.targets
git diff --check
```

The targets file only includes the new script in command-line compilation before Unity regenerates its `.csproj`. It does not alter Unity's scene, asset references or build settings.

The preload test compiles the actual queue and sheet code against lightweight Unity doubles. It checks priority, promotion, one-step-per-frame execution, fault handling, cancellation, owner destruction, scene teardown, eight-sprite batches, interrupted preparation, cache reuse and cleanup. It does not measure video decode, GPU upload, rendering or real startup duration.

## Target-device validation still required

Capture a Development Build cold launch and the first intro-to-game, lineup, boss and result transitions. Compare main-thread frame time, `BountyHunter.PreloadStep`, texture upload, video preparation and GC allocation. Exercise Enter during the intro, early interlude jumps, hiding/reopening a video wall mid-prepare and a scene reload while boss sheets are preparing.

Large textures, audio, animator controllers and clips are still directly referenced by the scene/prefabs and therefore loaded as scene dependencies. True deferred disk loading for those assets needs a separate asset-reference migration (for example Addressables or additive scenes), with import/build/reference validation; adding a runtime coroutine cannot defer that serialization work. No serialized scene/prefab/import settings were changed here.
