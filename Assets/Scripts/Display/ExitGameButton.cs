using UnityEngine;
using UnityEngine.UI;

namespace Fortal.Display
{
    // Closes the game from the kiosk screen so the touch launcher becomes visible again.
    [RequireComponent(typeof(Button))]
    public sealed class ExitGameButton : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Quit);
        }

        public void Quit()
        {
            Debug.Log("[ExitGameButton] Quit requested from the kiosk screen", this);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
