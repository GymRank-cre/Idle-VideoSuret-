using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Matériaux URP de la scène idle (U2) : créés une fois, enregistrés dans Assets/Prototype/Materials, couleurs mises à jour à chaque
// construction. Aucune ombre temps réel : tout est lisible par les couleurs saturées et les arêtes sombres.
public static class IdleMaterials
{
    const string Folder = "Assets/Prototype/Materials";

    public static Material Get(string name, Color color, float smoothness = 0f, bool transparent = false)
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prototype", "Materials");
        string path = Folder + "/Idle_" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        m.SetColor("_BaseColor", color);
        m.color = color;
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_SpecularHighlights", 0f);
        m.SetFloat("_EnvironmentReflections", 0f);
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        if (transparent)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // Aplat dessiné par-dessus tout (plaques d'achat) ; `queue` fixe l'ordre entre les pièces d'une même plaque.
    public static Material Overlay(string name, string hex, int queue)
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prototype", "Materials");
        string path = Folder + "/Idle_Overlay_" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Idle/Overlay")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Idle/Overlay");
        Color c;
        ColorUtility.TryParseHtmlString(hex, out c);
        m.SetColor("_BaseColor", c);
        m.renderQueue = queue;
        EditorUtility.SetDirty(m);
        return m;
    }

    public static Material Get(string name, string hex, float smoothness = 0f, bool transparent = false, float alpha = 1f)
    {
        Color c;
        ColorUtility.TryParseHtmlString(hex, out c);
        c.a = alpha;
        return Get(name, c, smoothness, transparent);
    }
}
