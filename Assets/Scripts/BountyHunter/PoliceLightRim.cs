using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Splashes the police siren onto one wall character: a copy of the character drawn with the rim
    // shader, lighting only its edge on the side facing the PoliceLight. It copies the still picture
    // at first; once the character's video plays it copies the video frame instead (keying out the
    // green) and takes the video's place, so the lit edge follows the animation. Hidden until a
    // PoliceLight shines on it.
    [RequireComponent(typeof(RawImage))]
    public sealed class PoliceLightRim : MonoBehaviour
    {
        private static readonly int LightDirId = Shader.PropertyToID("_LightDir");
        private static readonly int RimWidthId = Shader.PropertyToID("_RimWidth");
        private const string KeyGreen = "KEY_GREEN";

        // The still character picture whose edge gets lit.
        [SerializeField] private Image source;
        // The character's video, if it has one; leave empty for a still character.
        [SerializeField] private WallVideo video;
        [SerializeField] private Shader rimShader;
        // How far the light reaches in from the edge, in pixels of the still picture.
        [SerializeField, Min(1f)] private float rimWidth = 22f;

        private RawImage image;
        private Material material;

        private void Awake()
        {
            image = GetComponent<RawImage>();
            image.raycastTarget = false;
            if (rimShader == null) return;

            material = new Material(rimShader) { name = $"{name} Police Rim" };
            material.SetFloat(RimWidthId, rimWidth);
            image.material = material;
            if (source != null && source.sprite != null) image.texture = source.sprite.texture;
        }

        private void OnEnable()
        {
            if (video == null) return;

            video.Playing += FollowVideo;
            if (video.Frame != null) FollowVideo(video);
        }

        private void OnDisable()
        {
            if (video != null) video.Playing -= FollowVideo;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        // Lights the edge facing lightPosition (world) in color; a clear color turns it off.
        public void Light(Vector3 lightPosition, Color color)
        {
            if (material == null) return;

            var rect = (RectTransform)transform;
            Vector2 toLight = (Vector2)transform.InverseTransformPoint(lightPosition) - rect.rect.center;
            if (toLight.sqrMagnitude > 0.0001f) material.SetVector(LightDirId, toLight.normalized);
            image.color = color;
        }

        // Covers the video exactly and reads its frame, so the lit edge moves with the character.
        private void FollowVideo(WallVideo playing)
        {
            if (material == null || playing.Frame == null) return;

            var rect = (RectTransform)transform;
            var videoRect = (RectTransform)playing.transform;
            rect.anchorMin = videoRect.anchorMin;
            rect.anchorMax = videoRect.anchorMax;
            rect.pivot = videoRect.pivot;
            rect.anchoredPosition = videoRect.anchoredPosition;
            rect.sizeDelta = videoRect.sizeDelta;

            image.texture = playing.Frame;
            material.EnableKeyword(KeyGreen);
            // The width is set in still-picture pixels; the video has its own pixel size on the wall.
            float videoPixelsPerUnit = playing.Frame.width / Mathf.Max(1f, videoRect.rect.width);
            material.SetFloat(RimWidthId, rimWidth * videoPixelsPerUnit);
        }
    }
}
