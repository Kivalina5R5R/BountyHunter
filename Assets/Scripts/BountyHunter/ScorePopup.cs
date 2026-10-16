using TMPro;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // A small "+14$" (or "-5$") that pops out of a number, floats up and fades. Copies the score's text so it
    // shares its font and look, and removes itself when done.
    public sealed class ScorePopup : MonoBehaviour
    {
        private const float Seconds = 0.95f;
        private const float PopSeconds = 0.2f;
        private const float SizeVsScore = 0.62f;
        // Start and rise, as parts of the score's font size.
        private const float StartAbove = 0.35f;
        private const float Rise = 1.1f;

        private TMP_Text text;
        private Vector2 start;
        private Vector3 baseScale;
        // The score's font size in its parent's units; the popup's moves are measured in it.
        private float travel;
        private float elapsed;

        public static void Play(TMP_Text score, string message, Color color)
        {
            GameObject copy = Instantiate(score.gameObject, score.transform.parent);
            copy.name = "Score Popup";
            Destroy(copy.GetComponent<RollingLabel>());
            copy.transform.SetAsLastSibling();

            var popup = copy.AddComponent<ScorePopup>();
            popup.text = copy.GetComponent<TMP_Text>();
            popup.text.text = message;
            popup.text.color = color;
            popup.text.raycastTarget = false;
            popup.text.fontSize = score.fontSize * SizeVsScore;
            popup.text.textWrappingMode = TextWrappingModes.NoWrap;
            popup.text.rectTransform.localRotation = Quaternion.identity;
            popup.baseScale = popup.text.rectTransform.localScale;
            popup.travel = score.fontSize * score.rectTransform.localScale.y;
            popup.start = score.rectTransform.anchoredPosition + Vector2.up * (popup.travel * StartAbove);
            popup.Apply();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= Seconds)
            {
                Destroy(gameObject);
                return;
            }

            Apply();
        }

        private void Apply()
        {
            float u = elapsed / Seconds;
            float rise = 1f - (1f - u) * (1f - u);
            text.rectTransform.anchoredPosition = start + Vector2.up * (travel * Rise * rise);
            text.rectTransform.localScale = baseScale * BackOut(Mathf.Clamp01(elapsed / PopSeconds));
            text.alpha = u < 0.55f ? 1f : 1f - (u - 0.55f) / 0.45f;
        }

        // 0 to 1, running a little past 1 before settling.
        private static float BackOut(float t)
        {
            const float s = 2f;
            float u = t - 1f;
            return 1f + u * u * ((s + 1f) * u + s);
        }
    }
}
