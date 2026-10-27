using UnityEngine;

namespace BountyHunter.Thieves
{
    // Plays a stretch of one of the boss's moves on its sprite: frames from..to (both included) at the
    // sheet's own rate, looping or stopping on the last one. Pausing holds the picture where it is.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BossAnimator : MonoBehaviour
    {
        private SpriteRenderer body;
        private Sprite[] frames;
        private float framesPerSecond = 15f;
        private int from;
        private int to;
        private bool loop;
        private float time;

        public SpriteRenderer Body => body != null ? body : body = GetComponent<SpriteRenderer>();

        // The frame showing now, as a number in the whole sheet.
        public int Frame { get; private set; }

        // A stretch played once has reached its last frame.
        public bool Done => !loop && Frame >= to;

        public bool Paused { get; set; }

        // How many times its own rate the frames play at (1 unless something sets it); Play resets it.
        public float Pace { get; set; } = 1f;

        public void Play(Sprite[] sheet, float sheetFramesPerSecond, Vector2Int stretch, bool looping)
        {
            frames = sheet;
            framesPerSecond = sheetFramesPerSecond;
            from = Mathf.Clamp(stretch.x, 0, sheet.Length - 1);
            to = Mathf.Clamp(stretch.y, from, sheet.Length - 1);
            loop = looping;
            time = 0f;
            Paused = false;
            Pace = 1f;
            Show(from);
        }

        private void Update()
        {
            if (frames == null || Paused) return;

            time += Time.deltaTime * Pace;
            int length = to - from + 1;
            int step = Mathf.FloorToInt(time * framesPerSecond);
            Show(loop ? from + step % length : Mathf.Min(from + step, to));
        }

        private void Show(int frame)
        {
            Frame = frame;
            Body.sprite = frames[frame];
        }
    }
}
