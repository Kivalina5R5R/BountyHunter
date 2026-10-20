using UnityEngine;

namespace BountyHunter.Thieves
{
    // A star's glint: now and then the sparkle grows out of nothing with a little turn and fades back
    // to nothing. The first one comes delaySeconds after it is switched on, then one every
    // everySeconds. Sits on the sparkle picture itself.
    public sealed class Twinkle : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float delaySeconds = 2f;
        [SerializeField, Min(0.5f)] private float everySeconds = 2.7f;
        [SerializeField, Min(0.1f)] private float twinkleSeconds = 0.5f;
        [SerializeField, Range(0f, 180f)] private float turn = 45f;

        private float age;

        private void OnEnable()
        {
            age = 0f;
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float since = age - delaySeconds;
            float t = since < 0f ? 1f : Mathf.Repeat(since, everySeconds) / twinkleSeconds;
            if (t >= 1f)
            {
                transform.localScale = Vector3.zero;
                return;
            }

            transform.localScale = Vector3.one * Mathf.Sin(t * Mathf.PI);
            transform.localRotation = Quaternion.Euler(0f, 0f, turn * t);
        }
    }
}
