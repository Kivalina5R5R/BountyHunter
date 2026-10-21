using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The rest of the patrol car while it rushes into the intro video. The video only holds what is
    // inside its frame, so the car's back, still to the right of it, would be missing and the car
    // would seem to come out of the building's edge. This shows a picture of the whole car (a later
    // frame of the video, green screen and all, keyed by the same chroma key shader) moving with the
    // car, but only the part right of the video's edge, so the car drives in from behind the parked
    // car in the front (drawn over this). Positions come from keys: the car picture's rectangle in
    // video pixels (left, top, right, bottom; y down) from each key's time until the next, held (not
    // blended) so it moves in step with the video's own frames; before the first key, from the last
    // key on, and once the car is wholly inside the video, nothing shows. Sits on a RawImage that is
    // a child of the video's RawImage (its rectangle is set from the keys).
    [RequireComponent(typeof(RawImage))]
    public sealed class ArrivalTail : MonoBehaviour
    {
        [SerializeField] private WallVideo arrival;
        [SerializeField] private Shader chromaKeyShader;
        [SerializeField] private Vector2 videoSize = new Vector2(1918f, 1080f);
        // Seconds into the video, rising; and where the car picture is then.
        [SerializeField] private float[] times;
        [SerializeField] private Vector4[] rects;

        private RawImage picture;
        private Material material;

        private void Awake()
        {
            picture = GetComponent<RawImage>();
            picture.raycastTarget = false;
            picture.enabled = false;
            if (chromaKeyShader == null) return;

            material = new Material(chromaKeyShader) { name = $"{name} Chroma Key" };
            picture.material = material;
        }

        private void LateUpdate()
        {
            picture.enabled = TryPlace();
        }

        // Puts the picture where the keys say for the video's time, cut at the video's edge; false
        // when nothing of the car is outside the video then.
        private bool TryPlace()
        {
            if (arrival == null || !arrival.IsPlaying || times == null || rects == null) return false;

            int count = Mathf.Min(times.Length, rects.Length);
            float now = (float)arrival.PlaybackTime;
            if (count == 0 || now < times[0] || now >= times[count - 1]) return false;

            int key = 0;
            while (key + 1 < count && times[key + 1] <= now) key++;
            Vector4 rect = rects[key];

            float left = rect.x, top = rect.y, right = rect.z, bottom = rect.w;
            if (right <= videoSize.x || right <= left) return false;

            // Only what is right of the video's edge, and the matching part of the picture.
            float shownLeft = Mathf.Max(left, videoSize.x);
            float cut = (shownLeft - left) / (right - left);
            picture.uvRect = new Rect(cut, 0f, 1f - cut, 1f);

            RectTransform place = picture.rectTransform;
            place.anchorMin = new Vector2(shownLeft / videoSize.x, 1f - bottom / videoSize.y);
            place.anchorMax = new Vector2(right / videoSize.x, 1f - top / videoSize.y);
            place.offsetMin = Vector2.zero;
            place.offsetMax = Vector2.zero;
            return true;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
