using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves.EditorTools
{
    // Gives the score tag (Assets/Prefabs/UI/PlayerTag.prefab) a spot for the character's face before
    // the name when it has none, each time the editor loads, and hands each character its face (Assets/
    // ART/Player/Faces/Face <character>.png, cut from the floor markers) in the open scene's
    // CharacterScoreTags. Once built, the face is left alone: move or resize it in the prefab. Save
    // the scene afterwards.
    [InitializeOnLoad]
    public static class ScoreTagFaceBuilder
    {
        private const string TagPath = "Assets/Prefabs/UI/PlayerTag.prefab";
        private const string FacesFolder = "Assets/ART/Player/Faces/";
        private const string FaceName = "Face";

        // In the tag's own units: on the left of the black name tab, a little over its edges.
        private static readonly Vector2 FaceSpot = new Vector2(-300f, 103f);
        private static readonly Vector2 FaceSize = new Vector2(130f, 130f);

        static ScoreTagFaceBuilder()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        private static void BuildIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!AssetDatabase.IsValidFolder(FacesFolder.TrimEnd('/'))) return;

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { FacesFolder.TrimEnd('/') }))
            {
                ImportAsSprite(AssetDatabase.GUIDToAssetPath(guid));
            }

            AddToTag();
            Wire();
        }

        private static void AddToTag()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TagPath) == null) return;

            GameObject contents = PrefabUtility.LoadPrefabContents(TagPath);
            try
            {
                if (contents.transform.Find(FaceName) != null) return;

                var rect = new GameObject(FaceName, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(contents.transform, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = FaceSpot;
                rect.sizeDelta = FaceSize;
                var image = rect.gameObject.AddComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                rect.gameObject.SetActive(false);

                var tag = contents.GetComponent<PlayerScoreTag>();
                var fields = new SerializedObject(tag);
                fields.FindProperty("face").objectReferenceValue = image;
                fields.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, TagPath);
                Debug.Log("[ScoreTagFaceBuilder] The score tag has a spot for the character's face now.", AssetDatabase.LoadAssetAtPath<GameObject>(TagPath));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // Each character with no face yet gets its own, found by name.
        private static void Wire()
        {
            var tags = Object.FindFirstObjectByType<CharacterScoreTags>(FindObjectsInactive.Include);
            if (tags == null) return;

            var fields = new SerializedObject(tags);
            SerializedProperty entries = fields.FindProperty("entries");
            bool changed = false;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                SerializedProperty face = entry.FindPropertyRelative("face");
                if (face == null || face.objectReferenceValue != null) continue;

                string character = entry.FindPropertyRelative("character").stringValue;
                var picture = AssetDatabase.LoadAssetAtPath<Sprite>($"{FacesFolder}Face {character}.png");
                if (picture == null) continue;

                face.objectReferenceValue = picture;
                changed = true;
            }

            if (!changed) return;

            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(tags.gameObject.scene);
            Debug.Log("[ScoreTagFaceBuilder] Each character's score tag has its face; save the scene to keep it", tags);
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
