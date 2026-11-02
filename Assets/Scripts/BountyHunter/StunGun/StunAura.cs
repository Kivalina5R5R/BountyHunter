using UnityEngine;

namespace BountyHunter.Thieves
{
    // The crackling ring round a player carrying stun gun shots, lying on the floor: a glowing ring
    // that brightens and dims gently, with lightning crackling round it. The crackle pictures blend
    // smoothly one into the next rather than snapping, so it flickers without jumping, and the whole
    // ring turns slowly. StunGuns makes one per armed player and moves it with them.
    public sealed class StunAura : MonoBehaviour
    {
        // The ring line's width as a share of its picture's width (the rest is glow and crackle
        // room), so the ring itself comes out the size asked for.
        private const float RingShare = 0.465f;
        // Crackle pictures blended through a second, how far the glow dims (0 to 1) and how often
        // a second, and how fast it turns (degrees a second).
        private const float CracklesPerSecond = 7f;
        private const float GlowDip = 0.18f;
        private const float GlowPulsesPerSecond = 1.6f;
        private const float TurnPerSecond = 25f;
        private const float Height = 0.05f;

        private SpriteRenderer ring;
        private SpriteRenderer fading;
        private SpriteRenderer coming;
        private Sprite[] crackles;
        private float phase;

        // A ring diameter metres across (the ring line itself), drawn at order, crackling through
        // crackles (may be empty).
        public static StunAura Create(Transform parent, Sprite ringPicture, Sprite[] crackles, float diameter, int order)
        {
            var holder = new GameObject("Stun Aura");
            holder.transform.SetParent(parent, false);
            var aura = holder.AddComponent<StunAura>();
            aura.crackles = crackles ?? new Sprite[0];
            aura.ring = aura.Layer("Ring", ringPicture, order);
            aura.fading = aura.Layer("Crackle", null, order);
            aura.coming = aura.Layer("Crackle Next", null, order);
            aura.phase = Random.Range(0f, 10f);
            float across = ringPicture != null ? ringPicture.bounds.size.x * RingShare : 1f;
            holder.transform.localScale = Vector3.one * (diameter / Mathf.Max(0.0001f, across));
            return aura;
        }

        // Round the player standing at at.
        public void Show(Vector3 at)
        {
            float time = Time.time + phase;
            transform.SetPositionAndRotation(new Vector3(at.x, Height, at.z), Quaternion.Euler(90f, time * TurnPerSecond, 0f));
            ring.color = new Color(1f, 1f, 1f, 1f - GlowDip * (0.5f + 0.5f * Mathf.Sin(time * GlowPulsesPerSecond * Mathf.PI * 2f)));
            if (crackles.Length == 0) return;

            // One crackle fading out as the next fades in, eased so neither sits half-shown long.
            float step = time * CracklesPerSecond;
            int index = Mathf.FloorToInt(step);
            float blend = Mathf.SmoothStep(0f, 1f, step - index);
            fading.sprite = crackles[index % crackles.Length];
            coming.sprite = crackles[(index + 1) % crackles.Length];
            fading.color = new Color(1f, 1f, 1f, 1f - blend);
            coming.color = new Color(1f, 1f, 1f, blend);
        }

        private SpriteRenderer Layer(string name, Sprite sprite, int order)
        {
            var piece = new GameObject(name, typeof(SpriteRenderer));
            piece.transform.SetParent(transform, false);
            var picture = piece.GetComponent<SpriteRenderer>();
            picture.sprite = sprite;
            picture.sortingOrder = order;
            return picture;
        }
    }
}
