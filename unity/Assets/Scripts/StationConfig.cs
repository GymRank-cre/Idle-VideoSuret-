using UnityEngine;

// Réglages d'un poste d'installation (économie et vitesses).
[CreateAssetMenu(fileName = "StationConfig", menuName = "Idle/Station Config")]
public class StationConfig : ScriptableObject
{
    [SerializeField, Min(1)] int cratesPerJob = 3;        // caisses consommées par travail
    [SerializeField, Min(0.1f)] float jobDuration = 2f;   // secondes par travail
    [SerializeField, Min(0)] int billsPerJob = 5;         // billets produits par travail
    [SerializeField, Min(0)] int billValue = 10;          // valeur d'un billet (utilisée à l'étape 3)
    [SerializeField, Min(0.01f)] float depositInterval = 0.15f; // délai entre deux caisses déposées
    [SerializeField, Min(1)] int inputCapacity = 6;       // caisses stockées au poste

    public int CratesPerJob => cratesPerJob;
    public float JobDuration => jobDuration;
    public int BillsPerJob => billsPerJob;
    public int BillValue => billValue;
    public float DepositInterval => depositInterval;
    public int InputCapacity => inputCapacity;
}
