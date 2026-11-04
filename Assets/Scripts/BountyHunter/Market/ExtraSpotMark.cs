using UnityEngine;

namespace BountyHunter.Thieves
{
    // Marks a hiding spot the lucky wheel added for a while, so the players can tell it from the
    // others: a ring of stars bursts out as it springs up, then a glow breathes under it and stars
    // twinkle round it (FloorSwitchLure) wherever it creeps to. Finish throws the stars out once more
    // as the spot goes, and removes the mark; it also goes by itself if the spot is destroyed.
    public sealed class ExtraSpotMark : MonoBehaviour
    {
        private const int BurstStars = 12;
        // How long the last burst of stars has to fly before the mark is removed.
        private const float GoneSeconds = 0.7f;

        private Transform spot;
        private FloorSwitchLure lure;

        // Round spot, width metres across, its stars drawn from sparkles (may be empty).
        public static ExtraSpotMark Create(Transform parent, Transform spot, float width, Sprite[] sparkles)
        {
            var root = new GameObject("Extra Spot Mark");
            root.transform.SetParent(parent, false);
            root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var mark = root.AddComponent<ExtraSpotMark>();
            mark.spot = spot;
            mark.lure = FloorSwitchLure.Create(root.transform, width, sparkles);
            mark.Follow();
            mark.lure.Burst(BurstStars);
            mark.lure.Inviting = true;
            return mark;
        }

        public void Finish()
        {
            if (spot == null) return;

            spot = null;
            if (lure != null) lure.Burst(BurstStars);
            Destroy(gameObject, GoneSeconds);
        }

        private void LateUpdate()
        {
            if (spot != null)
            {
                Follow();
                return;
            }

            // The spot went without Finish (a new layout): the mark goes with it.
            if (lure != null && lure.Inviting) Destroy(gameObject);
        }

        private void Follow()
        {
            transform.position = spot.position;
        }
    }
}
