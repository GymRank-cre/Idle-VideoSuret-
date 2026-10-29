using System;
using UnityEngine;

// Niveaux d'amélioration du joueur, en mémoire seulement : les ScriptableObject ne sont jamais modifiés pendant le jeu.
// Les scripts concernés lisent ces multiplicateurs à chaque usage.
[DefaultExecutionOrder(-200)]
public class UpgradeState : MonoBehaviour
{
    [SerializeField] UpgradeConfig config;
    [SerializeField] Wallet wallet;

    int[] levels;

    public UpgradeConfig Config => config;
    public int Count => config.Upgrades.Length;
    public event Action OnChanged;
    public event Action<int> OnPurchased; // index de l'amélioration achetée

    void Awake()
    {
        EnsureLevels();
    }

    void EnsureLevels()
    {
        if (levels == null) levels = new int[config.Upgrades.Length];
    }

    public int Level(int index) { EnsureLevels(); return levels[index]; }
    public bool IsMax(int index) => Level(index) >= config.Upgrades[index].maxLevel;
    public int NextCost(int index) => config.Cost(config.Upgrades[index], Level(index));
    public bool CanAfford(int index) => !IsMax(index) && wallet.Money >= NextCost(index);

    public bool AnyAffordable
    {
        get
        {
            for (int i = 0; i < Count; i++)
                if (CanAfford(i)) return true;
            return false;
        }
    }

    public bool TryBuy(int index)
    {
        if (IsMax(index) || !wallet.TrySpend(NextCost(index))) return false;
        levels[index]++;
        OnChanged?.Invoke();
        OnPurchased?.Invoke(index);
        return true;
    }

    // Niveau par identifiant (sauvegarde).
    public int LevelOf(string id)
    {
        EnsureLevels();
        for (int i = 0; i < levels.Length; i++)
            if (config.Upgrades[i].id == id) return levels[i];
        return 0;
    }

    public void SetLevel(string id, int level)
    {
        EnsureLevels();
        for (int i = 0; i < levels.Length; i++)
            if (config.Upgrades[i].id == id)
                levels[i] = Mathf.Clamp(level, 0, config.Upgrades[i].maxLevel);
        OnChanged?.Invoke();
    }

    float ValueOf(UpgradeKind kind)
    {
        EnsureLevels();
        for (int i = 0; i < levels.Length; i++)
            if (config.Upgrades[i].kind == kind) return config.Value(kind, levels[i]);
        return kind == UpgradeKind.StackCapacity ? 0f : 1f;
    }

    public int ExtraCapacity => Mathf.RoundToInt(ValueOf(UpgradeKind.StackCapacity));
    public float PlayerSpeed => ValueOf(UpgradeKind.PlayerSpeed);
    public float JobDuration => ValueOf(UpgradeKind.JobSpeed);   // multiplicateur de durée (< 1 = plus rapide)
    public float CustomerPay => ValueOf(UpgradeKind.CustomerPay);
    public float EmployeeSpeed => ValueOf(UpgradeKind.EmployeeSpeed);
}
