using UnityEngine;

// Numéro de build : écrit par WebGLBuilder dans Resources/BuildVersion.txt (date, heure et hash court git du dépôt).
// Sert à savoir quel build tourne sur un téléphone (cache de Safari, ancien lien Vercel).
public static class BuildInfo
{
    static string version;

    public static string Version
    {
        get
        {
            if (version != null) return version;
            TextAsset text = Resources.Load<TextAsset>("BuildVersion");
            version = text != null ? text.text.Trim() : "éditeur (pas de numéro de build)";
            return version;
        }
    }
}
