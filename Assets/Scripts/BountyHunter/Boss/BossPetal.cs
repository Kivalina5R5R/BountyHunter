using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // One rose petal drifting in the boss's petal shower (BossPetalShower), the way a real petal falls:
    // it floats slowly along the wind, swaying gently across it, and tumbles over in three dimensions
    // about an axis that slowly wanders, so from above it shows full face, then turns to a thin
    // sliver and round again (the camera looks straight down, so the turn foreshortens it by itself),
    // while slowly turning in the plane too. It fades in as it comes, fades out from fadeOutAt and
    // removes itself then, or once it has drifted off the left or bottom of the floor.
    public sealed class BossPetal : MonoBehaviour
    {
        private const float Height = 0.07f;
        private const float FadeInSeconds = 0.4f;
        private const float FadeOutSeconds = 0.6f;
        // Past the floor's edge by this much it is out of sight.
        private const float GoneMargin = 0.4f;

        private SpriteRenderer picture;
        private Image wallPicture;
        private float wallScale;
        private Vector3 velocity;
        private Vector3 across;
        private float sway;
        private float swaysPerSecond;
        private float phase;
        private float tumbleDegreesPerSecond;
        private float tumbleAngle;
        private float axisAngle;
        private float axisDriftPerSecond;
        private float spinDegreesPerSecond;
        private float spinAngle;
        private float shade;
        private float fadeOutAt;
        private Rect floor;
        private float elapsed;
        private Vector3 drifted;

        // Sends one off from start along velocity (metres a second, on the floor plane), size metres
        // along its longer side, drawn at sortingOrder, shade (0..1) dimming it and motion scaling how
        // much it sways and turns; it fades out from fadeOutAt (Time.time) and goes once it has.
        public static void Blow(Sprite petal, Vector3 start, Vector3 velocity, float size, int sortingOrder, float shade, float motion,
            float fadeOutAt, Rect floor, Transform parent, float wallScale = 0f)
        {
            var blown = new GameObject("Rose Petal", typeof(RectTransform)).AddComponent<BossPetal>();
            blown.transform.SetParent(parent, false);
            blown.wallScale = wallScale;
            if (wallScale > 0f)
            {
                blown.wallPicture = blown.gameObject.AddComponent<Image>();
                blown.wallPicture.sprite = petal;
                blown.wallPicture.raycastTarget = false;
                Vector2 nativeSize = petal.rect.size;
                blown.wallPicture.rectTransform.sizeDelta = nativeSize * (size * wallScale / Mathf.Max(nativeSize.x, nativeSize.y));
                blown.wallPicture.color = Color.clear;
            }
            else
            {
                blown.picture = blown.gameObject.AddComponent<SpriteRenderer>();
                blown.picture.sprite = petal;
                blown.picture.sortingOrder = sortingOrder;
                blown.picture.flipX = Random.value < 0.5f;
                blown.picture.color = Color.clear;
            }
            blown.velocity = new Vector3(velocity.x, 0f, velocity.z);
            blown.across = new Vector3(-velocity.z, 0f, velocity.x).normalized;
            blown.sway = Random.Range(0.08f, 0.22f) * motion;
            blown.swaysPerSecond = Random.Range(0.35f, 0.8f);
            blown.phase = Random.Range(0f, 2f * Mathf.PI);
            blown.tumbleDegreesPerSecond = Random.Range(90f, 240f) * (Random.value < 0.5f ? -1f : 1f) * motion;
            blown.tumbleAngle = Random.Range(0f, 360f);
            blown.axisAngle = Random.Range(0f, 360f);
            blown.axisDriftPerSecond = Random.Range(-40f, 40f);
            blown.spinDegreesPerSecond = Random.Range(-60f, 60f) * motion;
            blown.spinAngle = Random.Range(0f, 360f);
            // A touch lighter or darker each as well, so they do not all look stamped out.
            blown.shade = shade * Random.Range(0.85f, 1f);
            blown.fadeOutAt = fadeOutAt;
            blown.floor = floor;
            blown.drifted = new Vector3(start.x, Height, start.z);

            Vector3 bounds = petal.bounds.size;
            float native = Mathf.Max(bounds.x, bounds.y);
            if (wallScale <= 0f) blown.transform.localScale = Vector3.one * (native > 0f ? size / native : 0f);
            blown.Place(0f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            drifted += velocity * dt;
            Place(dt);

            float fadeIn = Mathf.Clamp01(elapsed / FadeInSeconds);
            float fadeOut = 1f - Mathf.Clamp01((Time.time - fadeOutAt) / FadeOutSeconds);
            Color colour = new Color(shade, shade, shade, Mathf.Min(fadeIn, fadeOut));
            if (wallPicture != null) wallPicture.color = colour;
            else picture.color = colour;

            bool gone = drifted.x < floor.xMin - GoneMargin || drifted.z < floor.yMin - GoneMargin;
            if (gone || fadeOut <= 0f) Destroy(gameObject);
        }

        // Lies on the floor plane, then tumbles about an axis in its own plane (which slowly wanders)
        // and turns about its middle.
        private void Place(float dt)
        {
            tumbleAngle += tumbleDegreesPerSecond * dt;
            axisAngle += axisDriftPerSecond * dt;
            spinAngle += spinDegreesPerSecond * dt;

            float swing = Mathf.Sin(phase + elapsed * swaysPerSecond * 2f * Mathf.PI) * sway;
            Vector3 position = drifted + across * swing;
            if (wallPicture != null) wallPicture.rectTransform.anchoredPosition = new Vector2(position.x, position.z) * wallScale;
            else transform.position = position;

            Vector3 axis = Quaternion.AngleAxis(axisAngle, Vector3.forward) * Vector3.right;
            Quaternion tumble = Quaternion.AngleAxis(tumbleAngle, axis);
            Quaternion spin = Quaternion.AngleAxis(spinAngle, Vector3.forward);
            if (wallPicture != null) transform.localRotation = spin * tumble;
            else transform.rotation = Quaternion.Euler(90f, 0f, 0f) * spin * tumble;
        }
    }
}
