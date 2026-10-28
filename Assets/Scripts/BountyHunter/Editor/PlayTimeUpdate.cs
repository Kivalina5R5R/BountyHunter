using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BountyHunter.Thieves.EditorTools
{
    // Once per machine, when the editor loads: the open scene's play times move to the ones asked for
    // after playtesting. The boss chase's clock reads 1:30 (4.5 s entrance + 85.5 s chase, it was 62).
    // The security camera mini game lasts 40 seconds without its fades: 3 s of opening words, a clip
    // of 7 hops (about 8.4 s), 18 s to pick a bin, the reveal and cheer, and 4 s of closing words.
    // Only a value still at an old number changes; one set by hand stays. Save the scene afterwards.
    [InitializeOnLoad]
    public static class PlayTimeUpdate
    {
        private const string BossKey = "BountyHunter.PlayTimes.v1";
        private const string CctvKey = "BountyHunter.PlayTimes.Cctv40.v2";

        static PlayTimeUpdate()
        {
            EditorApplication.delayCall += UpdateOnce;
        }

        private static void UpdateOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var boss = Object.FindFirstObjectByType<BossChase>(FindObjectsInactive.Include);
            if (boss != null && !EditorPrefs.GetBool(BossKey, false))
            {
                if (Move(boss, ("chaseSeconds", new[] { 62f }, 85.5f)))
                {
                    EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
                    Debug.Log("[PlayTimeUpdate] The boss chase's clock reads 1:30; save the scene to keep it.");
                }

                EditorPrefs.SetBool(BossKey, true);
            }

            var cctv = Object.FindFirstObjectByType<CctvPuzzle>(FindObjectsInactive.Include);
            if (cctv != null && !EditorPrefs.GetBool(CctvKey, false))
            {
                // 38 and 10 were the first numbers, 60 and 40 a try in between.
                if (Move(cctv, ("totalSeconds", new[] { 38f, 60f }, 40f), ("answerSeconds", new[] { 10f, 40f }, 18f), ("hops", new[] { 5f }, 7f)))
                {
                    EditorSceneManager.MarkSceneDirty(cctv.gameObject.scene);
                    Debug.Log("[PlayTimeUpdate] Mini game 2 lasts 40 s (7 hops, 18 s to pick a bin); save the scene to keep it.");
                }

                EditorPrefs.SetBool(CctvKey, true);
            }
        }

        // Sets each field to its new value if it still has one of its old ones (whole numbers too).
        private static bool Move(Object target, params (string Field, float[] Was, float Now)[] fields)
        {
            var serialized = new SerializedObject(target);
            bool changed = false;
            foreach ((string field, float[] was, float now) in fields)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null) continue;

                bool whole = property.propertyType == SerializedPropertyType.Integer;
                float value = whole ? property.intValue : property.floatValue;
                if (!System.Array.Exists(was, old => Mathf.Approximately(old, value))) continue;

                if (whole) property.intValue = Mathf.RoundToInt(now);
                else property.floatValue = now;
                changed = true;
            }

            if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }
    }
}
