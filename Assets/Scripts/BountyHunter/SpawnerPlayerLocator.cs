using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Finds players among the ones UWBPlayerSpawner created, skipping any that are switched off
    // (for example a tag that is offline in Serial mode).
    public sealed class SpawnerPlayerLocator : IPlayerLocator
    {
        private readonly UWBPlayerSpawner spawner;

        public SpawnerPlayerLocator(UWBPlayerSpawner spawner)
        {
            this.spawner = spawner;
        }

        public bool TryGetNearest(Vector3 from, out Vector3 position, out float distance)
        {
            bool found = TryGetNearestPlayer(from, out UWBPlayerController player);
            position = found ? player.transform.position : default;
            distance = found ? FlatDistance(from, position) : float.MaxValue;
            return found;
        }

        // The closest active player itself, for when it matters who it is (such as who made a catch).
        public bool TryGetNearestPlayer(Vector3 from, out UWBPlayerController nearest)
        {
            nearest = null;
            if (spawner == null) return false;

            float best = float.MaxValue;
            foreach (UWBPlayerController player in spawner.SpawnedPlayers)
            {
                if (player == null || !player.isActiveAndEnabled) continue;

                float candidate = FlatDistance(from, player.transform.position);
                if (candidate >= best) continue;

                best = candidate;
                nearest = player;
            }

            return nearest != null;
        }

        public bool TryGetWithin(Vector3 center, float radius, out Vector3 position)
        {
            return TryGetNearest(center, out position, out float distance) && distance <= radius;
        }

        public void GetAll(List<Vector3> positions)
        {
            positions.Clear();
            if (spawner == null) return;

            foreach (UWBPlayerController player in spawner.SpawnedPlayers)
            {
                if (player != null && player.isActiveAndEnabled) positions.Add(player.transform.position);
            }
        }

        // Fills players with every active player within radius of the centre (clears it first).
        public void GetPlayersWithin(Vector3 center, float radius, List<UWBPlayerController> players)
        {
            players.Clear();
            if (spawner == null) return;

            foreach (UWBPlayerController player in spawner.SpawnedPlayers)
            {
                if (player != null && player.isActiveAndEnabled && FlatDistance(center, player.transform.position) <= radius) players.Add(player);
            }
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return Mathf.Sqrt(x * x + z * z);
        }
    }
}
