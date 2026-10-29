using UnityEngine;

// Machine générique de la chaîne : prend dans son stock d'entrée, travaille un cycle, pose dans son stock de sortie.
// Sortie pleine = elle s'arrête (état Blocked). Les stocks sont de simples compteurs : les objets visibles
// (tapis, boîtiers) sont gérés ailleurs et appellent TryAddInput / TakeOutput.
public class ProductionMachine : MonoBehaviour, IMachineStatus
{
    [SerializeField] MachineConfig config;

    int input;
    int output;
    bool working;
    float timer;

    public MachineConfig Config => config;
    public int InputCount => input;
    public int InputCapacity => config.InputCapacity;
    public int OutputCount => output;
    public int OutputCapacity => config.OutputCapacity;
    public float Progress => working ? Mathf.Clamp01(timer / config.CycleSeconds) : 0f;

    public MachineState State
    {
        get
        {
            if (working) return timer >= config.CycleSeconds ? MachineState.Blocked : MachineState.Running;
            return input >= config.InputPerCycle ? MachineState.Running : MachineState.WaitingInput;
        }
    }

    public void Configure(MachineConfig newConfig) { config = newConfig; }

    // Restauration depuis la sauvegarde.
    public void SetStocks(int newInput, int newOutput)
    {
        input = Mathf.Clamp(newInput, 0, config.InputCapacity);
        output = Mathf.Clamp(newOutput, 0, config.OutputCapacity);
    }

    public bool TryAddInput(int count = 1)
    {
        if (input + count > config.InputCapacity) return false;
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
            if (input < config.InputPerCycle) return;
            input -= config.InputPerCycle;
            working = true;
            timer = 0f;
        }

        timer = Mathf.Min(timer + dt, config.CycleSeconds);
        if (timer < config.CycleSeconds) return;

        // cycle fini : on attend une place en sortie (rien n'est perdu), la machine reste « bloquée » en attendant
        if (output + config.OutputPerCycle > config.OutputCapacity) return;
        output += config.OutputPerCycle;
        working = false;
        timer = 0f;
    }
}
