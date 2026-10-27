using System;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The pictures for the boss's water shot (BossWaterVolley), drawn for the game in
    // Assets/ART/Boss/Water as glowing light on transparent: the swirling ring warning where it will
    // land, the water streak shooting down, the lightning bolts that flicker as it strikes and the
    // burst on the floor. Each may be left empty; the volley then draws a plain ring for the warning,
    // or does without.
    [Serializable]
    public sealed class BossWaterArt
    {
        [SerializeField] private Sprite ring;
        // Head at the bottom, tail up.
        [SerializeField] private Sprite streak;
        // Bottom tip where it strikes; two or more flicker in turn.
        [SerializeField] private Sprite[] bolts;
        [SerializeField] private Sprite impact;

        public Sprite Ring => ring;
        public Sprite Streak => streak;
        public Sprite[] Bolts => bolts ?? Array.Empty<Sprite>();
        public Sprite Impact => impact;
    }
}
