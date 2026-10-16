using BountyHunter.Thieves;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace BountyHunter.Editor
{
    // Builds the intro wall under Map/SideDisplay/Intro from Assets/ART/Intro, laid out like the
    // reference shot: the police station filling the wall, the green-screen video of the patrol car
    // arriving and the dogs jumping out in the middle, and the blurred patrol car (right) and bin
    // (bottom left) in front. When the video ends, IntroShow fades in a living copy of its last
    // frame built from parts laid over it: the parked patrol car, its light glows, the dogs' breathing
    // videos and the logo, each placed where it is in that frame. Adds IntroSequence wired to the
    // game wall, the floor (the menu floor during the intro) and the rounds. Runs after scripts load and is on the menu; only what is
    // missing is added, so changes made in the scene stay.
    [InitializeOnLoad]
    public static class IntroSetup
    {
        private const string ScenePath = "Assets/Scenes/BountyHunter.unity";
        private const string Folder = "Assets/ART/Intro";
        private const string Background = Folder + "/BG.png";
        private const string Car = Folder + "/Car.png";
        private const string Bin = Folder + "/Bin.png";
        // The arrival with the video's own logo taken out; IntroLogo brings in Logo.png in its place.
        private const string Video = Folder + "/BountyHunter_Intro_NoLogo.mp4";
        private const string ChromaKey = "Assets/ART/BG/WallPlay/UI_ChromaKey.shader";
        private const string MenuFloor = "Assets/ART/BG/MapIntro/FloorMenu.png";
        private const string ParkedCar = Folder + "/Parts/Car_Parked.png";
        private const string LightGlow = Folder + "/Parts/Light_Glow.png";
        private const string LogoShine = Folder + "/Parts/Logo_Shine.png";
        private const string Glint = Folder + "/Parts/Glint.png";
        // The sheriff star on the logo, in parts of the video frame, with room for its glint.
        private static readonly Rect StarGlintArea = Rect.MinMaxRect(0.3292f, 0.7510f, 0.4228f, 0.9172f);
        // The bin standing before the crash is Bin.png (drawn lying, lid to the left) turned upright,
        // so it is the very bin that ends up lying in the corner. Its box in parts of the wall picture,
        // before turning: once turned it stands on the ground line just in front of where the car
        // parks, so the car's bumper reaches it as the car slides in.
        private static readonly Rect StandingBinArea = Rect.MinMaxRect(0.2750f, 0.0572f, 0.3686f, 0.3256f);
        // The bin's long side lies at 167 degrees in Bin.png (measured), so this stands it straight up.
        private const float StandingBinTurn = -77f;

        // Fine placement chosen in the editor, in the wall's units: the car arriving (the video), its
        // living copy and the standing bin in its path move together, so the swap and the crash still
        // line up; the blurred car in front sits a little lower and left and is stretched a touch wider.
        private static readonly Vector2 ArrivalShift = new Vector2(-57f, -26f);
        private static readonly Vector2 FrontCarShift = new Vector2(-25f, -34f);
        private static readonly Vector3 FrontCarScale = new Vector3(1.14f, 1.038062f, 1.038062f);
        // The clean logo picture (the one cut from the video lost parts of its badge to the key).
        private const string Logo = Folder + "/Logo.png";
        // The video's chroma key as a material, for the parts cut from video frames (car, logo).
        private const string KeyMaterial = Folder + "/Parts/Intro_ChromaKey.mat";
        private const string DogFolder = "Assets/ART/Player";
        private static readonly Color RedGlow = new Color(1f, 0.15f, 0.22f, 1f);
        private static readonly Color BlueGlow = new Color(0.25f, 0.45f, 1f, 1f);

        // A part of the idle copy: what it shows and where, in parts of the intro video's frame
        // ((0, 0) bottom left), measured from the video's last frame.
        private sealed class Part
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public bool IsVideo { get; set; }
            public Rect Area { get; set; }
        }

        // Back to front, as they overlap in the video.
        private static readonly Part[] IdleParts =
        {
            new Part { Name = "Car", Path = ParkedCar, Area = Rect.MinMaxRect(0.1210f, 0.0194f, 0.7649f, 0.5435f) },
            new Part { Name = "Chiwawa", Path = DogFolder + "/Chiwawa_Idle_Loop.mp4", IsVideo = true, Area = Rect.MinMaxRect(0.1174f, 0.2678f, 0.5174f, 0.6678f) },
            new Part { Name = "Saibirian", Path = DogFolder + "/Saibirian_Idle_Loop.mp4", IsVideo = true, Area = Rect.MinMaxRect(-0.2549f, -0.1133f, 0.5131f, 0.6547f) },
            new Part { Name = "Pom", Path = DogFolder + "/Pom_Idle_Loop.mp4", IsVideo = true, Area = Rect.MinMaxRect(0.2717f, -0.0234f, 0.7837f, 0.4886f) },
            new Part { Name = "Shiba", Path = DogFolder + "/Shiba_Idel_Loop.mp4", IsVideo = true, Area = Rect.MinMaxRect(0.4122f, -0.0523f, 1.1522f, 0.6877f) },
            new Part { Name = "Logo", Path = Logo, Area = Rect.MinMaxRect(0.3201f, 0.5075f, 0.7455f, 1.0000f) },
        };

        // The blurred car in front: its light bar, in parts of its picture; big soft glows, it is out of focus.
        private static readonly Rect FrontRedLightArea = Rect.MinMaxRect(0.191f, 0.652f, 0.415f, 1.162f);
        private static readonly Rect FrontBlueLightArea = Rect.MinMaxRect(0.325f, 0.666f, 0.549f, 1.176f);

        // The light bar's red and blue halves, in parts of the parked car picture.
        private static readonly Rect RedLightArea = Rect.MinMaxRect(0.5418f, 0.7035f, 0.7618f, 1.1835f);
        private static readonly Rect BlueLightArea = Rect.MinMaxRect(0.3831f, 0.7176f, 0.6031f, 1.1976f);
        // The wall picture is 8192 x 2160; the pieces are placed in parts of it, (0, 0) bottom left.
        private const float WallAspect = 8192f / 2160f;
        private static readonly Rect VideoArea = Rect.MinMaxRect(0.275f, 0.01f, 0.725f, 0.97f);
        private static readonly Rect CarArea = Rect.MinMaxRect(0.7156f, -0.0125f, 1.15f, 0.708f);
        private static readonly Rect BinArea = Rect.MinMaxRect(-0.033f, -0.246f, 0.132f, 0.228f);

        static IntroSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Setup();
            };
        }

        [MenuItem("Tools/Bounty Hunter/Setup Intro")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            SharpenOnWall(Background, 8192);
            SharpenOnWall(MenuFloor, 4096);
            SharpenOnWall(Car, 4096);

            var scene = EditorSceneManager.GetActiveScene();
            GameObject intro = GameObject.Find("Map/SideDisplay/Intro");
            GameObject arena = GameObject.Find("Map/Arena");
            if (!scene.isLoaded || scene.path != ScenePath || intro == null || arena == null) return;

            bool wasDirty = scene.isDirty;
            bool changed = FillWall(intro.transform);
            changed |= BuildWall(intro.transform, out GameObject wall);
            changed |= BuildIdle(wall);
            changed |= UpdateIdle(wall);
            changed |= BuildBinCrash(wall);
            changed |= PlaceFinely(wall.transform);
            changed |= BuildLogo(wall.transform);
            changed |= BuildRain(wall.transform);
            changed |= BuildAmbience(intro, wall.transform);
            changed |= FillSequence(intro, wall, arena.transform);
            changed |= BuildCurtain(intro, arena.transform);
            if (!changed) return;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!wasDirty) EditorSceneManager.SaveScene(scene);
            Debug.Log("[IntroSetup] The intro wall is ready under Map/SideDisplay/Intro. Press Enter or Space in Play to start the game.");
        }

        // The wall picture is shown on an 8192-wide wall, so it is imported at full size.
        private static void SharpenOnWall(string path, int size)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) || importer.maxTextureSize >= size) return;

            importer.maxTextureSize = size;
            importer.SaveAndReimport();
        }

        // The intro group must cover the whole wall, since the wall picture sizes itself from it.
        private static bool FillWall(Transform intro)
        {
            if (!(intro is RectTransform rect)) return false;
            if (rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one && rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero) return false;

            Undo.RecordObject(rect, "Setup Intro");
            Stretch(rect);
            rect.localScale = Vector3.one;
            rect.localPosition = new Vector3(rect.localPosition.x, rect.localPosition.y, 0f);
            return true;
        }

        private static bool BuildWall(Transform intro, out GameObject wall)
        {
            Transform existing = intro.Find("Background");
            wall = existing != null ? existing.gameObject : null;
            if (wall != null) return false;

            wall = new GameObject("Background", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            wall.transform.SetParent(intro, false);
            Stretch((RectTransform)wall.transform);
            var picture = wall.GetComponent<RawImage>();
            picture.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Background);
            picture.raycastTarget = false;
            var fitter = wall.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = WallAspect;

            AddVideo(wall.transform);
            AddPicture(wall.transform, "Car", Car, CarArea);
            AddPicture(wall.transform, "Bin", Bin, BinArea);
            Undo.RegisterCreatedObjectUndo(wall, "Setup Intro");
            return true;
        }

        // The living copy of the video's last frame, laid exactly over the video, plus IntroShow to run it.
        private static bool BuildIdle(GameObject wall)
        {
            Transform video = wall.transform.Find("Video");
            if (video == null || wall.transform.Find("Idle") != null) return false;

            var idle = new GameObject("Idle", typeof(RectTransform), typeof(CanvasGroup));
            idle.transform.SetParent(wall.transform, false);
            idle.transform.SetSiblingIndex(video.GetSiblingIndex() + 1);
            Place((RectTransform)idle.transform, VideoArea);
            idle.GetComponent<CanvasGroup>().blocksRaycasts = false;

            var dogs = new System.Collections.Generic.List<WallVideo>();
            RectTransform car = null;
            RectTransform logo = null;
            foreach (Part part in IdleParts)
            {
                RectTransform piece = part.IsVideo ? AddDog(idle.transform, part, dogs) : AddStill(idle.transform, part.Name, part.Path, part.Area);
                if (part.Path == ParkedCar) car = piece;
                if (part.Path == Logo) logo = piece;
            }

            AddGlow(car, "Red Light", RedLightArea, RedGlow);
            AddGlow(car, "Blue Light", BlueLightArea, BlueGlow);

            var arrival = video.GetComponent<WallVideo>();
            var arrivalData = new SerializedObject(arrival);
            arrivalData.FindProperty("playOnAwake").boolValue = false;
            arrivalData.ApplyModifiedPropertiesWithoutUndo();

            var show = wall.GetComponent<IntroShow>();
            if (show == null) show = wall.AddComponent<IntroShow>();
            var data = new SerializedObject(show);
            data.FindProperty("arrival").objectReferenceValue = arrival;
            data.FindProperty("idle").objectReferenceValue = idle.GetComponent<CanvasGroup>();
            data.FindProperty("car").objectReferenceValue = car;
            SerializedProperty dogList = data.FindProperty("dogs");
            dogList.arraySize = dogs.Count;
            for (int i = 0; i < dogs.Count; i++)
            {
                dogList.GetArrayElementAtIndex(i).objectReferenceValue = dogs[i];
            }

            data.ApplyModifiedPropertiesWithoutUndo();
            // Off until the arrival ends, so the dogs' videos do not decode while it plays.
            idle.SetActive(false);
            Undo.RegisterCreatedObjectUndo(idle, "Setup Intro Idle");
            return true;
        }

        // Brings an idle copy built earlier up to date: the car and logo keyed like the video, the
        // dogs waiting to be started, glows on the blurred front car, and every glow flashing.
        private static bool UpdateIdle(GameObject wall)
        {
            var show = wall.GetComponent<IntroShow>();
            Transform idle = wall.transform.Find("Idle");
            if (show == null || idle == null) return false;

            bool changed = false;
            Material key = KeyMaterialAsset();
            changed |= UseKeyedPicture(idle.Find("Car"), null, key);
            Transform logoPiece = wall.transform.Find("Logo Layer/Logo") ?? idle.Find("Logo");
            changed |= UsePlainPicture(logoPiece, AssetDatabase.LoadAssetAtPath<Texture2D>(Logo));

            var data = new SerializedObject(show);
            SerializedProperty dogs = data.FindProperty("dogs");
            for (int i = 0; i < dogs.arraySize; i++)
            {
                if (!(dogs.GetArrayElementAtIndex(i).objectReferenceValue is WallVideo dog)) continue;
                // Started by IntroShow, and looped without Unity's stall at the loop point: big on the wall, it shows.
                var dogData = new SerializedObject(dog);
                SerializedProperty playOnAwake = dogData.FindProperty("playOnAwake");
                SerializedProperty seamless = dogData.FindProperty("seamlessLoop");
                if (!playOnAwake.boolValue && seamless.boolValue) continue;
                playOnAwake.boolValue = false;
                seamless.boolValue = true;
                dogData.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            var front = wall.transform.Find("Car") as RectTransform;
            Graphic frontRed = FindOrAddGlow(front, "Red Light", FrontRedLightArea, RedGlow, ref changed);
            Graphic frontBlue = FindOrAddGlow(front, "Blue Light", FrontBlueLightArea, BlueGlow, ref changed);
            var parked = idle.Find("Car") as RectTransform;
            Graphic parkedRed = FindOrAddGlow(parked, "Red Light", RedLightArea, RedGlow, ref changed);
            Graphic parkedBlue = FindOrAddGlow(parked, "Blue Light", BlueLightArea, BlueGlow, ref changed);
            // The patrol car painted into the background flashes along with the others.
            Graphic backRed = BackGlow(wall.transform, "BG Red Light", BackRedArea, RedGlow, ref changed);
            Graphic backBlue = BackGlow(wall.transform, "BG Blue Light", BackBlueArea, BlueGlow, ref changed);
            changed |= SetGraphics(data.FindProperty("redLights"), parkedRed, frontRed, backRed);
            changed |= SetGraphics(data.FindProperty("blueLights"), parkedBlue, frontBlue, backBlue);
            data.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        // The standing bin the car knocks into the front corner, and IntroBinCrash to run it.
        private static bool BuildBinCrash(GameObject wall)
        {
            Transform video = wall.transform.Find("Video");
            if (video == null) return false;

            bool changed = false;
            Transform standing = wall.transform.Find("Bin Standing");
            if (standing == null)
            {
                standing = AddStill(wall.transform, "Bin Standing", Bin, StandingBinArea);
                standing.SetSiblingIndex(video.GetSiblingIndex() + 1);
                changed = true;
            }

            // Brings a bin made from the old upright picture over to the turned Bin.png.
            var standingImage = standing.GetComponent<RawImage>();
            Texture2D lyingBin = AssetDatabase.LoadAssetAtPath<Texture2D>(Bin);
            if (standingImage != null && standingImage.texture != lyingBin)
            {
                standingImage.texture = lyingBin;
                changed = true;
            }

            var standingRect = (RectTransform)standing;
            if (standingRect.anchorMin != StandingBinArea.min || standingRect.anchorMax != StandingBinArea.max)
            {
                Place(standingRect, StandingBinArea);
                changed = true;
            }

            if (!Mathf.Approximately(Mathf.DeltaAngle(standing.localEulerAngles.z, StandingBinTurn), 0f))
            {
                standing.localRotation = Quaternion.Euler(0f, 0f, StandingBinTurn);
                changed = true;
            }

            var crash = wall.GetComponent<IntroBinCrash>();
            if (crash == null)
            {
                crash = wall.AddComponent<IntroBinCrash>();
                changed = true;
            }

            // The arrival shows every frame, so the car rushing in is never skipped on a slow frame.
            var arrivalData = new SerializedObject(video.GetComponent<WallVideo>());
            SerializedProperty skip = arrivalData.FindProperty("skipFramesToKeepUp");
            if (skip != null && skip.boolValue)
            {
                skip.boolValue = false;
                arrivalData.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            var data = new SerializedObject(crash);
            Transform landed = wall.transform.Find("Bin");
            bool filled = SetIfEmpty(data.FindProperty("arrival"), video.GetComponent<WallVideo>());
            filled |= SetIfEmpty(data.FindProperty("standingBin"), standing);
            filled |= SetIfEmpty(data.FindProperty("landedBin"), landed != null ? landed.GetComponent<RawImage>() : null);
            filled |= FillSprites(data.FindProperty("trash"), ThiefSetup.LoadSprites("Assets/ART/BG/Map2/Trash", "Trash_"));
            filled |= FillSprites(data.FindProperty("puffs"), ThiefSetup.LoadSprites("Assets/ART/FX/Brawl", "Brawl_Puff_"));
            filled |= FillSprites(data.FindProperty("stars"), ThiefSetup.LoadSprites("Assets/ART/FX/Brawl", "Brawl_Star_"));
            if (filled) data.ApplyModifiedPropertiesWithoutUndo();
            return changed || filled;
        }

        // Applies the fine placement to pieces still where they were first put, so later changes made
        // in the scene are kept.
        private static bool PlaceFinely(Transform wall)
        {
            bool changed = false;
            foreach (string piece in new[] { "Video", "Idle", "Bin Standing" })
            {
                changed |= Shift(wall.Find(piece) as RectTransform, ArrivalShift, Vector3.one);
            }

            changed |= Shift(wall.Find("Car") as RectTransform, FrontCarShift, FrontCarScale);
            return changed;
        }

        private static bool Shift(RectTransform piece, Vector2 shift, Vector3 scale)
        {
            if (piece == null || piece.anchoredPosition != Vector2.zero || piece.localScale != Vector3.one) return false;

            Undo.RecordObject(piece, "Place Intro");
            piece.anchoredPosition = shift;
            piece.localScale = scale;
            return true;
        }

        // Moves the logo out of the idle copy onto its own layer over the video (same frame as the
        // video, so it keeps its place), clipped light band and star glint included, run by IntroLogo.
        private static bool BuildLogo(Transform wall)
        {
            var video = wall.Find("Video") as RectTransform;
            Transform idle = wall.Find("Idle");
            if (video == null || idle == null) return false;

            bool changed = false;
            var layer = wall.Find("Logo Layer") as RectTransform;
            if (layer == null)
            {
                layer = (RectTransform)new GameObject("Logo Layer", typeof(RectTransform)).transform;
                layer.SetParent(wall, false);
                Undo.RegisterCreatedObjectUndo(layer.gameObject, "Setup Intro Logo");
                changed = true;
            }

            // Keeps exactly the video's frame, including any fine placement given to it.
            if (layer.anchorMin != video.anchorMin || layer.anchorMax != video.anchorMax || layer.anchoredPosition != video.anchoredPosition || layer.sizeDelta != video.sizeDelta)
            {
                layer.anchorMin = video.anchorMin;
                layer.anchorMax = video.anchorMax;
                layer.anchoredPosition = video.anchoredPosition;
                layer.sizeDelta = video.sizeDelta;
                changed = true;
            }

            if (layer.GetSiblingIndex() != idle.GetSiblingIndex() + 1) layer.SetSiblingIndex(idle.GetSiblingIndex() + 1);

            var logo = layer.Find("Logo") as RectTransform;
            if (logo == null)
            {
                logo = idle.Find("Logo") as RectTransform;
                if (logo == null) return changed;
                logo.SetParent(layer, false);
                changed = true;
            }

            if (logo.GetComponent<Mask>() == null)
            {
                logo.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                changed = true;
            }

            var shine = logo.Find("Shine") as RectTransform;
            if (shine == null)
            {
                shine = AddStill(logo, "Shine", LogoShine, Rect.MinMaxRect(0.4f, -0.35f, 0.6f, 1.35f));
                shine.localRotation = Quaternion.Euler(0f, 0f, -20f);
                shine.GetComponent<RawImage>().color = new Color(1f, 1f, 1f, 0.55f);
                changed = true;
            }

            var glint = layer.Find("Star Glint") as RectTransform;
            if (glint == null)
            {
                glint = AddStill(layer, "Star Glint", Glint, StarGlintArea);
                changed = true;
            }

            var show = layer.GetComponent<IntroLogo>();
            if (show == null)
            {
                show = layer.gameObject.AddComponent<IntroLogo>();
                changed = true;
            }

            var data = new SerializedObject(show);
            bool filled = SetIfEmpty(data.FindProperty("arrival"), video.GetComponent<WallVideo>());
            filled |= SetIfEmpty(data.FindProperty("logo"), logo);
            filled |= SetIfEmpty(data.FindProperty("shine"), shine);
            filled |= SetIfEmpty(data.FindProperty("glint"), glint);
            if (filled) data.ApplyModifiedPropertiesWithoutUndo();

            // The arrival plays the copy without the video's logo.
            var arrivalData = new SerializedObject(video.GetComponent<WallVideo>());
            SerializedProperty clip = arrivalData.FindProperty("clip");
            var noLogo = AssetDatabase.LoadAssetAtPath<VideoClip>(Video);
            if (noLogo != null && clip.objectReferenceValue != noLogo)
            {
                clip.objectReferenceValue = noLogo;
                arrivalData.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            return changed || filled;
        }

        // The black curtain for fading between the intro and the game: a picture over the whole wall
        // canvas (its own canvas, drawn above everything on the wall) and a sprite over the floor.
        private static bool BuildCurtain(GameObject intro, Transform arena)
        {
            Transform side = intro.transform.parent;
            if (side == null || side.Find("Fade Curtain") != null) return false;

            var floor = new GameObject("Floor Fade", typeof(SpriteRenderer));
            floor.transform.SetParent(arena, false);
            floor.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.Euler(90f, 0f, 0f));
            floor.transform.localScale = new Vector3(16f, 9f, 1f);
            var floorSprite = floor.GetComponent<SpriteRenderer>();
            floorSprite.color = new Color(0f, 0f, 0f, 0f);
            floorSprite.sortingOrder = 100;

            var wall = new GameObject("Fade Curtain", typeof(RectTransform), typeof(Canvas), typeof(Image), typeof(FadeCurtain));
            wall.transform.SetParent(side, false);
            wall.transform.SetAsLastSibling();
            var rect = (RectTransform)wall.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            // A margin past the edges, so nothing peeks round it.
            rect.offsetMin = new Vector2(-200f, -200f);
            rect.offsetMax = new Vector2(200f, 200f);
            var canvas = wall.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            var picture = wall.GetComponent<Image>();
            picture.color = new Color(0f, 0f, 0f, 0f);
            picture.raycastTarget = false;

            var data = new SerializedObject(wall.GetComponent<FadeCurtain>());
            data.FindProperty("wall").objectReferenceValue = picture;
            data.FindProperty("floor").objectReferenceValue = floorSprite;
            data.ApplyModifiedPropertiesWithoutUndo();

            var sequence = new SerializedObject(intro.GetComponent<IntroSequence>());
            SerializedProperty curtain = sequence.FindProperty("curtain");
            if (curtain != null && curtain.objectReferenceValue == null)
            {
                curtain.objectReferenceValue = wall.GetComponent<FadeCurtain>();
                sequence.ApplyModifiedPropertiesWithoutUndo();
            }

            Undo.RegisterCreatedObjectUndo(floor, "Setup Fade");
            Undo.RegisterCreatedObjectUndo(wall, "Setup Fade");
            return true;
        }

        // Rain falling over the whole wall, then raindrops on the camera glass in front of everything.
        private static bool BuildRain(Transform wall)
        {
            bool changed = false;
            if (wall.Find("Rain") == null)
            {
                var rain = new GameObject("Rain", typeof(RectTransform), typeof(WallRain));
                rain.transform.SetParent(wall, false);
                Stretch((RectTransform)rain.transform);
                var data = new SerializedObject(rain.GetComponent<WallRain>());
                data.FindProperty("streak").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Rain/Rain_Streak.png");
                data.ApplyModifiedPropertiesWithoutUndo();
                Undo.RegisterCreatedObjectUndo(rain, "Setup Intro Rain");
                changed = true;
            }

            if (wall.Find("Lens Drops") == null)
            {
                var lens = new GameObject("Lens Drops", typeof(RectTransform), typeof(LensDrops));
                lens.transform.SetParent(wall, false);
                Stretch((RectTransform)lens.transform);
                var data = new SerializedObject(lens.GetComponent<LensDrops>());
                data.FindProperty("drop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Rain/Lens_Drop.png");
                data.FindProperty("trail").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Rain/Lens_Trail.png");
                data.ApplyModifiedPropertiesWithoutUndo();
                Undo.RegisterCreatedObjectUndo(lens, "Setup Intro Lens Drops");
                changed = true;
            }

            // The running teardrop came later; a Lens Drops made before gets it too.
            var lensDrops = wall.Find("Lens Drops").GetComponent<LensDrops>();
            var lensData = new SerializedObject(lensDrops);
            SerializedProperty sliding = lensData.FindProperty("slidingDrop");
            if (sliding.objectReferenceValue == null)
            {
                sliding.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Rain/Lens_Drop_Slide.png");
                lensData.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            // Toned down after the first look; only values still at the first settings change.
            changed |= TuneDown(lensData, "most", 22, 12);
            changed |= TuneDown(lensData, "landEvery", new Vector2(0.15f, 0.6f), new Vector2(0.35f, 1.1f));
            changed |= TuneDown(lensData, "size", new Vector2(0.025f, 0.065f), new Vector2(0.016f, 0.036f));
            changed |= TuneDown(lensData, "size", new Vector2(0.012f, 0.032f), new Vector2(0.016f, 0.036f));
            changed |= TuneDown(lensData, "size", new Vector2(0.025f, 0.07f), new Vector2(0.016f, 0.036f));
            changed |= TuneDown(lensData, "opacity", 0.85f, 0.6f);
            changed |= TuneDown(lensData, "opacity", 0.8f, 0.6f);
            var rainData = new SerializedObject(wall.Find("Rain").GetComponent<WallRain>());
            // The rain went too faint after that; brought back clearer than at first.
            changed |= TuneDown(rainData, "width", new Vector2(0.0015f, 0.0045f), new Vector2(0.0018f, 0.0045f));
            changed |= TuneDown(rainData, "width", new Vector2(0.0012f, 0.003f), new Vector2(0.0018f, 0.0045f));
            changed |= TuneDown(rainData, "brightness", new Vector2(0.12f, 0.45f), new Vector2(0.3f, 0.7f));
            changed |= TuneDown(rainData, "brightness", new Vector2(0.1f, 0.3f), new Vector2(0.3f, 0.7f));
            changed |= TuneDown(rainData, "count", 220, 480);
            changed |= TuneDown(rainData, "count", 280, 480);
            changed |= TuneDown(rainData, "length", new Vector2(0.035f, 0.11f), new Vector2(0.045f, 0.13f));
            // The lens drops ended up too small to see; set between the two earlier sizes.
            changed |= TuneDown(lensData, "size", new Vector2(0.006f, 0.016f), new Vector2(0.016f, 0.036f));
            changed |= TuneDown(lensData, "size", new Vector2(0.011f, 0.026f), new Vector2(0.016f, 0.036f));
            changed |= TuneDown(lensData, "opacity", 0.5f, 0.6f);

            // Kept in front of everything on the wall, glass last.
            Transform rainLayer = wall.Find("Rain");
            Transform lensLayer = wall.Find("Lens Drops");
            int last = wall.childCount - 1;
            if (lensLayer.GetSiblingIndex() != last || rainLayer.GetSiblingIndex() != last - 1)
            {
                rainLayer.SetAsLastSibling();
                lensLayer.SetAsLastSibling();
                changed = true;
            }

            return changed;
        }

        private static bool TuneDown(SerializedObject data, string name, int from, int to)
        {
            SerializedProperty property = data.FindProperty(name);
            if (property == null || property.intValue != from) return false;
            property.intValue = to;
            data.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool TuneDown(SerializedObject data, string name, float from, float to)
        {
            SerializedProperty property = data.FindProperty(name);
            if (property == null || !Mathf.Approximately(property.floatValue, from)) return false;
            property.floatValue = to;
            data.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool TuneDown(SerializedObject data, string name, Vector2 from, Vector2 to)
        {
            SerializedProperty property = data.FindProperty(name);
            if (property == null || (property.vector2Value - from).sqrMagnitude > 1e-10f) return false;
            property.vector2Value = to;
            data.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool FillSprites(SerializedProperty property, Sprite[] sprites)
        {
            if (property == null || property.arraySize > 0 || sprites.Length == 0) return false;
            property.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }

            return true;
        }

        // Light sources painted into BG.png, in parts of it: the parked patrol car's light bar and the
        // street lamp at the top.
        private static readonly Rect BackRedArea = Rect.MinMaxRect(0.601f, 0.295f, 0.625f, 0.385f);
        private static readonly Rect BackBlueArea = Rect.MinMaxRect(0.565f, 0.295f, 0.589f, 0.385f);
        private static readonly Rect LampArea = Rect.MinMaxRect(0.629f, 0.885f, 0.671f, 1.045f);
        private static readonly Color LampColor = new Color(1f, 0.85f, 0.55f, 0.55f);
        private const string Ripple = Folder + "/Rain/Ripple.png";
        private const string LightningSky = Folder + "/Rain/Lightning_Sky.png";

        // A glow right over the wall picture, behind everything drawn on it.
        private static Graphic BackGlow(Transform wall, string glowName, Rect area, Color color, ref bool changed)
        {
            Transform existing = wall.Find(glowName);
            if (existing != null) return existing.GetComponent<Graphic>();

            RectTransform glow = AddStill(wall, glowName, LightGlow, area);
            glow.SetSiblingIndex(0);
            glow.GetComponent<RawImage>().color = color;
            changed = true;
            return glow.GetComponent<Graphic>();
        }

        // The life of the intro wall: lightning, the street lamp, rain rings on the ground and a
        // hand-held drift of the view (IntroAmbience on the intro group).
        private static bool BuildAmbience(GameObject intro, Transform wall)
        {
            bool changed = false;
            Transform rain = wall.Find("Rain");

            var lamp = wall.Find("Lamp Glow") as RectTransform;
            if (lamp == null)
            {
                lamp = AddStill(wall, "Lamp Glow", LightGlow, LampArea);
                lamp.SetSiblingIndex(0);
                lamp.GetComponent<RawImage>().color = LampColor;
                changed = true;
            }

            var lightning = wall.Find("Lightning") as RectTransform;
            if (lightning == null)
            {
                var flash = new GameObject("Lightning", typeof(RectTransform), typeof(RawImage));
                lightning = (RectTransform)flash.transform;
                lightning.SetParent(wall, false);
                Stretch(lightning);
                var picture = flash.GetComponent<RawImage>();
                picture.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(LightningSky);
                picture.color = new Color(0.85f, 0.9f, 1f, 0f);
                picture.raycastTarget = false;
                if (rain != null) lightning.SetSiblingIndex(rain.GetSiblingIndex());
                changed = true;
            }

            // The mist was tried and dropped; layers made earlier go.
            foreach (string layerName in new[] { "Mist Far", "Mist Near" })
            {
                Transform old = wall.Find(layerName);
                if (old == null) continue;
                Undo.DestroyObjectImmediate(old.gameObject);
                changed = true;
            }

            // The lightning lights the sky only (a picture fading out by the middle), not the scene.
            var flashPicture = lightning.GetComponent<RawImage>();
            var sky = AssetDatabase.LoadAssetAtPath<Texture2D>(LightningSky);
            if (sky != null && flashPicture.texture != sky)
            {
                flashPicture.texture = sky;
                changed = true;
            }

            var ambience = intro.GetComponent<IntroAmbience>();
            if (ambience == null)
            {
                ambience = intro.AddComponent<IntroAmbience>();
                changed = true;
            }

            var data = new SerializedObject(ambience);
            bool filled = SetIfEmpty(data.FindProperty("lightning"), lightning.GetComponent<RawImage>());
            filled |= SetIfEmpty(data.FindProperty("ground"), wall);
            filled |= SetIfEmpty(data.FindProperty("ripple"), AssetDatabase.LoadAssetAtPath<Texture2D>(Ripple));
            SerializedProperty lamps = data.FindProperty("lamps");
            if (lamps.arraySize == 0)
            {
                lamps.arraySize = 1;
                lamps.GetArrayElementAtIndex(0).objectReferenceValue = lamp.GetComponent<RawImage>();
                filled = true;
            }

            if (filled) data.ApplyModifiedPropertiesWithoutUndo();
            // Gentler and rarer lightning after the first look.
            filled |= TuneDown(data, "lightningEvery", new Vector2(7f, 14f), new Vector2(18f, 35f));
            filled |= TuneDown(data, "lightningStrength", 0.4f, 0.15f);

            // The crash shakes the wall through the ambience, so the shake adds to the drift.
            var crash = wall.GetComponent<IntroBinCrash>();
            if (crash != null)
            {
                var crashData = new SerializedObject(crash);
                if (SetIfEmpty(crashData.FindProperty("ambience"), ambience))
                {
                    crashData.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }

            return changed || filled;
        }

        private static Material KeyMaterialAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(KeyMaterial);
            if (existing != null) return existing;

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ChromaKey);
            if (shader == null) return null;
            var material = new Material(shader) { name = "Intro_ChromaKey" };
            AssetDatabase.CreateAsset(material, KeyMaterial);
            return material;
        }

        private static bool UsePlainPicture(Transform piece, Texture2D texture)
        {
            RawImage image = piece != null ? piece.GetComponent<RawImage>() : null;
            if (image == null || texture == null) return false;
            if (image.material == image.defaultMaterial && image.texture == texture) return false;

            image.material = null;
            image.texture = texture;
            EditorUtility.SetDirty(image);
            return true;
        }

        // texture null keeps the picture's own.
        private static bool UseKeyedPicture(Transform piece, Texture2D texture, Material key)
        {
            RawImage image = piece != null ? piece.GetComponent<RawImage>() : null;
            if (image == null || key == null) return false;
            if (image.material == key && (texture == null || image.texture == texture)) return false;

            image.material = key;
            if (texture != null) image.texture = texture;
            EditorUtility.SetDirty(image);
            return true;
        }

        private static Graphic FindOrAddGlow(RectTransform car, string glowName, Rect area, Color color, ref bool changed)
        {
            if (car == null) return null;

            Transform existing = car.Find(glowName);
            if (existing != null) return existing.GetComponent<Graphic>();

            changed = true;
            return AddGlow(car, glowName, area, color);
        }

        private static bool SetGraphics(SerializedProperty property, params Graphic[] graphics)
        {
            var wanted = new System.Collections.Generic.List<Graphic>();
            foreach (Graphic graphic in graphics)
            {
                if (graphic != null) wanted.Add(graphic);
            }

            bool same = property.arraySize == wanted.Count;
            for (int i = 0; same && i < wanted.Count; i++)
            {
                same = property.GetArrayElementAtIndex(i).objectReferenceValue == wanted[i];
            }

            if (same) return false;
            property.arraySize = wanted.Count;
            for (int i = 0; i < wanted.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = wanted[i];
            }

            return true;
        }

        private static RectTransform AddStill(Transform parent, string pictureName, string path, Rect area)
        {
            var picture = new GameObject(pictureName, typeof(RectTransform), typeof(RawImage));
            picture.transform.SetParent(parent, false);
            var rect = (RectTransform)picture.transform;
            Place(rect, area);
            var image = picture.GetComponent<RawImage>();
            image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            image.raycastTarget = false;
            return rect;
        }

        private static RectTransform AddDog(Transform parent, Part part, System.Collections.Generic.List<WallVideo> dogs)
        {
            var dog = new GameObject(part.Name, typeof(RectTransform), typeof(RawImage));
            dog.transform.SetParent(parent, false);
            var rect = (RectTransform)dog.transform;
            Place(rect, part.Area);
            dog.GetComponent<RawImage>().raycastTarget = false;
            var video = dog.AddComponent<WallVideo>();
            var data = new SerializedObject(video);
            data.FindProperty("clip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VideoClip>(part.Path);
            data.FindProperty("chromaKeyShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(ChromaKey);
            data.ApplyModifiedPropertiesWithoutUndo();
            dogs.Add(video);
            return rect;
        }

        private static Graphic AddGlow(RectTransform car, string glowName, Rect area, Color color)
        {
            if (car == null) return null;

            var glow = new GameObject(glowName, typeof(RectTransform), typeof(RawImage));
            glow.transform.SetParent(car, false);
            Place((RectTransform)glow.transform, area);
            var image = glow.GetComponent<RawImage>();
            image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(LightGlow);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void AddVideo(Transform wall)
        {
            var video = new GameObject("Video", typeof(RectTransform), typeof(RawImage));
            video.transform.SetParent(wall, false);
            Place((RectTransform)video.transform, VideoArea);
            video.GetComponent<RawImage>().raycastTarget = false;
            var data = new SerializedObject(video.AddComponent<WallVideo>());
            data.FindProperty("clip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VideoClip>(Video);
            data.FindProperty("chromaKeyShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(ChromaKey);
            data.FindProperty("loop").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddPicture(Transform wall, string pictureName, string path, Rect area)
        {
            var picture = new GameObject(pictureName, typeof(RectTransform), typeof(RawImage));
            picture.transform.SetParent(wall, false);
            Place((RectTransform)picture.transform, area);
            var image = picture.GetComponent<RawImage>();
            image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            image.raycastTarget = false;
        }

        private static bool FillSequence(GameObject intro, GameObject wall, Transform arena)
        {
            bool changed = false;
            IntroSequence sequence = intro.GetComponent<IntroSequence>();
            if (sequence == null)
            {
                sequence = Undo.AddComponent<IntroSequence>(intro);
                changed = true;
            }

            var data = new SerializedObject(sequence);
            Transform side = intro.transform.parent;
            changed |= SetIfEmpty(data.FindProperty("introWall"), wall);
            changed |= SetIfEmpty(data.FindProperty("playWall"), side.Find("Play") != null ? side.Find("Play").gameObject : null);
            changed |= SetIfEmpty(data.FindProperty("floorImage"), FindComponent<RawImage>(arena, "FloorDisplay/Background"));
            changed |= SetIfEmpty(data.FindProperty("introFloor"), AssetDatabase.LoadAssetAtPath<Texture2D>(MenuFloor));
            changed |= SetIfEmpty(data.FindProperty("rounds"), FindComponent<BountyRounds>(arena, "Bounty"));
            changed |= SetIfEmpty(data.FindProperty("playerSpawner"), Object.FindFirstObjectByType<Fortal.Players.UWBPlayerSpawner>(FindObjectsInactive.Include));
            changed |= SetIfEmpty(data.FindProperty("scoreTags"), BuildScoreTags(arena));
            SerializedProperty pieces = data.FindProperty("gameFloorPieces");
            Transform bushes = arena.Find("Bushes");
            if (pieces.arraySize == 0 && bushes != null)
            {
                pieces.arraySize = 1;
                pieces.GetArrayElementAtIndex(0).objectReferenceValue = bushes.gameObject;
                changed = true;
            }

            if (changed) data.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        // The wall dog each character card stands for, by the name of its object on the wall.
        private static readonly (string Wall, string Character)[] WallDogs =
        {
            ("Saibirian", "Tedly"), ("Chiwawa", "Simba"), ("Pom", "Pompo"), ("Shiba", "Rex"),
        };

        // Map/Arena/Bounty gets CharacterScoreTags, each wall score tag listed under its dog's character.
        private static CharacterScoreTags BuildScoreTags(Transform arena)
        {
            Transform bounty = arena.Find("Bounty");
            if (bounty == null) return null;

            var tags = bounty.GetComponent<CharacterScoreTags>();
            bool made = tags == null;
            if (made) tags = bounty.gameObject.AddComponent<CharacterScoreTags>();
            var data = new SerializedObject(tags);
            SerializedProperty entries = data.FindProperty("entries");
            bool changed = false;
            if (made)
            {
                foreach (PlayerScoreTag tag in Object.FindObjectsByType<PlayerScoreTag>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    foreach ((string wallName, string character) in WallDogs)
                    {
                        if (Named(tag.transform, wallName) == null) continue;
                        entries.arraySize++;
                        SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                        entry.FindPropertyRelative("character").stringValue = character;
                        entry.FindPropertyRelative("tag").objectReferenceValue = tag;
                        changed = true;
                    }
                }
            }

            // Each entry also knows its dog, so a character nobody picks leaves the wall whole.
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                SerializedProperty dog = entry.FindPropertyRelative("dog");
                if (dog.objectReferenceValue != null || !(entry.FindPropertyRelative("tag").objectReferenceValue is PlayerScoreTag tag)) continue;

                foreach ((string wallName, string _) in WallDogs)
                {
                    Transform found = Named(tag.transform, wallName);
                    if (found == null) continue;
                    dog.objectReferenceValue = found.gameObject;
                    changed = true;
                }
            }

            if (changed) data.ApplyModifiedPropertiesWithoutUndo();
            return tags;
        }

        // The nearest ancestor (or the object itself) with this name, or null.
        private static Transform Named(Transform child, string ancestorName)
        {
            for (Transform t = child; t != null; t = t.parent)
            {
                if (t.name == ancestorName) return t;
            }

            return null;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // Pins the piece to a part of the wall picture, so it scales and moves with it.
        private static void Place(RectTransform rect, Rect area)
        {
            rect.anchorMin = area.min;
            rect.anchorMax = area.max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
    }
}
