using UnityEngine;

namespace BountyHunter.Thieves
{
    // The lightning that crackles round whatever a stun gun's bolt hits, in the pictures of the
    // crackling ring round an armed player (StunAura): it bursts out bigger than its spot, flicking
    // from one crackle picture to another, and shrinks back as it fades. Lies flat on the floor.
    // Removes itself.
    public sealed class StunZap : MonoBehaviour
    {
        private const float Height = 0.09f;
        private const float Seconds = 0.4f;
        // How often a second it flicks to another crackle picture.
        private const float FlicksPerSecond = 18f;
        // Over the bolt (29 to 31) and its sparks (32).
        private const int Order = 33;

        private SpriteRenderer picture;
        private Sprite[] crackles;
        private float size;
        private float age;
        private int shown;

        // Round at, diameter metres across, crackling through crackles.
        public static void Play(Vector3 at, Sprite[] crackles, float diameter, Transform parent)
        {
            if (crackles == null || crackles.Length == 0 || crackles[0] == null) return;

            var holder = new GameObject("Stun Zap", typeof(SpriteRenderer));
            holder.transform.SetParent(parent, true);
            holder.transform.SetPositionAndRotation(new Vector3(at.x, Height, at.z), Quaternion.Euler(90f, Random.Range(0f, 360f), 0f));
            var zap = holder.AddComponent<StunZap>();
            zap.picture = holder.GetComponent<SpriteRenderer>();
            zap.picture.sortingOrder = Order;
            zap.crackles = crackles;
            zap.shown = Random.Range(0, crackles.Length);
            zap.picture.sprite = crackles[zap.shown];
            zap.size = diameter / Mathf.Max(0.0001f, crackles[0].bounds.size.x);
            zap.Update();
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = age / Seconds;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            int flick = Mathf.FloorToInt(age * FlicksPerSecond);
            picture.sprite = crackles[(shown + flick) % crackles.Length];
            // Bursts out past its size, then draws back in as it fades.
            float burst = t < 0.25f ? Mathf.Lerp(0.6f, 1.15f, t / 0.25f) : Mathf.Lerp(1.15f, 0.9f, (t - 0.25f) / 0.75f);
            transform.localScale = Vector3.one * (size * burst);
            picture.color = new Color(1f, 1f, 1f, 1f - t * t);
        }
    }
}
