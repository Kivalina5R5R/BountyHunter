using BountyHunter.Thieves;
using Fortal.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BountyHunter.Editor
{
    // Builds one prefab per passer-by (the sloth first) from its frames in Assets/ART/NPC/<Name>
    // (cut from <Name>_Slow_Walk_Loop.mp4 by Tools/extract_thief_video.py: Walk/<Name>Walk_XX and
    // Idle/<Name>Idle_XX), and adds Map/Arena/NPCs (NpcCrowd) to the game scene with every kind,
    // handed to BountyRounds so they stroll during the rounds and grabbing one costs points. Runs
    // after scripts load and is on the menu; only what is missing is added. A new passer-by needs
    // its frames and one line in Kinds. Also fills the scuffle played when one is grabbed by mistake.
    [InitializeOnLoad]
    public static class NpcSetup
    {
        private const string ScenePath = "Assets/Scenes/BountyHunter.unity";
        private const string PrefabFolder = "Assets/Prefabs/NPC";
        private const string BrawlFolder = "Assets/ART/FX/Brawl";
        // The pieces only the mistaken-arrest scuffle has (drawn by Codex): OOPS! / WRONG!, a question
        // mark, a sweat drop and the red X. None belongs to one character, so they suit every passer-by.
        private const string MistakeFolder = BrawlFolder + "/Mistake";
        // Like the thieves: the art is sized to ReferenceUnits tall, then scaled to its height in metres.
        private const float ReferenceUnits = 13.83f;

        private sealed class NpcKind
        {
            public string Name { get; set; }
            // Measured walking height in pixels / ReferenceUnits.
            public float PixelsPerUnit { get; set; }
            // Height on the floor in metres (the thieves are 0.6, a player marker 0.665).
            public float Height { get; set; }
            // World distance of one walk cycle: the video's cycle seconds x the walk speed.
            public float CycleLength { get; set; }
            public float WalkSpeed { get; set; }
            // Which way the face in the art looks; it is mirrored to face where it walks.
            public bool ArtFacesRight { get; set; }

            public string Folder => $"Assets/ART/NPC/{Name}";
            public string PrefabPath => $"{PrefabFolder}/{Name}.prefab";
        }

        // The sloth: 892 pixels tall walking; a slow 4-second cycle at 0.3 m/s; looks to the right.
        private static readonly NpcKind[] Kinds =
        {
            new NpcKind { Name = "Sloth", PixelsPerUnit = 64.5f, Height = 0.75f, CycleLength = 1.2f, WalkSpeed = 0.3f, ArtFacesRight = true },
        };

        static NpcSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Setup();
            };
        }

        [MenuItem("Tools/Bounty Hunter/Setup NPCs")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var prefabs = new System.Collections.Generic.List<NpcWalker>();
            foreach (NpcKind kind in Kinds)
            {
                NpcWalker prefab = BuildPrefab(kind);
                if (prefab != null) prefabs.Add(prefab);
            }

            var scene = EditorSceneManager.GetActiveScene();
            GameObject arena = GameObject.Find("Map/Arena");
            if (!scene.isLoaded || scene.path != ScenePath || arena == null || prefabs.Count == 0) return;
            bool wasDirty = scene.isDirty;
            Transform existing = arena.transform.Find("NPCs");
            if (existing != null)
            {
                if (!FillMistakeBrawl(existing.GetComponent<NpcCrowd>())) return;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!wasDirty) EditorSceneManager.SaveScene(scene);
                return;
            }

            var holder = new GameObject("NPCs", typeof(NpcCrowd));
            holder.transform.SetParent(arena.transform, false);
            Undo.RegisterCreatedObjectUndo(holder, "Setup NPCs");

            var data = new SerializedObject(holder.GetComponent<NpcCrowd>());
            data.FindProperty("playerSpawner").objectReferenceValue = Object.FindFirstObjectByType<UWBPlayerSpawner>(FindObjectsInactive.Include);
            SetList(data.FindProperty("kinds"), prefabs.ToArray());
            // The same floor area as the thieves.
            var thieves = Object.FindFirstObjectByType<ThiefSpawner>(FindObjectsInactive.Include);
            if (thieves != null)
            {
                var spawner = new SerializedObject(thieves);
                data.FindProperty("areaMin").vector2Value = spawner.FindProperty("areaMin").vector2Value;
                data.FindProperty("areaMax").vector2Value = spawner.FindProperty("areaMax").vector2Value;
            }

            data.ApplyModifiedPropertiesWithoutUndo();
            FillMistakeBrawl(holder.GetComponent<NpcCrowd>());

            var rounds = Object.FindFirstObjectByType<BountyRounds>(FindObjectsInactive.Include);
            if (rounds != null)
            {
                var roundData = new SerializedObject(rounds);
                roundData.FindProperty("npcs").objectReferenceValue = holder.GetComponent<NpcCrowd>();
                roundData.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!wasDirty) EditorSceneManager.SaveScene(scene);
            Debug.Log("[NpcSetup] Map/Arena/NPCs is ready: the sloth strolls the floor during the rounds; grabbing it costs 5$.");
        }

        // The kind's prefab, made once its frames are in: root with NpcWalker, a Body lying flat on the
        // floor with the walk and breathing frames.
        private static NpcWalker BuildPrefab(NpcKind kind)
        {
            var existing = AssetDatabase.LoadAssetAtPath<NpcWalker>(kind.PrefabPath);
            if (existing != null) return existing;

            Sprite[] walk = ThiefSetup.LoadSprites($"{kind.Folder}/Walk", $"{kind.Name}Walk_", kind.PixelsPerUnit);
            Sprite[] idle = ThiefSetup.LoadSprites($"{kind.Folder}/Idle", $"{kind.Name}Idle_", kind.PixelsPerUnit);
            if (walk.Length == 0) return null;

            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "NPC");
            var root = new GameObject(kind.Name, typeof(NpcWalker));
            try
            {
                var body = new GameObject("Body", typeof(SpriteRenderer));
                body.transform.SetParent(root.transform, false);
                // Lies flat for the floor camera, head toward the wall, like the thieves.
                body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                body.transform.localScale = Vector3.one * (kind.Height / ReferenceUnits);
                var picture = body.GetComponent<SpriteRenderer>();
                picture.sprite = idle.Length > 0 ? idle[0] : walk[0];
                // With the thieves: above bushes (5), below player markers (20).
                picture.sortingOrder = 8;

                var animation = new SerializedObject(body.AddComponent<ThiefFrameAnimation>());
                animation.FindProperty("standing").objectReferenceValue = picture.sprite;
                SetList(animation.FindProperty("idleFrames"), idle);
                SetList(animation.FindProperty("runFrames"), walk);
                animation.FindProperty("cycleLength").floatValue = kind.CycleLength;
                animation.ApplyModifiedPropertiesWithoutUndo();

                var walker = new SerializedObject(root.GetComponent<NpcWalker>());
                walker.FindProperty("displayName").stringValue = kind.Name;
                walker.FindProperty("body").objectReferenceValue = picture;
                walker.FindProperty("walkSpeed").floatValue = kind.WalkSpeed;
                walker.FindProperty("artFacesRight").boolValue = kind.ArtFacesRight;
                walker.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, kind.PrefabPath);
                Debug.Log($"[NpcSetup] Made {kind.PrefabPath}: {walk.Length} walk frames, {idle.Length} breathing frames.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            return AssetDatabase.LoadAssetAtPath<NpcWalker>(kind.PrefabPath);
        }

        // The thieves' fight cloud, puffs, swooshes, dog's paw and handcuffs with the mistake's own
        // pieces: question marks and sweat for stars, OOPS! / WRONG! and the red X. Fills only empty
        // groups, so pieces swapped in the Inspector stay; the glove tried first (or its file, now
        // gone) gives way to the handcuffs. True when anything was filled.
        private static bool FillMistakeBrawl(NpcCrowd crowd)
        {
            if (crowd == null) return false;

            var data = new SerializedObject(crowd);
            bool changed = false;
            changed |= FillIfEmpty(data, "clouds", ThiefSetup.LoadSprites(BrawlFolder, "Brawl_Cloud_"));
            changed |= FillIfEmpty(data, "puffs", ThiefSetup.LoadSprites(BrawlFolder, "Brawl_Puff_"));
            changed |= FillIfEmpty(data, "swooshes", ThiefSetup.LoadSprites(BrawlFolder, "Brawl_Swoosh"));
            Sprite[] cuffs = ThiefSetup.LoadSprites(BrawlFolder, "Brawl_Cuffs");
            changed |= FillIfEmpty(data, "limbs", Join(ThiefSetup.LoadSprites(BrawlFolder, "Brawl_DogPaw"), cuffs));
            changed |= SwapGlove(data.FindProperty("mistakeBrawl.limbs"), cuffs);
            changed |= FillIfEmpty(data, "stars", Join(ThiefSetup.LoadSprites(MistakeFolder, "Mistake_Question"), ThiefSetup.LoadSprites(MistakeFolder, "Mistake_Sweat")));
            changed |= FillIfEmpty(data, "words", ThiefSetup.LoadSprites(MistakeFolder, "Mistake_Word_"));
            changed |= FillIfEmpty(data, "finale", ThiefSetup.LoadSprites(MistakeFolder, "Mistake_X"));
            if (!changed) return false;

            data.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[NpcSetup] The mistaken-arrest scuffle has its pieces (OOPS! / WRONG!, paw and handcuffs, question marks, sweat, red X).");
            return true;
        }

        // The glove (named Mistake_Glove, or missing once its file was removed) becomes the handcuffs.
        private static bool SwapGlove(SerializedProperty limbs, Sprite[] cuffs)
        {
            if (limbs == null || cuffs.Length == 0) return false;

            bool changed = false;
            for (int i = 0; i < limbs.arraySize; i++)
            {
                SerializedProperty limb = limbs.GetArrayElementAtIndex(i);
                Object sprite = limb.objectReferenceValue;
                if (sprite != null && !sprite.name.StartsWith("Mistake_")) continue;
                limb.objectReferenceValue = cuffs[0];
                changed = true;
            }

            return changed;
        }

        private static bool FillIfEmpty(SerializedObject data, string group, Sprite[] sprites)
        {
            SerializedProperty list = data.FindProperty($"mistakeBrawl.{group}");
            if (list == null || list.arraySize > 0 || sprites.Length == 0) return false;
            SetList(list, sprites);
            return true;
        }

        private static Sprite[] Join(Sprite[] a, Sprite[] b)
        {
            var all = new Sprite[a.Length + b.Length];
            a.CopyTo(all, 0);
            b.CopyTo(all, a.Length);
            return all;
        }

        private static void SetList<T>(SerializedProperty list, T[] items) where T : Object
        {
            list.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }
    }
}
