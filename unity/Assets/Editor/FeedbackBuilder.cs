using UnityEditor;
using UnityEngine;

// Construit le retour d'information : FeedbackConfig, prefab du chiffre flottant, prefab d'éclat
// de particules, et l'objet "Feedback" de la scène. Formes simples, aucun package.
public static class FeedbackBuilder
{
    const string Root = "Assets/Prototype";

    public static FeedbackConfig LoadOrCreateConfig()
    {
        string path = Root + "/FeedbackConfig.asset";
        FeedbackConfig config = AssetDatabase.LoadAssetAtPath<FeedbackConfig>(path);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<FeedbackConfig>();
            AssetDatabase.CreateAsset(config, path); // conservé s'il existe : les réglages ne sont pas écrasés
        }
        return config;
    }

    // Crée (ou refait) les prefabs et l'objet de scène ; renvoie le FeedbackSpawner.
    public static FeedbackSpawner Build()
    {
        FeedbackConfig config = LoadOrCreateConfig();
        GameObject textPrefab = BuildFloatingTextPrefab();
        GameObject burstPrefab = BuildBurstPrefab();

        GameObject old = GameObject.Find("Feedback");
        if (old != null) Object.DestroyImmediate(old);
        GameObject go = new GameObject("Feedback", typeof(FeedbackSpawner));
        var so = new SerializedObject(go.GetComponent<FeedbackSpawner>());
        so.FindProperty("config").objectReferenceValue = config;
        so.FindProperty("floatingTextPrefab").objectReferenceValue = textPrefab;
        so.FindProperty("burstPrefab").objectReferenceValue = burstPrefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        return go.GetComponent<FeedbackSpawner>();
    }

    // Texte 3D (police intégrée) avec un contour sombre décalé derrière.
    static GameObject BuildFloatingTextPrefab()
    {
        Font font = UiTheme.Font;

        GameObject root = new GameObject("FloatingText", typeof(TextMesh), typeof(FaceCamera), typeof(FloatingText));
        TextMesh main = root.GetComponent<TextMesh>();
        SetupText(main, font, Color.white);

        GameObject shadowGo = new GameObject("Shadow", typeof(TextMesh));
        shadowGo.transform.SetParent(root.transform, false);
        shadowGo.transform.localPosition = new Vector3(0.025f, -0.025f, 0.02f);
        TextMesh shadow = shadowGo.GetComponent<TextMesh>();
        SetupText(shadow, font, new Color(0.05f, 0.07f, 0.1f));

        GameObject iconGo = new GameObject("BillIcon", typeof(SpriteRenderer));
        iconGo.transform.SetParent(root.transform, false);
        iconGo.transform.localScale = Vector3.one * 0.55f;
        SpriteRenderer sr = iconGo.GetComponent<SpriteRenderer>();
        sr.sprite = UiSprites.Bill();
        sr.sortingOrder = 1;

        var so = new SerializedObject(root.GetComponent<FloatingText>());
        so.FindProperty("icon").objectReferenceValue = iconGo.transform;
        so.FindProperty("main").objectReferenceValue = main;
        so.FindProperty("shadow").objectReferenceValue = shadow;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/FX_FloatingText.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void SetupText(TextMesh tm, Font font, Color color)
    {
        tm.font = font;
        tm.text = "+0";
        tm.fontSize = 100;
        tm.characterSize = 0.09f; // lisible depuis la caméra de jeu
        tm.fontStyle = FontStyle.Bold;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.GetComponent<MeshRenderer>().sharedMaterial = font.material;
    }

    // Système de particules intégré : petits éclats qui retombent. Émis à la demande (Emit), non bouclé.
    static GameObject BuildBurstPrefab()
    {
        GameObject root = new GameObject("FX_Burst", typeof(ParticleSystem));
        var ps = root.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = 0.5f;
        main.startSpeed = 2.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;

        var emission = ps.emission;
        emission.enabled = false; // seulement par Emit()

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        // léger fondu : opaque au début, transparent à la fin
        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = new ParticleSystem.MinMaxGradient(fade);

        var renderer = root.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = SparkMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/FX_Burst.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    // Points ronds doux : la texture radiale générée pour les ombres, en matériau transparent sans lumière.
    static Material SparkMaterial()
    {
        string path = Root + "/Materials/FX_Spark.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        var radial = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Radial.png");
        if (radial != null) mat.SetTexture("_BaseMap", radial);
        mat.SetColor("_BaseColor", Color.white); // la couleur vient de la particule
        mat.SetFloat("_Surface", 1f);            // transparent
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
