using System;
using System.Collections;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // One of the boss's moves as a sprite sheet (Tools/make_boss_sheet.py): equal cells laid left to
    // right and top to bottom, cut into sprites when the game runs, so the sheet needs no slicing in
    // the editor. The pivot is where the boss's feet are inside a cell (the same spot for every move,
    // so switching moves does not make the boss jump).
    [Serializable]
    public sealed class BossSheet
    {
        [SerializeField] private Texture2D texture;
        [SerializeField, Min(1)] private int columns = 10;
        [SerializeField, Min(1)] private int frames = 90;
        [SerializeField, Min(1f)] private float framesPerSecond = 15f;
        [SerializeField] private Vector2 pivot = new Vector2(0.5f, 0.02f);
        private Sprite[] cached;
        private int preparedFrames;
        private float cachedPixelsPerUnit;

        public BossSheet()
        {
        }

        public BossSheet(int columns, int frames, Vector2 pivot)
        {
            this.columns = columns;
            this.frames = frames;
            this.pivot = pivot;
        }

        public bool IsSet => texture != null;
        public float FramesPerSecond => framesPerSecond;

        // The cells as sprites; pixelsPerUnit is shared by every sheet (they were all cut at one scale,
        // and BossSheetImport keeps the importer from shrinking them).
        public Sprite[] Slice(float pixelsPerUnit)
        {
            if (texture == null) return Array.Empty<Sprite>();
            EnsureCache(pixelsPerUnit);
            while (preparedFrames < cached.Length) PrepareFrame(pixelsPerUnit);
            return cached;
        }

        // Eight small sprite creations per frame, reused if the consumer arrives early.
        public IEnumerator Preload(float pixelsPerUnit)
        {
            if (texture == null) yield break;
            EnsureCache(pixelsPerUnit);
            while (preparedFrames < cached.Length)
            {
                int end = Mathf.Min(preparedFrames + 8, cached.Length);
                while (preparedFrames < end) PrepareFrame(pixelsPerUnit);
                yield return null;
            }
        }

        private void EnsureCache(float pixelsPerUnit)
        {
            if (cached != null && cachedPixelsPerUnit == pixelsPerUnit) return;
            Release();
            cached = new Sprite[frames];
            cachedPixelsPerUnit = pixelsPerUnit;
        }

        private void PrepareFrame(float pixelsPerUnit)
        {
            int rows = Mathf.CeilToInt(frames / (float)columns);
            float cellWidth = texture.width / (float)columns;
            float cellHeight = texture.height / (float)rows;
            int i = preparedFrames;
            var cell = new Rect((i % columns) * cellWidth, texture.height - (i / columns + 1) * cellHeight, cellWidth, cellHeight);
            cached[i] = Sprite.Create(texture, cell, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            cached[i].name = $"{texture.name}_{i:00}";
            preparedFrames++;
        }

        public void Release()
        {
            if (cached != null)
                foreach (Sprite sprite in cached)
                    if (sprite != null) UnityEngine.Object.Destroy(sprite);
            cached = null;
            preparedFrames = 0;
        }
    }
}
