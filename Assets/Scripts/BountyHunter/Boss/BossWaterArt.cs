using System;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The pictures for the boss's water shot (BossWaterVolley), drawn for the game in
    // Assets/ART/Minigame/Boss/Water on transparent: the swirling ring warning where it will land, the
    // long water streak shooting down, the fatter blob of water it turns into as it flattens on landing
    // and the burst on the floor. Each may be left empty; the volley then draws a plain ring for the
    // warning, or does without.
    [Serializable]
    public sealed class BossWaterArt
    {
        [SerializeField] private Sprite ring;
        // Head at the bottom, tail up.
        [SerializeField] private Sprite streak;
        // Its bottom edge where it strikes.
        [SerializeField] private Sprite splash;
        [SerializeField] private Sprite impact;

        public Sprite Ring => ring;
        public Sprite Streak => streak;
        public Sprite Splash => splash;
        public Sprite Impact => impact;
    }
}
