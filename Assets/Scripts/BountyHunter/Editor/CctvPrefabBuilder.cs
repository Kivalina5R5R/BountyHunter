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
            CctvPrefabBuilder.UseMonitorDesignIfOld();
            CctvPrefabBuilder.StandBinsUpright();
            if (Object.FindFirstObjectByType<CctvPuzzle>(FindObjectsInactive.Include) == null) CctvPrefabBuilder.Wire();
        }
    }

    // Tools > Bounty Hunter > Build CCTV Prefabs: makes the security camera mini game's prefabs in
    // Assets/Prefabs/Cctv: Cctv Bin (one bin on the floor), Cctv Floor (four Cctv Bins in a row, each
    // with its own picture) and Cctv Wall (the camera on the wall in the mini game's monitor design,
    // its four bins the same pictures in the same order, all as tall as each other, and the banana
    // peels that fly out of them), and wires the open scene to them: a Cctv Puzzle object next to the Lineup Puzzle
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
        // The mini game's own art: the monitor background, the title strip, the note, the TIME panel,
        // the banana peel and the bins.
        private const string Art = "Assets/ART/Minigame/Cctv/";
        private static readonly string[] ArtPictures = { "CctvBG", "TitleStrip", "Note", "TimePanel", "BananaPeel", "Bin1", "Bin2", "Bin3", "Bin4" };

        private const float RingRadius = 0.7f;
        private const int Bins = 4;
        private const float FloorSpacing = 2f;
        private const int Thieves = 3;
        // The monitor's screen in the background picture (wall units), which the camera's clip plays in.
        private static readonly Vector2 ScreenSpot = new Vector2(-76f, -20f);
        private static readonly Vector2 ScreenSize = new Vector2(851f, 394f);
        // Where the bins stand on the screen and how far apart (screen units): their bottoms on
        // BinBottom, each WallBinHeight tall counting only what is drawn of its picture.
        private const float BinY = -103f;
        private const float BinSpacing = 180f;
        private const float BinBottom = -168f;
        private const float WallBinHeight = 130f;
        // A banana peel's size once it is stuck on the screen.
        private static readonly Vector2 PeelSize = new Vector2(290f, 272f);
        private static readonly Color Title = new Color(1f, 0.86f, 0.2f, 1f);
        private static readonly Color Ink = new Color(0.17f, 0.09f, 0.06f, 1f);

        private static readonly string[] ThiefBodies =
        {
            "Assets/ART/Thif/Cat/Cat.png",
            "Assets/ART/Thif/Chicken/Chicken.png",
            "Assets/ART/Thif/Crow/Crow.png",
            "Assets/ART/Thif/Rat/Rat.png",
        };

        // Bin i shows BinPictures[i % length], on the floor and on the camera.
        private static readonly string[] BinPictures = { "Bin1", "Bin2", "Bin3", "Bin4" };
        // How far each of those pictures is turned (degrees, anticlockwise) to stand it upright: the
        // art draws the bins leaning a little.
        private static readonly float[] BinUpright = { -3.7f, -4.5f, 8f, 9.9f };

        [MenuItem("Tools/Bounty Hunter/Build CCTV Prefabs")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Cctv");

            UseArtAsSprites();
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
            SpriteRenderer bin = Picture("Bin", figure, Load<Sprite>(BinPicture(0)), 6);
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
                picture.transform.localRotation = Upright(i);
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
            return Art + BinPictures[bin % BinPictures.Length] + ".png";
        }

        private static Quaternion Upright(int bin)
        {
            return Quaternion.Euler(0f, 0f, BinUpright[bin % BinUpright.Length]);
        }

        // Turns the bins on the floor and on the camera upright when they are not yet, keeping
        // everything else in the prefabs.
        public static void StandBinsUpright()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FloorPath) != null)
            {
                GameObject floor = PrefabUtility.LoadPrefabContents(FloorPath);
                CctvBinView[] views = floor.GetComponentsInChildren<CctvBinView>(true);
                var pictures = new Transform[views.Length];
                for (int i = 0; i < views.Length; i++)
                {
                    var picture = (SpriteRenderer)new SerializedObject(views[i]).FindProperty("bin").objectReferenceValue;
                    pictures[i] = picture != null ? picture.transform : null;
                }

                SaveIfTurned(floor, FloorPath, pictures);
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(WallPath) == null) return;

            GameObject wall = PrefabUtility.LoadPrefabContents(WallPath);
            SerializedProperty bins = new SerializedObject(wall.GetComponent<CctvWall>()).FindProperty("bins");
            var rects = new Transform[bins.arraySize];
            for (int i = 0; i < rects.Length; i++)
            {
                var bin = (Image)bins.GetArrayElementAtIndex(i).objectReferenceValue;
                rects[i] = bin != null ? bin.transform : null;
            }

            SaveIfTurned(wall, WallPath, rects);
        }

        // Turns picture i to Upright(i), saving the prefab only when one of them moved.
        private static void SaveIfTurned(GameObject contents, string path, Transform[] pictures)
        {
            bool turned = false;
            for (int i = 0; i < pictures.Length; i++)
            {
                if (pictures[i] == null || Quaternion.Angle(pictures[i].localRotation, Upright(i)) < 0.01f) continue;

                pictures[i].localRotation = Upright(i);
                turned = true;
            }

            if (turned)
            {
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                Debug.Log($"[CctvPrefabBuilder] The bins in {path} now stand upright.");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        // Puts the wall and the floor's bins in the monitor design (the art in Assets/ART/Minigame/Cctv)
        // when the wall prefab is not in it yet. The wall is remade, so changes made in it by hand are
        // lost; the floor keeps its layout and only its bins' pictures change.
        public static void UseMonitorDesignIfOld()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<CctvWall>(WallPath);
            if (prefab == null || prefab.HasBananaPeels) return;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "CctvBG.png") == null) return;

            UseArtAsSprites();
            BuildWall();
            UseNewBinsOnFloor();
            Debug.Log("[CctvPrefabBuilder] Cctv Wall is now in the monitor design with banana peels, and the floor's bins have the new pictures.");
        }

        // The floor's bins, left to right, get the new bin pictures, each fitted to its height again.
        private static void UseNewBinsOnFloor()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FloorPath) == null) return;

            GameObject contents = PrefabUtility.LoadPrefabContents(FloorPath);
            CctvBinView[] views = contents.GetComponentsInChildren<CctvBinView>(true);
            for (int i = 0; i < views.Length; i++)
            {
                var picture = (SpriteRenderer)new SerializedObject(views[i]).FindProperty("bin").objectReferenceValue;
                if (picture == null) continue;

                picture.sprite = Load<Sprite>(BinPicture(i));
                if (picture.sprite != null) picture.transform.parent.localScale = Vector3.one * (0.9f / picture.sprite.bounds.size.y);
            }

            PrefabUtility.SaveAsPrefabAsset(contents, FloorPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        // Makes the mini game's art sprites (it comes in as plain textures), the background at full size.
        private static void UseArtAsSprites()
        {
            foreach (string name in ArtPictures)
            {
                string path = Art + name + ".png";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                if (importer.textureType == TextureImporterType.Sprite) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = name == "CctvBG" ? 8192 : 2048;
                importer.SaveAndReimport();
            }
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

        // Laid out in the wall canvas's units, at Display 2's proportions like the lineup's wall, on the
        // mini game's design: the monitor background filling the wall, the clip playing in its screen
        // with the bins along the bottom, the question on a black strip over the screen with the TIME
        // panel beside it, and FIND THE BAG on a note stuck over the newspaper on the left.
        private static void BuildWall()
        {
            var root = new GameObject("Cctv Wall", typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>();
            Place(rect, Vector2.zero, LineupPrefabBuilder.WallSize);
            var wall = root.AddComponent<CctvWall>();

            Image background = UiImage("Background", rect, Load<Sprite>(Art + "CctvBG.png"), Color.white);
            Place(background.rectTransform, Vector2.zero, LineupPrefabBuilder.WallSize);

            // The screen only shows what is inside it, so a thief running in comes from behind its edge.
            RectTransform view = UiRect("Screen", rect);
            Place(view, ScreenSpot, ScreenSize);
            view.gameObject.AddComponent<RectMask2D>();

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
                bin.rectTransform.localRotation = Upright(i);
                bins.GetArrayElementAtIndex(i).objectReferenceValue = bin;
            }

            // The peels go over the bins and the thieves; the box they fly in is the whole screen.
            RectTransform peelArea = UiRect("Banana Peels", view);
            Stretch(peelArea);
            var peels = peelArea.gameObject.AddComponent<CctvBananaPeels>();
            Image peel = UiImage("Peel", peelArea, Load<Sprite>(Art + "BananaPeel.png"), Color.white);
            peel.preserveAspect = true;
            Place(peel.rectTransform, Vector2.zero, PeelSize);
            peel.gameObject.SetActive(false);
            var peelSettings = new SerializedObject(peels);
            peelSettings.FindProperty("peel").objectReferenceValue = peel;
            peelSettings.ApplyModifiedPropertiesWithoutUndo();

            TMP_Text stamp = UiText("Stamp", view, "CAUGHT", 72f, new Color(0.86f, 0.12f, 0.1f, 1f), new Vector2(0f, 40f), new Vector2(360f, 96f));
            stamp.fontSharedMaterial = LoadGuid<Material>(LineupPrefabBuilder.OutlinedFontGuid);
            stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
            stamp.gameObject.SetActive(false);

            // The question on the black strip over the top of the screen.
            Image strip = Fitted("Title Strip", rect, "TitleStrip", new Vector2(-102f, 135f), 65f);
            Vector2 stripSize = strip.rectTransform.sizeDelta;
            TMP_Text title = UiText("Title", strip.rectTransform, "WHERE IS THE MONEY BAG?", 40f, Title, Vector2.zero, new Vector2(0.86f * stripSize.x, 0.8f * stripSize.y));
            title.enableAutoSizing = true;
            title.fontSizeMin = 18f;
            title.fontSizeMax = 44f;

            // The seconds left to pick, on the blue part of the TIME panel.
            Image time = Fitted("Time", rect, "TimePanel", new Vector2(198f, 128f), 72f);
            Vector2 timeSize = time.rectTransform.sizeDelta;
            TMP_Text countdown = UiText("Seconds", time.rectTransform, "00:24", 40f, Color.white, new Vector2(0.06f * timeSize.x, -0.14f * timeSize.y), new Vector2(0.75f * timeSize.x, 0.55f * timeSize.y));
            countdown.enableAutoSizing = true;
            countdown.fontSizeMin = 16f;
            countdown.fontSizeMax = 44f;
            time.gameObject.SetActive(false);

            // What to find, on a note stuck over the newspaper.
            Image note = Fitted("Find The Bag", rect, "Note", new Vector2(-632f, 98f), 146f);
            Vector2 noteSize = note.rectTransform.sizeDelta;
            Image wanted = UiImage("Bag", note.rectTransform, Load<Sprite>(LineupArt + "Icon_Bag.png"), Color.white);
            wanted.preserveAspect = true;
            Place(wanted.rectTransform, new Vector2(0f, 0.1f * noteSize.y), 0.5f * noteSize);
            TMP_Text findLabel = UiText("Label", note.rectTransform, "FIND THE BAG", 26f, Ink, new Vector2(0f, -0.3f * noteSize.y), new Vector2(0.9f * noteSize.x, 0.2f * noteSize.y));
            findLabel.enableAutoSizing = true;
            findLabel.fontSizeMin = 12f;
            findLabel.fontSizeMax = 28f;

            serialized.FindProperty("title").objectReferenceValue = title;
            serialized.FindProperty("line").objectReferenceValue = null;
            serialized.FindProperty("recLight").objectReferenceValue = null;
            serialized.FindProperty("clock").objectReferenceValue = null;
            serialized.FindProperty("peels").objectReferenceValue = peels;
            serialized.FindProperty("countdownGroup").objectReferenceValue = time.gameObject;
            serialized.FindProperty("countdown").objectReferenceValue = countdown;
            serialized.FindProperty("countdownFormat").stringValue = "{1:00}:{2:00}";
            serialized.FindProperty("stamp").objectReferenceValue = stamp.rectTransform;
            serialized.FindProperty("entrance").vector2Value = new Vector2(-ScreenSize.x * 0.5f - 80f, BinY);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, WallPath);
            Object.DestroyImmediate(root);
        }

        // The picture height tall, as wide as its own shape makes it.
        private static Image Fitted(string name, Transform parent, string art, Vector2 position, float height)
        {
            Sprite sprite = Load<Sprite>(Art + art + ".png");
            Image image = UiImage(name, parent, sprite, Color.white);
            float width = sprite != null ? height * sprite.rect.width / sprite.rect.height : height;
            Place(image.rectTransform, position, new Vector2(width, height));
            return image;
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
