using System;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Where the score tags stand on the wall in the boss phases (the vault and the chase alike), laid
    // out in the Boss Wall prefab: one spot per tag, in the same order as the score tags in BossChase,
    // each with the tag's middle at its middle and its size by its scale (BossStaging moves and sizes
    // the real tags to match), and inside it where the leader's star goes on that tag (its middle,
    // its width the star's size). Each shows a picture of a tag and a star to lay them out by; the
    // pictures are switched off when the game runs.
    public sealed class BossScoreTagSpots : MonoBehaviour
    {
        [Serializable]
        private sealed class Spot
        {
            [SerializeField] private RectTransform tag;
            [SerializeField] private RectTransform star;

            public RectTransform Tag => tag;
            public RectTransform Star => star;
        }

        [SerializeField] private Spot[] spots = new Spot[0];

        private void Awake()
        {
            foreach (Graphic preview in GetComponentsInChildren<Graphic>(true)) preview.enabled = false;
        }

        // The spot for the score tag at index and where its star goes; false when there is none.
        public bool TryGetSpot(int index, out RectTransform tag, out RectTransform star)
        {
            Spot spot = index >= 0 && index < spots.Length ? spots[index] : null;
            tag = spot?.Tag;
            star = spot?.Star;
            return tag != null;
        }
    }
}
