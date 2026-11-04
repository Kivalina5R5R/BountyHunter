using Fortal.Players;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BountyHunter.Thieves.EditorTools
{
    // Sets up the market (stage 3) each time the editor loads: imports the art Codex drew
    // (Assets/ART/Market, prepared by Tools/MarketArt) as sprites, adds the handcuffs and police cars
    // (PoliceCuffs) and the lucky wheel (LuckyWheel) next to the open scene's BountyRounds when they
    // are missing, fills every reference left empty (the wheel spins on the game wall the posters
    // hang on; its ring and stars are the park tape box's: the players' select ring with the radial
    // fill material, and the park's sparkles), points BountyRounds and the gameplay sounds (GameSfx) at the cuffs, and keeps the market's hiding
    // spots off the cars (the third map's Keep Clear, when empty).
    // Anything set by hand is kept. Save the scene afterwards.
    [InitializeOnLoad]
    public static class MarketAutoWire
    {
        private const string Art = "Assets/ART/Market/";
        private const string PoliceCar = Art + "PoliceCar.png";
        private const string Handcuffs = Art + "Handcuffs.png";
        private const string Wheel = Art + "LuckyWheel.png";
        private const string WheelPointer = Art + "LuckyWheelPointer.png";
        // What the wheel landed on is written on this banner in the wall's font and its outlined material.
        private const string CaptionBack = Art + "LuckyWheelCaptionBack.png";
        private const string Font = "Assets/Front/BebasNeue-Regular SDF.asset";
        private const string FontMaterial = "Assets/Front/BebasNeue-Regular SDF - Outline.mat";
        // The mystery box lying on the floor.
        private const string MysteryBox = Art + "MysteryBox.png";
        // In LuckyWheelResult order.
        private static readonly string[] ResultIcons =
        {
            Art + "LuckyIconMoreHiding.png", Art + "LuckyIconCallOut.png", Art + "LuckyIconStunGuns.png", Art + "LuckyIconCarsMove.png",
        };
        private static readonly string[] Sparkles = { "Assets/ART/Park/Sparkle1.png", "Assets/ART/Park/Sparkle2.png" };
        private const string SelectRing = "Assets/ART/Player/Floor/Load.png";
        private const string RadialFill = "Assets/ART/Player/Floor/Sprite_RadialFill.mat";

        static MarketAutoWire()
        {
            EditorApplication.delayCall += Wire;
        }

        private static void Wire()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            foreach (string path in new[] { PoliceCar, Handcuffs, Wheel, WheelPointer, MysteryBox, CaptionBack }) ImportAsSprite(path);
            foreach (string path in ResultIcons) ImportAsSprite(path);

            var rounds = Object.FindFirstObjectByType<BountyRounds>(FindObjectsInactive.Include);
            if (rounds == null) return;

            bool changed = false;
            PoliceCuffs cuffs = rounds.GetComponent<PoliceCuffs>();
            if (cuffs == null)
            {
                cuffs = rounds.gameObject.AddComponent<PoliceCuffs>();
                changed = true;
            }

            var fields = new SerializedObject(cuffs);
            changed |= Fill(fields, "rounds", rounds);
            changed |= Fill(fields, "thieves", Object.FindFirstObjectByType<ThiefSpawner>(FindObjectsInactive.Include));
            changed |= Fill(fields, "players", Object.FindFirstObjectByType<UWBPlayerSpawner>(FindObjectsInactive.Include));
            changed |= Fill(fields, "bombs", Object.FindFirstObjectByType<BombSquad>(FindObjectsInactive.Include));
            changed |= Fill(fields, "timer", Object.FindFirstObjectByType<StageTimer>(FindObjectsInactive.Include));
            changed |= Fill(fields, "policeCar", AssetDatabase.LoadAssetAtPath<Sprite>(PoliceCar));
            changed |= Fill(fields, "handcuffs", AssetDatabase.LoadAssetAtPath<Sprite>(Handcuffs));
            fields.ApplyModifiedPropertiesWithoutUndo();

            var roundFields = new SerializedObject(rounds);
            changed |= Fill(roundFields, "cuffs", cuffs);
            roundFields.ApplyModifiedPropertiesWithoutUndo();

            var sounds = Object.FindFirstObjectByType<GameSfx>(FindObjectsInactive.Include);
            if (sounds != null)
            {
                var soundFields = new SerializedObject(sounds);
                changed |= Fill(soundFields, "policeCuffs", cuffs);
                soundFields.ApplyModifiedPropertiesWithoutUndo();
            }

            changed |= KeepHidingSpotsOffCars(fields);
            changed |= WireLuckyWheel(rounds);
            if (!changed) return;

            EditorSceneManager.MarkSceneDirty(rounds.gameObject.scene);
            Debug.Log("[MarketAutoWire] The market's handcuffs, police cars and lucky wheel are set up; save the scene to keep them.", cuffs);
        }

        private static bool WireLuckyWheel(BountyRounds rounds)
        {
            bool changed = false;
            LuckyWheel wheel = rounds.GetComponent<LuckyWheel>();
            if (wheel == null)
            {
                wheel = rounds.gameObject.AddComponent<LuckyWheel>();
                changed = true;
            }

            var fields = new SerializedObject(wheel);
            changed |= Fill(fields, "rounds", rounds);
            changed |= Fill(fields, "players", Object.FindFirstObjectByType<UWBPlayerSpawner>(FindObjectsInactive.Include));
            changed |= Fill(fields, "thieves", Object.FindFirstObjectByType<ThiefSpawner>(FindObjectsInactive.Include));
            changed |= Fill(fields, "hidingSpots", Object.FindFirstObjectByType<Fortal.Display.BushScatter>(FindObjectsInactive.Include));
            changed |= Fill(fields, "passersBy", Object.FindFirstObjectByType<NpcCrowd>(FindObjectsInactive.Include));
            changed |= Fill(fields, "stunGuns", Object.FindFirstObjectByType<StunGuns>(FindObjectsInactive.Include));
            changed |= Fill(fields, "cuffs", rounds.GetComponent<PoliceCuffs>());
            // The game wall: where the posters hang.
            var board = Object.FindFirstObjectByType<WantedBoard>(FindObjectsInactive.Include);
            if (board != null) changed |= Fill(fields, "wall", board.transform.parent as RectTransform);
            changed |= Fill(fields, "wheel", AssetDatabase.LoadAssetAtPath<Sprite>(Wheel));
            changed |= Fill(fields, "pointer", AssetDatabase.LoadAssetAtPath<Sprite>(WheelPointer));
            changed |= Fill(fields, "mysteryBox", AssetDatabase.LoadAssetAtPath<Sprite>(MysteryBox));
            changed |= Fill(fields, "captionBack", AssetDatabase.LoadAssetAtPath<Sprite>(CaptionBack));
            changed |= Fill(fields, "font", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Font));
            changed |= Fill(fields, "fontMaterial", AssetDatabase.LoadAssetAtPath<Material>(FontMaterial));
            changed |= FillList(fields, "resultIcons", ResultIcons);
            changed |= FillList(fields, "sparkles", Sparkles);
            changed |= Fill(fields, "selectRing", AssetDatabase.LoadAssetAtPath<Sprite>(SelectRing));
            changed |= Fill(fields, "radialFill", AssetDatabase.LoadAssetAtPath<Material>(RadialFill));
            fields.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        // A list of sprites filled in, in order, when it is empty.
        private static bool FillList(SerializedObject fields, string name, string[] paths)
        {
            SerializedProperty list = fields.FindProperty(name);
            if (list == null || list.arraySize > 0) return false;

            list.arraySize = paths.Length;
            for (int i = 0; i < paths.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
            return paths.Length > 0;
        }

        // The cuffs' stage's map (stage 1 is the first map) keeps a circle round each car free of
        // hiding spots, reaching past the car's corners or to its hand-over ring, whichever is larger.
        private static bool KeepHidingSpotsOffCars(SerializedObject cuffs)
        {
            var switcher = Object.FindFirstObjectByType<MapSwitcher>(FindObjectsInactive.Include);
            if (switcher == null) return false;

            var switcherFields = new SerializedObject(switcher);
            SerializedProperty maps = switcherFields.FindProperty("maps");
            int index = cuffs.FindProperty("stage").intValue - 1;
            if (maps == null || index < 0 || index >= maps.arraySize) return false;

            SerializedProperty keepClear = maps.GetArrayElementAtIndex(index).FindPropertyRelative("keepClear");
            if (keepClear == null || keepClear.arraySize > 0) return false;

            SerializedProperty spots = cuffs.FindProperty("carSpots");
            float radius = Mathf.Max(cuffs.FindProperty("carRadius").floatValue, cuffs.FindProperty("carLength").floatValue * 0.6f);
            keepClear.arraySize = spots.arraySize;
            for (int i = 0; i < spots.arraySize; i++)
            {
                Vector2 spot = spots.GetArrayElementAtIndex(i).vector2Value;
                keepClear.GetArrayElementAtIndex(i).vector3Value = new Vector3(spot.x, spot.y, radius);
            }

            switcherFields.ApplyModifiedPropertiesWithoutUndo();
            return spots.arraySize > 0;
        }

        // Sets an empty reference; true when it did.
        private static bool Fill(SerializedObject fields, string name, Object value)
        {
            SerializedProperty property = fields.FindProperty(name);
            if (property == null || value == null || property.objectReferenceValue != null) return false;

            property.objectReferenceValue = value;
            return true;
        }

        // A drawing imported as a single sprite without mipmaps, the first time.
        private static void ImportAsSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer || importer.textureType == TextureImporterType.Sprite) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }
}
