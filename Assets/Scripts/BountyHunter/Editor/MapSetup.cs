using BountyHunter.Thieves;
using TMPro;
using Fortal.Display;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Editor
{
    // Adds Map/Arena/Maps (MapSwitcher, N switches map) to the game scene with two maps: Map1 the
    // street (bins, trash) and Map2 the park (bushes, leaves), and gives the wall's BG_Time banner
    // its StageTimer. Runs by itself after imports and is
    // also on the menu. Only empty slots are filled, so art added later (such as the trash sprites
    // in Map1/Trash) is picked up and Inspector changes are kept.
    [InitializeOnLoad]
    public static class MapSetup
    {
        private const string ScenePath = "Assets/Scenes/BountyHunter.unity";
        private const string TimerPrefabPath = "Assets/Prefabs/UI/BG_Time.prefab";
        // The countdown's font with a black outline, like the stage banner reference. Named after
        // the font asset so TextMesh Pro lists it as one of that font's material presets.
        private const string OutlineMaterialPath = "Assets/Front/BebasNeue-Regular SDF - Outline.mat";
        private const float OutlineWidth = 0.32f;
        private static readonly Color OutlineColor = new Color(0.06f, 0.04f, 0.03f, 1f);

        private sealed class MapArt
        {
            public string Name { get; set; }
            public string Floor { get; set; }
            public string SpotFolder { get; set; }
            public string SpotPrefix { get; set; }
            public float MaxTiltDegrees { get; set; }
            // Size as a multiple of the player width on the art's narrowest side.
            public Vector2 SizeVsPlayer { get; set; }
            public string BurstFolder { get; set; }
            public string BurstPrefix { get; set; }
        }

        private static readonly MapArt[] Maps =
        {
            // Bins are drawn upright, so they never turn, and stay close to one size.
            new MapArt { Name = "Map1 Street", Floor = "Assets/ART/BG/Map1/FloorMap1.png", SpotFolder = "Assets/ART/BG/Map1", SpotPrefix = "Been",
                MaxTiltDegrees = 0f, SizeVsPlayer = new Vector2(1.35f, 1.6f), BurstFolder = "Assets/ART/BG/Map1/Trash", BurstPrefix = "Trash_" },
            new MapArt { Name = "Map2 Park", Floor = "Assets/ART/BG/Map2/FloorMap2.png", SpotFolder = "Assets/ART/BG/Map2", SpotPrefix = "pummai",
                MaxTiltDegrees = 180f, SizeVsPlayer = new Vector2(1.15f, 2.3f), BurstFolder = "Assets/ART/BG/Map2/Leaf", BurstPrefix = "Leaf_" },
        };

        static MapSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Setup();
            };
        }

        // Fills the maps again when map art is imported, so new art (such as trash) needs no menu click.
        private sealed class ArtWatcher : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                foreach (string path in imported)
                {
                    if (!path.StartsWith("Assets/ART/BG/") || !path.EndsWith(".png")) continue;
                    EditorApplication.delayCall += Setup;
                    return;
                }
            }
        }

        [MenuItem("Tools/Bounty Hunter/Setup Maps")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = EditorSceneManager.GetActiveScene();
            GameObject arena = GameObject.Find("Map/Arena");
            if (!scene.isLoaded || scene.path != ScenePath || arena == null) return;

            bool wasDirty = scene.isDirty;
            AddTimerToPrefab();
            OutlineTimerDigits();
            Transform maps = arena.transform.Find("Maps");
            if (maps == null)
            {
                maps = new GameObject("Maps", typeof(MapSwitcher)).transform;
                maps.SetParent(arena.transform, false);
                Undo.RegisterCreatedObjectUndo(maps.gameObject, "Setup Maps");
            }

            if (!Fill(maps.GetComponent<MapSwitcher>(), arena.transform)) return;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!wasDirty) EditorSceneManager.SaveScene(scene);
            Debug.Log("[MapSetup] Map/Arena/Maps is ready. Press N in Play to switch map.");
        }

        // Returns true when anything was filled in.
        private static bool Fill(MapSwitcher switcher, Transform arena)
        {
            var data = new SerializedObject(switcher);
            bool changed = false;
            changed |= SetIfEmpty(data.FindProperty("floorImage"), FindComponent<RawImage>(arena, "FloorDisplay/Background"));
            changed |= SetIfEmpty(data.FindProperty("hidingSpots"), FindComponent<BushScatter>(arena, "Bushes"));
            changed |= SetIfEmpty(data.FindProperty("thieves"), FindComponent<ThiefSpawner>(arena, "Thieves"));

            SerializedProperty list = data.FindProperty("maps");
            changed |= StreetFirst(list);
            if (list.arraySize < Maps.Length)
            {
                int first = list.arraySize;
                list.arraySize = Maps.Length;
                for (int i = first; i < Maps.Length; i++)
                {
                    SerializedProperty map = list.GetArrayElementAtIndex(i);
                    map.FindPropertyRelative("name").stringValue = Maps[i].Name;
                    map.FindPropertyRelative("maxTiltDegrees").floatValue = Maps[i].MaxTiltDegrees;
                    map.FindPropertyRelative("sizeVsPlayer").vector2Value = Maps[i].SizeVsPlayer;
                }

                changed = true;
            }

            for (int i = 0; i < Maps.Length; i++)
            {
                SerializedProperty map = list.GetArrayElementAtIndex(i);
                changed |= SetIfEmpty(map.FindPropertyRelative("floor"), AssetDatabase.LoadAssetAtPath<Texture2D>(Maps[i].Floor));
                changed |= FillIfEmpty(map.FindPropertyRelative("hidingSpots"), ThiefSetup.LoadSprites(Maps[i].SpotFolder, Maps[i].SpotPrefix));
                changed |= FillIfEmpty(map.FindPropertyRelative("burstSprites"), ThiefSetup.LoadSprites(Maps[i].BurstFolder, Maps[i].BurstPrefix));
            }

            if (changed) data.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        // The park used to be the first map and the street the second; a scene still in that order
        // swaps them, keeping their settings, and takes the new names.
        private static bool StreetFirst(SerializedProperty list)
        {
            if (list.arraySize < 2) return false;
            SerializedProperty first = list.GetArrayElementAtIndex(0).FindPropertyRelative("name");
            if (first.stringValue != "Map1 Park") return false;

            list.MoveArrayElement(1, 0);
            list.GetArrayElementAtIndex(0).FindPropertyRelative("name").stringValue = Maps[0].Name;
            list.GetArrayElementAtIndex(1).FindPropertyRelative("name").stringValue = Maps[1].Name;
            Debug.Log("[MapSetup] The street is Map1 now and the park Map2.");
            return true;
        }

        // Gives the banner prefab a StageTimer driving its Title and Time texts, once.
        private static void AddTimerToPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TimerPrefabPath) == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(TimerPrefabPath);
            try
            {
                if (root.GetComponent<StageTimer>() != null) return;

                var data = new SerializedObject(root.AddComponent<StageTimer>());
                data.FindProperty("title").objectReferenceValue = FindComponent<TMP_Text>(root.transform, "Title");
                data.FindProperty("time").objectReferenceValue = FindComponent<TMP_Text>(root.transform, "Time");
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, TimerPrefabPath);
                Debug.Log("[MapSetup] BG_Time now counts down (StageTimer).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Puts the outlined material on the banner's Time text, making the material the first time.
        private static void OutlineTimerDigits()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TimerPrefabPath) == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(TimerPrefabPath);
            try
            {
                TMP_Text digits = FindComponent<TMP_Text>(root.transform, "Time");
                if (digits == null || digits.font == null) return;

                Material outlined = OutlineMaterial(digits.font);
                if (digits.fontSharedMaterial == outlined) return;

                digits.fontSharedMaterial = outlined;
                PrefabUtility.SaveAsPrefabAsset(root, TimerPrefabPath);
                Debug.Log("[MapSetup] BG_Time digits now have a black outline.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Material OutlineMaterial(TMP_FontAsset font)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (existing != null) return existing;

            var material = new Material(font.material);
            material.EnableKeyword("OUTLINE_ON");
            material.SetFloat("_OutlineWidth", OutlineWidth);
            // TMP centres the outline on the letter edge; growing the letters by the same amount
            // keeps the digits their full size and puts the whole outline outside them.
            material.SetFloat("_FaceDilate", OutlineWidth);
            material.SetColor("_OutlineColor", OutlineColor);
            AssetDatabase.CreateAsset(material, OutlineMaterialPath);
            return material;
        }

        private static T FindComponent<T>(Transform parent, string path) where T : Component
        {
            Transform child = parent.Find(path);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static bool SetIfEmpty(SerializedProperty property, Object value)
        {
            if (property.objectReferenceValue != null || value == null) return false;
            property.objectReferenceValue = value;
            return true;
        }

        private static bool FillIfEmpty(SerializedProperty property, Sprite[] sprites)
        {
            if (property.arraySize > 0 || sprites.Length == 0) return false;
            property.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }

            return true;
        }
    }
}
