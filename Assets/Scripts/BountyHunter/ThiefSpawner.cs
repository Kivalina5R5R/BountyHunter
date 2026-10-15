using System;
using System.Collections.Generic;
using Fortal.Display;
using Fortal.Players;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // Spawns the thieves at random spots on the floor when Play starts and turns every bush from
    // BushScatter into a hiding spot that rustles, shakes and throws leaves (BushReaction) and
    // creeps away from players while a thief is inside (BushWalker). Any number of thief types can be listed, each a prefab
    // with a ThiefController and its own count. Console lines start with [ThiefSpawner].
    // Runs after the player spawner so spawn points can keep away from players already placed.
    [DefaultExecutionOrder(10)]
    public sealed class ThiefSpawner : MonoBehaviour, IHidingSpots, IThiefCrowd
    {
        [Serializable]
        private sealed class ThiefType
        {
            [SerializeField] private ThiefController prefab;
            [SerializeField, Min(0)] private int count = 3;

            public ThiefController Prefab => prefab;
            public int Count => count;
        }

        [SerializeField] private UWBPlayerSpawner playerSpawner;
        [SerializeField] private BushScatter bushScatter;
        [SerializeField] private ThiefType[] thiefTypes;

        [Header("Floor (world X/Z)")]
        // Vector2.y is world Z. Thieves spawn and move inside this area.
        [SerializeField] private Vector2 areaMin = new Vector2(-5.15f, -2.3f);
        [SerializeField] private Vector2 areaMax = new Vector2(5.15f, 2.3f);
        [SerializeField, Min(0f)] private float spawnClearanceFromPlayers = 2f;
        [SerializeField, Min(0f)] private float spawnClearanceFromBushes = 0.3f;
        [SerializeField, Min(0f)] private float spawnClearanceFromThieves = 0.9f;
        [SerializeField] private float height = 0.04f;

        [Header("Bushes")]
        [SerializeField, Min(0.1f)] private float secondsToFlushBush = 2f;
        // A route to a bush that passes closer than this to any player is not safe.
        [SerializeField, Min(0f)] private float routeClearance = 0.9f;
        // Leaves thrown out when a thief jumps into a bush; leave empty for no leaf burst.
        [SerializeField] private Sprite[] leafSprites;

        [Header("Thieves")]
        // The cartoon scuffle played where a thief is caught; leave the clouds empty for none.
        [SerializeField] private CatchBrawl.Art catchBrawl;
        // Dust puffs kicked up under running thieves; leave empty for no dust.
        [SerializeField] private Sprite[] dustSprites;

        private readonly List<HidingBush> hidingBushes = new List<HidingBush>();
        private readonly List<ThiefController> thieves = new List<ThiefController>();
        private IPlayerLocator players;

        public IReadOnlyList<ThiefController> Thieves => thieves;
        public IReadOnlyList<HidingBush> Bushes => hidingBushes;

        private void Start()
        {
            players = new SpawnerPlayerLocator(playerSpawner);
            if (bushScatter != null)
            {
                bushScatter.Clearing += EvictHiddenThieves;
                bushScatter.Scattered += RegisterBushes;
                RegisterBushes();
            }

            SpawnThieves();
        }

        private void OnDestroy()
        {
            if (bushScatter == null) return;
            bushScatter.Clearing -= EvictHiddenThieves;
            bushScatter.Scattered -= RegisterBushes;
        }

        public HidingBush FindHidingBush(ThiefController thief, IReadOnlyList<Vector3> playerPositions, HidingBush excluded)
        {
            Vector3 position = thief.transform.position;
            HidingBush best = null;
            float bestDistance = float.MaxValue;
            foreach (HidingBush bush in hidingBushes)
            {
                if (bush == null || bush == excluded || !bush.IsFreeFor(thief)) continue;

                float thiefDistance = SpawnerPlayerLocator.FlatDistance(position, bush.Center);
                if (thiefDistance >= bestDistance || !SafeRoute(position, bush.Center, thiefDistance, playerPositions)) continue;

                best = bush;
                bestDistance = thiefDistance;
            }

            return best;
        }

        // Safe when the thief gets there clearly before every player and never runs close past one.
        private bool SafeRoute(Vector3 from, Vector3 to, float length, IReadOnlyList<Vector3> playerPositions)
        {
            foreach (Vector3 player in playerPositions)
            {
                if (length >= SpawnerPlayerLocator.FlatDistance(player, to) * 0.85f) return false;
                if (DistanceToSegment(player, from, to) < routeClearance) return false;
            }

            return true;
        }

        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            var p = new Vector2(point.x, point.z);
            var start = new Vector2(a.x, a.z);
            var end = new Vector2(b.x, b.z);
            Vector2 line = end - start;
            float t = line.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - start, line) / line.sqrMagnitude) : 0f;
            return Vector2.Distance(p, start + line * t);
        }

        // A new bush layout is about to replace the bushes; thieves hiding in them come out first.
        private void EvictHiddenThieves()
        {
            foreach (HidingBush bush in hidingBushes)
            {
                if (bush != null) bush.EvictOccupant();
            }
        }

        // Re-reads the bushes; called again whenever BushScatter makes a new layout.
        private void RegisterBushes()
        {
            hidingBushes.Clear();
            foreach (ScatteredBush scattered in bushScatter.Bushes)
            {
                if (scattered.Transform == null) continue;

                HidingBush bush = scattered.Transform.GetComponent<HidingBush>();
                if (bush == null) bush = scattered.Transform.gameObject.AddComponent<HidingBush>();
                bush.Initialize(players, scattered.Radius, secondsToFlushBush);
                BushReaction reaction = bush.GetComponent<BushReaction>();
                if (reaction == null) bush.gameObject.AddComponent<BushReaction>().Initialize(leafSprites);
                BushWalker walker = bush.GetComponent<BushWalker>();
                if (walker == null) walker = bush.gameObject.AddComponent<BushWalker>();
                walker.Initialize(players, hidingBushes, bushScatter.AreaMin, bushScatter.AreaMax);
                hidingBushes.Add(bush);
            }

            Debug.Log($"[ThiefSpawner] {hidingBushes.Count} bushes ready for hiding ({secondsToFlushBush:0.#}s on a bush flushes its thief)", this);
        }

        // Sprites thrown out of hiding spots registered from now on (leaves for bushes, trash for bins).
        public void UseBurstSprites(Sprite[] sprites)
        {
            leafSprites = sprites;
        }

        // Removes every thief, hidden or not, without any exit effects; used when the map changes.
        public void RemoveThieves()
        {
            foreach (HidingBush bush in hidingBushes)
            {
                if (bush != null && bush.Occupant != null) bush.Leave(bush.Occupant);
            }

            foreach (ThiefController thief in thieves)
            {
                if (thief == null) continue;
                thief.Caught -= OnThiefCaught;
                Destroy(thief.gameObject);
            }

            thieves.Clear();
        }

        public void SpawnThieves()
        {
            if (thiefTypes == null) return;

            foreach (ThiefType type in thiefTypes)
            {
                if (type == null || type.Prefab == null) continue;

                for (int i = 0; i < type.Count; i++)
                {
                    Spawn(type.Prefab, i + 1);
                }
            }

            Debug.Log($"[ThiefSpawner] Spawned {thieves.Count} thieves", this);
        }

        private void Spawn(ThiefController prefab, int number)
        {
            Vector3 position = FindSpawnPoint();
            ThiefController thief = Instantiate(prefab, position, Quaternion.identity, transform);
            thief.name = $"{prefab.TypeName}{number:00}";
            thief.Initialize(players, this, this, areaMin, areaMax);
            thief.gameObject.AddComponent<DustTrail>().Initialize(dustSprites, thief.Body);
            thief.Caught += OnThiefCaught;
            thieves.Add(thief);
            Debug.Log($"[ThiefSpawner] Spawned {thief.name} at {position.x:0.00}, {position.z:0.00}", this);
        }

        // Tries random spots that are away from players and not on a bush; falls back to the last try.
        private Vector3 FindSpawnPoint()
        {
            Vector3 point = Vector3.zero;
            for (int attempt = 0; attempt < 60; attempt++)
            {
                point = new Vector3(Random.Range(areaMin.x, areaMax.x), height, Random.Range(areaMin.y, areaMax.y));
                if (players.TryGetWithin(point, spawnClearanceFromPlayers, out _)) continue;
                if (OnABush(point)) continue;
                if (NearAThief(point)) continue;
                return point;
            }

            return point;
        }

        private bool NearAThief(Vector3 point)
        {
            foreach (ThiefController thief in thieves)
            {
                if (thief != null && SpawnerPlayerLocator.FlatDistance(point, thief.transform.position) < spawnClearanceFromThieves) return true;
            }

            return false;
        }

        private bool OnABush(Vector3 point)
        {
            foreach (HidingBush bush in hidingBushes)
            {
                if (bush != null && SpawnerPlayerLocator.FlatDistance(point, bush.Center) < bush.Radius + spawnClearanceFromBushes) return true;
            }

            return false;
        }

        private void OnThiefCaught(ThiefController thief)
        {
            CatchBrawl.Play(thief.transform.position, catchBrawl, transform);
            thieves.Remove(thief);
            Debug.Log($"[ThiefSpawner] {thief.name} caught, {thieves.Count} thieves left", this);
            if (thieves.Count == 0) Debug.Log("[ThiefSpawner] ALL THIEVES CAUGHT", this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 1f);
            var center = new Vector3((areaMin.x + areaMax.x) * 0.5f, height, (areaMin.y + areaMax.y) * 0.5f);
            Gizmos.DrawWireCube(center, new Vector3(areaMax.x - areaMin.x, 0f, areaMax.y - areaMin.y));
        }
#endif
    }
}
