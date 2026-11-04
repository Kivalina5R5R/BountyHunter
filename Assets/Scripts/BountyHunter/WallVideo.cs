using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace BountyHunter.Thieves
{
    // Plays a green-screen video on the wall (looping: the rabbit and the sloth typing, the player
    // dogs' idles; once: the intro's patrol car arriving) in place of a still picture. The video is
    // keyed by the chroma key shader, so only the character (and desk) show. The still picture stays
    // until the first video frame is ready, then hides, so the two never show at once. Sits on a
    // RawImage sized to the whole video frame, left clear in the scene so the still picture shows
    // while editing. Unity's own looping stalls a moment each time it jumps back to the start, which
    // shows on big characters; Seamless Loop instead keeps a second player waiting at the start and
    // hands over to it just before the end. No player loops by itself (jumping back makes Windows
    // flush the decoder, which fails once several videos are open and freezes the picture): a loop
    // that reaches its end (a seamless one with no spare ready) stops and starts again instead. A
    // player that reports an error is restarted the same way, so a character never stays frozen.
    // Restarts never happen inside the video player's own callbacks (doing so can crash the decoder
    // on some machines): they wait for the next Update, and after an error they wait longer each
    // time, giving up after MostRetries in a row and keeping the last frame.
    [RequireComponent(typeof(RawImage))]
    public sealed class WallVideo : MonoBehaviour
    {
        [SerializeField] private VideoClip clip;
        // 1 is the video's own speed; lower plays slower.
        [SerializeField, Range(0.1f, 2f)] private float playbackSpeed = 1f;
        // Off plays the video once and holds its last frame (the intro).
        [SerializeField] private bool loop = true;
        // Loops through a second player with no stall at the loop point; costs a second video frame
        // in memory, so it is meant for the big intro characters.
        [SerializeField] private bool seamlessLoop;
        // Off only gets the video ready; Play starts it (the intro waits for the scene to settle).
        [SerializeField] private bool playOnAwake = true;
        // On drops frames to keep up when the game stalls (fine for loops); off shows every frame,
        // slowing down instead, so a short moment such as the intro's car rushing in is never lost.
        [SerializeField] private bool skipFramesToKeepUp = true;
        [SerializeField] private Shader chromaKeyShader;
        [SerializeField] private Graphic stillPicture;

        // Frames of the video before its end at which the waiting player takes over.
        private const float HandOverFrames = 2.5f;
        // The wait before the first restart after an error, doubling with each error in a row.
        private const float FirstRetrySeconds = 1f;
        private const int MostRetries = 5;
        private const float NoRetry = -1f;

        private RawImage image;
        private VideoPlayer player;
        // Seamless looping only: the player waiting at the start, and whether it has been started.
        private VideoPlayer spare;
        private bool handingOver;
        private Material material;
        private bool prepared;
        private bool playWhenPrepared;
        // Whether the video should be playing: started and, if it does not loop, not yet at its end.
        // Hiding the wall stops a video player; this brings it back when the wall shows again.
        private bool wanted;
        private ScenePreload.Ticket preparation;
        private ScenePreload.Ticket sparePreparation;
        // When (unscaled time) the playing player and the spare start over; NoRetry when they need not.
        private float playerRestartAt = NoRetry;
        private float spareRestartAt = NoRetry;
        // Errors since the video last played through cleanly.
        private int failures;

        // The video frame once it is playing, else null. Raised with the frame when playback starts.
        public RenderTexture Frame { get; private set; }
        public event Action<WallVideo> Playing;
        // Seconds into the video while it plays; 0 before it starts, since a video that is only
        // prepared does not report a time that can be trusted.
        public double PlaybackTime => IsPlaying ? player.time : 0d;
        public bool IsPlaying => player != null && player.isPlaying;
        // Raised when a video that does not loop reaches its end; its last frame stays.
        public event Action<WallVideo> Finished;

        // Set by the first decoder error in any wall video: this machine cannot keep so many videos
        // open, so every seamless loop gives up its second player (halving the videos open) and
        // loops by starting over instead, with a short stall at the loop point.
        private static bool decodersStruggling;

        private bool UsesSpare => loop && seamlessLoop && !decodersStruggling;

        // Play mode can start without reloading the scripts, which would keep the flag from the last run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStruggling()
        {
            decodersStruggling = false;
        }

        private void Awake()
        {
            image = GetComponent<RawImage>();
            image.raycastTarget = false;
            image.enabled = false;
            if (clip == null || chromaKeyShader == null) return;

            material = new Material(chromaKeyShader) { name = $"{name} Chroma Key" };
            image.material = material;

            player = CreatePlayer("Video");
            image.texture = player.targetTexture;
            player.sendFrameReadyEvents = true;
            player.frameReady += OnFirstFrame;
            player.prepareCompleted += OnPrepared;
            player.loopPointReached += OnEnd;
            player.errorReceived += OnError;
            playWhenPrepared = playOnAwake;
            wanted = playOnAwake;
            QueuePreparation();
        }

        private void QueuePreparation()
        {
            if (preparation != null && !preparation.Completed) return;
            preparation = ScenePreload.Enqueue(this, loop ? 10 : 0, PreparePrimary());
        }

        private IEnumerator PreparePrimary()
        {
            if (!isActiveAndEnabled || player == null) yield break;
            player.Prepare();
            float deadline = Time.realtimeSinceStartup + 8f;
            while (isActiveAndEnabled && !player.isPrepared && Time.realtimeSinceStartup < deadline) yield return null;
            if (!player.isPrepared && isActiveAndEnabled) Debug.LogWarning($"[WallVideo] Preparation timed out: {name}; keeping the still picture", this);
        }

        private IEnumerator PrepareSpare()
        {
            if (!isActiveAndEnabled || player == null || !UsesSpare) yield break;
            if (spare == null) spare = CreatePlayer("Video Spare");
            spare.Prepare();
            float deadline = Time.realtimeSinceStartup + 8f;
            while (isActiveAndEnabled && !spare.isPrepared && Time.realtimeSinceStartup < deadline) yield return null;
        }

        private VideoPlayer CreatePlayer(string label)
        {
            var target = new RenderTexture((int)clip.width, (int)clip.height, 0) { name = $"{name} {label}", wrapMode = TextureWrapMode.Clamp };
            var created = gameObject.AddComponent<VideoPlayer>();
            created.playOnAwake = false;
            created.clip = clip;
            created.renderMode = VideoRenderMode.RenderTexture;
            created.targetTexture = target;
            // No player loops by itself (see above): one that reaches its end starts over (OnEnd).
            created.isLooping = false;
            created.skipOnDrop = skipFramesToKeepUp;
            created.audioOutputMode = VideoAudioOutputMode.None;
            created.playbackSpeed = playbackSpeed;
            return created;
        }

        // Starts the video, at once if it is ready, else as soon as it is.
        public void Play()
        {
            wanted = true;
            if (prepared && player != null) player.Play();
            else
            {
                playWhenPrepared = true;
                if (player != null) QueuePreparation();
            }
            if (preparation != null) preparation.Prioritize();
        }

        private void OnPrepared(VideoPlayer source)
        {
            source.prepareCompleted -= OnPrepared;
            prepared = true;
            if (playWhenPrepared && isActiveAndEnabled) source.Play();
            if (UsesSpare && (sparePreparation == null || sparePreparation.Completed))
                sparePreparation = ScenePreload.Enqueue(this, 20, PrepareSpare());
        }

        private void OnEnd(VideoPlayer source)
        {
            // A loop starts over from a clean stop (a seamless one only when it missed its hand-over,
            // having no spare ready).
            if (loop)
            {
                failures = 0;
                if (source == player && !handingOver) playerRestartAt = Time.unscaledTime;
                return;
            }

            wanted = false;
            Finished?.Invoke(this);
        }

        // A wall hidden for a while (as during a mini game) stops its video; it carries on when the
        // wall shows again, a seamless hand-over cut short starting over cleanly.
        private void OnEnable()
        {
            if (player == null) return;

            if (handingOver)
            {
                spare.frameReady -= OnSpareFrame;
                spare.sendFrameReadyEvents = false;
                spare.Stop();
                handingOver = false;
            }

            prepared = player.isPrepared;
            if (prepared)
            {
                if (wanted) player.Play();
                if (UsesSpare && (spare == null || !spare.isPrepared) && (sparePreparation == null || sparePreparation.Completed))
                    sparePreparation = ScenePreload.Enqueue(this, 20, PrepareSpare());
                return;
            }

            // Hidden before it was ready: get it ready again and play once it is.
            playWhenPrepared = wanted;
            player.prepareCompleted -= OnPrepared;
            player.prepareCompleted += OnPrepared;
            QueuePreparation();
        }

        private void OnDisable()
        {
            preparation?.Cancel();
            sparePreparation?.Cancel();
        }

        // Swaps the still for the video once there is a picture to show, then stops the per-frame events.
        private void OnFirstFrame(VideoPlayer source, long frame)
        {
            source.frameReady -= OnFirstFrame;
            source.sendFrameReadyEvents = false;
            image.enabled = true;
            image.color = Color.white;
            if (stillPicture != null) stillPicture.enabled = false;
            Frame = source.targetTexture;
            Playing?.Invoke(this);
        }

        // Starts the waiting player a moment before the end, so its first frame is ready in time.
        private void Update()
        {
            RestartWhenDue();
            if (decodersStruggling && spare != null && !handingOver) DropSpare();
            if (!UsesSpare || handingOver || player == null || !player.isPlaying || spare == null || !spare.isPrepared) return;

            double framesLeft = (clip.length - player.time) * clip.frameRate;
            if (framesLeft > HandOverFrames + Time.deltaTime * playbackSpeed * clip.frameRate) return;

            handingOver = true;
            spare.sendFrameReadyEvents = true;
            spare.frameReady += OnSpareFrame;
            spare.Play();
        }

        // The waiting player has its first frame: show it, and send the finished one back to wait.
        private void OnSpareFrame(VideoPlayer source, long frame)
        {
            source.frameReady -= OnSpareFrame;
            source.sendFrameReadyEvents = false;
            image.texture = source.targetTexture;
            Frame = source.targetTexture;

            VideoPlayer finished = player;
            MoveEvents(finished, source);
            player = source;
            spare = finished;
            finished.Stop();
            // Ready again well before the next hand-over, without waiting in the preload queue.
            finished.Prepare();
            handingOver = false;
            failures = 0;
        }

        // The playing player's end and error events follow it as the two players swap.
        private void MoveEvents(VideoPlayer from, VideoPlayer to)
        {
            from.loopPointReached -= OnEnd;
            from.errorReceived -= OnError;
            to.loopPointReached -= OnEnd;
            to.loopPointReached += OnEnd;
            to.errorReceived -= OnError;
            to.errorReceived += OnError;
        }

        // The decoder failed: after a wait, the player starts over from a clean stop if it should be
        // playing, and a failed spare is only made ready again.
        private void OnError(VideoPlayer source, string message)
        {
            if (source == spare && handingOver)
            {
                source.frameReady -= OnSpareFrame;
                source.sendFrameReadyEvents = false;
                handingOver = false;
            }

            if (!decodersStruggling)
            {
                decodersStruggling = true;
                Debug.LogWarning("[WallVideo] A video decoder failed: seamless loops are off from now on, to keep fewer videos open", this);
            }

            failures++;
            if (failures > MostRetries)
            {
                playerRestartAt = NoRetry;
                spareRestartAt = NoRetry;
                if (failures == MostRetries + 1) Debug.LogWarning($"[WallVideo] {name}: {message}; giving up after {MostRetries} restarts, keeping the last frame", this);
                return;
            }

            float wait = FirstRetrySeconds * (1 << (failures - 1));
            Debug.LogWarning($"[WallVideo] {name}: {message}; restarting it in {wait:0}s", this);
            if (source == player) playerRestartAt = Time.unscaledTime + wait;
            else spareRestartAt = Time.unscaledTime + wait;
        }

        private void RestartWhenDue()
        {
            float now = Time.unscaledTime;
            if (playerRestartAt != NoRetry && now >= playerRestartAt)
            {
                playerRestartAt = NoRetry;
                if (wanted && player != null) Restart(player);
            }

            if (spareRestartAt != NoRetry && now >= spareRestartAt)
            {
                spareRestartAt = NoRetry;
                if (spare != null && !handingOver)
                {
                    spare.Stop();
                    spare.Prepare();
                }
            }
        }

        // Gives up the seamless loop's second player and its decoder for good.
        private void DropSpare()
        {
            sparePreparation?.Cancel();
            spareRestartAt = NoRetry;
            VideoPlayer dropped = spare;
            spare = null;
            dropped.Stop();
            Release(dropped);
            Destroy(dropped);
        }

        // Stop releases the decoder (no flush); Play then gets it ready again and starts from the
        // beginning, the last frame staying on screen meanwhile.
        private static void Restart(VideoPlayer source)
        {
            source.Stop();
            source.Play();
        }

        // Stops the video for good and gives its decoders back (for a video no longer shown, such as
        // the intro once the dogs cover it); Play starts it again.
        public void Close()
        {
            wanted = false;
            playWhenPrepared = false;
            playerRestartAt = NoRetry;
            spareRestartAt = NoRetry;
            preparation?.Cancel();
            sparePreparation?.Cancel();
            if (handingOver && spare != null)
            {
                spare.frameReady -= OnSpareFrame;
                spare.sendFrameReadyEvents = false;
                handingOver = false;
            }

            if (player != null) player.Stop();
            if (spare != null) spare.Stop();
            prepared = false;
            if (player != null)
            {
                player.prepareCompleted -= OnPrepared;
                player.prepareCompleted += OnPrepared;
            }
        }

        private void OnDestroy()
        {
            Release(player);
            Release(spare);
            if (material != null) Destroy(material);
        }

        private void Release(VideoPlayer released)
        {
            if (released == null) return;

            released.frameReady -= OnFirstFrame;
            released.frameReady -= OnSpareFrame;
            released.prepareCompleted -= OnPrepared;
            released.loopPointReached -= OnEnd;
            released.errorReceived -= OnError;
            RenderTexture target = released.targetTexture;
            if (target == null) return;
            target.Release();
            Destroy(target);
        }
    }
}
