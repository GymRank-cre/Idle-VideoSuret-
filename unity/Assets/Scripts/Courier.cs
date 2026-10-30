using UnityEngine;
using UnityEngine.AI;

// Employé livreur : va au plateau de sortie du poste, prend les produits (ProductCarrier, comme le joueur),
// les apporte au comptoir (ServiceCounter les prend, comme pour le joueur) et revient.
// Si le client en tête commande un produit qu'il n'a pas, il retourne en chercher ; mains pleines, il vide à la corbeille.
[RequireComponent(typeof(NavMeshAgent), typeof(ProductCarrier))]
public class Courier : MonoBehaviour
{
    enum State { GoToTray, Collect, GoToCounter, Deliver, GoToBin }

    [SerializeField] EmployeeConfig config;
    [SerializeField] UpgradeState upgrades;     // facultatif : amélioration de vitesse des employés
    [SerializeField] InstallStation station;     // pour savoir s'il reste des produits sur le plateau
    [SerializeField] CustomerQueue queue;
    [Header("Commandes (11c) : il préfère le quai quand une commande en cours a besoin de ce qu'il porte")]
    [SerializeField] OrderBoard board;
    [SerializeField] Transform dockStand;
    [SerializeField] Transform dockLook;
    [SerializeField] Transform trayStand;
    [SerializeField] Transform trayLook;
    [Header("Chaîne de production (11a-4) : quand les deux tapis sont achetés, il prend au plateau d'Emballage")]
    [SerializeField] ProcessStation chainEnd;
    [SerializeField] GameObject chainBelt1;
    [SerializeField] GameObject chainBelt2;
    [SerializeField] Transform chainStand;
    [SerializeField] Transform chainLook;
    [SerializeField] Transform counterStand;
    [SerializeField] Transform counterLook;
    [SerializeField] Transform binStand;
    [SerializeField] float arriveTolerance = 0.25f;
    [SerializeField] float turnSpeed = 360f;
    [SerializeField] float mismatchTimeout = 4f; // sans le bon produit pour le client en tête, il repart chercher
    [SerializeField] float collectPatience = 2.5f; // attente d'un produit de plus au plateau avant de partir livrer
    [SerializeField] float stuckTimeout = 12f;

    NavMeshAgent agent;
    ProductCarrier carrier;
    State state;
    float timer;
    float mismatchTimer;
    float travelTime;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        carrier = GetComponent<ProductCarrier>();
    }

    void OnEnable()
    {
        agent.speed = Speed;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            agent.Warp(hit.position);
        Begin(State.GoToTray);
    }

    void Begin(State next)
    {
        state = next;
        timer = 0f;
        mismatchTimer = 0f;
        travelTime = 0f;
        switch (next)
        {
            case State.GoToTray: MoveTo(TrayStand); break;
            case State.GoToCounter: toDock = CarriesWhatOrderNeeds(); MoveTo(toDock ? dockStand : counterStand); break;
            case State.GoToBin: MoveTo(binStand); break;
        }
    }

    void MoveTo(Transform target)
    {
        if (!agent.isOnNavMesh) return;
        Vector3 point = target.position;
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, 3f, NavMesh.AllAreas)) point = hit.position;
        agent.SetDestination(point);
    }

    bool Arrived()
    {
        return agent.isOnNavMesh && !agent.pathPending
            && agent.remainingDistance <= agent.stoppingDistance + arriveTolerance;
    }

    public bool ChainAutomated => chainEnd != null && chainBelt1 != null && chainBelt2 != null
        && chainEnd.gameObject.activeInHierarchy && chainBelt1.activeInHierarchy && chainBelt2.activeInHierarchy;
    Transform TrayStand => ChainAutomated && chainStand != null ? chainStand : trayStand;
    Transform TrayLook => ChainAutomated && chainLook != null ? chainLook : trayLook;
    int ProductsOnTray => ChainAutomated ? chainEnd.ProductsOnTray : station.ProductsOnTray;

    readonly System.Collections.Generic.List<ProductType> neededTypes = new System.Collections.Generic.List<ProductType>();
    bool toDock;

    // Une commande en cours attend un colis (stade 2) que le livreur porte.
    bool CarriesWhatOrderNeeds()
    {
        if (board == null || !board.HasActive || dockStand == null) return false;
        board.GetNeeded(neededTypes);
        foreach (ProductType t in neededTypes)
            if (carrier.HasTypeStage(t, 2)) return true;
        return false;
    }

    float Speed => config.MoveSpeed * (upgrades != null ? upgrades.EmployeeSpeed : 1f);

    void Update()
    {
        agent.speed = Speed; // relu à chaque image : une amélioration achetée agit tout de suite
        switch (state)
        {
            case State.GoToTray:
                if (Arrived()) { state = State.Collect; timer = 0f; }
                else CheckStuck(TrayStand);
                break;

            case State.Collect:
                Face(TrayLook);
                if (carrier.IsFull) { Begin(State.GoToCounter); break; }
                // des produits sont portés et le plateau est vide : après une courte attente, il part livrer
                if (carrier.Count > 0 && ProductsOnTray == 0)
                {
                    timer += Time.deltaTime;
                    if (timer >= collectPatience) Begin(State.GoToCounter);
                }
                else
                {
                    timer = 0f;
                }
                break;

            case State.GoToCounter:
                if (Arrived()) { state = State.Deliver; timer = 0f; mismatchTimer = 0f; }
                else CheckStuck(toDock ? dockStand : counterStand);
                break;

            case State.Deliver:
                if (toDock) // au quai : il attend que la commande ait pris ses colis, puis va au comptoir avec le reste
                {
                    Face(dockLook);
                    timer += Time.deltaTime;
                    if (!CarriesWhatOrderNeeds() && timer > 0.3f) Begin(carrier.Count == 0 ? State.GoToTray : State.GoToCounter);
                    else if (timer > 6f) Begin(State.GoToCounter);
                    break;
                }
                Face(counterLook);
                if (carrier.Count == 0)
                {
                    timer += Time.deltaTime;
                    if (timer >= config.RestDuration) Begin(State.GoToTray);
                    break;
                }
                timer = 0f;
                Customer head = queue.Head;
                if (head != null && !carrier.Has(head.OrderType))
                {
                    mismatchTimer += Time.deltaTime;
                    if (mismatchTimer >= mismatchTimeout)
                        Begin(carrier.IsFull ? State.GoToBin : State.GoToTray);
                }
                else
                {
                    mismatchTimer = 0f;
                }
                break;

            case State.GoToBin:
                if (Arrived())
                {
                    // la corbeille vide les produits portés dès qu'il est dans sa zone
                    if (carrier.Count == 0) Begin(State.GoToTray);
                }
                else
                {
                    CheckStuck(binStand);
                }
                break;
        }
    }

    void CheckStuck(Transform target)
    {
        travelTime += Time.deltaTime;
        if (travelTime < stuckTimeout) return;
        travelTime = 0f;
        MoveTo(target);
    }

    void Face(Transform target)
    {
        if (target == null || agent.velocity.sqrMagnitude > 0.05f) return;
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, Quaternion.LookRotation(dir, Vector3.up), turnSpeed * Time.deltaTime);
    }
}
