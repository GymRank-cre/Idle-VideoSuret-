using UnityEngine;

// Réglages de la musique. `clip` (facultatif) : fichier libre de droits fourni plus tard ; sans fichier,
// une boucle d'ambiance très douce est générée par code.
[CreateAssetMenu(fileName = "MusicConfig", menuName = "Idle/Music Config")]
public class MusicConfig : ScriptableObject
{
    [SerializeField] AudioClip clip;
    [SerializeField, Range(0f, 1f)] float musicVolume = 0.22f;
    [SerializeField, Range(0f, 1f)] float sfxVolume = 0.8f;
    [SerializeField] bool musicOnByDefault = false; // générée par code, à écouter avant de l'activer par défaut
    [SerializeField] bool soundOnByDefault = true;
    [SerializeField] bool vibrationOnByDefault = true;
    [SerializeField, Min(0.1f)] float minVibrationGap = 0.5f;
    [SerializeField, Range(1, 8)] int maxSameSound = 3;
    [Header("Sons des employés")]
    [SerializeField, Range(0f, 1f)] float employeeVolume = 0.35f;   // part du volume du joueur
    [SerializeField, Min(1f)] float employeeHearDistance = 12f;       // inaudible au-delà (distance au joueur)
    [Header("Billets : hauteur progressive")]
    [SerializeField, Min(0.05f)] float billChainWindow = 0.6f;   // billets ramassés à moins de ce délai : la note monte
    [SerializeField, Range(1, 24)] int billMaxSteps = 8;          // demi-tons maximum avant plafond

    public AudioClip Clip => clip;
    public float MusicVolume => musicVolume;
    public float SfxVolume => sfxVolume;
    public bool MusicOnByDefault => musicOnByDefault;
    public bool SoundOnByDefault => soundOnByDefault;
    public bool VibrationOnByDefault => vibrationOnByDefault;
    public float MinVibrationGap => minVibrationGap;
    public int MaxSameSound => maxSameSound;
    public float EmployeeVolume => employeeVolume;
    public float EmployeeHearDistance => employeeHearDistance;
    public float BillChainWindow => billChainWindow;
    public int BillMaxSteps => billMaxSteps;
}
