using System.Collections.Generic;
using TMPro;

namespace Fortal.Displays
{
    // Swaps a set of texts between their own (English) font and a Thai one as the game's language
    // changes (GameLanguage): Add each text once, then Apply before setting its words.
    public sealed class LanguageFonts
    {
        private readonly List<(TMP_Text Text, TMP_FontAsset English, UnityEngine.Material EnglishMaterial, UnityEngine.Font Thai)> texts =
            new List<(TMP_Text, TMP_FontAsset, UnityEngine.Material, UnityEngine.Font)>();

        // text keeps the font it has now for English; thaiFont (may be null: English font always) for Thai.
        public void Add(TMP_Text text, UnityEngine.Font thaiFont)
        {
            if (text == null) return;
            texts.Add((text, text.font, text.fontSharedMaterial, thaiFont));
        }

        // Puts each text in the font for the language the game is in now.
        public void Apply()
        {
            foreach ((TMP_Text text, TMP_FontAsset english, UnityEngine.Material englishMaterial, UnityEngine.Font thai) in texts)
            {
                if (text == null) continue;

                TMP_FontAsset thaiAsset = GameLanguage.IsThai ? GameLanguage.ThaiFontAsset(thai, english) : null;
                if (thaiAsset != null)
                {
                    if (text.font != thaiAsset) text.font = thaiAsset;
                    continue;
                }

                if (text.font == english) continue;
                text.font = english;
                // The English font's own look (an outline) comes back with it.
                if (englishMaterial != null) text.fontSharedMaterial = englishMaterial;
            }
        }
    }
}
