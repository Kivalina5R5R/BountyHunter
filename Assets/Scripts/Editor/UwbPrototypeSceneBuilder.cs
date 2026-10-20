using Fortal.Configuration;
using Fortal.Display;
using Fortal.Players;
using Fortal.UWB;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Fortal.Editor
{
    // Builds the UWB installation layout (kiosk, wall and floor outputs with the painted Bounty Hunter
    // backgrounds, arena and player spawner) into the open scene. Positions and sizes match the FoodIsekaiZ installation: an 11 x 5.26 floor
    // seen from above and a 1536 x 435 wall canvas standing along the back edge of the floor.
    // It only adds what is missing, so it is safe to run again; it never touches gameplay objects.
    [InitializeOnLoad]
    public static class UwbPrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/BountyHunter.unity";
        private const string PrefabFolder = "Assets/Prefabs";
        private const string PlayerPrefabPath = PrefabFolder + "/UWBPlayer.prefab";
        private const string WallArtPath = "Assets/ART/BG/Hunter_BG.png";
        private const string FloorArtPath = "Assets/ART/BG/Map2/FloorMap2.png";
        private const string BushArtFolder = "Assets/ART/BG/Map2";
        private const string BushArtPrefix = "pummai";
        private const string KioskPrefabPath = PrefabFolder + "/KioskDisplay.prefab";
        private const string KioskName = "KioskDisplay";
        private const string AutoBuildKey = "Fortal.UwbPrototypeSceneBuilt";

        // The floor is 11 world units wide; its depth follows the 2944 x 1408 LED floor output.
        private static readonly Vector2 ArenaSize = new Vector2(11f, 5.2608695f);
        private const float FloorCameraSize = 2.6304348f;
        private const float WallCameraSize = 1.5576172f;
        private static readonly Vector2 WallCanvasSize = new Vector2(1536f, 435f);
        private const float WallCanvasScale = 0.0071614585f;
        private const float WallCanvasDepth = 2.52f;
        private static readonly Color WallBackground = new Color(0.025f, 0.035f, 0.055f, 1f);
        private static readonly Vector2 FloorCanvasSize = new Vector2(2944f, 1408f);

        // The kiosk canvas stands to the right of the wall, outside the wall and floor camera views.
        private static readonly Vector3 KioskPosition = new Vector3(9.6f, WallCameraSize, WallCanvasDepth);
        private static readonly Vector2 KioskCanvasSize = new Vector2(1080f, 1920f);
        private const float KioskCanvasScale = 0.005f;

        static UwbPrototypeSceneBuilder()
        {
            // Builds the BountyHunter scene once after this script is first imported, and upgrades scenes built by older versions.
            EditorApplication.delayCall += () =>
            {
                // Upgrades a player prefab made before the tag circle marker existed.
                var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                if (player != null && player.GetComponentInChildren<TagCircle>(true) == null) CreatePlayerPrefab(true);

                Scene scene = SceneManager.GetActiveScene();
                if (!scene.isLoaded || scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (EditorPrefs.GetBool(AutoBuildKey, false) && !NeedsUpgrade()) return;
                EditorPrefs.SetBool(AutoBuildKey, true);
                bool wasDirty = scene.isDirty;
                Build();
                if (!wasDirty) EditorSceneManager.SaveScene(scene);
            };
        }

        // True when the scene still has the overlay kiosk or lacks the painted floor or the bushes.
        private static bool NeedsUpgrade()
        {
            return GameObject.Find("Display/Display1_Kiosk") != null ||
                GameObject.Find("Map/Arena/FloorDisplay") == null ||
                GameObject.Find("Map/Arena/Bushes") == null;
        }

        [MenuItem("Tools/UWB Prototype/Build Scene Layout")]
        public static void Build()
        {
            Scene scene = SceneManager.GetActiveScene();
            UWBPlayerController playerPrefab = CreatePlayerPrefab(false);

            RemoveDefaultMainCamera();
            UWBManager uwbManager = BuildSystems();
            BuildDisplays(out Camera kioskCamera, out Canvas kioskCanvas, out Camera wallCamera, out Camera floorCamera);
            Canvas wallCanvas = BuildMap(wallCamera, floorCamera);
            BuildPlayers(playerPrefab);
            BuildEventSystem();

            DisplayManager displayManager = Object.FindFirstObjectByType<DisplayManager>(FindObjectsInactive.Include);
            var displayData = new SerializedObject(displayManager);
            displayData.FindProperty("kioskCamera").objectReferenceValue = kioskCamera;
            displayData.FindProperty("sideCamera").objectReferenceValue = wallCamera;
            displayData.FindProperty("floorCamera").objectReferenceValue = floorCamera;
            SetSingle(displayData.FindProperty("kioskCanvases"), kioskCanvas);
            SetSingle(displayData.FindProperty("sideCanvases"), wallCanvas);
            displayData.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[UwbPrototypeSceneBuilder] Scene layout ready (UWB {uwbManager.name}). Game view: Display 1 kiosk, Display 2 wall, Display 3 floor.");
        }

        private static UWBManager BuildSystems()
        {
            GameObject systems = FindOrCreateRoot("Systems");
            GetOrAdd<UWBConfigManager>(systems);
            UWBManager manager = systems.GetComponent<UWBManager>();
            if (manager != null) return manager;

            manager = systems.AddComponent<UWBManager>();
            // Start in Simulation so the scene runs without hardware; switch to Serial on site.
            var data = new SerializedObject(manager);
            data.FindProperty("transportMode").enumValueIndex = (int)UWBTransportMode.Simulation;
            data.ApplyModifiedPropertiesWithoutUndo();
            return manager;
        }

        private static void BuildDisplays(out Camera kioskCamera, out Canvas kioskCanvas, out Camera wallCamera, out Camera floorCamera)
        {
            GameObject display = FindOrCreateRoot("Display");
            GetOrAdd<DisplayManager>(display);

            GameObject kiosk = BuildKioskDisplay(display.transform);
            kioskCamera = kiosk.GetComponentInChildren<Camera>(true);
            kioskCanvas = kiosk.GetComponentInChildren<Canvas>(true);

            Transform wallRoot = FindOrCreateChild(display.transform, "Display2_Wall");
            wallCamera = FindOrCreateCamera(wallRoot, "SideCamera", out bool wallCreated);
            if (wallCreated)
            {
                wallCamera.transform.localPosition = new Vector3(0f, WallCameraSize, WallCanvasDepth - 10f);
                wallCamera.orthographic = true;
                wallCamera.orthographicSize = WallCameraSize;
                wallCamera.clearFlags = CameraClearFlags.SolidColor;
                wallCamera.backgroundColor = WallBackground;
                wallCamera.targetDisplay = DisplayOutput.WallDisplayIndex;
            }

            Transform floorRoot = FindOrCreateChild(display.transform, "Display3_Floor");
            floorCamera = FindOrCreateCamera(floorRoot, "FloorCamera", out bool floorCreated);
            if (floorCreated)
            {
                floorCamera.transform.localPosition = new Vector3(0f, 10f, 0f);
                floorCamera.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                floorCamera.orthographic = true;
                floorCamera.orthographicSize = FloorCameraSize;
                floorCamera.clearFlags = CameraClearFlags.SolidColor;
                floorCamera.backgroundColor = Color.black;
                floorCamera.targetDisplay = DisplayOutput.FloorDisplayIndex;
                floorCamera.gameObject.AddComponent<AudioListener>();
            }
        }

        // Places the kiosk prefab under Display, replacing the earlier overlay kiosk if present.
        private static GameObject BuildKioskDisplay(Transform display)
        {
            Transform existing = display.Find(KioskName);
            if (existing != null) return existing.gameObject;

            Transform legacy = display.Find("Display1_Kiosk");
            if (legacy != null) Undo.DestroyObjectImmediate(legacy.gameObject);

            GameObject prefab = CreateKioskPrefab();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, display);
            instance.transform.SetAsFirstSibling();
            return instance;
        }

        // Kiosk screen laid out like ChaosDisplay in FoodIsekaiZ: a world-space 1080 x 1920 canvas
        // standing beside the wall, so it can be edited in the Scene view, and its own camera that
        // renders only the UI layer onto Display 1. No sprites; recolor Background or add art to it.
        private static GameObject CreateKioskPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(KioskPrefabPath);
            if (existing != null) return existing;

            EnsureFolder(PrefabFolder);
            int uiLayer = LayerMask.NameToLayer("UI");
            var root = new GameObject(KioskName);
            root.transform.localPosition = KioskPosition;

            var cameraObject = new GameObject("KioskCamera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = KioskCanvasSize.y * KioskCanvasScale * 0.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << uiLayer;
            camera.targetDisplay = DisplayOutput.KioskDisplayIndex;

            var canvasObject = new GameObject("KioskCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = uiLayer;
            canvasObject.transform.SetParent(root.transform, false);
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = KioskCanvasSize;
            canvasRect.localScale = Vector3.one * KioskCanvasScale;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.targetDisplay = DisplayOutput.KioskDisplayIndex;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.referenceResolution = KioskCanvasSize;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject background = CreateUiChild("Background", canvasObject.transform, uiLayer);
            Stretch((RectTransform)background.transform);
            background.AddComponent<Image>().color = new Color(0.06f, 0.07f, 0.1f, 1f);

            GameObject button = CreateUiChild("ExitButton", canvasObject.transform, uiLayer);
            var buttonRect = (RectTransform)button.transform;
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 109f);
            buttonRect.sizeDelta = new Vector2(560f, 150f);
            button.AddComponent<Image>().color = new Color(1f, 0.78f, 0.38f, 1f);
            button.AddComponent<Button>();
            button.AddComponent<ExitGameButton>();

            GameObject labelObject = CreateUiChild("Label", button.transform, uiLayer);
            Stretch((RectTransform)labelObject.transform);
            var label = labelObject.AddComponent<Text>();
            label.text = "EXIT";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 64;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.16f, 0.09f, 0.03f, 1f);
            label.raycastTarget = false;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, KioskPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateUiChild(string name, Transform parent, int layer)
        {
            var child = new GameObject(name, typeof(RectTransform)) { layer = layer };
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
        }

        private static Canvas BuildMap(Camera wallCamera, Camera floorCamera)
        {
            GameObject map = FindOrCreateRoot("Map");
            Transform arena = FindOrCreateChild(map.transform, "Arena");
            BuildFloorDisplay(arena, floorCamera);
            BuildBushes(arena);

            Transform existing = map.transform.Find("SideDisplay");
            Canvas canvas = existing != null ? existing.GetComponent<Canvas>() : CreateWallCanvas(map.transform, wallCamera);
            BuildPaintedBackground(canvas.transform, WallArtPath);
            return canvas;
        }

        // World-space wall canvas standing upright along the back edge of the floor (L-shaped preview).
        private static Canvas CreateWallCanvas(Transform map, Camera wallCamera)
        {
            var canvasObject = new GameObject("SideDisplay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(map, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = wallCamera;
            canvas.targetDisplay = DisplayOutput.WallDisplayIndex;
            var rect = (RectTransform)canvasObject.transform;
            rect.sizeDelta = WallCanvasSize;
            rect.localScale = Vector3.one * WallCanvasScale;
            rect.localPosition = new Vector3(0f, WallCameraSize, WallCanvasDepth);
            canvasObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 16f;
            return canvas;
        }

        // World-space floor canvas lying flat over the 11 x 5.26 arena, seen by the floor camera.
        // It replaces the plain floor quad made by earlier versions of this builder.
        private static void BuildFloorDisplay(Transform arena, Camera floorCamera)
        {
            Transform legacyQuad = arena.Find("Background");
            if (legacyQuad != null && legacyQuad.GetComponent<MeshRenderer>() != null) Undo.DestroyObjectImmediate(legacyQuad.gameObject);

            Transform floor = arena.Find("FloorDisplay");
            if (floor == null)
            {
                var canvasObject = new GameObject("FloorDisplay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(arena, false);
                canvasObject.transform.SetAsFirstSibling();
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = floorCamera;
                canvas.targetDisplay = DisplayOutput.FloorDisplayIndex;
                var rect = (RectTransform)canvasObject.transform;
                rect.sizeDelta = FloorCanvasSize;
                rect.localScale = Vector3.one * (ArenaSize.x / FloorCanvasSize.x);
                rect.localRotation = Quaternion.Euler(90f, 0f, 0f);
                rect.localPosition = Vector3.zero;
                floor = canvasObject.transform;
            }

            BuildPaintedBackground(floor, FloorArtPath);
        }

        // Fills a canvas with painted artwork that keeps its aspect (covering the canvas, cropping
        // the overflow). Replaces a plain color Background.
        private static void BuildPaintedBackground(Transform canvas, string artPath)
        {
            Transform existing = canvas.Find("Background");
            RawImage existingImage = existing != null ? existing.GetComponent<RawImage>() : null;
            if (existingImage != null)
            {
                // Refills a background whose artwork was removed from the project.
                if (existingImage.texture == null) AssignArtwork(existingImage, artPath);
                return;
            }
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            var background = new GameObject("Background", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            background.layer = canvas.gameObject.layer;
            background.transform.SetParent(canvas, false);
            background.transform.SetAsFirstSibling();
            Stretch((RectTransform)background.transform);

            var image = background.GetComponent<RawImage>();
            image.raycastTarget = false;
            background.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            AssignArtwork(image, artPath);
        }

        private static void AssignArtwork(RawImage image, string artPath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(artPath);
            if (texture == null) return;

            Undo.RecordObject(image, "Assign Background Artwork");
            image.texture = texture;
            AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter>();
            if (fitter != null)
            {
                Undo.RecordObject(fitter, "Assign Background Artwork");
                fitter.aspectRatio = (float)texture.width / texture.height;
            }
        }

        // Random bushes over the floor, spawned by BushScatter when Play starts.
        private static void BuildBushes(Transform arena)
        {
            if (arena.Find("Bushes") != null) return;

            var bushes = new GameObject("Bushes", typeof(BushScatter));
            bushes.transform.SetParent(arena, false);
            var sprites = AssetDatabase.FindAssets($"{BushArtPrefix} t:Sprite", new[] { BushArtFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(path => path)
                .Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path))
                .Where(sprite => sprite != null)
                .ToArray();

            var data = new SerializedObject(bushes.GetComponent<BushScatter>());
            SerializedProperty array = data.FindProperty("bushSprites");
            array.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildPlayers(UWBPlayerController playerPrefab)
        {
            GameObject players = FindOrCreateRoot("Players");
            if (players.GetComponent<UWBPlayerSpawner>() != null) return;

            UWBPlayerSpawner spawner = players.AddComponent<UWBPlayerSpawner>();
            var data = new SerializedObject(spawner);
            data.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        // The FoodIsekaiZ tag marker shown before a player picks a number: a flat cream circle
        // (120 px canvas at 0.012 scale) lying on the floor. Overwriting keeps the prefab GUID.
        private static UWBPlayerController CreatePlayerPrefab(bool overwrite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<UWBPlayerController>(PlayerPrefabPath);
            if (existing != null && !overwrite) return existing;

            EnsureFolder(PrefabFolder);
            var root = new GameObject("UWBPlayer", typeof(Rigidbody), typeof(SphereCollider));
            root.GetComponent<SphereCollider>().radius = 0.5f;

            var marker = new GameObject("Tag Marker", typeof(RectTransform), typeof(Canvas));
            marker.transform.SetParent(root.transform, false);
            var markerRect = (RectTransform)marker.transform;
            markerRect.localRotation = Quaternion.Euler(90f, 0f, 0f);
            markerRect.localScale = Vector3.one * 0.012f;
            markerRect.sizeDelta = new Vector2(120f, 120f);
            var markerCanvas = marker.GetComponent<Canvas>();
            markerCanvas.renderMode = RenderMode.WorldSpace;
            markerCanvas.overrideSorting = true;
            markerCanvas.sortingOrder = 20;

            var circleObject = new GameObject("Tag Circle", typeof(RectTransform), typeof(TagCircle));
            circleObject.transform.SetParent(marker.transform, false);
            ((RectTransform)circleObject.transform).sizeDelta = new Vector2(120f, 120f);
            var circle = circleObject.GetComponent<TagCircle>();
            circle.color = new Color(1f, 0.94f, 0.75f, 0.72f);
            circle.raycastTarget = false;

            var controller = root.AddComponent<UWBPlayerController>();
            var data = new SerializedObject(controller);
            data.FindProperty("marker").objectReferenceValue = marker;
            data.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<UWBPlayerController>();
        }

        // The template's Main Camera would render scene objects onto the kiosk display.
        private static void RemoveDefaultMainCamera()
        {
            GameObject mainCamera = GameObject.Find("Main Camera");
            if (mainCamera != null && mainCamera.transform.parent == null)
            {
                Undo.DestroyObjectImmediate(mainCamera);
            }
        }

        private static Camera FindOrCreateCamera(Transform parent, string name, out bool created)
        {
            Transform existing = parent.Find(name);
            created = existing == null;
            if (!created) return existing.GetComponent<Camera>();

            var cameraObject = new GameObject(name, typeof(Camera));
            cameraObject.transform.SetParent(parent, false);
            return cameraObject.GetComponent<Camera>();
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name) return root;
            }

            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Build UWB Prototype Scene");
            return created;
        }

        private static Transform FindOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;

            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void SetSingle(SerializedProperty array, Object value)
        {
            array.arraySize = 1;
            array.GetArrayElementAtIndex(0).objectReferenceValue = value;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            AssetDatabase.CreateFolder("Assets", path.Substring("Assets/".Length));
        }
    }
}
