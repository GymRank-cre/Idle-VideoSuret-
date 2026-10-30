using System;
using UnityEngine;

// Fait tourner le jeu idle dans la scène : charge la sauvegarde, applique le gain hors ligne, avance le temps, sauvegarde
// (toutes les 10 s si quelque chose a changé, à la mise en pause et à la sortie). Une seule responsabilité : relier IdleGame à Unity.
[DefaultExecutionOrder(-100)]
public class IdleHost : MonoBehaviour
{
    [SerializeField] IdleEconomyConfig config;
    [SerializeField, Min(1f)] float autosaveInterval = 10f;
    [SerializeField, Min(0.01f)] float maxFrameSeconds = 0.25f;   // un gros à-coup ne fait jamais avancer le jeu d'un bloc

    public static IdleHost Instance { get; private set; }
    public IdleGame Game { get; private set; }
    public IdleGame.OfflineResult LastOffline { get; private set; }     // gain calculé au démarrage ou au retour de pause
    public event Action<IdleGame.OfflineResult> OnWelcomeBack;
    public IdleEconomyConfig Config => config;

    string lastSavedJson = "";
    float timer;
    double pausedAtUtc;

    static double UtcNow() { return (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds; }

    void Awake()
    {
        Instance = this;
        SaveSystem.Suspended = false;
        double now = UtcNow();
        Game = new IdleGame(config, 0, now);
        SaveData data = SaveSystem.Read(config);
        if (data != null)
        {
            Game.Load(data);
            AudioManager audio = AudioManager.Instance;
            if (audio != null && data.settingsSaved) audio.Apply(data.soundOn, data.musicOn, data.vibrationOn);
            ApplyOffline(data.lastSaveUtc > 0 ? now - data.lastSaveUtc : 0);
        }
    }

    void ApplyOffline(double gapSeconds)
    {
        if (gapSeconds <= 0) return;
        Game.Now += gapSeconds;   // l'horloge de jeu suit le temps réel (turbo et offres)
        LastOffline = Game.Offline(gapSeconds);
        if (LastOffline.showWelcome) OnWelcomeBack?.Invoke(LastOffline);
    }

    void Update()
    {
        Game.Tick(Math.Min(Time.deltaTime, maxFrameSeconds));
        timer += Time.unscaledDeltaTime;
        if (timer >= autosaveInterval) { timer = 0f; Save(false); }
    }

    void OnApplicationPause(bool paused)
    {
        if (Game == null) return;
        if (paused) { pausedAtUtc = UtcNow(); Save(true); }
        else if (pausedAtUtc > 0) { ApplyOffline(UtcNow() - pausedAtUtc); pausedAtUtc = 0; }
    }

    void OnApplicationQuit() { if (Game != null) Save(true); }

    public SaveData Capture()
    {
        SaveData d = Game.ToSave((long)UtcNow());
        AudioManager audio = AudioManager.Instance;
        if (audio != null) { d.settingsSaved = true; d.soundOn = audio.SoundOn; d.musicOn = audio.MusicOn; d.vibrationOn = audio.VibrationOn; }
        return d;
    }

    public void Save(bool force)
    {
        if (SaveSystem.Suspended || Game == null) return;
        SaveData d = Capture();
        d.lastSaveUtc = 0;
        string compare = JsonUtility.ToJson(d);
        if (!force && compare == lastSavedJson) return;   // rien n'a changé
        d.lastSaveUtc = (long)UtcNow();
        SaveSystem.Write(d);
        lastSavedJson = compare;
    }
}
