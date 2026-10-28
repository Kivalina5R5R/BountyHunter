using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The calling card falls past the wall's bottom edge before the floor smoke starts.
    public sealed class BossRoseFall : MonoBehaviour
    {
        private const float HeightShare = 0.22f;
        private const float SwayShare = 0.025f;
        private const float Sways = 1.75f;
        private const float RockDegrees = 18f;
        private Image picture;
        private RectTransform wall;
        private float fallSeconds;
        private float startedAt;

        public bool HasPassed => Time.time - startedAt >= fallSeconds;

        public static BossRoseFall Drop(Sprite rose, RectTransform wall, float fallSeconds)
        {
            var fall = new GameObject("Boss Rose", typeof(RectTransform)).AddComponent<BossRoseFall>();
            fall.transform.SetParent(wall, false);
            fall.wall = wall;
            fall.picture = fall.gameObject.AddComponent<Image>();
            fall.picture.sprite = rose;
            fall.picture.raycastTarget = false;
            fall.picture.rectTransform.anchorMin = fall.picture.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            fall.picture.rectTransform.pivot = new Vector2(0.5f, 0f);
            fall.fallSeconds = Mathf.Max(0.1f, fallSeconds);
            fall.startedAt = Time.time;
            fall.Place(0f);
            return fall;
        }

        private void Update()
        {
            Place(Mathf.Clamp01((Time.time - startedAt) / fallSeconds));
            if (HasPassed) Destroy(gameObject);
        }

        // fallen: 0 above the wall, 1 fully below it; vertical motion never slows to a stop.
        private void Place(float fallen)
        {
            float calm = 1f - fallen;
            float wave = Mathf.Sin(fallen * Sways * 2f * Mathf.PI);
            float rock = Mathf.Cos(fallen * Sways * 2f * Mathf.PI);
            float height = wall.rect.height * HeightShare;
            picture.rectTransform.sizeDelta = new Vector2(height * picture.sprite.rect.width / picture.sprite.rect.height, height);
            float y = Mathf.Lerp(wall.rect.height * 0.5f + height * 0.1f, -wall.rect.height * 0.5f - height * 1.1f, fallen);
            picture.rectTransform.anchoredPosition = new Vector2(wave * wall.rect.width * SwayShare * calm, y);
            transform.localRotation = Quaternion.Euler(0f, 0f, rock * RockDegrees * calm);
        }
    }
}
