using UnityEngine;

namespace BountyHunter.Thieves
{
    // Plays a thief's run cycle on its sprite while it moves and a breathing loop (or the standing
    // sprite) when it stops. Frames advance by distance travelled, not time, so a slow walk steps
    // slowly and a dash steps fast without the feet sliding. Switching between running and standing
    // cross-fades: the last picture fades out over the new one instead of snapping.
    // A hopper (hopStride filled in) works the other way round: its frames play by time while its
    // mover wants to go (MoveIntent), and the mover only travels as far as StrideNow allows, so it
    // moves while the picture is in the air and stands still while the feet are down.
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
        [SerializeField, Min(0f)] private float stopDelay = 0.25f;
        // How long the old pose fades out when switching between running and standing.
        [SerializeField, Min(0f)] private float crossfadeSeconds = 0.15f;

        [Header("Angry")]
        // An act played once, start to end, when asked (PlayAngry: a passer-by bumped into by
        // mistake). Its pictures may be smaller files than the others but share their centre
        // (Tools/extract_angry_video.py lines them up). Leave empty for none.
        [SerializeField] private Sprite[] angryFrames;
        [SerializeField, Min(1f)] private float angryFramesPerSecond = 10f;
        // The act's last picture fades into standing this slowly (slower than other switches), so a
        // small difference between the two pictures melts away instead of jumping.
        [SerializeField, Min(0f)] private float angryEndFadeSeconds = 0.35f;

        [Header("Hopping")]
        // For a character that hops instead of walking: each run frame's share of the forward travel,
        // 0 with the feet on the ground up to 1 in the air (extract_thief_video.py --hop writes these).
        // Leave empty for an ordinary walk.
        [SerializeField] private float[] hopStride;
        // How fast the hop frames play (the rate they were cut from the video at).
        [SerializeField, Min(1f)] private float hopFramesPerSecond = 15f;

        private SpriteRenderer spriteRenderer;
        private SpriteRenderer fadeRenderer;
        private Vector3 lastRootPosition;
        private float cycle;
        private float stoppedFor;
        private float idleTime;
        private float fadeTime = float.MaxValue;
        // How long the fade under way takes.
        private float fadeSeconds;
        private bool standingPose = true;
        private float nextBlinkAt;
        private float blinkStartedAt = -1f;
        private float hopTime;
        private float strideScale = 1f;
        // Seconds into the angry act, or below 0 when it is not playing.
        private float angryTime = -1f;

        private Transform Root => transform.parent != null ? transform.parent : transform;

        public bool Hops => runFrames != null && runFrames.Length > 0 && hopStride != null && hopStride.Length == runFrames.Length;

        // For a hopper: the speed its mover wants right now, set every frame before LateUpdate.
        public float MoveIntent { get; set; }

        // For a hopper: the share of its walking speed to move at this moment. 0 while standing or
        // with the feet down; over a whole hop it averages 1, so the walking speed is kept.
        public float StrideNow { get; private set; }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            lastRootPosition = Root.position;
            if (standing == null) standing = spriteRenderer.sprite;
            fadeRenderer = CreateFadeRenderer();
            ScheduleBlink();
            if (Hops) strideScale = StrideScale();
        }

        // Shows the standing pose at once (no cross-fade), as when put back after being hidden.
        public void Stand()
        {
            angryTime = -1f;
            standingPose = true;
            stoppedFor = stopDelay;
            cycle = 0f;
            hopTime = 0f;
            idleTime = 0f;
            StrideNow = 0f;
            fadeTime = float.MaxValue;
            if (fadeRenderer != null) fadeRenderer.enabled = false;
            lastRootPosition = Root.position;
            spriteRenderer.sprite = IdleSprite();
        }

        // Starts the angry act from its first picture and returns how long it lasts (0 with none).
        public float PlayAngry()
        {
            if (angryFrames == null || angryFrames.Length == 0) return 0f;

            angryTime = 0f;
            StrideNow = 0f;
            fadeTime = float.MaxValue;
            spriteRenderer.sprite = angryFrames[0];
            return angryFrames.Length / angryFramesPerSecond;
        }

        private void LateUpdate()
        {
            if (angryTime >= 0f)
            {
                UpdateAngry();
                UpdateFade();
                return;
            }

            if (Hops)
            {
                UpdateHopper();
                UpdateFade();
                return;
            }

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

        // Plays the angry act through once, then cross-fades to standing.
        private void UpdateAngry()
        {
            lastRootPosition = Root.position;
            angryTime += Time.deltaTime;
            int frame = Mathf.FloorToInt(angryTime * angryFramesPerSecond);
            if (frame < angryFrames.Length)
            {
                spriteRenderer.sprite = angryFrames[frame];
                return;
            }

            angryTime = -1f;
            hopTime = 0f;
            cycle = 0f;
            idleTime = 0f;
            stoppedFor = stopDelay;
            BeginCrossfade(true, angryEndFadeSeconds);
            spriteRenderer.sprite = IdleSprite();
        }

        // Hops while the mover wants to go, and always finishes a hop it is in the air for, so it
        // never stops in mid-air; stands once it wants to stop with its feet down.
        private void UpdateHopper()
        {
            lastRootPosition = Root.position;
            bool airborne = !standingPose && hopStride[HopFrame()] > 0f;
            if (MoveIntent < standSpeed && !airborne)
            {
                StrideNow = 0f;
                UpdateStanding();
                // The next hop starts from the first frame (feet down) once it has settled.
                if (standingPose) hopTime = 0f;
                return;
            }

            stoppedFor = 0f;
            if (standingPose) BeginCrossfade(false);
            idleTime = 0f;
            hopTime += Time.deltaTime;
            int frame = HopFrame();
            // Blends towards the next frame's stride, so the speed flows instead of stepping.
            float between = Mathf.Repeat(hopTime * hopFramesPerSecond, 1f);
            float next = hopStride[(frame + 1) % hopStride.Length];
            StrideNow = Mathf.Lerp(hopStride[frame], next, between) * strideScale;
            spriteRenderer.sprite = BlinkFrames()[frame];
        }

        private int HopFrame()
        {
            return Mathf.FloorToInt(hopTime * hopFramesPerSecond) % runFrames.Length;
        }

        // Makes StrideNow average 1 over a hop.
        private float StrideScale()
        {
            float total = 0f;
            foreach (float stride in hopStride)
            {
                total += Mathf.Max(0f, stride);
            }

            return total > 0f ? hopStride.Length / total : 1f;
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
            BeginCrossfade(toStanding, crossfadeSeconds);
        }

        private void BeginCrossfade(bool toStanding, float seconds)
        {
            standingPose = toStanding;
            if (seconds <= 0f || spriteRenderer.sprite == null) return;

            fadeRenderer.sprite = spriteRenderer.sprite;
            fadeSeconds = seconds;
            fadeTime = 0f;
        }

        private void UpdateFade()
        {
            if (fadeTime >= fadeSeconds)
            {
                if (fadeRenderer.enabled) fadeRenderer.enabled = false;
                return;
            }

            fadeTime += Time.deltaTime;
            float t = Mathf.Clamp01(fadeTime / fadeSeconds);
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
