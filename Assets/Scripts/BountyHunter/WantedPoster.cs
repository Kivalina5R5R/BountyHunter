using TMPro;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // One WANTED poster on the wall, tied to the thief drawn on it. Shows that thief's bounty in
    // its Money text; a raise rolls the number up with a "+N$". A poster without a thief is never
    // put up; PosterMotion moves it.
    public sealed class WantedPoster : MonoBehaviour
    {
        [SerializeField] private ThiefController thief;
        [SerializeField] private TMP_Text money;
        // {0} is the bounty.
        [SerializeField] private string moneyFormat = "{0}$";
        // {0} is the raise.
        [SerializeField] private string raiseFormat = "+{0}$";
        // Red like the CAUGHT stamp: the thief got richer and more wanted.
        [SerializeField] private Color raiseColor = new Color(0.76f, 0.09f, 0.07f, 1f);

        private int bounty;

        public ThiefController Thief => thief;
        public int Bounty => bounty;
        public RectTransform Rect => (RectTransform)transform;
        // The Money text's font, so marks drawn on the poster match it.
        public TMP_FontAsset Font => money != null ? money.font : null;

        public void SetBounty(int value)
        {
            bounty = Mathf.Max(0, value);
            if (money != null) RollingLabel.On(money).Show(string.Format(moneyFormat, bounty));
        }

        // Adds to the bounty with the number rolling up, a pop and a floating "+N$".
        public void RaiseBounty(int amount)
        {
            if (amount <= 0) return;

            bounty += amount;
            if (money == null || !isActiveAndEnabled) return;

            RollingLabel roller = RollingLabel.On(money);
            roller.Roll(string.Format(moneyFormat, bounty), 0.35f);
            roller.Pop(0.35f, raiseColor, 0.6f);
            ScorePopup.Play(money, string.Format(raiseFormat, amount), raiseColor);
        }
    }
}
