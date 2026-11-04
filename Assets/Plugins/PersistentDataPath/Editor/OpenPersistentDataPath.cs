using UnityEditor;
using UnityEngine;

namespace PersistantDataPathUtilities.Editor
{
    public static class OpenPersistentDataPath
    {
        [MenuItem("Tools/Open Persistent Data Path")]
        static void Open()
        {
            string path = Application.persistentDataPath;
            if (System.IO.Directory.Exists(path))
                EditorUtility.OpenWithDefaultApp(path);
        }
    }
}
