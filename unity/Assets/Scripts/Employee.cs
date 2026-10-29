using UnityEngine;
using UnityEngine.AI;

// Employé automatique : répète seul le cycle tas de cartons -> poste. Il n'a rien de nouveau côté zones :
// ResourcePile et InstallStation détectent déjà un StackCarrier dans leur trigger, comme pour le joueur.
// Déplacement par NavMeshAgent. Aucun accès aux billets ni aux produits (le joueur garde clients et livraison).
[RequireComponent(typeof(NavMeshAgent), typeof(StackCarrier))]
public class Employee : MonoBehaviour
{
    enum State { GoToPile, FillUp, GoToStation, Deposit }

    [SerializeField] EmployeeConfig config;
    [SerializeField] UpgradeState upgrades;     // facultatif : amélioration de vitesse des employés
    [SerializeField] Transform pileStand;       // où il se tient près du tas (dans la zone de ramassage)
    [SerializeField] Transform pileLookAt;
    [SerializeField] Transform stationStand;    // où il se tient au poste, de côté pour laisser le centre au joueur
    [SerializeField] Transform stationLookAt;
    [SerializeField] float arriveTolerance = 0.25f;
    [SerializeField] float turnSpeed = 360f;
    [SerializeField] float stuckTimeout = 12f;  // sécurité : on recalcule le trajet si l'arrivée tarde trop

    NavMeshAgent agent;
    StackCarrier carrier;
    State state;
    float timer;
    float travelTime;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        carrier = GetComponent<StackCarrier>();
    }

    void OnEnable()
    {
        agent.speed = Speed;

        // L'agent doit être posé sur le NavMesh avant de recevoir une destination.
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            agent.Warp(hit.position);

        Begin(State.GoToPile);
    }

    void Begin(State next)
    {
        state = next;
        timer = 0f;
        travelTime = 0f;
        switch (next)
        {
            case State.GoToPile: MoveTo(pileStand); break;
            case State.GoToStation: MoveTo(stationStand); break;
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

    float Speed => config.MoveSpeed * (upgrades != null ? upgrades.EmployeeSpeed : 1f);

    void Update()
    {
        agent.speed = Speed; // relu à chaque image : une amélioration achetée agit tout de suite
        switch (state)
        {
            case State.GoToPile:
                if (Arrived()) Begin2(State.FillUp);
                else CheckStuck(pileStand);
                break;

            case State.FillUp:
                Face(pileLookAt);
                // Il attend d'être plein, puis marque une courte pause avant de partir.
                if (carrier.IsFull)
                {
                    timer += Time.deltaTime;
                    if (timer >= config.RestDuration) Begin(State.GoToStation);
                }
                break;

            case State.GoToStation:
                if (Arrived()) Begin2(State.Deposit);
                else CheckStuck(stationStand);
                break;

            case State.Deposit:
                Face(stationLookAt);
                // Poste plein : il reste là (à côté) jusqu'à avoir tout déposé, puis repart au tas.
                if (carrier.IsEmpty)
                {
                    timer += Time.deltaTime;
                    if (timer >= config.RestDuration) Begin(State.GoToPile);
                }
                else
                {
                    timer = 0f;
                }
                break;
        }
    }

    void Begin2(State next)
    {
        state = next;
        timer = 0f;
        travelTime = 0f;
    }

    void CheckStuck(Transform target)
    {
        travelTime += Time.deltaTime;
        if (travelTime < stuckTimeout) return;
        travelTime = 0f;
        MoveTo(target);
    }

    // À l'arrêt, il se tourne vers le tas ou le poste.
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
