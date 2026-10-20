using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // A soft halo behind a piece that slowly brightens and dims, swelling a little as it brightens.
    // It fades in delaySeconds after it is switched on, so it waits for the piece in front to arrive.
    [RequireComponent(typeof(Graphic))]
    public sealed class GlowPulse : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float delaySeconds = 1f;
        [SerializeField, Min(0.1f)] private float fadeInSeconds = 0.8f;
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.25f;
        [SerializeField, Range(0f, 1f)] private float brightAlpha = 0.6f;
        [SerializeField, Range(0f, 0.3f)] private float swell = 0.06f;
        [SerializeField, Min(0.5f)] private float seconds = 3.4f;

        private Graphic glow;
        private Vector3 restScale;
        private float age;

        private void Awake()
        {
            glow = GetComponent<Graphic>();
            restScale = transform.localScale;
        }

        private void OnEnable()
        {
            age = 0f;
            SetAlpha(0f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float since = age - delaySeconds;
            if (since < 0f) return;

            float fade = Mathf.SmoothStep(0f, 1f, since / fadeInSeconds);
            float beat = 0.5f - 0.5f * Mathf.Cos(since / seconds * Mathf.PI * 2f);
            SetAlpha(fade * Mathf.Lerp(dimAlpha, brightAlpha, beat));
            transform.localScale = restScale * (1f + swell * beat);
        }

        private void SetAlpha(float alpha)
        {
            Color color = glow.color;
            color.a = alpha;
            glow.color = color;
        }
    }
}
