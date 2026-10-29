using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves.EditorTools
{
    // Imports the boss cut-in's art in Assets/ART/Transition as sprites without mipmaps, at up to
    // 4096. Only first imports are set up; a change made by hand stays.
    public sealed class BossTransitionArtImport : AssetPostprocessor
    {
        public const string Folder = "Assets/ART/Transition/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder) || !assetImporter.importSettingsMissing) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096;
        }
    }

    // Builds Assets/Prefabs/Boss/Boss Transition.prefab from the art in Assets/ART/Transition/Boss Open
    // when it is missing, each time the editor loads, puts it over the wall just behind the fade
    // curtain and hands it to MapSwitcher, so the boss chase starts with the cut-in. A prefab still in
    // the first design (the rift, banners and doves) is remade in the rooftop design. Save the scene
    // afterwards.
    [InitializeOnLoad]
    public static class BossTransitionAutoBuild
    {
        static BossTransitionAutoBuild()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        private static void BuildIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Object.FindFirstObjectByType<MapSwitcher>(FindObjectsInactive.Include) == null) return;
            if (!BossTransitionBuilder.ArtIsThere()) return;

            BossTransitionBuilder.UseFullSizeWideArt();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BossTransitionBuilder.PrefabPath) == null) BossTransitionBuilder.Build();
            else BossTransitionBuilder.UseRooftopDesignIfOld();

            if (AssetDatabase.LoadAssetAtPath<GameObject>(BossTransitionBuilder.PrefabPath) == null) return;
            if (Object.FindFirstObjectByType<BossTransition>(FindObjectsInactive.Include) == null) BossTransitionBuilder.Wire();
        }
    }

    // Tools > Bounty Hunter > Build Boss Transition: makes the boss cut-in prefab (see BossTransition)
    // in the wall canvas's units and wires the open scene to it. Run it again to start the prefab over
    // (changes made in it by hand are lost); save the scene afterwards.
    public static class BossTransitionBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Boss/Boss Transition.prefab";
        private const string Art = BossTransitionArtImport.Folder + "Boss Open/";
        private const string Background = Art + "Boss_BG.png";
        private const string Streak = Art + "Boss_LineEffect.png";
        private const string Boss = Art + "Boss_Char.png";
        private const string Pigeon = Art + "Boss_Pigeon.png";
        private const string Feather = Art + "Boss_Feather.png";
        private const string Dot = Art + "Boss_Dot.png";

        // The boss in the middle of the wall, how wide its picture is (wall units).
        private static readonly Vector2 BossSpot = new Vector2(15f, 20f);
        private const float BossWidth = 800f;
        // The pigeons round the boss: where, how tall, and whether turned to face right (the picture
        // faces left).
        private static readonly (Vector2 At, float Size, bool FacesRight)[] Pigeons =
        {
            (new Vector2(-260f, -9f), 125f, true),
            (new Vector2(375f, 113f), 120f, false),
            (new Vector2(291f, -126f), 135f, false),
        };
        private const float FeatherHeight = 60f;
        private const float DotSize = 80f;
        private static readonly Color DotRed = new Color(0.95f, 0.04f, 0.08f, 1f);

        public static bool ArtIsThere()
        {
            foreach (string path in new[] { Background, Streak, Boss, Pigeon, Feather, Dot })
            {
                if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null) return false;
            }

            return true;
        }

        [MenuItem("Tools/Bounty Hunter/Build Boss Transition")]
        public static void Build()
        {
            if (!ArtIsThere())
            {
                Debug.LogError($"[BossTransitionBuilder] Needs Boss_BG, Boss_LineEffect, Boss_Char, Boss_Pigeon, Boss_Feather and Boss_Dot (as sprites) in {Art}");
                return;
            }

            BuildPrefab();
            Wire();
            Debug.Log("[BossTransitionBuilder] Boss Transition built and wired; save the scene to keep it.");
        }

        // The rooftop and the streak are drawn 8192 px wide; they are imported at full size so they
        // stay sharp across the wall.
        public static void UseFullSizeWideArt()
        {
            foreach (string path in new[] { Background, Streak })
            {
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) || importer.maxTextureSize >= 8192) continue;

                importer.maxTextureSize = 8192;
                importer.SaveAndReimport();
            }
        }

        // A prefab still in the first design (no rooftop) is emptied and filled in the rooftop design,
        // keeping its root and its BossTransition, so the scene's link to it holds.
        public static void UseRooftopDesignIfOld()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<BossTransition>(PrefabPath);
            if (prefab == null || prefab.IsSetUp) return;

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            var rect = (RectTransform)contents.transform;
            for (int i = rect.childCount - 1; i >= 0; i--) Object.DestroyImmediate(rect.GetChild(i).gameObject);
            Fill(rect, contents.GetComponent<BossTransition>());
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            Debug.Log("[BossTransitionBuilder] Boss Transition is now the rooftop cut-in: feathers, the streak wiping in, the boss and the pigeons.");
        }

        // Puts the prefab over the wall (once) and hands it to MapSwitcher.
        public static void Wire()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<BossTransition>(PrefabPath);
            var maps = Object.FindFirstObjectByType<MapSwitcher>(FindObjectsInactive.Include);
            if (prefab == null || maps == null) return;

            BossTransition placed = null;
            foreach (BossTransition existing in Object.FindObjectsByType<BossTransition>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing) == prefab) placed = existing;
            }

            if (placed == null)
            {
                Transform side = Find("SideDisplay");
                placed = (BossTransition)PrefabUtility.InstantiatePrefab(prefab, side);
                Undo.RegisterCreatedObjectUndo(placed.gameObject, "Wire Boss Transition");
                Transform curtain = Find("Fade Curtain");
                if (curtain != null && curtain.parent == side) placed.transform.SetSiblingIndex(curtain.GetSiblingIndex());
                placed.gameObject.SetActive(false);
            }

            var serialized = new SerializedObject(maps);
            serialized.FindProperty("bossTransition").objectReferenceValue = placed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(maps.gameObject.scene);
        }

        private static void BuildPrefab()
        {
            var root = new GameObject("Boss Transition", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            Place(rect, Vector2.zero, LineupPrefabBuilder.WallSize);
            Fill(rect, root.AddComponent<BossTransition>());

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        // Back to front: the rooftop filling the wall, the streak across it, the dots on the streak,
        // the boss, the pigeons, and the feathers falling over all of it.
        private static void Fill(RectTransform rect, BossTransition transition)
        {
            Vector2 wall = LineupPrefabBuilder.WallSize;
            Image background = UiImage("Rooftop", rect, Load<Sprite>(Background), Color.white);
            Place(background.rectTransform, Vector2.zero, wall);

            Sprite streakArt = Load<Sprite>(Streak);
            var streakSize = new Vector2(wall.x, streakArt != null ? wall.x * streakArt.rect.height / streakArt.rect.width : 300f);
            Image streak = UiImage("Line Effect", rect, streakArt, Color.white);
            Place(streak.rectTransform, Vector2.zero, streakSize);
            WipeFromRight(streak);

            RectTransform dotArea = UiRect("Dots", rect);
            Place(dotArea, Vector2.zero, streakSize);
            var dots = dotArea.gameObject.AddComponent<BossDotPops>();
            Image dot = UiImage("Dot", dotArea, Load<Sprite>(Dot), DotRed);
            Place(dot.rectTransform, Vector2.zero, Vector2.one * DotSize);
            dot.gameObject.SetActive(false);
            SetReference(dots, "dot", dot);

            Sprite bossArt = Load<Sprite>(Boss);
            Image boss = UiImage("Boss", rect, bossArt, Color.white);
            boss.preserveAspect = true;
            float bossHeight = bossArt != null ? BossWidth * bossArt.rect.height / bossArt.rect.width : BossWidth * 0.43f;
            Place(boss.rectTransform, BossSpot, new Vector2(BossWidth, bossHeight));

            var pigeons = new RectTransform[Pigeons.Length];
            for (int i = 0; i < Pigeons.Length; i++)
            {
                (Vector2 at, float size, bool facesRight) = Pigeons[i];
                Image pigeon = UiImage($"Pigeon {i + 1}", rect, Load<Sprite>(Pigeon), Color.white);
                pigeon.preserveAspect = true;
                Place(pigeon.rectTransform, at, Vector2.one * size);
                if (facesRight) pigeon.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                pigeons[i] = pigeon.rectTransform;
            }

            RectTransform featherArea = UiRect("Feathers", rect);
            Stretch(featherArea);
            var feathers = featherArea.gameObject.AddComponent<BossFeatherFall>();
            Sprite featherArt = Load<Sprite>(Feather);
            Image feather = UiImage("Feather", featherArea, featherArt, Color.white);
            feather.preserveAspect = true;
            float featherWidth = featherArt != null ? FeatherHeight * featherArt.rect.width / featherArt.rect.height : FeatherHeight;
            Place(feather.rectTransform, Vector2.zero, new Vector2(featherWidth, FeatherHeight));
            feather.gameObject.SetActive(false);
            SetReference(feathers, "feather", feather);

            var serialized = new SerializedObject(transition);
            serialized.FindProperty("background").objectReferenceValue = background;
            serialized.FindProperty("streak").objectReferenceValue = streak;
            serialized.FindProperty("boss").objectReferenceValue = boss.rectTransform;
            serialized.FindProperty("feathers").objectReferenceValue = feathers;
            serialized.FindProperty("dots").objectReferenceValue = dots;
            SetList(serialized.FindProperty("pigeons"), pigeons);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Filled from the right, empty to start with: the cut-in wipes it across by its fill.
        private static void WipeFromRight(Image picture)
        {
            picture.type = Image.Type.Filled;
            picture.fillMethod = Image.FillMethod.Horizontal;
            picture.fillOrigin = (int)Image.OriginHorizontal.Right;
            picture.fillAmount = 0f;
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetList<T>(SerializedProperty list, T[] items) where T : Object
        {
            list.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
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
            if (asset == null) Debug.LogWarning($"[BossTransitionBuilder] Missing {typeof(T).Name} at {path}");
            return asset;
        }
    }
}
