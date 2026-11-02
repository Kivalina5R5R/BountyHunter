using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves.EditorTools
{
    // Puts the boss chase's board (BossChaseBoard) into Assets/Prefabs/Boss/Boss Wall.prefab when it
    // is missing, each time the editor loads, laid out as in the designer's example (the Boss Chase
    // folder's screenshot): the dark brush board centred under the clock, BOSS CHASE! with the count
    // beside it in one row along its top, and three pairs of handcuffs centred on it, each grey until caught (Assets/ART/
    // Minigame/Boss/Chase). Then hands it to the scene's BossChase. Once built, the board is left
    // alone: move, resize or restyle it in the prefab. Save the scene afterwards.
    [InitializeOnLoad]
    public static class BossChaseBoardBuilder
    {
        private const string BossWallPath = "Assets/Prefabs/Boss/Boss Wall.prefab";
        private const string Art = "Assets/ART/Minigame/Boss/Chase/";
        private const string BoardArt = Art + "BossChaseBoard.png";
        private const string TitleArt = Art + "BossChaseTitle.png";
        private const string CaughtArt = Art + "HandcuffsCaught.png";
        private const string EmptyArt = Art + "HandcuffsEmpty.png";
        private const string StarPicture = "Assets/ART/StunGun/StunStar.png";
        private const string BoardName = "Chase Board";

        // In the wall's units, from its middle: the board, and on it (from the board's middle) the
        // title, the count and the three pairs of handcuffs.
        private static readonly Vector2 BoardSpot = new Vector2(0f, -2f);
        private static readonly Vector2 BoardSize = new Vector2(340f, 114f);
        private static readonly Vector2 TitleSpot = new Vector2(-40f, 46f);
        private static readonly Vector2 TitleSize = new Vector2(141f, 31f);
        private static readonly Vector2 CountSpot = new Vector2(72f, 46f);
        private static readonly Vector2 CountSize = new Vector2(90f, 56f);
        private static readonly float[] CuffsAcross = { -84f, 0f, 84f };
        private const float CuffsY = -6f;
        private static readonly Vector2 EmptySize = new Vector2(74f, 68f);
        private static readonly Vector2 CaughtSize = new Vector2(91f, 85f);
        private static readonly Color BoardTint = new Color(1f, 1f, 1f, 0.88f);
        private static readonly Color CountColour = new Color(1f, 0.8f, 0.15f, 1f);

        static BossChaseBoardBuilder()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        private static void BuildIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(BoardArt) == null) return;

            foreach (string path in new[] { BoardArt, TitleArt, CaughtArt, EmptyArt }) ImportAsSprite(path);
            AddToBossWall();
            Wire();
        }

        private static void AddToBossWall()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BossWallPath) == null) return;

            GameObject contents = PrefabUtility.LoadPrefabContents(BossWallPath);
            try
            {
                if (contents.GetComponentInChildren<BossChaseBoard>(true) != null) return;

                Build((RectTransform)contents.transform);
                PrefabUtility.SaveAsPrefabAsset(contents, BossWallPath);
                Debug.Log("[BossChaseBoardBuilder] The Boss Wall has its chase board now.", AssetDatabase.LoadAssetAtPath<GameObject>(BossWallPath));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void Build(RectTransform wall)
        {
            RectTransform holder = UiRect(BoardName, wall);
            Stretch(holder);
            holder.SetAsLastSibling();
            var chaseBoard = holder.gameObject.AddComponent<BossChaseBoard>();

            RectTransform board = UiRect("Board", holder);
            Place(board, BoardSpot, BoardSize);
            Image back = Picture("Back", board, BoardArt, BoardTint);
            Stretch(back.rectTransform);
            Image title = Picture("Title", board, TitleArt, Color.white);
            Place(title.rectTransform, TitleSpot, TitleSize);

            RectTransform countRect = UiRect("Count", board);
            Place(countRect, CountSpot, CountSize);
            var count = countRect.gameObject.AddComponent<TextMeshProUGUI>();
            count.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(LineupPrefabBuilder.FontGuid));
            var outlined = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(LineupPrefabBuilder.OutlinedFontGuid));
            if (outlined != null) count.fontSharedMaterial = outlined;
            count.text = "0/3";
            count.fontSize = 52f;
            count.color = CountColour;
            count.alignment = TextAlignmentOptions.Center;
            count.textWrappingMode = TextWrappingModes.NoWrap;
            count.raycastTarget = false;

            var empty = new RectTransform[CuffsAcross.Length];
            var caught = new RectTransform[CuffsAcross.Length];
            for (int i = 0; i < CuffsAcross.Length; i++)
            {
                var spot = new Vector2(CuffsAcross[i], CuffsY);
                Image emptyCuffs = Picture($"Handcuffs {i + 1} Empty", board, EmptyArt, Color.white);
                Place(emptyCuffs.rectTransform, spot, EmptySize);
                Image caughtCuffs = Picture($"Handcuffs {i + 1} Caught", board, CaughtArt, Color.white);
                Place(caughtCuffs.rectTransform, spot, CaughtSize);
                caughtCuffs.gameObject.SetActive(false);
                empty[i] = emptyCuffs.rectTransform;
                caught[i] = caughtCuffs.rectTransform;
            }

            RectTransform sparkles = UiRect("Sparkles", board);
            Stretch(sparkles);
            var particles = sparkles.gameObject.AddComponent<CutInParticles>();
            var particleFields = new SerializedObject(particles);
            particleFields.FindProperty("star").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(StarPicture);
            particleFields.ApplyModifiedPropertiesWithoutUndo();

            var fields = new SerializedObject(chaseBoard);
            fields.FindProperty("board").objectReferenceValue = board;
            fields.FindProperty("count").objectReferenceValue = count;
            fields.FindProperty("particles").objectReferenceValue = particles;
            SetList(fields.FindProperty("caughtCuffs"), caught);
            SetList(fields.FindProperty("emptyCuffs"), empty);
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        // The scene's BossChase shows its catches on the board, unless it has one already.
        private static void Wire()
        {
            var chase = Object.FindFirstObjectByType<BossChase>(FindObjectsInactive.Include);
            var board = Object.FindFirstObjectByType<BossChaseBoard>(FindObjectsInactive.Include);
            if (chase == null || board == null) return;

            var fields = new SerializedObject(chase);
            SerializedProperty property = fields.FindProperty("board");
            if (property == null || property.objectReferenceValue != null) return;

            property.objectReferenceValue = board;
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(chase.gameObject.scene);
            Debug.Log("[BossChaseBoardBuilder] The boss chase shows its catches on the board; save the scene to keep it", board);
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

        private static Image Picture(string name, Transform parent, string path, Color tint)
        {
            var image = UiRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            image.color = tint;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static void SetList(SerializedProperty list, RectTransform[] items)
        {
            list.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        private static RectTransform UiRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
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
    }
}
