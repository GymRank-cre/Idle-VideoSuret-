using System;
using UnityEngine;

public enum UpgradeKind { StackCapacity, PlayerSpeed, JobSpeed, CustomerPay, EmployeeSpeed, EmployeeCapacity }

// Liste des améliorations achetables. Aucun niveau n'est écrit ici : les niveaux vivent dans UpgradeState (runtime).
[CreateAssetMenu(fileName = "UpgradeConfig", menuName = "Idle/Upgrade Config")]
public class UpgradeConfig : ScriptableObject
{
    [Serializable]
    public class Upgrade
    {
        public string id = "upgrade";           // identifiant stable (sauvegarde)
        public string displayName = "Amélioration";
        public UpgradeKind kind;
        [Min(1)] public int baseCost = 60;
        [Min(1f)] public float costGrowth = 1.35f;
        [Min(1)] public int maxLevel = 10;
        public float effectPerLevel = 0.05f;    // +1 caisse, ou une part (0,05 = 5 %)
    }

    [SerializeField] Upgrade[] upgrades;

    public Upgrade[] Upgrades => upgrades;

    // Coût du prochain niveau (le niveau actuel est `level`), arrondi.
    public int Cost(Upgrade u, int level)
    {
        return Mathf.RoundToInt(u.baseCost * Mathf.Pow(u.costGrowth, level));
    }

    // Multiplicateur (ou nombre de caisses en plus pour la pile) à un niveau donné.
    public float Value(UpgradeKind kind, int level)
    {
        foreach (Upgrade u in upgrades)
        {
            if (u.kind != kind) continue;
            switch (kind)
            {
                case UpgradeKind.StackCapacity:
                case UpgradeKind.EmployeeCapacity: return u.effectPerLevel * level;
                case UpgradeKind.JobSpeed: return Mathf.Max(0.2f, 1f - u.effectPerLevel * level); // durée du travail
                default: return 1f + u.effectPerLevel * level;
            }
        }
        return kind == UpgradeKind.StackCapacity || kind == UpgradeKind.EmployeeCapacity ? 0f : 1f;
    }
}
