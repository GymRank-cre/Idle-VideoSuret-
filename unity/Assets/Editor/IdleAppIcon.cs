using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// Icône de l'application (UW-5) : une seule image 1024, utilisée par défaut sur toutes les plateformes.
public static class IdleAppIcon
{
    const string Path = "Assets/Prototype/Textures/AppIcon.png";

    public static void Apply()
    {
        var importer = AssetImporter.GetAtPath(Path) as TextureImporter;
        if (importer == null) { Debug.LogError("Icône manquante : " + Path); return; }
        if (importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Path);
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
    }
}
