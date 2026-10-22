using System;
using System.Collections.Generic;
using Fortal.Display;
using Fortal.Players;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // Spawns thieves at random spots on the floor when asked (BountyRounds asks for one per WANTED
    // poster) and turns every bush from BushScatter into a hiding spot that rustles, shakes and
    // throws leaves (BushReaction) and creeps away from players while a thief is inside (BushWalker).
    // Reports each catch with the player who made it. Console lines start with [ThiefSpawner].
    // Runs after the player spawner so spawn points can keep away from players already placed.
    [DefaultExecutionOrder(10)]
    public sealed class ThiefSpawner : MonoBehaviour, IHidingSpots, IThiefCrowd, IFloorWalkers
    {
        [SerializeField] private UWBPlayerSpawner playerSpawner;
        // The passers-by the thieves keep from walking over; may be empty. Thieves come in at least
        // passerSpacing from them when they can.
        [SerializeField] private NpcCrowd passersBy;
        [SerializeField, Min(0f)] private float passerSpacing = 1.8f;
        [SerializeField] private BushScatter bushScatter;

        [Header("Floor (world X/Z)")]
        // Vector2.y is world Z. Thieves spawn and move inside this area.
        [SerializeField] private Vector2 areaMin = new Vector2(-5.15f, -2.3f);
        [SerializeField] private Vector2 areaMax = new Vector2(5.15f, 2.3f);
        [SerializeField, Min(0f)] private float spawnClearanceFromPlayers = 2f;
        [SerializeField] private float height = 0.04f;
        // With no free hiding spot to start in, a thief walks in from this far beyond the floor edge
        // to this far inside it.
        [SerializeField, Min(0f)] private float walkInFromOutside = 1f;
        [SerializeField, Min(0f)] private float walkInToInside = 0.7f;
        // An entrance with a player closer than this is not used: a thief would walk into their arms.
        [SerializeField, Min(0f)] private float safeEntranceDistance = 2.8f;
        // The thieves of one round come in at least this far apart (metres) whenever the floor allows.
        [SerializeField, Min(0f)] private float entranceSpread = 3.5f;
        // Spots along the floor edges weighed for each entrance (with the free hiding spots).
        [SerializeField, Min(1)] private int edgeCandidates = 16;

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

        // One way a thief can come in: out of a hiding spot, or walking in over a floor edge.
        private struct Entrance
        {
            public Vector3 Point;
            public HidingBush Bush;
            // Edges only: the way onto the floor.
            public Vector3 Inward;
        }

        private readonly List<HidingBush> hidingBushes = new List<HidingBush>();
        // Where thieves came in this round and last round, so entrances spread out and change.
        private readonly List<Vector3> roundEntrances = new List<Vector3>();
        private readonly List<Vector3> lastRoundEntrances = new List<Vector3>();
        private bool lastEntranceWasBush;

        // A thief waiting off the floor for its turn to come in, and the way it planned to.
        private sealed class Arrival
        {
            public ThiefController Thief;
            public Entrance Entrance;
            public float At;
        }

        private readonly List<Arrival> arrivals = new List<Arrival>();
        private readonly List<ThiefController> thieves = new List<ThiefController>();
        private readonly List<Vector3> passerPositions = new List<Vector3>();
        private SpawnerPlayerLocator players;

        public IReadOnlyList<ThiefController> Thieves => thieves;

        // Where thieves are waiting to come in, and the hiding spots one is in or heading for.
        public void AddTakenSpots(List<Vector3> spots)
        {
            foreach (Arrival arrival in arrivals)
            {
                spots.Add(arrival.Entrance.Point);
            }

            foreach (HidingBush bush in hidingBushes)
            {
                if (bush != null && (bush.Occupant != null || bush.ReservedBy != null)) spots.Add(bush.Center);
            }
        }

        // The thieves out in the open (not hidden or caught), for the passers-by to keep clear of.
        public void AddPositions(List<Vector3> positions, Transform except)
        {
            foreach (ThiefController thief in thieves)
            {
                if (thief == null || thief.transform == except || thief.IsHidden || thief.State == ThiefState.Caught) continue;
                positions.Add(thief.transform.position);
            }
        }
        public IReadOnlyList<HidingBush> Bushes => hidingBushes;

        // A thief was caught; the player is the one nearest to it, or null when none is on the floor.
        public event Action<ThiefController, UWBPlayerController> ThiefCaught;

        private void Start()
        {
            players = new SpawnerPlayerLocator(playerSpawner);
            if (bushScatter != null)
            {
                bushScatter.Clearing += EvictHiddenThieves;
                bushScatter.Scattered += RegisterBushes;
                RegisterBushes();
            }
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
        // A player behind it (chasing it away from the bush) is left behind, not run past, so only
        // players level with it or ahead along the way count; otherwise a chaser close on its heels
        // would rule out every bush, even the one right next to it.
        private bool SafeRoute(Vector3 from, Vector3 to, float length, IReadOnlyList<Vector3> playerPositions)
        {
            foreach (Vector3 player in playerPositions)
            {
                if (length >= SpawnerPlayerLocator.FlatDistance(player, to) * 0.85f) return false;
                if (IsBehind(player, from, to)) continue;
                if (DistanceToSegment(player, from, to) < routeClearance) return false;
            }

            return true;
        }

        // True when point lies behind from, looking from from towards to (flat).
        private static bool IsBehind(Vector3 point, Vector3 from, Vector3 to)
        {
            var back = new Vector2(point.x - from.x, point.z - from.z);
            var ahead = new Vector2(to.x - from.x, to.z - from.z);
            return Vector2.Dot(back, ahead) < 0f;
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
                bushScatter.CentreLimits(scattered.Reach, out Vector2 moveMin, out Vector2 moveMax);
                walker.Initialize(players, hidingBushes, moveMin, moveMax);
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
            arrivals.Clear();
            if (roundEntrances.Count == 0) return;

            lastRoundEntrances.Clear();
            lastRoundEntrances.AddRange(roundEntrances);
            roundEntrances.Clear();
        }

        // Adds one thief of this kind, unseen, and returns it. After entranceDelay seconds it sneaks out
        // of a free hiding spot or walks in over a floor edge, whichever entrance is best spread out
        // (see PickEntrance), so the players cannot guess where the next one appears.
        public ThiefController SpawnThief(ThiefController prefab, float entranceDelay)
        {
            Entrance entrance = PickEntrance();
            roundEntrances.Add(entrance.Point);
            lastEntranceWasBush = entrance.Bush != null;
            HidingBush bush = entrance.Bush;
            Vector3 position = bush != null ? entrance.Point : entrance.Point - entrance.Inward * walkInFromOutside;
            ThiefController thief = Instantiate(prefab, position, Quaternion.identity, transform);
            thief.name = prefab.TypeName;
            thief.Initialize(players, this, this, passersBy, areaMin, areaMax);
            if (bush != null)
            {
                thief.StartHiddenIn(bush, entranceDelay);
            }
            else
            {
                // Which edge it comes over is checked again when its turn comes (see Update).
                thief.WaitOffFloor();
                arrivals.Add(new Arrival { Thief = thief, Entrance = entrance, At = Time.time + entranceDelay });
            }
            thief.gameObject.AddComponent<DustTrail>().Initialize(dustSprites, thief.Body);
            thief.Caught += OnThiefCaught;
            thieves.Add(thief);
            string where = bush != null ? $"hidden in {bush.name}" : "off the floor edge";
            where += $" at ({entrance.Point.x:0.0}, {entrance.Point.z:0.0})";
            Debug.Log($"[ThiefSpawner] Spawned {thief.name} {where}", this);
            return thief;
        }

        // Lets waiting thieves in when their turn comes. A player may have gone to stand at the planned
        // edge meanwhile; then it comes in another way, a safe edge or a free hiding spot.
        private void Update()
        {
            for (int i = arrivals.Count - 1; i >= 0; i--)
            {
                Arrival arrival = arrivals[i];
                if (arrival.Thief == null)
                {
                    arrivals.RemoveAt(i);
                    continue;
                }

                if (Time.time < arrival.At) continue;
                arrivals.RemoveAt(i);

                Entrance entrance = arrival.Entrance;
                if (!Safe(entrance.Point))
                {
                    entrance = PickEntrance();
                    Debug.Log($"[ThiefSpawner] A player is waiting where {arrival.Thief.name} planned to come in; it comes in elsewhere", this);
                }

                if (entrance.Bush != null)
                {
                    arrival.Thief.StartHiddenIn(entrance.Bush, 0f);
                    continue;
                }

                Vector3 inside = entrance.Point + entrance.Inward * walkInToInside;
                Vector3 outside = entrance.Point - entrance.Inward * walkInFromOutside;
                arrival.Thief.WalkIn(outside, inside, 0f);
            }
        }

        private bool Safe(Vector3 point)
        {
            return !players.TryGetWithin(point, safeEntranceDistance, out _);
        }

        // Scores every free hiding spot (none with a player close by) and edgeCandidates spots along the
        // floor edges, and takes the best: far from where the other thieves of this round came in (and
        // never within entranceSpread of one unless nothing else is left), on the halves of the floor
        // (left/right and top/bottom) fewer of them used, not where they came in last round, the other
        // way in than the last thief (a bush after an edge and the other way round), and away from the
        // players, with a little chance mixed in.
        private Entrance PickEntrance()
        {
            var candidates = new List<Entrance>();
            foreach (HidingBush bush in hidingBushes)
            {
                if (bush == null || bush.Occupant != null || bush.ReservedBy != null) continue;
                if (players.TryGetWithin(bush.Center, spawnClearanceFromPlayers, out _)) continue;
                candidates.Add(new Entrance { Point = new Vector3(bush.Center.x, height, bush.Center.z), Bush = bush });
            }

            for (int i = 0; i < edgeCandidates; i++)
            {
                candidates.Add(EdgeSpot());
            }

            Entrance best = candidates[0];
            float bestScore = float.MinValue;
            foreach (Entrance candidate in candidates)
            {
                float score = EntranceScore(candidate);
                if (score <= bestScore) continue;
                bestScore = score;
                best = candidate;
            }

            return best;
        }

        private float EntranceScore(Entrance candidate)
        {
            float score = Random.Range(0f, 1f);
            if (roundEntrances.Count > 0)
            {
                float nearest = NearestOf(candidate.Point, roundEntrances, 12f);
                score += nearest;
                if (nearest < entranceSpread) score -= 40f;
                if ((candidate.Bush != null) != lastEntranceWasBush) score += 2f;

                // The left and right halves take turns, and so do the top and bottom halves, so even
                // with the players crowding one end the thieves come in above and below them too.
                score += HalfTurn(candidate.Point.x, point => point.x) * 3f;
                score += HalfTurn(candidate.Point.z, point => point.z) * 3f;
            }

            score += NearestOf(candidate.Point, lastRoundEntrances, 4f) * 0.5f;
            score += (players.TryGetNearest(candidate.Point, out _, out float player) ? Mathf.Min(player, 4f) : 4f) * 0.3f;
            // An entrance with a player standing by is only taken when there is nothing else.
            if (!Safe(candidate.Point)) score -= 100f;

            // Away from the passers-by, and never right next to one unless nothing else is free.
            if (passersBy != null)
            {
                passerPositions.Clear();
                passersBy.AddPositions(passerPositions, null);
                passersBy.AddTakenSpots(passerPositions);
                float passer = NearestOf(candidate.Point, passerPositions, 4f);
                score += passer * 0.5f;
                if (passer < passerSpacing) score -= 50f;
            }

            return score;
        }

        // 1 when the candidate's half of the floor along one axis (below or above zero) had fewer
        // entrances this round than the other half, otherwise 0.
        private float HalfTurn(float candidate, System.Func<Vector3, float> axis)
        {
            int below = roundEntrances.FindAll(point => axis(point) < 0f).Count;
            int above = roundEntrances.Count - below;
            bool isBelow = candidate < 0f;
            return (isBelow && below < above) || (!isBelow && above < below) ? 1f : 0f;
        }

        // Distance to the closest of points, at most cap (also when there are none).
        private static float NearestOf(Vector3 point, List<Vector3> points, float cap)
        {
            float nearest = cap;
            foreach (Vector3 other in points)
            {
                nearest = Mathf.Min(nearest, SpawnerPlayerLocator.FlatDistance(point, other));
            }

            return nearest;
        }

        // A random spot on one of the floor edges: half on the left and right ends, half along the top
        // and bottom.
        private Entrance EdgeSpot()
        {
            float sign = Random.value < 0.5f ? -1f : 1f;
            if (Random.value < 0.5f)
            {
                return new Entrance
                {
                    Point = new Vector3(sign < 0f ? areaMin.x : areaMax.x, height, Random.Range(areaMin.y * 0.7f, areaMax.y * 0.7f)),
                    Inward = new Vector3(-sign, 0f, 0f)
                };
            }

            return new Entrance
            {
                Point = new Vector3(Random.Range(areaMin.x * 0.8f, areaMax.x * 0.8f), height, sign < 0f ? areaMin.y : areaMax.y),
                Inward = new Vector3(0f, 0f, -sign)
            };
        }

        private void OnThiefCaught(ThiefController thief)
        {
            CatchBrawl.Play(thief.transform.position, catchBrawl, transform);
            thieves.Remove(thief);
            players.TryGetNearestPlayer(thief.transform.position, out UWBPlayerController catcher);
            Debug.Log($"[ThiefSpawner] {thief.name} caught by {(catcher != null ? catcher.name : "nobody")}, {thieves.Count} thieves left", this);
            ThiefCaught?.Invoke(thief, catcher);
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
