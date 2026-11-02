using Fortal.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BountyHunter.Thieves.EditorTools
{
    // Sets up the stun guns each time the editor loads: imports their art as sprites
    // (Assets/ART/StunGun: the pickup Codex drew; the shot bolt, star and lock-on reticle from
    // Tools/StunGunArt), adds StunGuns and StunGunPickups next to the open scene's BountyRounds when
    // they are missing, and fills every reference left empty (the rounds, players, thieves,
    // passers-by, the art, and the radial fill material for the reticle). Points BountyRounds at the
    // guns so a bolt into a passer-by is fined. Anything set by hand is kept. Save the scene afterwards.
    [InitializeOnLoad]
    public static class StunGunAutoWire
    {
        private const string Art = "Assets/ART/StunGun/";
        private const string PickupIcon = Art + "StunGunPickup.png";
        private const string ShotIcon = Art + "StunBoltIcon.png";
        private const string Star = Art + "StunStar.png";
        private const string ReticleBack = Art + "StunReticleBack.png";
        private const string ReticleFill = Art + "StunReticleFill.png";
        private const string RadialFill = "Assets/ART/Player/Floor/Sprite_RadialFill.mat";

        static StunGunAutoWire()
        {
            EditorApplication.delayCall += Wire;
        }

        private static void Wire()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            foreach (string path in new[] { PickupIcon, ShotIcon, Star, ReticleBack, ReticleFill }) ImportAsSprite(path);

            var rounds = Object.FindFirstObjectByType<BountyRounds>(FindObjectsInactive.Include);
            if (rounds == null) return;

            var players = Object.FindFirstObjectByType<UWBPlayerSpawner>(FindObjectsInactive.Include);
            var thieves = Object.FindFirstObjectByType<ThiefSpawner>(FindObjectsInactive.Include);
            var passersBy = Object.FindFirstObjectByType<NpcCrowd>(FindObjectsInactive.Include);
            bool changed = false;

            StunGuns guns = GetOrAdd<StunGuns>(rounds.gameObject, ref changed);
            var gunFields = new SerializedObject(guns);
            changed |= Fill(gunFields, "rounds", rounds);
            changed |= Fill(gunFields, "thieves", thieves);
            changed |= Fill(gunFields, "passersBy", passersBy);
            changed |= Fill(gunFields, "shotIcon", AssetDatabase.LoadAssetAtPath<Sprite>(ShotIcon));
            changed |= Fill(gunFields, "stunStar", AssetDatabase.LoadAssetAtPath<Sprite>(Star));
            changed |= Fill(gunFields, "reticleBack", AssetDatabase.LoadAssetAtPath<Sprite>(ReticleBack));
            changed |= Fill(gunFields, "reticleFill", AssetDatabase.LoadAssetAtPath<Sprite>(ReticleFill));
            changed |= Fill(gunFields, "radialFill", AssetDatabase.LoadAssetAtPath<Material>(RadialFill));
            gunFields.ApplyModifiedPropertiesWithoutUndo();

            StunGunPickups pickups = GetOrAdd<StunGunPickups>(rounds.gameObject, ref changed);
            var pickupFields = new SerializedObject(pickups);
            changed |= Fill(pickupFields, "rounds", rounds);
            changed |= Fill(pickupFields, "guns", guns);
            changed |= Fill(pickupFields, "players", players);
            changed |= Fill(pickupFields, "hidingSpots", thieves);
            changed |= Fill(pickupFields, "icon", AssetDatabase.LoadAssetAtPath<Sprite>(PickupIcon));
            pickupFields.ApplyModifiedPropertiesWithoutUndo();

            var roundFields = new SerializedObject(rounds);
            changed |= Fill(roundFields, "stunGuns", guns);
            roundFields.ApplyModifiedPropertiesWithoutUndo();

            if (!changed) return;

            EditorSceneManager.MarkSceneDirty(rounds.gameObject.scene);
            Debug.Log("[StunGunAutoWire] The stun guns are set up; save the scene to keep them.", guns);
        }

        private static T GetOrAdd<T>(GameObject holder, ref bool changed) where T : Component
        {
            if (holder.TryGetComponent(out T found)) return found;

            changed = true;
            return holder.AddComponent<T>();
        }

        // Sets an empty reference; true when it did.
        private static bool Fill(SerializedObject fields, string name, Object value)
        {
            SerializedProperty property = fields.FindProperty(name);
            if (property == null || value == null || property.objectReferenceValue != null) return false;

            property.objectReferenceValue = value;
            return true;
        }

        // A drawing imported as a single sprite, without mipmaps, the first time.
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
