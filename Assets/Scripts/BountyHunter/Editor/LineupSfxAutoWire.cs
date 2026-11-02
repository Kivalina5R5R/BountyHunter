using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BountyHunter.Thieves.EditorTools
{
    // Gives the suspect lineup (mini game 1) its sounds each time the editor loads: adds LineupSfx next
    // to the open scene's LineupPuzzle when it has none, points it at the puzzle and its wall, and
    // fills every sound left empty (Assets/Audio/Lineup from Tools/LineupSounds/make_lineup_sounds.py,
    // the step and coin from the select and gameplay sounds). A sound still on the gameplay clip it
    // first had (the clock's ticks and the whistle) gets the lineup's own. A clip swapped in by hand
    // is kept; set a volume to 0 to silence one. Save the scene afterwards.
    [InitializeOnLoad]
    public static class LineupSfxAutoWire
    {
        private const string Lineup = "Assets/Audio/Lineup/";
        private const string Gameplay = "Assets/Audio/Gameplay/";

        // Each sound's field, its clip, and the clip it replaces when still set to it (or null).
        private static readonly (string Field, string Clip, string Replaces)[] Sounds =
        {
            ("drop", Lineup + "SFX_Lineup_Drop.wav", null),
            ("clue", Lineup + "SFX_Lineup_Clue.wav", null),
            ("stepOn", "Assets/Audio/Select/SFX_Step_On.wav", null),
            ("locked", Lineup + "SFX_Lineup_Locked.wav", null),
            ("tick", Lineup + "SFX_Lineup_Hurry.wav", Gameplay + "SFX_Tick.wav"),
            ("finalTick", Lineup + "SFX_Lineup_Hurry_Final.wav", Gameplay + "SFX_Tick_Final.wav"),
            ("timeUp", Lineup + "SFX_Lineup_TimeUp.wav", Gameplay + "SFX_Time_Up.wav"),
            ("stamp", Lineup + "SFX_Lineup_Stamp.wav", null),
            ("cheer", Lineup + "SFX_Lineup_Cheer.wav", null),
            ("coin", Gameplay + "SFX_Coin.wav", null),
        };

        static LineupSfxAutoWire()
        {
            EditorApplication.delayCall += Wire;
        }

        private static void Wire()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var puzzle = Object.FindFirstObjectByType<LineupPuzzle>(FindObjectsInactive.Include);
            if (puzzle == null) return;

            bool changed = false;
            if (!puzzle.TryGetComponent(out LineupSfx sfx))
            {
                sfx = puzzle.gameObject.AddComponent<LineupSfx>();
                changed = true;
            }

            var serialized = new SerializedObject(sfx);
            changed |= Fill(serialized.FindProperty("puzzle"), puzzle);
            changed |= Fill(serialized.FindProperty("wall"), new SerializedObject(puzzle).FindProperty("wall").objectReferenceValue);
            foreach ((string field, string clip, string replaces) in Sounds)
            {
                SerializedProperty property = serialized.FindProperty(field);
                var audio = AssetDatabase.LoadAssetAtPath<AudioClip>(clip);
                if (replaces != null && property != null && audio != null
                    && property.objectReferenceValue == AssetDatabase.LoadAssetAtPath<AudioClip>(replaces))
                {
                    property.objectReferenceValue = audio;
                    changed = true;
                    continue;
                }

                changed |= Fill(property, audio);
            }

            if (!changed) return;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(puzzle.gameObject.scene);
            Debug.Log("[LineupSfxAutoWire] The lineup has its sounds; save the scene to keep them.", sfx);
        }

        // Sets an empty reference; true when it did.
        private static bool Fill(SerializedProperty property, Object value)
        {
            if (property == null || value == null || property.objectReferenceValue != null) return false;

            property.objectReferenceValue = value;
            return true;
        }
    }
}
