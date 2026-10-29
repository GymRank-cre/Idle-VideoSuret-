using UnityEngine;

// Machine générique de la chaîne : prend dans son stock d'entrée, travaille un cycle, pose dans son stock de sortie.
// Sortie pleine = elle s'arrête (état Blocked). Les stocks sont de simples compteurs : les objets visibles
// (tapis, boîtiers) sont gérés ailleurs et appellent TryAddInput / TakeOutput.
public class ProductionMachine : MonoBehaviour, IMachineStatus, IThroughput
{
    [SerializeField] MachineConfig config;
    [SerializeField] MachineUpgradeState upgrades;   // facultatif : améliorations par machine (11b)
    [SerializeField] string machineId;

    int input;
    int output;
    bool working;
    float timer;

    public MachineConfig Config => config;
    public int InputCount => input;
    // Valeurs effectives : réglages + niveaux (AJOUTER = une unité de plus en parallèle, VITESSE = cycle plus court).
    int Extra => upgrades != null && !string.IsNullOrEmpty(machineId) ? upgrades.ExtraUnits(machineId) : 0;
    public float CycleSeconds => config.CycleSeconds / (upgrades != null && !string.IsNullOrEmpty(machineId) ? upgrades.SpeedFactor(machineId) : 1f);
    public int InputPerCycle => config.InputPerCycle + Extra;
    public int OutputPerCycle => config.OutputPerCycle + Extra;
    public int InputCapacity => config.InputCapacity + Extra * 2;
    public int OutputCount => output;
    public int OutputCapacity => config.OutputCapacity + Extra * 2;
    public float Progress => working ? Mathf.Clamp01(timer / CycleSeconds) : 0f;
    public float ThroughputPerSecond => OutputPerCycle / CycleSeconds;

    public MachineState State
    {
        get
        {
            if (working) return timer >= CycleSeconds ? MachineState.Blocked : MachineState.Running;
            return input >= InputPerCycle ? MachineState.Running : MachineState.WaitingInput;
        }
    }

    public void Configure(MachineConfig newConfig) { config = newConfig; }

    // Restauration depuis la sauvegarde.
    public void SetStocks(int newInput, int newOutput)
    {
        input = Mathf.Clamp(newInput, 0, InputCapacity);
        output = Mathf.Clamp(newOutput, 0, OutputCapacity);
    }

    public bool TryAddInput(int count = 1)
    {
        if (input + count > InputCapacity) return false;
        input += count;
        return true;
    }

    public bool TryTakeOutput(int count = 1)
    {
        if (output < count) return false;
        output -= count;
        return true;
    }

    void Update() { Step(Time.deltaTime); }

    // Avance la machine de `dt` secondes (public pour les tests).
    public void Step(float dt)
    {
        if (!working)
        {
            if (input < InputPerCycle) return;
            input -= InputPerCycle;
            working = true;
            timer = 0f;
        }

        float cycle = CycleSeconds;
        timer = Mathf.Min(timer + dt, cycle);
        if (timer < cycle) return;

        // cycle fini : on attend une place en sortie (rien n'est perdu), la machine reste « bloquée » en attendant
        if (output + OutputPerCycle > OutputCapacity) return;
        output += OutputPerCycle;
        working = false;
        timer = 0f;
    }
}
