using System;
using System.Collections.Generic;

// Contenu du fichier de sauvegarde (JsonUtility). Pas de client ni de caisse en cours : on repart d'un atelier calme.
[Serializable]
public class SaveData
{
    public const int CurrentVersion = 3;

    [Serializable]
    public class PadState
    {
        public string id;
        public int remaining; // prix restant ; 0 = payée
    }

    [Serializable]
    public class UpgradeLevel
    {
        public string id;
        public int level;
    }

    public int version = CurrentVersion;
    public int money;
    public long lastSaveUtc;                 // secondes Unix
    public List<PadState> pads = new List<PadState>();
    public List<string> employees = new List<string>(); // "hauler", "courier"
    public List<UpgradeLevel> upgrades = new List<UpgradeLevel>(); // v2
    public bool settingsSaved;               // v3 : faux = anciennes sauvegardes, on garde les réglages par défaut
    public bool soundOn = true;
    public bool musicOn;
    public bool vibrationOn = true;
    public bool guideDone;
    public int pendingOffline;               // gain hors ligne calculé mais pas encore récupéré
    public long pendingSeconds;              // durée d'absence correspondante
}
