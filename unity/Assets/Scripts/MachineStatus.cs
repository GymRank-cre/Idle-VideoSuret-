// État lisible d'une machine : voyant (StockLabel) et, plus tard, calcul hors ligne.
public enum MachineState
{
    Running,       // elle travaille
    WaitingInput,  // pas assez d'objets en entrée
    Blocked        // sortie pleine : c'est le goulot
}

public interface IMachineStatus
{
    int InputCount { get; }
    int InputCapacity { get; }
    int OutputCount { get; }
    int OutputCapacity { get; }
    MachineState State { get; }
}
