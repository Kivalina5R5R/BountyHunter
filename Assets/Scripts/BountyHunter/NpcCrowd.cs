using System;
using System.Collections;
using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The passers-by on the floor during the game (NpcWalker prefabs; the sloth first). Begin sends
    // each one onto the floor: walking in over an edge or stepping out of an empty hiding spot (with
    // its rustle), wherever is far from the players and from the thieves and other passers-by. They
    // never hide again after that; only thieves hide. One a player bumps into vanishes in a mistaken-arrest
    // scuffle (the thieves' fight cloud with its paw and handcuffs, but OOPS! / WRONG!, question
    // marks and sweat instead of stars, and a big red X slamming down as it bursts),
    // NpcBumped says who did it (BountyRounds fines them), and returnSeconds later it comes back the
    // same way. Clear takes them all away. Console lines start with
    // [NpcCrowd].
    public sealed class NpcCrowd : MonoBehaviour, IFloorWalkers
    {
        [SerializeField] private UWBPlayerSpawner playerSpawner;
        [SerializeField] private NpcWalker[] kinds;
        // The thieves the passers-by keep from walking over (they keep clear of each other too).
        [SerializeField] private ThiefSpawner thieves;
        // The floor the passers-by stroll on (x and world z), like the thieves' area.
        [SerializeField] private Vector2 areaMin = new Vector2(-5.15f, -2.3f);
        [SerializeField] private Vector2 areaMax = new Vector2(5.15f, 2.3f);
        [SerializeField, Min(0f)] private float height = 0.04f;
        [SerializeField, Min(1f)] private float returnSeconds = 15f;
        // Starts this far outside an edge, just out of sight, and walks straight in until it is free
        // to stroll (NpcWalker).
        [SerializeField, Min(0f)] private float startOutside = 0.45f;
        // It only comes in at a spot at least this far from every player, and spawnSpacing from every
        // thief and passer-by in sight; while there is none it waits off the floor and looks again
        // every retrySeconds.
        [SerializeField, Min(0f)] private float entranceClearance = 2.5f;
        [SerializeField, Min(0f)] private float spawnSpacing = 1.8f;
        // How often an empty hiding spot is picked over an edge, when both are clear.
        [SerializeField, Range(0f, 1f)] private float hidingSpotChance = 0.5f;
        [SerializeField, Min(0.1f)] private float retrySeconds = 0.5f;
        // The scuffle played when one is grabbed by mistake.
        [SerializeField] private CatchBrawl.Art mistakeBrawl;

        private readonly List<NpcWalker> walkers = new List<NpcWalker>();
        private readonly List<Vector3> playerPositions = new List<Vector3>();
        private readonly List<Vector3> walkerPositions = new List<Vector3>();

        // Where a passer-by comes onto the floor: over an edge (heading inward) or out of a hiding spot.
        private struct Entrance
        {
            public Vector3 Point;
            public Vector3 Inward;
            public HidingBush Bush;
        }
        private SpawnerPlayerLocator players;

        // The passer-by and the player who bumped into it.
        public event Action<NpcWalker, UWBPlayerController> NpcBumped;

        private void Awake()
        {
            players = new SpawnerPlayerLocator(playerSpawner);
        }

        // Sends every passer-by onto the floor, once; later calls do nothing while they are out.
        public void Begin()
        {
            if (walkers.Count > 0 || kinds == null) return;

            foreach (NpcWalker kind in kinds)
            {
                if (kind == null) continue;

                NpcWalker walker = Instantiate(kind, transform);
                walker.name = kind.name;
                walker.Initialize(players, new IFloorWalkers[] { this, thieves }, areaMin, areaMax);
                walker.Bumped += OnBumped;
                walkers.Add(walker);
                walker.gameObject.SetActive(false);
                StartCoroutine(Enter(walker));
            }

            Debug.Log($"[NpcCrowd] {walkers.Count} passer-by on the floor", this);
        }

        // The passers-by on the floor now (not ones waiting off it to come back).
        public void AddPositions(List<Vector3> positions, Transform except)
        {
            foreach (NpcWalker walker in walkers)
            {
                if (walker == null || walker.transform == except || !walker.isActiveAndEnabled) continue;
                positions.Add(walker.transform.position);
            }
        }

        // The passers-by come straight onto the floor once a spot is picked, so none is taken unseen.
        public void AddTakenSpots(List<Vector3> spots)
        {
        }

        // Takes every passer-by away, with any waiting to come back.
        public void Clear()
        {
            StopAllCoroutines();
            foreach (NpcWalker walker in walkers)
            {
                if (walker == null) continue;
                walker.Bumped -= OnBumped;
                Destroy(walker.gameObject);
            }

            walkers.Clear();
        }

        private void OnBumped(NpcWalker walker)
        {
            Vector3 at = walker.transform.position;
            CatchBrawl.Play(at, mistakeBrawl, transform);
            walker.gameObject.SetActive(false);
            StartCoroutine(ComeBack(walker));

            if (!players.TryGetNearestPlayer(at, out UWBPlayerController player)) return;
            Debug.Log($"[NpcCrowd] Player {player.PlayerId} grabbed {walker.DisplayName}; back in {returnSeconds:0}s", this);
            NpcBumped?.Invoke(walker, player);
        }

        private IEnumerator ComeBack(NpcWalker walker)
        {
            yield return new WaitForSeconds(returnSeconds);
            yield return Enter(walker);
        }

        // Waits off the floor until a spot is clear, then walks in over the edge there or steps out
        // of the hiding spot.
        private IEnumerator Enter(NpcWalker walker)
        {
            Entrance entrance;
            while (!TryPickEntrance(walker, out entrance))
            {
                yield return new WaitForSeconds(retrySeconds);
            }

            if (walker == null) yield break;
            walker.gameObject.SetActive(true);
            if (entrance.Bush != null)
            {
                walker.StepOutAt(entrance.Point);
                Vector2 side = UnityEngine.Random.insideUnitCircle.normalized;
                entrance.Bush.PopOut(entrance.Point + new Vector3(side.x, 0f, side.y));
                yield break;
            }

            // Only the way in; it turns off on its own long before reaching that point.
            walker.WalkIn(entrance.Point - entrance.Inward * startOutside, entrance.Point + entrance.Inward * 3f);
        }

        // The best clear spot among a few on each edge and the empty hiding spots: far from the
        // players and the others on the floor, now and then a hiding spot. False when none is clear.
        private bool TryPickEntrance(NpcWalker walker, out Entrance best)
        {
            players.GetAll(playerPositions);
            walkerPositions.Clear();
            AddPositions(walkerPositions, walker.transform);
            if (thieves != null)
            {
                thieves.AddPositions(walkerPositions, null);
                thieves.AddTakenSpots(walkerPositions);
            }

            var candidates = new List<Entrance>();
            for (int i = 0; i < 24; i++)
            {
                candidates.Add(EdgeSpot(i % 4));
            }

            if (thieves != null)
            {
                foreach (HidingBush bush in thieves.Bushes)
                {
                    if (bush == null || bush.Occupant != null || bush.ReservedBy != null || bush.PlayerIsStandingOn) continue;
                    candidates.Add(new Entrance { Point = new Vector3(bush.Center.x, height, bush.Center.z), Bush = bush });
                }
            }

            bool preferSpot = UnityEngine.Random.value < hidingSpotChance;
            best = default;
            float bestScore = float.MinValue;
            foreach (Entrance candidate in candidates)
            {
                float fromPlayers = Nearest(candidate.Point, playerPositions);
                float fromWalkers = Nearest(candidate.Point, walkerPositions);
                if (fromPlayers < entranceClearance || fromWalkers < spawnSpacing) continue;

                float score = Mathf.Min(fromPlayers, 5f) + Mathf.Min(fromWalkers, 5f) + UnityEngine.Random.value;
                if ((candidate.Bush != null) == preferSpot) score += 3f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = candidate;
            }

            return bestScore > float.MinValue;
        }

        // A random spot along one side of the floor (0 left, 1 right, 2 near, 3 far) and the way in.
        private Entrance EdgeSpot(int side)
        {
            float along = UnityEngine.Random.Range(0.15f, 0.85f);
            switch (side)
            {
                case 0:
                    return new Entrance { Point = new Vector3(areaMin.x, height, Mathf.Lerp(areaMin.y, areaMax.y, along)), Inward = Vector3.right };
                case 1:
                    return new Entrance { Point = new Vector3(areaMax.x, height, Mathf.Lerp(areaMin.y, areaMax.y, along)), Inward = Vector3.left };
                case 2:
                    return new Entrance { Point = new Vector3(Mathf.Lerp(areaMin.x, areaMax.x, along), height, areaMin.y), Inward = Vector3.forward };
                default:
                    return new Entrance { Point = new Vector3(Mathf.Lerp(areaMin.x, areaMax.x, along), height, areaMax.y), Inward = Vector3.back };
            }
        }

        // Distance to the closest of points; very far when there are none.
        private static float Nearest(Vector3 point, List<Vector3> points)
        {
            float nearest = float.MaxValue;
            foreach (Vector3 other in points)
            {
                nearest = Mathf.Min(nearest, SpawnerPlayerLocator.FlatDistance(point, other));
            }

            return nearest;
        }
    }
}
