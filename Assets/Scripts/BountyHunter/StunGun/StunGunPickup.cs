using UnityEngine;

namespace BountyHunter.Thieves
{
    // One stun gun waiting on the floor: it pops up with a little overshoot, then bobs gently and,
    // every couple of seconds, gives a short excited jiggle (a quick shake and a hop) so the players
    // notice it; picked up, it swells and fades away.
    public sealed class StunGunPickup : MonoBehaviour
    {
        private const float Height = 0.04f;
        private const float PopSeconds = 0.4f;
        private const float CollectSeconds = 0.25f;
        // Over the hiding spots (5), under the thieves (8).
        private const int Order = 6;
        // The jiggle: how often, how long, how far it shakes (degrees) and how much it hops.
        private const float JiggleEverySeconds = 1.8f;
        private const float JiggleSeconds = 0.45f;
        private const float JiggleDegrees = 14f;
        private const float JiggleHop = 0.18f;

        private SpriteRenderer picture;
        private float scale;
        private float age;
        private float collectedAt = -1f;

        public static StunGunPickup Create(Transform parent, Sprite icon, float width, Vector3 at)
        {
            var holder = new GameObject("Stun Gun Pickup", typeof(SpriteRenderer));
            holder.transform.SetParent(parent, false);
            holder.transform.position = new Vector3(at.x, Height, at.z);
            var pickup = holder.AddComponent<StunGunPickup>();
            pickup.picture = holder.GetComponent<SpriteRenderer>();
            pickup.picture.sprite = icon;
            pickup.picture.sortingOrder = Order;
            pickup.scale = width / Mathf.Max(0.01f, icon.bounds.size.x);
            pickup.Update();
            return pickup;
        }

        // Picked up: it swells and fades, then removes itself.
        public void Collect()
        {
            if (collectedAt < 0f) collectedAt = age;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (collectedAt >= 0f)
            {
                float gone = (age - collectedAt) / CollectSeconds;
                if (gone >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }

                transform.localScale = Vector3.one * (scale * (1f + 0.6f * gone));
                picture.color = new Color(1f, 1f, 1f, 1f - gone);
                return;
            }

            float pop = EaseOutBack(Mathf.Clamp01(age / PopSeconds));
            float bob = 1f + 0.06f * Mathf.Sin(age * 3.2f);
            float jiggle = Mathf.Repeat(age, JiggleEverySeconds) / JiggleSeconds;
            float shake = 0f;
            float hop = 0f;
            if (age > PopSeconds && jiggle < 1f)
            {
                float fade = 1f - jiggle;
                shake = JiggleDegrees * Mathf.Sin(jiggle * Mathf.PI * 6f) * fade;
                hop = JiggleHop * Mathf.Sin(jiggle * Mathf.PI);
            }

            transform.localScale = Vector3.one * (scale * pop * (bob + hop));
            transform.rotation = Quaternion.Euler(90f, shake, 0f);
        }

        // Runs a little past 1 before settling on it.
        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float back = t - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }
    }
}
