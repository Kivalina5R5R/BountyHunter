using UnityEngine;
using UnityEngine.UI;

namespace Fortal.Display
{
    // The kiosk screen's TUTORIAL button: brings up the HOW TO PLAY board (TutorialPopup).
    [RequireComponent(typeof(Button))]
    public sealed class TutorialButton : MonoBehaviour
    {
        [SerializeField] private TutorialPopup popup;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnPressed);
        }

        private void OnPressed()
        {
            if (popup != null) popup.Open();
        }
    }
}
