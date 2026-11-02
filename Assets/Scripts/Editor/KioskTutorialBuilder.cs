using Fortal.Display;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Fortal.Editor
{
    // Builds the HOW TO PLAY tutorial: the board is shown on the wall and worked from the kiosk.
    // - Kiosk prefab (Assets/Prefabs/Display/KioskDisplay): the remote (TutorialRemote) on the dimmed
    //   kiosk, on the exit question's board (KioskConfirmPanel), with LOOK AT THE BIG SCREEN, the page
    //   counter, and BACK (blue, the tutorial button's art) beside NEXT (red, the exit button's art);
    //   the TUTORIAL button gets a TutorialButton. A kiosk-sized board from before the tutorial moved
    //   to the wall is taken out.
    // - The open scene: a world-space canvas over the wall's (SideDisplay), drawn over everything on
    //   the wall, holding the wide police-tape board (WallTutorialPanel,
    //   Tools/KioskTutorial/make_tutorial_art.py) with HOW TO PLAY on a plaque over the tape, the
    //   sliding page (sunburst and picture on the left, step, title and words on the right) and the
    //   page dots; wired to the kiosk's remote and TUTORIAL button.
    //   The scene is marked changed and left for you to save.
    // Runs whenever the editor loads and a piece is missing; once built, each is left alone: change
    // the pages, move or restyle them in the prefab and the scene.
    [InitializeOnLoad]
    public static class KioskTutorialBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Display/KioskDisplay.prefab";
        private const string KioskArt = "Assets/ART/UI/Kiosk/";
        private const string WallPanelPath = KioskArt + "WallTutorialPanel.png";
        private const string RemotePanelPath = KioskArt + "KioskConfirmPanel.png";
        private const string BurstPath = KioskArt + "KioskTutorialBurst.png";
        private const string NextArtPath = KioskArt + "BT_Exit.png";
        private const string BackArtPath = KioskArt + "BT-Tutorial.png";
        private const string OldKioskBoardName = "Tutorial";
        private const string RemoteName = "Tutorial Remote";
        private const string OpenButtonName = "TutorialButton";
        private const string WallCanvasName = "SideDisplay";
        private const string WallTutorialName = "Wall Tutorial";
        // Only the first wall layout had this.
        private const string FirstLayoutMark = "Tutorial/Board/Time Bar";
        // Drawn over everything on the wall (its pictures go up to about 20).
        private const int WallSortingOrder = 500;
        // The kiosk buttons' font and its outlined material.
        private const string FontGuid = "d4e98a79a56adb245b3f28d26d672faf";
        private const string FontMaterialGuid = "ba7aada5ab4287b478f894e8210a4181";

        private static readonly Vector2 RemoteBoardSize = new Vector2(900f, 680f);
        private static readonly Vector2 RemoteButtonSize = new Vector2(370f, 95f);
        // The wall canvas is 1536 x 435.
        private static readonly Vector2 WallBoardSize = new Vector2(1200f, 420f);
        private static readonly Color Dim = new Color(0.02f, 0.03f, 0.08f, 0.8f);
        private static readonly Color HeaderColour = new Color(1f, 0.8f, 0.13f, 1f);
        private static readonly Color MessageColour = new Color(0.82f, 0.86f, 1f, 1f);
        private static readonly Color BurstColour = new Color(0.55f, 0.65f, 1f, 0.45f);
        private static readonly Color PlaqueColour = new Color(0.07f, 0.08f, 0.2f, 1f);
        private static readonly Color DividerColour = new Color(1f, 0.8f, 0.13f, 0.35f);

        // Each page: its picture, title and words.
        private static readonly (string picture, string title, string body)[] Pages =
        {
            (KioskArt + "KioskRex.png", "PICK YOUR DOG",
                "PUT ON YOUR TAG AND STEP ONTO THE FLOOR.\nSTAND ON A DOG CARD FOR 2 SECONDS TO PICK IT!"),
            ("Assets/ART/Post/Postcat.png", "WANTED!",
                "THE WALL SHOWS WHO TO CATCH\nAND HOW MUCH THEIR BOUNTY IS."),
            ("Assets/ART/Thif/Cat/Cat.png", "CATCH THE THIEF",
                "RUN INTO A THIEF TO CATCH IT AND TAKE ITS BOUNTY.\nIT HIDES IN BINS AND BUSHES: STAND GUARD TO FLUSH IT OUT!"),
            ("Assets/ART/FX/Bomb/Frames/Bomb_Fuse_01.png", "WATCH OUT!",
                "GET OUT OF THE RED RING BEFORE THE BOMB GOES OFF.\nBUMPING INTO A PASSER-BY COSTS YOU 5$!"),
            ("Assets/ART/StunGun/StunBoltIcon.png", "STUN GUN",
                "GRAB A STUN GUN AND STAY CLOSE TO A THIEF.\nIT LOCKS ON AND FIRES BY ITSELF!"),
            ("Assets/ART/Minigame/Cctv/Bin1.png", "MINI GAMES",
                "BETWEEN STAGES, STAND ON THE RING OF YOUR ANSWER.\nGET IT RIGHT FOR BONUS CASH!"),
            ("Assets/ART/Minigame/Boss/BossHat.png", "MOST WANTED",
                "CATCH THE BOSS IN THE FINAL CHASE.\nTHE MOST CASH AT THE END WINS!"),
        };

        static KioskTutorialBuilder()
        {
            EditorApplication.delayCall += Build;
        }

        private static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            BuildRemote();
            // The scene's kiosk picks up the saved prefab first.
            EditorApplication.delayCall += BuildWall;
        }

        private static void BuildRemote()
        {
            Sprite panel = UiSprite(RemotePanelPath);
            var nextArt = AssetDatabase.LoadAssetAtPath<Sprite>(NextArtPath);
            var backArt = AssetDatabase.LoadAssetAtPath<Sprite>(BackArtPath);
            if (panel == null || nextArt == null || backArt == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform canvas = root.transform.Find("KioskCanvas");
                Button openButton = FindButton(root, OpenButtonName);
                if (canvas == null || openButton == null) return;

                bool changed = RemoveOldKioskBoard(canvas);
                if (openButton.GetComponent<TutorialButton>() == null)
                {
                    openButton.gameObject.AddComponent<TutorialButton>();
                    changed = true;
                }

                if (canvas.Find(RemoteName) == null)
                {
                    CreateRemote(canvas, panel, nextArt, backArt);
                    changed = true;
                }

                if (!changed) return;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[KioskTutorialBuilder] Added the tutorial remote to the kiosk", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The kiosk-sized board this builder made before the tutorial moved to the wall.
        private static bool RemoveOldKioskBoard(Transform canvas)
        {
            Transform old = canvas.Find(OldKioskBoardName);
            if (old == null || old.GetComponent<TutorialPopup>() == null) return false;
            Object.DestroyImmediate(old.gameObject);
            return true;
        }

        private static void BuildWall()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Scene scene = SceneManager.GetActiveScene();
            Canvas wall = FindInScene<Canvas>(scene, WallCanvasName);
            TutorialButton opener = FindInScene<TutorialButton>(scene, OpenButtonName);
            TutorialRemote remote = FindInScene<TutorialRemote>(scene, RemoteName);
            if (wall == null || opener == null || remote == null || wall.transform.parent == null) return;

            Transform existing = wall.transform.parent.Find(WallTutorialName);
            if (existing != null && existing.Find(FirstLayoutMark) == null)
            {
                FillMissingPictures(existing, scene);
                return;
            }

            // The first layout (with a time bar) is replaced by the current one.
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            Sprite panel = UiSprite(WallPanelPath);
            Sprite burst = UiSprite(BurstPath);
            if (panel == null || burst == null) return;

            TutorialPopup popup = CreateWallTutorial(wall, panel, burst, remote);
            var wiring = new SerializedObject(opener);
            wiring.FindProperty("popup").objectReferenceValue = popup;
            wiring.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[KioskTutorialBuilder] Added the HOW TO PLAY board to the wall in {scene.name}: save the scene to keep it", popup);
        }

        // A page whose picture has gone (its file was deleted) gets this builder's picture for it back.
        private static void FillMissingPictures(Transform wallTutorial, Scene scene)
        {
            var popup = wallTutorial.GetComponentInChildren<TutorialPopup>(true);
            if (popup == null) return;

            var serialized = new SerializedObject(popup);
            SerializedProperty pages = serialized.FindProperty("pages");
            bool changed = false;
            for (int i = 0; i < pages.arraySize && i < Pages.Length; i++)
            {
                SerializedProperty picture = pages.GetArrayElementAtIndex(i).FindPropertyRelative("picture");
                if (picture.objectReferenceValue != null) continue;
                picture.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Pages[i].picture);
                changed |= picture.objectReferenceValue != null;
            }

            if (!changed) return;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[KioskTutorialBuilder] Put back the wall tutorial's missing pictures: save the scene to keep them", popup);
        }

        private static T FindInScene<T>(Scene scene, string name) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;

            foreach (GameObject top in scene.GetRootGameObjects())
            {
                foreach (T found in top.GetComponentsInChildren<T>(true))
                {
                    if (found.name == name) return found;
                }
            }

            return null;
        }

        private static Button FindButton(GameObject root, string name)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.name == name) return button;
            }

            return null;
        }

        // The picture, imported as a UI sprite the first time.
        private static Sprite UiSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return null;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void CreateRemote(Transform canvas, Sprite panel, Sprite nextArt, Sprite backArt)
        {
            RectTransform remoteRect = Rect(RemoteName, canvas);
            Stretch(remoteRect);
            var group = remoteRect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            RectTransform backdropRect = Rect("Backdrop", remoteRect);
            Stretch(backdropRect);
            var backdropImage = backdropRect.gameObject.AddComponent<Image>();
            backdropImage.color = Dim;
            var backdrop = backdropRect.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;

            RectTransform board = Rect("Board", remoteRect);
            board.sizeDelta = RemoteBoardSize;
            board.anchoredPosition = new Vector2(0f, 40f);
            var boardImage = board.gameObject.AddComponent<Image>();
            boardImage.sprite = panel;
            AddShadow(board.gameObject);

            Label("Title", board, "HOW TO PLAY", 104, Color.white, new Vector2(0f, 105f), new Vector2(820f, 120f));
            Label("Message", board, "LOOK AT THE BIG SCREEN!", 50, MessageColour, new Vector2(0f, 10f), new Vector2(820f, 70f));
            RectTransform counter = Label("Counter", board, "PAGE 1 / 7", 44, HeaderColour, new Vector2(0f, -65f), new Vector2(820f, 60f));
            (Button back, TMP_Text backLabel) = BoardButton("BackButton", board, backArt, "CLOSE", new Vector2(-200f, -205f));
            (Button next, TMP_Text nextLabel) = BoardButton("NextButton", board, nextArt, "NEXT", new Vector2(200f, -205f));

            var remote = remoteRect.gameObject.AddComponent<TutorialRemote>();
            var serialized = new SerializedObject(remote);
            serialized.FindProperty("board").objectReferenceValue = board;
            serialized.FindProperty("counter").objectReferenceValue = counter.GetComponent<TMP_Text>();
            serialized.FindProperty("backButton").objectReferenceValue = back;
            serialized.FindProperty("backLabel").objectReferenceValue = backLabel;
            serialized.FindProperty("nextButton").objectReferenceValue = next;
            serialized.FindProperty("nextLabel").objectReferenceValue = nextLabel;
            serialized.FindProperty("backdrop").objectReferenceValue = backdrop;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            remoteRect.gameObject.SetActive(false);
        }

        // A world-space canvas just in front of the wall's, the same size, drawn over it.
        private static TutorialPopup CreateWallTutorial(Canvas wall, Sprite panel, Sprite burstArt, TutorialRemote remote)
        {
            var wallRect = (RectTransform)wall.transform;
            var canvasObject = new GameObject(WallTutorialName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.layer = wall.gameObject.layer;
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.SetParent(wallRect.parent, false);
            canvasRect.SetSiblingIndex(wallRect.GetSiblingIndex() + 1);
            canvasRect.anchorMin = wallRect.anchorMin;
            canvasRect.anchorMax = wallRect.anchorMax;
            canvasRect.pivot = wallRect.pivot;
            canvasRect.sizeDelta = wallRect.sizeDelta;
            canvasRect.anchoredPosition = wallRect.anchoredPosition;
            canvasRect.localScale = wallRect.localScale;
            canvasRect.localPosition = wallRect.localPosition + new Vector3(0f, 0f, -0.5f);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = wall.worldCamera;
            canvas.sortingLayerID = wall.sortingLayerID;
            canvas.sortingOrder = WallSortingOrder;
            canvasObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 16f;

            RectTransform popupRect = Rect("Tutorial", canvasRect);
            Stretch(popupRect);
            var group = popupRect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            RectTransform dimRect = Rect("Dim", popupRect);
            Stretch(dimRect);
            var dim = dimRect.gameObject.AddComponent<Image>();
            dim.color = Dim;
            dim.raycastTarget = false;

            RectTransform board = Rect("Board", popupRect);
            board.sizeDelta = WallBoardSize;
            var boardImage = board.gameObject.AddComponent<Image>();
            boardImage.sprite = panel;
            boardImage.raycastTarget = false;
            AddShadow(board.gameObject, new Vector2(4f, -5f));

            // The header sits on a plaque over the police tape.
            RectTransform plaque = Rect("Header Plaque", board);
            plaque.sizeDelta = new Vector2(340f, 64f);
            plaque.anchoredPosition = new Vector2(0f, 147f);
            var plaqueImage = plaque.gameObject.AddComponent<Image>();
            plaqueImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            plaqueImage.type = Image.Type.Sliced;
            plaqueImage.color = PlaqueColour;
            plaqueImage.raycastTarget = false;
            var plaqueRim = plaque.gameObject.AddComponent<Outline>();
            plaqueRim.effectColor = HeaderColour;
            plaqueRim.effectDistance = new Vector2(3f, -3f);
            RectTransform header = Label("Header", plaque, "HOW TO PLAY", 42, HeaderColour, Vector2.zero, Vector2.zero);
            Stretch(header);

            // Left: the picture; right of a thin line: the words. The page clips to the board's face
            // under the tape so it can slide in and out of view.
            RectTransform divider = Rect("Divider", board);
            divider.sizeDelta = new Vector2(3f, 220f);
            divider.anchoredPosition = new Vector2(-110f, -30f);
            var dividerImage = divider.gameObject.AddComponent<Image>();
            dividerImage.color = DividerColour;
            dividerImage.raycastTarget = false;

            RectTransform viewport = Rect("Page View", board);
            viewport.sizeDelta = new Vector2(1150f, 300f);
            viewport.anchoredPosition = new Vector2(0f, -30f);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform page = Rect("Page", viewport);
            Stretch(page);
            var pageGroup = page.gameObject.AddComponent<CanvasGroup>();

            RectTransform burst = Rect("Burst", page);
            burst.sizeDelta = new Vector2(400f, 400f);
            burst.anchoredPosition = new Vector2(-340f, 0f);
            var burstImage = burst.gameObject.AddComponent<Image>();
            burstImage.sprite = burstArt;
            burstImage.color = BurstColour;
            burstImage.raycastTarget = false;

            RectTransform pictureRect = Rect("Picture", page);
            pictureRect.sizeDelta = new Vector2(270f, 270f);
            pictureRect.anchoredPosition = new Vector2(-340f, 0f);
            var picture = pictureRect.gameObject.AddComponent<Image>();
            picture.preserveAspect = true;
            picture.raycastTarget = false;
            AddShadow(pictureRect.gameObject, new Vector2(4f, -5f));

            RectTransform step = Label("Step", page, "", 30, HeaderColour, new Vector2(230f, 88f), new Vector2(640f, 40f));
            RectTransform title = Label("Title", page, "", 78, Color.white, new Vector2(230f, 32f), new Vector2(640f, 84f));
            title.GetComponent<TextMeshProUGUI>().characterSpacing = 2f;
            RectTransform body = Label("Body", page, "", 36, MessageColour, new Vector2(230f, -62f), new Vector2(640f, 110f));
            // Longer words shrink to fit rather than spill out of the board.
            var bodyText = body.GetComponent<TextMeshProUGUI>();
            bodyText.enableAutoSizing = true;
            bodyText.fontSizeMin = 24f;
            bodyText.fontSizeMax = 36f;
            bodyText.characterSpacing = 1f;
            bodyText.lineSpacing = 8f;

            RectTransform dotRow = Rect("Dots", board);
            dotRow.sizeDelta = new Vector2(400f, 20f);
            dotRow.anchoredPosition = new Vector2(230f, -152f);
            var layout = dotRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            RectTransform dot = Rect("Dot", dotRow);
            dot.sizeDelta = new Vector2(12f, 12f);
            var dotImage = dot.gameObject.AddComponent<Image>();
            dotImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            dotImage.raycastTarget = false;

            var popup = popupRect.gameObject.AddComponent<TutorialPopup>();
            var serialized = new SerializedObject(popup);
            serialized.FindProperty("board").objectReferenceValue = board;
            serialized.FindProperty("pageRoot").objectReferenceValue = page;
            serialized.FindProperty("pageGroup").objectReferenceValue = pageGroup;
            serialized.FindProperty("picture").objectReferenceValue = picture;
            serialized.FindProperty("burst").objectReferenceValue = burst;
            serialized.FindProperty("title").objectReferenceValue = title.GetComponent<TMP_Text>();
            serialized.FindProperty("body").objectReferenceValue = bodyText;
            serialized.FindProperty("step").objectReferenceValue = step.GetComponent<TMP_Text>();
            serialized.FindProperty("dotTemplate").objectReferenceValue = dotImage;
            serialized.FindProperty("remote").objectReferenceValue = remote;
            FillPages(serialized.FindProperty("pages"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            popupRect.gameObject.SetActive(false);
            return popup;
        }

        private static void FillPages(SerializedProperty pages)
        {
            pages.arraySize = Pages.Length;
            for (int i = 0; i < Pages.Length; i++)
            {
                SerializedProperty page = pages.GetArrayElementAtIndex(i);
                page.FindPropertyRelative("picture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Pages[i].picture);
                page.FindPropertyRelative("title").stringValue = Pages[i].title;
                page.FindPropertyRelative("body").stringValue = Pages[i].body;
            }
        }

        private static (Button, TMP_Text) BoardButton(string name, RectTransform board, Sprite art, string word, Vector2 position)
        {
            RectTransform rect = Rect(name, board);
            rect.sizeDelta = RemoteButtonSize;
            rect.anchoredPosition = position;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = art;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            AddShadow(rect.gameObject);

            RectTransform label = Label("Label", rect, word, 60, Color.white, new Vector2(0f, -3f), Vector2.zero);
            Stretch(label);
            label.anchoredPosition = new Vector2(0f, -3f);
            return (button, label.GetComponent<TMP_Text>());
        }

        private static RectTransform Label(string name, RectTransform parent, string words, float size, Color colour, Vector2 position, Vector2 box)
        {
            RectTransform rect = Rect(name, parent);
            rect.sizeDelta = box;
            rect.anchoredPosition = position;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(FontMaterialGuid));
            if (material != null) text.fontSharedMaterial = material;
            text.text = words;
            text.fontSize = size;
            text.color = colour;
            text.characterSpacing = 4f;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return rect;
        }

        // The kiosk buttons' drop shadow.
        private static void AddShadow(GameObject target)
        {
            AddShadow(target, new Vector2(8f, -10f));
        }

        private static void AddShadow(GameObject target, Vector2 distance)
        {
            var shadow = target.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = distance;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
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
