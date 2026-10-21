using System;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Where on the room's two speakers (left and right of the floor) a sound on the floor comes from:
    // its world X across the floor, from the left edge (all left) to the right edge (all right).
    // Unity hears through one AudioListener only, so the two speakers are its stereo channels and
    // each sound is panned to where it happens. swapSides turns it round for speakers wired the
    // other way.
    [Serializable]
    public sealed class FloorStereo
    {
        // Half the floor's width (world X from the middle to an edge).
        [SerializeField, Min(0.1f)] private float floorHalfWidth = 5.15f;
        // How far to the side a sound at the floor's edge goes (1: only that speaker).
        [SerializeField, Range(0f, 1f)] private float width = 1f;
        [SerializeField] private bool swapSides;

        // The AudioSource.panStereo for a sound at point (-1 left speaker, 1 right).
        public float PanAt(Vector3 point)
        {
            float pan = Mathf.Clamp(point.x / floorHalfWidth, -1f, 1f) * width;
            return swapSides ? -pan : pan;
        }
    }
}
