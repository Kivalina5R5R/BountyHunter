$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$folder = Join-Path $projectRoot 'Assets/Scripts/BountyHunter/Lineup'
# Both files start with their own using lines; C# wants them all ahead of the code, so they go first.
$lines = @('LineupCase.cs', 'LineupCaseGenerator.cs') | ForEach-Object { Get-Content (Join-Path $folder $_) }
$usings = $lines | Where-Object { $_ -match '^using ' } | Sort-Object -Unique
$source = ($usings -join "`n") + "`n" + (($lines | Where-Object { $_ -notmatch '^using ' }) -join "`n")

# LineupCase.cs and LineupCaseGenerator.cs use only System, so the production files compile as they
# are. Checks 2,000 lineups: no two suspects carry the very same things, the clues mix two ticks
# and two crosses, and they narrow the lineup step by step (4-5, 3, 2, then the culprit alone).
$check = @'
namespace BountyHunter.Thieves {
using System;
using System.Collections.Generic;
public static class LineupCaseCheck {
    public static void Run() {
        var species = new List<LineupSpecies>();
        foreach (string name in new[] { "Cat", "Chicken", "Crow", "Rat" }) species.Add(new LineupSpecies(name, true));
        foreach (string name in new[] { "Capybara", "Goat", "Otter", "Pig", "Raccoon" }) species.Add(new LineupSpecies(name, false));
        var maker = new LineupCaseGenerator(species, new Random(7));
        for (int run = 0; run < 2000; run++) {
            LineupCase lineup = maker.Make();
            var seen = new HashSet<string>();
            foreach (LineupSuspect s in lineup.Suspects) {
                string key = s.Hat + "|" + s.Mask + s.Glasses + s.FaceMask + s.Mustache + s.Newspaper + s.Gloves + s.Bag;
                if (!seen.Add(key)) throw new Exception("Two suspects carry the same things in lineup " + run);
            }
            if (lineup.RemainingAfter(4) != 1 || !lineup.FitsFirst(lineup.Culprit, 4)) throw new Exception("The clues do not single out the culprit in lineup " + run);
            int first = lineup.RemainingAfter(1), second = lineup.RemainingAfter(2), third = lineup.RemainingAfter(3);
            if (first < 4 || first > 5 || second != 3 || third != 2) throw new Exception("Pace broken in lineup " + run + ": " + first + "/" + second + "/" + third);
            int ticks = 0;
            foreach (LineupClue clue in lineup.Clues) if (clue.Has) ticks++;
            if (ticks != 2) throw new Exception("Clues not mixed in lineup " + run + ": " + ticks + " ticks");
        }
        Console.WriteLine("PASS: 2000 lineups, every suspect carries a different set, 2 ticks + 2 crosses, 4-5 / 3 / 2 / 1 left, only the last clue settles it");
    }
}
}
'@
Add-Type -TypeDefinition ($source + "`n" + $check)
[BountyHunter.Thieves.LineupCaseCheck]::Run()
