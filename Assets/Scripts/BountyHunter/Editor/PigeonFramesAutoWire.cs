using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BountyHunter.Thieves.EditorTools
{
    // Gives the boss cut-in's pigeons their flight each time the editor loads: imports the frames cut
    // from Pigeon_Fly_Loop.mp4 (Assets/ART/Transition/Boss Open/PigeonFly, made by Tools/PigeonArt)
    // as sprites and fills the Boss Transition prefab's Pigeon Frames with them, in order, when it is
    // empty. Anything set by hand is kept.
    [InitializeOnLoad]
    public static class PigeonFramesAutoWire
    {
        private const string Folder = "Assets/ART/Transition/Boss Open/PigeonFly";

        static PigeonFramesAutoWire()
        {
            EditorApplication.delayCall += Wire;
        }

        private static void Wire()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !AssetDatabase.IsValidFolder(Folder)) return;

            string[] paths = AssetDatabase.FindAssets("PigeonFly_ t:Texture2D", new[] { Folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .ToArray();
            if (paths.Length == 0) return;

            foreach (string path in paths) ImportAsSprite(path);

            var prefab = AssetDatabase.LoadAssetAtPath<BossTransition>(BossTransitionBuilder.PrefabPath);
            if (prefab == null) return;

            var fields = new SerializedObject(prefab);
            SerializedProperty frames = fields.FindProperty("pigeonFrames");
            if (frames == null || frames.arraySize > 0) return;

            frames.arraySize = paths.Length;
            for (int i = 0; i < paths.Length; i++) frames.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PigeonFramesAutoWire] The boss cut-in's pigeons fly with {paths.Length} frames of Pigeon_Fly_Loop.", prefab);
        }

        private static void ImportAsSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer || importer.textureType == TextureImporterType.Sprite) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }
}
