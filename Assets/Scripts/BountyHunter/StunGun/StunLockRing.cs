using UnityEngine;

namespace BountyHunter.Thieves
{
    // The lock-on reticle a stun gun draws round its target while the lock builds: a thin ring with
    // four ticks, turning slowly, and a thick ring filling round it (the radial fill material's
    // _Fill), closing in tight on the thief and turning from its first colour to its locked colour
    // (yellow to red) as it fills. Lies on the floor under the thief, centred on its body. StunGuns
    // moves it.
    public sealed class StunLockRing : MonoBehaviour
    {
        private const float Height = 0.045f;
        // Under the thief (8), over the hiding spots (5).
        private const int Order = 7;
        // Its size as it starts and as the lock completes (shares of its width), and how fast the
        // ticks turn.
        private const float StartSize = 1.15f;
        private const float LockedSize = 0.75f;
        private const float TurnDegreesPerSecond = 70f;
        private static readonly int FillId = Shader.PropertyToID("_Fill");

        private SpriteRenderer back;
        private SpriteRenderer fill;
        private MaterialPropertyBlock block;
        private Color colour;
        private Color lockedColour;
        // Its scale at the given width, before it closes in.
        private float baseScale;

        public static StunLockRing Create(Transform parent, Sprite backSprite, Sprite fillSprite, Material radialFill, float width, Color colour, Color lockedColour)
        {
            var root = new GameObject("Stun Lock Ring");
            root.transform.SetParent(parent, false);
            var ring = root.AddComponent<StunLockRing>();
            ring.colour = colour;
            ring.lockedColour = lockedColour;
            ring.block = new MaterialPropertyBlock();
            ring.back = Part(root.transform, "Back", backSprite, Order);
            ring.fill = Part(root.transform, "Fill", fillSprite, Order);
            if (radialFill != null) ring.fill.sharedMaterial = radialFill;
            ring.baseScale = width / Mathf.Max(0.01f, backSprite.bounds.size.x);
            root.transform.localScale = Vector3.one * ring.baseScale;
            ring.Hide();
            return ring;
        }

        // Round a spot on the floor, filled this far (0 to 1).
        public void Show(Vector3 at, float filled)
        {
            filled = Mathf.Clamp01(filled);
            transform.SetPositionAndRotation(new Vector3(at.x, Height, at.z), Quaternion.Euler(90f, 0f, 0f));
            float closing = Mathf.Lerp(StartSize, LockedSize, 1f - (1f - filled) * (1f - filled));
            transform.localScale = Vector3.one * (baseScale * closing);
            Color now = Color.Lerp(colour, lockedColour, filled);
            back.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * TurnDegreesPerSecond);
            back.enabled = true;
            fill.enabled = true;
            back.color = new Color(now.r, now.g, now.b, now.a * 0.55f);
            fill.color = now;
            fill.GetPropertyBlock(block);
            block.SetFloat(FillId, filled);
            fill.SetPropertyBlock(block);
        }

        public void Hide()
        {
            back.enabled = false;
            fill.enabled = false;
        }

        private static SpriteRenderer Part(Transform parent, string name, Sprite sprite, int order)
        {
            var part = new GameObject(name, typeof(SpriteRenderer));
            part.transform.SetParent(parent, false);
            var picture = part.GetComponent<SpriteRenderer>();
            picture.sprite = sprite;
            picture.sortingOrder = order;
            return picture;
        }
    }
}
