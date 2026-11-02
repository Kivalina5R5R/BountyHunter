using Fortal.Display;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The drop shadow every character on the floor casts (thieves, passers-by, the boss and its
    // disguise): its own picture in see-through black, pushed to the right and a little back like the
    // hiding spots', so its bottom stays level with the feet and the character stands on the floor
    // instead of floating over it. Change the look of all of them here.
    public static class CharacterShadow
    {
        // World X/Z (y is world Z, toward the wall when positive).
        private static readonly Vector2 Offset = new Vector2(0.06f, 0.025f);
        private static readonly Color Colour = new Color(0f, 0f, 0f, 0.5f);

        public static void Add(SpriteRenderer body)
        {
            if (body != null) DropShadow.Add(body, Offset, Colour);
        }
    }
}
