using System.Linq;
using BountyHunter.Thieves;
using Fortal.Players;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BountyHunter.Editor
{
    // Ties each WANTED poster prefab to its thief and gives every poster the Money text made in
    // PostCat. Then adds a WantedBoard to Map/SideDisplay/Post and Map/Arena/Bounty (BountyRounds)
    // wired to the thieves, the posters, the stage timer and the player score tags. Runs by itself
    // after scripts load and is also on the menu. Only empty slots are filled, so Inspector changes stay.
    [InitializeOnLoad]
    public static class BountySetup
    {
        private const string ScenePath = "Assets/Scenes/BountyHunter.unity";
        private const string PosterFolder = "Assets/Prefabs/UI";
        private const string ThiefFolder = "Assets/Prefabs/Thieves";
        // The poster whose Money text the others copy.
        private const string MoneySource = "Cat";
        private const string MoneyName = "Money";
        private const string BombFrames = "Assets/ART/FX/Bomb/Frames";
        private const string StarSprite = "Assets/ART/BG/WallPlay/No1.png";
        private const string DamageFont = "Assets/Front/BebasNeue-Regular SDF.asset";
        private const string DamageMaterial = "Assets/Front/BebasNeue-Regular SDF - Outline.mat";
        private static readonly string[] Thieves = { "Chicken", "Cat", "Crow", "Rat" };

        static BountySetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Setup();
            };
        }

        [MenuItem("Tools/Bounty Hunter/Setup Bounty")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            foreach (string thief in Thieves) UpdatePoster(thief);

            var scene = EditorSceneManager.GetActiveScene();
            GameObject arena = GameObject.Find("Map/Arena");
            GameObject posts = GameObject.Find("Map/SideDisplay/Post");
            if (!scene.isLoaded || scene.path != ScenePath || arena == null || posts == null) return;

            bool wasDirty = scene.isDirty;
            bool changed = FillBoard(posts);
            changed |= FillRounds(arena.transform, posts.GetComponent<WantedBoard>());
            changed |= FillLeaderStar(posts.transform);
            if (!changed) return;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!wasDirty) EditorSceneManager.SaveScene(scene);
            Debug.Log("[BountySetup] WANTED posters and Map/Arena/Bounty are ready. Stage 1-1 shows one poster, 1-2 two, 1-3 three.");
        }

        private static string PosterPath(string thief)
        {
            return $"{PosterFolder}/Post{thief}.prefab";
        }

        private static void UpdatePoster(string thief)
        {
            string path = PosterPath(thief);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = AddMoney(root, thief);
                WantedPoster poster = root.GetComponent<WantedPoster>();
                if (poster == null)
                {
                    poster = root.AddComponent<WantedPoster>();
                    changed = true;
                }

                if (root.GetComponent<PosterMotion>() == null)
                {
                    root.AddComponent<PosterMotion>();
                    changed = true;
                }

                var data = new SerializedObject(poster);
                changed |= SetIfEmpty(data.FindProperty("thief"), AssetDatabase.LoadAssetAtPath<ThiefController>($"{ThiefFolder}/{thief}.prefab"));
                changed |= SetIfEmpty(data.FindProperty("money"), FindComponent<TMP_Text>(root.transform, MoneyName));
                if (!changed) return;

                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[BountySetup] Post{thief} is tied to the {thief} thief and shows its bounty.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Copies the Money text from the source poster, same place and style. Returns true when added.
        private static bool AddMoney(GameObject root, string thief)
        {
            if (thief == MoneySource || root.transform.Find(MoneyName) != null) return false;

            GameObject source = PrefabUtility.LoadPrefabContents(PosterPath(MoneySource));
            try
            {
                Transform money = source.transform.Find(MoneyName);
                if (money == null) return false;

                Object.Instantiate(money.gameObject, root.transform, false).name = MoneyName;
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(source);
            }
        }

        private static bool FillBoard(GameObject posts)
        {
            WantedBoard board = posts.GetComponent<WantedBoard>();
            bool changed = false;
            if (board == null)
            {
                board = Undo.AddComponent<WantedBoard>(posts);
                changed = true;
            }

            var data = new SerializedObject(board);
            WantedPoster[] posters = posts.GetComponentsInChildren<WantedPoster>(true);
            changed |= FillIfEmpty(data.FindProperty("posters"), posters);
            if (changed) data.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static bool FillRounds(Transform arena, WantedBoard board)
        {
            bool changed = false;
            Transform bounty = arena.Find("Bounty");
            if (bounty == null)
            {
                bounty = new GameObject("Bounty", typeof(BountyRounds)).transform;
                bounty.SetParent(arena, false);
                Undo.RegisterCreatedObjectUndo(bounty.gameObject, "Setup Bounty");
                changed = true;
            }

            var rounds = bounty.GetComponent<BountyRounds>();
            var data = new SerializedObject(rounds);
            changed |= SetIfEmpty(data.FindProperty("spawner"), FindComponent<ThiefSpawner>(arena, "Thieves"));
            changed |= SetIfEmpty(data.FindProperty("board"), board);
            changed |= SetIfEmpty(data.FindProperty("timer"), Object.FindFirstObjectByType<StageTimer>(FindObjectsInactive.Include));
            PlayerScoreTag[] tags = Object.FindObjectsByType<PlayerScoreTag>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(tag => tag.PlayerNumber).ToArray();
            changed |= FillIfEmpty(data.FindProperty("scoreTags"), tags);
            changed |= SetIfEmpty(data.FindProperty("damageFont"), AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DamageFont));
            changed |= SetIfEmpty(data.FindProperty("damageMaterial"), AssetDatabase.LoadAssetAtPath<Material>(DamageMaterial));
            if (changed) data.ApplyModifiedPropertiesWithoutUndo();

            BombSquad squad = FillBombs(arena, out bool bombsChanged);
            bool bombsLinked = SetIfEmpty(data.FindProperty("bombs"), squad);
            if (bombsLinked) data.ApplyModifiedPropertiesWithoutUndo();
            changed |= bombsChanged || bombsLinked;

            MapSwitcher switcher = FindComponent<MapSwitcher>(arena, "Maps");
            if (switcher == null) return changed;

            var switcherData = new SerializedObject(switcher);
            if (!SetIfEmpty(switcherData.FindProperty("rounds"), rounds)) return changed;
            switcherData.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // Adds Map/Arena/Bombs (BombSquad) wired to the thieves, the players and the bomb frames.
        private static BombSquad FillBombs(Transform arena, out bool changed)
        {
            changed = false;
            Transform holder = arena.Find("Bombs");
            if (holder == null)
            {
                holder = new GameObject("Bombs", typeof(BombSquad)).transform;
                holder.SetParent(arena, false);
                Undo.RegisterCreatedObjectUndo(holder.gameObject, "Setup Bombs");
                changed = true;
            }

            var squad = holder.GetComponent<BombSquad>();
            var data = new SerializedObject(squad);
            bool filled = SetIfEmpty(data.FindProperty("thieves"), FindComponent<ThiefSpawner>(arena, "Thieves"));
            filled |= SetIfEmpty(data.FindProperty("playerSpawner"), Object.FindFirstObjectByType<UWBPlayerSpawner>(FindObjectsInactive.Include));
            filled |= FillIfEmpty(data.FindProperty("art.fuse"), ThiefSetup.LoadSprites(BombFrames, "Bomb_Fuse_"));
            filled |= FillIfEmpty(data.FindProperty("art.squash"), ThiefSetup.LoadSprites(BombFrames, "Bomb_Squash_"));
            // Refilled whenever the frames on disk change, since they were re-cut once already.
            filled |= ReplaceIfDifferent(data.FindProperty("art.boom"), ThiefSetup.LoadSprites(BombFrames, "Bomb_Boom_"));
            if (filled) data.ApplyModifiedPropertiesWithoutUndo();
            changed |= filled;
            return squad;
        }

        // Adds Map/SideDisplay/Leader Star (LeaderStar) just above the posters, showing No1.png and
        // watching every player score tag.
        private static bool FillLeaderStar(Transform posts)
        {
            bool changed = false;
            Transform side = posts.parent;
            Transform holder = side.Find("Leader Star");
            if (holder == null)
            {
                var star = new GameObject("Leader Star", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(CanvasGroup), typeof(LeaderStar));
                holder = star.transform;
                holder.SetParent(side, false);
                holder.SetSiblingIndex(posts.GetSiblingIndex() + 1);
                var image = star.GetComponent<UnityEngine.UI.Image>();
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(StarSprite);
                image.preserveAspect = true;
                image.raycastTarget = false;
                // Hidden until Play gives someone the lead, so it does not cover the posters in the Scene view.
                star.GetComponent<CanvasGroup>().alpha = 0f;
                Undo.RegisterCreatedObjectUndo(star, "Setup Leader Star");
                changed = true;
            }

            var data = new SerializedObject(holder.GetComponent<LeaderStar>());
            PlayerScoreTag[] tags = Object.FindObjectsByType<PlayerScoreTag>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(tag => tag.PlayerNumber).ToArray();
            if (FillIfEmpty(data.FindProperty("tags"), tags))
            {
                data.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            return changed;
        }

        private static T FindComponent<T>(Transform parent, string path) where T : Component
        {
            Transform child = parent.Find(path);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static bool SetIfEmpty(SerializedProperty property, Object value)
        {
            if (property == null || property.objectReferenceValue != null || value == null) return false;
            property.objectReferenceValue = value;
            return true;
        }

        private static bool ReplaceIfDifferent(SerializedProperty property, Object[] values)
        {
            if (property == null || values.Length == 0) return false;

            bool same = property.arraySize == values.Length;
            for (int i = 0; same && i < values.Length; i++)
            {
                same = property.GetArrayElementAtIndex(i).objectReferenceValue == values[i];
            }

            if (same) return false;
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            return true;
        }

        private static bool FillIfEmpty(SerializedProperty property, Object[] values)
        {
            if (property == null || property.arraySize > 0 || values.Length == 0) return false;
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            return true;
        }
    }
}
