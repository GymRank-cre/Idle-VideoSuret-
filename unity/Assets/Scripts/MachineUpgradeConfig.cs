using System;
using UnityEngine;

// Améliorations par machine (11b) : deux axes, VITESSE (cycle plus court) et AJOUTER (une unité de plus en parallèle).
// Tous les chiffres sont ici (jamais dans le code) : coûts de base par machine, croissance, plafonds, paliers.
[CreateAssetMenu(fileName = "MachineUpgradeConfig", menuName = "Idle/Machine Upgrade Config")]
public class MachineUpgradeConfig : ScriptableObject
{
    [Serializable]
    public class Machine
    {
        public string id = "assembly";         // identifiant stable (sauvegarde)
        public string displayName = "Assemblage";
        [Min(1)] public int speedBaseCost = 120;
        [Min(1)] public int addBaseCost = 200;
        [Min(1)] public int speedMax = 50;
        [Min(1)] public int addMax = 25;       // l'assemblage est plafonné par son plateau de sortie (4 places)
    }

    [SerializeField] Machine[] machines;
    [SerializeField, Min(1f)] float costGrowth = 1.19f;           // coût x1,19 par niveau (11b : recalé pour le mur doux du niveau 15, voir TACHES.md)
    [SerializeField, Min(0f)] float speedPerLevel = 0.15f;        // cycle = base / (1 + 0,15 x niveau)
    [SerializeField] int[] milestones = { 10, 25, 50 };           // paliers : débit x2 et amélioration visible
    [SerializeField, Min(1f)] float milestoneFactor = 2f;

    public Machine[] Machines => machines;
    public float CostGrowth => costGrowth;
    public int[] Milestones => milestones;

    public int IndexOf(string id)
    {
        for (int i = 0; i < machines.Length; i++) if (machines[i].id == id) return i;
        return -1;
    }

    // Prix du prochain niveau (`level` = niveau actuel).
    public int SpeedCost(Machine m, int level) { return Mathf.RoundToInt(m.speedBaseCost * Mathf.Pow(costGrowth, level)); }
    public int AddCost(Machine m, int level) { return Mathf.RoundToInt(m.addBaseCost * Mathf.Pow(costGrowth, level)); }

    // Nombre de paliers atteints à ce niveau.
    public int MilestonesReached(int level)
    {
        int n = 0;
        foreach (int m in milestones) if (level >= m) n++;
        return n;
    }

    // Multiplicateur de vitesse (cycle = base / ce nombre) : effet linéaire des niveaux de VITESSE, x2 par palier de l'un ou l'autre axe.
    public float SpeedFactor(int speedLevel, int addLevel)
    {
        float linear = 1f + speedPerLevel * speedLevel;
        return linear * Mathf.Pow(milestoneFactor, MilestonesReached(speedLevel) + MilestonesReached(addLevel));
    }
}
