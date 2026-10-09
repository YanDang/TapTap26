using UnityEditor;

// Resources/*/Face/ 下的表情贴图（make_face_sprites.py 生成）：导入成 Sprite、100 PPU、不压缩、无 mipmap
public class FaceSpriteImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        var p = assetPath.Replace('\\', '/');
        if (!p.Contains("/Face/") && !p.Contains("Resources/Furniture/")) return;   // 表情贴图、战斗家具贴图
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.npotScale = TextureImporterNPOTScale.None;
    }
}
