using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fortal.Display
{
    // Routes the installation outputs: Display 1 is the main window on the touch kiosk,
    // Display 2 is the wall and Display 3 is the LED floor.
    [DefaultExecutionOrder(-200)]
    public sealed class DisplayManager : MonoBehaviour
    {
        [Header("Display 1 - Kiosk (Touch Launcher Screen)")]
        [SerializeField] private Camera kioskCamera;
        [SerializeField] private Canvas[] kioskCanvases;
        [SerializeField] private Vector2Int kioskDisplayResolution = new Vector2Int(1080, 1920);

        [Header("Display 2 - Wall")]
        [SerializeField] private Camera sideCamera;
        [SerializeField] private Canvas[] sideCanvases;

        [Header("Display 3 - LED Floor")]
        [SerializeField] private Camera floorCamera;
        [SerializeField] private Canvas[] floorCanvases;

        [Header("Options")]
        // Activates the wall and floor outputs on startup.
        [SerializeField] private bool activateSecondDisplayOnAwake = true;
        [SerializeField] private bool applyPaperArenaResolutionsInStandalone = true;
        // In a build, keep the main window on the kiosk monitor and send the wall and floor
        // to the monitors whose shape matches their resolutions.
        [SerializeField] private bool placeOutputsOnMatchingMonitors = true;
        // Optional part of the kiosk monitor name as printed in Player.log. Leave empty to use the Windows main display.
        [SerializeField] private string kioskMonitorName = "";
        [SerializeField] private Vector2Int sideDisplayResolution = new Vector2Int(8192, 2160);
        [SerializeField] private Vector2Int floorDisplayResolution = new Vector2Int(2944, 1408);
        [SerializeField, Min(30)] private int refreshRate = 60;
        [SerializeField] private Color sideDisplayBackground = new Color(0.025f, 0.035f, 0.055f, 1f);

        public Vector2Int SideDisplayResolution => sideDisplayResolution;
        public Vector2Int FloorDisplayResolution => floorDisplayResolution;

        private int floorOutputWidth;
        private int floorOutputHeight;
        private int wallDisplayIndex = DisplayOutput.WallDisplayIndex;
        private int floorDisplayIndex = DisplayOutput.FloorDisplayIndex;

        private void Awake()
        {
            ConfigureOutputs();

            if (placeOutputsOnMatchingMonitors && !Application.isEditor)
            {
                StartCoroutine(PlaceOutputsOnMatchingMonitors());
                return;
            }

            ApplyKioskResolution();

            if (activateSecondDisplayOnAwake)
            {
                ActivateDisplays();
            }
        }

        private IEnumerator PlaceOutputsOnMatchingMonitors()
        {
            // The window is not ready to move during the first Awake.
            yield return null;

            var layout = new List<DisplayInfo>();
            Screen.GetDisplayLayout(layout);
            for (int i = 0; i < layout.Count; i++)
            {
                Debug.Log($"[DisplayManager] Monitor {i}: \"{layout[i].name}\" {layout[i].width}x{layout[i].height}, " +
                    $"workArea={layout[i].workArea}, primary={IsPrimary(layout[i])}", this);
            }

            // Unity reopens on the monitor it last used, so bring the main window back to the kiosk every launch.
            int kioskMonitor = FindKioskMonitor(layout);
            if (kioskMonitor >= 0 && !layout[kioskMonitor].Equals(Screen.mainWindowDisplayInfo))
            {
                DisplayInfo target = layout[kioskMonitor];
                Debug.Log($"[DisplayManager] Moving main window to kiosk monitor {kioskMonitor} \"{target.name}\"", this);

                // Moving a fullscreen window is unreliable, so move it as a window and go fullscreen afterwards.
                if (Screen.fullScreenMode != FullScreenMode.Windowed)
                {
                    Screen.fullScreenMode = FullScreenMode.Windowed;
                    yield return null;
                }
                yield return Screen.MoveMainWindowTo(target, Vector2Int.zero);
                yield return null;
            }

            ApplyKioskResolution();
            yield return null;
            Debug.Log($"[DisplayManager] Kiosk (main window) is on \"{Screen.mainWindowDisplayInfo.name}\"", this);

            FindSecondaryOutputs();
            ConfigureOutputs();

            if (activateSecondDisplayOnAwake)
            {
                ActivateDisplays();
            }

            StartCoroutine(KeepGameWindowsOnTop());
        }

        private void ApplyKioskResolution()
        {
            if (!applyPaperArenaResolutionsInStandalone || Application.isEditor) return;
            Screen.SetResolution(kioskDisplayResolution.x, kioskDisplayResolution.y,
                FullScreenMode.FullScreenWindow, TargetRefreshRate);
        }

        private RefreshRate TargetRefreshRate => new RefreshRate
        {
            numerator = (uint)refreshRate,
            denominator = 1u
        };

        // New display windows appear a few frames after activation, and the launcher may still hold focus.
        private IEnumerator KeepGameWindowsOnTop()
        {
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForSecondsRealtime(1f);
                GameWindowsOnTop.Apply();
            }
        }

        private int FindKioskMonitor(List<DisplayInfo> layout)
        {
            if (!string.IsNullOrWhiteSpace(kioskMonitorName))
            {
                for (int i = 0; i < layout.Count; i++)
                {
                    if (layout[i].name != null &&
                        layout[i].name.IndexOf(kioskMonitorName.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return i;
                    }
                }
                Debug.LogWarning($"[DisplayManager] No monitor name contains \"{kioskMonitorName}\"; using the Windows main display", this);
            }

            for (int i = 0; i < layout.Count; i++)
            {
                if (IsPrimary(layout[i])) return i;
            }

            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < layout.Count; i++)
            {
                float score = MatchScore(layout[i].width, layout[i].height, kioskDisplayResolution);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        // Display.displays[0] is the main window (kiosk). The floor picks first from the other outputs,
        // then the wall takes the closest remaining shape.
        private void FindSecondaryOutputs()
        {
            var displays = UnityEngine.Display.displays;
            for (int i = 0; i < displays.Length; i++)
            {
                Debug.Log($"[DisplayManager] Unity display {i}: {displays[i].systemWidth}x{displays[i].systemHeight}", this);
            }

            floorDisplayIndex = FindOutput(floorDisplayResolution, -1);
            wallDisplayIndex = FindOutput(sideDisplayResolution, floorDisplayIndex);
        }

        private static int FindOutput(Vector2Int resolution, int excludedIndex)
        {
            var displays = UnityEngine.Display.displays;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 1; i < displays.Length; i++)
            {
                if (i == excludedIndex) continue;
                float score = MatchScore(displays[i].systemWidth, displays[i].systemHeight, resolution);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        // The Windows main display starts at the desktop origin; the work area only loses the taskbar.
        private static bool IsPrimary(DisplayInfo display)
        {
            return Mathf.Abs(display.workArea.x) < display.width / 4 && Mathf.Abs(display.workArea.y) < display.height / 4;
        }

        // Lower is better. Aspect ratio decides the role; exact size only breaks ties.
        private static float MatchScore(int width, int height, Vector2Int resolution)
        {
            if (width <= 0 || height <= 0 || resolution.x <= 0 || resolution.y <= 0) return float.MaxValue;
            float aspectDifference = Mathf.Abs((float)width / height - (float)resolution.x / resolution.y);
            float sizeDifference = Mathf.Abs(width - resolution.x) + Mathf.Abs(height - resolution.y);
            return aspectDifference * 100000f + sizeDifference;
        }

        // Resolution changes complete after the current frame. Fit again when the floor output changes.
        private void LateUpdate()
        {
            if (Application.isEditor || !applyPaperArenaResolutionsInStandalone || floorCamera == null) return;
            var displays = UnityEngine.Display.displays;
            if (floorDisplayIndex < 0 || floorDisplayIndex >= displays.Length || !displays[floorDisplayIndex].active) return;
            UnityEngine.Display floor = displays[floorDisplayIndex];
            int width = floor.renderingWidth;
            int height = floor.renderingHeight;
            if (width <= 0 || height <= 0 || floorDisplayResolution.x <= 0 || floorDisplayResolution.y <= 0) return;
            if (width == floorOutputWidth && height == floorOutputHeight) return;
            floorOutputWidth = width;
            floorOutputHeight = height;

            float referenceAspect = (float)floorDisplayResolution.x / floorDisplayResolution.y;
            float outputAspect = (float)width / height;
            Rect viewport = new Rect(0f, 0f, 1f, 1f);
            if (outputAspect > referenceAspect)
            {
                viewport.width = referenceAspect / outputAspect;
                viewport.x = (1f - viewport.width) * 0.5f;
            }
            else
            {
                viewport.height = outputAspect / referenceAspect;
                viewport.y = (1f - viewport.height) * 0.5f;
            }
            floorCamera.rect = viewport;
            floorCamera.aspect = referenceAspect;
            Debug.Log($"[DisplayManager] Floor requested={floorDisplayResolution.x}x{floorDisplayResolution.y}, " +
                $"rendering={width}x{height}, monitor={floor.systemWidth}x{floor.systemHeight}, " +
                $"display={floorDisplayIndex}, displays={displays.Length}, viewport={viewport}", this);
        }

        [ContextMenu("Configure Camera And Canvas Outputs")]
        public void ConfigureOutputs()
        {
            // Role constants describe the editor preview; a build may move the wall and floor
            // to the Unity displays that match their monitors.
            if (kioskCamera != null)
            {
                kioskCamera.targetDisplay = DisplayOutput.KioskDisplayIndex;
            }

            if (floorCamera != null)
            {
                floorCamera.targetDisplay = OutputIndex(floorDisplayIndex, DisplayOutput.FloorDisplayIndex);
            }

            if (sideCamera != null)
            {
                sideCamera.targetDisplay = OutputIndex(wallDisplayIndex, DisplayOutput.WallDisplayIndex);
                sideCamera.clearFlags = CameraClearFlags.SolidColor;
                sideCamera.backgroundColor = sideDisplayBackground;
            }

            SetCanvasDisplay(kioskCanvases, DisplayOutput.KioskDisplayIndex);
            SetCanvasDisplay(floorCanvases, OutputIndex(floorDisplayIndex, DisplayOutput.FloorDisplayIndex));
            SetCanvasDisplay(sideCanvases, OutputIndex(wallDisplayIndex, DisplayOutput.WallDisplayIndex));
        }

        private static int OutputIndex(int foundIndex, int roleIndex)
        {
            return foundIndex >= 0 ? foundIndex : roleIndex;
        }

        [ContextMenu("Activate Displays")]
        public void ActivateDisplays()
        {
            if (UnityEngine.Display.displays.Length < 2)
            {
                Debug.LogWarning("[DisplayManager] ระบบปฏิบัติการรายงานจอเพียง 1 จอ", this);
                return;
            }

            ActivateOutput(wallDisplayIndex, sideDisplayResolution, "Wall");
            ActivateOutput(floorDisplayIndex, floorDisplayResolution, "Floor");
        }

        private void ActivateOutput(int displayIndex, Vector2Int resolution, string role)
        {
            var displays = UnityEngine.Display.displays;
            if (displayIndex < 1 || displayIndex >= displays.Length)
            {
                Debug.LogWarning($"[DisplayManager] {role} output has no monitor (displays={displays.Length})", this);
                return;
            }

            if (applyPaperArenaResolutionsInStandalone && !Application.isEditor)
            {
                displays[displayIndex].Activate(resolution.x, resolution.y, TargetRefreshRate);
            }
            else
            {
                displays[displayIndex].Activate();
            }
            Debug.Log($"[DisplayManager] {role} output on Unity display {displayIndex} " +
                $"({displays[displayIndex].systemWidth}x{displays[displayIndex].systemHeight})", this);
        }

        private static void SetCanvasDisplay(Canvas[] canvases, int displayIndex)
        {
            if (canvases == null)
            {
                return;
            }

            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null)
                {
                    canvases[i].targetDisplay = displayIndex;
                }
            }
        }
    }
}
