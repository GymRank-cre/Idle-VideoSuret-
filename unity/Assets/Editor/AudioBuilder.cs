using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Configuration audio et outils pour écouter et mesurer les sons.
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
