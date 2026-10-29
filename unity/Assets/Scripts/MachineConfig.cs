using UnityEngine;

// Réglages d'une machine de la chaîne de production (11a) : tout ce qui touche à l'économie est ici, jamais dans le code.
[CreateAssetMenu(fileName = "MachineConfig", menuName = "Idle/Machine Config")]
public class MachineConfig : ScriptableObject
{
    [SerializeField] string displayName = "Machine";
    [SerializeField, Min(1)] int inputPerCycle = 1;     // objets consommés à chaque cycle
    [SerializeField, Min(1)] int outputPerCycle = 1;    // objets produits à chaque cycle
    [SerializeField, Min(0.1f)] float cycleSeconds = 2f;
    [SerializeField, Min(1)] int inputCapacity = 10;    // stock d'entrée (plein : le maillon précédent attend)
    [SerializeField, Min(1)] int outputCapacity = 10;   // stock de sortie (plein : la machine s'arrête = goulot)
    [SerializeField, Min(0.1f)] float valueMultiplier = 1f; // valeur d'un objet sorti, par rapport à la caisse (x1)

    public string DisplayName => displayName;
    public int InputPerCycle => inputPerCycle;
    public int OutputPerCycle => outputPerCycle;
    public float CycleSeconds => cycleSeconds;
    public int InputCapacity => inputCapacity;
    public int OutputCapacity => outputCapacity;
    public float ValueMultiplier => valueMultiplier;

    // Objets sortis par seconde quand la machine tourne sans interruption (sert au calcul hors ligne).
    public float Throughput => outputPerCycle / cycleSeconds;
}
