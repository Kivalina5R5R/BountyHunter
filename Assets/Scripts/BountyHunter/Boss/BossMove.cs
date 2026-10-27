using UnityEngine;

namespace BountyHunter.Thieves
{
    // One of the boss's moves cut into sprites, with the rate its sheet plays at.
    public sealed class BossMove
    {
        public BossMove(BossSheet sheet, float pixelsPerUnit)
        {
            Frames = sheet.Slice(pixelsPerUnit);
            FramesPerSecond = sheet.FramesPerSecond;
        }

        public Sprite[] Frames { get; }
        public float FramesPerSecond { get; }
        public bool IsEmpty => Frames.Length == 0;
        public Vector2Int All => new Vector2Int(0, Frames.Length - 1);
    }
}
