using System.Collections.Generic;
using System.IO;
using System.Linq;
using BountyHunter.Thieves;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Editor
{
    // Builds the result screen from Assets/ART/BG/Result, laid out like the reference shot: the board
    // in the middle with the logo, TIME'S UP, WINNER and the four standings, the winner dancing on the
    // bench to its right and the others clapping on both sides, over the game wall's station.
    // - Animation clips from the frames in Result/Sprites (one per folder, 30 fps, looping) and one
    //   animator per dog with a Dance and a Clap state, in Result/Animations. A clip whose frame count
    //   no longer matches its folder (the frames were made again) is refilled.
    // - Assets/Prefabs/UI/Result.prefab, made once; adjust it there.
    // - An instance on the wall (Map/SideDisplay/Result) and Map/Result Sequence, which brings it up
    //   when the time is up.
    // Runs after scripts load and is on the menu; only what is missing is added.
    [InitializeOnLoad]
    public static class ResultSetup
    {
        private const string ScenePath = "Assets/Scenes/BountyHunter.unity";
        private const string Folder = "Assets/ART/BG/Result";
        private const string Frames = Folder + "/Sprites";
        private const string Animations = Folder + "/Animations";
        private const string PrefabPath = "Assets/Prefabs/UI/Result.prefab";
        private const string Font = "Assets/Front/BebasNeue-Regular SDF.asset";
        // The intro logo's light band and star sparkle, reused so both screens shine alike.
        private const string ShineTexture = "Assets/ART/Intro/Parts/Logo_Shine.png";
        private const string GlintTexture = "Assets/ART/Intro/Parts/Glint.png";
        private const float FramesPerSecond = 30f;
        private static readonly Color TextColor = new Color(1f, 0.93f, 0.85f, 1f);
        // The winner's row stands out in orange from the cream of the others.
        private static readonly Color FirstPlaceColor = new Color(1f, 0.6f, 0.16f, 1f);

        // Character (as picked on the floor), the dog in the frame folders, its face in the rows and its
        // size on the wall per frame pixel. The sizes make each dog as tall as in the intro's idle
        // (measured: Shiba 251 units, Saibirian 235) from its frames' height. Pom (174 there) and Chiwawa
        // (124) sat further back and are made bigger here (200 and 146), Pom clearly bigger than Chiwawa.
        private static readonly (string Character, string Dog, string Face, float Size)[] Dogs =
        {
            ("Rex", "Shiba", "Layer 506", 0.592f),
            ("Tedly", "Saibirian", "Layer 505", 0.593f),
            ("Pompo", "Pom", "Layer 504-1", 0.47f),
            ("Simba", "Chiwawa", "Layer 504", 0.365f),
        };

        // The layout is measured on the reference shot (1687 x 453 pixels, the wall picture's shape)
        // and scaled onto the wall picture, which is 435 units high.
        private const float RefScale = 435f / 453f;
        private static readonly Vector2 RefCentre = new Vector2(843.5f, 226.5f);
        private const float WallAspect = 8192f / 2160f;

        // Where each piece sits on the reference shot (its centre, or the feet for a dog) and how wide.
        private static readonly Vector2 BoardAt = new Vector2(852f, 230f);
        private const float BoardShadowWidth = 740f;
        private const float BoardWidth = 690f;
        private static readonly Vector2 LogoAt = new Vector2(612f, 110f);
        private const float LogoWidth = 200f;
        private static readonly Vector2 TimeUpAt = new Vector2(858f, 68f);
        private const float TimeUpWidth = 200f;
        private static readonly Vector2 WinnerTitleAt = new Vector2(832f, 145f);
        private const float WinnerTitleWidth = 255f;

        // Row 1 (the winner) and rows 2 to 4: the line behind, then the rank, name and score across it.
        private static readonly Vector2 FirstRowAt = new Vector2(828f, 216f);
        private const float FirstRowWidth = 290f;
        private static readonly float[] FirstRowColumns = { 735f, 823f, 925f };
        private static readonly float[] FirstRowFontSizes = { 30f, 38f, 52f };
        private static readonly float[] OtherRowHeights = { 270f, 322f, 376f };
        private const float OtherRowX = 783f;
        private const float OtherRowWidth = 270f;
        private const float FaceX = 627f;
        private const float FaceWidth = 56f;
        private static readonly float[] OtherRowColumns = { 693f, 772f, 870f };
        private static readonly float[] OtherRowFontSizes = { 25f, 30f, 40f };

        private static readonly Vector2 BenchAt = new Vector2(1058f, 426f);
        private const float BenchWidth = 205f;
        private static readonly Vector2 WinnerFeet = new Vector2(1062f, 404f);
        // The clapping dogs for 2nd, 3rd and 4th place: their feet, and the dog shown in the prefab.
        private static readonly (string Name, Vector2 Feet, string PreviewDog)[] Clappers =
        {
            ("Clapper Right", new Vector2(1340f, 450f), "Saibirian"),
            ("Clapper Left", new Vector2(290f, 450f), "Pom"),
            ("Clapper Middle", new Vector2(455f, 450f), "Chiwawa"),
        };
        // The spots' scales of the first layout, before the dogs had their own sizes.
        private static readonly float[] FirstSpotScales = { 0.58f, 0.52f, 0.40f, 0.44f };

        // Game wall pieces that make way for the result, from Map/SideDisplay/Play.
        private static readonly string[] HiddenOnResult =
        {
            "Background/Board", "Background/BG_Time", "Post", "Leader Star", "Pom", "Shiba", "Saibirian", "Chiwawa",
        };

        static ResultSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Setup();
            };
        }

        [MenuItem("Tools/Bounty Hunter/Setup Result Screen")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!AssetDatabase.IsValidFolder(Frames)) return;

            Dictionary<string, RuntimeAnimatorController> animators = BuildAnimators();
            // Waits until every dog's frames are in (they may still be importing).
            if (animators.Count < Dogs.Length) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) BuildPrefab(animators);
            AddIdleToPrefab();
            AddShineToPrefab();
            RestyleBoard();
            QuietIcons();
            PomAndFirstPlace();

            var scene = EditorSceneManager.GetActiveScene();
            GameObject wall = GameObject.Find("Map/SideDisplay");
            if (!scene.isLoaded || scene.path != ScenePath || wall == null) return;

            bool wasDirty = scene.isDirty;
            bool changed = PlaceOnWall(wall.transform, out ResultScreen screen);
            changed |= BuildSequence(wall.transform, screen);
            changed |= LinkMapSwitcher();
            if (!changed) return;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!wasDirty) EditorSceneManager.SaveScene(scene);
            Debug.Log("[ResultSetup] The result screen is on the wall (Map/SideDisplay/Result). Press R in Play to bring it up.");
        }

        // One animator per dog, keyed by the character's name.
        private static Dictionary<string, RuntimeAnimatorController> BuildAnimators()
        {
            var animators = new Dictionary<string, RuntimeAnimatorController>();
            if (!AssetDatabase.IsValidFolder(Animations)) AssetDatabase.CreateFolder(Folder, "Animations");

            foreach ((string character, string dog, string _, float _) in Dogs)
            {
                AnimationClip dance = BuildClip($"{dog}_Dance");
                AnimationClip clap = BuildClip($"{dog}_Clap");
                if (dance == null || clap == null) continue;

                string path = $"{Animations}/{dog}.controller";
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null)
                {
                    controller = AnimatorController.CreateAnimatorControllerAtPath(path);
                    AnimatorStateMachine states = controller.layers[0].stateMachine;
                    states.AddState("Dance").motion = dance;
                    states.AddState("Clap").motion = clap;
                }

                animators[character] = controller;
            }

            return animators;
        }

        // A looping clip that swaps the Image's sprite through the frames in Sprites/<name>.
        private static AnimationClip BuildClip(string name)
        {
            Sprite[] frames = LoadFrames(name);
            if (frames.Length == 0) return null;

            string path = $"{Animations}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            var binding = EditorCurveBinding.PPtrCurve("", typeof(Image), "m_Sprite");
            if (clip != null && AnimationUtility.GetObjectReferenceCurve(clip, binding)?.Length == frames.Length + 1) return clip;

            bool created = clip == null;
            if (created) clip = new AnimationClip { name = name };
            clip.frameRate = FramesPerSecond;

            // The last frame is keyed again at the end so it shows as long as the others before the loop.
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < frames.Length; i++)
            {
                keys[i] = new ObjectReferenceKeyframe { time = i / FramesPerSecond, value = frames[i] };
            }

            keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / FramesPerSecond, value = frames[frames.Length - 1] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (created) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ResultSetup] {path}: {frames.Length} frames");
            return clip;
        }

        private static Sprite[] LoadFrames(string name)
        {
            string folder = $"{Frames}/{name}";
            if (!AssetDatabase.IsValidFolder(folder)) return new Sprite[0];

            return Directory.GetFiles(folder, $"{name}_*.png")
                .Select(file => file.Replace('\\', '/'))
                .OrderBy(file => file, System.StringComparer.Ordinal)
                .Select(LoadSprite)
                .Where(sprite => sprite != null)
                .ToArray();
        }

        private static void BuildPrefab(Dictionary<string, RuntimeAnimatorController> animators)
        {
            var root = new GameObject("Result", typeof(RectTransform), typeof(ResultScreen));
            try
            {
                Stretch((RectTransform)root.transform);

                // Shaped like the wall picture and covering the wall the same way, so the layout
                // stays on the station behind it.
                var layout = new GameObject("Layout", typeof(RectTransform), typeof(AspectRatioFitter));
                layout.transform.SetParent(root.transform, false);
                var fitter = layout.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = WallAspect;

                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Font);
                var entrance = new List<RectTransform>();

                RectTransform board = Group(layout.transform, "Board", Ref(BoardAt));
                entrance.Add(board);
                Picture(board, "Board Shadow", "Bg Bord", BoardAt, BoardAt, BoardShadowWidth);
                Picture(board, "Board Frame", "Bord", BoardAt, BoardAt, BoardWidth);
                RectTransform timeUp = Picture(board, "Time Up", "Time UP", BoardAt, TimeUpAt, TimeUpWidth);
                RectTransform logo = Picture(board, "Logo", "Logo", BoardAt, LogoAt, LogoWidth);
                RectTransform title = Picture(board, "Winner Title", "Winer", BoardAt, WinnerTitleAt, WinnerTitleWidth);
                entrance.Add(timeUp);
                entrance.Add(logo);
                entrance.Add(title);

                var rows = new List<(RectTransform Root, Image Face, TMP_Text Rank, TMP_Text Name, TMP_Text Score)>();
                // Filled in the order of Dogs for the preview, as in the reference shot.
                rows.Add(BuildRow(board, "Row 1", FirstRowAt, FirstRowWidth, FirstRowColumns, FirstRowFontSizes, null, font, 1, Dogs[0].Character));
                for (int i = 0; i < OtherRowHeights.Length; i++)
                {
                    var at = new Vector2(OtherRowX, OtherRowHeights[i]);
                    rows.Add(BuildRow(board, $"Row {i + 2}", at, OtherRowWidth, OtherRowColumns, OtherRowFontSizes, Dogs[i + 1].Face, font, i + 2, Dogs[i + 1].Character));
                }

                entrance.AddRange(rows.Select(row => row.Root));

                RectTransform podium = Group(layout.transform, "Podium", Ref(BenchAt));
                Picture(podium, "Bench", "Tabel", BenchAt, BenchAt, BenchWidth);
                Animator winner = Dog(podium, "Winner", Ref(WinnerFeet) - Ref(BenchAt), Dogs[0].Size, animators["Rex"], "Shiba_Dance");
                entrance.Add(podium);

                var clappers = new List<Animator>();
                foreach ((string name, Vector2 feet, string dog) in Clappers)
                {
                    var look = Dogs.First(entry => entry.Dog == dog);
                    Animator clapper = Dog(layout.transform, name, Ref(feet), look.Size, animators[look.Character], $"{dog}_Clap");
                    clappers.Add(clapper);
                    entrance.Add((RectTransform)clapper.transform);
                }

                var data = new SerializedObject(root.GetComponent<ResultScreen>());
                SerializedProperty looks = data.FindProperty("looks");
                looks.arraySize = Dogs.Length;
                for (int i = 0; i < Dogs.Length; i++)
                {
                    SerializedProperty look = looks.GetArrayElementAtIndex(i);
                    look.FindPropertyRelative("character").stringValue = Dogs[i].Character;
                    look.FindPropertyRelative("animations").objectReferenceValue = animators[Dogs[i].Character];
                    look.FindPropertyRelative("face").objectReferenceValue = LoadSprite($"{Folder}/{Dogs[i].Face}.png");
                    look.FindPropertyRelative("size").floatValue = Dogs[i].Size;
                }

                SerializedProperty rowList = data.FindProperty("rows");
                rowList.arraySize = rows.Count;
                for (int i = 0; i < rows.Count; i++)
                {
                    SerializedProperty row = rowList.GetArrayElementAtIndex(i);
                    row.FindPropertyRelative("root").objectReferenceValue = rows[i].Root.gameObject;
                    row.FindPropertyRelative("face").objectReferenceValue = rows[i].Face;
                    row.FindPropertyRelative("rank").objectReferenceValue = rows[i].Rank;
                    row.FindPropertyRelative("name").objectReferenceValue = rows[i].Name;
                    row.FindPropertyRelative("score").objectReferenceValue = rows[i].Score;
                }

                data.FindProperty("winner").objectReferenceValue = winner;
                SetList(data.FindProperty("clappers"), clappers);
                SetList(data.FindProperty("entrance"), entrance);
                data.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[ResultSetup] Made {PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Gives the board's pieces their idle motion once (a prefab without any): WINNER beats like a
        // heart with row 1, and the logo and TIME'S UP only drift and breathe a touch, so they stay
        // quiet. Rows 2 to 4 have a light sweep instead (AddShineToPrefab).
        private static void AddIdleToPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var data = new SerializedObject(root.GetComponent<ResultScreen>());
                SerializedProperty idle = data.FindProperty("idle");
                if (idle == null || idle.arraySize > 0) return;

                foreach (BoardMotion motion in BoardMotions)
                {
                    AddMotion(idle, root.transform.Find(motion.Path), motion);
                }

                AddMotion(idle, root.transform.Find("Layout/Board/Row 1"), new BoardMotion("Layout/Board/Row 1", IdleMotion.Style.Wave, Vector2.zero, 0.025f, 0f, 1.2f, 0.1f));

                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[ResultSetup] {PrefabPath}: the board's pieces now move gently once they are in.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Pieces that get a band of light sweeping across them: the path, when the first sweep comes
        // (after the piece has arrived; the rows one after another, top to bottom) and how bright.
        private static readonly (string Path, float Delay, float Strength)[] Shines =
        {
            ("Layout/Board/Row 1", 3f, 0.3f),
            ("Layout/Board/Row 2", 3.15f, 0.3f),
            ("Layout/Board/Row 3", 3.3f, 0.3f),
            ("Layout/Board/Row 4", 3.45f, 0.3f),
        };

        // The stars that glint: the picture, the star's box on it ((0, 0) bottom left, measured), the
        // first glint and how often.
        private static readonly (string Path, Rect Star, float Delay, float Every)[] Glints =
        {
            ("Layout/Board/Winner Title", Rect.MinMaxRect(0.013f, 0.057f, 0.262f, 0.955f), 1.8f, 3.1f),
        };

        // Adds the light sweeps and star glints once, and drops the first idle motions (the floating
        // rows, the swaying logo, the breathing TIME'S UP) where they were never changed by hand.
        private static void AddShineToPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                bool changed = DropFirstIdle(root.GetComponent<ResultScreen>());
                var shine = AssetDatabase.LoadAssetAtPath<Texture2D>(ShineTexture);
                var glint = AssetDatabase.LoadAssetAtPath<Texture2D>(GlintTexture);
                foreach ((string path, float delay, float strength) in Shines)
                {
                    var piece = root.transform.Find(path) as RectTransform;
                    if (piece == null || piece.Find("Shine Mask") != null || shine == null) continue;
                    AddShine(piece, shine, delay, strength);
                    changed = true;
                }

                foreach ((string path, Rect star, float delay, float every) in Glints)
                {
                    var piece = root.transform.Find(path) as RectTransform;
                    if (piece == null || piece.Find("Star Glint") != null || glint == null) continue;
                    AddGlint(piece, glint, star, delay, every);
                    changed = true;
                }

                if (!changed) return;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[ResultSetup] {PrefabPath}: light now sweeps across the board and its stars glint.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // A copy of the piece's picture as a Mask (not drawn itself) with the light band inside, laid
        // first so the piece's texts stay over the light.
        private static void AddShine(RectTransform piece, Texture2D texture, float delay, float strength)
        {
            var picture = piece.GetComponent<Image>();
            var mask = new GameObject("Shine Mask", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ShineSweep));
            mask.transform.SetParent(piece, false);
            mask.transform.SetAsFirstSibling();
            Stretch((RectTransform)mask.transform);
            var shape = mask.GetComponent<Image>();
            shape.sprite = picture != null ? picture.sprite : null;
            shape.preserveAspect = picture != null && picture.preserveAspect;
            shape.raycastTarget = false;
            mask.GetComponent<Mask>().showMaskGraphic = false;

            var band = new GameObject("Shine", typeof(RectTransform), typeof(RawImage));
            band.transform.SetParent(mask.transform, false);
            var bandRect = (RectTransform)band.transform;
            bandRect.anchorMin = new Vector2(0.4f, -0.35f);
            bandRect.anchorMax = new Vector2(0.6f, 1.35f);
            bandRect.offsetMin = Vector2.zero;
            bandRect.offsetMax = Vector2.zero;
            bandRect.localRotation = Quaternion.Euler(0f, 0f, -20f);
            var light = band.GetComponent<RawImage>();
            light.texture = texture;
            light.color = new Color(1f, 1f, 1f, strength);
            light.raycastTarget = false;
            band.SetActive(false);

            var data = new SerializedObject(mask.GetComponent<ShineSweep>());
            data.FindProperty("band").objectReferenceValue = bandRect;
            data.FindProperty("delaySeconds").floatValue = delay;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // A sparkle over the star, a little bigger than it, on top of the piece.
        private static void AddGlint(RectTransform piece, Texture2D texture, Rect star, float delay, float every)
        {
            var sparkle = new GameObject("Star Glint", typeof(RectTransform), typeof(RawImage), typeof(Twinkle));
            sparkle.transform.SetParent(piece, false);
            var rect = (RectTransform)sparkle.transform;
            rect.anchorMin = rect.anchorMax = star.center;
            float size = Mathf.Max(star.width * piece.rect.width, star.height * piece.rect.height) * 1.3f;
            rect.sizeDelta = new Vector2(size, size);
            rect.localScale = Vector3.zero;
            var picture = sparkle.GetComponent<RawImage>();
            picture.texture = texture;
            picture.raycastTarget = false;

            var data = new SerializedObject(sparkle.GetComponent<Twinkle>());
            data.FindProperty("delaySeconds").floatValue = delay;
            data.FindProperty("everySeconds").floatValue = every;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // Removes the floating of rows 2 to 4 and TIME'S UP's breathing, and turns the logo's sway into
        // a breath, but only entries still at the values first made.
        private static bool DropFirstIdle(ResultScreen screen)
        {
            if (screen == null) return false;

            var data = new SerializedObject(screen);
            SerializedProperty idle = data.FindProperty("idle");
            bool changed = false;
            for (int i = idle.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty motion = idle.GetArrayElementAtIndex(i);
                var piece = motion.FindPropertyRelative("piece").objectReferenceValue as Transform;
                Vector2 bob = motion.FindPropertyRelative("bob").vector2Value;
                float pulse = motion.FindPropertyRelative("pulse").floatValue;
                float sway = motion.FindPropertyRelative("sway").floatValue;
                string name = piece != null ? piece.name : "";

                bool floatingRow = name.StartsWith("Row ") && name != "Row 1" && bob == new Vector2(0f, 2.5f) && pulse == 0f;
                bool breathingTimeUp = name == "Time Up" && bob == new Vector2(0f, 2f) && Mathf.Approximately(pulse, 0.03f);
                if (floatingRow || breathingTimeUp)
                {
                    idle.DeleteArrayElementAtIndex(i);
                    changed = true;
                    continue;
                }

                if (name == "Logo" && bob == new Vector2(0f, 3f) && Mathf.Approximately(sway, 2.5f))
                {
                    motion.FindPropertyRelative("bob").vector2Value = Vector2.zero;
                    motion.FindPropertyRelative("sway").floatValue = 0f;
                    motion.FindPropertyRelative("pulse").floatValue = 0.012f;
                    changed = true;
                }
            }

            if (changed) data.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        // An idle motion for one of the board's pieces.
        private readonly struct BoardMotion
        {
            public BoardMotion(string path, IdleMotion.Style style, Vector2 bob, float pulse, float sway, float seconds, float phase)
            {
                Path = path;
                Style = style;
                Bob = bob;
                Pulse = pulse;
                Sway = sway;
                Seconds = seconds;
                Phase = phase;
            }

            public string Path { get; }
            public IdleMotion.Style Style { get; }
            public Vector2 Bob { get; }
            public float Pulse { get; }
            public float Sway { get; }
            public float Seconds { get; }
            public float Phase { get; }
        }

        // WINNER beats like a heart; the logo and TIME'S UP drift a unit or two and breathe slowly, on
        // the same beat but half a cycle apart, so one rises while the other sinks.
        private static readonly BoardMotion[] BoardMotions =
        {
            new BoardMotion("Layout/Board/Winner Title", IdleMotion.Style.Wave, Vector2.zero, 0.06f, 0f, 1.2f, 0f),
            new BoardMotion("Layout/Board/Logo", IdleMotion.Style.Wave, new Vector2(0f, 1.5f), 0.012f, 0f, 4.5f, 0f),
            new BoardMotion("Layout/Board/Time Up", IdleMotion.Style.Wave, new Vector2(0f, 1.2f), 0.01f, 0f, 4.5f, 0.5f),
        };

        private static void AddMotion(SerializedProperty idle, Transform piece, BoardMotion motion)
        {
            if (piece == null) return;

            idle.arraySize++;
            SetMotion(idle.GetArrayElementAtIndex(idle.arraySize - 1), piece, motion);
        }

        private static void SetMotion(SerializedProperty entry, Transform piece, BoardMotion motion)
        {
            entry.FindPropertyRelative("piece").objectReferenceValue = piece;
            entry.FindPropertyRelative("style").enumValueIndex = (int)motion.Style;
            entry.FindPropertyRelative("bob").vector2Value = motion.Bob;
            entry.FindPropertyRelative("pulse").floatValue = motion.Pulse;
            entry.FindPropertyRelative("sway").floatValue = motion.Sway;
            entry.FindPropertyRelative("seconds").floatValue = motion.Seconds;
            entry.FindPropertyRelative("phase").floatValue = motion.Phase;
        }

        // Brings a prefab made before these changes up to date, once (while the logo still has its light
        // sweep): the logo and TIME'S UP lose the sweep, the three titles get their own motions, and
        // the dogs take their intro sizes with the spots back at scale 1.
        private static void RestyleBoard()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform logoSweep = root.transform.Find("Layout/Board/Logo/Shine Mask");
                if (logoSweep == null) return;

                Object.DestroyImmediate(logoSweep.gameObject);
                Transform timeUpSweep = root.transform.Find("Layout/Board/Time Up/Shine Mask");
                if (timeUpSweep != null) Object.DestroyImmediate(timeUpSweep.gameObject);

                var screen = root.GetComponent<ResultScreen>();
                var data = new SerializedObject(screen);
                SerializedProperty idle = data.FindProperty("idle");
                foreach (BoardMotion motion in BoardMotions)
                {
                    Transform piece = root.transform.Find(motion.Path);
                    SerializedProperty entry = FindMotion(idle, piece);
                    if (entry != null) SetMotion(entry, piece, motion);
                    else AddMotion(idle, piece, motion);
                }

                SerializedProperty looks = data.FindProperty("looks");
                for (int i = 0; i < looks.arraySize; i++)
                {
                    SerializedProperty look = looks.GetArrayElementAtIndex(i);
                    string character = look.FindPropertyRelative("character").stringValue;
                    foreach (var dog in Dogs)
                    {
                        if (dog.Character == character) look.FindPropertyRelative("size").floatValue = dog.Size;
                    }
                }

                ResizeSpot(root.transform.Find("Layout/Podium/Winner"), Dogs[0].Size);
                foreach ((string name, Vector2 _, string dog) in Clappers)
                {
                    ResizeSpot(root.transform.Find($"Layout/{name}"), Dogs.First(entry => entry.Dog == dog).Size);
                }

                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[ResultSetup] {PrefabPath}: the dogs are their intro sizes, and WINNER, the logo and TIME'S UP each move their own way.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // A prefab made before the icons were quietened (the logo glowing and its star glinting,
        // TIME'S UP tick-tocking) loses the halo and the glint and takes the quiet motions of
        // BoardMotions; Chiwawa takes its new size if it never had it.
        private static void QuietIcons()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform glow = root.transform.Find("Layout/Board/Logo Glow");
                Transform glint = root.transform.Find("Layout/Board/Logo/Star Glint");
                if (glow == null && glint == null) return;

                if (glow != null) Object.DestroyImmediate(glow.gameObject);
                if (glint != null) Object.DestroyImmediate(glint.gameObject);

                var screen = new SerializedObject(root.GetComponent<ResultScreen>());
                SerializedProperty idle = screen.FindProperty("idle");
                foreach (BoardMotion motion in BoardMotions)
                {
                    Transform piece = root.transform.Find(motion.Path);
                    SerializedProperty entry = FindMotion(idle, piece);
                    if (entry != null) SetMotion(entry, piece, motion);
                    else AddMotion(idle, piece, motion);
                }

                Resize(root.transform, screen.FindProperty("looks"), "Simba", 0.311f, "Layout/Clapper Middle");
                screen.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[ResultSetup] {PrefabPath}: the logo and TIME'S UP now only drift and breathe a little.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // A dog's size still at firstSize (never changed by hand) takes its size in Dogs, and so does
        // the prefab's preview of it at spot. Returns true when anything changed.
        private static bool Resize(Transform root, SerializedProperty looks, string character, float firstSize, string spotPath)
        {
            var dog = Dogs.First(entry => entry.Character == character);
            bool changed = false;
            for (int i = 0; i < looks.arraySize; i++)
            {
                SerializedProperty look = looks.GetArrayElementAtIndex(i);
                SerializedProperty lookSize = look.FindPropertyRelative("size");
                if (look.FindPropertyRelative("character").stringValue != character || !Mathf.Approximately(lookSize.floatValue, firstSize)) continue;
                lookSize.floatValue = dog.Size;
                changed = true;
            }

            var spot = root.Find(spotPath) as RectTransform;
            if (spot == null || !spot.TryGetComponent(out Image picture) || picture.sprite == null) return changed;
            if (!picture.sprite.name.StartsWith(dog.Dog)) return changed;
            if (Mathf.Abs(spot.sizeDelta.y - picture.sprite.rect.height * firstSize) > 0.5f) return changed;

            spot.sizeDelta = picture.sprite.rect.size * dog.Size;
            return true;
        }

        // Pom grows from its first size, and the winner's row turns orange where its texts are still
        // the cream of the others.
        private static void PomAndFirstPlace()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var screen = new SerializedObject(root.GetComponent<ResultScreen>());
                bool changed = Resize(root.transform, screen.FindProperty("looks"), "Pompo", 0.409f, "Layout/Clapper Left");
                screen.ApplyModifiedPropertiesWithoutUndo();

                Transform row = root.transform.Find("Layout/Board/Row 1");
                foreach (string text in new[] { "Rank", "Name", "Score" })
                {
                    Transform piece = row != null ? row.Find(text) : null;
                    if (piece == null || !piece.TryGetComponent(out TMP_Text label) || label.color != TextColor) continue;
                    label.color = FirstPlaceColor;
                    changed = true;
                }

                if (!changed) return;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[ResultSetup] {PrefabPath}: Pom is bigger and the winner's row is orange.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static SerializedProperty FindMotion(SerializedProperty idle, Transform piece)
        {
            if (piece == null) return null;
            for (int i = 0; i < idle.arraySize; i++)
            {
                SerializedProperty entry = idle.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("piece").objectReferenceValue == piece) return entry;
            }

            return null;
        }

        // A spot still at a first-layout scale goes back to 1, its picture sized for the dog instead.
        private static void ResizeSpot(Transform spot, float size)
        {
            if (spot == null || !spot.TryGetComponent(out Image picture) || picture.sprite == null) return;
            if (!FirstSpotScales.Any(scale => Mathf.Abs(spot.localScale.x - scale) < 0.001f)) return;

            spot.localScale = Vector3.one;
            picture.SetNativeSize();
            picture.rectTransform.sizeDelta *= size;
        }

        private static (RectTransform, Image, TMP_Text, TMP_Text, TMP_Text) BuildRow(RectTransform board, string name, Vector2 at, float width, float[] columns, float[] sizes, string face, TMP_FontAsset font, int place, string character)
        {
            RectTransform row = Picture(board, name, "BG Line Score", BoardAt, at, width);
            Image faceImage = null;
            if (face != null)
            {
                RectTransform faceRect = Picture(row, "Face", face, at, new Vector2(FaceX, at.y), FaceWidth);
                faceImage = faceRect.GetComponent<Image>();
            }

            Color color = place == 1 ? FirstPlaceColor : TextColor;
            TMP_Text rank = Label(row, "Rank", at, columns[0], sizes[0], $"NO. {place}", font, color);
            TMP_Text label = Label(row, "Name", at, columns[1], sizes[1], character.ToUpperInvariant(), font, color);
            TMP_Text score = Label(row, "Score", at, columns[2], sizes[2], "4000", font, color);
            return (row, faceImage, rank, label, score);
        }

        private static TMP_Text Label(RectTransform row, string name, Vector2 rowAt, float x, float size, string text, TMP_FontAsset font, Color color)
        {
            var piece = new GameObject(name, typeof(RectTransform));
            piece.transform.SetParent(row, false);
            var label = piece.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.rectTransform.sizeDelta = new Vector2(size * 4f, size * 1.2f);
            label.rectTransform.anchoredPosition = new Vector2((x - rowAt.x) * RefScale, 0f);
            return label;
        }

        // A dog standing with its feet at the spot (the picture's bottom centre is its pivot), its first
        // frame shown size times its pixels.
        private static Animator Dog(Transform parent, string name, Vector2 feet, float size, RuntimeAnimatorController animations, string previewClip)
        {
            var piece = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Animator));
            piece.transform.SetParent(parent, false);
            var rect = (RectTransform)piece.transform;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = feet;

            var image = piece.GetComponent<Image>();
            image.raycastTarget = false;
            Sprite[] frames = LoadFrames(previewClip);
            if (frames.Length > 0)
            {
                image.sprite = frames[0];
                image.SetNativeSize();
                rect.sizeDelta *= size;
            }

            var animator = piece.GetComponent<Animator>();
            animator.runtimeAnimatorController = animations;
            return animator;
        }

        // An empty piece centred at a spot, to group and pop the pieces inside it together.
        private static RectTransform Group(Transform parent, string name, Vector2 at)
        {
            var group = new GameObject(name, typeof(RectTransform));
            group.transform.SetParent(parent, false);
            var rect = (RectTransform)group.transform;
            rect.anchoredPosition = at;
            return rect;
        }

        // A picture from Assets/ART/BG/Result centred at a spot on the reference shot, width wide there,
        // inside a parent centred at parentAt.
        private static RectTransform Picture(RectTransform parent, string name, string file, Vector2 parentAt, Vector2 at, float width)
        {
            var piece = new GameObject(name, typeof(RectTransform), typeof(Image));
            piece.transform.SetParent(parent, false);
            var image = piece.GetComponent<Image>();
            image.sprite = LoadSprite($"{Folder}/{file}.png");
            image.preserveAspect = true;
            image.raycastTarget = false;

            var rect = (RectTransform)piece.transform;
            float aspect = image.sprite != null ? image.sprite.rect.height / image.sprite.rect.width : 1f;
            rect.sizeDelta = new Vector2(width, width * aspect) * RefScale;
            rect.anchoredPosition = Ref(at) - Ref(parentAt);
            return rect;
        }

        // A spot on the reference shot (pixels from its top left) in the wall picture's units from its centre.
        private static Vector2 Ref(Vector2 spot)
        {
            return new Vector2(spot.x - RefCentre.x, RefCentre.y - spot.y) * RefScale;
        }

        private static bool PlaceOnWall(Transform wall, out ResultScreen screen)
        {
            Transform existing = wall.Find("Result");
            if (existing != null)
            {
                screen = existing.GetComponent<ResultScreen>();
                return false;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, wall);
            instance.name = "Result";
            Stretch((RectTransform)instance.transform);
            Transform play = wall.Find("Play");
            if (play != null) instance.transform.SetSiblingIndex(play.GetSiblingIndex() + 1);
            Undo.RegisterCreatedObjectUndo(instance, "Setup Result Screen");
            screen = instance.GetComponent<ResultScreen>();
            return true;
        }

        private static bool BuildSequence(Transform wall, ResultScreen screen)
        {
            if (screen == null || Object.FindFirstObjectByType<ResultSequence>(FindObjectsInactive.Include) != null) return false;

            var holder = new GameObject("Result Sequence", typeof(ResultSequence));
            holder.transform.SetParent(wall.parent, false);
            Undo.RegisterCreatedObjectUndo(holder, "Setup Result Sequence");

            var data = new SerializedObject(holder.GetComponent<ResultSequence>());
            data.FindProperty("timer").objectReferenceValue = Object.FindFirstObjectByType<StageTimer>(FindObjectsInactive.Include);
            data.FindProperty("rounds").objectReferenceValue = Object.FindFirstObjectByType<BountyRounds>(FindObjectsInactive.Include);
            data.FindProperty("scoreTags").objectReferenceValue = Object.FindFirstObjectByType<CharacterScoreTags>(FindObjectsInactive.Include);
            data.FindProperty("curtain").objectReferenceValue = Object.FindFirstObjectByType<FadeCurtain>(FindObjectsInactive.Include);
            data.FindProperty("result").objectReferenceValue = screen;

            var hidden = new List<GameObject>();
            Transform play = wall.Find("Play");
            foreach (string path in HiddenOnResult)
            {
                Transform piece = play != null ? play.Find(path) : null;
                if (piece != null) hidden.Add(piece.gameObject);
                else Debug.LogWarning($"[ResultSetup] Map/SideDisplay/Play/{path} not found; add it to Result Sequence's Hide On Result if it should make way.");
            }

            SetList(data.FindProperty("hideOnResult"), hidden);
            data.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // N on the last map ends the game through the result sequence.
        private static bool LinkMapSwitcher()
        {
            var switcher = Object.FindFirstObjectByType<MapSwitcher>(FindObjectsInactive.Include);
            var sequence = Object.FindFirstObjectByType<ResultSequence>(FindObjectsInactive.Include);
            if (switcher == null || sequence == null) return false;

            var data = new SerializedObject(switcher);
            SerializedProperty result = data.FindProperty("result");
            if (result.objectReferenceValue != null) return false;

            result.objectReferenceValue = sequence;
            data.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static void SetList<T>(SerializedProperty list, IList<T> items) where T : Object
        {
            list.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
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
