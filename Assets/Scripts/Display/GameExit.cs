using UnityEngine;

namespace Fortal.Display
{
    // Closes the game so the kiosk's touch launcher shows again (stops Play mode in the editor).
    public static class GameExit
    {
        public static void Quit(Object requestedBy)
        {
            Debug.Log("[GameExit] Quit requested from the kiosk screen", requestedBy);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
