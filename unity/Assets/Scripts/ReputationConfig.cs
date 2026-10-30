using System;
using UnityEngine;

// Arbre de bonus permanents « Réputation » (11d) : tout est ici, jamais en dur. Les Points Pro (commandes) s'y dépensent.
public enum ReputationEffect
{
    Revenue,        // + x % de tous les paiements (clients et commandes)
    ChainSpeed,     // + x % de vitesse des machines
    OfflineGain,    // + x % de gains hors ligne
    OfflineCap,     // + n heures de plafond hors ligne
    StackCapacity,  // + n caisses dans le dos du technicien
    PlayerSpeed,    // + x % de vitesse du technicien
    OrderPatience,  // + x % de patience des clients de commande
    OfferSlots,     // + n offres affichées
    ActiveOrders,   // + n commandes en cours en parallèle (pas encore géré : voir TACHES.md)
    PadDiscount     // - x % sur le prix des dalles de déblocage
}

[CreateAssetMenu(fileName = "ReputationConfig", menuName = "Idle/Reputation Config")]
public class ReputationConfig : ScriptableObject
{
    [Serializable]
    public class Node
    {
        public string id = "revenue";
        public string displayName = "Revenus";
        public string description = "+{0} % de tous les paiements";   // {0} = effet total au niveau courant
        public string iconText = "$";
        public Color iconColor = new Color32(0x3D, 0xDC, 0x5A, 0xFF);
        public ReputationEffect effect;
        [Min(0f)] public float valuePerLevel = 10f;                    // en % ou en unités selon l'effet
        [Min(1)] public int maxLevel = 10;
        [Min(1)] public int baseCost = 2;
        [Min(1f)] public float costGrowth = 1.6f;
        public string prerequisiteId = "";                             // noeud à avoir amélioré d'abord
        [Min(0)] public int prerequisiteLevel = 0;
        public bool implemented = true;                                // faux : affiché « Bientôt »
    }

    [SerializeField] Node[] nodes;

    public Node[] Nodes => nodes;

    public int IndexOf(string id)
    {
        for (int i = 0; i < nodes.Length; i++) if (nodes[i].id == id) return i;
        return -1;
    }

    // Coût en Points Pro du prochain niveau (`level` = niveau actuel).
    public int Cost(Node n, int level)
    {
        return Mathf.Max(1, Mathf.RoundToInt(n.baseCost * Mathf.Pow(n.costGrowth, level)));
    }

    // Effet total d'un noeud au niveau donné (en % ou en unités).
    public float Total(Node n, int level) { return n.valuePerLevel * level; }
}
