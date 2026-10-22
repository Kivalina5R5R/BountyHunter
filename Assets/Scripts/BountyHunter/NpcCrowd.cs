using System;
using System.Collections;
using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The passers-by on the floor during the game (NpcWalker prefabs, one of each kind at most at a
    // time). Begin sends a few in, staggered, then one more every arriveEverySeconds while fewer
    // than mostOnFloor are out; each strolls for a while (staySeconds, counted once it is on the
    // floor) and then walks off over an edge. A new one is a kind not on the floor, the one gone the
    // longest first, so they all take turns. Each comes in walking over an edge or stepping out of an
    // empty hiding spot (with its rustle), wherever is far from the players and from the thieves and
    // other passers-by. They never hide again after that; only thieves hide. One a player bumps into vanishes in a mistaken-arrest
    // scuffle (the thieves' fight cloud with its paw and handcuffs, but OOPS! / WRONG!, question
    // marks and sweat instead of stars, and a big red X slamming down as it bursts), and
    // NpcBumped says who did it (BountyRounds fines them). Once the scuffle is over it is back on
    // the same spot, stamping about in its angry act (NpcWalker.GetAngry), then strolls on (or goes,
    // when its time is up). Clear takes them all away. Console lines start with [NpcCrowd].
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
        // Starts this far outside an edge, just out of sight, and walks straight in until it is free
        // to stroll (NpcWalker).
        [SerializeField, Min(0f)] private float startOutside = 0.45f;
        // It only comes in at a spot at least this far from every player, and spawnSpacing from every
        // thief and passer-by in sight; while there is none it waits off the floor and looks again
        // every retrySeconds.
        [SerializeField, Min(0f)] private float entranceClearance = 1.6f;
        [SerializeField, Min(0f)] private float spawnSpacing = 1.8f;
        // How often an empty hiding spot is picked over an edge, when both are clear.
        [SerializeField, Range(0f, 1f)] private float hidingSpotChance = 0.5f;
        [SerializeField, Min(0.1f)] private float retrySeconds = 0.5f;

        [Header("Coming and going")]
        // How many come in at the start (inclusive range), staggerSeconds apart.
        [SerializeField] private Vector2Int startingCount = new Vector2Int(2, 3);
        [SerializeField, Min(0f)] private float staggerSeconds = 1.5f;
        // After the starting ones, one more comes in this often (while fewer than mostOnFloor are
        // out and none is still waiting for a clear spot).
        [SerializeField, Min(0.5f)] private float arriveEverySeconds = 8f;
        [SerializeField, Min(1)] private int mostOnFloor = 4;
        // How long each strolls about once on the floor before it walks off (random in the range).
        [SerializeField] private Vector2 staySeconds = new Vector2(10f, 15f);
        // The scuffle played when one is grabbed by mistake.
        [SerializeField] private CatchBrawl.Art mistakeBrawl;
        // The bumped passer-by starts its angry act this long before the scuffle's X is gone.
        [SerializeField, Min(0f)] private float angryBeforeStampGone = 0.9f;

        private readonly List<NpcWalker> walkers = new List<NpcWalker>();
        // The kind each walker out now was made from, walkers still waiting to come in, and when each
        // kind last left the floor.
        private readonly Dictionary<NpcWalker, NpcWalker> kindOf = new Dictionary<NpcWalker, NpcWalker>();
        private readonly HashSet<NpcWalker> waiting = new HashSet<NpcWalker>();
        private readonly Dictionary<NpcWalker, float> lastLeftAt = new Dictionary<NpcWalker, float>();
        private Coroutine schedule;
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

        // Starts the passers-by coming and going; later calls do nothing until Clear.
        public void Begin()
        {
            if (schedule != null || kinds == null) return;

            schedule = StartCoroutine(ComeAndGo());
        }

        // A few at the start, then one more every arriveEverySeconds while there is room.
        private IEnumerator ComeAndGo()
        {
            int starting = UnityEngine.Random.Range(startingCount.x, Mathf.Max(startingCount.x, startingCount.y) + 1);
            for (int i = 0; i < starting; i++)
            {
                if (i > 0) yield return new WaitForSeconds(staggerSeconds);
                SendOne();
            }

            while (true)
            {
                yield return new WaitForSeconds(arriveEverySeconds);
                if (walkers.Count < mostOnFloor && waiting.Count == 0) SendOne();
            }
        }

        // Sends in a kind not out now, the one gone the longest (or never out) first.
        private void SendOne()
        {
            NpcWalker kind = NextKind();
            if (kind == null) return;

            NpcWalker walker = Instantiate(kind, transform);
            walker.name = kind.name;
            walker.Initialize(players, new IFloorWalkers[] { this, thieves }, thieves, areaMin, areaMax);
            walker.Bumped += OnBumped;
            walker.Left += OnLeft;
            walkers.Add(walker);
            kindOf[walker] = kind;
            walker.gameObject.SetActive(false);
            StartCoroutine(Visit(walker));
            Debug.Log($"[NpcCrowd] {walker.DisplayName} comes in, {walkers.Count} passer-by out", this);
        }

        private NpcWalker NextKind()
        {
            NpcWalker next = null;
            float nextLeftAt = float.MaxValue;
            foreach (NpcWalker kind in kinds)
            {
                if (kind == null || kindOf.ContainsValue(kind)) continue;

                // Never out counts as gone the longest; a little chance breaks ties.
                float leftAt = (lastLeftAt.TryGetValue(kind, out float at) ? at : -100000f) + UnityEngine.Random.value * 0.01f;
                if (leftAt >= nextLeftAt) continue;
                nextLeftAt = leftAt;
                next = kind;
            }

            return next;
        }

        // Comes in, strolls for its stay once fully on the floor, then heads off.
        private IEnumerator Visit(NpcWalker walker)
        {
            waiting.Add(walker);
            yield return Enter(walker);
            waiting.Remove(walker);
            while (walker != null && walker.IsComingIn) yield return null;

            yield return new WaitForSeconds(UnityEngine.Random.Range(staySeconds.x, Mathf.Max(staySeconds.x, staySeconds.y)));
            if (walker != null) walker.Leave();
        }

        private void OnLeft(NpcWalker walker)
        {
            if (kindOf.TryGetValue(walker, out NpcWalker kind)) lastLeftAt[kind] = Time.time;
            kindOf.Remove(walker);
            walkers.Remove(walker);
            walker.Bumped -= OnBumped;
            walker.Left -= OnLeft;
            Destroy(walker.gameObject);
            Debug.Log($"[NpcCrowd] {walker.DisplayName} walked off, {walkers.Count} passer-by out", this);
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

        // Takes every passer-by away, with any waiting to come in, and stops new ones coming.
        public void Clear()
        {
            StopAllCoroutines();
            schedule = null;
            foreach (NpcWalker walker in walkers)
            {
                if (walker == null) continue;
                walker.Bumped -= OnBumped;
                walker.Left -= OnLeft;
                Destroy(walker.gameObject);
            }

            walkers.Clear();
            kindOf.Clear();
            waiting.Clear();
        }

        private void OnBumped(NpcWalker walker)
        {
            Vector3 at = walker.transform.position;
            float scuffle = CatchBrawl.Play(at, mistakeBrawl, transform);
            walker.gameObject.SetActive(false);
            StartCoroutine(GetAngryAfter(walker, scuffle));

            if (!players.TryGetNearestPlayer(at, out UWBPlayerController player)) return;
            Debug.Log($"[NpcCrowd] Player {player.PlayerId} grabbed {walker.DisplayName}", this);
            NpcBumped?.Invoke(walker, player);
        }

        // The passer-by is back on its spot just before the X slams down (still hidden in the cloud),
        // so the X lands on it as the cloud bursts. It starts its angry act angryBeforeStampGone
        // before the X is gone; when that is earlier still, it is back already angry, under the cloud.
        private IEnumerator GetAngryAfter(NpcWalker walker, float scuffleSeconds)
        {
            float angry = Mathf.Clamp(CatchBrawl.StampGoneSeconds - angryBeforeStampGone, 0f, scuffleSeconds);
            float reappear = Mathf.Min(Mathf.Max(0f, CatchBrawl.StampSeconds - 0.05f), angry);
            yield return new WaitForSeconds(reappear);
            if (walker == null) yield break;

            walker.gameObject.SetActive(true);
            walker.GetAngry(Mathf.Max(0f, angry - reappear));
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

                // Clear of the players is enough (further gains little, or every passer-by would come
                // in on the side away from them); being away from the others spreads them out.
                float score = Mathf.Min(fromPlayers, entranceClearance + 0.5f) + Mathf.Min(fromWalkers, 5f) + UnityEngine.Random.value;
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
