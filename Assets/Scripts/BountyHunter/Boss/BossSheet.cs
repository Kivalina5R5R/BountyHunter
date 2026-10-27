using System;
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

            int rows = Mathf.CeilToInt(frames / (float)columns);
            float cellWidth = texture.width / (float)columns;
            float cellHeight = texture.height / (float)rows;
            var sprites = new Sprite[frames];
            for (int i = 0; i < frames; i++)
            {
                var cell = new Rect((i % columns) * cellWidth, texture.height - (i / columns + 1) * cellHeight, cellWidth, cellHeight);
                sprites[i] = Sprite.Create(texture, cell, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprites[i].name = $"{texture.name}_{i:00}";
            }

            return sprites;
        }
    }
}
