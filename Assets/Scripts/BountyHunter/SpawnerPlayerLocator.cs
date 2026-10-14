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
            position = default;
            distance = float.MaxValue;
            if (spawner == null) return false;

            foreach (UWBPlayerController player in spawner.SpawnedPlayers)
            {
                if (player == null || !player.isActiveAndEnabled) continue;

                float candidate = FlatDistance(from, player.transform.position);
                if (candidate >= distance) continue;

                distance = candidate;
                position = player.transform.position;
            }

            return distance < float.MaxValue;
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

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return Mathf.Sqrt(x * x + z * z);
        }
    }
}
