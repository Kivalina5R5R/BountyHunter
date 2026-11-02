using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BountyHunter.Thieves.EditorTools
{
    // Gives the security camera (mini game 2) its sounds each time the editor loads: adds CctvSfx next
    // to the open scene's CctvPuzzle when it has none, points it at the puzzle, its wall and the wall's
    // banana peels, and fills every sound left empty (Assets/Audio/Cctv from
    // Tools/CctvSounds/make_cctv_sounds.py, the rest shared with the lineup). A clip swapped in by hand
    // is kept; set a volume to 0 to silence one. Save the scene afterwards.
    [InitializeOnLoad]
    public static class CctvSfxAutoWire
    {
        private const string Cctv = "Assets/Audio/Cctv/";
        private const string Lineup = "Assets/Audio/Lineup/";

        private static readonly (string Field, string Clip)[] Sounds =
        {
            ("jump", Cctv + "SFX_Cctv_Jump.wav"),
            ("land", Cctv + "SFX_Cctv_Land.wav"),
            ("peelThrow", Cctv + "SFX_Cctv_Peel_Throw.wav"),
            ("peelSplat", Cctv + "SFX_Cctv_Peel_Splat.wav"),
            ("peek", Cctv + "SFX_Cctv_Peek.wav"),
            ("stepOn", "Assets/Audio/Select/SFX_Step_On.wav"),
            ("locked", Lineup + "SFX_Lineup_Locked.wav"),
            ("tick", Lineup + "SFX_Lineup_Hurry.wav"),
            ("finalTick", Lineup + "SFX_Lineup_Hurry_Final.wav"),
            ("timeUp", Lineup + "SFX_Lineup_TimeUp.wav"),
            ("stamp", Lineup + "SFX_Lineup_Stamp.wav"),
            ("cheer", Lineup + "SFX_Lineup_Cheer.wav"),
            ("coin", "Assets/Audio/Gameplay/SFX_Coin.wav"),
        };

        static CctvSfxAutoWire()
        {
            EditorApplication.delayCall += Wire;
        }

        private static void Wire()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var puzzle = Object.FindFirstObjectByType<CctvPuzzle>(FindObjectsInactive.Include);
            if (puzzle == null) return;

            bool changed = false;
            if (!puzzle.TryGetComponent(out CctvSfx sfx))
            {
                sfx = puzzle.gameObject.AddComponent<CctvSfx>();
                changed = true;
            }

            Object wall = new SerializedObject(puzzle).FindProperty("wall").objectReferenceValue;
            Object peels = wall != null ? new SerializedObject(wall).FindProperty("peels").objectReferenceValue : null;
            var serialized = new SerializedObject(sfx);
            changed |= Fill(serialized.FindProperty("puzzle"), puzzle);
            changed |= Fill(serialized.FindProperty("wall"), wall);
            changed |= Fill(serialized.FindProperty("peels"), peels);
            foreach ((string field, string clip) in Sounds)
            {
                changed |= Fill(serialized.FindProperty(field), AssetDatabase.LoadAssetAtPath<AudioClip>(clip));
            }

            if (!changed) return;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(puzzle.gameObject.scene);
            Debug.Log("[CctvSfxAutoWire] The security camera mini game has its sounds; save the scene to keep them.", sfx);
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
