using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The size of a player's floor marker (its plain tag circle), so things drawn round or behind a
    // player keep in proportion to it.
    public static class PlayerMarker
    {
        // The tag circle's width when it cannot be measured (metres).
        public const float UsualWidth = 0.665f;

        // The width of the player's floor marker (its UI circle), measured in the world.
        public static float Width(UWBPlayerController player)
        {
            var marker = player != null ? player.GetComponentInChildren<RectTransform>(true) : null;
            if (marker == null) return UsualWidth;

            float width = marker.rect.width * marker.lossyScale.x;
            return width > 0.05f ? width : UsualWidth;
        }
    }
}
