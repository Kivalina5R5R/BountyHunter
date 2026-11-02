using UnityEngine;

namespace BountyHunter.Thieves
{
    // The police tape round a taped-off hiding spot: a loop of tape that pops down round it, stays
    // while it is locked and fades away as the lock ends. It is centred on the spot's drawn picture
    // (the art has empty margins) and keeps upright on the floor whatever way the spot is turned. Sits
    // on the hiding spot, so it follows it as it creeps and goes with it when the layout changes.
    // Removes itself.
    public sealed class BushTape : MonoBehaviour
    {
        private const float PopSeconds = 0.3f;
        private const float FadeSeconds = 0.5f;
        // Just over the hiding spot, so its leaves never cover it.
        private const float Lift = 0.01f;
        // Over the hiding spot (5), under the thieves (8).
        private const int Order = 7;

        private SpriteRenderer picture;
        private SpriteRenderer spot;
        private float width;
        private float seconds;
        private float age;

        // Round the hiding spot, width across (metres), for seconds.
        public static void Play(HidingBush bush, Sprite tape, float width, float seconds)
        {
            if (tape == null) return;

            BushTape old = bush.GetComponentInChildren<BushTape>();
            if (old != null) Destroy(old.gameObject);

            var holder = new GameObject("Police Tape", typeof(SpriteRenderer));
            holder.transform.SetParent(bush.transform, false);
            var effect = holder.AddComponent<BushTape>();
            effect.picture = holder.GetComponent<SpriteRenderer>();
            effect.picture.sprite = tape;
            effect.picture.sortingOrder = Order;
            effect.spot = bush.GetComponent<SpriteRenderer>();
            effect.width = width;
            effect.seconds = seconds;
            effect.LateUpdate();
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= seconds)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 centre = spot != null ? spot.bounds.center : transform.parent.position;
            transform.SetPositionAndRotation(new Vector3(centre.x, transform.parent.position.y + Lift, centre.z), Quaternion.Euler(90f, 0f, 0f));
            float pop = EaseOutBack(Mathf.Clamp01(age / PopSeconds));
            float size = width / Mathf.Max(0.01f, picture.sprite.bounds.size.x * transform.parent.lossyScale.x);
            transform.localScale = Vector3.one * (size * pop);
            picture.color = new Color(1f, 1f, 1f, Mathf.Clamp01((seconds - age) / FadeSeconds));
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
