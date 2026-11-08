using UnityEditor;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// Fixed import settings for everything the Blender pipeline (ArtSource/) produces: no
    /// colliders (gameplay colliders stay on the original objects, see M16ArtPass), no embedded
    /// materials or animation, and flat normals exactly as authored.
    /// </summary>
    public sealed class ArtModelPostprocessor : AssetPostprocessor
    {
        private const string ModelsFolder = "Assets/Art/Models/";
        private const string TexturesFolder = "Assets/Art/Textures/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelsFolder)) return;
            ModelImporter importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.addCollider = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TexturesFolder)) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            bool atlas = assetPath.EndsWith("SS_Atlas.png");
            // The atlas holds flat colour cells: mipmaps would bleed neighbouring cells together
            // at distance, and Repeat would wrap across its edge. Tiling surfaces want the opposite.
            importer.mipmapEnabled = !atlas;
            importer.wrapMode = atlas ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (!atlas) importer.anisoLevel = 4;
        }
    }
}
