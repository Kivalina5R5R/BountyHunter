using UnityEngine;

namespace BountyHunter.Thieves
{
    // A band of light sweeping across a picture now and then, left to right, clipped to the picture's
    // shape by the Mask this sits on. The first sweep comes delaySeconds after it is switched on
    // (so it waits for the piece to arrive), then one every everySeconds.
    public sealed class ShineSweep : MonoBehaviour
    {
        // The light band, a child of this.
        [SerializeField] private RectTransform band;
        [SerializeField, Min(0f)] private float delaySeconds = 2f;
        [SerializeField, Min(0.5f)] private float everySeconds = 4.5f;
        [SerializeField, Min(0.1f)] private float sweepSeconds = 0.9f;

        private float age;

        private void OnEnable()
        {
            age = 0f;
            if (band != null) band.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (band == null) return;

            age += Time.deltaTime;
            float since = age - delaySeconds;
            float t = since < 0f ? 1f : Mathf.Repeat(since, everySeconds) / sweepSeconds;
            bool sweeping = t < 1f;
            band.gameObject.SetActive(sweeping);
            if (!sweeping) return;

            float eased = t * t * (3f - 2f * t);
            float width = ((RectTransform)transform).rect.width;
            band.anchoredPosition = new Vector2(Mathf.Lerp(-0.75f, 0.75f, eased) * width, 0f);
        }
    }
}
