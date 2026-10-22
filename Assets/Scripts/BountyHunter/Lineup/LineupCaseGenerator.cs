using System;
using System.Collections.Generic;

namespace BountyHunter.Thieves
{
    // Makes a fresh lineup puzzle: six suspects of six different species, every thief species and
    // passers-by for the rest, in a random order, the culprit any one of them; each carries 1 to
    // MostItems random things (a hat, disguises, the money bag), no more than MostFullRows carrying
    // the most and at least one carrying a single thing. The witness's four clues close in
    // on the culprit at the pace the design asks for: after the first clue 3 or 4 suspects still fit,
    // after the second 2 or 3, after the third 1 or 2 (only one in about uniqueAfterThirdShare of the
    // puzzles, so reading well pays off) and after the fourth exactly one. Every clue rules at least
    // one more suspect out, except the fourth when the third already left one. The first two clues
    // are about whether the culprit has a thing at all; the last two may name the kind of hat. At
    // most one clue is about hats (a hat at all, a cap or a beanie), since two would give it away.
    public sealed class LineupCaseGenerator
    {
        private const int SuspectCount = 6;
        private const int ClueCount = 4;
        private const int Attempts = 400;
        // Each suspect carries 1 to MostItems things; at most MostFullRows of them carry MostItems, and at
        // least one carries just one, so the rows over their heads differ in length.
        private const int MostItems = 3;
        private const int MostFullRows = 2;
        // How often a suspect is dealt 1, 2 or 3 things before the rule above is checked.
        private static readonly double[] ItemCountOdds = { 0.35, 0.4, 0.25 };
        // The things a suspect can carry: a hat (a cap or a beanie) and the others in LineupSuspect order.
        private const int ThingKinds = 8;
        // Orders of the lineup tried, to find one whose rows are not lined up by length.
        private const int ArrangeTries = 20;

        private static readonly LineupFeature[] Features = (LineupFeature[])Enum.GetValues(typeof(LineupFeature));

        private readonly IReadOnlyList<LineupSpecies> species;
        private readonly Random random;
        private readonly double uniqueAfterThirdShare;
        private readonly List<int> thieves = new List<int>();
        private readonly List<int> passersBy = new List<int>();

        public LineupCaseGenerator(IReadOnlyList<LineupSpecies> species, Random random, double uniqueAfterThirdShare = 0.4)
        {
            this.species = species;
            this.random = random;
            this.uniqueAfterThirdShare = uniqueAfterThirdShare;
            for (int i = 0; i < species.Count; i++)
            {
                (species[i].IsThief ? thieves : passersBy).Add(i);
            }
        }

        // A puzzle meeting every rule; throws if none turned up (too few species, or no thief).
        public LineupCase Make()
        {
            if (thieves.Count + passersBy.Count < SuspectCount)
            {
                throw new InvalidOperationException($"The lineup needs {SuspectCount} species in all.");
            }

            // Picked once: a pace that is harder to meet must not be swapped for the other on a retry,
            // or the share of each would drift away from uniqueAfterThirdShare.
            int leftAfterThird = random.NextDouble() < uniqueAfterThirdShare ? 1 : 2;
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                List<LineupSuspect> suspects = MakeSuspects();
                int culprit = random.Next(suspects.Count);
                List<LineupClue> clues = FindClues(suspects, culprit, leftAfterThird);
                if (clues != null) return Arrange(suspects, culprit, clues);
            }

            throw new InvalidOperationException("No lineup puzzle fits the rules with these species.");
        }

        // The same suspects in a random order, any but one where the rows over their heads go steadily
        // longer (or shorter) from one end to the other. The clues do not depend on where anyone
        // stands, so they stay as they are.
        private LineupCase Arrange(List<LineupSuspect> suspects, int culprit, List<LineupClue> clues)
        {
            var order = new List<int>();
            for (int i = 0; i < suspects.Count; i++) order.Add(i);

            for (int attempt = 0; attempt < ArrangeTries; attempt++)
            {
                Shuffle(order);
                if (!SortedByLength(suspects, order)) break;
            }

            var arranged = order.ConvertAll(i => suspects[i]);
            return new LineupCase(species, arranged, order.IndexOf(culprit), clues);
        }

        private static bool SortedByLength(List<LineupSuspect> suspects, List<int> order)
        {
            bool longer = true;
            bool shorter = true;
            for (int i = 1; i < order.Count; i++)
            {
                int before = suspects[order[i - 1]].Carried;
                int now = suspects[order[i]].Carried;
                if (now < before) longer = false;
                if (now > before) shorter = false;
            }

            return longer || shorter;
        }

        private List<LineupSuspect> MakeSuspects()
        {
            var kinds = new List<int>(thieves);
            Shuffle(kinds);
            if (kinds.Count > SuspectCount) kinds.RemoveRange(SuspectCount, kinds.Count - SuspectCount);

            var others = new List<int>(passersBy);
            Shuffle(others);
            kinds.AddRange(others.GetRange(0, SuspectCount - kinds.Count));
            Shuffle(kinds);

            int[] counts = DealItemCounts();
            var suspects = new List<LineupSuspect>(SuspectCount);
            for (int i = 0; i < kinds.Count; i++)
            {
                suspects.Add(MakeSuspect(kinds[i], counts[i]));
            }

            return suspects;
        }

        // How many things each suspect carries: 1 to MostItems each, no more than MostFullRows full rows
        // and at least one suspect with a single thing.
        private int[] DealItemCounts()
        {
            var counts = new int[SuspectCount];
            while (true)
            {
                int full = 0;
                int single = 0;
                for (int i = 0; i < counts.Length; i++)
                {
                    counts[i] = DealOne();
                    if (counts[i] == MostItems) full++;
                    if (counts[i] == 1) single++;
                }

                if (full <= MostFullRows && single >= 1) return counts;
            }
        }

        private int DealOne()
        {
            double roll = random.NextDouble();
            for (int count = 1; count <= ItemCountOdds.Length; count++)
            {
                roll -= ItemCountOdds[count - 1];
                if (roll < 0) return count;
            }

            return ItemCountOdds.Length;
        }

        // A suspect carrying count different things, picked at random (a hat is a cap or a beanie).
        private LineupSuspect MakeSuspect(int kind, int count)
        {
            var things = new List<int>();
            for (int i = 0; i < ThingKinds; i++) things.Add(i);
            Shuffle(things);
            bool[] carried = new bool[ThingKinds];
            for (int i = 0; i < count; i++) carried[things[i]] = true;

            // Slot 0 is the hat; the rest are the bandit mask, glasses, face mask, moustache, newspaper,
            // gloves and bag.
            LineupHat hat = carried[0] ? (random.Next(2) == 0 ? LineupHat.Cap : LineupHat.Beanie) : LineupHat.None;
            return new LineupSuspect(kind, hat, carried[1], carried[2], carried[3], carried[4], carried[5], carried[6], carried[7]);
        }

        // Clues true of the culprit, in an order that narrows the lineup as the rules say; null if none.
        private List<LineupClue> FindClues(List<LineupSuspect> suspects, int culprit, int leftAfterThird)
        {
            var candidates = new List<LineupClue>();
            foreach (LineupFeature feature in Features)
            {
                bool has = LineupCase.Has(suspects[culprit], feature);
                candidates.Add(new LineupClue(feature, has));
            }

            Shuffle(candidates);
            var chosen = new List<LineupClue>(ClueCount);
            var fitting = new List<int>();
            for (int i = 0; i < suspects.Count; i++) fitting.Add(i);
            return Search(suspects, candidates, chosen, fitting, leftAfterThird) ? chosen : null;
        }

        private bool Search(List<LineupSuspect> suspects, List<LineupClue> candidates, List<LineupClue> chosen, List<int> fitting, int leftAfterThird)
        {
            if (chosen.Count == ClueCount) return true;

            int step = chosen.Count;
            foreach (LineupClue clue in candidates)
            {
                if (step < 2 && !clue.IsShared) continue;
                if (chosen.Exists(said => said.Feature == clue.Feature)) continue;
                // One clue about hats at most: a second (HAT and then BEANIE) gives the answer away.
                if (IsAboutHats(clue.Feature) && chosen.Exists(said => IsAboutHats(said.Feature))) continue;

                List<int> left = fitting.FindAll(suspect => LineupCase.Has(suspects[suspect], clue.Feature) == clue.Has);
                if (!Allowed(step, fitting.Count, left.Count, leftAfterThird)) continue;

                chosen.Add(clue);
                if (Search(suspects, candidates, chosen, left, leftAfterThird)) return true;
                chosen.RemoveAt(chosen.Count - 1);
            }

            return false;
        }

        private static bool IsAboutHats(LineupFeature feature)
        {
            return feature == LineupFeature.Hat || feature == LineupFeature.Cap || feature == LineupFeature.Beanie;
        }

        // Whether going from before to after suspects at this clue (0 = the first) keeps to the pace.
        private static bool Allowed(int step, int before, int after, int leftAfterThird)
        {
            switch (step)
            {
                case 0: return after == 3 || after == 4;
                case 1: return after < before && (after == 2 || after == 3);
                case 2: return after < before && after == leftAfterThird;
                default: return after == 1 && (after < before || before == 1);
            }
        }

        private void Shuffle<T>(List<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
