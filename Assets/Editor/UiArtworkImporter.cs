using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class UiArtworkImporter : AssetPostprocessor
{
    private const string Path = "Assets/Resources/UI/ArenaIcons.png";
    static UiArtworkImporter() { EditorApplication.delayCall += Configure; }

    private static void Configure()
    {
        TextureImporter importer = AssetImporter.GetAtPath(Path) as TextureImporter;
        if (importer == null) return;
        if (!importer.mipmapEnabled && importer.alphaIsTransparency && importer.npotScale == TextureImporterNPOTScale.None &&
            importer.textureCompression == TextureImporterCompression.Uncompressed && importer.wrapMode == TextureWrapMode.Clamp) return;
        Apply(importer);
        importer.SaveAndReimport();
    }

    private void OnPreprocessTexture()
    {
        if (assetPath == Path || assetPath.StartsWith("Assets/Resources/UI/Tree/")) Apply((TextureImporter)assetImporter);
    }

    private static void Apply(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = false;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
    }
}
