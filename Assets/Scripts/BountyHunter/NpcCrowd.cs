using System;
using System.Collections;
using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The passers-by on the floor during the game (NpcWalker prefabs; the sloth first). Begin sends
    // each one walking in over a floor edge. One a player bumps into vanishes in a mistaken-arrest
    // scuffle (the thieves' fight cloud with its paw and handcuffs, but OOPS! / WRONG!, question
    // marks and sweat instead of stars, and a big red X slamming down as it bursts),
    // NpcBumped says who did it (BountyRounds fines them), and returnSeconds later it walks back in
    // from the edge furthest from the players. Clear takes them all away. Console lines start with
    // [NpcCrowd].
    public sealed class NpcCrowd : MonoBehaviour
    {
        [SerializeField] private UWBPlayerSpawner playerSpawner;
        [SerializeField] private NpcWalker[] kinds;
        // The floor the passers-by stroll on (x and world z), like the thieves' area.
        [SerializeField] private Vector2 areaMin = new Vector2(-5.15f, -2.3f);
        [SerializeField] private Vector2 areaMax = new Vector2(5.15f, 2.3f);
        [SerializeField, Min(0f)] private float height = 0.04f;
        [SerializeField, Min(1f)] private float returnSeconds = 15f;
        // Starts this far outside an edge, just out of sight, and walks straight in until it is free
        // to stroll (NpcWalker).
        [SerializeField, Min(0f)] private float startOutside = 0.45f;
        // It only walks in at an edge spot at least this far from every player; while there is none
        // (players along every edge) it waits off the floor and looks again every retrySeconds.
        [SerializeField, Min(0f)] private float entranceClearance = 2.5f;
        [SerializeField, Min(0.1f)] private float retrySeconds = 0.5f;
        // The scuffle played when one is grabbed by mistake.
        [SerializeField] private CatchBrawl.Art mistakeBrawl;

        private readonly List<NpcWalker> walkers = new List<NpcWalker>();
        private readonly List<Vector3> playerPositions = new List<Vector3>();
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
                walker.Initialize(players, areaMin, areaMax);
                walker.Bumped += OnBumped;
                walkers.Add(walker);
                walker.gameObject.SetActive(false);
                StartCoroutine(Enter(walker));
            }

            Debug.Log($"[NpcCrowd] {walkers.Count} passer-by on the floor", this);
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

        // Waits off the floor until an edge spot is clear of the players, then walks in there.
        private IEnumerator Enter(NpcWalker walker)
        {
            Vector3 edge;
            Vector3 inward;
            while (EdgeEntrance(out edge, out inward) < entranceClearance)
            {
                yield return new WaitForSeconds(retrySeconds);
            }

            if (walker == null) yield break;
            walker.gameObject.SetActive(true);
            // Only the way in; it turns off on its own long before reaching that point.
            walker.WalkIn(edge - inward * startOutside, edge + inward * 3f);
        }

        // A point on the floor's edge, picked among several on each side as the one furthest from
        // every player, and the way into the floor from it. Returns how far the nearest player is.
        private float EdgeEntrance(out Vector3 point, out Vector3 inward)
        {
            players.GetAll(playerPositions);
            point = Vector3.zero;
            inward = Vector3.right;
            float best = float.MinValue;
            for (int i = 0; i < 24; i++)
            {
                int side = i % 4;
                float along = UnityEngine.Random.Range(0.15f, 0.85f);
                Vector3 candidate;
                Vector3 into;
                switch (side)
                {
                    case 0:
                        candidate = new Vector3(areaMin.x, height, Mathf.Lerp(areaMin.y, areaMax.y, along));
                        into = Vector3.right;
                        break;
                    case 1:
                        candidate = new Vector3(areaMax.x, height, Mathf.Lerp(areaMin.y, areaMax.y, along));
                        into = Vector3.left;
                        break;
                    case 2:
                        candidate = new Vector3(Mathf.Lerp(areaMin.x, areaMax.x, along), height, areaMin.y);
                        into = Vector3.forward;
                        break;
                    default:
                        candidate = new Vector3(Mathf.Lerp(areaMin.x, areaMax.x, along), height, areaMax.y);
                        into = Vector3.back;
                        break;
                }

                float clearance = float.MaxValue;
                foreach (Vector3 player in playerPositions)
                {
                    clearance = Mathf.Min(clearance, SpawnerPlayerLocator.FlatDistance(candidate, player));
                }

                if (clearance <= best) continue;
                best = clearance;
                point = candidate;
                inward = into;
            }

            return best;
        }
    }
}
