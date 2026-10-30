using System;
using System.Collections.Generic;

// Contenu du fichier de sauvegarde (JsonUtility), version 8 : jeu idle sans personnage (U1).
// Les anciens champs de la version 7 (argent entier, Points Pro, niveaux de Réputation) restent lisibles pour la migration ;
// tout le reste de l'ancien jeu (postes à poser, employés, piles de caisses...) est ignoré par JsonUtility.
[Serializable]
public class SaveData
{
    public const int CurrentVersion = 8;

    [Serializable]
    public class StationSave
    {
        public bool unlocked;
        public int speed;
        public int add;
    }

    [Serializable]
    public class ReputationLevel
    {
        public string id;
        public int level;
    }

    [Serializable]
    public class OfferSave
    {
        public int id, client, station, qty, pro, say;
        public double reward, progress;
    }

    public int version = CurrentVersion;
    public long lastSaveUtc;                 // secondes Unix
    public double cash;                      // v8
    public double earned;
    public long buys;
    public int proPoints;                    // Points Pro (depuis la v6)
    public int proTotal;
    public List<StationSave> stations = new List<StationSave>();
    public List<ReputationLevel> reputation = new List<ReputationLevel>();   // depuis la v7 ; ids de l'ancien arbre migrés vers ceux de la v8
    public List<OfferSave> offers = new List<OfferSave>();
    public List<OfferSave> active = new List<OfferSave>();
    public int orderSeq = 1;
    public int ordersDone;
    public double nextOfferIn;               // secondes avant la prochaine offre
    public double boostRemaining;            // secondes de turbo restantes
    public double boostCooldown;             // secondes avant que le turbo soit de nouveau prêt
    public int buyMode = 1;                  // 1, 10 ou 0 (= max)
    public bool settingsSaved;               // faux = anciennes sauvegardes, réglages par défaut
    public bool soundOn = true;
    public bool musicOn;
    public bool vibrationOn = true;
    public int money;                        // v7 seulement : argent entier de l'ancien jeu (migré vers `cash`)
}

// Migration des anciens fichiers vers la v8. Règle de Sébastien (U1) : on garde l'argent, les Points Pro et la Réputation,
// et on remet les postes à zéro (le jeu a changé de nature).
public static class SaveMigration
{
    // Ancien arbre de Réputation -> nouveaux bonus (les noeuds sans équivalent sont abandonnés).
    static readonly string[,] RepMap =
    {
        { "revenue", "rev" }, { "chain", "spd" }, { "discount", "cost" }, { "offline", "off" }, { "active", "slot" },
    };

    public static SaveData ToCurrent(SaveData d, IdleEconomyConfig config)
    {
        if (d.stations == null) d.stations = new List<SaveData.StationSave>();
        if (d.reputation == null) d.reputation = new List<SaveData.ReputationLevel>();
        if (d.offers == null) d.offers = new List<SaveData.OfferSave>();
        if (d.active == null) d.active = new List<SaveData.OfferSave>();

        if (d.version < 8)
        {
            d.cash = d.money > 0 ? d.money : config.startCash;
            d.earned = d.cash;
            d.proTotal = d.proPoints;
            d.stations = new List<SaveData.StationSave>();     // postes à zéro : le premier ouvert, les autres verrouillés
            for (int i = 0; i < config.stations.Length; i++) d.stations.Add(new SaveData.StationSave { unlocked = i == 0 });
            var migrated = new List<SaveData.ReputationLevel>();
            foreach (SaveData.ReputationLevel old in d.reputation)
                for (int k = 0; k < RepMap.GetLength(0); k++)
                    if (RepMap[k, 0] == old.id) migrated.Add(new SaveData.ReputationLevel { id = RepMap[k, 1], level = old.level });
            d.reputation = migrated;
            d.offers.Clear(); d.active.Clear();
            d.orderSeq = 1; d.ordersDone = 0;
            d.nextOfferIn = 0; d.boostRemaining = 0; d.boostCooldown = 0;
            d.version = 8;
        }
        while (d.stations.Count < config.stations.Length) d.stations.Add(new SaveData.StationSave());
        if (d.stations.Count > 0) d.stations[0].unlocked = true;
        return d;
    }
}
