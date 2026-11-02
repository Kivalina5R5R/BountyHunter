using UnityEngine;
using UnityEngine.UI;

namespace Fortal.Display
{
    // The kiosk screen's EXIT button: asks first (ExitConfirmDialog), so a stray touch does not close
    // the game; with no dialog set it closes the game at once.
    [RequireComponent(typeof(Button))]
    public sealed class ExitGameButton : MonoBehaviour
    {
        [SerializeField] private ExitConfirmDialog confirmDialog;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnPressed);
        }

        private void OnPressed()
        {
            if (confirmDialog != null) confirmDialog.Open();
            else GameExit.Quit(this);
        }
    }
}
