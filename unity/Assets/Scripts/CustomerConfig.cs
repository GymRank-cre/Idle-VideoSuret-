using UnityEngine;

// Réglages des clients, de la file d'attente et du paiement. Rien n'est en dur dans les scripts.
[CreateAssetMenu(fileName = "CustomerConfig", menuName = "Idle/Customer Config")]
public class CustomerConfig : ScriptableObject
{
    [Header("Arrivée")]
    [SerializeField, Min(0.5f)] float spawnInterval = 8f;   // secondes entre deux clients
    [SerializeField, Min(1)] int maxWaiting = 3;            // clients présents dans la file (y compris ceux qui arrivent)

    [Header("Déplacement")]
    [SerializeField, Min(0.1f)] float walkSpeed = 1.8f;     // m/s
    [SerializeField, Min(0.5f)] float queueSpacing = 1f;    // mètres entre deux places de la file
    [SerializeField, Min(0f)] float exitPause = 0.6f;       // pause de joie après le paiement, avant de partir

    [Header("Commande")]
    [SerializeField, Min(1)] int minQuantity = 1;
    [SerializeField, Min(1)] int maxQuantity = 2;

    [Header("Premier client (le joueur apprend)")]
    [SerializeField] bool forceFirstOrder = true;               // le tout premier client commande toujours le même produit
    [SerializeField, Min(0)] int firstOrderProductIndex = 0;    // 0 = caméra dôme (1er produit de la liste du poste)
    [SerializeField, Min(1)] int firstOrderQuantity = 1;

    [Header("Livraison et paiement")]
    [SerializeField, Min(0.05f)] float deliveryInterval = 0.25f; // délai entre deux produits livrés
    [SerializeField, Min(0.05f)] float deliveryFlight = 0.25f;   // durée du vol d'un produit vers le client
    [SerializeField, Min(1)] int maxBillValue = 10;              // valeur maximale d'un billet (le prix est découpé en billets)

    public bool ForceFirstOrder => forceFirstOrder;
    public int FirstOrderProductIndex => firstOrderProductIndex;
    public int FirstOrderQuantity => firstOrderQuantity;

    public float SpawnInterval => spawnInterval;
    public int MaxWaiting => maxWaiting;
    public float WalkSpeed => walkSpeed;
    public float QueueSpacing => queueSpacing;
    public float ExitPause => exitPause;
    public int MinQuantity => minQuantity;
    public int MaxQuantity => Mathf.Max(minQuantity, maxQuantity);
    public float DeliveryInterval => deliveryInterval;
    public float DeliveryFlight => deliveryFlight;
    public int MaxBillValue => maxBillValue;
}
