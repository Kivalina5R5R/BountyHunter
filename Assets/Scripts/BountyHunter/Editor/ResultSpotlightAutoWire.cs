using UnityEditor;
using UnityEngine;

namespace BountyHunter.Thieves.EditorTools
{
    // Adds the winner's spotlight (WinnerSpotlight) to the result prefab
    // (Assets/Prefabs/UI/Result.prefab) the first time the editor loads without it. It draws its own
    // pictures, so there is nothing else to fill in; tune it on the prefab's root.
    [InitializeOnLoad]
    public static class ResultSpotlightAutoWire
    {
        private const string PrefabPath = "Assets/Prefabs/UI/Result.prefab";

        static ResultSpotlightAutoWire()
        {
            EditorApplication.delayCall += Wire;
        }

        private static void Wire()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || prefab.GetComponent<ResultScreen>() == null || prefab.GetComponent<WinnerSpotlight>() != null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                root.AddComponent<WinnerSpotlight>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[ResultSpotlightAutoWire] The result's winner gets a spotlight", prefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
