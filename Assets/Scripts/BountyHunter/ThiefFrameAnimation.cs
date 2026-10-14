using UnityEngine;

namespace BountyHunter.Thieves
{
    // Plays a thief's run cycle on its sprite while it moves and a breathing loop (or the standing
    // sprite) when it stops. Frames advance by distance travelled, not time, so a slow walk steps
    // slowly and a dash steps fast without the feet sliding. Switching between running and standing
    // cross-fades: the last picture fades out over the new one instead of snapping.
    // Sits on the sprite object; the parent moves.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ThiefFrameAnimation : MonoBehaviour
    {
        [SerializeField] private Sprite standing;
        // Breathing loop played while standing; leave empty to show the standing sprite.
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField, Min(1f)] private float idleFramesPerSecond = 8f;
        // One full cycle in play order (both legs).
        [SerializeField] private Sprite[] runFrames;
        // Same frames with the eyes half closed and closed; leave empty for no blinking.
        [SerializeField] private Sprite[] runFramesHalfBlink;
        [SerializeField] private Sprite[] runFramesBlink;
        // Seconds between blinks while running, picked at random in this range.
        [SerializeField] private Vector2 blinkIntervalSeconds = new Vector2(2f, 4.5f);
        [SerializeField, Min(0.05f)] private float blinkSeconds = 0.18f;
        // World distance covered by one full cycle of runFrames.
        [SerializeField, Min(0.05f)] private float cycleLength = 0.45f;
        // Below this speed (units per second) it counts as standing.
        [SerializeField, Min(0f)] private float standSpeed = 0.05f;
        // Brief stops shorter than this keep the current frame, so turning does not flicker.
        [SerializeField, Min(0f)] private float stopDelay = 0.12f;
        // How long the old pose fades out when switching between running and standing.
        [SerializeField, Min(0f)] private float crossfadeSeconds = 0.15f;

        private SpriteRenderer spriteRenderer;
        private SpriteRenderer fadeRenderer;
        private Vector3 lastRootPosition;
        private float cycle;
        private float stoppedFor;
        private float idleTime;
        private float fadeTime = float.MaxValue;
        private bool standingPose = true;
        private float nextBlinkAt;
        private float blinkStartedAt = -1f;

        private Transform Root => transform.parent != null ? transform.parent : transform;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            lastRootPosition = Root.position;
            if (standing == null) standing = spriteRenderer.sprite;
            fadeRenderer = CreateFadeRenderer();
            ScheduleBlink();
        }

        private void LateUpdate()
        {
            Vector3 rootPosition = Root.position;
            Vector3 step = rootPosition - lastRootPosition;
            lastRootPosition = rootPosition;
            step.y = 0f;
            float distance = step.magnitude;
            float speed = distance / Mathf.Max(Time.deltaTime, 0.0001f);

            if (runFrames != null && runFrames.Length > 0)
            {
                if (speed < standSpeed) UpdateStanding();
                else UpdateRunning(distance);
            }

            UpdateFade();
        }

        private void UpdateStanding()
        {
            stoppedFor += Time.deltaTime;
            if (stoppedFor < stopDelay) return;

            if (!standingPose) BeginCrossfade(true);
            cycle = 0f;
            idleTime += Time.deltaTime;
            spriteRenderer.sprite = IdleSprite();
        }

        private void UpdateRunning(float distance)
        {
            stoppedFor = 0f;
            if (standingPose) BeginCrossfade(false);
            idleTime = 0f;
            cycle = Mathf.Repeat(cycle + distance / cycleLength, 1f);
            int frame = Mathf.Min(runFrames.Length - 1, Mathf.FloorToInt(cycle * runFrames.Length));
            spriteRenderer.sprite = BlinkFrames()[frame];
        }

        // Picks the open, half closed or closed version of the run frames for this moment of the
        // blink: half closed, closed, half closed, then open again.
        private Sprite[] BlinkFrames()
        {
            if (!HasFrames(runFramesBlink) || !HasFrames(runFramesHalfBlink)) return runFrames;

            if (blinkStartedAt < 0f)
            {
                if (Time.time < nextBlinkAt) return runFrames;
                blinkStartedAt = Time.time;
            }

            float t = (Time.time - blinkStartedAt) / blinkSeconds;
            if (t >= 1f)
            {
                blinkStartedAt = -1f;
                ScheduleBlink();
                return runFrames;
            }

            return t < 0.25f || t >= 0.75f ? runFramesHalfBlink : runFramesBlink;
        }

        private void ScheduleBlink()
        {
            nextBlinkAt = Time.time + Random.Range(blinkIntervalSeconds.x, Mathf.Max(blinkIntervalSeconds.x, blinkIntervalSeconds.y));
        }

        private bool HasFrames(Sprite[] frames)
        {
            return frames != null && runFrames != null && frames.Length == runFrames.Length;
        }

        private Sprite IdleSprite()
        {
            if (idleFrames == null || idleFrames.Length == 0) return standing;
            int frame = Mathf.FloorToInt(idleTime * idleFramesPerSecond) % idleFrames.Length;
            return idleFrames[frame];
        }

        // Keeps the picture shown so far on top and fades it out while the new pose plays below.
        private void BeginCrossfade(bool toStanding)
        {
            standingPose = toStanding;
            if (crossfadeSeconds <= 0f || spriteRenderer.sprite == null) return;

            fadeRenderer.sprite = spriteRenderer.sprite;
            fadeTime = 0f;
        }

        private void UpdateFade()
        {
            if (fadeTime >= crossfadeSeconds)
            {
                if (fadeRenderer.enabled) fadeRenderer.enabled = false;
                return;
            }

            fadeTime += Time.deltaTime;
            float t = Mathf.Clamp01(fadeTime / crossfadeSeconds);
            fadeRenderer.enabled = spriteRenderer.enabled;
            fadeRenderer.flipX = spriteRenderer.flipX;
            Color color = spriteRenderer.color;
            color.a *= 1f - t * t * (3f - 2f * t);
            fadeRenderer.color = color;
        }

        private SpriteRenderer CreateFadeRenderer()
        {
            var fade = new GameObject("Crossfade", typeof(SpriteRenderer));
            fade.layer = gameObject.layer;
            fade.transform.SetParent(transform, false);
            var fadeSprite = fade.GetComponent<SpriteRenderer>();
            fadeSprite.sortingLayerID = spriteRenderer.sortingLayerID;
            fadeSprite.sortingOrder = spriteRenderer.sortingOrder + 1;
            fadeSprite.sharedMaterial = spriteRenderer.sharedMaterial;
            fadeSprite.enabled = false;
            return fadeSprite;
        }
    }
}
