using Fortal.Display;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Fortal.Editor
{
    // Builds the "exit the game?" dialog into the kiosk prefab (Assets/Prefabs/Display/KioskDisplay)
    // the first time the editor loads without it, and points the EXIT button at it: the dimmed screen,
    // the police-tape board (KioskConfirmPanel, Tools/KioskConfirm/make_confirm_panel.py) with its
    // question, and EXIT (red, the exit button's art) beside CANCEL (blue, the tutorial button's art),
    // in the kiosk buttons' font. Once built it is left alone: move or restyle it in the prefab.
    [InitializeOnLoad]
    public static class KioskExitConfirmBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Display/KioskDisplay.prefab";
        private const string PanelPath = "Assets/ART/UI/Kiosk/KioskConfirmPanel.png";
        private const string ConfirmArtPath = "Assets/ART/UI/Kiosk/BT_Exit.png";
        private const string CancelArtPath = "Assets/ART/UI/Kiosk/BT-Tutorial.png";
        private const string DialogName = "Exit Confirm";
        // The kiosk buttons' font and its outlined material.
        private const string FontGuid = "d4e98a79a56adb245b3f28d26d672faf";
        private const string FontMaterialGuid = "ba7aada5ab4287b478f894e8210a4181";

        private static readonly Vector2 BoardSize = new Vector2(900f, 680f);
        private static readonly Vector2 ButtonSize = new Vector2(370f, 95f);
        private static readonly Color Dim = new Color(0.02f, 0.03f, 0.08f, 0.78f);
        private static readonly Color MessageColour = new Color(0.82f, 0.86f, 1f, 1f);

        static KioskExitConfirmBuilder()
        {
            EditorApplication.delayCall += Build;
        }

        private static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Sprite panel = PanelSprite();
            var confirmArt = AssetDatabase.LoadAssetAtPath<Sprite>(ConfirmArtPath);
            var cancelArt = AssetDatabase.LoadAssetAtPath<Sprite>(CancelArtPath);
            if (panel == null || confirmArt == null || cancelArt == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform canvas = root.transform.Find("KioskCanvas");
                var exit = root.GetComponentInChildren<ExitGameButton>(true);
                if (canvas == null || exit == null || canvas.Find(DialogName) != null) return;

                ExitConfirmDialog dialog = CreateDialog(canvas, panel, confirmArt, cancelArt);
                var wiring = new SerializedObject(exit);
                wiring.FindProperty("confirmDialog").objectReferenceValue = dialog;
                wiring.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[KioskExitConfirmBuilder] Added the exit confirmation to the kiosk", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The board's picture, imported as a UI sprite the first time.
        private static Sprite PanelSprite()
        {
            if (AssetImporter.GetAtPath(PanelPath) is not TextureImporter importer) return null;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(PanelPath);
        }

        private static ExitConfirmDialog CreateDialog(Transform canvas, Sprite panel, Sprite confirmArt, Sprite cancelArt)
        {
            RectTransform dialogRect = Rect(DialogName, canvas);
            Stretch(dialogRect);
            var group = dialogRect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            RectTransform backdropRect = Rect("Backdrop", dialogRect);
            Stretch(backdropRect);
            var backdropImage = backdropRect.gameObject.AddComponent<Image>();
            backdropImage.color = Dim;
            var backdrop = backdropRect.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;

            RectTransform board = Rect("Board", dialogRect);
            board.sizeDelta = BoardSize;
            board.anchoredPosition = new Vector2(0f, 40f);
            var boardImage = board.gameObject.AddComponent<Image>();
            boardImage.sprite = panel;
            AddShadow(board.gameObject);

            Label("Title", board, "EXIT GAME?", 104, Color.white, new Vector2(0f, 95f), new Vector2(820f, 130f));
            Label("Message", board, "THE CASE IS STILL OPEN!\nLEAVE THE GAME NOW?", 46, MessageColour, new Vector2(0f, -35f), new Vector2(820f, 130f));
            Button confirm = BoardButton("ConfirmButton", board, confirmArt, "EXIT", new Vector2(-200f, -205f));
            Button cancel = BoardButton("CancelButton", board, cancelArt, "CANCEL", new Vector2(200f, -205f));

            var dialog = dialogRect.gameObject.AddComponent<ExitConfirmDialog>();
            var serialized = new SerializedObject(dialog);
            serialized.FindProperty("board").objectReferenceValue = board;
            serialized.FindProperty("confirmButton").objectReferenceValue = confirm;
            serialized.FindProperty("cancelButton").objectReferenceValue = cancel;
            serialized.FindProperty("backdrop").objectReferenceValue = backdrop;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            dialogRect.gameObject.SetActive(false);
            return dialog;
        }

        private static Button BoardButton(string name, RectTransform board, Sprite art, string word, Vector2 position)
        {
            RectTransform rect = Rect(name, board);
            rect.sizeDelta = ButtonSize;
            rect.anchoredPosition = position;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = art;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            AddShadow(rect.gameObject);

            RectTransform label = Label("Label", rect, word, 60, Color.white, new Vector2(0f, -3f), Vector2.zero);
            Stretch(label);
            label.anchoredPosition = new Vector2(0f, -3f);
            return button;
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
            var shadow = target.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(8f, -10f);
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
