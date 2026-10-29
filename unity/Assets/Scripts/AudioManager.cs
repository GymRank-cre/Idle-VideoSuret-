using System;
using UnityEngine;

// Seule instance audio du jeu : pool de 8 sources pour les effets (sons 2D, 3 lectures simultanées maximum d'un même
// son), une source pour la musique, interrupteurs Sons / Musique / Vibrations. Aucune allocation pendant le jeu.
[DefaultExecutionOrder(-300)]
public class AudioManager : MonoBehaviour
{
    const int PoolSize = 8;

    [SerializeField] MusicConfig config;

    AudioSource[] pool;
    float[] startedAt;
    AudioSource music;
    AudioClip[] clips;
    float lastBillTime = -10f;
    int billStep;
    float lastVibration = -10f;
    Transform listener;   // le joueur : les sons lointains s'atténuent
    bool locked;          // WebGL : le navigateur bloque le son avant le premier toucher

    public static AudioManager Instance { get; private set; }

    public bool SoundOn { get; private set; }
    public bool MusicOn { get; private set; }
    public bool VibrationOn { get; private set; }
    public AudioClip[] Clips => clips;
    public bool IsLocked => locked;
    public MusicConfig Config => config;
    public event Action OnSettingsChanged;

    void Awake()
    {
        Instance = this;
        SoundOn = config.SoundOnByDefault;
        MusicOn = config.MusicOnByDefault;
        VibrationOn = config.VibrationOnByDefault;

        locked = Application.platform == RuntimePlatform.WebGLPlayer;
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null) listener = player.transform;

        clips = SfxLibrary.CreateAll(); // une fois pour toutes
        pool = new AudioSource[PoolSize];
        startedAt = new float[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            pool[i] = NewSource("Sfx " + i, false);
            startedAt[i] = -10f;
        }

        music = NewSource("Music", true);
        music.clip = config.Clip != null ? config.Clip : MusicLoop.Create();
        ApplyMusic();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    AudioSource NewSource(string sourceName, bool loop)
    {
        var go = new GameObject(sourceName);
        go.transform.SetParent(transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f; // 2D
        s.loop = loop;
        return s;
    }

    // ------------------------------------------------------------------ réglages

    // Premier toucher de l'utilisateur (WebGL) : le son peut démarrer.
    public void Unlock()
    {
        locked = false;
        ApplyMusic();
    }

    // Appelé au chargement d'une sauvegarde (valeurs mémorisées).
    public void Apply(bool sound, bool musicOn, bool vibration)
    {
        SoundOn = sound;
        MusicOn = musicOn;
        VibrationOn = vibration;
        ApplyMusic();
        OnSettingsChanged?.Invoke();
    }

    public void SetSound(bool on)
    {
        SoundOn = on;
        ApplyMusic();
        OnSettingsChanged?.Invoke();
    }

    public void SetMusic(bool on)
    {
        MusicOn = on;
        ApplyMusic();
        OnSettingsChanged?.Invoke();
    }

    public void SetVibration(bool on)
    {
        VibrationOn = on;
        OnSettingsChanged?.Invoke();
    }

    // La musique suit les interrupteurs ; « Sons » coupe tout (effets et musique).
    void ApplyMusic()
    {
        music.volume = config.MusicVolume;
        bool want = SoundOn && MusicOn && !locked;
        if (want && !music.isPlaying) music.Play();
        else if (!want && music.isPlaying) music.Pause();
    }

    // ------------------------------------------------------------------ lecture

    public static void Play(SfxId id, float pitch = 1f)
    {
        if (Instance != null) Instance.PlayInternal(id, pitch, 1f);
    }

    // Son d'un événement situé dans l'atelier : volume réduit pour un employé, atténué avec la distance au joueur.
    public static void PlayAt(SfxId id, Vector3 position, bool byEmployee)
    {
        if (Instance != null) Instance.PlayAtInternal(id, position, byEmployee);
    }

    // Billets ramassés à moins de 0,6 s d'écart : la note monte d'un demi-ton, jusqu'au plafond, puis retombe.
    public static void PlayBill()
    {
        if (Instance != null) Instance.PlayBillInternal();
    }

    void PlayAtInternal(SfxId id, Vector3 position, bool byEmployee)
    {
        float scale = byEmployee ? config.EmployeeVolume : 1f;
        if (listener != null)
        {
            Vector3 d = position - listener.position;
            d.y = 0f;
            scale *= Mathf.Clamp01(1f - d.magnitude / config.EmployeeHearDistance);
        }
        if (scale < 0.02f) return;
        PlayInternal(id, 1f, scale);
    }

    void PlayBillInternal()
    {
        float now = Time.unscaledTime;
        billStep = now - lastBillTime < config.BillChainWindow ? Mathf.Min(billStep + 1, config.BillMaxSteps) : 0;
        lastBillTime = now;
        PlayInternal(SfxId.Bill, Mathf.Pow(1.059463f, billStep), 1f); // 2^(1/12) par demi-ton
    }

    void PlayInternal(SfxId id, float pitch, float volumeScale)
    {
        if (!SoundOn || locked) return;
        AudioClip clip = clips[(int)id];

        int same = 0, oldest = 0, free = -1;
        for (int i = 0; i < PoolSize; i++)
        {
            AudioSource s = pool[i];
            if (s.isPlaying)
            {
                if (s.clip == clip) same++;
                if (startedAt[i] < startedAt[oldest]) oldest = i;
            }
            else if (free < 0)
            {
                free = i;
            }
        }
        if (same >= config.MaxSameSound) return;

        int slot = free >= 0 ? free : oldest; // pool plein : on remplace le son le plus ancien
        AudioSource src = pool[slot];
        src.clip = clip;
        src.pitch = pitch;
        src.volume = config.SfxVolume * volumeScale;
        src.Play();
        startedAt[slot] = Time.unscaledTime;
    }

    // Vibration : gros événements seulement, 0,5 s minimum entre deux, sur appareil mobile uniquement.
    public static void Vibrate()
    {
        if (Instance != null) Instance.VibrateInternal();
    }

    void VibrateInternal()
    {
        if (!VibrationOn || !Application.isMobilePlatform) return;
        float now = Time.unscaledTime;
        if (now - lastVibration < config.MinVibrationGap) return;
        lastVibration = now;
        Handheld.Vibrate();
    }
}
