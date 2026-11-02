using UnityEditor;
using UnityEngine;

namespace BountyHunter.Thieves.EditorTools
{
    // Gives the wall transitions' prefabs (the cage, the police car, the boss's cut-in) their sounds
    // from Assets/Audio/Transition (Tools/TransitionSounds/make_transition_sounds.py) each time the
    // editor loads. Only empty sounds are filled: a clip swapped in by hand is kept, but an emptied
    // one comes back, so set its volume to 0 to silence it.
    [InitializeOnLoad]
    public static class TransitionSoundsAutoWire
    {
        private const string Audio = "Assets/Audio/Transition/";

        private static readonly (string Prefab, System.Type Transition, (string Field, string Clip)[] Sounds)[] Prefabs =
        {
            (CageTransitionBuilder.PrefabPath, typeof(CageTransition), new[]
            {
                ("slideSound", "SFX_Cage_Slide"),
                ("slamSound", "SFX_Cage_Slam"),
                ("openSound", "SFX_Cage_Open"),
            }),
            (PoliceCarTransitionBuilder.PrefabPath, typeof(PoliceCarTransition), new[]
            {
                ("passSound", "SFX_Police_Pass"),
            }),
            (BossTransitionBuilder.PrefabPath, typeof(BossTransition), new[]
            {
                ("feathersSound", "SFX_Boss_Feathers"),
                ("strikeSound", "SFX_Boss_Strike"),
                ("bossSound", "SFX_Boss_Reveal"),
                ("pigeonsSound", "SFX_Boss_Pigeons"),
                ("outSound", "SFX_Boss_Out"),
            }),
        };

        static TransitionSoundsAutoWire()
        {
            EditorApplication.delayCall += FillAll;
        }

        private static void FillAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            foreach ((string prefab, System.Type transition, (string Field, string Clip)[] sounds) in Prefabs)
            {
                Fill(prefab, transition, sounds);
            }
        }

        private static void Fill(string prefab, System.Type transition, (string Field, string Clip)[] sounds)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefab) == null) return;

            GameObject contents = PrefabUtility.LoadPrefabContents(prefab);
            Component owner = contents.GetComponent(transition);
            bool changed = false;
            if (owner != null)
            {
                var serialized = new SerializedObject(owner);
                foreach ((string field, string clip) in sounds)
                {
                    SerializedProperty property = serialized.FindProperty(field + ".clip");
                    if (property == null || property.objectReferenceValue != null) continue;

                    var audio = AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + clip + ".wav");
                    if (audio == null) continue;

                    property.objectReferenceValue = audio;
                    changed = true;
                }

                if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(contents, prefab);
                Debug.Log($"[TransitionSoundsAutoWire] {prefab} has its sounds.");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
