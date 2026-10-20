using UnityEditor;

namespace BountyHunter.Editor
{
    // Imports the result screen's animation frames (made by Tools/ResultSprites/make_result_sprites.py
    // into Assets/ART/BG/Result/Sprites) as sprites without mipmaps, compressed, so the hundreds of
    // frames stay light. Only first imports are set up; a change made by hand in the Inspector stays.
    public sealed class ResultFrameImport : AssetPostprocessor
    {
        public const string Folder = "Assets/ART/BG/Result/Sprites/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder) || !assetImporter.importSettingsMissing) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
        }
    }
}
