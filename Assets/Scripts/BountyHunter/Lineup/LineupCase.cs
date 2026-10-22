using System.Collections.Generic;

namespace BountyHunter.Thieves
{
    // What a suspect in the lineup can be told apart by: only the things it carries, shown in a row
    // over its head (a hat, the bandit mask, glasses, a face mask, a fake moustache, a newspaper
    // with eye holes, white gloves, the money bag). What the species
    // wears in its own art (the otter's cap, the crow's goggles) never counts, so the players only
    // match the clue cards against the rows. The kind of hat (cap or beanie) pins a suspect down
    // more than a hat at all, so the witness gives those last.
    public enum LineupFeature
    {
        Mask,
        Glasses,
        FaceMask,
        Mustache,
        Newspaper,
        Gloves,
        Hat,
        Bag,
        Cap,
        Beanie,
    }

    public enum LineupHat
    {
        None,
        Cap,
        Beanie,
    }

    // A species that can stand in the lineup.
    public sealed class LineupSpecies
    {
        public LineupSpecies(string name, bool isThief)
        {
            Name = name;
            IsThief = isThief;
        }

        public string Name { get; }
        // Every thief stands in each lineup; passers-by fill the rest. Any of them can be the culprit.
        public bool IsThief { get; }
    }

    // One suspect: its species (an index into the case's species) and what it carries.
    public readonly struct LineupSuspect
    {
        public LineupSuspect(int species, LineupHat hat, bool mask, bool glasses, bool faceMask, bool mustache, bool newspaper, bool gloves, bool bag)
        {
            Species = species;
            Hat = hat;
            Mask = mask;
            Glasses = glasses;
            FaceMask = faceMask;
            Mustache = mustache;
            Newspaper = newspaper;
            Gloves = gloves;
            Bag = bag;
        }

        public int Species { get; }
        public LineupHat Hat { get; }
        public bool Mask { get; }
        public bool Glasses { get; }
        public bool FaceMask { get; }
        public bool Mustache { get; }
        public bool Newspaper { get; }
        public bool Gloves { get; }
        public bool Bag { get; }

        // How many things it carries.
        public int Carried => (Hat != LineupHat.None ? 1 : 0) + (Mask ? 1 : 0) + (Glasses ? 1 : 0) + (FaceMask ? 1 : 0)
            + (Mustache ? 1 : 0) + (Newspaper ? 1 : 0) + (Gloves ? 1 : 0) + (Bag ? 1 : 0);
    }

    // One thing the witness says: the culprit has (or does not have) a feature.
    public readonly struct LineupClue
    {
        public LineupClue(LineupFeature feature, bool has)
        {
            Feature = feature;
            Has = has;
        }

        public LineupFeature Feature { get; }
        public bool Has { get; }

        // Whether the culprit has something at all narrows the lineup down gently, so these are said
        // first; the kind of hat is said later.
        public bool IsShared => Feature != LineupFeature.Cap && Feature != LineupFeature.Beanie;
    }

    // A whole puzzle: the suspects in lineup order, which one did it, and the witness's clues in order.
    public sealed class LineupCase
    {
        private readonly IReadOnlyList<LineupSpecies> species;

        public LineupCase(IReadOnlyList<LineupSpecies> species, IReadOnlyList<LineupSuspect> suspects, int culprit, IReadOnlyList<LineupClue> clues)
        {
            this.species = species;
            Suspects = suspects;
            Culprit = culprit;
            Clues = clues;
        }

        public IReadOnlyList<LineupSuspect> Suspects { get; }
        public int Culprit { get; }
        public IReadOnlyList<LineupClue> Clues { get; }

        public LineupSpecies SpeciesOf(int suspect)
        {
            return species[Suspects[suspect].Species];
        }

        // True when the suspect fits the clue.
        public bool Fits(int suspect, LineupClue clue)
        {
            return Has(Suspects[suspect], clue.Feature) == clue.Has;
        }

        // How many suspects still fit after the first clueCount clues.
        public int RemainingAfter(int clueCount)
        {
            int remaining = 0;
            for (int suspect = 0; suspect < Suspects.Count; suspect++)
            {
                if (FitsFirst(suspect, clueCount)) remaining++;
            }

            return remaining;
        }

        // True when the suspect fits every one of the first clueCount clues.
        public bool FitsFirst(int suspect, int clueCount)
        {
            for (int i = 0; i < clueCount; i++)
            {
                if (!Fits(suspect, Clues[i])) return false;
            }

            return true;
        }

        public static bool Has(LineupSuspect suspect, LineupFeature feature)
        {
            LineupHat hat = suspect.Hat;
            switch (feature)
            {
                case LineupFeature.Mask: return suspect.Mask;
                case LineupFeature.Glasses: return suspect.Glasses;
                case LineupFeature.FaceMask: return suspect.FaceMask;
                case LineupFeature.Mustache: return suspect.Mustache;
                case LineupFeature.Newspaper: return suspect.Newspaper;
                case LineupFeature.Gloves: return suspect.Gloves;
                case LineupFeature.Hat: return hat != LineupHat.None;
                case LineupFeature.Bag: return suspect.Bag;
                case LineupFeature.Cap: return hat == LineupHat.Cap;
                default: return hat == LineupHat.Beanie;
            }
        }
    }
}
