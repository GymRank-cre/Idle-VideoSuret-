using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Étape 8a : gestionnaire audio, bouton engrenage et panneau de réglages, outils pour écouter et mesurer les sons.
public static class AudioBuilder
{
    const string Root = "Assets/Prototype";

    public static MusicConfig LoadOrCreateConfig()
    {
        string path = Root + "/MusicConfig.asset";
        MusicConfig config = AssetDatabase.LoadAssetAtPath<MusicConfig>(path);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<MusicConfig>();
            AssetDatabase.CreateAsset(config, path);
        }
        return config;
    }

    public static void Build()
    {
        GameObject canvas = GameObject.Find("Canvas");
        Transform safe = canvas != null ? canvas.transform.Find("HudSafeArea") : null;
        if (safe == null) return;

        foreach (string old in new[] { "AudioManager" })
        {
            GameObject g = GameObject.Find(old);
            if (g != null) UnityEngine.Object.DestroyImmediate(g);
        }
        foreach (string old in new[] { "SettingsButton", "SettingsPanel", "SettingsHost", "DiagnosticPanel", "DiagnosticHost" })
        {
            Transform t = safe.Find(old);
            if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
        }

        GameObject am = new GameObject("AudioManager", typeof(AudioManager));
        var aso = new SerializedObject(am.GetComponent<AudioManager>());
        aso.FindProperty("config").objectReferenceValue = LoadOrCreateConfig();
        aso.ApplyModifiedPropertiesWithoutUndo();

        BuildSettingsUi(safe);
    }

    // ------------------------------------------------------------------ interface

    static void BuildSettingsUi(Transform safe)
    {
        Sprite rounded = UiSprites.Rounded();
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Sprite square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // Bouton engrenage : discret, en haut au centre
        RectTransform gear = UpgradeBuilder.Box("SettingsButton", safe, circle, UiTheme.Slate, Vector2.zero, new Vector2(124f, 124f), true);
        gear.anchorMin = gear.anchorMax = gear.pivot = new Vector2(0.5f, 1f);
        gear.anchoredPosition = new Vector2(0f, -56f);
        RectTransform gearBase = UiTheme.AddBase(gear, new Color32(0x12, 0x1A, 0x24, 0xFF), 10f, 1f); // socle plein (rond)
        gearBase.GetComponent<Image>().sprite = circle;
        gearBase.GetComponent<Image>().type = Image.Type.Simple;
        for (int i = 0; i < 4; i++)
            UpgradeBuilder.Rot(UpgradeBuilder.Box("Tooth" + i, gear, square, new Color(1f, 1f, 1f, 0.85f), Vector2.zero, new Vector2(14f, 62f), false), i * 45f);
        UpgradeBuilder.Box("Disc", gear, circle, new Color(1f, 1f, 1f, 0.9f), Vector2.zero, new Vector2(46f, 46f), false);
        UpgradeBuilder.Box("Hole", gear, circle, new Color(0.12f, 0.14f, 0.18f, 1f), Vector2.zero, new Vector2(20f, 20f), false);

        // Panneau
        GameObject panelGo = new GameObject("SettingsPanel", typeof(RectTransform), typeof(PopIn));
        panelGo.transform.SetParent(safe, false);
        RectTransform panel = panelGo.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(0f, 40f);
        panel.sizeDelta = new Vector2(860f, 820f);
        Image back = panelGo.AddComponent<Image>();
        back.sprite = rounded;
        back.type = Image.Type.Sliced;
        back.pixelsPerUnitMultiplier = 0.9f;
        back.color = new Color(0.10f, 0.13f, 0.18f, 0.92f);

        UpgradeBuilder.Label("Title", panel, Loc.T("settings.title"), 70, new Color(1f, 0.85f, 0.2f), new Vector2(0f, 320f), new Vector2(600f, 110f));
        RectTransform close = UpgradeBuilder.Box("CloseButton", panel, circle, new Color(0.75f, 0.25f, 0.25f), new Vector2(345f, 325f), new Vector2(90f, 90f), true);
        UpgradeBuilder.Rot(UpgradeBuilder.Box("X1", close, square, Color.white, Vector2.zero, new Vector2(14f, 52f), false), 45f);
        UpgradeBuilder.Rot(UpgradeBuilder.Box("X2", close, square, Color.white, Vector2.zero, new Vector2(14f, 52f), false), -45f);

        Button sound = Toggle(panel, rounded, circle, Loc.T("settings.sound"), 215f);
        Button music = Toggle(panel, rounded, circle, Loc.T("settings.music"), 100f);
        Button vibration = Toggle(panel, rounded, circle, Loc.T("settings.vibration"), -15f);
        Button motion = Toggle(panel, rounded, circle, Loc.T("settings.motion"), -130f);

        RectTransform erase = UpgradeBuilder.Box("EraseButton", panel, rounded, new Color(0.75f, 0.25f, 0.25f), new Vector2(0f, -265f), new Vector2(680f, 110f), true);
        erase.GetComponent<Image>().pixelsPerUnitMultiplier = 0.8f;
        UpgradeBuilder.Label("Text", erase, Loc.T("settings.erase"), 46, Color.white, Vector2.zero, new Vector2(680f, 110f));

        // Confirmation
        RectTransform confirm = UpgradeBuilder.Box("ConfirmBox", panel, rounded, new Color(0.06f, 0.08f, 0.12f, 0.98f), new Vector2(0f, -60f), new Vector2(760f, 430f), false);
        confirm.GetComponent<Image>().pixelsPerUnitMultiplier = 0.9f;
        confirm.GetComponent<Image>().raycastTarget = true; // bloque les clics derrière
        UpgradeBuilder.Label("Question", confirm, Loc.T("settings.erase.ask"), 48, Color.white, new Vector2(0f, 110f), new Vector2(700f, 120f));
        UpgradeBuilder.Label("Warn", confirm, Loc.T("settings.erase.warn"), 36, new Color(0.8f, 0.85f, 0.9f), new Vector2(0f, 30f), new Vector2(700f, 60f));
        RectTransform yes = UpgradeBuilder.Box("Yes", confirm, rounded, new Color(0.75f, 0.25f, 0.25f), new Vector2(-180f, -110f), new Vector2(300f, 110f), true);
        yes.GetComponent<Image>().pixelsPerUnitMultiplier = 0.8f;
        UpgradeBuilder.Label("Text", yes, Loc.T("settings.erase.yes"), 48, Color.white, Vector2.zero, new Vector2(300f, 110f));
        RectTransform no = UpgradeBuilder.Box("No", confirm, rounded, new Color(0.3f, 0.33f, 0.38f), new Vector2(180f, -110f), new Vector2(300f, 110f), true);
        no.GetComponent<Image>().pixelsPerUnitMultiplier = 0.8f;
        UpgradeBuilder.Label("Text", no, Loc.T("settings.erase.no"), 48, Color.white, Vector2.zero, new Vector2(300f, 110f));

        // Numéro de build (lisible même si la scène 3D est vide) et accès au panneau de diagnostic
        Text version = UpgradeBuilder.Label("Version", panel, "Version : ?", 28, new Color(0.8f, 0.85f, 0.9f), new Vector2(-150f, -352f), new Vector2(520f, 60f));
        version.alignment = TextAnchor.MiddleLeft;
        version.horizontalOverflow = HorizontalWrapMode.Wrap;
        RectTransform diagButton = UpgradeBuilder.Box("DiagnosticButton", panel, rounded, new Color(0.3f, 0.33f, 0.38f), new Vector2(300f, -352f), new Vector2(250f, 70f), true);
        diagButton.GetComponent<Image>().pixelsPerUnitMultiplier = 0.8f;
        UpgradeBuilder.Label("Text", diagButton, Loc.T("settings.diag"), 32, Color.white, Vector2.zero, new Vector2(250f, 70f));

        // Panneau de diagnostic (au-dessus de tout, dans la zone sûre)
        RectTransform diag = UpgradeBuilder.Box("DiagnosticPanel", safe, rounded, new Color(0.04f, 0.05f, 0.08f, 0.95f), new Vector2(0f, 0f), new Vector2(1000f, 1250f), false);
        diag.GetComponent<Image>().pixelsPerUnitMultiplier = 0.9f;
        diag.GetComponent<Image>().raycastTarget = true;
        Text diagBody = UpgradeBuilder.Label("Body", diag, "", 30, Color.white, new Vector2(0f, -40f), new Vector2(940f, 1100f));
        diagBody.alignment = TextAnchor.UpperLeft;
        diagBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        diagBody.verticalOverflow = VerticalWrapMode.Truncate;
        RectTransform diagClose = UpgradeBuilder.Box("CloseButton", diag, circle, new Color(0.75f, 0.25f, 0.25f), new Vector2(430f, 570f), new Vector2(90f, 90f), true);
        UpgradeBuilder.Rot(UpgradeBuilder.Box("X1", diagClose, square, Color.white, Vector2.zero, new Vector2(14f, 52f), false), 45f);
        UpgradeBuilder.Rot(UpgradeBuilder.Box("X2", diagClose, square, Color.white, Vector2.zero, new Vector2(14f, 52f), false), -45f);
        GameObject diagHost = new GameObject("DiagnosticHost", typeof(RectTransform), typeof(DiagnosticPanel));
        diagHost.transform.SetParent(safe, false);
        var dso = new SerializedObject(diagHost.GetComponent<DiagnosticPanel>());
        dso.FindProperty("panel").objectReferenceValue = diag.gameObject;
        dso.FindProperty("body").objectReferenceValue = diagBody;
        dso.FindProperty("openButton").objectReferenceValue = diagButton.GetComponent<Button>();
        dso.FindProperty("closeButton").objectReferenceValue = diagClose.GetComponent<Button>();
        dso.ApplyModifiedPropertiesWithoutUndo();

        // Le script vit sur un objet toujours actif (le panneau est désactivé quand il est fermé).
        GameObject host = new GameObject("SettingsHost", typeof(RectTransform), typeof(SettingsPanel));
        host.transform.SetParent(safe, false);
        var so = new SerializedObject(host.GetComponent<SettingsPanel>());
        so.FindProperty("panel").objectReferenceValue = panelGo;
        so.FindProperty("confirmBox").objectReferenceValue = confirm.gameObject;
        so.FindProperty("openButton").objectReferenceValue = gear.GetComponent<Button>();
        so.FindProperty("closeButton").objectReferenceValue = close.GetComponent<Button>();
        so.FindProperty("soundToggle").objectReferenceValue = sound;
        so.FindProperty("musicToggle").objectReferenceValue = music;
        so.FindProperty("vibrationToggle").objectReferenceValue = vibration;
        so.FindProperty("motionToggle").objectReferenceValue = motion;
        so.FindProperty("eraseButton").objectReferenceValue = erase.GetComponent<Button>();
        so.FindProperty("confirmYes").objectReferenceValue = yes.GetComponent<Button>();
        so.FindProperty("confirmNo").objectReferenceValue = no.GetComponent<Button>();
        so.FindProperty("versionText").objectReferenceValue = version;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Ligne « libellé + interrupteur » : piste arrondie colorée et bouton rond qui glisse (enfant 0).
    static Button Toggle(RectTransform panel, Sprite rounded, Sprite circle, string label, float y)
    {
        Text t = UpgradeBuilder.Label(label + "Label", panel, label, 52, Color.white, new Vector2(-150f, y), new Vector2(420f, 90f));
        t.alignment = TextAnchor.MiddleLeft;
        RectTransform track = UpgradeBuilder.Box(label + "Toggle", panel, rounded, new Color(0.2f, 0.7f, 0.3f), new Vector2(250f, y), new Vector2(170f, 84f), true);
        track.GetComponent<Image>().pixelsPerUnitMultiplier = 0.9f;
        UpgradeBuilder.Box("Knob", track, circle, Color.white, new Vector2(36f, 0f), new Vector2(68f, 68f), false);
        return track.GetComponent<Button>();
    }

    // ------------------------------------------------------------------ outils (menus)

    [MenuItem("Tools/Idle Prototype/Mesurer les sons")]
    static void Measure()
    {
        AudioClip[] clips = SfxLibrary.CreateAll();
        float minDb = 100f, maxDb = -100f;
        string report = "Sons générés (durée, crête, RMS sur la partie sonore) :\n";
        for (int i = 0; i < clips.Length; i++)
        {
            Stats(clips[i], out float dur, out float peak, out float rmsDb);
            minDb = Mathf.Min(minDb, rmsDb);
            maxDb = Mathf.Max(maxDb, rmsDb);
            report += string.Format("  {0,-14} {1:0.00} s  crête {2:0.00}  RMS {3:0.0} dBFS\n", (SfxId)i, dur, peak, rmsDb);
        }
        report += string.Format("Écart de niveau entre sons : {0:0.0} dB (cible : +/- 3 dB autour de la moyenne)\n", maxDb - minDb);
        AudioClip music = MusicLoop.Create();
        Stats(music, out float md, out float mp, out float mr);
        report += string.Format("  musique         {0:0.0} s  crête {1:0.00}  RMS {2:0.0} dBFS", md, mp, mr);
        Debug.Log(report);
    }

    static void Stats(AudioClip clip, out float duration, out float peak, out float rmsDb)
    {
        var data = new float[clip.samples];
        clip.GetData(data, 0);
        peak = 0f;
        double sum = 0;
        int active = 0;
        foreach (float v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
        foreach (float v in data)
        {
            if (Mathf.Abs(v) > peak * 0.02f) { sum += v * v; active++; }
        }
        duration = clip.length;
        rmsDb = 20f * Mathf.Log10(Mathf.Sqrt((float)(sum / Mathf.Max(1, active))) + 1e-9f);
    }

    static int listenIndex;
    static double nextAt;
    static AudioClip[] listenClips;
    static AudioClip listenMusic;

    [MenuItem("Tools/Idle Prototype/Écouter les sons")]
    static void Listen()
    {
        listenClips = SfxLibrary.CreateAll();
        listenMusic = MusicLoop.Create();
        listenIndex = 0;
        nextAt = EditorApplication.timeSinceStartup + 0.2;
        EditorApplication.update -= ListenTick;
        EditorApplication.update += ListenTick;
        Debug.Log("Écoute des sons : un son toutes les 1,2 s, puis 12 s de musique. Mets le son de ton PC.");
    }

    static void ListenTick()
    {
        if (EditorApplication.timeSinceStartup < nextAt) return;

        if (listenIndex < listenClips.Length)
        {
            Debug.Log("Son " + (listenIndex + 1) + "/" + listenClips.Length + " : " + (SfxId)listenIndex);
            PlayPreview(listenClips[listenIndex]);
            listenIndex++;
            nextAt = EditorApplication.timeSinceStartup + 1.2;
        }
        else if (listenIndex == listenClips.Length)
        {
            Debug.Log("Musique d'ambiance (12 s)");
            PlayPreview(listenMusic);
            listenIndex++;
            nextAt = EditorApplication.timeSinceStartup + 12.0;
        }
        else
        {
            StopPreview();
            Debug.Log("Écoute terminée.");
            EditorApplication.update -= ListenTick;
        }
    }

    // Lecture dans l'éditeur (hors Play) par l'API interne de l'éditeur.
    static void PlayPreview(AudioClip clip)
    {
        Type util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        StopPreview();
        MethodInfo m = util.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public,
            null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
        m.Invoke(null, new object[] { clip, 0, false });
    }

    static void StopPreview()
    {
        Type util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        MethodInfo m = util.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public);
        if (m != null) m.Invoke(null, null);
    }
}
