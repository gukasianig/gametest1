using UnityEditor;
using UnityEngine;

// Runtime creates Sprites from Texture2D; keep the source as a regular alpha texture.
public class UpgradeArtworkImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/UI/Upgrades/", System.StringComparison.Ordinal) ||
            !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 256;
    }
}
