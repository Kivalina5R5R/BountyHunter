using Fortal.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BountyHunter.Thieves.EditorTools
{
    // Sets up the park's police tape and mud (stage 2) each time the editor loads: imports the art
    // Codex drew (Assets/ART/Park) as sprites, adds ParkPoliceTape and MudPuddles next to the open
    // scene's BountyRounds when they are missing, fills every reference left empty (the crate's ring
    // is the players' select ring, drawn with the radial fill material), and points the stun guns and their drops at
    // the mud so locking on from it is slower and none is dropped in it. Anything set by hand is
    // kept. Save the scene afterwards.
    [InitializeOnLoad]
    public static class ParkAutoWire
    {
        private const string Art = "Assets/ART/Park/";
        private const string TapeItem = Art + "TapeItem.png";
        private static readonly string[] Sparkles = { Art + "Sparkle1.png", Art + "Sparkle2.png" };
        // Cut from the designer's example: its torn ends (this many pixels each) stay, its middle repeats.
        private const string TapeStrip = Art + "BushTapeStrip.png";
        private const float TapeStripEnd = 28f;
        private const string Mud = Art + "MudPuddle.png";
        private const string SelectRing = "Assets/ART/Player/Floor/Load.png";
        private const string RadialFill = "Assets/ART/Player/Floor/Sprite_RadialFill.mat";

        static ParkAutoWire()
        {
            EditorApplication.delayCall += Wire;
        }

        private static void Wire()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            foreach (string path in new[] { TapeItem, Mud, Sparkles[0], Sparkles[1] }) ImportAsSprite(path);
            ImportAsStrip(TapeStrip, TapeStripEnd);

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
            changed |= Fill(tapeFields, "tapeItem", AssetDatabase.LoadAssetAtPath<Sprite>(TapeItem));
            changed |= FillList(tapeFields, "sparkles", Sparkles);
            changed |= Fill(tapeFields, "tapeStrip", AssetDatabase.LoadAssetAtPath<Sprite>(TapeStrip));
            changed |= Fill(tapeFields, "selectRing", AssetDatabase.LoadAssetAtPath<Sprite>(SelectRing));
            changed |= Fill(tapeFields, "radialFill", AssetDatabase.LoadAssetAtPath<Material>(RadialFill));
            tapeFields.ApplyModifiedPropertiesWithoutUndo();

            MudPuddles mud = GetOrAdd<MudPuddles>(rounds.gameObject, ref changed);
            var mudFields = new SerializedObject(mud);
            changed |= Fill(mudFields, "rounds", rounds);
            changed |= Fill(mudFields, "thieves", thieves);
            changed |= Fill(mudFields, "policeTape", policeTape);
            changed |= Fill(mudFields, "mud", AssetDatabase.LoadAssetAtPath<Sprite>(Mud));
            mudFields.ApplyModifiedPropertiesWithoutUndo();
            changed |= Fill(tapeFields, "mud", mud);
            tapeFields.ApplyModifiedPropertiesWithoutUndo();

            if (rounds.TryGetComponent(out StunGuns guns))
            {
                var gunFields = new SerializedObject(guns);
                changed |= Fill(gunFields, "mud", mud);
                gunFields.ApplyModifiedPropertiesWithoutUndo();
            }

            if (rounds.TryGetComponent(out StunGunPickups pickups))
            {
                var pickupFields = new SerializedObject(pickups);
                changed |= Fill(pickupFields, "mud", mud);
                pickupFields.ApplyModifiedPropertiesWithoutUndo();
                changed |= Fill(tapeFields, "guns", pickups);
                tapeFields.ApplyModifiedPropertiesWithoutUndo();
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
        // A list of sprites filled in when it is empty.
        private static bool FillList(SerializedObject fields, string name, string[] paths)
        {
            SerializedProperty list = fields.FindProperty(name);
            if (list == null || list.arraySize > 0) return false;

            int count = 0;
            foreach (string path in paths)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;
                list.arraySize = count + 1;
                list.GetArrayElementAtIndex(count++).objectReferenceValue = sprite;
            }

            return count > 0;
        }

        // A strip drawn tiled (SpriteRenderer Tiled): a full-rect sprite whose ends are its borders.
        private static void ImportAsStrip(string path, float end)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            var border = new Vector4(end, 0f, end, 0f);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteBorder == border && settings.spriteMeshType == SpriteMeshType.FullRect) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.spriteBorder = border;
            importer.SaveAndReimport();
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
