using System;
using System.Collections.Generic;

namespace BountyHunter.Thieves
{
    // One thief jumping out of one bin and into another, at start seconds into the clip.
    public readonly struct CctvHop
    {
        public CctvHop(int thief, int from, int to, float start, float seconds)
        {
            Thief = thief;
            From = from;
            To = to;
            Start = start;
            Seconds = seconds;
        }

        public int Thief { get; }
        public int From { get; }
        public int To { get; }
        public float Start { get; }
        public float Seconds { get; }
    }

    // What the security camera shows in mini game 2: the thieves run in and dive into bins one after
    // another (thief i at EnterStart(i), into StartBins[i]), then hop from bin to bin (Hops, in order,
    // never into a bin that is taken). One of them, BagThief, has the money bag; Answer is the bin it
    // ends up in. Times are seconds from the start of the clip, which lasts Length.
    public sealed class CctvClip
    {
        private readonly int[] startBins;
        private readonly int[] finalBins;
        private readonly CctvHop[] hops;
        private readonly float enterStagger;

        public CctvClip(int bins, int[] startBins, int bagThief, CctvHop[] hops, float enterStagger, float enterSeconds, float length)
        {
            Bins = bins;
            this.startBins = startBins;
            BagThief = bagThief;
            this.hops = hops;
            this.enterStagger = enterStagger;
            EnterSeconds = enterSeconds;
            Length = length;

            finalBins = (int[])startBins.Clone();
            foreach (CctvHop hop in hops) finalBins[hop.Thief] = hop.To;
        }

        public int Bins { get; }
        public int Thieves => startBins.Length;
        public int BagThief { get; }
        public IReadOnlyList<int> StartBins => startBins;
        public IReadOnlyList<int> FinalBins => finalBins;
        public IReadOnlyList<CctvHop> Hops => hops;
        // How long one thief's run and dive into its first bin takes.
        public float EnterSeconds { get; }
        public float Length { get; }
        public int Answer => finalBins[BagThief];

        public float EnterStart(int thief)
        {
            return thief * enterStagger;
        }

        // The thief in bin at the end of the clip, or -1 when it is empty.
        public int ThiefIn(int bin)
        {
            return Array.IndexOf(finalBins, bin);
        }
    }

    // Makes a new random clip for every game. Each hop is quicker than the one before (by speedUp),
    // half of them (rounded up) are the bag thief's, a thief never hops straight back into the bin it
    // just left when another is free, and the bag never ends in the bin it started in, so watching only
    // the start is not enough.
    public sealed class CctvClipMaker
    {
        private const int Tries = 50;

        private readonly Random random;
        private readonly float firstHopSeconds;
        private readonly float speedUp;
        private readonly float gapSeconds;
        private readonly float enterStagger;
        private readonly float enterSeconds;
        private readonly float endHoldSeconds;

        // firstHopSeconds: how long the first hop takes; speedUp: each hop and gap after it takes this
        // much of the one before (below 1 is faster); gapSeconds: the pause after the first hop;
        // enterStagger: between one thief's entrance and the next; endHoldSeconds: still frames after
        // the last hop.
        public CctvClipMaker(Random random, float firstHopSeconds, float speedUp, float gapSeconds, float enterStagger, float enterSeconds, float endHoldSeconds)
        {
            this.random = random;
            this.firstHopSeconds = firstHopSeconds;
            this.speedUp = speedUp;
            this.gapSeconds = gapSeconds;
            this.enterStagger = enterStagger;
            this.enterSeconds = enterSeconds;
            this.endHoldSeconds = endHoldSeconds;
        }

        public CctvClip Make(int bins, int thieves, int hopCount)
        {
            if (thieves < 1 || bins <= thieves) throw new ArgumentException("There must be more bins than thieves.");

            CctvClip clip = null;
            for (int attempt = 0; attempt < Tries; attempt++)
            {
                clip = TryMake(bins, thieves, Math.Max(0, hopCount));
                if (clip.Hops.Count == 0 || clip.Answer != clip.StartBins[clip.BagThief]) return clip;
            }

            return clip;
        }

        private CctvClip TryMake(int bins, int thieves, int hopCount)
        {
            int[] where = Pick(bins, thieves);
            int[] start = (int[])where.Clone();
            int bag = random.Next(thieves);
            int[] left = new int[thieves];
            for (int i = 0; i < thieves; i++) left[i] = -1;

            bool[] bagHops = BagTurns(hopCount);
            var hops = new CctvHop[hopCount];
            float time = (thieves - 1) * enterStagger + enterSeconds + gapSeconds;
            float seconds = firstHopSeconds;
            float gap = gapSeconds;
            int last = -1;
            for (int i = 0; i < hopCount; i++)
            {
                int thief = bagHops[i] || thieves == 1 ? bag : OtherThief(thieves, bag, last);
                int to = FreeBin(bins, where, left[thief]);
                hops[i] = new CctvHop(thief, where[thief], to, time, seconds);
                left[thief] = where[thief];
                where[thief] = to;
                last = thief;
                time += seconds + gap;
                seconds *= speedUp;
                gap *= speedUp;
            }

            float length = time - gap + endHoldSeconds;
            return new CctvClip(bins, start, bag, hops, enterStagger, enterSeconds, length);
        }

        // Which hops are the bag thief's: half of them, rounded up, in a random order.
        private bool[] BagTurns(int hopCount)
        {
            var turns = new bool[hopCount];
            for (int i = 0; i < (hopCount + 1) / 2; i++) turns[i] = true;
            for (int i = hopCount - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (turns[i], turns[j]) = (turns[j], turns[i]);
            }

            return turns;
        }

        // A thief other than the bag thief, not the one that just hopped when there is a choice.
        private int OtherThief(int thieves, int bag, int last)
        {
            var choices = new List<int>();
            for (int thief = 0; thief < thieves; thief++)
            {
                if (thief != bag && thief != last) choices.Add(thief);
            }

            if (choices.Count == 0)
            {
                for (int thief = 0; thief < thieves; thief++)
                {
                    if (thief != bag) choices.Add(thief);
                }
            }

            return choices[random.Next(choices.Count)];
        }

        // An empty bin, not avoid (the one the thief just left) when another is empty.
        private int FreeBin(int bins, int[] where, int avoid)
        {
            var free = new List<int>();
            for (int bin = 0; bin < bins; bin++)
            {
                if (Array.IndexOf(where, bin) < 0 && bin != avoid) free.Add(bin);
            }

            if (free.Count == 0) free.Add(avoid);
            return free[random.Next(free.Count)];
        }

        // count different bins, in a random order.
        private int[] Pick(int bins, int count)
        {
            var all = new List<int>();
            for (int bin = 0; bin < bins; bin++) all.Add(bin);
            var picked = new int[count];
            for (int i = 0; i < count; i++)
            {
                int at = random.Next(all.Count);
                picked[i] = all[at];
                all.RemoveAt(at);
            }

            return picked;
        }
    }
}
