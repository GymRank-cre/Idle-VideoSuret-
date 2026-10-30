using System;
using UnityEngine;

// Toutes les valeurs d'économie du jeu idle (U1) : postes, coûts, paliers, Réputation, commandes, hors ligne, turbo.
// Les valeurs par défaut sont celles de la référence web (usine-web/src/econ.js), à l'identique. Rien n'est en dur ailleurs.
[CreateAssetMenu(fileName = "IdleEconomyConfig", menuName = "Idle/Idle Economy Config")]
public class IdleEconomyConfig : ScriptableObject
{
    [Serializable]
    public class Station
    {
        public string id;
        public string nameKey;                 // clé Loc du nom
        public float cycle = 1.6f;             // secondes par cycle au niveau 0
        public double value = 1;               // valeur d'une pièce au niveau 0
        public double unlock = 0;              // prix de déblocage (0 = déjà ouvert)
        public Station(string id, string nameKey, float cycle, double value, double unlock)
        {
            this.id = id; this.nameKey = nameKey; this.cycle = cycle; this.value = value; this.unlock = unlock;
        }
    }

    [Serializable]
    public class RepNode
    {
        public string id;
        public int maxLevel = 10;
        public double baseCost = 2;            // coût = ceil(base x mul^niveau)
        public double mul = 1.55;
        public RepNode(string id, int maxLevel, double baseCost, double mul = 1.55)
        {
            this.id = id; this.maxLevel = maxLevel; this.baseCost = baseCost; this.mul = mul;
        }
    }

    [Header("Postes (dans l'ordre de la chaîne)")]
    public Station[] stations =
    {
        new Station("recep", "station.recep", 1.6f, 1, 0),
        new Station("assem", "station.assem", 3.0f, 12, 180),
        new Station("test", "station.test", 4.5f, 144, 14400),
        new Station("pack", "station.pack", 6.0f, 1728, 1152000),
        new Station("ship", "station.ship", 8.0f, 20736, 92160000),
    };

    [Header("Améliorations")]
    [Min(0f)] public double speedStep = 0.12;          // vitesse = 1 + speedStep x niveau
    [Min(1f)] public double growthSpeed = 1.19;        // coût x1,19 par niveau de vitesse
    [Min(1f)] public double growthAdd = 1.26;          // coût x1,26 par niveau d'ajout
    [Min(0f)] public double costSpeedFactor = 12;      // prix de base = max(plancher, revenu0 x facteur)
    [Min(0f)] public double costAddFactor = 6;
    [Min(0f)] public double costSpeedFloor = 10;
    [Min(0f)] public double costAddFloor = 7;
    public int[] milestones = { 10, 25, 50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000 };   // valeur de la pièce x2 à chaque palier

    [Header("Réputation (Points Pro)")]
    public RepNode[] reputation =
    {
        new RepNode("rev", 10, 2),
        new RepNode("spd", 10, 2),
        new RepNode("cost", 10, 3),
        new RepNode("ordr", 8, 2),
        new RepNode("slot", 2, 12, 3.3),
        new RepNode("off", 5, 4),
        new RepNode("turbo", 4, 6),
    };
    public double repRevenuePerLevel = 0.10;           // revenus +10 % par niveau
    public double repSpeedPerLevel = 0.05;             // cadence +5 %
    public double repCostPerLevel = 0.03;              // prix des améliorations -3 %
    public double repCostFloor = 0.4;                  // le prix ne descend jamais sous 40 %
    public double repOrderPerLevel = 0.15;             // gains des commandes +15 %

    [Header("Hors ligne et turbo")]
    public double offlineBaseHours = 4;
    public double offlineHoursPerLevel = 2;
    public double turboBaseSeconds = 60;
    public double turboSecondsPerLevel = 15;
    public double turboBaseCooldown = 240;
    public double turboCooldownPerLevel = 30;
    public double turboCooldownFloor = 90;
    public double turboMultiplier = 2;
    [Min(0f)] public float offlineMinSeconds = 20f;    // en dessous, on ne calcule rien
    [Min(0f)] public float welcomeMinSeconds = 60f;    // en dessous, pas d'écran « Bon retour »

    [Header("Commandes")]
    [Min(1)] public int offerCount = 3;
    [Min(0f)] public double rewardFactor = 1.5;        // récompense = quantité x valeur x 1,5 x bonus (3,5 ouvrait le 5e poste à 16 min au lieu de 25)
    [Min(0f)] public float nextOfferDelay = 8f;        // secondes avant une nouvelle offre après une acceptation
    [Min(1)] public int minQuantity = 3;
    public float fillSecondsMin = 70f;                 // une offre demande 70 à 130 s de production du poste
    public float fillSecondsMax = 130f;
    [Range(0f, 1f)] public float lastStationChance = 0.55f;   // chance qu'une offre concerne le dernier poste ouvert
    public int proBase = 1;
    public int proFromStation = 2;                     // +1 Point Pro à partir de ce poste
    public int proBigQuantity = 100;                   // +1 Point Pro pour 100 pièces ou plus
    [Min(1)] public int clientCount = 8;               // clients différents (clés Loc client.N.name)
    [Min(1)] public int sayCount = 6;                  // répliques différentes (clés Loc order.say.N)

    [Header("Animation des machines")]
    public float[] visualCap = { 1.6f, 1.2f, 1.2f, 1.0f, 0.45f };   // cycles visuels par seconde au plus (même si la production va plus vite)

    [Header("Départ et achat conseillé")]
    public double startCash = 10;
    public double newStationFavor = 0.35;              // un nouveau poste est favorisé (délai de remboursement x0,35)
    public double goalFarSeconds = 12;                 // objectif trop loin : on conseille le meilleur achat abordable
}
