using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BountyHunter.Editor
{
    // Keeps the empty objects used to group the wall (such as Map/SideDisplay/Play and Intro) the
    // full size of the wall canvas. A new empty RectTransform is only 100 x 100, and anything inside
    // that sizes itself from its parent (the wall Background's AspectRatioFitter set to Envelope
    // Parent) shrinks to fit it. Only plain groups are touched: direct children of the wall canvas
    // with nothing but a RectTransform. Runs after scripts load, when leaving Play and from the menu;
    // it changes the open scene without saving it.
    [InitializeOnLoad]
    public static class SideDisplayGroups
    {
        private const string WallPath = "Map/SideDisplay";

        static SideDisplayGroups()
        {
            EditorApplication.delayCall += Stretch;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Stretch;
            };
        }

        [MenuItem("Tools/Bounty Hunter/Stretch Wall Groups")]
        public static void Stretch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            GameObject wall = GameObject.Find(WallPath);
            if (wall == null || wall.GetComponent<Canvas>() == null) return;

            foreach (Transform child in wall.transform)
            {
                if (!(child is RectTransform group) || child.GetComponents<Component>().Length != 1 || Stretched(group)) continue;

                // Whatever is inside stays exactly where it is on the wall.
                var positions = new Vector3[group.childCount];
                for (int i = 0; i < group.childCount; i++)
                {
                    positions[i] = group.GetChild(i).position;
                    Undo.RecordObject(group.GetChild(i), "Stretch Wall Group");
                }

                Undo.RecordObject(group, "Stretch Wall Group");
                group.anchorMin = Vector2.zero;
                group.anchorMax = Vector2.one;
                group.pivot = new Vector2(0.5f, 0.5f);
                group.offsetMin = Vector2.zero;
                group.offsetMax = Vector2.zero;
                group.localScale = Vector3.one;
                group.localRotation = Quaternion.identity;
                group.localPosition = new Vector3(group.localPosition.x, group.localPosition.y, 0f);
                for (int i = 0; i < group.childCount; i++)
                {
                    group.GetChild(i).position = positions[i];
                }

                EditorSceneManager.MarkSceneDirty(group.gameObject.scene);
                Debug.Log($"[SideDisplayGroups] {WallPath}/{group.name} now fills the wall, so what sizes itself from it (the Background) does too. Save the scene to keep it.", group);
            }
        }

        private static bool Stretched(RectTransform group)
        {
            return group.anchorMin == Vector2.zero && group.anchorMax == Vector2.one &&
                group.offsetMin == Vector2.zero && group.offsetMax == Vector2.zero &&
                group.localScale == Vector3.one && Mathf.Approximately(group.localPosition.z, 0f);
        }
    }
}
