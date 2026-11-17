using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Plays a looping animation from one sprite sheet page on the wall (the player dogs' idles cut
    // from a green-screen video by Tools/PlayerIdleArt/make_idle_sheet.py) in place of a still
    // picture. The frames are laid left to right, top to bottom; each cell has a clear gutter round
    // it, which the shown part leaves out, so the police rim can read a little past the frame's edge
    // without picking up the next one. Sits on a RawImage covering the area one frame shows; the
    // still picture hides while the loop shows and comes back if this is turned off.
    [RequireComponent(typeof(RawImage))]
    public sealed class WallSheetLoop : MonoBehaviour
    {
        [SerializeField] private Texture2D sheet;
        [SerializeField, Min(1)] private int columns = 1;
        [SerializeField, Min(1)] private int rows = 1;
        // Frames used on the page; the last row may be part full.
        [SerializeField, Min(1)] private int frameCount = 1;
        [SerializeField, Min(1f)] private float framesPerSecond = 12f;
        // Clear pixels round each frame inside its cell.
        [SerializeField, Min(0)] private int gutter;
        [SerializeField] private Graphic stillPicture;

        private RawImage image;
        private float time;

        public Texture Sheet => sheet;
        // The part of the page the current frame shows, in the RawImage's uv terms.
        public Rect FrameRect => image != null ? image.uvRect : new Rect(0f, 0f, 1f, 1f);
        public bool IsShowing => isActiveAndEnabled && sheet != null;

        private void Awake()
        {
            image = GetComponent<RawImage>();
            image.raycastTarget = false;
            image.texture = sheet;
        }

        private void OnEnable()
        {
            if (sheet == null) return;

            // Each dog starts at a different moment, so several on the wall do not move in step.
            time = Random.Range(0f, frameCount / framesPerSecond);
            image.enabled = true;
            image.color = Color.white;
            ShowFrame();
            if (stillPicture != null) stillPicture.enabled = false;
        }

        private void OnDisable()
        {
            if (image != null) image.enabled = false;
            if (stillPicture != null) stillPicture.enabled = true;
        }

        private void Update()
        {
            if (sheet == null) return;

            time += Time.deltaTime;
            ShowFrame();
        }

        private void ShowFrame()
        {
            int frame = Mathf.FloorToInt(time * framesPerSecond) % frameCount;
            float cellWidth = sheet.width / (float)columns;
            float cellHeight = sheet.height / (float)rows;
            int column = frame % columns;
            // Row 0 is the page's top, while uv y runs up from the bottom.
            int row = rows - 1 - frame / columns;
            image.uvRect = new Rect(
                (column * cellWidth + gutter) / sheet.width,
                (row * cellHeight + gutter) / sheet.height,
                (cellWidth - 2f * gutter) / sheet.width,
                (cellHeight - 2f * gutter) / sheet.height);
        }
    }
}
