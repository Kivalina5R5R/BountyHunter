using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BountyHunter.Thieves;

namespace UnityEngine
{
    public class Object
    {
        private bool destroyed;
        public string name { get; set; }
        public static void Destroy(Object value) { if (!ReferenceEquals(value, null)) value.destroyed = true; }
        public static bool operator ==(Object a, Object b) => (ReferenceEquals(a, null) || a.destroyed) ? ReferenceEquals(b, null) || b.destroyed : ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object value) => ReferenceEquals(this, value);
        public override int GetHashCode() => base.GetHashCode();
    }
    public class MonoBehaviour : Object { }
    public class GameObject : Object
    {
        public GameObject(string name) { this.name = name; }
        public T AddComponent<T>() where T : new() => new T();
    }
    public class SerializeField : Attribute { }
    public class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public struct Vector2 { public Vector2(float x, float y) { } }
    public struct Rect { public Rect(float x, float y, float width, float height) { } }
    public enum SpriteMeshType { FullRect }
    public class Texture2D : Object { public int width => 100; public int height => 20; }
    public class Sprite : Object
    {
        public static int Creations { get; private set; }
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float ppu, int border, SpriteMeshType type)
        { Creations++; return new Sprite(); }
    }
    public static class Mathf
    {
        public static int CeilToInt(float n) => (int)Math.Ceiling(n);
        public static int Min(int a, int b) => Math.Min(a, b);
    }
    public static class Debug { public static void LogException(Exception error, Object owner) { } }
}
namespace Unity.Profiling
{
    public struct ProfilerMarker
    {
        public ProfilerMarker(string name) { }
        public Scope Auto() => new Scope();
        public struct Scope : IDisposable { public void Dispose() { } }
    }
}
public static class ScenePreloadChecks
{
    private static readonly List<string> events = new List<string>();
    private static void Require(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    private static IEnumerator Work(string label, int steps)
    {
        for (int i = 0; i < steps; i++) { events.Add(label); yield return null; }
    }
    private static IEnumerator Fault()
    {
        yield return null;
        throw new InvalidOperationException("Expected preparation failure");
    }
    private static object Runner => typeof(ScenePreload).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    private static void Tick() => typeof(ScenePreload).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Runner, null);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    public static void Run()
    {
        var owner = new UnityEngine.Object();
        var later = ScenePreload.Enqueue(owner, 100, Work("later", 3));
        var first = ScenePreload.Enqueue(owner, 0, Work("first", 1));
        Tick(); Require(events.Count == 1 && events[0] == "first", "Did not prioritize startup work or ran several steps in one frame");
        Tick(); Require(first.Completed, "Completed work stayed in queue");
        Tick(); Require(events.Count == 2 && events[1] == "later", "Background work did not advance");
        var imminent = ScenePreload.Enqueue(owner, 200, Work("imminent", 1));
        imminent.Prioritize(); Tick();
        Require(events[2] == "imminent", "Early consumer did not preempt background work");
        Tick();
        while (!later.Completed) Tick();
        var broken = ScenePreload.Enqueue(owner, 0, Fault());
        Tick(); Tick(); Require(broken.Completed && broken.Failed, "Fault left queue blocked");
        var goneOwner = new UnityEngine.Object();
        var gone = ScenePreload.Enqueue(goneOwner, 0, Work("should not run", 1));
        UnityEngine.Object.Destroy(goneOwner); int before = events.Count;
        Tick(); Require(gone.Completed && events.Count == before, "Destroyed owner continued preparing assets");
        var cancelled = ScenePreload.Enqueue(owner, 0, Work("cancelled", 1));
        cancelled.Cancel(); Tick();
        Require(events.Count == before, "Hidden video's cancelled preparation still ran");
        var unloading = ScenePreload.Enqueue(owner, 0, Work("unloading", 2));
        typeof(ScenePreload).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Runner, null);
        Require(unloading.Completed && Runner == null, "Scene unload retained pending work");

        var sheet = new BossSheet();
        Set(sheet, "texture", new UnityEngine.Texture2D { name = "Test" });
        Set(sheet, "frames", 20);
        IEnumerator loading = sheet.Preload(100f);
        before = UnityEngine.Sprite.Creations;
        Require(loading.MoveNext() && UnityEngine.Sprite.Creations - before == 8, "Sprite preparation was not bounded to eight per frame");
        IEnumerator second = sheet.Preload(100f);
        second.MoveNext();
        var frames = sheet.Slice(100f);
        Require(frames.Length == 20 && UnityEngine.Sprite.Creations - before == 20, "Early consumer duplicated sprites or received incomplete data");
        Require(!loading.MoveNext() && !second.MoveNext(), "Already prepared sheets kept doing work");
        Require(ReferenceEquals(frames, sheet.Slice(100f)), "Prepared sprites were not reused");
        sheet.Release();
        foreach (var sprite in frames) Require(sprite == null, "Generated sprite leaked after owner unload");
        var empty = new BossSheet();
        Require(empty.Slice(100f).Length == 0 && !empty.Preload(100f).MoveNext(), "Empty optional sheet did not complete");
        Console.WriteLine("PASS: actual preload queue priority, one step/frame, promotion, failure and scene cleanup; actual sprite batching, early consumption, cache reuse and release.");
    }
}
