using System;
using UnityEngine;

// Niveaux des améliorations par machine (11b). Lu par les machines, le calcul hors ligne et la sauvegarde.
public class MachineUpgradeState : MonoBehaviour
{
    public const int Speed = 0;
    public const int Add = 1;

    [SerializeField] MachineUpgradeConfig config;

    int[] levels; // [machine * 2 + axe]

    public MachineUpgradeConfig Config => config;
    public event Action<string> OnChanged; // identifiant de la machine

    void EnsureLevels()
    {
        if (levels == null) levels = new int[config.Machines.Length * 2];
    }

    public int Level(string id, int axis)
    {
        int i = config.IndexOf(id);
        if (i < 0) return 0;
        EnsureLevels();
        return levels[i * 2 + axis];
    }

    public int Max(string id, int axis)
    {
        int i = config.IndexOf(id);
        if (i < 0) return 0;
        return axis == Speed ? config.Machines[i].speedMax : config.Machines[i].addMax;
    }

    public bool IsMax(string id, int axis) { return Level(id, axis) >= Max(id, axis); }

    // Prix du prochain niveau d'un axe.
    public int NextCost(string id, int axis)
    {
        int i = config.IndexOf(id);
        if (i < 0) return 0;
        MachineUpgradeConfig.Machine m = config.Machines[i];
        return axis == Speed ? config.SpeedCost(m, Level(id, axis)) : config.AddCost(m, Level(id, axis));
    }

    // Achat d'un niveau (l'argent est déjà payé par la dalle) ; renvoie true si un palier vient d'être atteint.
    public bool Purchase(string id, int axis)
    {
        int i = config.IndexOf(id);
        if (i < 0 || IsMax(id, axis)) return false;
        EnsureLevels();
        int before = config.MilestonesReached(levels[i * 2 + axis]);
        levels[i * 2 + axis]++;
        bool milestone = config.MilestonesReached(levels[i * 2 + axis]) > before;
        OnChanged?.Invoke(id);
        return milestone;
    }

    public void SetLevel(string id, int axis, int level)
    {
        int i = config.IndexOf(id);
        if (i < 0) return;
        EnsureLevels();
        levels[i * 2 + axis] = Mathf.Clamp(level, 0, Max(id, axis));
        OnChanged?.Invoke(id);
    }

    // Multiplicateur de vitesse (cycle = base / ce nombre) et unités en plus.
    public float SpeedFactor(string id) { return config.SpeedFactor(Level(id, Speed), Level(id, Add)); }
    public int ExtraUnits(string id) { return Level(id, Add); }
    public int MilestoneCount(string id) { return config.MilestonesReached(Level(id, Speed)) + config.MilestonesReached(Level(id, Add)); }
    public int TotalLevel(string id) { return Level(id, Speed) + Level(id, Add); }
}
