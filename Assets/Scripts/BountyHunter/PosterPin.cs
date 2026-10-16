using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The red push pin holding a WANTED poster to the cork board, with a soft shadow under it.
    // Sits on the posters' layer rather than on the poster, so it stays round while the poster
    // unrolls and can fly off on its own when the poster is torn down.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PosterPin : MonoBehaviour
    {
        private RectTransform rect;
        private CanvasGroup group;

        public Vector2 Position => rect.anchoredPosition;

        // size is the pin head's width in the layer's units.
        public static PosterPin Create(RectTransform layer, float size)
        {
            var pinObject = new GameObject("Pin", typeof(RectTransform), typeof(CanvasGroup));
            pinObject.transform.SetParent(layer, false);
            var pin = pinObject.AddComponent<PosterPin>();
            pin.rect = (RectTransform)pinObject.transform;
            pin.group = pinObject.GetComponent<CanvasGroup>();
            pin.group.blocksRaycasts = false;

            // The shadow falls down and to the right, away from the lamp.
            AddImage(pin.rect, "Shadow", size * 1.05f, new Vector2(size * 0.12f, -size * 0.16f), new Color(0f, 0f, 0f, 0.4f), PosterSprites.Dot);
            AddImage(pin.rect, "Head", size, Vector2.zero, Color.white, PosterSprites.Pin);
            pinObject.SetActive(false);
            return pin;
        }

        private static void AddImage(RectTransform parent, string imageName, float size, Vector2 offset, Color color, Sprite sprite)
        {
            var imageObject = new GameObject(imageName, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            image.rectTransform.anchoredPosition = offset;
        }

        public void Place(Vector2 position, float scale, float alpha, float degrees = 0f)
        {
            gameObject.SetActive(true);
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one * scale;
            rect.localRotation = Quaternion.Euler(0f, 0f, degrees);
            group.alpha = alpha;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
