using System;
using UnityEngine;

// Niveaux de l'arbre Réputation (11d) ; les Points Pro s'y dépensent ; recalcule les multiplicateurs de `Bonuses`.
public class ReputationState : MonoBehaviour
{
    [SerializeField] ReputationConfig config;
    [SerializeField] ProPoints pro;

    int[] levels;

    public ReputationConfig Config => config;
    public int Points => pro.Points;
    public event Action OnChanged;
    public event Action<int> OnPurchased;   // indice du noeud acheté

    void EnsureLevels()
    {
        if (levels == null || levels.Length != config.Nodes.Length) levels = new int[config.Nodes.Length];
    }

    void Awake() { EnsureLevels(); Recompute(); }

    void OnEnable() { pro.OnChanged += OnPro; }
    void OnDisable() { pro.OnChanged -= OnPro; }
    void OnPro(int p) { OnChanged?.Invoke(); }

    public int Level(int index) { EnsureLevels(); return levels[index]; }
    public bool IsMax(int index) { return Level(index) >= config.Nodes[index].maxLevel; }
    public int NextCost(int index) { return config.Cost(config.Nodes[index], Level(index)); }

    // Noeud verrouillé : prérequis manquant ; renvoie le nom du prérequis, ou null.
    public string MissingPrerequisite(int index)
    {
        ReputationConfig.Node n = config.Nodes[index];
        if (string.IsNullOrEmpty(n.prerequisiteId)) return null;
        int p = config.IndexOf(n.prerequisiteId);
        if (p < 0 || Level(p) >= n.prerequisiteLevel) return null;
        return config.Nodes[p].displayName + " niv " + n.prerequisiteLevel;
    }

    public bool CanBuy(int index)
    {
        return config.Nodes[index].implemented && !IsMax(index) && MissingPrerequisite(index) == null && pro.Points >= NextCost(index);
    }

    public bool AnyAffordable
    {
        get { for (int i = 0; i < config.Nodes.Length; i++) if (CanBuy(i)) return true; return false; }
    }

    public bool TryBuy(int index)
    {
        if (!CanBuy(index) || !pro.TrySpend(NextCost(index))) return false;
        EnsureLevels();
        levels[index]++;
        Recompute();
        OnChanged?.Invoke();
        OnPurchased?.Invoke(index);
        return true;
    }

    public void SetLevel(string id, int level)
    {
        int i = config.IndexOf(id);
        if (i < 0) return;
        EnsureLevels();
        levels[i] = Mathf.Clamp(level, 0, config.Nodes[i].maxLevel);
        Recompute();
        OnChanged?.Invoke();
    }

    // Effet actuel d'un noeud (en % ou en unités selon son type).
    public float Total(int index) { return config.Total(config.Nodes[index], Level(index)); }

    // Recalcule tous les multiplicateurs de `Bonuses` d'après les niveaux.
    public void Recompute()
    {
        EnsureLevels();
        Bonuses.Reset();
        for (int i = 0; i < config.Nodes.Length; i++)
        {
            ReputationConfig.Node n = config.Nodes[i];
            if (!n.implemented) continue;
            float t = config.Total(n, levels[i]);
            switch (n.effect)
            {
                case ReputationEffect.Revenue: Bonuses.Revenue += t / 100f; break;
                case ReputationEffect.ChainSpeed: Bonuses.ChainSpeed += t / 100f; break;
                case ReputationEffect.OfflineGain: Bonuses.OfflineGain += t / 100f; break;
                case ReputationEffect.OfflineCap: Bonuses.OfflineCapHours += t; break;
                case ReputationEffect.StackCapacity: Bonuses.StackCapacity += Mathf.RoundToInt(t); break;
                case ReputationEffect.PlayerSpeed: Bonuses.PlayerSpeed += t / 100f; break;
                case ReputationEffect.OrderPatience: Bonuses.OrderPatience += t / 100f; break;
                case ReputationEffect.OfferSlots: Bonuses.OfferSlots += Mathf.RoundToInt(t); break;
                case ReputationEffect.ActiveOrders: Bonuses.ActiveOrders += Mathf.RoundToInt(t); break;
                case ReputationEffect.PadDiscount: Bonuses.PadPrice = Mathf.Max(0.2f, Bonuses.PadPrice - t / 100f); break;
            }
        }
    }

    void OnDestroy() { Bonuses.Reset(); }
}
