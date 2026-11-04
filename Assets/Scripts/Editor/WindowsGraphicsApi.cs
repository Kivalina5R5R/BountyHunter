using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fortal.Editor
{
    // Keeps Windows builds on Direct3D 11. On Direct3D 12 the video players' frames reach the game
    // through Windows' D3D11-on-12 bridge, and with as many videos open as this game keeps (the dogs,
    // the rabbit, the sloth) that bridge loses the graphics device mid-game ("Unrecoverable D3D12
    // device error", WindowsVideoTextureOutput error 0x887a0005) and the game crashes. Direct3D 11
    // hands the frames over directly. Set whenever the editor loads and again before every build,
    // so the setting cannot quietly slip back to Auto.
    [InitializeOnLoad]
    public sealed class WindowsGraphicsApi : IPreprocessBuildWithReport
    {
        private static readonly BuildTarget[] Targets = { BuildTarget.StandaloneWindows64, BuildTarget.StandaloneWindows };
        private static readonly GraphicsDeviceType[] Wanted = { GraphicsDeviceType.Direct3D11 };

        static WindowsGraphicsApi()
        {
            EditorApplication.delayCall += Apply;
        }

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Apply();
        }

        private static void Apply()
        {
            foreach (BuildTarget target in Targets)
            {
                if (IsSet(target)) continue;

                PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
                PlayerSettings.SetGraphicsAPIs(target, Wanted);
                Debug.Log($"[WindowsGraphicsApi] {target} builds now use Direct3D 11 only (Direct3D 12 crashes with many videos open)");
            }
        }

        private static bool IsSet(BuildTarget target)
        {
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(target)) return false;

            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(target);
            return apis.Length == 1 && apis[0] == GraphicsDeviceType.Direct3D11;
        }
    }
}
