using BountyHunter.Thieves;
using Fortal.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BountyHunter.Editor
{
    // Lays out character picking on the intro floor (Map/Arena/Character Select), as in the reference
    // shot: Tedly top left, Rex top right, Pompo bottom left, Simba bottom right, each card on its
    // folder with a SELECT button under it, a loading ring around it and a SELECTED stamp waiting.
    // The floor art lies flat like the bushes, its top toward the wall. Hands the cards to
    // IntroSequence so they only show during the intro. Runs after scripts load and is on the menu;
    // only what is missing is added.
    [InitializeOnLoad]
    public static class CharacterSelectSetup
    {
        private const string ScenePath = "Assets/Scenes/BountyHunter.unity";
        private const string Folder = "Assets/ART/Player";
        private const string FillShader = Folder + "/Floor/SpriteRadialFill.shader";
        private const string FillMaterial = Folder + "/Floor/Sprite_RadialFill.mat";
        private const float Height = 0.03f;
        private const string CountdownFont = "Assets/Front/BebasNeue-Regular SDF.asset";
        private const string CountdownMaterial = "Assets/Front/BebasNeue-Regular SDF - Outline.mat";

        // Sizes on the floor in metres (a player marker is about 0.67 wide).
        private const float CardWidth = 2.2f;
        private const float ButtonWidth = 1.4f;
        private const float StampWidth = 1.05f;
        private const float RingWidth = 2.6f;
        // Button and stamp, from the card's centre (x right, y toward the wall).
        private static readonly Vector2 ButtonOffset = new Vector2(-0.15f, -1.05f);
        private static readonly Vector2 StampOffset = new Vector2(-0.35f, -0.25f);
        private const float StampTurn = 10f;

        // Card picture name, where its centre sits on the floor (x, z) and the player marker ring
        // picture shown once it is picked.
        private static readonly (string Name, Vector2 Spot, string Marker)[] Cards =
        {
            ("Tedly", new Vector2(-3.7f, 1.3f), "Player Ted"),
            ("Rex", new Vector2(3.7f, 1.3f), "Player Rex"),
            ("Pompo", new Vector2(-3.7f, -1.3f), "Player Pom"),
            ("Simba", new Vector2(3.7f, -1.3f), "Player Simba"),
        };

        // A layout tried once and dropped; a card still there goes back to its spot above.
        private static readonly (string Name, Vector2 Spot)[] DroppedSpots =
        {
            ("Simba", new Vector2(-3.7f, 1.3f)),
            ("Pompo", new Vector2(3.7f, 1.3f)),
            ("Tedly", new Vector2(-3.7f, -1.3f)),
            ("Rex", new Vector2(3.7f, -1.3f)),
        };

        static CharacterSelectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Setup();
            };
        }

        [MenuItem("Tools/Bounty Hunter/Setup Character Select")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var scene = EditorSceneManager.GetActiveScene();
            GameObject arena = GameObject.Find("Map/Arena");
            if (!scene.isLoaded || scene.path != ScenePath || arena == null) return;

            bool wasDirty = scene.isDirty;
            bool changed = false;
            Transform root = arena.transform.Find("Character Select");
            if (root == null)
            {
                root = BuildRoot(arena.transform);
                changed = true;
            }

            changed |= MoveBack(root);
            changed |= StrongerBounce(root);
            changed |= BuildCountdown(root);
            changed |= LinkToIntro(root.gameObject);
            changed |= FillMarkers(root.GetComponent<CharacterSelect>());
            if (!changed) return;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!wasDirty) EditorSceneManager.SaveScene(scene);
            Debug.Log("[CharacterSelectSetup] Map/Arena/Character Select is ready: stand on a card for 2 seconds to pick it.");
        }

        private static Transform BuildRoot(Transform arena)
        {
            var root = new GameObject("Character Select", typeof(CharacterSelect));
            root.transform.SetParent(arena, false);
            Undo.RegisterCreatedObjectUndo(root, "Setup Character Select");

            Material fill = FillMaterialAsset();
            Sprite button = LoadSprite($"{Folder}/Select.png");
            Sprite stamp = LoadSprite($"{Folder}/Selected.png");
            Sprite ringBack = LoadSprite($"{Folder}/Floor/BG-Load.png");
            Sprite ringFill = LoadSprite($"{Folder}/Floor/Load.png");

            var cards = new CharacterCard[Cards.Length];
            for (int i = 0; i < Cards.Length; i++)
            {
                (string name, Vector2 spot, string _) = Cards[i];
                var station = new GameObject($"Card {name}", typeof(CharacterCard));
                station.transform.SetParent(root.transform, false);
                station.transform.SetPositionAndRotation(new Vector3(spot.x, Height, spot.y), Quaternion.Euler(90f, 0f, 0f));

                SpriteRenderer back = AddSprite(station.transform, "Ring Back", ringBack, RingWidth, Vector2.zero, 0f, 2);
                SpriteRenderer front = AddSprite(station.transform, "Ring Fill", ringFill, RingWidth, Vector2.zero, 0f, 3);
                if (fill != null) front.sharedMaterial = fill;
                SpriteRenderer picture = AddSprite(station.transform, "Card", LoadSprite($"{Folder}/{name}.png"), CardWidth, Vector2.zero, 0f, 4);
                SpriteRenderer select = AddSprite(station.transform, "Select", button, ButtonWidth, ButtonOffset, 0f, 5);
                SpriteRenderer selected = AddSprite(station.transform, "Stamp", stamp, StampWidth, StampOffset, StampTurn, 6);

                var card = station.GetComponent<CharacterCard>();
                var data = new SerializedObject(card);
                data.FindProperty("character").stringValue = name;
                data.FindProperty("card").objectReferenceValue = picture;
                data.FindProperty("button").objectReferenceValue = select;
                data.FindProperty("stamp").objectReferenceValue = selected;
                data.FindProperty("ringBack").objectReferenceValue = back;
                data.FindProperty("ringFill").objectReferenceValue = front;
                data.ApplyModifiedPropertiesWithoutUndo();
                cards[i] = card;
            }

            var picking = new SerializedObject(root.GetComponent<CharacterSelect>());
            picking.FindProperty("playerSpawner").objectReferenceValue = Object.FindFirstObjectByType<UWBPlayerSpawner>(FindObjectsInactive.Include);
            SerializedProperty list = picking.FindProperty("cards");
            list.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            }

            picking.ApplyModifiedPropertiesWithoutUndo();
            return root.transform;
        }

        // A flat sprite on the station, width metres wide, offset in the card's plane.
        private static SpriteRenderer AddSprite(Transform station, string pieceName, Sprite sprite, float width, Vector2 offset, float turn, int order)
        {
            var piece = new GameObject(pieceName, typeof(SpriteRenderer));
            piece.transform.SetParent(station, false);
            piece.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            piece.transform.localRotation = Quaternion.Euler(0f, 0f, turn);
            var spriteRenderer = piece.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = order;
            if (sprite != null) piece.transform.localScale = Vector3.one * (width / sprite.bounds.size.x);
            return spriteRenderer;
        }

        // The step-on bounce was too small to see from above; cards still at the first values get the bigger one.
        private static bool StrongerBounce(Transform root)
        {
            bool changed = false;
            foreach (CharacterCard card in root.GetComponentsInChildren<CharacterCard>(true))
            {
                var data = new SerializedObject(card);
                SerializedProperty bounce = data.FindProperty("stepBounce");
                SerializedProperty seconds = data.FindProperty("stepBounceSeconds");
                if (!Mathf.Approximately(bounce.floatValue, 0.12f) || !Mathf.Approximately(seconds.floatValue, 0.4f)) continue;

                bounce.floatValue = 0.28f;
                seconds.floatValue = 0.55f;
                data.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            return changed;
        }

        private static bool MoveBack(Transform root)
        {
            bool changed = false;
            foreach ((string name, Vector2 first) in DroppedSpots)
            {
                Transform station = root.Find($"Card {name}");
                if (station == null) continue;

                Vector3 at = station.position;
                if (Vector2.Distance(new Vector2(at.x, at.z), first) > 0.01f) continue;

                foreach ((string cardName, Vector2 spot, string _) in Cards)
                {
                    if (cardName != name) continue;
                    Undo.RecordObject(station, "Move Character Card");
                    station.position = new Vector3(spot.x, at.y, spot.y);
                    changed = true;
                }
            }

            return changed;
        }

        // The 3, 2, 1 in the middle of the floor, lying flat like the cards: its number (white, in the
        // outlined Bebas) is a child in the scene, to be adjusted there.
        private static bool BuildCountdown(Transform root)
        {
            bool changed = false;
            Transform countdown = root.Find("Countdown");
            if (countdown == null)
            {
                countdown = new GameObject("Countdown", typeof(FloorCountdown)).transform;
                countdown.SetParent(root, false);
                countdown.SetPositionAndRotation(new Vector3(0f, 0.1f, 0f), Quaternion.Euler(90f, 0f, 0f));
                Undo.RegisterCreatedObjectUndo(countdown.gameObject, "Setup Countdown");
                changed = true;
            }

            var data = new SerializedObject(countdown.GetComponent<FloorCountdown>());
            // The ring of light was dropped; one made earlier goes.
            Transform oldRing = countdown.Find("Ring");
            if (oldRing != null)
            {
                Undo.DestroyObjectImmediate(oldRing.gameObject);
                changed = true;
            }

            if (countdown.Find("Number") == null)
            {
                var numberObject = new GameObject("Number", typeof(RectTransform));
                numberObject.transform.SetParent(countdown, false);
                var number = numberObject.AddComponent<TMPro.TextMeshPro>();
                var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(CountdownFont);
                if (font != null) number.font = font;
                var outline = AssetDatabase.LoadAssetAtPath<Material>(CountdownMaterial);
                if (outline != null) number.fontSharedMaterial = outline;
                number.text = "3";
                number.fontSize = 20f;
                number.color = Color.white;
                number.alignment = TMPro.TextAlignmentOptions.Center;
                number.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                number.rectTransform.sizeDelta = new Vector2(4f, 3f);
                numberObject.GetComponent<MeshRenderer>().sortingOrder = 25;
                data.FindProperty("number").objectReferenceValue = number;
                changed = true;
            }

            if (changed) data.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static bool FillMarkers(CharacterSelect select)
        {
            if (select == null) return false;

            var data = new SerializedObject(select);
            SerializedProperty markers = data.FindProperty("markers");
            if (markers.arraySize > 0) return false;

            markers.arraySize = Cards.Length;
            for (int i = 0; i < Cards.Length; i++)
            {
                SerializedProperty entry = markers.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("character").stringValue = Cards[i].Name;
                entry.FindPropertyRelative("marker").objectReferenceValue = LoadSprite($"{Folder}/Floor/{Cards[i].Marker}.png");
            }

            data.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool LinkToIntro(GameObject root)
        {
            var intro = Object.FindFirstObjectByType<IntroSequence>(FindObjectsInactive.Include);
            if (intro == null) return false;

            var data = new SerializedObject(intro);
            bool changed = false;
            SerializedProperty select = data.FindProperty("characterSelect");
            if (select.objectReferenceValue == null)
            {
                select.objectReferenceValue = root.GetComponent<CharacterSelect>();
                changed = true;
            }

            SerializedProperty countdown = data.FindProperty("countdown");
            Transform count = root.transform.Find("Countdown");
            if (countdown.objectReferenceValue == null && count != null)
            {
                countdown.objectReferenceValue = count.GetComponent<FloorCountdown>();
                changed = true;
            }

            SerializedProperty pieces = data.FindProperty("introFloorPieces");
            bool listed = false;
            for (int i = 0; i < pieces.arraySize; i++)
            {
                if (pieces.GetArrayElementAtIndex(i).objectReferenceValue == root) listed = true;
            }

            if (!listed)
            {
                pieces.arraySize++;
                pieces.GetArrayElementAtIndex(pieces.arraySize - 1).objectReferenceValue = root;
                changed = true;
            }

            if (changed) data.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static Material FillMaterialAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(FillMaterial);
            if (existing != null) return existing;

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(FillShader);
            if (shader == null) return null;
            var material = new Material(shader) { name = "Sprite_RadialFill" };
            AssetDatabase.CreateAsset(material, FillMaterial);
            return material;
        }

        private static Sprite LoadSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
