using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace BountyHunter.Thieves
{
    // Plays a green-screen video loop on the wall (the rabbit and the sloth typing) in place of a
    // still picture. The video is keyed by the chroma key shader, so only the character and desk
    // show. The still picture stays until the first video frame is ready, then hides, so the two
    // never show at once. Sits on a RawImage sized to the whole video frame, left clear in the scene
    // so the still picture shows while editing.
    [RequireComponent(typeof(RawImage))]
    public sealed class WallVideo : MonoBehaviour
    {
        [SerializeField] private VideoClip clip;
        // 1 is the video's own speed; lower plays slower.
        [SerializeField, Range(0.1f, 2f)] private float playbackSpeed = 1f;
        [SerializeField] private Shader chromaKeyShader;
        [SerializeField] private Graphic stillPicture;

        private RawImage image;
        private VideoPlayer player;
        private RenderTexture target;
        private Material material;

        private void Awake()
        {
            image = GetComponent<RawImage>();
            image.raycastTarget = false;
            image.enabled = false;
            if (clip == null || chromaKeyShader == null) return;

            target = new RenderTexture((int)clip.width, (int)clip.height, 0) { name = $"{name} Video" };
            material = new Material(chromaKeyShader) { name = $"{name} Chroma Key" };
            image.texture = target;
            image.material = material;

            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.clip = clip;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = target;
            player.isLooping = true;
            player.skipOnDrop = true;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.playbackSpeed = playbackSpeed;
            player.sendFrameReadyEvents = true;
            player.frameReady += OnFirstFrame;
            player.prepareCompleted += OnPrepared;
            player.Prepare();
        }

        private void OnPrepared(VideoPlayer source)
        {
            source.Play();
        }

        // Swaps the still for the video once there is a picture to show, then stops the per-frame events.
        private void OnFirstFrame(VideoPlayer source, long frame)
        {
            source.frameReady -= OnFirstFrame;
            source.sendFrameReadyEvents = false;
            image.enabled = true;
            image.color = Color.white;
            if (stillPicture != null) stillPicture.enabled = false;
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.frameReady -= OnFirstFrame;
                player.prepareCompleted -= OnPrepared;
            }

            if (target != null) target.Release();
            if (target != null) Destroy(target);
            if (material != null) Destroy(material);
        }
    }
}
