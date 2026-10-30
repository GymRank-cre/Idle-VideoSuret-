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
    [SerializeField] ProPoints proPoints;         // Points Pro (11c)
    [SerializeField] ReputationState reputation;  // arbre de bonus permanents (11d)
    [SerializeField] MachineUpgradeState machineUpgrades; // améliorations par machine (11b)
    [SerializeField] ProcessStation[] stations;   // machines de la chaîne (Test, Emballage)
    [SerializeField] GameObject belt1;            // tapis (actif = acheté)
    [SerializeField] GameObject belt2;
    [SerializeField] MachineConfig testConfig;
    [SerializeField] MachineConfig packConfig;
    [SerializeField] GameObject haulerObject;     // porteur de caisses (actif = embauché)
    [SerializeField] GameObject courierObject;    // livreur
    [SerializeField] UpgradeState upgradeState;
    [SerializeField] AudioManager audioManager;
    [SerializeField] GuideConfig guideConfig;
    [SerializeField] OfflineConfig offlineConfig;
    [SerializeField] StationConfig stationConfig;
    [SerializeField] EmployeeConfig haulerConfig;
    [SerializeField] EmployeeConfig courierConfig;
    [SerializeField] CustomerConfig customerConfig;
    [SerializeField] OfflineWelcomeScreen welcomeScreen;
    [SerializeField, Min(1f)] float autosaveInterval = 10f;

#if UNITY_WEBGL && !UNITY_EDITOR
    const bool WebGlRuntime = true;   // le système de fichiers WebGL est en mémoire : on passe par PlayerPrefs
#else
    const bool WebGlRuntime = false;
#endif
    // Vrai pour tester le repli WebGL dans l'éditeur.
    public static bool ForcePlayerPrefs;
    const string PrefsKey = "save_json";
    const string PrefsBackupKey = "save_json_bak";

    static bool UsePlayerPrefs => WebGlRuntime || ForcePlayerPrefs;

    // Vrai pendant un test « absence simulée » ou « effacer » en Play : plus aucune écriture.
    public static bool Suspended;

    static string guideKey = "guide_done";

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
        if (guideConfig != null) guideKey = guideConfig.DoneKey;
        loaded = Read();
        // Avant tous les Start : le guide lit ce drapeau.
        if (loaded != null && loaded.guideDone && guideConfig != null)
            PlayerPrefs.SetInt(guideConfig.DoneKey, 1);
    }

    void Start()
    {
        if (loaded == null) return; // nouvelle partie : le gameplay est inchangé

        if (audioManager != null && loaded.settingsSaved)
            audioManager.Apply(loaded.soundOn, loaded.musicOn, loaded.vibrationOn);
        wallet.Add(loaded.money);
        if (proPoints != null && loaded.proPoints > 0) proPoints.Set(loaded.proPoints);
        if (reputation != null && loaded.reputation != null)
            foreach (SaveData.ReputationLevel r in loaded.reputation) reputation.SetLevel(r.id, r.level);
        if (upgradeState != null)
            foreach (SaveData.UpgradeLevel u in loaded.upgrades)
                upgradeState.SetLevel(u.id, u.level);
        if (machineUpgrades != null && loaded.machineLevels != null)
            foreach (SaveData.MachineLevel ml in loaded.machineLevels)
            {
                machineUpgrades.SetLevel(ml.id, MachineUpgradeState.Speed, ml.speed);
                machineUpgrades.SetLevel(ml.id, MachineUpgradeState.Add, ml.add);
            }
        foreach (UnlockPad pad in pads)
        {
            SaveData.PadState state = loaded.pads.Find(p => p.id == pad.SaveId);
            if (state != null) pad.Restore(state.remaining);
        }

        if (stations != null && loaded.machines != null)
            foreach (ProcessStation st in stations)
            {
                SaveData.MachineStock stock = loaded.machines.Find(m => m.id == st.name);
                if (stock != null && st.gameObject.activeInHierarchy) st.RestoreStocks(stock.input, stock.output);
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
                upgradeState != null ? upgradeState.CustomerPay : 1f,
                upgradeState != null ? upgradeState.EmployeeExtraCapacity : 0,
                ChainOffline());
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

    // Chaîne de production pour le calcul hors ligne : seulement si les deux tapis sont achetés (livreur automatique au bout).
    OfflineEarnings.Chain ChainOffline()
    {
        bool automated = belt1 != null && belt2 != null && belt1.activeInHierarchy && belt2.activeInHierarchy
            && testConfig != null && packConfig != null;
        var chain = new OfflineEarnings.Chain { automated = automated, valueMultiplier = automated ? packConfig.ValueMultiplier : 1f };
        if (machineUpgrades != null)
        {
            chain.assemblySpeed = machineUpgrades.SpeedFactor("assembly");
            chain.assemblyExtraProducts = machineUpgrades.ExtraUnits("assembly");
        }
        if (automated)
        {
            chain.testThroughput = Effective(testConfig, "test");
            chain.packThroughput = Effective(packConfig, "pack");
        }
        return chain;
    }

    // Débit d'une machine avec ses améliorations : (sortie + unités en plus) / (cycle / vitesse).
    float Effective(MachineConfig c, string id)
    {
        float speed = machineUpgrades != null ? machineUpgrades.SpeedFactor(id) : 1f;
        int extra = machineUpgrades != null ? machineUpgrades.ExtraUnits(id) : 0;
        return (c.OutputPerCycle + extra) / (c.CycleSeconds / (speed * Bonuses.ChainSpeed));
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
            proPoints = proPoints != null ? proPoints.Points : 0,
            lastSaveUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            guideDone = guideConfig != null && PlayerPrefs.GetInt(guideConfig.DoneKey, 0) == 1,
            settingsSaved = audioManager != null,
            soundOn = audioManager == null || audioManager.SoundOn,
            musicOn = audioManager != null && audioManager.MusicOn,
            vibrationOn = audioManager == null || audioManager.VibrationOn,
            pendingOffline = pending != null ? pending.pendingOffline : 0,
            pendingSeconds = pending != null ? pending.pendingSeconds : 0
        };
        if (upgradeState != null)
            for (int i = 0; i < upgradeState.Count; i++)
                data.upgrades.Add(new SaveData.UpgradeLevel { id = upgradeState.Config.Upgrades[i].id, level = upgradeState.Level(i) });
        foreach (UnlockPad pad in pads)
            data.pads.Add(new SaveData.PadState { id = pad.SaveId, remaining = pad.RemainingPrice });
        if (reputation != null)
            for (int i = 0; i < reputation.Config.Nodes.Length; i++)
                if (reputation.Level(i) > 0) data.reputation.Add(new SaveData.ReputationLevel { id = reputation.Config.Nodes[i].id, level = reputation.Level(i) });
        if (machineUpgrades != null)
            foreach (MachineUpgradeConfig.Machine m in machineUpgrades.Config.Machines)
                data.machineLevels.Add(new SaveData.MachineLevel
                {
                    id = m.id,
                    speed = machineUpgrades.Level(m.id, MachineUpgradeState.Speed),
                    add = machineUpgrades.Level(m.id, MachineUpgradeState.Add)
                });
        if (stations != null)
            foreach (ProcessStation st in stations)
                if (st != null && st.gameObject.activeInHierarchy)
                    data.machines.Add(new SaveData.MachineStock { id = st.name, input = st.SavedInput, output = st.SavedOutput });
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
        if (UsePlayerPrefs)
        {
            string previous = PlayerPrefs.GetString(PrefsKey, "");
            if (previous.Length > 0) PlayerPrefs.SetString(PrefsBackupKey, previous); // copie de secours sur une 2e clé
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
            return;
        }

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
        if (UsePlayerPrefs)
        {
            foreach (string key in new[] { PrefsKey, PrefsBackupKey })
            {
                SaveData data = TryParse(PlayerPrefs.GetString(key, ""));
                if (data != null) return Migrate(data);
            }
            return null;
        }

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
            return File.Exists(path) ? TryParse(File.ReadAllText(path)) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    static SaveData TryParse(string json)
    {
        try
        {
            if (string.IsNullOrEmpty(json)) return null;
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.version <= 0 || data.pads == null || data.employees == null) return null;
            return data;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Conversion des anciens fichiers vers le format courant.
    public static SaveData Migrate(SaveData data)
    {
        if (data.version < 2)
        {
            data.upgrades = new System.Collections.Generic.List<SaveData.UpgradeLevel>(); // v1 : niveaux à 0
            data.version = 2;
        }
        if (data.upgrades == null) data.upgrades = new System.Collections.Generic.List<SaveData.UpgradeLevel>();
        if (data.version < 3)
        {
            data.settingsSaved = false; // v2 : pas de réglages, valeurs par défaut
            data.version = 3;
        }
        if (data.machines == null) data.machines = new System.Collections.Generic.List<SaveData.MachineStock>();
        if (data.version < 4)
        {
            data.version = 4; // v3 : pas de chaîne, machines et tapis verrouillés (leurs dalles absentes = prix plein)
        }
        if (data.machineLevels == null) data.machineLevels = new System.Collections.Generic.List<SaveData.MachineLevel>();
        if (data.version < 5)
        {
            data.version = 5; // v4 : aucune amélioration par machine, tout à 0
        }
        if (data.version < 6)
        {
            data.version = 6; // v5 : pas de Points Pro (0)
        }
        if (data.reputation == null) data.reputation = new System.Collections.Generic.List<SaveData.ReputationLevel>();
        if (data.version < 7)
        {
            data.version = 7; // v6 : arbre Réputation vide (tous les niveaux à 0)
        }
        return data;
    }

    // ------------------------------------------------------------------ outils de test (menus de l'éditeur)

    // Bouton « Effacer la sauvegarde » des réglages : fichier, copie de secours et mémoire du guide.
    public static void EraseEverything()
    {
        DeleteSave();
        PlayerPrefs.DeleteKey(guideKey);
        PlayerPrefs.Save();
    }

    public static void DeleteSave()
    {
        Suspended = true; // en Play, l'arrêt ne doit pas réécrire le fichier
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.DeleteKey(PrefsBackupKey);
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
        if (!UsePlayerPrefs && File.Exists(BackupPath)) File.Delete(BackupPath);
        return true;
    }
}
