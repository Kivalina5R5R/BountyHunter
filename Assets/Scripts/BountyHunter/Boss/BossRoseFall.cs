using UnityEngine;

namespace BountyHunter.Thieves
{
    // The boss's calling card at the start of the chase: a red rose drifting down onto the floor. It
    // starts big, as if close to the camera above, and shrinks to its size on the floor as it falls,
    // swaying gently side to side and rocking, slowing as it settles; it lands after fallSeconds and
    // stays until FadeAway, then fades and removes itself.
    public sealed class BossRoseFall : MonoBehaviour
    {
        private const float Height = 0.06f;
        // Length, bloom to stem end, when it starts falling and when it lies on the floor (metres).
        private const float StartLength = 3.4f;
        private const float LandedLength = 0.38f;
        // It sways this far either side, this many times on the way down, less as it nears the floor.
        private const float Sway = 0.35f;
        private const float Sways = 1.75f;
        private const float RockDegrees = 18f;
        // It comes in from a little towards the wall.
        private const float StartRise = 0.5f;
        private const float FadeSeconds = 0.4f;
        // Over everything, like something between the camera and the floor.
        private const int SortingOrder = 31;

        private SpriteRenderer picture;
        private Vector3 landing;
        private float fallSeconds;
        private float elapsed;
        private float fadeStartedAt = -1f;

        public static BossRoseFall Drop(Sprite rose, Vector3 landing, float fallSeconds, Transform parent)
        {
            var fall = new GameObject("Boss Rose").AddComponent<BossRoseFall>();
            fall.transform.SetParent(parent, false);
            fall.picture = fall.gameObject.AddComponent<SpriteRenderer>();
            fall.picture.sprite = rose;
            fall.picture.sortingOrder = SortingOrder;
            fall.landing = new Vector3(landing.x, Height, landing.z);
            fall.fallSeconds = Mathf.Max(0.1f, fallSeconds);
            fall.Place(0f);
            return fall;
        }

        // Fades out where it lies (the smoke takes over).
        public void FadeAway()
        {
            if (fadeStartedAt < 0f) fadeStartedAt = elapsed;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            Place(Mathf.Clamp01(elapsed / fallSeconds));
            if (fadeStartedAt < 0f) return;

            float fade = Mathf.Clamp01((elapsed - fadeStartedAt) / FadeSeconds);
            picture.color = new Color(1f, 1f, 1f, 1f - fade);
            if (fade >= 1f) Destroy(gameObject);
        }

        // fallen: 0 at the start, 1 on the floor.
        private void Place(float fallen)
        {
            // Quick at first, settling gently at the end, like a petal on the air.
            float settled = 1f - (1f - fallen) * (1f - fallen);
            float calm = 1f - settled;
            float wave = Mathf.Sin(fallen * Sways * 2f * Mathf.PI);
            float rock = Mathf.Cos(fallen * Sways * 2f * Mathf.PI);
            transform.position = landing + new Vector3(wave * Sway * calm, 0f, StartRise * calm);
            transform.rotation = Quaternion.AngleAxis(rock * RockDegrees * calm, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            float length = Mathf.Lerp(StartLength, LandedLength, settled);
            float native = picture.sprite.bounds.size.y;
            transform.localScale = Vector3.one * (native > 0f ? length / native : 0f);
        }
    }
}
