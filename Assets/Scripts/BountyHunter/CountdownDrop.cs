using UnityEngine;

namespace BountyHunter.Thieves
{
    // How a countdown number (3, 2, 1, GO!) moves through its step: it falls from big, shrinking
    // faster and faster and fading in, squashes as it lands and springs back, holds, then fades. The
    // floor's countdown at the start of the game and the camera's count-in in mini game 2 move alike.
    public static class CountdownDrop
    {
        private const float BounceEnd = 0.5f;
        private const float FadeFrom = 0.8f;

        // At share t (0 to 1) of the step: its scale against its own size, and its alpha. fallPart is
        // the share the fall takes, dropFromScale how big it starts, landSquash how much it squashes.
        public static void Pose(float t, float fallPart, float dropFromScale, float landSquash, out Vector3 scale, out float alpha)
        {
            scale = Vector3.one;
            alpha = 1f;
            if (t < fallPart)
            {
                float u = t / fallPart;
                scale *= Mathf.Lerp(dropFromScale, 1f, u * u);
                alpha = Mathf.Clamp01(u * 3f);
            }
            else if (t < BounceEnd)
            {
                float u = (t - fallPart) / (BounceEnd - fallPart);
                float spring = Mathf.Cos(u * Mathf.PI * 2.5f) * (1f - u) * (1f - u) * landSquash;
                scale = new Vector3(1f + spring, 1f - spring, 1f);
            }
            else if (t > FadeFrom)
            {
                alpha = 1f - (t - FadeFrom) / (1f - FadeFrom);
            }
        }
    }
}
