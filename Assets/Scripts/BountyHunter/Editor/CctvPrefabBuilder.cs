using BountyHunter.Thieves;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves.EditorTools
{
    // Builds the security camera mini game's prefabs when they are missing and wires the open scene
    // to it when it is not yet, each time the editor loads. Whatever it changes in the scene then
    // needs saving.
    [InitializeOnLoad]
    public static class CctvPrefabAutoBuild
    {
        static CctvPrefabAutoBuild()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        private static void BuildIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Object.FindFirstObjectByType<MapSwitcher>(FindObjectsInactive.Include) == null) return;

            bool prefabsMissing = AssetDatabase.LoadAssetAtPath<GameObject>(CctvPrefabBuilder.BinPath) == null
                                  || AssetDatabase.LoadAssetAtPath<GameObject>(CctvPrefabBuilder.FloorPath) == null
                                  || AssetDatabase.LoadAssetAtPath<GameObject>(CctvPrefabBuilder.WallPath) == null;
            if (prefabsMissing)
            {
                Debug.Log("[CctvPrefabBuilder] The CCTV prefabs are missing; building them now.");
                CctvPrefabBuilder.Build();
                return;
            }

            CctvPrefabBuilder.DropFloorBand();
            if (Object.FindFirstObjectByType<CctvPuzzle>(FindObjectsInactive.Include) == null) CctvPrefabBuilder.Wire();
        }
    }

    // Tools > Bounty Hunter > Build CCTV Prefabs: makes the security camera mini game's prefabs in
    // Assets/Prefabs/Cctv: Cctv Bin (one bin on the floor), Cctv Floor (four Cctv Bins in a row, each
    // with its own picture) and Cctv Wall (the camera on the wall, its four bins the
    // same pictures in the same order, all as tall as each other), and wires the open scene to them: a Cctv Puzzle object next to the Lineup Puzzle
    // (taking its players, score tags and money font), the wall panel over the game wall just behind
    // the fade curtain, and the mini game hooked up after map 2 if nothing is there yet. Run it again
    // to start the prefabs over (it overwrites them, so changes made in them by hand are lost); save
    // the scene afterwards.
    public static class CctvPrefabBuilder
    {
        private const string Folder = "Assets/Prefabs/Cctv";
        public const string BinPath = Folder + "/Cctv Bin.prefab";
        public const string FloorPath = Folder + "/Cctv Floor.prefab";
        public const string WallPath = Folder + "/Cctv Wall.prefab";
        private const string LineupArt = "Assets/ART/Minigame/Lineup/";
        private const string BinArt = "Assets/ART/BG/Map1/";

        private const float RingRadius = 0.7f;
        private const int Bins = 4;
        private const float FloorSpacing = 2f;
        private const int Thieves = 3;
        // Where the bins stand on the camera screen and how far apart (screen units): their bottoms on
        // BinBottom, each WallBinHeight tall counting only what is drawn of its picture.
        private const float BinY = -70f;
        private const float BinSpacing = 230f;
        private const float BinBottom = -130f;
        private const float WallBinHeight = 120f;
        private static readonly Vector2 ScreenSize = new Vector2(1090f, 300f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 1f);
        // The camera shows the bins and thieves in their own colours on a dark screen, its words in soft white.
        private static readonly Color ScreenColor = new Color(0.1f, 0.11f, 0.13f, 1f);
        private static readonly Color CameraText = new Color(0.9f, 0.9f, 0.9f, 1f);

        private static readonly string[] ThiefBodies =
        {
            "Assets/ART/Thif/Cat/Cat.png",
            "Assets/ART/Thif/Chicken/Chicken.png",
            "Assets/ART/Thif/Crow/Crow.png",
            "Assets/ART/Thif/Rat/Rat.png",
        };

        // Bin i shows BinPictures[i % length], on the floor and on the camera. The upright bins only:
        // the long dumpster (Been4) does not sit well beside them.
        private static readonly string[] BinPictures = { "Been1", "Been2", "Been3" };

        [MenuItem("Tools/Bounty Hunter/Build CCTV Prefabs")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Cctv");

            BuildBin();
            BuildFloor();
            BuildWall();
            Wire();
            Debug.Log("[CctvPrefabBuilder] CCTV prefabs built and the scene wired; save the scene to keep it.");
        }

        // Wires the open scene to the prefabs, keeping whatever is wired already.
        [MenuItem("Tools/Bounty Hunter/Wire CCTV Mini Game")]
        public static void Wire()
        {
            var floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloorPath);
            var wallPrefab = AssetDatabase.LoadAssetAtPath<CctvWall>(WallPath);
            var lineup = Object.FindFirstObjectByType<LineupPuzzle>(FindObjectsInactive.Include);
            if (floorPrefab == null || wallPrefab == null || lineup == null)
            {
                Debug.LogError("[CctvPrefabBuilder] Needs the CCTV prefabs and the Lineup Puzzle in the open scene. Open Assets/Scenes/BountyHunter.unity and run Tools > Bounty Hunter > Build CCTV Prefabs.");
                return;
            }

            var puzzle = Object.FindFirstObjectByType<CctvPuzzle>(FindObjectsInactive.Include);
            if (puzzle == null)
            {
                var holder = new GameObject("Cctv Puzzle");
                Undo.RegisterCreatedObjectUndo(holder, "Wire CCTV Mini Game");
                holder.transform.SetParent(lineup.transform.parent, false);
                holder.transform.SetSiblingIndex(lineup.transform.GetSiblingIndex() + 1);
                puzzle = holder.AddComponent<CctvPuzzle>();
                holder.AddComponent<CctvFloor>();
                CopyGamePieces(lineup, puzzle);
            }

            var floor = puzzle.GetComponent<CctvFloor>();
            if (floor == null) floor = puzzle.gameObject.AddComponent<CctvFloor>();
            Set(floor, "layoutPrefab", floorPrefab);
            SetIfEmpty(floor, "pickMark", Load<Sprite>(LineupArt + "Badge_Yes.png"));
            Set(puzzle, "floor", floor);
            Set(puzzle, "wall", PlaceWall(wallPrefab));
            SetThievesIfEmpty(puzzle);
            HookUpAfterSecondMap(puzzle);
            EditorSceneManager.MarkSceneDirty(puzzle.gameObject.scene);
            Debug.Log("[CctvPrefabBuilder] The CCTV mini game is wired after map 2; save the scene to keep it.");
        }

        // The players, the score tags and the money's font, the same as the lineup's.
        private static void CopyGamePieces(LineupPuzzle lineup, CctvPuzzle puzzle)
        {
            var from = new SerializedObject(lineup);
            var to = new SerializedObject(puzzle);
            foreach (string field in new[] { "playerSpawner", "moneyFont", "moneyMaterial" })
            {
                to.FindProperty(field).objectReferenceValue = from.FindProperty(field).objectReferenceValue;
            }

            SerializedProperty fromTags = from.FindProperty("scoreTags");
            SerializedProperty toTags = to.FindProperty("scoreTags");
            toTags.arraySize = fromTags.arraySize;
            for (int i = 0; i < fromTags.arraySize; i++)
            {
                toTags.GetArrayElementAtIndex(i).objectReferenceValue = fromTags.GetArrayElementAtIndex(i).objectReferenceValue;
            }

            to.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetThievesIfEmpty(CctvPuzzle puzzle)
        {
            var serialized = new SerializedObject(puzzle);
            FillIfEmpty(serialized.FindProperty("thiefBodies"), ThiefBodies);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void FillIfEmpty(SerializedProperty list, string[] paths)
        {
            if (list.arraySize > 0) return;

            list.arraySize = paths.Length;
            for (int i = 0; i < paths.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = Load<Sprite>(paths[i]);
        }

        // Map 2 (the park) gets the mini game after it, unless something is there already.
        private static void HookUpAfterSecondMap(CctvPuzzle puzzle)
        {
            var maps = Object.FindFirstObjectByType<MapSwitcher>(FindObjectsInactive.Include);
            if (maps == null) return;

            var serialized = new SerializedObject(maps);
            SerializedProperty list = serialized.FindProperty("maps");
            if (list.arraySize < 3) return;

            SerializedProperty after = list.GetArrayElementAtIndex(1).FindPropertyRelative("interludeAfter");
            if (after.objectReferenceValue != null) return;

            after.objectReferenceValue = puzzle;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // The wall panel's instance in the scene: the one there already, or a new one over the wall.
        private static CctvWall PlaceWall(CctvWall prefab)
        {
            foreach (CctvWall existing in Object.FindObjectsByType<CctvWall>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing) == prefab) return existing;
            }

            Transform side = Find("SideDisplay");
            var instance = (CctvWall)PrefabUtility.InstantiatePrefab(prefab, side);
            Transform curtain = Find("Fade Curtain");
            if (curtain != null && curtain.parent == side) instance.transform.SetSiblingIndex(curtain.GetSiblingIndex());
            instance.gameObject.SetActive(false);
            return instance;
        }

        private static void BuildBin()
        {
            var root = new GameObject("Cctv Bin");
            root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var view = root.AddComponent<CctvBinView>();

            SpriteRenderer spotlight = Picture("Spotlight", root.transform, Load<Sprite>(LineupArt + "Spotlight.png"), 2);
            spotlight.color = new Color(1f, 0.93f, 0.55f, 0.75f);
            Fit(spotlight, RingRadius * 4f);
            spotlight.gameObject.SetActive(false);
            SpriteRenderer back = Picture("Ring Back", root.transform, LoadGuid<Sprite>(LineupPrefabBuilder.RingBackGuid), 3);
            Fit(back, RingRadius * 2f);
            SpriteRenderer fill = Picture("Ring Fill", root.transform, LoadGuid<Sprite>(LineupPrefabBuilder.RingFillGuid), 4);
            fill.sharedMaterial = LoadGuid<Material>(LineupPrefabBuilder.RingFillMaterialGuid);
            Fit(fill, RingRadius * 2f);

            var figure = new GameObject("Figure").transform;
            figure.SetParent(root.transform, false);
            SpriteRenderer bin = Picture("Bin", figure, Load<Sprite>(BinArt + BinPictures[0] + ".png"), 6);
            if (bin.sprite != null) figure.localScale = Vector3.one * (0.9f / bin.sprite.bounds.size.y);

            // The thief pops out over the bin's top, the money bag over its head.
            var thief = new GameObject("Thief").transform;
            thief.SetParent(root.transform, false);
            thief.localPosition = new Vector3(0f, 0.45f, 0f);
            SpriteRenderer body = Picture("Body", thief, Load<Sprite>(ThiefBodies[0]), 7);
            SpriteRenderer bag = Picture("Bag", thief, Load<Sprite>(LineupArt + "Bag.png"), 8);
            bag.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            Fit(bag, 0.45f);
            thief.gameObject.SetActive(false);

            var serialized = new SerializedObject(view);
            serialized.FindProperty("ringFill").objectReferenceValue = fill;
            serialized.FindProperty("figure").objectReferenceValue = figure;
            serialized.FindProperty("bin").objectReferenceValue = bin;
            serialized.FindProperty("thief").objectReferenceValue = thief;
            serialized.FindProperty("thiefBody").objectReferenceValue = body;
            serialized.FindProperty("bag").objectReferenceValue = bag;
            serialized.FindProperty("spotlight").objectReferenceValue = spotlight;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, BinPath);
            Object.DestroyImmediate(root);
        }

        // Four Cctv Bins side by side across the middle of the floor, left to right in the hierarchy,
        // each with its own picture, straight on the map's floor. Lies flat like the bins.
        private static void BuildFloor()
        {
            var binPrefab = AssetDatabase.LoadAssetAtPath<CctvBinView>(BinPath);
            var root = new GameObject("Cctv Floor");

            for (int i = 0; i < Bins; i++)
            {
                var bin = (CctvBinView)PrefabUtility.InstantiatePrefab(binPrefab, root.transform);
                bin.name = $"Bin {i + 1}";
                bin.transform.localPosition = new Vector3((i - (Bins - 1) * 0.5f) * FloorSpacing, 0.03f, 0f);
                var serialized = new SerializedObject(bin);
                var picture = (SpriteRenderer)serialized.FindProperty("bin").objectReferenceValue;
                picture.sprite = Load<Sprite>(BinPicture(i));
                Transform figure = picture.transform.parent;
                if (picture.sprite != null) figure.localScale = Vector3.one * (0.9f / picture.sprite.bounds.size.y);
            }

            PrefabUtility.SaveAsPrefabAsset(root, FloorPath);
            Object.DestroyImmediate(root);
        }

        // Takes out the dark band the first Cctv Floor had under its bins (a stretched Card picture
        // that left a shadow on the floor), keeping everything else in the prefab.
        public static void DropFloorBand()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FloorPath) == null) return;

            GameObject contents = PrefabUtility.LoadPrefabContents(FloorPath);
            Transform band = contents.transform.Find("Band");
            var picture = band != null ? band.GetComponent<SpriteRenderer>() : null;
            if (picture != null && picture.sprite == AssetDatabase.LoadAssetAtPath<Sprite>(LineupArt + "Card.png"))
            {
                Object.DestroyImmediate(band.gameObject);
                PrefabUtility.SaveAsPrefabAsset(contents, FloorPath);
                Debug.Log("[CctvPrefabBuilder] Cctv Floor no longer has the dark band under its bins.");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static string BinPicture(int bin)
        {
            return BinArt + BinPictures[bin % BinPictures.Length] + ".png";
        }

        // Sizes the camera's bin so what is drawn of its picture (a tight sprite mesh's bounds leave
        // out the clear edge, its rect does not) is WallBinHeight tall, standing on BinBottom at x.
        private static void FitOnScreen(Image bin, float x)
        {
            Sprite picture = bin.sprite;
            if (picture == null) return;

            float unitsPerPixel = 1f / picture.pixelsPerUnit;
            Vector2 full = picture.rect.size * unitsPerPixel;
            Bounds drawn = picture.bounds;
            float scale = WallBinHeight / Mathf.Max(0.0001f, drawn.size.y);
            bin.rectTransform.sizeDelta = full * scale;
            // Bounds are measured from the sprite's pivot; the box's position is its middle.
            Vector2 pivotFromMiddle = picture.pivot * unitsPerPixel - full * 0.5f;
            Vector2 drawnFromMiddle = (Vector2)drawn.center + pivotFromMiddle;
            Vector2 drawnCentre = new Vector2(x, BinBottom + WallBinHeight * 0.5f);
            bin.rectTransform.anchoredPosition = drawnCentre - drawnFromMiddle * scale;
        }

        // Laid out in the wall canvas's units, at Display 2's proportions like the lineup's wall.
        private static void BuildWall()
        {
            var root = new GameObject("Cctv Wall", typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>();
            Place(rect, Vector2.zero, LineupPrefabBuilder.WallSize);
            var wall = root.AddComponent<CctvWall>();

            Image background = UiImage("Background", rect, null, new Color(0.02f, 0.03f, 0.04f, 1f));
            Stretch(background.rectTransform);
            TMP_Text title = UiText("Title", rect, "WATCH THE MONEY BAG!", 56f, Gold, new Vector2(0f, 172f), new Vector2(1200f, 72f));
            TMP_Text line = UiText("Line", rect, "WHICH BIN DOES IT END UP IN?", 28f, Color.white, new Vector2(0f, 130f), new Vector2(1200f, 38f));

            Image frame = UiImage("Screen Frame", rect, Load<Sprite>(LineupArt + "Card.png"), new Color(0.16f, 0.17f, 0.18f, 1f));
            frame.type = Image.Type.Sliced;
            Place(frame.rectTransform, new Vector2(0f, -50f), ScreenSize + new Vector2(30f, 30f));
            Image screen = UiImage("Screen", rect, null, ScreenColor);
            Place(screen.rectTransform, new Vector2(0f, -50f), ScreenSize);
            screen.gameObject.AddComponent<RectMask2D>();
            RectTransform view = screen.rectTransform;

            // The thieves come before the bins so a thief inside a bin is hidden behind it.
            var serialized = new SerializedObject(wall);
            SerializedProperty thieves = serialized.FindProperty("thieves");
            SerializedProperty bags = serialized.FindProperty("bags");
            thieves.arraySize = Thieves;
            bags.arraySize = Thieves;
            for (int i = 0; i < Thieves; i++)
            {
                Image thief = UiImage($"Thief {i + 1}", view, Load<Sprite>(ThiefBodies[i]), Color.white);
                thief.preserveAspect = true;
                Place(thief.rectTransform, new Vector2((i - 1) * BinSpacing, BinY), new Vector2(120f, 120f));
                Image bag = UiImage("Bag", thief.rectTransform, Load<Sprite>(LineupArt + "Bag.png"), Color.white);
                bag.preserveAspect = true;
                Place(bag.rectTransform, new Vector2(0f, 82f), new Vector2(64f, 64f));
                thief.gameObject.SetActive(false);
                thieves.GetArrayElementAtIndex(i).objectReferenceValue = thief;
                bags.GetArrayElementAtIndex(i).objectReferenceValue = bag;
            }

            SerializedProperty bins = serialized.FindProperty("bins");
            bins.arraySize = Bins;
            for (int i = 0; i < Bins; i++)
            {
                Image bin = UiImage($"Bin {i + 1}", view, Load<Sprite>(BinPicture(i)), Color.white);
                bin.preserveAspect = true;
                Place(bin.rectTransform, Vector2.zero, Vector2.one);
                FitOnScreen(bin, (i - (Bins - 1) * 0.5f) * BinSpacing);
                bins.GetArrayElementAtIndex(i).objectReferenceValue = bin;
            }

            TMP_Text stamp = UiText("Stamp", view, "CAUGHT", 72f, new Color(0.86f, 0.12f, 0.1f, 1f), new Vector2(0f, 40f), new Vector2(360f, 96f));
            stamp.fontSharedMaterial = LoadGuid<Material>(LineupPrefabBuilder.OutlinedFontGuid);
            stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
            stamp.gameObject.SetActive(false);

            TMP_Text rec = UiText("Rec", view, "<color=#FF3B30>●</color> REC", 30f, CameraText, new Vector2(-470f, 120f), new Vector2(140f, 40f));
            TMP_Text camera = UiText("Camera", view, "CAM 02 - PARK", 26f, CameraText, new Vector2(-440f, -128f), new Vector2(220f, 34f));
            camera.alpha = 0.8f;
            TMP_Text clock = UiText("Clock", view, "23:41:07", 30f, CameraText, new Vector2(460f, 120f), new Vector2(160f, 40f));

            // Beside the screen: what to find on the left, the seconds left to pick on the right.
            Image wanted = UiImage("Find The Bag", rect, Load<Sprite>(LineupArt + "Icon_Bag.png"), Color.white);
            wanted.preserveAspect = true;
            Place(wanted.rectTransform, new Vector2(-695f, -20f), new Vector2(150f, 150f));
            UiText("Find Label", rect, "FIND THE BAG", 28f, Gold, new Vector2(-695f, -120f), new Vector2(240f, 36f));
            RectTransform group = UiRect("Countdown", rect);
            Place(group, new Vector2(695f, -20f), new Vector2(240f, 240f));
            TMP_Text countdown = UiText("Seconds", group, "10", 110f, Gold, new Vector2(0f, 10f), new Vector2(240f, 130f));
            UiText("Label", group, "PICK A BIN!", 28f, Color.white, new Vector2(0f, -95f), new Vector2(240f, 36f));
            group.gameObject.SetActive(false);

            serialized.FindProperty("title").objectReferenceValue = title;
            serialized.FindProperty("line").objectReferenceValue = line;
            serialized.FindProperty("recLight").objectReferenceValue = rec;
            serialized.FindProperty("clock").objectReferenceValue = clock;
            serialized.FindProperty("countdownGroup").objectReferenceValue = group.gameObject;
            serialized.FindProperty("countdown").objectReferenceValue = countdown;
            serialized.FindProperty("stamp").objectReferenceValue = stamp.rectTransform;
            serialized.FindProperty("entrance").vector2Value = new Vector2(-ScreenSize.x * 0.5f - 80f, BinY);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, WallPath);
            Object.DestroyImmediate(root);
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetIfEmpty(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property.objectReferenceValue != null) return;

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Find(string name)
        {
            foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.name == name) return candidate;
            }

            return null;
        }

        private static SpriteRenderer Picture(string name, Transform parent, Sprite sprite, int order)
        {
            var renderer = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        // Scales the picture so it is size wide, in metres.
        private static void Fit(SpriteRenderer picture, float size)
        {
            if (picture.sprite != null) picture.transform.localScale = Vector3.one * (size / picture.sprite.bounds.size.x);
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

        private static TMP_Text UiText(string name, Transform parent, string text, float size, Color color, Vector2 position, Vector2 box)
        {
            RectTransform rect = UiRect(name, parent);
            Place(rect, position, box);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = LoadGuid<TMP_FontAsset>(LineupPrefabBuilder.FontGuid);
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
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
            if (asset == null) Debug.LogWarning($"[CctvPrefabBuilder] Missing {typeof(T).Name} at {path}");
            return asset;
        }

        private static T LoadGuid<T>(string guid) where T : Object
        {
            return Load<T>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
