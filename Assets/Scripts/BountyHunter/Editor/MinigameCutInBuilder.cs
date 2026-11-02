using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves.EditorTools
{
    // Builds the cut-ins that open the mini games (MinigameCutIn) as prefabs from their art, laid out
    // as in the designer's examples, each time the editor loads and one is missing, puts each over the
    // wall just behind the fade curtain and hands it to its mini game as the way in. One plan each:
    // the suspect lineup's PAW LINEUP (Assets/ART/Transition/Open Minigame 1, example CUT SCENE/001/EX.png)
    // and the security camera's BIN SHUFFLE (Open Minigame2, example CUT SCENE/002's screenshot).
    // A red streak, where there is one, is drawn along its zigzag (PathReveal) with its path map
    // (Tools/TransitionArt/make_streak_path.py), imported as plain 16-bit data, and knows where the
    // drawing's tip is from the same tool's middle line (.json); the bits flying about are two
    // CutInParticles layers (over everything, and just over the background for the twinkling stars),
    // their stars the stun gun's dizzy star, or, for the camera's, a broken-signal flicker on the
    // background (GlitchBars) in place of the stars; the printing on each tape slides along it (TapeScroll) by
    // the measures of Tools/TransitionArt/measure_tapes.py (<prefix>_Tapes.json). A prefab built before
    // any of that gets it added. Otherwise a built prefab is left alone: move, resize or retime its
    // pieces there. Tools > Bounty Hunter > Rebuild Mini Game Cut-Ins builds them again from scratch.
    // Save the scene afterwards.
    [InitializeOnLoad]
    public static class MinigameCutInBuilder
    {
        public const string LineupPrefabPath = "Assets/Prefabs/Transition/Lineup Open Transition.prefab";
        public const string CctvPrefabPath = "Assets/Prefabs/Transition/Cctv Open Transition.prefab";
        private const string PathRevealShader = "Assets/ART/Transition/UI_PathReveal.shader";
        private const string TapeScrollShader = "Assets/ART/Transition/UI_TapeScroll.shader";
        private const string StarPicture = "Assets/ART/StunGun/StunStar.png";
        // The art is drawn for the whole wall, 8192 x 2160 pixels.
        private const float ArtWidth = 8192f;
        private const float ArtHeight = 2160f;

        // A picture's place in the art, in pixels from its top left corner, and its drawn size
        // (a tape a little wider than its picture so it reaches the wall's right edge).
        private sealed class Placement
        {
            public readonly string File;
            public readonly Vector2 TopLeft;
            public readonly Vector2 Size;

            public Placement(string file, float left, float top, float width, float height)
            {
                File = file;
                TopLeft = new Vector2(left, top);
                Size = new Vector2(width, height);
            }
        }

        // One mini game's cut-in: where its art is, how it is laid out, and whose way in it is.
        private sealed class Plan
        {
            public string Name;
            public string PrefabPath;
            public string Art;
            // Its pictures' names start with this (OM1_BG.png, OM1_Tapes.json, ...).
            public string Prefix;
            public System.Type MiniGame;
            public Placement Streak;
            // In the order they whip in.
            public Placement[] Tapes;
            public Placement Logo;
            public Placement Badge;
            public Placement Character;
            // The background flickers like a torn camera picture (GlitchBars) instead of twinkling.
            public bool Glitch;
            // Null keeps the cut-in's own colours.
            public Color[] LogoColours;
            public Color[] TapeColours;

            public string Background => Art + Prefix + "_BG.png";
            public string StreakPathMap => Streak == null ? null : Art + System.IO.Path.GetFileNameWithoutExtension(Streak.File) + "_Path.png";
            public string StreakMiddle => Streak == null ? null : Art + System.IO.Path.GetFileNameWithoutExtension(Streak.File) + "_Path.json";
            public string TapeMeasures => Art + Prefix + "_Tapes.json";
        }

        private static readonly Plan[] Plans =
        {
            new Plan
            {
                Name = "Lineup Open Transition",
                PrefabPath = LineupPrefabPath,
                Art = "Assets/ART/Transition/Open Minigame 1/",
                Prefix = "OM1",
                MiniGame = typeof(LineupPuzzle),
                Streak = new Placement("OM1_Line5.png", 0f, 178f, 8192f, 2160f),
                Tapes = new[]
                {
                    new Placement("OM1_Line3.png", 0f, -134f, 8192f, 422f),
                    new Placement("OM1_Line2.png", -274f, -286f, 8126f, 773f),
                    new Placement("OM1_Line4.png", 0f, 1700f, 8192f, 531f),
                    new Placement("OM1_Line1.png", -229f, 1413f, 8421f, 919f),
                },
                Logo = new Placement("OM1_Logo.png", 2979f, 261f, 3425f, 1251f),
                Character = new Placement("OM1_Char.png", 1279f, 185f, 1423f, 2153f),
            },
            new Plan
            {
                Name = "Cctv Open Transition",
                PrefabPath = CctvPrefabPath,
                Art = "Assets/ART/Transition/Open Minigame2/",
                Prefix = "OM2",
                MiniGame = typeof(CctvPuzzle),
                Tapes = new[]
                {
                    new Placement("OM2_Line3.png", -283f, -367f, 8192f, 422f),
                    new Placement("OM2_Line2.png", -301f, -289f, 8126f, 773f),
                    new Placement("OM2_Line4.png", 0f, 1884f, 8192f, 531f),
                    new Placement("OM2_Line1.png", -301f, 1794f, 8493f, 919f),
                },
                Logo = new Placement("OM2_Logo.png", 2648f, 373f, 3634f, 1312f),
                Badge = new Placement("OM2_Live.png", 6976f, 241f, 451f, 162f),
                Glitch = true,
                // The same height and spot as the lineup's rabbit: ears to feet as tall, feet as low and
                // legs as far across (the megaphone reaching on to the right).
                Character = new Placement("OM2_Char.png", 837f, 180f, 2105.5f, 2158f),
                // BIN SHUFFLE's red, white and black; red and white STOP tapes beside the yellow ones.
                LogoColours = new[]
                {
                    new Color(1f, 0.15f, 0.2f, 1f), Color.white, new Color(1f, 0.55f, 0.55f, 1f), new Color(0.1f, 0.08f, 0.1f, 1f),
                },
                TapeColours = new[]
                {
                    new Color(1f, 0.15f, 0.25f, 1f), Color.white, new Color(0.93f, 1f, 0.1f, 1f), new Color(0.08f, 0.08f, 0.1f, 1f),
                },
            },
        };

        static MinigameCutInBuilder()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        private static void BuildIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            foreach (Plan plan in Plans)
            {
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(plan.Background) == null) continue;

                ImportPathMap(plan);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(plan.PrefabPath) == null) Build(plan);
                else DrawStreakAlongPath(plan);
                AddFinishingTouches(plan);
                Wire(plan);
            }
        }

        [MenuItem("Tools/Bounty Hunter/Rebuild Mini Game Cut-Ins")]
        public static void Rebuild()
        {
            foreach (Plan plan in Plans)
            {
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(plan.Background) == null) continue;

                ImportPathMap(plan);
                Build(plan);
                AddFinishingTouches(plan);
                Wire(plan);
            }
        }

        private static void Build(Plan plan)
        {
            ImportArt(plan);
            Vector2 wall = LineupPrefabBuilder.WallSize;
            var root = new GameObject(plan.Name, typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = wall;
            var cutIn = root.AddComponent<MinigameCutIn>();

            RectTransform shaker = UiRect("Shaker", rootRect);
            Stretch(shaker);
            Image background = Picture("Background", shaker, plan.Background);
            Stretch(background.rectTransform);

            PathReveal streakDraw = null;
            if (plan.Streak != null) streakDraw = AddPathReveal(Placed("Streak", shaker, plan, plan.Streak), plan);

            var tapes = new RectTransform[plan.Tapes.Length];
            for (int i = 0; i < plan.Tapes.Length; i++) tapes[i] = Placed($"Tape {i + 1}", shaker, plan, plan.Tapes[i]).rectTransform;
            Image logo = Placed("Logo", shaker, plan, plan.Logo);
            Image badge = plan.Badge != null ? Placed("Badge", shaker, plan, plan.Badge) : null;
            Image character = Placed("Character", shaker, plan, plan.Character);

            var fields = new SerializedObject(cutIn);
            fields.FindProperty("shaker").objectReferenceValue = shaker;
            fields.FindProperty("background").objectReferenceValue = background.rectTransform;
            fields.FindProperty("streakDraw").objectReferenceValue = streakDraw;
            fields.FindProperty("character").objectReferenceValue = character.rectTransform;
            fields.FindProperty("logo").objectReferenceValue = logo.rectTransform;
            fields.FindProperty("badge").objectReferenceValue = badge != null ? badge.rectTransform : null;
            SerializedProperty tapeList = fields.FindProperty("tapes");
            tapeList.arraySize = tapes.Length;
            for (int i = 0; i < tapes.Length; i++)
            {
                tapeList.GetArrayElementAtIndex(i).FindPropertyRelative("piece").objectReferenceValue = tapes[i];
            }

            SetColours(fields.FindProperty("logoColours"), plan.LogoColours);
            SetColours(fields.FindProperty("tapeColours"), plan.TapeColours);
            fields.ApplyModifiedPropertiesWithoutUndo();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(plan.PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, plan.PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[MinigameCutInBuilder] Built {plan.PrefabPath}", AssetDatabase.LoadAssetAtPath<GameObject>(plan.PrefabPath));
        }

        private static void SetColours(SerializedProperty list, Color[] colours)
        {
            if (list == null || colours == null) return;

            list.arraySize = colours.Length;
            for (int i = 0; i < colours.Length; i++) list.GetArrayElementAtIndex(i).colorValue = colours[i];
        }

        // A prefab built while its streak was still wiped across gets it drawn along its zigzag.
        private static void DrawStreakAlongPath(Plan plan)
        {
            if (plan.Streak == null) return;

            GameObject contents = PrefabUtility.LoadPrefabContents(plan.PrefabPath);
            try
            {
                var cutIn = contents.GetComponent<MinigameCutIn>();
                var streak = contents.transform.Find("Shaker/Streak")?.GetComponent<Image>();
                if (cutIn == null || streak == null) return;

                var fields = new SerializedObject(cutIn);
                SerializedProperty draw = fields.FindProperty("streakDraw");
                if (draw == null || draw.objectReferenceValue != null) return;

                PathReveal reveal = streak.GetComponent<PathReveal>();
                if (reveal == null) reveal = AddPathReveal(streak, plan);
                draw.objectReferenceValue = reveal;
                fields.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, plan.PrefabPath);
                Debug.Log($"[MinigameCutInBuilder] {plan.Name}'s streak now shoots along its zigzag", AssetDatabase.LoadAssetAtPath<GameObject>(plan.PrefabPath));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // A prefab without them gets its bits layers, its streak's middle line and its sliding tapes.
        private static void AddFinishingTouches(Plan plan)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(plan.PrefabPath);
            try
            {
                var cutIn = contents.GetComponent<MinigameCutIn>();
                Transform shaker = contents.transform.Find("Shaker");
                if (cutIn == null || shaker == null) return;

                bool changed = false;
                var fields = new SerializedObject(cutIn);
                SerializedProperty particles = fields.FindProperty("particles");
                if (particles != null && particles.objectReferenceValue == null)
                {
                    particles.objectReferenceValue = ParticleLayer("Particles", shaker, shaker.childCount);
                    fields.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                // The twinkling stars: just over the background, behind everything else.
                SerializedProperty backdrop = fields.FindProperty("backdropParticles");
                Transform background = shaker.Find("Background");
                if (backdrop != null && backdrop.objectReferenceValue == null && background != null)
                {
                    backdrop.objectReferenceValue = ParticleLayer("Backdrop Particles", shaker, background.GetSiblingIndex() + 1);
                    fields.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                // The broken-signal flicker on the background, in place of the twinkling stars.
                SerializedProperty glitch = fields.FindProperty("glitch");
                if (plan.Glitch && glitch != null && glitch.objectReferenceValue == null && background != null)
                {
                    RectTransform layer = UiRect("Glitch", shaker);
                    Stretch(layer);
                    layer.SetSiblingIndex(background.GetSiblingIndex() + 1);
                    glitch.objectReferenceValue = layer.gameObject.AddComponent<GlitchBars>();
                    fields.FindProperty("twinklesPerSecond").floatValue = 0f;
                    fields.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                var reveal = shaker.Find("Streak")?.GetComponent<PathReveal>();
                if (reveal != null && plan.Streak != null)
                {
                    var revealFields = new SerializedObject(reveal);
                    SerializedProperty middle = revealFields.FindProperty("middle");
                    Vector3[] points = ReadMiddle(plan);
                    if (middle != null && middle.arraySize == 0 && points.Length > 1)
                    {
                        middle.arraySize = points.Length;
                        for (int i = 0; i < points.Length; i++) middle.GetArrayElementAtIndex(i).vector3Value = points[i];
                        revealFields.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                }

                changed |= ScrollTapes(shaker, plan);
                if (!changed) return;
                PrefabUtility.SaveAsPrefabAsset(contents, plan.PrefabPath);
                Debug.Log($"[MinigameCutInBuilder] {plan.Name} has its flying bits and sliding tapes", AssetDatabase.LoadAssetAtPath<GameObject>(plan.PrefabPath));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // A layer of flying bits over the whole wall, at that place among the pieces (later is in front).
        private static CutInParticles ParticleLayer(string name, Transform shaker, int place)
        {
            RectTransform layer = UiRect(name, shaker);
            Stretch(layer);
            layer.SetSiblingIndex(place);
            var bits = layer.gameObject.AddComponent<CutInParticles>();
            var bitFields = new SerializedObject(bits);
            bitFields.FindProperty("star").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(StarPicture);
            bitFields.ApplyModifiedPropertiesWithoutUndo();
            return bits;
        }

        // Each tape without one gets a TapeScroll with its measures; true when any did.
        private static bool ScrollTapes(Transform shaker, Plan plan)
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(plan.TapeMeasures);
            if (json == null) return false;

            const string Number = @"(-?[0-9.eE+-]+)";
            bool changed = false;
            foreach (Image tape in shaker.GetComponentsInChildren<Image>(true))
            {
                if (!tape.name.StartsWith("Tape") || tape.sprite == null || tape.GetComponent<TapeScroll>() != null) continue;

                // "OM1_Line1.png": {"direction": [x, y], "period": p, "size": [w, h], ...}
                string entry = Regex.Escape(tape.sprite.name + ".png");
                string pattern = "\"" + entry + "\"\\s*:\\s*\\{\\s*\"direction\"\\s*:\\s*\\[\\s*" + Number + "\\s*,\\s*" + Number +
                    "\\s*\\]\\s*,\\s*\"period\"\\s*:\\s*" + Number + "\\s*,\\s*\"size\"\\s*:\\s*\\[\\s*" + Number + "\\s*,\\s*" + Number + "\\s*\\]";
                Match found = Regex.Match(json.text, pattern);
                if (!found.Success) continue;

                var scroll = tape.gameObject.AddComponent<TapeScroll>();
                var fields = new SerializedObject(scroll);
                fields.FindProperty("shader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(TapeScrollShader);
                fields.FindProperty("along").vector2Value = new Vector2(Parse(found.Groups[1].Value), Parse(found.Groups[2].Value));
                fields.FindProperty("period").floatValue = Parse(found.Groups[3].Value);
                fields.FindProperty("pictureSize").vector2Value = new Vector2(Parse(found.Groups[4].Value), Parse(found.Groups[5].Value));
                fields.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            return changed;
        }

        // The streak's middle line from make_streak_path.py: [x, y, along] triples.
        private static Vector3[] ReadMiddle(Plan plan)
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(plan.StreakMiddle);
            if (json == null) return System.Array.Empty<Vector3>();

            var points = new List<Vector3>();
            const string Number = @"(-?[0-9.eE+-]+)";
            foreach (Match triple in Regex.Matches(json.text, $@"\[\s*{Number}\s*,\s*{Number}\s*,\s*{Number}\s*\]"))
            {
                points.Add(new Vector3(Parse(triple.Groups[1].Value), Parse(triple.Groups[2].Value), Parse(triple.Groups[3].Value)));
            }

            return points.ToArray();
        }

        private static float Parse(string number)
        {
            return float.Parse(number, CultureInfo.InvariantCulture);
        }

        // The streak shown whole and drawn along its path by the path reveal shader.
        private static PathReveal AddPathReveal(Image streak, Plan plan)
        {
            streak.type = Image.Type.Simple;
            streak.fillAmount = 1f;
            var reveal = streak.gameObject.AddComponent<PathReveal>();
            var fields = new SerializedObject(reveal);
            fields.FindProperty("shader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(PathRevealShader);
            fields.FindProperty("pathMap").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(plan.StreakPathMap);
            fields.ApplyModifiedPropertiesWithoutUndo();
            return reveal;
        }

        // The path map is data, not a picture: linear, uncompressed 16-bit, clamped, no mipmaps.
        private static void ImportPathMap(Plan plan)
        {
            if (plan.Streak == null || AssetImporter.GetAtPath(plan.StreakPathMap) is not TextureImporter importer) return;
            if (importer.textureType == TextureImporterType.Default && !importer.sRGBTexture) return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            TextureImporterPlatformSettings settings = importer.GetDefaultPlatformTextureSettings();
            settings.format = TextureImporterFormat.R16;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(settings);
            importer.SaveAndReimport();
        }

        // Puts the cut-in over the wall, just behind the fade curtain and hidden, and makes it its
        // mini game's way in, unless the mini game already has one.
        private static void Wire(Plan plan)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<MinigameCutIn>(plan.PrefabPath);
            var miniGame = Object.FindFirstObjectByType(plan.MiniGame, FindObjectsInactive.Include) as StageInterlude;
            if (prefab == null || miniGame == null) return;

            var miniGameFields = new SerializedObject(miniGame);
            SerializedProperty way = miniGameFields.FindProperty("entryTransition");
            if (way == null || way.objectReferenceValue != null) return;

            MinigameCutIn placed = null;
            foreach (MinigameCutIn existing in Object.FindObjectsByType<MinigameCutIn>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing) == prefab) placed = existing;
            }

            if (placed == null)
            {
                Transform side = Find("SideDisplay");
                if (side == null) return;

                placed = (MinigameCutIn)PrefabUtility.InstantiatePrefab(prefab, side);
                Transform curtain = Find("Fade Curtain");
                if (curtain != null && curtain.parent == side) placed.transform.SetSiblingIndex(curtain.GetSiblingIndex());
                placed.gameObject.SetActive(false);
            }

            way.objectReferenceValue = placed;
            miniGameFields.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(miniGame.gameObject.scene);
            Debug.Log($"[MinigameCutInBuilder] {miniGame.name} opens with {plan.Name}; save the scene to keep it", placed);
        }

        // The pictures as UI sprites, at full size so they stay sharp across the wall.
        private static void ImportArt(Plan plan)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { plan.Art.TrimEnd('/') }))
            {
                string file = AssetDatabase.GUIDToAssetPath(guid);
                if (file == plan.StreakPathMap || AssetImporter.GetAtPath(file) is not TextureImporter importer) continue;
                if (importer.textureType == TextureImporterType.Sprite && importer.maxTextureSize >= 8192) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 8192;
                importer.SaveAndReimport();
            }
        }

        // A picture placed where it is in the art, in the wall canvas's units.
        private static Image Placed(string name, RectTransform parent, Plan plan, Placement place)
        {
            Image image = Picture(name, parent, plan.Art + place.File);
            float unit = LineupPrefabBuilder.WallSize.y / ArtHeight;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = place.Size * unit;
            Vector2 centre = place.TopLeft + place.Size * 0.5f;
            rect.anchoredPosition = new Vector2(centre.x - ArtWidth * 0.5f, ArtHeight * 0.5f - centre.y) * unit;
            return image;
        }

        private static Image Picture(string name, RectTransform parent, string path)
        {
            var image = UiRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            image.raycastTarget = false;
            return image;
        }

        private static Transform Find(string name)
        {
            foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.name == name) return candidate;
            }

            return null;
        }

        private static RectTransform UiRect(string name, Transform parent)
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
