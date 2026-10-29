using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UI;

// Étape 8b : version WebGL de test pour iPhone (Safari). Écran « Touche pour jouer », gabarit de page plein écran,
// réglages de build et menu `Tools > Idle Prototype > Build WebGL de test`. Ne publie rien.
public static class WebGLBuilder
{
    const string ScenePath = "Assets/Prototype/Prototype.unity";
    const string OutputDir = "E:/PROJET 1/Builds/WebGL";
    const string TemplateDir = "Assets/WebGLTemplates/IdleMobile";

    // ------------------------------------------------------------------ scène

    public static void Build()
    {
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null) return;
        Transform old = canvas.transform.Find("TapToPlay");
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

        if (Camera.main != null && Camera.main.GetComponent<WebGlPostFxGate>() == null)
            Camera.main.gameObject.AddComponent<WebGlPostFxGate>(); // post-traitement coupé sur WebGL seulement

        Sprite rounded = UiSprites.Rounded();

        GameObject host = new GameObject("TapToPlay", typeof(RectTransform), typeof(TapToPlayScreen));
        host.transform.SetParent(canvas.transform, false); // dernier enfant : au-dessus de tout
        Stretch(host.GetComponent<RectTransform>());

        // fond sombre plein écran = grand bouton (le premier toucher n'importe où)
        GameObject root = new GameObject("Root", typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(host.transform, false);
        Stretch(root.GetComponent<RectTransform>());
        Image dim = root.GetComponent<Image>();
        dim.color = new Color(0.04f, 0.05f, 0.08f, 0.88f);
        root.GetComponent<Button>().targetGraphic = dim;
        root.GetComponent<Button>().transition = Selectable.Transition.None;

        RectTransform panel = UpgradeBuilder.Box("Panel", root.transform, rounded, new Color(0.10f, 0.13f, 0.18f, 0.92f), new Vector2(0f, 40f), new Vector2(860f, 560f), false);
        panel.GetComponent<Image>().pixelsPerUnitMultiplier = 0.9f;
        UpgradeBuilder.Label("Title", panel, "Idle Sécurité", 84, new Color(1f, 0.85f, 0.2f), new Vector2(0f, 150f), new Vector2(780f, 130f));
        UpgradeBuilder.Label("Sub", panel, "Installation de sécurité électronique", 38, new Color(0.8f, 0.85f, 0.9f), new Vector2(0f, 60f), new Vector2(780f, 70f));
        RectTransform tap = UpgradeBuilder.Box("TapPill", panel, rounded, new Color(0.2f, 0.7f, 0.3f), new Vector2(0f, -110f), new Vector2(620f, 150f), false);
        tap.GetComponent<Image>().pixelsPerUnitMultiplier = 0.8f;
        UpgradeBuilder.Label("Text", tap, "Touche pour jouer", 62, Color.white, Vector2.zero, new Vector2(620f, 150f));

        var so = new SerializedObject(host.GetComponent<TapToPlayScreen>());
        so.FindProperty("root").objectReferenceValue = root;
        so.FindProperty("tapButton").objectReferenceValue = root.GetComponent<Button>();
        so.FindProperty("pulse").objectReferenceValue = tap;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // La scène du prototype doit être dans les « Build Settings » (aussi pour Android plus tard).
    public static void EnsureSceneInBuildSettings()
    {
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (s.path == ScenePath) return;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    // ------------------------------------------------------------------ build

    [MenuItem("Tools/Idle Prototype/Build WebGL de test")]
    static void BuildWebGl()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            Debug.LogError("Le module « WebGL Build Support » n'est pas installé pour Unity " + Application.unityVersion + ".\n" +
                "Unity Hub > Installations > (ta version) > ⚙ > Ajouter des modules > coche « WebGL Build Support » > Installer.\n" +
                "Puis relance ce menu.");
            return;
        }

        EnsureTemplate();
        EnsureSceneInBuildSettings();
        ApplySettings();

        BuildTarget previousTarget = EditorUserBuildSettings.activeBuildTarget;
        Directory.CreateDirectory(OutputDir);
        double start = EditorApplication.timeSinceStartup;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        ForceWebGlPipeline();
        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(options);
        }
        finally
        {
            RestoreRenderPipelines();
        }
        double seconds = EditorApplication.timeSinceStartup - start;

        long bytes = FolderSize(OutputDir);
        Debug.Log("Build WebGL : " + report.summary.result + " en " + seconds.ToString("0") + " s, dossier " + OutputDir +
            ", taille " + (bytes / 1048576f).ToString("0.0") + " Mo (compressé Gzip).");

        // retour à la cible précédente : le jeu de l'éditeur reste identique
        if (previousTarget != BuildTarget.WebGL)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(previousTarget), previousTarget);
    }

    static void ApplySettings()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;   // Brotli exige du HTTPS particulier
        PlayerSettings.WebGL.decompressionFallback = true;                      // hébergement statique simple
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.initialMemorySize = 256;                           // Mo : raisonnable pour Safari iOS
        PlayerSettings.WebGL.maximumMemorySize = 256;
        PlayerSettings.WebGL.template = "PROJECT:IdleMobile";
        PlayerSettings.runInBackground = false;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low); // pas de strip risqué
        PlayerSettings.defaultWebScreenWidth = 390;
        PlayerSettings.defaultWebScreenHeight = 844;
    }

    // Profil URP « WebGL » : copie du profil Mobile (rendu Forward, pas Forward+) sans ombres temps réel ni textures de
    // profondeur/opaque. Le profil PC (Forward+) donnait une scène noire sur WebGL (« Mismatch between texture format and
    // sampler type »). Les profils Mobile et PC ne sont pas modifiés.
    static UnityEngine.Rendering.RenderPipelineAsset EnsureWebGlPipeline()
    {
        const string source = "Assets/Settings/Mobile_RPAsset.asset";
        const string copy = "Assets/Settings/WebGL_RPAsset.asset";
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(copy) == null)
            AssetDatabase.CopyAsset(source, copy);
        var rp = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(copy);
        var rso = new SerializedObject(rp);
        SetBool(rso, "m_MainLightShadowsSupported", false);
        SetBool(rso, "m_AdditionalLightShadowsSupported", false);
        SetBool(rso, "m_SoftShadowsSupported", false);
        SetBool(rso, "m_SupportsHDR", false);
        SetBool(rso, "m_RequireDepthTexture", false);
        SetBool(rso, "m_RequireOpaqueTexture", false);
        SetBool(rso, "m_SupportsLightCookies", false);
        SetBool(rso, "m_SupportsLightLayers", false);
        SerializedProperty add = rso.FindProperty("m_AdditionalLightsRenderingMode");
        if (add != null) add.intValue = 0; // désactivé
        SerializedProperty msaa = rso.FindProperty("m_MSAA");
        if (msaa != null) msaa.intValue = 1;
        rso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rp);
        AssetDatabase.SaveAssets();
        return rp;
    }

    const string RestoreKey = "IdleWebGL_RestoreRP"; // chemins d'origine, gardés même si l'éditeur recharge ses scripts

    // Le temps du build, le profil WebGL remplace le profil par défaut ET celui de chaque niveau de qualité. Les chemins
    // d'origine sont mémorisés dans EditorPrefs : le changement de plateforme recharge les scripts, un simple « finally »
    // ne suffirait pas. RestoreRenderPipelines() remet tout (appelé après le build et au rechargement de l'éditeur).
    static void ForceWebGlPipeline()
    {
        UnityEngine.Rendering.RenderPipelineAsset rp = EnsureWebGlPipeline();

        var qso = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        SerializedProperty levels = qso.FindProperty("m_QualitySettings");
        string saved = AssetDatabase.GetAssetPath(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline);
        for (int i = 0; i < levels.arraySize; i++)
        {
            SerializedProperty pipe = levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline");
            saved += "|" + AssetDatabase.GetAssetPath(pipe.objectReferenceValue);
            pipe.objectReferenceValue = rp;
        }
        EditorPrefs.SetString(RestoreKey, saved);
        qso.ApplyModifiedPropertiesWithoutUndo();
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = rp;
        AssetDatabase.SaveAssets();
    }

    [InitializeOnLoadMethod]
    static void RestoreRenderPipelines()
    {
        string saved = EditorPrefs.GetString(RestoreKey, "");
        if (string.IsNullOrEmpty(saved)) return;
        if (BuildPipeline.isBuildingPlayer) return; // pas pendant le build lui-même

        string[] parts = saved.Split('|');
        var qso = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        SerializedProperty levels = qso.FindProperty("m_QualitySettings");
        for (int i = 0; i < levels.arraySize && i + 1 < parts.Length; i++)
            levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue =
                string.IsNullOrEmpty(parts[i + 1]) ? null : AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(parts[i + 1]);
        qso.ApplyModifiedPropertiesWithoutUndo();
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline =
            string.IsNullOrEmpty(parts[0]) ? null : AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(parts[0]);
        AssetDatabase.SaveAssets();
        EditorPrefs.DeleteKey(RestoreKey);
    }

    static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.boolValue = value;
    }

    static long FolderSize(string dir)
    {
        long total = 0;
        foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            total += new FileInfo(f).Length;
        return total;
    }

    // Page plein écran portrait : le canvas remplit la fenêtre, pas de défilement, toucher capté par le jeu.
    static void EnsureTemplate()
    {
        string dir = Path.Combine(Directory.GetCurrentDirectory(), TemplateDir);
        Directory.CreateDirectory(dir);
        string html = @"<!DOCTYPE html>
<html lang=""fr"">
  <head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover"">
    <meta name=""apple-mobile-web-app-capable"" content=""yes"">
    <script>if (!location.pathname.endsWith(""/"") && !location.pathname.endsWith("".html"")) location.replace(location.pathname + ""/"" + location.search);</script>
    <title>{{{ PRODUCT_NAME }}}</title>
    <style>
      html, body { margin: 0; height: 100%; overflow: hidden; background: #0b0d12; overscroll-behavior: none; }
      #unity-canvas { position: fixed; inset: 0; width: 100vw; height: 100vh; height: 100dvh; display: block; touch-action: none; background: #0b0d12; }
      #loading { position: fixed; inset: 0; display: flex; align-items: center; justify-content: center; color: #ffd93a; font: bold 28px sans-serif; }
    </style>
  </head>
  <body>
    <canvas id=""unity-canvas""></canvas>
    <div id=""loading"">Chargement...</div>
    <script src=""Build/{{{ LOADER_FILENAME }}}""></script>
    <script>
      createUnityInstance(document.querySelector(""#unity-canvas""), {
        dataUrl: ""Build/{{{ DATA_FILENAME }}}"",
        frameworkUrl: ""Build/{{{ FRAMEWORK_FILENAME }}}"",
#if USE_THREADS
        workerUrl: ""Build/{{{ WORKER_FILENAME }}}"",
#endif
#if USE_WASM
        codeUrl: ""Build/{{{ CODE_FILENAME }}}"",
#endif
#if SYMBOLS_FILENAME
        symbolsUrl: ""Build/{{{ SYMBOLS_FILENAME }}}"",
#endif
        streamingAssetsUrl: ""StreamingAssets"",
        companyName: {{{ JSON.stringify(COMPANY_NAME) }}},
        productName: {{{ JSON.stringify(PRODUCT_NAME) }}},
        productVersion: {{{ JSON.stringify(PRODUCT_VERSION) }}},
        devicePixelRatio: Math.min(window.devicePixelRatio || 1, 2),
      }, function (progress) {
        document.querySelector(""#loading"").textContent = ""Chargement "" + Math.round(progress * 100) + "" %"";
      }).then(function () {
        document.querySelector(""#loading"").style.display = ""none"";
      }).catch(function (message) { document.querySelector(""#loading"").textContent = ""Erreur : "" + message; });
    </script>
  </body>
</html>
";
        File.WriteAllText(Path.Combine(dir, "index.html"), html);
        AssetDatabase.Refresh();
    }
}
