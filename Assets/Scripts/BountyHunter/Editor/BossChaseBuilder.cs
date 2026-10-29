using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves.EditorTools
{
    // Imports the boss's art in Assets/ART/Boss (the move sheets from Tools/make_boss_sheet.py and its
    // hat) as sprites without mipmaps and at full size, so the sheets' cells keep their pixel size
    // (BossSheet cuts them by it). Only first imports are set up; a change made by hand stays.
    public sealed class BossArtImport : AssetPostprocessor
    {
        public const string Folder = "Assets/ART/Boss/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder) || !assetImporter.importSettingsMissing) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 8192;
            importer.textureCompression = TextureImporterCompression.Compressed;
        }
    }

    // Wires the boss chase into the open scene each time the editor loads, when it is not yet: a Boss
    // Chase object with its sheets, the hat, the feather, the water pictures and the game's pieces (players, score tags, passers-by,
    // clock, fonts, catch scuffle and floor area, taken from BountyRounds and ThiefSpawner), set as
    // MapSwitcher's finale. Sheets made later are filled in on the next load, and each sheet's layout
    // (columns, frames, rate, pivot) is kept in step with the .json make_boss_sheet.py writes next to
    // it, so a new cut needs nothing typed in. Save the scene afterwards.
    [InitializeOnLoad]
    public static class BossChaseBuilder
    {
        private const string Sheets = BossArtImport.Folder + "Sheets/";
        private const string HatPath = BossArtImport.Folder + "BossHat.png";
        // The boss's rooftop map pictures.
        private const string BossMap = "Assets/ART/BG/MapBoss/";
        private const string BossWallPath = "Assets/Prefabs/Boss/Boss Wall.prefab";
        // The feather blowing about in the entrance, the same as the cut-in's.
        private const string FeatherPath = BossTransitionArtImport.Folder + "Boss Open/Boss_Feather.png";
        // The water and lightning pictures Codex drew for the boss's shot.
        private const string Water = BossArtImport.Folder + "Water/";

        // The layout file make_boss_sheet.py writes next to a sheet.
        [System.Serializable]
        private sealed class SheetLayout
        {
            public int columns;
            public int frames;
            public float framesPerSecond;
            public float[] pivot;
        }

        static BossChaseBuilder()
        {
            EditorApplication.delayCall += WireIfMissing;
        }

        [MenuItem("Tools/Bounty Hunter/Wire Boss Chase")]
        private static void WireIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var switcher = Object.FindFirstObjectByType<MapSwitcher>();
            if (switcher == null) return;

            bool changed = false;
            var chase = Object.FindFirstObjectByType<BossChase>(FindObjectsInactive.Include);
            if (chase == null)
            {
                chase = new GameObject("Boss Chase").AddComponent<BossChase>();
                Undo.RegisterCreatedObjectUndo(chase.gameObject, "Wire Boss Chase");
                CopyGamePieces(chase);
                changed = true;
                Debug.Log("[BossChaseBuilder] Added the Boss Chase to the scene");
            }

            changed |= FillArt(chase);
            changed |= FillHidingSpots(chase);
            changed |= FillThiefExtras(chase);
            changed |= FillRooftop(chase);
            changed |= SetFinale(switcher, chase);
            if (changed) EditorSceneManager.MarkSceneDirty(chase.gameObject.scene);
        }

        // The same pieces the stages use, copied from fields of the same name.
        private static void CopyGamePieces(BossChase chase)
        {
            var target = new SerializedObject(chase);
            var rounds = Object.FindFirstObjectByType<BountyRounds>(FindObjectsInactive.Include);
            if (rounds != null) Copy(new SerializedObject(rounds), target, "scoreTags", "npcs", "timer", "damageFont", "damageMaterial");

            var thieves = Object.FindFirstObjectByType<ThiefSpawner>(FindObjectsInactive.Include);
            if (thieves != null) Copy(new SerializedObject(thieves), target, "playerSpawner", "catchBrawl", "areaMin", "areaMax");

            target.ApplyModifiedPropertiesWithoutUndo();
        }

        // An existing Boss Chase made before the boss could hide gets the map's hiding spots too.
        private static bool FillHidingSpots(BossChase chase)
        {
            var target = new SerializedObject(chase);
            SerializedProperty spots = target.FindProperty("hidingSpots");
            if (spots == null || spots.objectReferenceValue != null) return false;

            var thieves = Object.FindFirstObjectByType<ThiefSpawner>(FindObjectsInactive.Include);
            if (thieves == null) return false;

            spots.objectReferenceValue = thieves;
            target.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // The thieves' footfall sounds and dust, given to a Boss Chase that has none yet.
        private static bool FillThiefExtras(BossChase chase)
        {
            var target = new SerializedObject(chase);
            bool changed = false;
            SerializedProperty sounds = target.FindProperty("sounds");
            var sfx = Object.FindFirstObjectByType<GameSfx>(FindObjectsInactive.Include);
            if (sounds != null && sounds.objectReferenceValue == null && sfx != null)
            {
                sounds.objectReferenceValue = sfx;
                changed = true;
            }

            SerializedProperty dust = target.FindProperty("dustSprites");
            var thieves = Object.FindFirstObjectByType<ThiefSpawner>(FindObjectsInactive.Include);
            if (dust != null && dust.arraySize == 0 && thieves != null)
            {
                target.CopyFromSerializedProperty(new SerializedObject(thieves).FindProperty("dustSprites"));
                changed = dust.arraySize > 0;
            }

            if (changed) target.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        // The boss's rooftop: the floor picture MapSwitcher shows maps on and the boss's floor
        // picture, the Boss Wall prefab (made if missing) placed in the wall's background picture
        // (Background under Play) under everything else there, the stage's wall pieces to put away
        // during the chase, and the helicopter.
        private static bool FillRooftop(BossChase chase)
        {
            var target = new SerializedObject(chase);
            bool changed = false;
            var switcher = Object.FindFirstObjectByType<MapSwitcher>(FindObjectsInactive.Include);
            SerializedProperty floor = target.FindProperty("floorImage");
            if (floor != null && floor.objectReferenceValue == null && switcher != null)
            {
                floor.objectReferenceValue = new SerializedObject(switcher).FindProperty("floorImage").objectReferenceValue;
                changed |= floor.objectReferenceValue != null;
            }

            changed |= FillReference(target.FindProperty("bossFloor"), BossMap + "BossFloor.png");
            changed |= FillSprite(target.FindProperty("helicopter"), BossArtImport.Folder + "Helicopter.png");
            changed |= FillSprite(target.FindProperty("helicopterRotor"), BossArtImport.Folder + "HelicopterRotor.png");

            RectTransform wallPicture = WallPicture();
            SerializedProperty wall = target.FindProperty("wallPanel");
            if (wallPicture != null && wall != null && wall.objectReferenceValue == null)
            {
                GameObject placed = PlaceBossWall(wallPicture);
                if (placed != null)
                {
                    wall.objectReferenceValue = placed;
                    target.FindProperty("wallSky").objectReferenceValue = placed.transform.Find("Sky");
                    changed = true;
                }
            }

            if (wallPicture != null) changed |= FillStagePieces(target, wallPicture);
            if (changed) target.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        // The wall's background picture: Background under Play.
        private static RectTransform WallPicture()
        {
            foreach (RawImage image in Object.FindObjectsByType<RawImage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (image.name == "Background" && image.transform.parent != null && image.transform.parent.name == "Play") return image.rectTransform;
            }

            return null;
        }

        // The prefab (made if missing) put in the wall picture as its first child, so the stage's
        // pieces there are drawn over it (they are put away during the chase) and it covers the
        // stage's picture; off until the chase.
        private static GameObject PlaceBossWall(RectTransform wallPicture)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossWallPath) ?? MakeBossWall();
            if (prefab == null) return null;

            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, wallPicture.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(placed, "Place Boss Wall");
            placed.transform.SetParent(wallPicture, false);
            placed.transform.SetAsFirstSibling();
            placed.SetActive(false);
            Debug.Log("[BossChaseBuilder] Placed the Boss Wall in the wall's background picture");
            return placed;
        }

        // Boss Wall: the rooftop picture, the Sky the helicopter flies in, and the buildings in front
        // of the sky over it, each filling the wall.
        private static GameObject MakeBossWall()
        {
            var rooftop = AssetDatabase.LoadAssetAtPath<Texture2D>(BossMap + "BGBoss.png");
            var front = AssetDatabase.LoadAssetAtPath<Texture2D>(BossMap + "BGBossFront.png");
            if (rooftop == null) return null;

            var root = new GameObject("Boss Wall", typeof(RectTransform));
            Fill((RectTransform)root.transform);
            Picture(root.transform, "Rooftop", rooftop);
            Fill((RectTransform)new GameObject("Sky", typeof(RectTransform)).transform, root.transform);
            if (front != null) Picture(root.transform, "Buildings In Front", front);

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(BossWallPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BossWallPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[BossChaseBuilder] Made {BossWallPath}");
            return prefab;
        }

        private static void Picture(Transform parent, string name, Texture texture)
        {
            var picture = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            Fill((RectTransform)picture.transform, parent);
            var image = picture.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
        }

        // Stretched over the whole of its parent.
        private static void Fill(RectTransform rect, Transform parent = null)
        {
            if (parent != null) rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // The stage's wall pieces put away during the chase (the board, the side pictures, the tables
        // and the posters), and the dogs' own pictures switched off (their score tags stay).
        private static bool FillStagePieces(SerializedObject target, RectTransform wallPicture)
        {
            bool changed = false;
            SerializedProperty hide = target.FindProperty("hideDuringChase");
            if (hide != null && hide.arraySize == 0)
            {
                Transform play = wallPicture.parent;
                foreach (string path in new[] { "Background/Board", "Background/BGleft", "Background/BGright", "Background/Table1", "Background/Table2", "Post" })
                {
                    Transform piece = play.Find(path);
                    if (piece == null) continue;
                    hide.InsertArrayElementAtIndex(hide.arraySize);
                    hide.GetArrayElementAtIndex(hide.arraySize - 1).objectReferenceValue = piece.gameObject;
                    changed = true;
                }
            }

            SerializedProperty off = target.FindProperty("switchOffDuringChase");
            var tags = Object.FindFirstObjectByType<CharacterScoreTags>(FindObjectsInactive.Include);
            if (off != null && off.arraySize == 0 && tags != null)
            {
                SerializedProperty entries = new SerializedObject(tags).FindProperty("entries");
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var dog = entries.GetArrayElementAtIndex(i).FindPropertyRelative("dog").objectReferenceValue as GameObject;
                    if (dog == null) continue;
                    foreach (Graphic graphic in dog.GetComponentsInChildren<Graphic>(true))
                    {
                        // The score tag and everything in it stay.
                        if (graphic.GetComponentInParent<PlayerScoreTag>(true) != null) continue;
                        off.InsertArrayElementAtIndex(off.arraySize);
                        off.GetArrayElementAtIndex(off.arraySize - 1).objectReferenceValue = graphic;
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private static void Copy(SerializedObject source, SerializedObject target, params string[] names)
        {
            foreach (string name in names)
            {
                SerializedProperty property = source.FindProperty(name);
                if (property != null) target.CopyFromSerializedProperty(property);
            }
        }

        // Fills in sheets and the hat that are not set yet; true when something was.
        private static bool FillArt(BossChase chase)
        {
            var target = new SerializedObject(chase);
            bool changed = false;
            changed |= FillSheet(target, "walk", "BossWalk");
            changed |= FillSheet(target, "shoot", "BossShoot");
            changed |= FillSheet(target, "smokeBomb", "BossSmokeBomb");
            changed |= FillSheet(target, "capeWrap", "BossCapeWrap");
            changed |= FillSheet(target, "capeReveal", "BossCapeReveal");
            changed |= FillSprite(target.FindProperty("boss.hat"), HatPath);
            changed |= FillSprite(target.FindProperty("feather"), FeatherPath);
            changed |= FillSprite(target.FindProperty("boss.water.ring"), Water + "WaterRing.png");
            changed |= FillSprite(target.FindProperty("boss.water.streak"), Water + "WaterStreak.png");
            changed |= FillSprite(target.FindProperty("boss.water.impact"), Water + "WaterImpact.png");
            changed |= FillSprites(target.FindProperty("boss.water.bolts"), Water + "Lightning1.png", Water + "Lightning2.png");
            if (changed) target.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static bool FillSheet(SerializedObject target, string field, string sheet)
        {
            bool changed = FillReference(target.FindProperty(field + ".texture"), Sheets + sheet + ".png");
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(Sheets + sheet + ".json");
            if (file == null) return changed;

            var layout = JsonUtility.FromJson<SheetLayout>(file.text);
            changed |= SetInt(target.FindProperty(field + ".columns"), layout.columns);
            changed |= SetInt(target.FindProperty(field + ".frames"), layout.frames);
            changed |= SetFloat(target.FindProperty(field + ".framesPerSecond"), layout.framesPerSecond);
            if (layout.pivot != null && layout.pivot.Length == 2)
            {
                SerializedProperty pivot = target.FindProperty(field + ".pivot");
                var value = new Vector2(layout.pivot[0], layout.pivot[1]);
                if (pivot != null && pivot.vector2Value != value)
                {
                    pivot.vector2Value = value;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool SetInt(SerializedProperty property, int value)
        {
            if (property == null || value <= 0 || property.intValue == value) return false;
            property.intValue = value;
            return true;
        }

        private static bool SetFloat(SerializedProperty property, float value)
        {
            if (property == null || value <= 0f || Mathf.Approximately(property.floatValue, value)) return false;
            property.floatValue = value;
            return true;
        }

        private static bool FillReference(SerializedProperty property, string path)
        {
            if (property == null || property.objectReferenceValue != null) return false;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) return false;

            property.objectReferenceValue = texture;
            return true;
        }

        private static bool FillSprite(SerializedProperty property, string path)
        {
            if (property == null || property.objectReferenceValue != null) return false;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) return false;

            property.objectReferenceValue = sprite;
            return true;
        }

        // Fills an empty list with the pictures found at paths (missing ones are left out).
        private static bool FillSprites(SerializedProperty list, params string[] paths)
        {
            if (list == null || !list.isArray || list.arraySize > 0) return false;

            foreach (string path in paths)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sprite;
            }

            return list.arraySize > 0;
        }

        private static bool SetFinale(MapSwitcher switcher, BossChase chase)
        {
            var target = new SerializedObject(switcher);
            SerializedProperty finale = target.FindProperty("finale");
            if (finale == null || finale.objectReferenceValue != null) return false;

            finale.objectReferenceValue = chase;
            target.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[BossChaseBuilder] The boss chase plays after the last map");
            return true;
        }
    }
}
