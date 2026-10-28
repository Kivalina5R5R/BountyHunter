using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves.EditorTools
{
    // Builds Assets/Prefabs/Transition/Police Car Transition.prefab from the car in
    // Assets/ART/Transition/End Minigame1 when it is missing, each time the editor loads, puts it over
    // the wall just behind the fade curtain and makes it the suspect lineup's way out (mini game 1).
    // Save the scene afterwards.
    [InitializeOnLoad]
    public static class PoliceCarTransitionAutoBuild
    {
        static PoliceCarTransitionAutoBuild()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        private static void BuildIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Object.FindFirstObjectByType<LineupPuzzle>(FindObjectsInactive.Include) == null) return;
            if (AssetDatabase.LoadAssetAtPath<Sprite>(PoliceCarTransitionBuilder.CarPath) == null) return;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PoliceCarTransitionBuilder.PrefabPath) == null) PoliceCarTransitionBuilder.Build();
            else
            {
                PoliceCarTransitionBuilder.RelinkCover();
                PoliceCarTransitionBuilder.SlowCarOnce();
                if (Object.FindFirstObjectByType<PoliceCarTransition>(FindObjectsInactive.Include) == null) PoliceCarTransitionBuilder.Wire();
            }
        }
    }

    // Tools > Bounty Hunter > Build Police Car Transition: makes the prefab (see PoliceCarTransition)
    // in the wall canvas's units and wires the open scene to it. Run it again to start the prefab over
    // (changes made in it by hand are lost); save the scene afterwards.
    public static class PoliceCarTransitionBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Transition/Police Car Transition.prefab";
        public const string CarPath = BossTransitionArtImport.Folder + "End Minigame1/PoliceCar_Side.png";
        private const string Glow = "Assets/ART/Minigame/Lineup/Spotlight.png";
        private static readonly Color CoverDark = new Color(0.03f, 0.04f, 0.09f, 1f);
        // The car on the wall: how tall, and how far below the middle it drives.
        private const float CarHeight = 220f;
        private const float CarY = -70f;
        // Where the light bar's red and blue halves sit on the car picture, as shares of its size from
        // its middle (x to the right, y up), and how big each glow is next to the car's height.
        private static readonly Vector2 RedSpot = new Vector2(-0.107f, 0.39f);
        private static readonly Vector2 BlueSpot = new Vector2(-0.012f, 0.39f);
        private const float GlowShare = 0.55f;

        [MenuItem("Tools/Bounty Hunter/Build Police Car Transition")]
        public static void Build()
        {
            Sprite carArt = AssetDatabase.LoadAssetAtPath<Sprite>(CarPath);
            if (carArt == null)
            {
                Debug.LogError($"[PoliceCarTransitionBuilder] Needs {CarPath} (as a sprite)");
                return;
            }

            BuildPrefab(carArt);
            Wire();
            Debug.Log("[PoliceCarTransitionBuilder] Police Car Transition built and set as mini game 1's way out; save the scene to keep it.");
        }

        // Puts the prefab over the wall (once) and makes it the lineup's way out, unless it has one.
        public static void Wire()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<PoliceCarTransition>(PrefabPath);
            var lineup = Object.FindFirstObjectByType<LineupPuzzle>(FindObjectsInactive.Include);
            if (prefab == null || lineup == null) return;

            PoliceCarTransition placed = null;
            foreach (PoliceCarTransition existing in Object.FindObjectsByType<PoliceCarTransition>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing) == prefab) placed = existing;
            }

            if (placed == null)
            {
                Transform side = Find("SideDisplay");
                placed = (PoliceCarTransition)PrefabUtility.InstantiatePrefab(prefab, side);
                Undo.RegisterCreatedObjectUndo(placed.gameObject, "Wire Police Car Transition");
                Transform curtain = Find("Fade Curtain");
                if (curtain != null && curtain.parent == side) placed.transform.SetSiblingIndex(curtain.GetSiblingIndex());
                placed.gameObject.SetActive(false);
            }

            var serialized = new SerializedObject(lineup);
            SerializedProperty way = serialized.FindProperty("exitTransition");
            if (way.objectReferenceValue == null) way.objectReferenceValue = placed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(lineup.gameObject.scene);
        }

        // The cover used to be linked as a RectTransform; the transition now wants its Image (it fades).
        // A prefab still linking the RectTransform (shown as a type mismatch) gets the Image, keeping
        // everything else in it.
        public static void RelinkCover()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            var serialized = new SerializedObject(contents.GetComponent<PoliceCarTransition>());
            SerializedProperty cover = serialized.FindProperty("cover");
            Image found = contents.transform.Find("Cover")?.GetComponent<Image>();
            if (!(cover.objectReferenceValue is Image) && found != null)
            {
                cover.objectReferenceValue = found;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log("[PoliceCarTransitionBuilder] Police Car Transition: the wall fades dark, then the car crosses once to show the new map.");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        // A prefab with the first drive time (1.1 s) crosses a second slower; a time set by hand stays.
        public static void SlowCarOnce()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            var serialized = new SerializedObject(contents.GetComponent<PoliceCarTransition>());
            SerializedProperty drive = serialized.FindProperty("driveSeconds");
            if (drive != null && Mathf.Approximately(drive.floatValue, 1.1f))
            {
                drive.floatValue = 2.1f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log("[PoliceCarTransitionBuilder] The police car now takes 2.1 s to cross the wall.");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        // Back to front: the cover, the car, the siren's two glows on the car's light bar.
        private static void BuildPrefab(Sprite carArt)
        {
            Vector2 wall = LineupPrefabBuilder.WallSize;
            var root = new GameObject("Police Car Transition", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            Place(rect, Vector2.zero, wall);
            var transition = root.AddComponent<PoliceCarTransition>();

            Image cover = UiImage("Cover", rect, null, CoverDark);
            Place(cover.rectTransform, Vector2.zero, wall);

            var carSize = new Vector2(CarHeight * carArt.rect.width / carArt.rect.height, CarHeight);
            Image car = UiImage("Car", rect, carArt, Color.white);
            car.preserveAspect = true;
            Place(car.rectTransform, new Vector2(0f, CarY), carSize);

            Sprite glow = AssetDatabase.LoadAssetAtPath<Sprite>(Glow);
            Image red = UiImage("Red Light", car.rectTransform, glow, new Color(1f, 0.15f, 0.2f, 0.85f));
            Place(red.rectTransform, Vector2.Scale(RedSpot, carSize), Vector2.one * CarHeight * GlowShare);
            Image blue = UiImage("Blue Light", car.rectTransform, glow, new Color(0.2f, 0.45f, 1f, 0.85f));
            Place(blue.rectTransform, Vector2.Scale(BlueSpot, carSize), Vector2.one * CarHeight * GlowShare);

            var serialized = new SerializedObject(transition);
            serialized.FindProperty("cover").objectReferenceValue = cover;
            serialized.FindProperty("car").objectReferenceValue = car.rectTransform;
            serialized.FindProperty("redLight").objectReferenceValue = red;
            serialized.FindProperty("blueLight").objectReferenceValue = blue;
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

        private static Image UiImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var image = rect.gameObject.AddComponent<Image>();
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
    }
}
