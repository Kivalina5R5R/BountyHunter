using UnityEngine;

namespace Fortal.Display
{
    // A soft drop shadow under a bush or bin: the same picture in see-through black, just under it
    // and pushed a little toward the bottom right of the floor. Sits on a child of the bush so it
    // follows its sway, flip and creeping, but keeps the same world offset however the bush is turned.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BushShadow : MonoBehaviour
    {
        // Lowered a touch so it never draws through the bush.
        private const float Drop = 0.002f;

        private SpriteRenderer shadow;
        private SpriteRenderer bush;
        private Vector3 worldOffset;

        // offset is in world X/Z (Vector2.y is world Z).
        public static BushShadow Add(SpriteRenderer bush, Vector2 offset, Color color)
        {
            var shadowObject = new GameObject("Shadow", typeof(SpriteRenderer));
            shadowObject.transform.SetParent(bush.transform, false);
            var shadow = shadowObject.AddComponent<BushShadow>();
            shadow.bush = bush;
            shadow.shadow = shadowObject.GetComponent<SpriteRenderer>();
            shadow.shadow.sprite = bush.sprite;
            shadow.shadow.color = color;
            shadow.shadow.sortingOrder = bush.sortingOrder - 1;
            shadow.worldOffset = new Vector3(offset.x, -Drop, offset.y);
            shadow.LateUpdate();
            return shadow;
        }

        private void LateUpdate()
        {
            transform.position = bush.transform.position + worldOffset;
            shadow.flipX = bush.flipX;
            shadow.flipY = bush.flipY;
        }
    }
}
