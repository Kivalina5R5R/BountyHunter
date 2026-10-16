using System.Collections;
using TMPro;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Changes a TextMesh Pro label like a rolling counter: the characters that change slide up and
    // fade out while the new ones rise into place from below; the start both texts share stays put
    // ("STAGE 1-" in "STAGE 1-1" to "STAGE 1-2"). Can also pop the label with a short swell and colour
    // flash. Add it with RollingLabel.On; it copies the label once for the outgoing and steady parts.
    [RequireComponent(typeof(TMP_Text))]
    public sealed class RollingLabel : MonoBehaviour
    {
        // How far the texts travel, as a part of the font size.
        private const float TravelVsFontSize = 0.9f;
        // Hides characters while keeping their space, so every copy lays out the same.
        private const string HideStart = "<color=#00000000>";
        private const string HideEnd = "</color>";

        private TMP_Text label;
        private TMP_Text ghost;
        private TMP_Text steady;
        // The plain text being shown; label.text may hold hiding tags mid-roll.
        private string shown;
        private Vector2 home;
        private Vector3 baseScale;
        private Color baseColor;
        private Coroutine rolling;
        private Coroutine popping;

        // The roller on this label, added the first time it is asked for.
        public static RollingLabel On(TMP_Text text)
        {
            RollingLabel roller = text.GetComponent<RollingLabel>();
            return roller != null ? roller : text.gameObject.AddComponent<RollingLabel>();
        }

        // Rolls to text over seconds; shows it straight away when the label is hidden.
        public void Roll(string text, float seconds)
        {
            Prepare();
            if (shown == text) return;

            string previous = shown;
            StopRolling();
            shown = text;
            if (!isActiveAndEnabled || seconds <= 0f) return;

            int kept = SharedStart(previous, text);
            label.text = Hide(text, 0, kept);
            ghost.text = Hide(previous, 0, kept);
            ghost.color = label.color;
            ghost.gameObject.SetActive(true);
            if (kept > 0)
            {
                steady.text = Hide(text, kept, text.Length - kept);
                steady.color = label.color;
                steady.gameObject.SetActive(true);
            }

            rolling = StartCoroutine(RollRoutine(seconds));
        }

        // Shows text straight away, stopping any roll.
        public void Show(string text)
        {
            Prepare();
            StopRolling();
            shown = text;
            label.text = text;
        }

        // Swells the label by the given part of its size and back, tinting it toward flash.
        public void Pop(float swell, Color flash, float seconds)
        {
            Prepare();
            if (!isActiveAndEnabled) return;

            if (popping != null) StopCoroutine(popping);
            popping = StartCoroutine(PopRoutine(swell, flash, seconds));
        }

        private void Prepare()
        {
            if (label != null) return;

            label = GetComponent<TMP_Text>();
            home = label.rectTransform.anchoredPosition;
            baseScale = label.rectTransform.localScale;
            baseColor = label.color;
            shown = label.text;
            ghost = Copy("Rolling Out");
            steady = Copy("Steady");
        }

        private TMP_Text Copy(string part)
        {
            GameObject copy = Instantiate(label.gameObject, label.transform.parent);
            copy.name = $"{label.name} ({part})";
            Destroy(copy.GetComponent<RollingLabel>());
            copy.transform.SetSiblingIndex(label.transform.GetSiblingIndex() + 1);
            var text = copy.GetComponent<TMP_Text>();
            text.raycastTarget = false;
            copy.SetActive(false);
            return text;
        }

        private void StopRolling()
        {
            if (rolling != null) StopCoroutine(rolling);
            rolling = null;
            ghost.gameObject.SetActive(false);
            steady.gameObject.SetActive(false);
            label.text = shown;
            label.rectTransform.anchoredPosition = home;
            label.alpha = 1f;
        }

        private static int SharedStart(string a, string b)
        {
            int length = Mathf.Min(a.Length, b.Length);
            int shared = 0;
            while (shared < length && a[shared] == b[shared]) shared++;
            return shared;
        }

        // text with count characters from start made invisible.
        private static string Hide(string text, int start, int count)
        {
            if (count <= 0) return text;
            return text.Substring(0, start) + HideStart + text.Substring(start, count) + HideEnd + text.Substring(start + count);
        }

        private IEnumerator RollRoutine(float seconds)
        {
            float travel = label.fontSize * TravelVsFontSize;
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                float u = time / seconds;
                // The new text comes up with a little overshoot; the old one leaves quickly.
                label.rectTransform.anchoredPosition = home + Vector2.down * (travel * (1f - BackOut(u)));
                label.alpha = Mathf.Clamp01(u * 2.5f);
                ghost.rectTransform.anchoredPosition = home + Vector2.up * (travel * (1f - (1f - u) * (1f - u)));
                ghost.alpha = 1f - Mathf.Clamp01(u * 1.6f);
                yield return null;
            }

            rolling = null;
            StopRolling();
        }

        // Punches up fast, then springs back with a squash and a small wobble. The colour holds the
        // flash for a moment before easing back, so it reads clearly.
        private IEnumerator PopRoutine(float swell, Color flash, float seconds)
        {
            const float attack = 0.12f;
            float wobble = Random.value < 0.5f ? -5f : 5f;
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                float u = time / seconds;
                float spring = u < attack ? u / attack : 1f;
                float v = u < attack ? 0f : (u - attack) / (1f - attack);
                float settle = (1f - v) * (1f - v);
                float punch = spring * Mathf.Cos(v * Mathf.PI * 3f) * settle;
                label.rectTransform.localScale = Vector3.Scale(baseScale, new Vector3(1f + swell * punch, 1f + swell * 0.8f * punch, 1f));
                label.rectTransform.localRotation = Quaternion.Euler(0f, 0f, wobble * Mathf.Sin(v * Mathf.PI * 4f) * settle);

                float tint = u < attack ? u / attack : 1f - Mathf.SmoothStep(0.35f, 1f, u);
                Color color = Color.Lerp(baseColor, flash, tint);
                color.a = label.color.a;
                label.color = color;
                yield return null;
            }

            label.rectTransform.localScale = baseScale;
            label.rectTransform.localRotation = Quaternion.identity;
            Color settled = baseColor;
            settled.a = label.color.a;
            label.color = settled;
            popping = null;
        }

        // 0 to 1, running a little past 1 before settling.
        private static float BackOut(float t)
        {
            const float s = 1.6f;
            float u = Mathf.Clamp01(t) - 1f;
            return 1f + u * u * ((s + 1f) * u + s);
        }
    }
}
