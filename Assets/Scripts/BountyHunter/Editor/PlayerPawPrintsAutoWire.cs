using Fortal.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BountyHunter.Thieves.EditorTools
{
    // Gives the players their running paw prints each time the editor loads: adds PlayerPawPrints
    // next to the open scene's ThiefSpawner when it has none and points it at the player spawner and
    // the intro (no prints while the characters are being picked). Save the scene afterwards.
    [InitializeOnLoad]
    public static class PlayerPawPrintsAutoWire
    {
        static PlayerPawPrintsAutoWire()
        {
            EditorApplication.delayCall += Wire;
        }

        private static void Wire()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var thieves = Object.FindFirstObjectByType<ThiefSpawner>(FindObjectsInactive.Include);
            if (thieves == null) return;

            bool changed = false;
            if (!thieves.TryGetComponent(out PlayerPawPrints prints))
            {
                prints = thieves.gameObject.AddComponent<PlayerPawPrints>();
                changed = true;
            }

            var serialized = new SerializedObject(prints);
            changed |= Fill(serialized.FindProperty("players"), Object.FindFirstObjectByType<UWBPlayerSpawner>(FindObjectsInactive.Include));
            changed |= Fill(serialized.FindProperty("intro"), Object.FindFirstObjectByType<IntroSequence>(FindObjectsInactive.Include));
            if (!changed) return;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(thieves.gameObject.scene);
            Debug.Log("[PlayerPawPrintsAutoWire] The players leave paw prints when they run; save the scene to keep them.", prints);
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
