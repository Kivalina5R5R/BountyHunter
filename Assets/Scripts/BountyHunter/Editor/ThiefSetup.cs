using BountyHunter.Thieves;
using Fortal.Display;
using Fortal.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BountyHunter.Editor
{
    // Builds one prefab per thief kind (Chicken, Cat, Crow) from the frames cut out of each video
    // (Tools/extract_thief_video.py, Tools/make_blink_frames.py) and adds Map/Arena/Thieves
    // (ThiefSpawner) to the open game scene with every kind in its Thief Types list. Runs by itself
    // after imports and is also on the menu. A new kind needs its frames and one line in Kinds.
    [InitializeOnLoad]
    public static class ThiefSetup
    {
        private const string ScenePath = "Assets/Scenes/BountyHunter.unity";
        private const string PrefabFolder = "Assets/Prefabs/Thieves";
        private const string LeafFolder = "Assets/ART/BG/Leaf";
        private const string DustFolder = "Assets/ART/FX/Dust";

        // Every kind's pixels-per-unit makes its walking height this many units, and the body is
        // then scaled so that is ThiefHeight in the game: a little shorter than the player marker
        // (0.665), so it fits under a bush.
        private const float ReferenceUnits = 13.83f;
        private const float ThiefHeight = 0.6f;

        private sealed class ThiefKind
        {
            public string Name;
            // Folder name of the standing breath frames: Sit for the chicken, Idle for the others.
            public string IdleName;
            // Measured walking height in pixels / ReferenceUnits.
            public float PixelsPerUnit;
            // World distance of one walk cycle: video cycle seconds x the 0.6 units/s walk.
            public float CycleLength;
            public int Count;

            public string Folder => $"Assets/ART/Thif/{Name}";
            public string PrefabPath => $"{PrefabFolder}/{Name}.prefab";
        }

        private static readonly ThiefKind[] Kinds =
        {
            new ThiefKind { Name = "Chicken", IdleName = "Sit", PixelsPerUnit = 66.94f, CycleLength = 0.56f, Count = 3 },
            new ThiefKind { Name = "Cat", IdleName = "Idle", PixelsPerUnit = 62.68f, CycleLength = 0.5f, Count = 2 },
            new ThiefKind { Name = "Crow", IdleName = "Idle", PixelsPerUnit = 66.2f, CycleLength = 0.48f, Count = 2 },
        };

        static ThiefSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                foreach (ThiefKind kind in Kinds) UpdatePrefab(kind);
                GameObject existing = GameObject.Find("Map/Arena/Thieves");
                if (existing == null) Setup();
                else UpdateSpawner(existing.GetComponent<ThiefSpawner>());
            };
        }

        [MenuItem("Tools/Bounty Hunter/Setup Thieves")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            GameObject arena = GameObject.Find("Map/Arena");
            if (!scene.isLoaded || scene.path != ScenePath || arena == null) return;

            Transform existing = arena.transform.Find("Thieves");
            if (existing != null)
            {
                UpdateSpawner(existing.GetComponent<ThiefSpawner>());
                return;
            }

            bool wasDirty = scene.isDirty;
            var thieves = new GameObject("Thieves", typeof(ThiefSpawner));
            thieves.transform.SetParent(arena.transform, false);
            Undo.RegisterCreatedObjectUndo(thieves, "Setup Thieves");

            var data = new SerializedObject(thieves.GetComponent<ThiefSpawner>());
            data.FindProperty("playerSpawner").objectReferenceValue = Object.FindFirstObjectByType<UWBPlayerSpawner>(FindObjectsInactive.Include);
            data.FindProperty("bushScatter").objectReferenceValue = Object.FindFirstObjectByType<BushScatter>(FindObjectsInactive.Include);
            data.ApplyModifiedPropertiesWithoutUndo();
            UpdateSpawner(thieves.GetComponent<ThiefSpawner>());

            if (!wasDirty) EditorSceneManager.SaveScene(scene);
            Debug.Log("[ThiefSetup] Added Map/Arena/Thieves. Press Play and watch the Console.");
        }

        // Adds missing thief kinds and art to the spawner, leaving kinds and counts already there alone.
        private static void UpdateSpawner(ThiefSpawner spawner)
        {
            if (spawner == null) return;

            AssignIfMissing(spawner, "leafSprites", LoadSprites(LeafFolder, "Leaf_"), "leaf sprites to the bush burst");
            AssignIfMissing(spawner, "dustSprites", LoadSprites(DustFolder, "Dust_"), "dust sprites to the running dust");

            var data = new SerializedObject(spawner);
            SerializedProperty types = data.FindProperty("thiefTypes");
            bool changed = false;
            foreach (ThiefKind kind in Kinds)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<ThiefController>(kind.PrefabPath);
                if (prefab == null || HasType(types, prefab)) continue;

                int index = types.arraySize;
                types.arraySize = index + 1;
                types.GetArrayElementAtIndex(index).FindPropertyRelative("prefab").objectReferenceValue = prefab;
                types.GetArrayElementAtIndex(index).FindPropertyRelative("count").intValue = kind.Count;
                changed = true;
                Debug.Log($"[ThiefSetup] Added {kind.Count} {kind.Name} thieves to the spawner. Save the scene (Ctrl+S) to keep it.");
            }

            if (!changed) return;
            data.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        }

        private static bool HasType(SerializedProperty types, ThiefController prefab)
        {
            for (int i = 0; i < types.arraySize; i++)
            {
                if (types.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue == prefab) return true;
            }

            return false;
        }

        private static void AssignIfMissing(ThiefSpawner spawner, string propertyName, Sprite[] sprites, string description)
        {
            var data = new SerializedObject(spawner);
            SerializedProperty property = data.FindProperty(propertyName);
            if (sprites.Length == 0 || property.arraySize > 0) return;

            SetSprites(property, sprites);
            data.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
            Debug.Log($"[ThiefSetup] Assigned {sprites.Length} {description}. Save the scene (Ctrl+S) to keep it.");
        }

        // Loads the sprites named prefix* in folder, converting textures to sprites if needed.
        // pixelsPerUnit above zero is applied too.
        private static Sprite[] LoadSprites(string folder, string prefix, float pixelsPerUnit = 0f)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return new Sprite[0];

            var sprites = new System.Collections.Generic.List<Sprite>();
            foreach (string guid in AssetDatabase.FindAssets($"{prefix} t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!System.IO.Path.GetFileName(path).StartsWith(prefix)) continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                bool wrongType = importer != null && importer.textureType != TextureImporterType.Sprite;
                bool wrongSize = importer != null && pixelsPerUnit > 0f && !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit);
                if (wrongType || wrongSize)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    if (pixelsPerUnit > 0f) importer.spritePixelsPerUnit = pixelsPerUnit;
                    importer.SaveAndReimport();
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) sprites.Add(sprite);
            }

            sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites.ToArray();
        }

        private static void SetSprites(SerializedProperty property, Sprite[] sprites)
        {
            property.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }
        }

        // Creates the kind's prefab if needed and gives it the walk cycle, blink and breathing frames
        // once they exist. Does nothing when the prefab already has them.
        private static void UpdatePrefab(ThiefKind kind)
        {
            // "<Name>Walk_" matches only the walk frames; blinks are <Name>WalkHalf_ / <Name>WalkClosed_.
            Sprite[] frames = LoadSprites($"{kind.Folder}/Walk", $"{kind.Name}Walk_", kind.PixelsPerUnit);
            if (frames.Length == 0) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(kind.PrefabPath) == null) CreatePrefab(kind, frames[0]);

            GameObject root = PrefabUtility.LoadPrefabContents(kind.PrefabPath);
            try
            {
                SpriteRenderer body = root.GetComponentInChildren<SpriteRenderer>(true);
                if (body == null) return;

                ThiefFrameAnimation animation = body.GetComponent<ThiefFrameAnimation>();
                if (animation == null) animation = body.gameObject.AddComponent<ThiefFrameAnimation>();
                var data = new SerializedObject(animation);
                SerializedProperty runFrames = data.FindProperty("runFrames");
                SerializedProperty standing = data.FindProperty("standing");
                SerializedProperty idleFrames = data.FindProperty("idleFrames");
                Sprite[] idle = LoadSprites($"{kind.Folder}/{kind.IdleName}", $"{kind.Name}{kind.IdleName}_", kind.PixelsPerUnit);
                Sprite stand = idle.Length > 0 ? idle[0] : body.sprite;
                Sprite[] half = LoadSprites($"{kind.Folder}/Walk", $"{kind.Name}WalkHalf_", kind.PixelsPerUnit);
                Sprite[] closed = LoadSprites($"{kind.Folder}/Walk", $"{kind.Name}WalkClosed_", kind.PixelsPerUnit);
                SerializedProperty halfFrames = data.FindProperty("runFramesHalfBlink");
                SerializedProperty closedFrames = data.FindProperty("runFramesBlink");
                bool upToDate = runFrames.arraySize == frames.Length && runFrames.GetArrayElementAtIndex(0).objectReferenceValue == frames[0] &&
                    idleFrames.arraySize == idle.Length && standing.objectReferenceValue == stand &&
                    halfFrames.arraySize == half.Length && closedFrames.arraySize == closed.Length;
                if (upToDate) return;

                standing.objectReferenceValue = stand;
                SetSprites(idleFrames, idle);
                body.sprite = stand;
                body.transform.localScale = Vector3.one * (ThiefHeight / ReferenceUnits);
                data.FindProperty("cycleLength").floatValue = kind.CycleLength;
                SetSprites(runFrames, frames);
                SetSprites(halfFrames, half);
                SetSprites(closedFrames, closed);
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, kind.PrefabPath);
                Debug.Log($"[ThiefSetup] {kind.Name} prefab plays {frames.Length} walk frames (blinking: {closed.Length > 0}) and {idle.Length} breathing frames.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CreatePrefab(ThiefKind kind, Sprite sprite)
        {
            EnsureFolder(PrefabFolder);
            var root = new GameObject(kind.Name, typeof(ThiefController));
            var body = new GameObject("Body", typeof(SpriteRenderer));
            body.transform.SetParent(root.transform, false);
            // Lies flat for the floor camera, head toward the wall.
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = Vector3.one * (ThiefHeight / ReferenceUnits);
            var spriteRenderer = body.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            // Above bushes (5), below player markers (20).
            spriteRenderer.sortingOrder = 8;

            var data = new SerializedObject(root.GetComponent<ThiefController>());
            data.FindProperty("typeName").stringValue = kind.Name;
            data.FindProperty("body").objectReferenceValue = spriteRenderer;
            // All thieves are drawn walking toward the left.
            data.FindProperty("artFacesRight").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, kind.PrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }
    }
}
