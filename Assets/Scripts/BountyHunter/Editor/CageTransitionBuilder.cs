using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves.EditorTools
{
    // Builds Assets/Prefabs/Transition/Cage Transition.prefab from the cage art in
    // Assets/ART/Transition when it is missing, each time the editor loads, puts it over the wall just
    // behind the fade curtain and hands it to IntroSequence, so the game starts behind the cage. Save
    // the scene afterwards.
    [InitializeOnLoad]
    public static class CageTransitionAutoBuild
    {
        static CageTransitionAutoBuild()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        private static void BuildIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Object.FindFirstObjectByType<IntroSequence>(FindObjectsInactive.Include) == null) return;
            if (!CageTransitionBuilder.ArtIsThere()) return;

            CageTransitionBuilder.UseFullSizeDoor();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CageTransitionBuilder.PrefabPath) == null)
            {
                CageTransitionBuilder.Build();
                return;
            }

            CageTransitionBuilder.UseNewDoor();
            if (Object.FindFirstObjectByType<CageTransition>(FindObjectsInactive.Include) == null) CageTransitionBuilder.Wire();
        }
    }

    // Tools > Bounty Hunter > Build Cage Transition: makes the cage prefab (see CageTransition) in the
    // wall canvas's units, the right door the left one mirrored, and wires the open scene to it. Run it
    // again to start the prefab over (changes made in it by hand are lost); save the scene afterwards.
    public static class CageTransitionBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Transition/Cage Transition.prefab";
        // One half of the cell door, its lock plate on its right edge; the right door is it mirrored.
        private const string Door = BossTransitionArtImport.Folder + "Open Map 1/cage_police.png";
        // The cell behind the bars: a deep navy, near black.
        private static readonly Color CellDark = new Color(0.03f, 0.04f, 0.09f, 1f);

        public static bool ArtIsThere()
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(Door) != null;
        }

        [MenuItem("Tools/Bounty Hunter/Build Cage Transition")]
        public static void Build()
        {
            if (!ArtIsThere())
            {
                Debug.LogError($"[CageTransitionBuilder] Needs {Door} (as a sprite)");
                return;
            }

            BuildPrefab();
            Wire();
            Debug.Log("[CageTransitionBuilder] Cage Transition built and wired; save the scene to keep it.");
        }

        // Puts the prefab over the wall (once) and hands it to IntroSequence.
        public static void Wire()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<CageTransition>(PrefabPath);
            var intro = Object.FindFirstObjectByType<IntroSequence>(FindObjectsInactive.Include);
            if (prefab == null || intro == null) return;

            CageTransition placed = null;
            foreach (CageTransition existing in Object.FindObjectsByType<CageTransition>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing) == prefab) placed = existing;
            }

            if (placed == null)
            {
                Transform side = Find("SideDisplay");
                placed = (CageTransition)PrefabUtility.InstantiatePrefab(prefab, side);
                Undo.RegisterCreatedObjectUndo(placed.gameObject, "Wire Cage Transition");
                Transform curtain = Find("Fade Curtain");
                if (curtain != null && curtain.parent == side) placed.transform.SetSiblingIndex(curtain.GetSiblingIndex());
                placed.gameObject.SetActive(false);
            }

            var serialized = new SerializedObject(intro);
            serialized.FindProperty("cage").objectReferenceValue = placed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(intro.gameObject.scene);
        }

        // The door art is drawn wide (4401 px); it is imported at full size so it stays sharp on the
        // wall (half of 8192 px).
        public static void UseFullSizeDoor()
        {
            if (!(AssetImporter.GetAtPath(Door) is TextureImporter importer) || importer.maxTextureSize >= 8192) return;

            importer.maxTextureSize = 8192;
            importer.SaveAndReimport();
        }

        // A cage prefab made with the first door and its padlock gets the new door on both sides and
        // loses the padlock, keeping everything else in it.
        public static void UseNewDoor()
        {
            Sprite door = AssetDatabase.LoadAssetAtPath<Sprite>(Door);
            if (door == null) return;

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            bool changed = false;
            foreach (string name in new[] { "Shaker/Left Door", "Shaker/Right Door" })
            {
                Image image = contents.transform.Find(name)?.GetComponent<Image>();
                if (image == null || image.sprite == door) continue;

                image.sprite = door;
                changed = true;
            }

            Transform padlock = contents.transform.Find("Shaker/Padlock");
            if (padlock != null)
            {
                Object.DestroyImmediate(padlock.gameObject);
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log("[CageTransitionBuilder] Cage Transition uses the new cell door (cage_police) and no padlock.");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        // Back to front: the dark cell and the two doors meeting in the middle.
        private static void BuildPrefab()
        {
            Vector2 wall = LineupPrefabBuilder.WallSize;
            var root = new GameObject("Cage Transition", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            Place(rect, Vector2.zero, wall);
            var cage = root.AddComponent<CageTransition>();

            Image shade = UiImage("Cell", rect, null, CellDark);
            Stretch(shade.rectTransform);
            RectTransform shaker = UiRect("Shaker", rect);
            Stretch(shaker);

            Sprite door = Load<Sprite>(Door);
            // Each door is half the wall, a little more so they overlap at the join.
            var doorSize = new Vector2(wall.x * 0.5f + 20f, wall.y);
            Image left = UiImage("Left Door", shaker, door, Color.white);
            Place(left.rectTransform, new Vector2(-wall.x * 0.25f + 10f, 0f), doorSize);
            Image right = UiImage("Right Door", shaker, door, Color.white);
            Place(right.rectTransform, new Vector2(wall.x * 0.25f - 10f, 0f), doorSize);
            right.rectTransform.localScale = new Vector3(-1f, 1f, 1f);


            var serialized = new SerializedObject(cage);
            serialized.FindProperty("shaker").objectReferenceValue = shaker;
            serialized.FindProperty("shade").objectReferenceValue = shade;
            serialized.FindProperty("leftDoor").objectReferenceValue = left.rectTransform;
            serialized.FindProperty("rightDoor").objectReferenceValue = right.rectTransform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        private static Transform Find(string name)
        {
            foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.name == name) return candidate;
            }

            return null;
        }

        private static RectTransform UiRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image UiImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var image = UiRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogWarning($"[CageTransitionBuilder] Missing {typeof(T).Name} at {path}");
            return asset;
        }
    }
}
