using Fortal.Display;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fortal.Editor
{
    // Turns the kiosk's and the wall's pop-ups into prefabs of their own in Assets/Prefabs/UI, so
    // each can be opened and changed by itself: the "exit the game?" dialog (Exit Confirm) and the
    // tutorial remote (Tutorial Remote) become prefabs nested in the kiosk prefab, and the HOW TO
    // PLAY board on the wall (Wall Tutorial) a prefab placed in the open scene. The exit dialog's
    // buttons are put EXIT on the left, CANCEL on the right as it is split off. Making a nested
    // prefab gives its pieces new identities, so the wall board is pointed at the kiosk's remote
    // again afterwards. Runs whenever the editor loads and a piece is still not a prefab; the scene
    // is marked changed and left for you to save.
    [InitializeOnLoad]
    public static class KioskUiPrefabs
    {
        private const string KioskPath = "Assets/Prefabs/Display/KioskDisplay.prefab";
        private const string Folder = "Assets/Prefabs/UI/";
        private const string ExitConfirmName = "Exit Confirm";
        private const string RemoteName = "Tutorial Remote";
        private const string WallTutorialName = "Wall Tutorial";

        static KioskUiPrefabs()
        {
            EditorApplication.delayCall += Split;
        }

        private static void Split()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            SplitKiosk();
            // The scene's kiosk picks up the saved prefab first.
            EditorApplication.delayCall += SplitWall;
        }

        private static void SplitKiosk()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(KioskPath);
            try
            {
                Transform canvas = root.transform.Find("KioskCanvas");
                if (canvas == null) return;

                bool changed = false;
                Transform exit = canvas.Find(ExitConfirmName);
                if (exit != null && !PrefabUtility.IsPartOfPrefabInstance(exit.gameObject))
                {
                    PutExitOnTheLeft(exit);
                    changed |= MakePrefab(exit.gameObject, ExitConfirmName);
                }

                Transform remote = canvas.Find(RemoteName);
                if (remote != null && !PrefabUtility.IsPartOfPrefabInstance(remote.gameObject))
                    changed |= MakePrefab(remote.gameObject, RemoteName);

                if (!changed) return;
                PrefabUtility.SaveAsPrefabAsset(root, KioskPath);
                Debug.Log("[KioskUiPrefabs] The kiosk's Exit Confirm and Tutorial Remote are prefabs of their own in " + Folder, AssetDatabase.LoadAssetAtPath<GameObject>(KioskPath));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // EXIT on the left, CANCEL on the right: their places are swapped while CANCEL is on the left.
        private static void PutExitOnTheLeft(Transform dialog)
        {
            var confirm = dialog.Find("Board/ConfirmButton") as RectTransform;
            var cancel = dialog.Find("Board/CancelButton") as RectTransform;
            if (confirm == null || cancel == null || cancel.anchoredPosition.x >= confirm.anchoredPosition.x) return;

            (confirm.anchoredPosition, cancel.anchoredPosition) = (cancel.anchoredPosition, confirm.anchoredPosition);
        }

        private static void SplitWall()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;

            bool changed = false;
            GameObject wall = FindRoot(scene, WallTutorialName);
            if (wall != null && !PrefabUtility.IsPartOfPrefabInstance(wall)) changed |= MakePrefab(wall, WallTutorialName);

            changed |= ReconnectRemote(scene);
            if (!changed) return;

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[KioskUiPrefabs] The wall's HOW TO PLAY board is a prefab in {Folder}: save the scene to keep it", wall);
        }

        // Points the wall board at the kiosk's remote when it has lost it.
        private static bool ReconnectRemote(Scene scene)
        {
            TutorialPopup popup = FindInScene<TutorialPopup>(scene);
            TutorialRemote remote = FindInScene<TutorialRemote>(scene);
            if (popup == null || remote == null) return false;

            var fields = new SerializedObject(popup);
            SerializedProperty property = fields.FindProperty("remote");
            if (property == null || property.objectReferenceValue != null) return false;

            property.objectReferenceValue = remote;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool MakePrefab(GameObject piece, string name)
        {
            string path = Folder + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.LogWarning($"[KioskUiPrefabs] {path} already exists; {piece.name} is left as it is", piece);
                return false;
            }

            try
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(piece, path, InteractionMode.AutomatedAction);
                return true;
            }
            catch (System.Exception problem)
            {
                Debug.LogError($"[KioskUiPrefabs] Could not make {path} from {piece.name}: {problem.Message}", piece);
                return false;
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject top in scene.GetRootGameObjects())
            {
                foreach (Transform child in top.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == name) return child.gameObject;
                }
            }

            return null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject top in scene.GetRootGameObjects())
            {
                T found = top.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }

            return null;
        }
    }
}
