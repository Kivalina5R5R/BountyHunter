using System.Collections;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // A short film played over the wall with its background cut out, as sprite sheet pages and a sound
    // in Assets/Resources/Transition (Tools/BossVideoArt/make_video_sheet.py): frames laid left to
    // right and top to bottom, page after page. The pages are big, so they are in memory only from
    // Load until Unload rather than with the scene.
    public sealed class TransitionClip
    {
        private const string Folder = "Transition/";
        private const int Columns = 2;
        private const int Rows = 6;
        private const float FramesPerSecond = 60f;

        // Its pages are named name_0, name_1, ... and its sound name_Sound.
        private readonly string name;
        private readonly int pages;
        private readonly int frames;

        private Texture2D[] loaded;
        private AudioClip sound;

        public TransitionClip(string name, int pages, int frames)
        {
            this.name = name;
            this.pages = pages;
            this.frames = frames;
        }

        public float Seconds => frames / FramesPerSecond;
        public AudioClip Sound => sound;

        // True once every page is in memory.
        public bool IsLoaded { get; private set; }

        // True from the start of Load until it has finished or failed.
        public bool IsLoading { get; private set; }

        // One frame's width against its height.
        public float Aspect => IsLoaded ? loaded[0].width * Rows / (float)(loaded[0].height * Columns) : 1f;

        // Loads the pages and the sound without holding up the game; a missing page is logged and
        // leaves it unloaded, so the caller goes without the film.
        public IEnumerator Load()
        {
            if (IsLoaded || IsLoading) yield break;

            IsLoading = true;
            var textures = new Texture2D[pages];
            for (int i = 0; i < pages; i++)
            {
                ResourceRequest page = Resources.LoadAsync<Texture2D>($"{Folder}{name}_{i}");
                yield return page;
                textures[i] = page.asset as Texture2D;
                if (textures[i] != null) continue;

                Debug.LogError($"[TransitionClip] Page {i} of {name} is missing");
                IsLoading = false;
                yield break;
            }

            ResourceRequest clip = Resources.LoadAsync<AudioClip>($"{Folder}{name}_Sound");
            yield return clip;
            sound = clip.asset as AudioClip;
            loaded = textures;
            IsLoaded = true;
            IsLoading = false;
        }

        public void Unload()
        {
            IsLoaded = false;
            IsLoading = false;
            if (loaded != null)
            {
                foreach (Texture2D page in loaded)
                {
                    if (page != null) Resources.UnloadAsset(page);
                }
            }

            loaded = null;
            if (sound != null) Resources.UnloadAsset(sound);
            sound = null;
        }

        // The page holding the frame (past the end, the last frame), and where on it the frame is. The
        // area stops half a pixel short of each edge, so the filter does not pick up the next frame.
        public Texture2D FrameAt(float seconds, out Rect area)
        {
            int frame = Mathf.Clamp(Mathf.FloorToInt(seconds * FramesPerSecond), 0, frames - 1);
            int perPage = Columns * Rows;
            Texture2D page = loaded[Mathf.Min(frame / perPage, loaded.Length - 1)];
            int inPage = frame % perPage;
            float width = 1f / Columns;
            float height = 1f / Rows;
            float insetX = 0.5f / page.width;
            float insetY = 0.5f / page.height;
            area = new Rect(inPage % Columns * width + insetX, 1f - (inPage / Columns + 1) * height + insetY, width - insetX * 2f, height - insetY * 2f);
            return page;
        }
    }
}
