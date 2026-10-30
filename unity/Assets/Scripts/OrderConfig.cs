using System;
using UnityEngine;

// Commandes de clients (11c) : tous les chiffres ici, jamais dans le code.
[CreateAssetMenu(fileName = "OrderConfig", menuName = "Idle/Order Config")]
public class OrderConfig : ScriptableObject
{
    [Serializable]
    public class Client
    {
        public string displayName = "Karim";
        public string job = "Gérant";
        public Color skin = new Color32(0xE0, 0xAC, 0x82, 0xFF);
        public Color hair = new Color32(0x3B, 0x2A, 0x20, 0xFF);
        public Color cloth = new Color32(0x1F, 0xA0, 0xFF, 0xFF);
        public Color backdrop = new Color32(0xCF, 0xE6, 0xFF, 0xFF); // fond pastel clair du portrait
    }

    [Serializable]
    public class Tier
    {
        public string displayName = "Petite";
        [Min(1)] public int minItems = 1;
        [Min(1)] public int maxItems = 2;
        [Min(1f)] public float patience = 120f;   // secondes avant que la camionnette reparte
        [Min(0f)] public float weight = 1f;       // chance relative d'être tirée
    }

    [SerializeField] Client[] clients;
    [SerializeField] Tier[] tiers;
    [SerializeField, Min(1f)] float rewardFactor = 1.8f;      // récompense = somme des valeurs x 1,8
    [SerializeField, Min(1)] int proDivisor = 200;            // Points Pro = récompense / 200, minimum 1
    [SerializeField, Min(1f)] float offerDelayMin = 45f;
    [SerializeField, Min(1f)] float offerDelayMax = 90f;
    [SerializeField, Min(1f)] float offerLifetime = 180f;     // une offre non acceptée expire
    [SerializeField, Min(1)] int maxOffers = 3;
    [SerializeField, Min(1)] int maxActive = 1;
    [SerializeField, Min(0f)] float firstOfferDelay = 2f;

    public Client[] Clients => clients;

    // Nom affiché d'un client : la table Loc l'emporte sur la donnée (traduction).
    public static string ClientName(Client c, int index) { return Loc.Or("client." + index + ".name", c.displayName); }
    public Tier[] Tiers => tiers;
    public float RewardFactor => rewardFactor;
    public int ProDivisor => proDivisor;
    public float OfferDelayMin => offerDelayMin;
    public float OfferDelayMax => offerDelayMax;
    public float OfferLifetime => offerLifetime;
    public int MaxOffers => maxOffers;
    public int MaxActive => maxActive;
    public float FirstOfferDelay => firstOfferDelay;

    public int Reward(int baseValue) { return Mathf.RoundToInt(baseValue * rewardFactor); }
    public int ProPoints(int reward) { return Mathf.Max(1, Mathf.RoundToInt(reward / (float)proDivisor)); }
}
