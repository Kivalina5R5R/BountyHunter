$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$clipSource = Get-Content (Join-Path $projectRoot 'Assets/Scripts/BountyHunter/Cctv/CctvClip.cs') -Raw

# CctvClip.cs uses only System, so the production file compiles as it is. Checks 2,000 clips per hop
# count against the rules the mini game promises: no thief lands in a taken bin, half the hops
# (rounded up) are the bag thief's, the bag never ends where it started, and the clip stays 6-12 s.
$check = @'
namespace BountyHunter.Thieves {
using System;
using System.Collections.Generic;
public static class CctvClipCheck {
    public static void Run() {
        foreach (int hopCount in new[] { 3, 5, 9 }) {
            var maker = new CctvClipMaker(new Random(hopCount), 1.1f, 0.85f, 0.25f, 0.35f, 0.8f, 0.6f);
            float shortest = float.MaxValue, longest = 0f;
            for (int run = 0; run < 2000; run++) {
                CctvClip clip = maker.Make(4, 3, hopCount);
                var where = new List<int>(clip.StartBins);
                if (new HashSet<int>(where).Count != 3) throw new Exception("Two thieves start in one bin");
                int bagHops = 0;
                float lastEnd = 0f;
                foreach (CctvHop hop in clip.Hops) {
                    if (where[hop.Thief] != hop.From) throw new Exception("Hop starts from the wrong bin");
                    if (where.Contains(hop.To)) throw new Exception("Thief landed in a taken bin");
                    if (hop.Start < lastEnd) throw new Exception("Hops overlap");
                    lastEnd = hop.Start + hop.Seconds;
                    where[hop.Thief] = hop.To;
                    if (hop.Thief == clip.BagThief) bagHops++;
                }
                if (bagHops != (hopCount + 1) / 2) throw new Exception("Bag thief hops " + bagHops + " of " + hopCount);
                if (clip.Answer != where[clip.BagThief]) throw new Exception("Answer is not the bag's last bin");
                if (clip.Answer == clip.StartBins[clip.BagThief]) throw new Exception("Bag ended where it started");
                if (lastEnd > clip.Length) throw new Exception("Clip ends before its last hop");
                shortest = Math.Min(shortest, clip.Length);
                longest = Math.Max(longest, clip.Length);
            }
            if (shortest < 5f || longest > 12f) throw new Exception("Clip length out of range: " + shortest + " to " + longest);
            Console.WriteLine("PASS: " + hopCount + " hops, 2000 clips, no taken bins, half the hops are the bag's, bag moves, clip " + shortest.ToString("0.0") + " s");
        }
    }
}
}
'@
Add-Type -TypeDefinition ($clipSource + "`n" + $check)
[BountyHunter.Thieves.CctvClipCheck]::Run()
