using UnityEngine;

namespace BountyHunter.Thieves
{
    // One stun gun waiting on the floor, shown as a lightning bolt hovering over its shadow: it pops
    // up with a little overshoot, then floats, rising up the screen and swelling a little as it goes
    // up while its shadow shrinks and fades, then sinking back, with a slow sway. Every few seconds
    // it calls for attention: it swells quickly, shakes hard while it is big and settles back down;
    // each pickup keeps its own timing so they do not all move together. Picked up by a player, it
    // swells and fades away; snatched by a thief, it spins, shrinks to nothing and is gone.
    public sealed class StunGunPickup : MonoBehaviour
    {
        private const float Height = 0.04f;
        private const float PopSeconds = 0.4f;
        private const float CollectSeconds = 0.25f;
        // The shadow over the hiding spots (5), the bolt over it, both under the thieves (8).
        private const int ShadowOrder = 6;
        private const int Order = 7;
        private static readonly Color ShadowColour = new Color(0f, 0f, 0f, 0.35f);

        // The float: one rise and fall takes this long; at the top the bolt is this far up the screen
        // (shares of its height) and this much bigger; it sways this far either way (degrees).
        private const float FloatSeconds = 1.6f;
        private const float FloatLift = 0.22f;
        private const float FloatSwell = 0.1f;
        private const float SwayDegrees = 6f;

        // The call for attention: how often, how long, how much bigger it gets and how hard it shakes.
        private const float AttentionEverySeconds = 2.6f;
        private const float AttentionSeconds = 0.7f;
        private const float AttentionSwell = 0.4f;
        private const float AttentionDegrees = 16f;
        private const float AttentionShakes = 5f;

        private Transform bolt;
        private SpriteRenderer picture;
        private SpriteRenderer shadow;
        private float scale;
        private float boltHeight;
        private float age;
        private float collectedAt = -1f;
        private bool snatched;
        // Where this pickup is in its float and its attention calls, so pickups differ.
        private float phase;

        public static StunGunPickup Create(Transform parent, Sprite icon, float height, Vector3 at)
        {
            var holder = new GameObject("Stun Gun Pickup");
            holder.transform.SetParent(parent, false);
            holder.transform.SetPositionAndRotation(new Vector3(at.x, Height, at.z), Quaternion.Euler(90f, 0f, 0f));
            var pickup = holder.AddComponent<StunGunPickup>();
            pickup.shadow = Piece(holder.transform, "Shadow", icon, ShadowOrder);
            pickup.shadow.color = ShadowColour;
            pickup.picture = Piece(holder.transform, "Bolt", icon, Order);
            pickup.bolt = pickup.picture.transform;
            pickup.scale = height / Mathf.Max(0.01f, icon.bounds.size.y);
            pickup.boltHeight = icon.bounds.size.y;
            pickup.phase = Random.Range(0f, AttentionEverySeconds);
            pickup.Update();
            return pickup;
        }

        // Picked up: it swells and fades, then removes itself.
        public void Collect()
        {
            if (collectedAt < 0f) collectedAt = age;
        }

        // Snatched by a thief: it spins, shrinks to nothing and fades, then removes itself.
        public void Snatch()
        {
            if (collectedAt >= 0f) return;
            collectedAt = age;
            snatched = true;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (collectedAt >= 0f)
            {
                PlayCollected();
                return;
            }

            transform.localScale = Vector3.one * (scale * EaseOutBack(Mathf.Clamp01(age / PopSeconds)));

            // 0 at the bottom of the float, 1 at the top.
            float up = 0.5f - 0.5f * Mathf.Cos((age + phase) * Mathf.PI * 2f / FloatSeconds);
            float sway = SwayDegrees * Mathf.Sin((age + phase) * Mathf.PI * 2f / (FloatSeconds * 1.7f));
            Attention(out float swell, out float shake);

            bolt.localPosition = new Vector3(0f, boltHeight * FloatLift * (0.35f + up), 0f);
            bolt.localScale = Vector3.one * (1f + FloatSwell * up + swell);
            bolt.localRotation = Quaternion.Euler(0f, 0f, sway + shake);

            // The shadow stays on the floor, smaller and fainter the higher the bolt is.
            float lifted = up + swell;
            shadow.transform.localScale = Vector3.one * (0.95f - 0.15f * lifted);
            Color dark = ShadowColour;
            dark.a *= 1f - 0.4f * Mathf.Clamp01(lifted);
            shadow.color = dark;
        }

        // While a call for attention plays: how much bigger the bolt is and how far it is shaken.
        // It swells quickly, shakes while it is big, then shrinks back.
        private void Attention(out float swell, out float shake)
        {
            swell = 0f;
            shake = 0f;
            if (age < PopSeconds) return;

            float t = Mathf.Repeat(age + phase, AttentionEverySeconds) / AttentionSeconds;
            if (t >= 1f) return;

            float grow = EaseOutBack(Mathf.Clamp01(t / 0.25f));
            float settle = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.65f, 1f, t));
            swell = AttentionSwell * grow * settle;
            float shaking = Mathf.Sin(Mathf.InverseLerp(0.15f, 0.85f, t) * Mathf.PI);
            shake = AttentionDegrees * shaking * Mathf.Sin(t * Mathf.PI * 2f * AttentionShakes);
        }

        private void PlayCollected()
        {
            float gone = (age - collectedAt) / CollectSeconds;
            if (gone >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            if (snatched)
            {
                bolt.localScale = Vector3.one * (1f - gone * gone);
                bolt.localRotation = Quaternion.Euler(0f, 0f, 540f * gone);
            }
            else
            {
                bolt.localScale = Vector3.one * (1f + 0.6f * gone);
            }

            picture.color = new Color(1f, 1f, 1f, 1f - gone);
            Color dark = ShadowColour;
            dark.a *= 1f - gone;
            shadow.color = dark;
        }

        private static SpriteRenderer Piece(Transform holder, string name, Sprite sprite, int order)
        {
            var piece = new GameObject(name, typeof(SpriteRenderer));
            piece.transform.SetParent(holder, false);
            var renderer = piece.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
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
