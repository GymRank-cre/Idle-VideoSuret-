using System;
using System.IO;
using UnityEngine;

// Sauvegarde JSON : lit au démarrage, écrit toutes les 10 s si l'état a changé, à la mise en pause et à la sortie.
// Écriture atomique (.tmp puis remplacement) avec une copie de secours .bak ; lecture tolérante (fichier absent ou abîmé).
[DefaultExecutionOrder(-100)]
public class SaveSystem : MonoBehaviour
{
    const string FileName = "save.json";

    [SerializeField] Wallet wallet;
    [SerializeField] UnlockPad[] pads;            // toutes les dalles, même cachées
    [SerializeField] GameObject haulerObject;     // porteur de caisses (actif = embauché)
    [SerializeField] GameObject courierObject;    // livreur
    [SerializeField] UpgradeState upgradeState;
    [SerializeField] GuideConfig guideConfig;
    [SerializeField] OfflineConfig offlineConfig;
    [SerializeField] StationConfig stationConfig;
    [SerializeField] EmployeeConfig haulerConfig;
    [SerializeField] EmployeeConfig courierConfig;
    [SerializeField] CustomerConfig customerConfig;
    [SerializeField] OfflineWelcomeScreen welcomeScreen;
    [SerializeField, Min(1f)] float autosaveInterval = 10f;

    // Vrai pendant un test « absence simulée » ou « effacer » en Play : plus aucune écriture.
    public static bool Suspended;

    SaveData loaded;
    SaveData pending;      // gain hors ligne en attente de récupération
    string lastSavedJson = "";
    float timer;

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
    static string BackupPath => FilePath + ".bak";
    static string TempPath => FilePath + ".tmp";

    void Awake()
    {
        Suspended = false;
        loaded = Read();
        // Avant tous les Start : le guide lit ce drapeau.
        if (loaded != null && loaded.guideDone && guideConfig != null)
            PlayerPrefs.SetInt(guideConfig.DoneKey, 1);
    }

    void Start()
    {
        if (loaded == null) return; // nouvelle partie : le gameplay est inchangé

        wallet.Add(loaded.money);
        if (upgradeState != null)
            foreach (SaveData.UpgradeLevel u in loaded.upgrades)
                upgradeState.SetLevel(u.id, u.level);
        foreach (UnlockPad pad in pads)
        {
            SaveData.PadState state = loaded.pads.Find(p => p.id == pad.SaveId);
            if (state != null) pad.Restore(state.remaining);
        }

        pending = loaded;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (pending.pendingOffline <= 0)
        {
            long elapsed = now - loaded.lastSaveUtc;
            pending.pendingOffline = OfflineEarnings.Compute(elapsed, loaded.employees.Contains("hauler"),
                loaded.employees.Contains("courier"), offlineConfig, stationConfig, haulerConfig, courierConfig, customerConfig,
                upgradeState != null ? upgradeState.JobDuration : 1f,
                upgradeState != null ? upgradeState.EmployeeSpeed : 1f,
                upgradeState != null ? upgradeState.CustomerPay : 1f);
            pending.pendingSeconds = Math.Max(0, elapsed);
        }

        if (pending.pendingOffline > 0 && welcomeScreen != null)
        {
            Save(); // le gain en attente est mémorisé tant qu'il n'est pas récupéré
            welcomeScreen.Show(pending.pendingSeconds, pending.pendingOffline, Claim);
        }
        else
        {
            pending = null;
        }
        lastSavedJson = Snapshot();
    }

    void Claim()
    {
        if (pending == null) return;
        wallet.Add(pending.pendingOffline);
        pending = null;
        Save();
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < autosaveInterval) return;
        timer = 0f;
        if (Snapshot() != lastSavedJson) Save();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) Save();
    }

    void OnApplicationQuit()
    {
        Save();
    }

    SaveData Capture()
    {
        var data = new SaveData
        {
            money = wallet.Money,
            lastSaveUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            guideDone = guideConfig != null && PlayerPrefs.GetInt(guideConfig.DoneKey, 0) == 1,
            pendingOffline = pending != null ? pending.pendingOffline : 0,
            pendingSeconds = pending != null ? pending.pendingSeconds : 0
        };
        if (upgradeState != null)
            for (int i = 0; i < upgradeState.Count; i++)
                data.upgrades.Add(new SaveData.UpgradeLevel { id = upgradeState.Config.Upgrades[i].id, level = upgradeState.Level(i) });
        foreach (UnlockPad pad in pads)
            data.pads.Add(new SaveData.PadState { id = pad.SaveId, remaining = pad.RemainingPrice });
        if (haulerObject != null && haulerObject.activeSelf) data.employees.Add("hauler");
        if (courierObject != null && courierObject.activeSelf) data.employees.Add("courier");
        return data;
    }

    // État sans l'horodatage : sert à savoir si quelque chose a changé depuis la dernière écriture.
    string Snapshot()
    {
        SaveData data = Capture();
        data.lastSaveUtc = 0;
        return JsonUtility.ToJson(data);
    }

    void Save()
    {
        if (Suspended || wallet == null) return;
        SaveData data = Capture();
        Write(JsonUtility.ToJson(data, true));
        data.lastSaveUtc = 0;
        lastSavedJson = JsonUtility.ToJson(data);
    }

    // ------------------------------------------------------------------ fichier

    static void Write(string json)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(TempPath, json);
            if (File.Exists(FilePath))
                File.Replace(TempPath, FilePath, BackupPath); // l'ancien fichier devient la copie de secours
            else
                File.Move(TempPath, FilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Sauvegarde impossible : " + e.Message);
        }
    }

    // Fichier absent ou abîmé -> copie de secours -> sinon null (nouvelle partie). Ne lève jamais d'exception.
    static SaveData Read()
    {
        foreach (string path in new[] { FilePath, BackupPath })
        {
            SaveData data = TryRead(path);
            if (data != null) return Migrate(data);
        }
        return null;
    }

    static SaveData TryRead(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (data == null || data.version <= 0 || data.pads == null || data.employees == null) return null;
            return data;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Conversion des anciens fichiers vers le format courant.
    static SaveData Migrate(SaveData data)
    {
        if (data.version < 2)
        {
            data.upgrades = new System.Collections.Generic.List<SaveData.UpgradeLevel>(); // v1 : niveaux à 0
            data.version = 2;
        }
        if (data.upgrades == null) data.upgrades = new System.Collections.Generic.List<SaveData.UpgradeLevel>();
        return data;
    }

    // ------------------------------------------------------------------ outils de test (menus de l'éditeur)

    public static void DeleteSave()
    {
        Suspended = true; // en Play, l'arrêt ne doit pas réécrire le fichier
        foreach (string path in new[] { FilePath, BackupPath, TempPath })
            if (File.Exists(path)) File.Delete(path);
    }

    // Recule l'horodatage de la dernière sauvegarde pour simuler une absence.
    public static bool SimulateAbsence(float hours)
    {
        SaveData data = Read();
        if (data == null) return false;
        Suspended = true;
        data.lastSaveUtc -= (long)(hours * 3600f);
        data.pendingOffline = 0; // recalculé au prochain démarrage
        data.pendingSeconds = 0;
        Write(JsonUtility.ToJson(data, true));
        if (File.Exists(BackupPath)) File.Delete(BackupPath);
        return true;
    }
}
