using System;
using System.Collections.Generic;

// Contenu du fichier de sauvegarde (JsonUtility). Pas de client ni de caisse en cours : on repart d'un atelier calme.
[Serializable]
public class SaveData
{
    public const int CurrentVersion = 1;

    [Serializable]
    public class PadState
    {
        public string id;
        public int remaining; // prix restant ; 0 = payée
    }

    public int version = CurrentVersion;
    public int money;
    public long lastSaveUtc;                 // secondes Unix
    public List<PadState> pads = new List<PadState>();
    public List<string> employees = new List<string>(); // "hauler", "courier"
    public bool guideDone;
    public int pendingOffline;               // gain hors ligne calculé mais pas encore récupéré
    public long pendingSeconds;              // durée d'absence correspondante
}
