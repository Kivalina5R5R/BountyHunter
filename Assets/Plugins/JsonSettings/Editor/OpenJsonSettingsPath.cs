using UnityEditor;
using UnityEngine;

namespace JsonSettingsUtilities.Editor
{
    public static class OpenJsonSettingsPath
    {
        [MenuItem("Tools/Open Json Settings Path")]
        static void Open()
        {
            string gameRoot = System.IO.Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string path = System.IO.Path.Combine(gameRoot, "Settings");
            if (System.IO.Directory.Exists(path))
                EditorUtility.OpenWithDefaultApp(path);
        }
    }
}