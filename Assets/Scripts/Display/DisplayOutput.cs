namespace Fortal.Display
{
    // Unity display slots for each installation role (Game view Display 1, 2, 3).
    // Display 1 is the main window, which stays on the touch kiosk so the launcher can be covered.
    public static class DisplayOutput
    {
        public const int KioskDisplayIndex = 0;

        public const int WallDisplayIndex = 1;

        public const int FloorDisplayIndex = 2;
    }
}
