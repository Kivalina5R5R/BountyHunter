using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Fortal.Displays
{
    // The one language the game's words are shown in, English or Thai: set by the kiosk's TH/EN
    // button (the Fortal launcher's UDP "language" command, through ArenaRemoteLink) or B on the
    // keyboard, and read back by the launcher. Everything that shows words in both languages
    // (WallTutorial, TutorialPopup, TutorialRemote) listens to Changed and swaps its lines in place.
    // Console lines start with [GameLanguage].
    public static class GameLanguage
    {
        public const string English = "en";
        public const string Thai = "th";

        // Thai fonts made into dynamic TMP fonts, one per font, shared by everything that uses it.
        private static readonly Dictionary<(Font, TMP_FontAsset), TMP_FontAsset> thaiAssets = new Dictionary<(Font, TMP_FontAsset), TMP_FontAsset>();

        // "en" or "th".
        public static string Code => IsThai ? Thai : English;

        public static bool IsThai { get; private set; }

        // Raised after the language changes.
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // The game always opens in English; only the kiosk's TH/EN (or B) switches it to Thai,
            // and nothing is remembered between runs.
            IsThai = false;
            Changed = null;
            thaiAssets.Clear();
        }

        // code: "th" or "en"; anything else is ignored. Setting the language it already is does nothing.
        public static void Set(string code)
        {
            if (code != Thai && code != English) return;

            bool thai = code == Thai;
            if (thai == IsThai) return;

            IsThai = thai;
            Debug.Log($"[GameLanguage] Now {(thai ? "Thai" : "English")}");
            Changed?.Invoke();
        }

        public static void Toggle()
        {
            Set(IsThai ? English : Thai);
        }

        // English or Thai, whichever the game is in now.
        public static string Pick(string english, string thai)
        {
            return IsThai && !string.IsNullOrEmpty(thai) ? thai : english;
        }

        // The TMP font to draw Thai with: thaiFont made into a dynamic font (once), falling back to
        // englishFont for anything it lacks. Null when there is no Thai font.
        public static TMP_FontAsset ThaiFontAsset(Font thaiFont, TMP_FontAsset englishFont)
        {
            if (thaiFont == null) return null;
            if (thaiAssets.TryGetValue((thaiFont, englishFont), out TMP_FontAsset made) && made != null) return made;

            made = TMP_FontAsset.CreateFontAsset(thaiFont);
            if (made != null && englishFont != null)
            {
                made.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
                made.fallbackFontAssetTable.Add(englishFont);
            }

            thaiAssets[(thaiFont, englishFont)] = made;
            return made;
        }
    }
}
