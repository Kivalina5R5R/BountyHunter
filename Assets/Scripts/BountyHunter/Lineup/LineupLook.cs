using System;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // One species that can stand in the lineup: its picture and whether it is a thief (every thief
    // stands in each lineup, passers-by fill the rest; any of them can be the culprit). What a
    // suspect carries (a hat and disguises, the money bag) is shown in a row over its head
    // (LineupSuspectView); only that counts for the clues.
    [Serializable]
    public sealed class LineupLook
    {
        [SerializeField] private string species = "Cat";
        [SerializeField] private Sprite body;
        [SerializeField] private bool isThief = true;

        public Sprite Body => body;

        public LineupSpecies ToSpecies()
        {
            return new LineupSpecies(species, isThief);
        }
    }

    // The witness's picture and words for one feature.
    [Serializable]
    public sealed class LineupFeatureArt
    {
        [SerializeField] private LineupFeature feature;
        [SerializeField] private Sprite icon;
        [SerializeField] private string label = "MASK";

        public LineupFeature Feature => feature;
        public Sprite Icon => icon;
        public string Label => label;
    }
}
