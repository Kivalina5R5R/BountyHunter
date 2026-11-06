using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Plays a TransitionClip over everything on the wall, under the fade curtain: as tall as the wall,
    // in its middle, easing in from a little bigger with its sound. Runs on unscaled time, so it plays while
    // the game is frozen. Whoever makes it removes it.
    public sealed class TransitionClipPlayer : MonoBehaviour
    {
        // Over the wall's pictures and the mini games, under the fade curtain (100).
        private const int SortingOrder = 90;
        private const float EaseInSeconds = 0.25f;
        private const float EaseInFrom = 1.04f;

        private RawImage picture;
        private AudioSource voice;
        private bool stopped;

        // Over wall, the canvas area it fills.
        public static TransitionClipPlayer Create(RectTransform wall, float aspect)
        {
            var root = new GameObject("Transition Clip", typeof(RectTransform), typeof(Canvas), typeof(RawImage));
            var rect = (RectTransform)root.transform;
            rect.SetParent(wall, false);
            root.layer = wall.gameObject.layer;
            float height = wall.rect.height;
            rect.sizeDelta = new Vector2(height * aspect, height);
            var canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            var player = root.AddComponent<TransitionClipPlayer>();
            player.picture = root.GetComponent<RawImage>();
            player.picture.raycastTarget = false;
            player.picture.color = Color.clear;
            player.voice = TransitionSound.SourceOn(root);
            return player;
        }

        // Plays clip to its end, calling ending once endingSeconds before it (for a fade that should
        // finish as the clip does).
        public IEnumerator Play(TransitionClip clip, float endingSeconds, Action ending)
        {
            if (clip.Sound != null) voice.PlayOneShot(clip.Sound);
            bool endingStarted = false;
            for (float time = 0f; time < clip.Seconds && !stopped; time += Time.unscaledDeltaTime)
            {
                picture.texture = clip.FrameAt(time, out Rect area);
                picture.uvRect = area;
                float eased = Mathf.SmoothStep(0f, 1f, time / EaseInSeconds);
                picture.color = new Color(1f, 1f, 1f, eased);
                picture.rectTransform.localScale = Vector3.one * Mathf.Lerp(EaseInFrom, 1f, eased);
                if (!endingStarted && time >= clip.Seconds - endingSeconds)
                {
                    endingStarted = true;
                    ending?.Invoke();
                }

                yield return null;
            }
        }

        // Cuts it short (the finale being skipped).
        public void Stop()
        {
            stopped = true;
        }
    }
}
