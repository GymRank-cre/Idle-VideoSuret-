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

    [Header("Produits finis (étape 5a)")]
    [SerializeField] bool produceProducts = true;        // faux : le poste ne fabrique que des billets (comme avant)
    [SerializeField, Min(1)] int productsPerJob = 1;     // produits posés sur le plateau à chaque travail
    [SerializeField] ProductType[] products;             // produits fabriqués, à tour de rôle
    [SerializeField, Min(1)] int outputCapacity = 4;     // places du plateau de sortie

    public bool ProduceProducts => produceProducts;
    public int ProductsPerJob => productsPerJob;
    public ProductType[] Products => products;
    public int OutputCapacity => outputCapacity;

    public int CratesPerJob => cratesPerJob;
    public float JobDuration => jobDuration;
    public int BillsPerJob => billsPerJob;
    public int BillValue => billValue;
    public float DepositInterval => depositInterval;
    public int InputCapacity => inputCapacity;
}
