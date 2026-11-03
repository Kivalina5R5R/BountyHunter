using Fortal.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BountyHunter.Thieves.EditorTools
{
    // Sets up boss phase 1, the vault, each time the editor loads: imports its props Codex drew
    // (Assets/ART/Vault, prepared by Tools/VaultArt) as sprites; when the open scene's MapSwitcher
    // still plays the chase as its finale, adds a Boss Finale next to the chase (BossFinale and
    // BossVault) and makes it the finale, so the vault plays first and the chase after. Fills every
    // reference left empty, most of them taken from the chase (its score tags, floor picture, the
    // wall its Boss Wall sits in, its money font). Anything set by hand is kept. Save the scene
    // afterwards. Also gives the vault its floor and wall pictures (Assets/ART/BG/MapBoss/P1) and the
    // chase its rooftop's own hiding spots (Assets/ART/BG/MapBoss/P2/BossHide*).
    [InitializeOnLoad]
    public static class BossVaultAutoWire
    {
        private const string Art = "Assets/ART/Vault/";
        private const string Diamond = Art + "DiamondPedestal.png";
        private const string LightSwitch = Art + "LightSwitch.png";
        private static readonly string[] Sparkles = { "Assets/ART/Park/Sparkle1.png", "Assets/ART/Park/Sparkle2.png" };
        private const string SelectRing = "Assets/ART/Player/Floor/Load.png";
        private const string RadialFill = "Assets/ART/Player/Floor/Sprite_RadialFill.mat";
        // The chase's rooftop hiding spots.
        private static readonly string[] RooftopSpots =
        {
            "Assets/ART/BG/MapBoss/P2/BossHide1.png", "Assets/ART/BG/MapBoss/P2/BossHide2.png", "Assets/ART/BG/MapBoss/P2/BossHide3.png",
            "Assets/ART/BG/MapBoss/P2/BossHide4.png", "Assets/ART/BG/MapBoss/P2/BossHide5.png",
        };
        // The vault's own floor and wall pictures.
        private const string VaultFloor = "Assets/ART/BG/MapBoss/P1/BGFloor_Boss_P1.png";
        private const string VaultWall = "Assets/ART/BG/MapBoss/P1/BG_Boss_P1.png";

        static BossVaultAutoWire()
        {
            EditorApplication.delayCall += Wire;
        }

        private static void Wire()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            ImportAsSprite(Diamond);
            ImportAsSprite(LightSwitch);

            var switcher = Object.FindFirstObjectByType<MapSwitcher>(FindObjectsInactive.Include);
            var chase = Object.FindFirstObjectByType<BossChase>(FindObjectsInactive.Include);
            if (switcher == null || chase == null) return;

            bool changed = false;
            BossFinale finale = Object.FindFirstObjectByType<BossFinale>(FindObjectsInactive.Include);
            if (finale == null)
            {
                var holder = new GameObject("Boss Finale");
                holder.transform.SetParent(chase.transform.parent, false);
                finale = holder.AddComponent<BossFinale>();
                changed = true;
            }

            BossVault vault = finale.GetComponent<BossVault>();
            if (vault == null)
            {
                vault = finale.gameObject.AddComponent<BossVault>();
                changed = true;
            }

            var chaseFields = new SerializedObject(chase);
            changed |= Fill(chaseFields, "spotScatter", Object.FindFirstObjectByType<Fortal.Display.BushScatter>(FindObjectsInactive.Include));
            changed |= FillSprites(chaseFields.FindProperty("rooftopSpots"), RooftopSpots);
            chaseFields.ApplyModifiedPropertiesWithoutUndo();
            var vaultFields = new SerializedObject(vault);
            changed |= Fill(vaultFields, "playerSpawner", Object.FindFirstObjectByType<UWBPlayerSpawner>(FindObjectsInactive.Include));
            changed |= Fill(vaultFields, "npcs", Object.FindFirstObjectByType<NpcCrowd>(FindObjectsInactive.Include));
            changed |= Fill(vaultFields, "hidingSpots", Object.FindFirstObjectByType<ThiefSpawner>(FindObjectsInactive.Include));
            changed |= Fill(vaultFields, "timer", Object.FindFirstObjectByType<StageTimer>(FindObjectsInactive.Include));
            changed |= Fill(vaultFields, "chase", chase);
            changed |= Fill(vaultFields, "floorImage", chaseFields.FindProperty("floorImage").objectReferenceValue);
            var wallPanel = chaseFields.FindProperty("wallPanel").objectReferenceValue as GameObject;
            if (wallPanel != null) changed |= Fill(vaultFields, "wallBackground", wallPanel.transform.parent as RectTransform);
            changed |= Fill(vaultFields, "damageFont", chaseFields.FindProperty("damageFont").objectReferenceValue);
            changed |= Fill(vaultFields, "damageMaterial", chaseFields.FindProperty("damageMaterial").objectReferenceValue);
            changed |= CopyList(chaseFields.FindProperty("scoreTags"), vaultFields.FindProperty("scoreTags"));
            changed |= Fill(vaultFields, "diamondPedestal", AssetDatabase.LoadAssetAtPath<Sprite>(Diamond));
            changed |= Fill(vaultFields, "lightSwitch", AssetDatabase.LoadAssetAtPath<Sprite>(LightSwitch));
            changed |= Fill(vaultFields, "floorPicture", AssetDatabase.LoadAssetAtPath<Texture2D>(VaultFloor));
            changed |= Fill(vaultFields, "wallPicture", AssetDatabase.LoadAssetAtPath<Sprite>(VaultWall));
            changed |= Fill(vaultFields, "stealRing", AssetDatabase.LoadAssetAtPath<Sprite>(SelectRing));
            changed |= Fill(vaultFields, "radialFill", AssetDatabase.LoadAssetAtPath<Material>(RadialFill));
            changed |= FillSprites(vaultFields.FindProperty("sparkles"), Sparkles);
            vaultFields.ApplyModifiedPropertiesWithoutUndo();

            var switcherFields = new SerializedObject(switcher);
            var finaleFields = new SerializedObject(finale);
            changed |= Fill(finaleFields, "vault", vault);
            changed |= Fill(finaleFields, "chase", chase);
            changed |= Fill(finaleFields, "curtain", switcherFields.FindProperty("curtain").objectReferenceValue);
            finaleFields.ApplyModifiedPropertiesWithoutUndo();

            // The chase becomes phase 2: the finale plays the vault first.
            SerializedProperty finaleSlot = switcherFields.FindProperty("finale");
            if (finaleSlot != null && finaleSlot.objectReferenceValue == chase)
            {
                finaleSlot.objectReferenceValue = finale;
                switcherFields.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            if (!changed) return;

            EditorSceneManager.MarkSceneDirty(switcher.gameObject.scene);
            Debug.Log("[BossVaultAutoWire] Boss phase 1 (the vault) is set up before the chase; save the scene to keep it.", finale);
        }

        // Sets an empty reference; true when it did.
        private static bool Fill(SerializedObject fields, string name, Object value)
        {
            SerializedProperty property = fields.FindProperty(name);
            if (property == null || value == null || property.objectReferenceValue != null) return false;

            property.objectReferenceValue = value;
            return true;
        }

        // Copies a list of references into an empty one.
        private static bool CopyList(SerializedProperty from, SerializedProperty to)
        {
            if (from == null || to == null || to.arraySize > 0 || from.arraySize == 0) return false;

            to.arraySize = from.arraySize;
            for (int i = 0; i < from.arraySize; i++) to.GetArrayElementAtIndex(i).objectReferenceValue = from.GetArrayElementAtIndex(i).objectReferenceValue;
            return true;
        }

        private static bool FillSprites(SerializedProperty list, string[] paths)
        {
            if (list == null || list.arraySize > 0) return false;

            list.arraySize = paths.Length;
            for (int i = 0; i < paths.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
            return paths.Length > 0;
        }

        private static void ImportAsSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer || importer.textureType == TextureImporterType.Sprite) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }
}
