using System;
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
    // hands over to it just before the end.
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
        // Hidden while still getting ready, which may cut the preparing short.
        private bool hiddenBeforeReady;

        // The video frame once it is playing, else null. Raised with the frame when playback starts.
        public RenderTexture Frame { get; private set; }
        public event Action<WallVideo> Playing;
        // Seconds into the video while it plays; 0 before it starts, since a video that is only
        // prepared does not report a time that can be trusted.
        public double PlaybackTime => IsPlaying ? player.time : 0d;
        public bool IsPlaying => player != null && player.isPlaying;
        // Raised when a video that does not loop reaches its end; its last frame stays.
        public event Action<WallVideo> Finished;

        private bool UsesSpare => loop && seamlessLoop;

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
            playWhenPrepared = playOnAwake;
            wanted = playOnAwake;
            player.Prepare();

            if (!UsesSpare) return;
            spare = CreatePlayer("Video Spare");
            spare.Prepare();
        }

        private VideoPlayer CreatePlayer(string label)
        {
            var target = new RenderTexture((int)clip.width, (int)clip.height, 0) { name = $"{name} {label}", wrapMode = TextureWrapMode.Clamp };
            var created = gameObject.AddComponent<VideoPlayer>();
            created.playOnAwake = false;
            created.clip = clip;
            created.renderMode = VideoRenderMode.RenderTexture;
            created.targetTexture = target;
            created.isLooping = loop && !seamlessLoop;
            created.skipOnDrop = skipFramesToKeepUp;
            created.audioOutputMode = VideoAudioOutputMode.None;
            created.playbackSpeed = playbackSpeed;
            return created;
        }

        // Starts the video, at once if it is ready, else as soon as it is.
        public void Play()
        {
            if (player == null) return;
            wanted = true;
            if (prepared) player.Play();
            else playWhenPrepared = true;
        }

        private void OnPrepared(VideoPlayer source)
        {
            source.prepareCompleted -= OnPrepared;
            prepared = true;
            if (playWhenPrepared) source.Play();
        }

        private void OnEnd(VideoPlayer source)
        {
            if (loop) return;

            wanted = false;
            Finished?.Invoke(this);
        }

        // A wall hidden for a while (as during a mini game) stops its video; it carries on when the
        // wall shows again, a seamless hand-over cut short starting over cleanly.
        private void OnEnable()
        {
            if (player == null || !wanted) return;

            if (handingOver)
            {
                spare.frameReady -= OnSpareFrame;
                spare.sendFrameReadyEvents = false;
                spare.Stop();
                spare.Prepare();
                handingOver = false;
            }

            if (prepared)
            {
                player.Play();
                return;
            }

            // Hidden before it was ready: get it ready again and play once it is.
            if (!hiddenBeforeReady) return;

            hiddenBeforeReady = false;
            playWhenPrepared = true;
            player.prepareCompleted -= OnPrepared;
            player.prepareCompleted += OnPrepared;
            player.Prepare();
        }

        private void OnDisable()
        {
            if (!prepared) hiddenBeforeReady = true;
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
            if (!UsesSpare || handingOver || player == null || !player.isPlaying) return;

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
            player = source;
            spare = finished;
            finished.Stop();
            finished.Prepare();
            handingOver = false;
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
            RenderTexture target = released.targetTexture;
            if (target == null) return;
            target.Release();
            Destroy(target);
        }
    }
}
