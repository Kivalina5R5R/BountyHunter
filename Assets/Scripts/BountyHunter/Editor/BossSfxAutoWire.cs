using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BountyHunter.Thieves.EditorTools
{
    // Gives the boss chase its sounds each time the editor loads: adds BossSfx next to the open
    // scene's BossChase when it has none, points it at the chase, and fills every sound left empty
    // (Assets/Audio/Boss from Tools/BossSounds/make_boss_sounds.py, the scuffle, handcuffs and chime
    // shared with the gameplay). A clip swapped in by hand is kept; set a volume to 0 to silence one.
    // Save the scene afterwards.
    [InitializeOnLoad]
    public static class BossSfxAutoWire
    {
        private const string Boss = "Assets/Audio/Boss/";
        private const string Gameplay = "Assets/Audio/Gameplay/";

        private static readonly (string Field, string Clip)[] Sounds =
        {
            ("gust", Boss + "SFX_Boss_Gust.wav"),
            ("appear", Boss + "SFX_Boss_Appear.wav"),
            ("smokeLit", Boss + "SFX_Boss_Smoke_Lit.wav"),
            ("smoke", Boss + "SFX_Boss_Smoke.wav"),
            ("gunPump", Boss + "SFX_Boss_Gun_Pump.wav"),
            ("gunFire", Boss + "SFX_Boss_Gun_Fire.wav"),
            ("rainWarn", Boss + "SFX_Boss_Rain_Warn.wav"),
            ("rainStrike", Boss + "SFX_Boss_Rain_Strike.wav"),
            ("soaked", Boss + "SFX_Boss_Soaked.wav"),
            ("cape", Boss + "SFX_Boss_Cape.wav"),
            ("disguised", Boss + "SFX_Boss_Disguised.wav"),
            ("reveal", Boss + "SFX_Boss_Reveal.wav"),
            ("hit", Boss + "SFX_Boss_Hit.wav"),
            ("coin", Gameplay + "SFX_Coin.wav"),
            ("scuffle", Gameplay + "SFX_Scuffle.wav"),
            ("cuffs", Gameplay + "SFX_Cuffs.wav"),
            ("caught", Boss + "SFX_Boss_Caught.wav"),
            ("escape", Boss + "SFX_Boss_Escape.wav"),
            ("helicopter", Boss + "SFX_Boss_Heli_Loop.wav"),
        };

        static BossSfxAutoWire()
        {
            EditorApplication.delayCall += Wire;
        }

        private static void Wire()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var chase = Object.FindFirstObjectByType<BossChase>(FindObjectsInactive.Include);
            if (chase == null) return;

            bool changed = false;
            if (!chase.TryGetComponent(out BossSfx sfx))
            {
                sfx = chase.gameObject.AddComponent<BossSfx>();
                changed = true;
            }

            var serialized = new SerializedObject(sfx);
            changed |= Fill(serialized.FindProperty("chase"), chase);
            foreach ((string field, string clip) in Sounds)
            {
                changed |= Fill(serialized.FindProperty(field), AssetDatabase.LoadAssetAtPath<AudioClip>(clip));
            }

            if (!changed) return;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(chase.gameObject.scene);
            Debug.Log("[BossSfxAutoWire] The boss chase has its sounds; save the scene to keep them.", sfx);
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
