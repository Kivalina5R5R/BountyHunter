using BountyHunter.Thieves;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves.EditorTools
{
    // Keeps the lineup up to date by itself each time the editor loads: builds the prefabs when they
    // are missing (and wires the open scene), remakes the suspect prefab when it has no item row yet,
    // fits the wall to Display 2, and gives the open scene's lineup the current cast. Whatever it
    // changes in the scene then needs saving.
    [InitializeOnLoad]
    public static class LineupPrefabAutoBuild
    {
        static LineupPrefabAutoBuild()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        private static void BuildIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LineupPrefabBuilder.SuspectPath) == null)
            {
                Debug.Log("[LineupPrefabBuilder] The lineup prefabs are missing; building them now.");
                LineupPrefabBuilder.Build();
                return;
            }

            // A wall prefab made before it had its fixed Display 2 size gets it.
            if (!LineupPrefabBuilder.WallFitsDisplay()) LineupPrefabBuilder.FitWall();
            if (!LineupPrefabBuilder.SuspectHasItemRow()) LineupPrefabBuilder.RebuildSuspect();
            LineupPrefabBuilder.UpdateCastIfOld();
            LineupPrefabBuilder.DropSuspectNumber();
            LineupPrefabBuilder.SetPickMarkIfMissing();
            LineupPrefabBuilder.HideGameWallDuringMiniGames();
            LineupPrefabBuilder.AddCulpritPanelIfMissing();
            LineupPrefabBuilder.CentreCulpritPanel();
            LineupPrefabBuilder.AddRewardPanelIfMissing();
        }
    }

    // Tools > Bounty Hunter > Build Lineup Prefabs: makes the suspect lineup's two prefabs,
    // Assets/Prefabs/Lineup/Lineup Suspect.prefab (one suspect on the floor) and Lineup Wall.prefab
    // (the wall panel), from the lineup art, and wires the open scene to them: the wall panel goes
    // over the game wall just behind the fade curtain, the Lineup Puzzle object gets both, and the
    // mini game is hooked up after map 1 if it is not yet. Run it again to start the prefabs over
    // (it overwrites them, so changes made in them by hand are lost); save the scene afterwards.
    public static class LineupPrefabBuilder
    {
        private const string Folder = "Assets/Prefabs/Lineup";
        public const string SuspectPath = Folder + "/Lineup Suspect.prefab";
        public const string WallPath = Folder + "/Lineup Wall.prefab";
        private const string Art = "Assets/ART/Lineup/";

        // The character cards' ring art and radial fill material, the game's font and its outlined material.
        private const string RingBackGuid = "8758dec64893bbe49a5f1000e9815246";
        private const string RingFillGuid = "8e2e7c8a2047803448ea7332356dea0e";
        private const string RingFillMaterialGuid = "c336646ec748937479367356284f07bf";
        private const string FontGuid = "d4e98a79a56adb245b3f28d26d672faf";
        private const string OutlinedFontGuid = "ba7aada5ab4287b478f894e8210a4181";
        private const string WallBackgroundPath = "Assets/ART/Lineup/WallBackground.png";
        private const string WitnessPath = "Assets/ART/Police/Bunny.png";

        private const float RingRadius = 0.6f;
        // The wall panel's size in the wall canvas's units: as tall as the canvas (435) and as wide as
        // Display 2 shows at that height (8192 x 2160), a little wider than the canvas itself (1536),
        // so the panel covers the whole wall and opens in its prefab at the wall's real proportions.
        private static readonly Vector2 WallSize = new Vector2(435f * 8192f / 2160f, 435f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 1f);
        private static readonly Color Ink = new Color(0.17f, 0.09f, 0.06f, 1f);

        // Who can stand in the lineup and where things sit on each (see LineupLook), tuned on the art.
        private static readonly Cast[] Lineup =
        {
            new Cast("Cat", "Assets/ART/Thif/Cat/Cat.png", true),
            new Cast("Chicken", "Assets/ART/Thif/Chicken/Chicken.png", true),
            new Cast("Crow", "Assets/ART/Thif/Crow/Crow.png", true),
            new Cast("Rat", "Assets/ART/Thif/Rat/Rat.png", true),
            new Cast("Capybara", "Assets/ART/NPC/Capybara/Idle/CapybaraIdle_00.png", false),
            new Cast("Goat", "Assets/ART/NPC/Goat/Idle/GoatIdle_00.png", false),
            new Cast("Otter", "Assets/ART/NPC/Otter/Idle/OtterIdle_00.png", false),
            new Cast("Pig", "Assets/ART/NPC/Pig/Idle/PigIdle_00.png", false),
            new Cast("Raccoon", "Assets/ART/NPC/Raccoon/Idle/RaccoonIdle_00.png", false),
            new Cast("Sloth", "Assets/ART/NPC/Sloth/Idle/SlothIdle_00.png", false),
        };

        // The witness's picture and words for each feature, in LineupFeature order.
        private static readonly (LineupFeature Feature, string Icon, string Label)[] Features =
        {
            (LineupFeature.Mask, "Icon_Mask", "MASK"),
            (LineupFeature.Glasses, "Icon_Glasses", "GLASSES"),
            (LineupFeature.FaceMask, "Icon_FaceMask", "FACE MASK"),
            (LineupFeature.Mustache, "Icon_Mustache", "MUSTACHE"),
            (LineupFeature.Newspaper, "Icon_Newspaper", "NEWSPAPER"),
            (LineupFeature.Gloves, "Icon_Gloves", "GLOVES"),
            (LineupFeature.Hat, "Icon_Hat", "HAT"),
            (LineupFeature.Bag, "Icon_Bag", "MONEY BAG"),
            (LineupFeature.Cap, "Icon_Cap", "CAP"),
            (LineupFeature.Beanie, "Icon_Beanie", "BEANIE"),
        };

        // What the suspects carry, by the LineupFloor field that holds it.
        private static readonly (string Field, string Art)[] Carried =
        {
            ("cap", "Cap"), ("beanie", "Beanie"), ("mask", "Mask"), ("glasses", "Glasses"), ("faceMask", "FaceMask"),
            ("mustache", "Mustache"), ("newspaper", "Newspaper"), ("gloves", "Gloves"), ("bag", "Bag"),
        };

        // One LineupLook's values.
        private sealed class Cast
        {
            public Cast(string species, string body, bool isThief)
            {
                Species = species;
                Body = body;
                IsThief = isThief;
            }

            public string Species { get; }
            public string Body { get; }
            public bool IsThief { get; }
        }

        // True when the suspect prefab has its row of items over the head (or there is no suspect prefab).
        public static bool SuspectHasItemRow()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<LineupSuspectView>(SuspectPath);
            return prefab == null || prefab.HasItemRow;
        }

        // Remakes only the suspect prefab (the wall prefab, maybe changed by hand, is left alone).
        public static void RebuildSuspect()
        {
            LineupSuspectView suspect = BuildSuspect();
            var puzzle = Object.FindFirstObjectByType<LineupPuzzle>(FindObjectsInactive.Include);
            var floor = puzzle != null ? puzzle.GetComponent<LineupFloor>() : null;
            if (floor != null)
            {
                Set(floor, "suspectPrefab", suspect);
                EditorSceneManager.MarkSceneDirty(floor.gameObject.scene);
            }

            Debug.Log("[LineupPrefabBuilder] Lineup Suspect remade with its row of items over the head; save the scene to keep it.");
        }

        // Gives the open scene's lineup the current cast and clue words, if it has an older one.
        // Takes the number (1 to 6) out of the suspect prefab, keeping everything else in it.
        public static void DropSuspectNumber()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SuspectPath) == null) return;

            GameObject contents = PrefabUtility.LoadPrefabContents(SuspectPath);
            Transform number = contents.transform.Find("Number");
            if (number != null)
            {
                Object.DestroyImmediate(number.gameObject);
                PrefabUtility.SaveAsPrefabAsset(contents, SuspectPath);
                Debug.Log("[LineupPrefabBuilder] The numbers are gone from Lineup Suspect.");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        // Adds the culprit panel to a wall prefab made before it had one, keeping everything else in it.
        // Adds the "right answer now pays" panel to a wall prefab made before it had one.
        public static void AddRewardPanelIfMissing()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<LineupWall>(WallPath);
            if (prefab == null || prefab.HasRewardPanel) return;

            GameObject contents = PrefabUtility.LoadPrefabContents(WallPath);
            var serialized = new SerializedObject(contents.GetComponent<LineupWall>());
            AddRewardPanel(contents.GetComponent<RectTransform>(), serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, WallPath);
            PrefabUtility.UnloadPrefabContents(contents);
            Debug.Log("[LineupPrefabBuilder] Lineup Wall now shows what a right answer pays, under the clue cards.");
        }

        // Under the clue cards, near the bottom of the wall: a cream strip saying what a right answer
        // given now pays. Hidden until the first clue.
        private static void AddRewardPanel(RectTransform wall, SerializedObject serialized)
        {
            Image panel = UiImage("Reward", wall, Load<Sprite>(Art + "Pill.png"), Color.white);
            panel.type = Image.Type.Sliced;
            panel.pixelsPerUnitMultiplier = 2f;
            Place(panel.rectTransform, new Vector2(0f, -188f), new Vector2(470f, 50f));
            TMP_Text text = UiText("Text", panel.rectTransform, "RIGHT ANSWER NOW: +20$", 34f, Ink, Vector2.zero, new Vector2(450f, 46f));
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = 34f;

            serialized.FindProperty("rewardPanel").objectReferenceValue = panel.rectTransform;
            serialized.FindProperty("rewardText").objectReferenceValue = text;
            panel.gameObject.SetActive(false);
        }

        public static void AddCulpritPanelIfMissing()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<LineupWall>(WallPath);
            if (prefab == null || prefab.HasCulpritPanel) return;

            GameObject contents = PrefabUtility.LoadPrefabContents(WallPath);
            var serialized = new SerializedObject(contents.GetComponent<LineupWall>());
            AddCulpritPanel(contents.GetComponent<RectTransform>(), serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, WallPath);
            PrefabUtility.UnloadPrefabContents(contents);
            Debug.Log("[LineupPrefabBuilder] Lineup Wall now shows the culprit at the reveal.");
        }

        // Where the culprit panel sits on the wall (the middle) and how big its picture is there: as
        // tall as the wall allows over the strip, since the reveal has no words over it. The picture
        // and the stamp sit pictureRise above the panel's middle, the strip stripDrop below it.
        private static readonly Vector2 CulpritPanelSpot = new Vector2(0f, -20f);
        private static readonly Vector2 CulpritPictureSize = new Vector2(320f, 320f);
        private const float CulpritPictureRise = 45f;
        private const float CulpritStripDrop = -157f;
        // The layout the panel had before (too small), which is moved to the one above.
        private static readonly Vector2 OldCulpritPictureSize = new Vector2(210f, 210f);

        // Moves a culprit panel made on the right (where the countdown is) to the middle of the wall,
        // and gives it its strip, keeping everything else in the prefab.
        public static void CentreCulpritPanel()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<LineupWall>(WallPath);
            if (prefab == null || !prefab.HasCulpritPanel) return;

            GameObject contents = PrefabUtility.LoadPrefabContents(WallPath);
            var wall = contents.GetComponent<LineupWall>();
            var serialized = new SerializedObject(wall);
            var panel = (RectTransform)serialized.FindProperty("culprit").objectReferenceValue;
            SerializedProperty strip = serialized.FindProperty("culpritStrip");
            bool changed = false;
            var picture = (Image)serialized.FindProperty("culpritPicture").objectReferenceValue;
            bool oldLayout = panel != null && (panel.anchoredPosition.x > 300f || (picture != null && picture.rectTransform.sizeDelta == OldCulpritPictureSize));
            if (oldLayout)
            {
                EnlargeCulprit(panel, picture, serialized);
                changed = true;
            }

            if (panel != null && strip.objectReferenceValue == null)
            {
                Transform found = panel.Find("Strip");
                if (found != null) strip.objectReferenceValue = found;
                changed = true;
            }

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, WallPath);
                Debug.Log("[LineupPrefabBuilder] The culprit now comes up in the middle of the wall.");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        // Moves an older culprit panel to the middle and makes it as big as the wall allows.
        private static void EnlargeCulprit(RectTransform panel, Image picture, SerializedObject serialized)
        {
            panel.anchoredPosition = CulpritPanelSpot;
            if (picture != null)
            {
                picture.rectTransform.sizeDelta = CulpritPictureSize;
                picture.rectTransform.anchoredPosition = new Vector2(0f, CulpritPictureRise);
            }

            var stamp = (RectTransform)serialized.FindProperty("culpritStamp").objectReferenceValue;
            if (stamp != null)
            {
                stamp.anchoredPosition = new Vector2(0f, CulpritPictureRise);
                stamp.sizeDelta = new Vector2(400f, 110f);
                var stampText = stamp.GetComponent<TMP_Text>();
                if (stampText != null) stampText.fontSize = 84f;
            }

            Transform strip = panel.Find("Strip");
            if (strip is RectTransform stripRect) stripRect.anchoredPosition = new Vector2(0f, CulpritStripDrop);
        }

        // The culprit at the reveal, in the middle of the wall: its picture, the CAUGHT
        // stamp over it and what it carries on a cream strip under it. Hidden until the reveal.
        private static void AddCulpritPanel(RectTransform wall, SerializedObject serialized)
        {
            RectTransform panel = UiRect("Culprit", wall);
            Place(panel, CulpritPanelSpot, new Vector2(300f, 330f));

            Image picture = UiImage("Picture", panel, Load<Sprite>("Assets/ART/Thif/Cat/Cat.png"), Color.white);
            picture.preserveAspect = true;
            Place(picture.rectTransform, new Vector2(0f, CulpritPictureRise), CulpritPictureSize);

            Image strip = UiImage("Strip", panel, Load<Sprite>(Art + "Pill.png"), Color.white);
            strip.type = Image.Type.Sliced;
            strip.pixelsPerUnitMultiplier = 2f;
            Place(strip.rectTransform, new Vector2(0f, CulpritStripDrop), new Vector2(236f, 76f));
            SerializedProperty items = serialized.FindProperty("culpritItems");
            items.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                Image item = UiImage($"Item {i + 1}", panel, Load<Sprite>(Art + "Glasses.png"), Color.white);
                item.preserveAspect = true;
                Place(item.rectTransform, new Vector2((i - 1) * 72f, CulpritStripDrop), new Vector2(60f, 60f));
                items.GetArrayElementAtIndex(i).objectReferenceValue = item;
            }

            TMP_Text stamp = UiText("Stamp", panel, "CAUGHT", 84f, new Color(0.86f, 0.12f, 0.1f, 1f), new Vector2(0f, CulpritPictureRise), new Vector2(400f, 110f));
            stamp.fontSharedMaterial = LoadGuid<Material>(OutlinedFontGuid);
            stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);

            serialized.FindProperty("culprit").objectReferenceValue = panel;
            serialized.FindProperty("culpritStrip").objectReferenceValue = strip.rectTransform;
            serialized.FindProperty("culpritPicture").objectReferenceValue = picture;
            serialized.FindProperty("culpritStamp").objectReferenceValue = stamp.rectTransform;
            panel.gameObject.SetActive(false);
        }

        // Puts the game wall (Play, under SideDisplay: the board, posters, dogs and their tags, the
        // background) on the list MapSwitcher switches off while a mini game plays, if it is not on
        // it yet, so it does not draw behind the mini game.
        public static void HideGameWallDuringMiniGames()
        {
            var maps = Object.FindFirstObjectByType<MapSwitcher>(FindObjectsInactive.Include);
            Transform wall = null;
            foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.name == "Play" && candidate.parent != null && candidate.parent.name == "SideDisplay") wall = candidate;
            }

            if (maps == null || wall == null) return;

            var serialized = new SerializedObject(maps);
            SerializedProperty hide = serialized.FindProperty("hideDuringInterlude");
            for (int i = 0; i < hide.arraySize; i++)
            {
                if (hide.GetArrayElementAtIndex(i).objectReferenceValue == wall.gameObject) return;
            }

            hide.arraySize++;
            hide.GetArrayElementAtIndex(hide.arraySize - 1).objectReferenceValue = wall.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(maps.gameObject.scene);
            Debug.Log("[LineupPrefabBuilder] The game wall is now switched off during mini games; save the scene to keep it.");
        }

        // Gives the open scene's lineup floor the tick a player gets once they have answered.
        public static void SetPickMarkIfMissing()
        {
            var floor = Object.FindFirstObjectByType<LineupFloor>(FindObjectsInactive.Include);
            if (floor == null) return;

            var serialized = new SerializedObject(floor);
            SerializedProperty mark = serialized.FindProperty("pickMark");
            if (mark.objectReferenceValue != null) return;

            mark.objectReferenceValue = Load<Sprite>(Art + "Badge_Yes.png");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(floor.gameObject.scene);
            Debug.Log("[LineupPrefabBuilder] Players who answer now get a tick; save the scene to keep it.");
        }

        public static void UpdateCastIfOld()
        {
            var puzzle = Object.FindFirstObjectByType<LineupPuzzle>(FindObjectsInactive.Include);
            if (puzzle == null) return;

            var serialized = new SerializedObject(puzzle);
            SerializedProperty looks = serialized.FindProperty("looks");
            bool current = looks.arraySize == Lineup.Length;
            for (int i = 0; current && i < Lineup.Length; i++)
            {
                current = looks.GetArrayElementAtIndex(i).FindPropertyRelative("species").stringValue == Lineup[i].Species;
            }

            SerializedProperty features = serialized.FindProperty("features");
            current &= features.arraySize == Features.Length;
            for (int i = 0; current && i < Features.Length; i++)
            {
                current = features.GetArrayElementAtIndex(i).FindPropertyRelative("label").stringValue == Features[i].Label;
            }

            if (current) return;

            ApplyCast(puzzle);
            Debug.Log("[LineupPrefabBuilder] The lineup's clues are now only what the suspects carry; save the scene to keep it.");
        }

        private static void ApplyCast(LineupPuzzle puzzle)
        {
            var serialized = new SerializedObject(puzzle);
            SerializedProperty looks = serialized.FindProperty("looks");
            looks.arraySize = Lineup.Length;
            for (int i = 0; i < Lineup.Length; i++)
            {
                Cast cast = Lineup[i];
                SerializedProperty look = looks.GetArrayElementAtIndex(i);
                look.FindPropertyRelative("species").stringValue = cast.Species;
                look.FindPropertyRelative("body").objectReferenceValue = Load<Sprite>(cast.Body);
                look.FindPropertyRelative("isThief").boolValue = cast.IsThief;
            }

            SerializedProperty features = serialized.FindProperty("features");
            features.arraySize = Features.Length;
            for (int i = 0; i < Features.Length; i++)
            {
                SerializedProperty feature = features.GetArrayElementAtIndex(i);
                feature.FindPropertyRelative("feature").enumValueIndex = (int)Features[i].Feature;
                feature.FindPropertyRelative("icon").objectReferenceValue = Load<Sprite>(Art + Features[i].Icon + ".png");
                feature.FindPropertyRelative("label").stringValue = Features[i].Label;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            var floor = puzzle.GetComponent<LineupFloor>();
            if (floor != null)
            {
                foreach ((string field, string art) in Carried) Set(floor, field, Load<Sprite>(Art + art + ".png"));
            }

            EditorSceneManager.MarkSceneDirty(puzzle.gameObject.scene);
        }

        // True when the wall prefab already has its fixed Display 2 size (or there is no wall prefab).
        public static bool WallFitsDisplay()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<RectTransform>(WallPath);
            return prefab == null || (prefab.anchorMin == prefab.anchorMax && prefab.sizeDelta == WallSize);
        }

        // Tools > Bounty Hunter > Fit Lineup Wall To Display 2: gives the existing wall prefab its fixed
        // Display 2 size (keeping everything else in it) and the scene's panel the same; save the
        // scene afterwards.
        [MenuItem("Tools/Bounty Hunter/Fit Lineup Wall To Display 2")]
        public static void FitWall()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(WallPath);
            Place(contents.GetComponent<RectTransform>(), Vector2.zero, WallSize);
            PrefabUtility.SaveAsPrefabAsset(contents, WallPath);
            PrefabUtility.UnloadPrefabContents(contents);

            var prefab = AssetDatabase.LoadAssetAtPath<LineupWall>(WallPath);
            foreach (LineupWall wall in Object.FindObjectsByType<LineupWall>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(wall) != prefab) continue;

                // The scene keeps its own copy of the panel's size; take the prefab's instead.
                PrefabUtility.RevertObjectOverride(wall.transform, InteractionMode.AutomatedAction);
                EditorSceneManager.MarkSceneDirty(wall.gameObject.scene);
            }

            Debug.Log($"[LineupPrefabBuilder] Lineup Wall is now {WallSize.x:0} x {WallSize.y:0} (Display 2's shape); save the scene to keep it.");
        }

        [MenuItem("Tools/Bounty Hunter/Build Lineup Prefabs")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Lineup");

            LineupSuspectView suspect = BuildSuspect();
            LineupWall wall = BuildWall();
            WireScene(suspect, wall);
            Debug.Log("[LineupPrefabBuilder] Lineup prefabs built and the scene wired; save the scene to keep it.");
        }

        private static LineupSuspectView BuildSuspect()
        {
            var root = new GameObject("Lineup Suspect");
            root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var view = root.AddComponent<LineupSuspectView>();

            SpriteRenderer spotlight = Picture("Spotlight", root.transform, Load<Sprite>(Art + "Spotlight.png"), 2);
            spotlight.color = new Color(1f, 0.93f, 0.55f, 0.75f);
            Fit(spotlight, RingRadius * 4.2f);
            SpriteRenderer back = Picture("Ring Back", root.transform, LoadGuid<Sprite>(RingBackGuid), 3);
            Fit(back, RingRadius * 2f);
            SpriteRenderer fill = Picture("Ring Fill", root.transform, LoadGuid<Sprite>(RingFillGuid), 4);
            fill.sharedMaterial = LoadGuid<Material>(RingFillMaterialGuid);
            Fit(fill, RingRadius * 2f);


            var figure = new GameObject("Figure").transform;
            figure.SetParent(root.transform, false);
            SpriteRenderer body = Picture("Body", figure, Load<Sprite>("Assets/ART/Thif/Cat/Cat.png"), 6);
            var items = new GameObject("Items").transform;
            items.SetParent(root.transform, false);
            items.localPosition = new Vector3(0f, 0.97f, 0f);
            SpriteRenderer backing = Picture("Backing", items, Load<Sprite>(Art + "Pill.png"), 7);
            backing.drawMode = SpriteDrawMode.Sliced;
            backing.transform.localScale = Vector3.one * 0.3f;
            backing.size = new Vector2(1.5f, 0.54f) / 0.3f;
            SpriteRenderer slotOne = Picture("Slot 1", items, Load<Sprite>(Art + "Cap.png"), 8);
            SpriteRenderer slotTwo = Picture("Slot 2", items, Load<Sprite>(Art + "Glasses.png"), 8);
            SpriteRenderer slotThree = Picture("Slot 3", items, Load<Sprite>(Art + "Bag.png"), 8);
            figure.localScale = Vector3.one * (1.35f / body.sprite.bounds.size.y);


            spotlight.gameObject.SetActive(false);

            var serialized = new SerializedObject(view);
            serialized.FindProperty("ringFill").objectReferenceValue = fill;
            serialized.FindProperty("figure").objectReferenceValue = figure;
            serialized.FindProperty("body").objectReferenceValue = body;
            serialized.FindProperty("items").objectReferenceValue = items;
            serialized.FindProperty("itemBacking").objectReferenceValue = backing;
            serialized.FindProperty("slotOne").objectReferenceValue = slotOne;
            serialized.FindProperty("slotTwo").objectReferenceValue = slotTwo;
            serialized.FindProperty("slotThree").objectReferenceValue = slotThree;
            serialized.FindProperty("spotlight").objectReferenceValue = spotlight;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, SuspectPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<LineupSuspectView>();
        }

        // Laid out in the wall canvas's units (1536 x 435).
        private static LineupWall BuildWall()
        {
            var root = new GameObject("Lineup Wall", typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>();
            Place(rect, Vector2.zero, WallSize);
            var wall = root.AddComponent<LineupWall>();

            Image background = UiImage("Background", rect, Load<Sprite>(WallBackgroundPath), Color.white);
            Stretch(background.rectTransform);
            Image shade = UiImage("Shade", rect, null, new Color(0.02f, 0.03f, 0.08f, 0.25f));
            Stretch(shade.rectTransform);

            TMP_Text title = UiText("Title", rect, "WHO IS THE BIG BOSS?", 62f, Gold, new Vector2(0f, 162f), new Vector2(1100f, 80f));
            TMP_Text line = UiText("Line", rect, "STAND ON A SUSPECT TO LOCK IN", 30f, Color.white, new Vector2(0f, 112f), new Vector2(1100f, 40f));

            Image witness = UiImage("Witness", rect, Load<Sprite>(WitnessPath), Color.white);
            witness.preserveAspect = true;
            Place(witness.rectTransform, new Vector2(-615f, -15f), new Vector2(300f, 300f));
            UiText("Witness Label", rect, "WITNESS", 28f, Gold, new Vector2(-615f, -185f), new Vector2(260f, 36f));

            var serialized = new SerializedObject(wall);
            SerializedProperty cards = serialized.FindProperty("cards");
            cards.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                RectTransform card = UiRect($"Clue {i + 1}", rect);
                Place(card, new Vector2((i - 1.5f) * 215f, -40f), new Vector2(190f, 230f));
                Image panel = UiImage("Panel", card, Load<Sprite>(Art + "Card.png"), new Color(0.98f, 0.93f, 0.8f, 1f));
                panel.type = Image.Type.Sliced;
                Stretch(panel.rectTransform);
                Image icon = UiImage("Icon", card, Load<Sprite>(Art + "Icon_Earring.png"), Color.white);
                icon.preserveAspect = true;
                Place(icon.rectTransform, new Vector2(0f, 22f), new Vector2(150f, 150f));
                Image badge = UiImage("Badge", card, Load<Sprite>(Art + "Badge_Yes.png"), Color.white);
                badge.preserveAspect = true;
                Place(badge.rectTransform, new Vector2(66f, 92f), new Vector2(70f, 70f));
                TMP_Text label = UiText("Label", card, "EARRING", 30f, Ink, new Vector2(0f, -86f), new Vector2(180f, 44f));
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = 30f;

                SerializedProperty entry = cards.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("root").objectReferenceValue = card;
                entry.FindPropertyRelative("icon").objectReferenceValue = icon;
                entry.FindPropertyRelative("badge").objectReferenceValue = badge;
                entry.FindPropertyRelative("label").objectReferenceValue = label;
            }

            RectTransform group = UiRect("Countdown", rect);
            Place(group, new Vector2(615f, -10f), new Vector2(260f, 260f));
            TMP_Text countdown = UiText("Seconds", group, "25", 120f, Gold, new Vector2(0f, 10f), new Vector2(260f, 140f));
            UiText("Label", group, "LOCK IN!", 30f, Color.white, new Vector2(0f, -100f), new Vector2(260f, 40f));

            AddCulpritPanel(rect, serialized);
            AddRewardPanel(rect, serialized);
            serialized.FindProperty("title").objectReferenceValue = title;
            serialized.FindProperty("line").objectReferenceValue = line;
            serialized.FindProperty("countdownGroup").objectReferenceValue = group.gameObject;
            serialized.FindProperty("countdown").objectReferenceValue = countdown;
            serialized.FindProperty("yes").objectReferenceValue = Load<Sprite>(Art + "Badge_Yes.png");
            serialized.FindProperty("no").objectReferenceValue = Load<Sprite>(Art + "Badge_No.png");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, WallPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<LineupWall>();
        }

        private static void WireScene(LineupSuspectView suspectPrefab, LineupWall wallPrefab)
        {
            var puzzle = Object.FindFirstObjectByType<LineupPuzzle>(FindObjectsInactive.Include);
            if (puzzle == null)
            {
                Debug.LogError("[LineupPrefabBuilder] No Lineup Puzzle in the open scene. Open Assets/Scenes/BountyHunter.unity (Don't Save) and run this again.");
                return;
            }

            // The wall used to be built by a LineupWall on the puzzle object itself.
            foreach (LineupWall old in puzzle.GetComponents<LineupWall>()) Object.DestroyImmediate(old, true);

            LineupWall wall = PlaceWall(wallPrefab);
            Set(puzzle, "wall", wall);
            var floor = puzzle.GetComponent<LineupFloor>();
            if (floor != null)
            {
                Set(floor, "suspectPrefab", suspectPrefab);
                Set(floor, "pickMark", Load<Sprite>(Art + "Badge_Yes.png"));
                Set(puzzle, "floor", floor);
            }

            ApplyCast(puzzle);
            HookUpAfterFirstMap(puzzle);
            EditorSceneManager.MarkSceneDirty(puzzle.gameObject.scene);
        }

        // The wall panel's instance in the scene: the one there already, or a new one over the wall.
        private static LineupWall PlaceWall(LineupWall prefab)
        {
            foreach (LineupWall existing in Object.FindObjectsByType<LineupWall>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing) == prefab) return existing;
            }

            Transform side = Find("SideDisplay");
            var instance = (LineupWall)PrefabUtility.InstantiatePrefab(prefab, side);
            Transform curtain = Find("Fade Curtain");
            if (curtain != null && curtain.parent == side) instance.transform.SetSiblingIndex(curtain.GetSiblingIndex());
            instance.gameObject.SetActive(false);
            return instance;
        }

        private static void HookUpAfterFirstMap(LineupPuzzle puzzle)
        {
            var maps = Object.FindFirstObjectByType<MapSwitcher>(FindObjectsInactive.Include);
            if (maps == null) return;

            var serialized = new SerializedObject(maps);
            SerializedProperty list = serialized.FindProperty("maps");
            if (list.arraySize > 1)
            {
                SerializedProperty after = list.GetArrayElementAtIndex(0).FindPropertyRelative("interludeAfter");
                if (after.objectReferenceValue == null) after.objectReferenceValue = puzzle;
            }

            SerializedProperty curtain = serialized.FindProperty("curtain");
            if (curtain.objectReferenceValue == null) curtain.objectReferenceValue = Object.FindFirstObjectByType<FadeCurtain>(FindObjectsInactive.Include);
            SerializedProperty hide = serialized.FindProperty("hideDuringInterlude");
            Transform bushes = Find("Bushes");
            if (hide.arraySize == 0 && bushes != null)
            {
                hide.arraySize = 1;
                hide.GetArrayElementAtIndex(0).objectReferenceValue = bushes.gameObject;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
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

        private static TextMeshPro FloorText(string name, Transform parent, string text, float size, Color color, int order)
        {
            var holder = new GameObject(name, typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            var label = holder.AddComponent<TextMeshPro>();
            label.font = LoadGuid<TMP_FontAsset>(FontGuid);
            label.fontSharedMaterial = LoadGuid<Material>(OutlinedFontGuid);
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(3f, 1f);
            label.renderer.sortingOrder = order;
            return label;
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
            label.font = LoadGuid<TMP_FontAsset>(FontGuid);
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
            if (asset == null) Debug.LogWarning($"[LineupPrefabBuilder] Missing {typeof(T).Name} at {path}");
            return asset;
        }

        private static T LoadGuid<T>(string guid) where T : Object
        {
            return Load<T>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
