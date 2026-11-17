using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Fortal.Arena.EditorTools
{
    // Warns at the end of a build if no built scene has anything connecting the tutorial (the script alone in the project does nothing).
    // Goes by class name: FortalArenaRemoteBridge, or a class of your own with "ArenaRemote" in its name.
    sealed class FortalArenaRemoteBuildCheck : IPreprocessBuildWithReport, IProcessSceneWithReport, IPostprocessBuildWithReport
    {
        static bool found;
        public int callbackOrder { get { return 0; } }
        public void OnPreprocessBuild(BuildReport report) { found = false; }
        public void OnProcessScene(UnityEngine.SceneManagement.Scene scene, BuildReport report)
        {
            if (report == null) return;   // entering Play mode, not a build
            foreach (var root in scene.GetRootGameObjects())
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) continue;
                    var name = behaviour.GetType().Name;
                    if (name.Contains("ArenaRemote") && name != "FortalArenaRemote") found = true;
                }
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            if (!found) Debug.LogWarning("[FortalArenaRemote] No scene in this build connects the tutorial: the launcher's TUTORIAL button will not work. Add FortalArenaRemoteBridge to an active object and set it up (README_TH.md).");
        }
    }
}
