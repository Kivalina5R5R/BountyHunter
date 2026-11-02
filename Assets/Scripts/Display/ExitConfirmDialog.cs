using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Fortal.Display
{
    // The kiosk's "exit the game?" question: the screen dims, the board pops up with a little
    // overshoot and a wobble, and offers EXIT (closes the game) or CANCEL (the board drops away
    // again). Touching the dimmed screen round it cancels too. Runs on unscaled time, so it works
    // while the game is frozen. Built into the kiosk prefab by KioskExitConfirmBuilder.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ExitConfirmDialog : MonoBehaviour
    {
        [SerializeField] private RectTransform board;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        // The dimmed screen round the board; touching it cancels.
        [SerializeField] private Button backdrop;
        [SerializeField, Min(0.05f)] private float openSeconds = 0.35f;
        [SerializeField, Min(0.05f)] private float closeSeconds = 0.18f;
        // The board's tilt as it pops up, settling to straight.
        [SerializeField, Range(0f, 15f)] private float popTilt = 4f;

        private CanvasGroup group;
        private Coroutine showing;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.AddListener(Close);
            if (backdrop != null) backdrop.onClick.AddListener(Close);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            Play(PopUp());
        }

        public void Close()
        {
            if (!gameObject.activeInHierarchy) return;
            Play(DropAway());
        }

        private void Confirm()
        {
            group.interactable = false;
            GameExit.Quit(this);
        }

        private void Play(IEnumerator routine)
        {
            if (showing != null) StopCoroutine(showing);
            showing = StartCoroutine(routine);
        }

        private IEnumerator PopUp()
        {
            group.interactable = true;
            group.blocksRaycasts = true;
            for (float t = 0f; t < openSeconds; t += Time.unscaledDeltaTime)
            {
                float u = t / openSeconds;
                group.alpha = Mathf.Clamp01(u * 2.5f);
                PlaceBoard(Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(u)), popTilt * (1f - u) * Mathf.Sin(u * Mathf.PI * 3f));
                yield return null;
            }

            group.alpha = 1f;
            PlaceBoard(1f, 0f);
            showing = null;
        }

        private IEnumerator DropAway()
        {
            group.interactable = false;
            float from = group.alpha;
            for (float t = 0f; t < closeSeconds; t += Time.unscaledDeltaTime)
            {
                float u = t / closeSeconds;
                group.alpha = from * (1f - u);
                PlaceBoard(Mathf.Lerp(1f, 0.85f, u * u), 0f);
                yield return null;
            }

            group.alpha = 0f;
            group.blocksRaycasts = false;
            showing = null;
            gameObject.SetActive(false);
        }

        private void PlaceBoard(float scale, float tilt)
        {
            if (board == null) return;
            board.localScale = Vector3.one * scale;
            board.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        // Runs a little past 1 before settling on it.
        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float back = t - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }
    }
}
