using TMPro;
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

    // Builds Assets/Prefabs/Boss/Boss Transition.prefab from the art in Assets/ART/Transition when it
    // is missing, each time the editor loads, puts it over the wall just behind the fade curtain and
    // hands it to MapSwitcher, so the boss chase starts with the cut-in. Save the scene afterwards.
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

            if (AssetDatabase.LoadAssetAtPath<GameObject>(BossTransitionBuilder.PrefabPath) == null) BossTransitionBuilder.Build();
            else
            {
                BossTransitionBuilder.LengthenHold();
                BossTransitionBuilder.UseDoves();
            }

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
        private const string Art = BossTransitionArtImport.Folder;
        private const string Rift = Art + "Boss Open/Transition_Rift.png";
        private const string Inside = Art + "Boss Open/Transition_Inside.png";
        private const string Banner = Art + "Boss Open/Transition_Banner.png";
        private const string Dove = Art + "Boss Open/Transition_Dove.png";
        private const string Ink = Art + "Boss Open/Transition_Ink.png";
        private const string Emblem = Art + "Boss Open/Boss_Icon_Transition.png";
        private const string Glow = "Assets/ART/Minigame/Lineup/Spotlight.png";

        private const float BannerHeight = 150f;
        private const int BannerTiles = 4;

        // Where the ink splats land over the stage (wall units), their size and turn.
        private static readonly (Vector2 At, float Size, float Turn)[] Splats =
        {
            (new Vector2(-560f, 95f), 190f, 20f),
            (new Vector2(-400f, -120f), 150f, -35f),
            (new Vector2(390f, 125f), 170f, 70f),
            (new Vector2(570f, -90f), 200f, -10f),
            (new Vector2(-140f, 150f), 120f, 140f),
        };

        // The two banners crossing: turn (degrees), height above the middle, and their words (none
        // leaves the banner's own row of top hats and card suits showing).
        private static readonly (float Turn, float Y, string Words)[] Banners =
        {
            (-13f, 45f, "FINAL BOSS          FINAL BOSS          FINAL BOSS          FINAL BOSS"),
            (10f, -55f, ""),
        };
        private static readonly Color WordsStrip = new Color(0.42f, 0.02f, 0.06f, 1f);

        public static bool ArtIsThere()
        {
            foreach (string path in new[] { Rift, Inside, Banner, Dove, Ink, Emblem })
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
                Debug.LogError($"[BossTransitionBuilder] Needs Transition_Rift, _Inside, _Banner, _Dove, _Ink and Boss_Icon_Transition (as sprites) in {Art}Boss Open/");
                return;
            }

            BuildPrefab();
            Wire();
            Debug.Log("[BossTransitionBuilder] Boss Transition built and wired; save the scene to keep it.");
        }

        // A prefab made with the first hold (1.56 s, the reference's) holds a second longer; any other
        // hold set by hand is kept.
        public static void LengthenHold()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            var serialized = new SerializedObject(contents.GetComponent<BossTransition>());
            SerializedProperty hold = serialized.FindProperty("holdSeconds");
            if (Mathf.Approximately(hold.floatValue, 1.56f))
            {
                hold.floatValue = 2.56f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log("[BossTransitionBuilder] The boss cut-in now holds 2.56 s.");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        // A prefab made with white gloves round the emblem gets white doves there instead, keeping
        // where each sits and everything else in it.
        public static void UseDoves()
        {
            var dove = AssetDatabase.LoadAssetAtPath<Sprite>(Dove);
            if (dove == null) return;

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            bool changed = false;
            foreach (Image image in contents.GetComponentsInChildren<Image>(true))
            {
                if (!image.name.StartsWith("Glove ")) continue;

                image.sprite = dove;
                image.name = "Dove " + image.name.Substring("Glove ".Length);
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log("[BossTransitionBuilder] White doves now fly out round the boss's emblem instead of gloves.");
            }

            PrefabUtility.UnloadPrefabContents(contents);
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

        // Back to front: ink splats, the rift, the world inside it, the banners, the glow, the doves,
        // the emblem, the flash.
        private static void BuildPrefab()
        {
            var root = new GameObject("Boss Transition", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            Place(rect, Vector2.zero, LineupPrefabBuilder.WallSize);
            var transition = root.AddComponent<BossTransition>();
            RectTransform shaker = UiRect("Shaker", rect);
            Stretch(shaker);

            var inks = new Image[Splats.Length];
            for (int i = 0; i < Splats.Length; i++)
            {
                inks[i] = UiImage($"Ink {i + 1}", shaker, Load<Sprite>(Ink), Color.white);
                inks[i].preserveAspect = true;
                Place(inks[i].rectTransform, Splats[i].At, Vector2.one * Splats[i].Size);
                inks[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, Splats[i].Turn);
            }

            Image rift = UiImage("Rift", shaker, Load<Sprite>(Rift), Color.white);
            rift.preserveAspect = true;
            Place(rift.rectTransform, Vector2.zero, new Vector2(1150f, 420f));
            rift.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f);

            Image inside = UiImage("Inside", shaker, Load<Sprite>(Inside), Color.white);
            Stretch(inside.rectTransform);
            // Fills the wall without stretching, cropping what does not fit.
            var fitter = inside.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = inside.sprite.rect.width / inside.sprite.rect.height;

            var banners = new RectTransform[Banners.Length];
            for (int i = 0; i < Banners.Length; i++) banners[i] = MakeBanner(shaker, i);

            Image glow = UiImage("Emblem Glow", shaker, Load<Sprite>(Glow), new Color(1f, 0.85f, 0.9f, 1f));
            Place(glow.rectTransform, new Vector2(0f, 5f), new Vector2(620f, 620f));

            var doves = new RectTransform[6];
            for (int i = 0; i < doves.Length; i++)
            {
                // Round the emblem, an oval as wide as the wall allows, each dove flying outward.
                float angle = 25f + i * 60f;
                float radians = angle * Mathf.Deg2Rad;
                Image dove = UiImage($"Dove {i + 1}", shaker, Load<Sprite>(Dove), Color.white);
                dove.preserveAspect = true;
                Place(dove.rectTransform, new Vector2(Mathf.Cos(radians) * 330f, Mathf.Sin(radians) * 135f), new Vector2(140f, 140f));
                dove.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                doves[i] = dove.rectTransform;
            }

            Image emblem = UiImage("Emblem", shaker, Load<Sprite>(Emblem), Color.white);
            emblem.preserveAspect = true;
            Place(emblem.rectTransform, new Vector2(0f, 5f), new Vector2(400f, 400f));

            Image flash = UiImage("Flash", shaker, null, Color.white);
            Stretch(flash.rectTransform);

            var serialized = new SerializedObject(transition);
            serialized.FindProperty("shaker").objectReferenceValue = shaker;
            serialized.FindProperty("rift").objectReferenceValue = rift;
            serialized.FindProperty("inside").objectReferenceValue = inside;
            serialized.FindProperty("flash").objectReferenceValue = flash;
            serialized.FindProperty("emblem").objectReferenceValue = emblem.rectTransform;
            serialized.FindProperty("emblemGlow").objectReferenceValue = glow;
            SetList(serialized.FindProperty("inks"), inks);
            SetList(serialized.FindProperty("banners"), banners);
            SetList(serialized.FindProperty("doves"), doves);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        // A long banner across the wall: the banner art side by side, its words along it.
        private static RectTransform MakeBanner(Transform parent, int index)
        {
            (float turn, float y, string words) = Banners[index];
            Sprite art = Load<Sprite>(Banner);
            float tileWidth = BannerHeight * art.rect.width / art.rect.height;
            RectTransform banner = UiRect($"Banner {index + 1}", parent);
            Place(banner, new Vector2(0f, y), new Vector2(tileWidth * BannerTiles, BannerHeight));
            banner.localRotation = Quaternion.Euler(0f, 0f, turn);
            for (int tile = 0; tile < BannerTiles; tile++)
            {
                Image piece = UiImage($"Tile {tile + 1}", banner, art, Color.white);
                Place(piece.rectTransform, new Vector2((tile - (BannerTiles - 1) * 0.5f) * tileWidth, 0f), new Vector2(tileWidth, BannerHeight));
            }

            if (string.IsNullOrEmpty(words)) return banner;

            // The words go on a dark strip over the banner's row of icons, so they read clearly.
            Image strip = UiImage("Words Strip", banner, null, WordsStrip);
            Place(strip.rectTransform, Vector2.zero, new Vector2(tileWidth * BannerTiles, BannerHeight * 0.42f));
            RectTransform holder = UiRect("Words", banner);
            Place(holder, Vector2.zero, new Vector2(tileWidth * BannerTiles, BannerHeight * 0.6f));
            var text = holder.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = LoadGuid<TMP_FontAsset>(LineupPrefabBuilder.FontGuid);
            text.fontSharedMaterial = LoadGuid<Material>(LineupPrefabBuilder.OutlinedFontGuid);
            text.text = words;
            text.fontSize = 56f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return banner;
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

        private static T LoadGuid<T>(string guid) where T : Object
        {
            return Load<T>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
