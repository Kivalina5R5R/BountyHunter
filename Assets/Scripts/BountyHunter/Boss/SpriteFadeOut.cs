using UnityEngine;

namespace BountyHunter.Thieves
{
    // A copy of a sprite left behind that plays on through some frames once (the smoke cloud billowing
    // where the boss vanished), then fades away and removes itself.
    public sealed class SpriteFadeOut : MonoBehaviour
    {
        private SpriteRenderer copy;
        private Sprite[] frames;
        private float framesPerSecond;
        private Vector2Int stretch;
        private float playSeconds;
        private float fadeSeconds;
        private float elapsed;
        private bool fadeWhilePlaying;
        private float startingAlpha;

        // Plays sheet's frames stretch.x..stretch.y (both included) at framesPerSecond, then fades over
        // fadeSeconds. An empty sheet just fades the picture original shows now.
        // sortingOffset puts the copy over (or under) the original.
        // fadeWhilePlaying spreads a smooth fade across the animation and tail without extending its lifetime.
        public static void LeaveBehind(SpriteRenderer original, Sprite[] sheet, float framesPerSecond, Vector2Int stretch, float fadeSeconds, Transform parent, int sortingOffset = 0, bool fadeWhilePlaying = false)
        {
            var left = new GameObject($"{original.name} Left Behind");
            left.transform.SetParent(parent, true);
            left.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
            left.transform.localScale = original.transform.lossyScale;

            var fade = left.AddComponent<SpriteFadeOut>();
            fade.copy = left.AddComponent<SpriteRenderer>();
            fade.copy.sprite = original.sprite;
            fade.copy.flipX = original.flipX;
            fade.copy.sortingLayerID = original.sortingLayerID;
            fade.copy.sortingOrder = original.sortingOrder + sortingOffset;
            fade.copy.color = original.color;
            fade.startingAlpha = original.color.a;
            fade.fadeWhilePlaying = fadeWhilePlaying;
            fade.fadeSeconds = Mathf.Max(0.05f, fadeSeconds);

            bool plays = sheet != null && sheet.Length > 0 && framesPerSecond > 0f;
            if (!plays) return;

            fade.frames = sheet;
            fade.framesPerSecond = framesPerSecond;
            fade.stretch = new Vector2Int(Mathf.Clamp(stretch.x, 0, sheet.Length - 1), Mathf.Clamp(stretch.y, 0, sheet.Length - 1));
            fade.playSeconds = Mathf.Max(0, fade.stretch.y - fade.stretch.x) / framesPerSecond;
            if (fadeWhilePlaying)
            {
                fade.fadeSeconds += fade.playSeconds;
                fade.playSeconds = 0f;
            }
            fade.copy.sprite = sheet[fade.stretch.x];
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (frames != null && stretch.y >= stretch.x)
            {
                int frame = Mathf.Min(stretch.x + Mathf.FloorToInt(elapsed * framesPerSecond), stretch.y);
                copy.sprite = frames[frame];
            }

            float fading = elapsed - playSeconds;
            if (fading <= 0f) return;

            Color color = copy.color;
            float progress = Mathf.Clamp01(fading / fadeSeconds);
            if (fadeWhilePlaying) progress = Mathf.SmoothStep(0f, 1f, progress);
            color.a = startingAlpha * (1f - progress);
            copy.color = color;
            if (fading >= fadeSeconds) Destroy(gameObject);
        }
    }
}
