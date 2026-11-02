using Fortal.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BountyHunter.Thieves.EditorTools
{
    // Sets up the park's police tape and mud (stage 2) each time the editor loads: imports the art
    // Codex drew (Assets/ART/Park) as sprites, adds ParkPoliceTape and MudPuddles next to the open
    // scene's BountyRounds when they are missing, fills every reference left empty (the crate's ring
    // from Tools/ParkArt, drawn with the radial fill material), and points the stun guns at
    // the mud so locking on from it is slower. Anything set by hand is kept. Save the scene afterwards.
    [InitializeOnLoad]
    public static class ParkAutoWire
    {
        private const string Art = "Assets/ART/Park/";
        private const string Crate = Art + "TapeCrate.png";
        private const string Tape = Art + "BushTape.png";
        private const string Mud = Art + "MudPuddle.png";
        private const string RingBack = "Assets/ART/Props/SwitchRingBack.png";
        private const string RingFill = "Assets/ART/Props/SwitchRingFill.png";
        private const string RadialFill = "Assets/ART/Player/Floor/Sprite_RadialFill.mat";

        static ParkAutoWire()
        {
            EditorApplication.delayCall += Wire;
        }

        private static void Wire()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            foreach (string path in new[] { Crate, Tape, Mud, RingBack, RingFill }) ImportAsSprite(path);

            var rounds = Object.FindFirstObjectByType<BountyRounds>(FindObjectsInactive.Include);
            if (rounds == null) return;

            var players = Object.FindFirstObjectByType<UWBPlayerSpawner>(FindObjectsInactive.Include);
            var thieves = Object.FindFirstObjectByType<ThiefSpawner>(FindObjectsInactive.Include);
            bool changed = false;

            ParkPoliceTape policeTape = GetOrAdd<ParkPoliceTape>(rounds.gameObject, ref changed);
            var tapeFields = new SerializedObject(policeTape);
            changed |= Fill(tapeFields, "rounds", rounds);
            changed |= Fill(tapeFields, "players", players);
            changed |= Fill(tapeFields, "hidingSpots", thieves);
            changed |= Fill(tapeFields, "crate", AssetDatabase.LoadAssetAtPath<Sprite>(Crate));
            changed |= Fill(tapeFields, "tape", AssetDatabase.LoadAssetAtPath<Sprite>(Tape));
            changed |= Fill(tapeFields, "switchRingBack", AssetDatabase.LoadAssetAtPath<Sprite>(RingBack));
            changed |= Fill(tapeFields, "switchRingFill", AssetDatabase.LoadAssetAtPath<Sprite>(RingFill));
            changed |= Fill(tapeFields, "radialFill", AssetDatabase.LoadAssetAtPath<Material>(RadialFill));
            tapeFields.ApplyModifiedPropertiesWithoutUndo();

            MudPuddles mud = GetOrAdd<MudPuddles>(rounds.gameObject, ref changed);
            var mudFields = new SerializedObject(mud);
            changed |= Fill(mudFields, "rounds", rounds);
            changed |= Fill(mudFields, "thieves", thieves);
            changed |= Fill(mudFields, "policeTape", policeTape);
            changed |= Fill(mudFields, "mud", AssetDatabase.LoadAssetAtPath<Sprite>(Mud));
            mudFields.ApplyModifiedPropertiesWithoutUndo();

            if (rounds.TryGetComponent(out StunGuns guns))
            {
                var gunFields = new SerializedObject(guns);
                changed |= Fill(gunFields, "mud", mud);
                gunFields.ApplyModifiedPropertiesWithoutUndo();
            }

            if (!changed) return;

            EditorSceneManager.MarkSceneDirty(rounds.gameObject.scene);
            Debug.Log("[ParkAutoWire] The park's police tape and mud are set up; save the scene to keep them.", policeTape);
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
