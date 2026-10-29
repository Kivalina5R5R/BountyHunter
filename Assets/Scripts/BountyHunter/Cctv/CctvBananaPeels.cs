using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The banana peels in mini game 2: now and then, as a thief dives into a bin, a peel flies out of
    // it, slaps onto the camera's screen, sticks there a moment and slowly slides down off it, hiding
    // part of the clip. Sits on a box stretched over the screen in Assets/Prefabs/Cctv/Cctv Wall.prefab;
    // its Peel child is the picture every peel is copied from, switched off, at the size a peel has
    // once it is stuck on the screen (resize it or swap its picture there).
    public sealed class CctvBananaPeels : MonoBehaviour
    {
        [SerializeField] private Image peel;
        // How likely a peel flies out each time a thief lands in a bin, and how many can be up at once.
        [SerializeField, Range(0f, 1f)] private float chance = 0.35f;
        [SerializeField, Min(1)] private int mostAtOnce = 2;

        [Header("Flying out")]
        [SerializeField, Min(0.05f)] private float flySeconds = 0.35f;
        // How big it is as it leaves the bin, against its stuck size.
        [SerializeField, Range(0.05f, 1f)] private float startSize = 0.25f;
        // Where on the screen it sticks: this far up it (0 bottom, 1 top), picked between the two at
        // random, and at most landSpread to either side of the bin it came out of (screen units).
        [SerializeField] private Vector2 landHeight = new Vector2(0.45f, 0.75f);
        [SerializeField, Min(0f)] private float landSpread = 180f;
        // How far it is tilted once stuck, either way, and how much it turns over while flying.
        [SerializeField, Range(0f, 90f)] private float tilt = 25f;
        [SerializeField, Range(0f, 720f)] private float flySpin = 360f;

        [Header("Stuck")]
        // How much it squashes as it slaps onto the screen.
        [SerializeField, Range(0f, 0.5f)] private float splat = 0.18f;
        [SerializeField, Min(0.05f)] private float splatSeconds = 0.2f;
        [SerializeField, Min(0f)] private float stickSeconds = 0.6f;

        [Header("Sliding down")]
        // How long it takes from where it stuck to off the bottom of the screen; it starts slow.
        [SerializeField, Min(0.1f)] private float slideSeconds = 2.4f;
        [SerializeField, Range(0f, 30f)] private float slideWobble = 6f;

        private readonly List<RectTransform> flying = new List<RectTransform>();

        private void Awake()
        {
            if (peel != null) peel.gameObject.SetActive(false);
        }

        // Throws a peel out of the bin whose middle is at from (in this box's units, from its middle),
        // chance of the time, when there is room for another.
        public void MaybeThrow(Vector2 from)
        {
            if (peel == null || flying.Count >= mostAtOnce || Random.value >= chance) return;

            Throw(from);
        }

        public void Throw(Vector2 from)
        {
            if (peel == null) return;

            Image copy = Instantiate(peel, transform);
            copy.name = "Peel (flying)";
            copy.gameObject.SetActive(true);
            flying.Add(copy.rectTransform);
            StartCoroutine(Fly(copy.rectTransform, from));
        }

        // Takes every peel off the screen at once.
        public void Clear()
        {
            StopAllCoroutines();
            foreach (RectTransform copy in flying)
            {
                if (copy != null) Destroy(copy.gameObject);
            }

            flying.Clear();
        }

        private IEnumerator Fly(RectTransform copy, Vector2 from)
        {
            Rect box = ((RectTransform)transform).rect;
            Vector3 size = peel.rectTransform.localScale;
            float side = Random.value < 0.5f ? -1f : 1f;
            float halfWidth = copy.rect.width * 0.5f;
            float landX = Mathf.Clamp(from.x + Random.Range(-landSpread, landSpread), -box.width * 0.5f + halfWidth, box.width * 0.5f - halfWidth);
            float landY = -box.height * 0.5f + box.height * Random.Range(landHeight.x, landHeight.y);
            Vector2 land = new Vector2(landX, landY);
            float stuckAngle = side * Random.Range(tilt * 0.3f, tilt);

            for (float time = 0f; time < flySeconds; time += Time.deltaTime)
            {
                float u = time / flySeconds;
                float eased = 1f - (1f - u) * (1f - u);
                copy.anchoredPosition = Vector2.Lerp(from, land, eased);
                copy.localScale = size * Mathf.Lerp(startSize, 1f, eased);
                copy.localRotation = Quaternion.Euler(0f, 0f, stuckAngle + side * flySpin * (1f - eased));
                yield return null;
            }

            copy.anchoredPosition = land;
            copy.localRotation = Quaternion.Euler(0f, 0f, stuckAngle);
            for (float time = 0f; time < splatSeconds; time += Time.deltaTime)
            {
                float squash = splat * Mathf.Sin(Mathf.PI * time / splatSeconds);
                copy.localScale = Vector3.Scale(size, new Vector3(1f + squash, 1f - squash, 1f));
                yield return null;
            }

            copy.localScale = size;
            if (stickSeconds > 0f) yield return new WaitForSeconds(stickSeconds);

            // Off the bottom: far enough down that none of it, however it is turned, is left showing.
            float offY = -box.height * 0.5f - Mathf.Max(copy.rect.width, copy.rect.height) * Mathf.Max(size.x, size.y);
            for (float time = 0f; time < slideSeconds; time += Time.deltaTime)
            {
                float u = time / slideSeconds;
                copy.anchoredPosition = new Vector2(landX, Mathf.Lerp(landY, offY, u * u));
                copy.localRotation = Quaternion.Euler(0f, 0f, stuckAngle + slideWobble * Mathf.Sin(u * Mathf.PI * 3f));
                yield return null;
            }

            flying.Remove(copy);
            Destroy(copy.gameObject);
        }
    }
}
