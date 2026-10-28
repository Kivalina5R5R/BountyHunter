using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // A black curtain over the wall and the floor together, for fading between screens: down to
    // black, swap what is behind, back up. The wall's is a picture over the wall canvas, the
    // floor's a sprite lying over the whole floor above everything on it. The floor can also fade on
    // its own (the boss transition covers the wall itself).
    public sealed class FadeCurtain : MonoBehaviour
    {
        [SerializeField] private Graphic wall;
        [SerializeField] private SpriteRenderer floor;

        private void Awake()
        {
            // The floor curtain needs a plain picture; one made here keeps the scene free of art for it.
            if (floor != null && floor.sprite == null)
            {
                Texture2D white = Texture2D.whiteTexture;
                floor.sprite = Sprite.Create(white, new Rect(0f, 0f, white.width, white.height), new Vector2(0.5f, 0.5f), white.width);
            }

            Set(0f);
        }

        public IEnumerator Close(float seconds)
        {
            yield return Fade(0f, 1f, seconds, true);
        }

        public IEnumerator Open(float seconds)
        {
            yield return Fade(1f, 0f, seconds, true);
        }

        // The floor only, the wall left as it is.
        public IEnumerator CloseFloor(float seconds)
        {
            yield return Fade(0f, 1f, seconds, false);
        }

        public IEnumerator OpenFloor(float seconds)
        {
            yield return Fade(1f, 0f, seconds, false);
        }

        private IEnumerator Fade(float from, float to, float seconds, bool withWall)
        {
            for (float time = 0f; time < seconds; time += Time.unscaledDeltaTime)
            {
                float u = time / seconds;
                Set(Mathf.Lerp(from, to, u * u * (3f - 2f * u)), withWall);
                yield return null;
            }

            Set(to, withWall);
        }

        private void Set(float darkness, bool withWall = true)
        {
            if (wall != null && withWall)
            {
                Color color = wall.color;
                color.a = darkness;
                wall.color = color;
                wall.enabled = darkness > 0f;
            }

            if (floor != null)
            {
                Color color = floor.color;
                color.a = darkness;
                floor.color = color;
                floor.enabled = darkness > 0f;
            }
        }
    }
}
