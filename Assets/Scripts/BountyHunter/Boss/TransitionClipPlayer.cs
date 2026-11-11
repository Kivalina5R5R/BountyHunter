using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Plays a TransitionClip over everything on the wall, under the fade curtain: as tall as the wall
    // and as wide, the clip's middle kept true and only its ends stretched out to the wall's sides
    // (EdgeStretchImage), easing in from a little bigger, silently. Runs on unscaled time, so it plays while
    // the game is frozen. Where the clip skips a cut, the last frame before it dissolves into the
    // frames after it rather than jumping. Whoever makes it removes it.
    public sealed class TransitionClipPlayer : MonoBehaviour
    {
        // Over the wall's pictures and the mini games, under the fade curtain (100).
        private const int SortingOrder = 90;
        private const float EaseInSeconds = 0.25f;
        private const float EaseInFrom = 1.04f;
        // Share of each end of the clip that stretches to reach the wall's sides.
        private const float StretchedEdge = 0.1f;
        // How long the frame before a cut takes to dissolve into the ones after it.
        private const float DissolveSeconds = 0.3f;

        private EdgeStretchImage picture;
        // The frame before a cut, laid over the picture while it dissolves away.
        private EdgeStretchImage leaving;
        private bool stopped;

        // Over wall, the canvas area it fills.
        public static TransitionClipPlayer Create(RectTransform wall, float aspect)
        {
            var root = new GameObject("Transition Clip", typeof(RectTransform), typeof(Canvas), typeof(EdgeStretchImage));
            var rect = (RectTransform)root.transform;
            rect.SetParent(wall, false);
            root.layer = wall.gameObject.layer;
            rect.sizeDelta = wall.rect.size;
            var canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            var player = root.AddComponent<TransitionClipPlayer>();
            player.picture = root.GetComponent<EdgeStretchImage>();
            player.picture.Configure(aspect, StretchedEdge);
            player.picture.raycastTarget = false;
            player.picture.color = Color.clear;
            player.leaving = CreateLeaving(rect, aspect);
            return player;
        }

        private static EdgeStretchImage CreateLeaving(RectTransform parent, float aspect)
        {
            var holder = new GameObject("Leaving Frame", typeof(RectTransform), typeof(EdgeStretchImage));
            var rect = (RectTransform)holder.transform;
            rect.SetParent(parent, false);
            holder.layer = parent.gameObject.layer;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = holder.GetComponent<EdgeStretchImage>();
            image.Configure(aspect, StretchedEdge);
            image.raycastTarget = false;
            image.color = Color.clear;
            return image;
        }

        // Plays clip to its end, calling ending once endingSeconds before it (for a fade that should
        // finish as the clip does).
        public IEnumerator Play(TransitionClip clip, float endingSeconds, Action ending)
        {
            bool endingStarted = false;
            float dissolveLeft = 0f;
            for (float time = 0f; time < clip.Seconds && !stopped; time += Time.unscaledDeltaTime)
            {
                // Jumps over the clip's cuts.
                float skipped = clip.SkipCuts(time);
                if (skipped != time)
                {
                    // The frame still showing is the last one before the cut.
                    leaving.texture = picture.texture;
                    leaving.uvRect = picture.uvRect;
                    dissolveLeft = DissolveSeconds;
                    time = skipped;
                    if (time >= clip.Seconds) break;
                }

                picture.texture = clip.FrameAt(time, out Rect area);
                picture.uvRect = area;
                float eased = Mathf.SmoothStep(0f, 1f, time / EaseInSeconds);
                picture.color = new Color(1f, 1f, 1f, eased);
                picture.rectTransform.localScale = Vector3.one * Mathf.Lerp(EaseInFrom, 1f, eased);
                float leavingAlpha = Mathf.SmoothStep(0f, 1f, dissolveLeft / DissolveSeconds);
                leaving.color = new Color(1f, 1f, 1f, eased * leavingAlpha);
                dissolveLeft = Mathf.Max(0f, dissolveLeft - Time.unscaledDeltaTime);
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
