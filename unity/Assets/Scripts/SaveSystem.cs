using System;
using System.IO;
using UnityEngine;

// Fichier de sauvegarde JSON (v8) : écriture atomique (.tmp puis remplacement) avec copie de secours .bak, lecture tolérante
// (fichier absent ou abîmé -> copie de secours -> nouvelle partie), migration depuis les anciennes versions.
// Sur WebGL le système de fichiers est en mémoire : on passe par PlayerPrefs (avec une 2e clé de secours).
public static class SaveSystem
{
    const string FileName = "save.json";
    const string PrefsKey = "save_json";
    const string PrefsBackupKey = "save_json_bak";

#if UNITY_WEBGL && !UNITY_EDITOR
    const bool WebGlRuntime = true;
#else
    const bool WebGlRuntime = false;
#endif
    // Vrai pour tester le repli WebGL dans l'éditeur.
    public static bool ForcePlayerPrefs;
    static bool UsePlayerPrefs => WebGlRuntime || ForcePlayerPrefs;

    // Vrai pendant un test (triche, absence simulée, effacement) : plus aucune écriture.
    public static bool Suspended;

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
    static string BackupPath => FilePath + ".bak";
    static string TempPath => FilePath + ".tmp";

    // ------------------------------------------------------------------ écriture

    public static void Write(SaveData data)
    {
        if (Suspended || data == null) return;
        Write(JsonUtility.ToJson(data, true));
    }

    public static void Write(string json)
    {
        if (UsePlayerPrefs)
        {
            string previous = PlayerPrefs.GetString(PrefsKey, "");
            if (previous.Length > 0) PlayerPrefs.SetString(PrefsBackupKey, previous);
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(TempPath, json);
            if (File.Exists(FilePath)) File.Replace(TempPath, FilePath, BackupPath);   // l'ancien fichier devient la copie de secours
            else File.Move(TempPath, FilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Sauvegarde impossible : " + e.Message);
        }
    }

    // ------------------------------------------------------------------ lecture

    // Données migrées en v8, ou null (nouvelle partie). Ne lève jamais d'exception.
    public static SaveData Read(IdleEconomyConfig config)
    {
        if (UsePlayerPrefs)
        {
            foreach (string key in new[] { PrefsKey, PrefsBackupKey })
            {
                SaveData data = TryParse(PlayerPrefs.GetString(key, ""), config);
                if (data != null) return data;
            }
            return null;
        }
        foreach (string path in new[] { FilePath, BackupPath })
        {
            SaveData data = TryRead(path, config);
            if (data != null) return data;
        }
        return null;
    }

    static SaveData TryRead(string path, IdleEconomyConfig config)
    {
        try { return File.Exists(path) ? TryParse(File.ReadAllText(path), config) : null; }
        catch (Exception) { return null; }
    }

    public static SaveData TryParse(string json, IdleEconomyConfig config)
    {
        try
        {
            if (string.IsNullOrEmpty(json)) return null;
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.version <= 0 || data.version > SaveData.CurrentVersion) return null;
            return SaveMigration.ToCurrent(data, config);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // ------------------------------------------------------------------ effacement

    // Bouton « Effacer » des réglages et outils de test : fichier, copie de secours et clés de repli.
    public static void EraseEverything()
    {
        DeleteSave();
        PlayerPrefs.DeleteKey("guide_done");
        PlayerPrefs.Save();
    }

    public static void DeleteSave()
    {
        Suspended = true;   // en Play, l'arrêt ne doit pas réécrire le fichier
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.DeleteKey(PrefsBackupKey);
        foreach (string path in new[] { FilePath, BackupPath, TempPath })
            if (File.Exists(path)) File.Delete(path);
    }
}
