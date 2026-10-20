using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
namespace TilesHD {
// Editor-only static assets: runtime has no selector, script or mutation.
public static class VariantTiles {
 public const string Folder="Assets/Art/Tiles/Test/TilesHDVariants";
 public static int Index(Vector3Int cell){return ((cell.x%4)+4)%4+4*(((cell.y%4)+4)%4);}
 public static Tile[,] Ensure(Tile[] sources){
  var result=new Tile[16,40];
  var original=(TextureImporter)AssetImporter.GetAtPath("Assets/Pic/Test/basic_ground_tiles.png");
  Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
  for(int v=0;v<16;v++){
   string path="Assets/Pic/Test/TilesHDVariants/variant_"+v.ToString("00")+".png";
   var importer=(TextureImporter)AssetImporter.GetAtPath(path);
   if(!importer)throw new Exception("Missing variant "+path);
   if(importer.textureType!=TextureImporterType.Sprite || importer.spriteImportMode!=SpriteImportMode.Multiple || importer.spritesheet.Length!=56){
    importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
    importer.spritePixelsPerUnit=256;importer.filterMode=FilterMode.Bilinear;importer.mipmapEnabled=false;
    importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
    importer.maxTextureSize=2048;importer.spritesheet=original.spritesheet;importer.SaveAndReimport();
   }
   var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s=>s.name);
   for(int i=0;i<40;i++){
    string tilePath=Folder+"/v"+v.ToString("00")+"_tile_"+i.ToString("00")+".asset";
    var tile=AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
    if(!tile){tile=UnityEngine.Object.Instantiate(sources[i]);tile.name="v"+v.ToString("00")+"_tile_"+i.ToString("00");tile.sprite=sprites[sources[i].sprite.name];AssetDatabase.CreateAsset(tile,tilePath);}
    if(tile.sprite!=sprites[sources[i].sprite.name] || tile.colliderType!=sources[i].colliderType || tile.flags!=sources[i].flags)throw new Exception("Variant asset contract mismatch "+tilePath);
    result[v,i]=tile;
   }
  }
  AssetDatabase.SaveAssets();
  // Chunk-mode Tilemap sorting requires one shared texture; separate variant textures
  // otherwise batch out of cell depth order and expose side faces over neighboring tops.
  string atlasPath=Folder+"/TilesHDVariants.spriteatlas";
  var atlas=AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(atlasPath);
  if(!atlas){
   atlas=new UnityEngine.U2D.SpriteAtlas();
   UnityEditor.U2D.SpriteAtlasExtensions.SetPackingSettings(atlas,new UnityEditor.U2D.SpriteAtlasPackingSettings{enableRotation=false,enableTightPacking=false,padding=2});
   UnityEditor.U2D.SpriteAtlasExtensions.SetTextureSettings(atlas,new UnityEditor.U2D.SpriteAtlasTextureSettings{generateMipMaps=false,readable=false,sRGB=true,filterMode=FilterMode.Bilinear});
   UnityEditor.U2D.SpriteAtlasExtensions.SetPlatformSettings(atlas,new TextureImporterPlatformSettings{name="DefaultTexturePlatform",maxTextureSize=8192,textureCompression=TextureImporterCompression.Uncompressed});
   UnityEditor.U2D.SpriteAtlasExtensions.SetIncludeInBuild(atlas,true);
   var textures=Enumerable.Range(0,16).Select(v=>AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Pic/Test/TilesHDVariants/variant_"+v.ToString("00")+".png")).Cast<UnityEngine.Object>().ToArray();
   UnityEditor.U2D.SpriteAtlasExtensions.Add(atlas,textures);
   AssetDatabase.CreateAsset(atlas,atlasPath);AssetDatabase.SaveAssets();
  }
  UnityEditor.U2D.SpriteAtlasUtility.PackAtlases(new[]{atlas},EditorUserBuildSettings.activeBuildTarget);
  Debug.Log("[TilesHD] VARIANTS 640 static Tiles; shared atlas packing complete");
  return result;
 }
}
}
