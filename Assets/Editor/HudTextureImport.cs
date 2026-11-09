using UnityEditor;

// Картинки панели героя из Blender (Assets/Resources/UI/Hud/): как UI-спрайты — без mip-карт и сжатия,
// с прозрачностью по краям, чтобы рамка и значки не мылились и не давали тёмных ореолов.
public class HudTextureImport : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        // Также фон главного меню (Assets/Resources/UI/Menu/) — без сжатия, чтобы небо не шло полосами.
        if (!path.StartsWith("Assets/Resources/UI/Hud/") && !path.StartsWith("Assets/Resources/UI/Menu/") && !path.StartsWith("Assets/Resources/UI/Weapons/")) return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 1024;
    }
}
