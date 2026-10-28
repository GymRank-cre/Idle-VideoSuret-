using System.Collections.Generic;
using UnityEngine;

// Fait arriver les clients par la porte, les range en file devant le comptoir (points de passage, pas de NavMesh),
// puis les fait sortir après paiement. Le client en tête de file porte la bulle de commande.
public class CustomerQueue : MonoBehaviour
{
    struct Leaving
    {
        public Customer customer;
        public float leaveAt;
    }

    [SerializeField] CustomerConfig config;
    [SerializeField] GameObject customerPrefab;
    [SerializeField] StationConfig products;   // liste des produits que les clients peuvent commander
    [SerializeField] int poolSize = 6;
    [SerializeField] Transform spawnPoint;     // juste à l'intérieur de la porte
    [SerializeField] Transform laneCorner;     // coin du couloir d'arrivée, aligné avec la file
    [SerializeField] Transform queueHead;      // place du client en tête de file
    [SerializeField] Vector3 queueDirection = new Vector3(0f, 0f, -1f); // la file s'allonge dans cette direction
    [SerializeField] Transform[] exitPoints;   // chemin de sortie jusqu'à la porte (dernier point : hors du mur)

    readonly List<Customer> line = new List<Customer>();
    readonly List<Leaving> leaving = new List<Leaving>();
    ObjectPool pool;
    float timer;

    public int Waiting => line.Count;

    // Le client en tête, arrivé à sa place et pas encore servi ; sinon null.
    public Customer Head
    {
        get
        {
            if (line.Count == 0) return null;
            Customer c = line[0];
            return !c.IsWalking && !c.IsComplete ? c : null;
        }
    }

    void Awake()
    {
        pool = new ObjectPool(customerPrefab, new GameObject("Customers Pool").transform);
        var made = new List<GameObject>();
        for (int i = 0; i < poolSize; i++)
        {
            GameObject go = pool.Get(spawnPoint.position, Quaternion.identity);
            go.GetComponent<Customer>().Bubble.Setup(Types());
            made.Add(go);
        }
        foreach (GameObject go in made) pool.Release(go); // 6 clients prêts, inactifs
        timer = config.SpawnInterval * 0.5f; // le premier client arrive vite
    }

    ProductType[] Types()
    {
        return products.Products;
    }

    void Update()
    {
        UpdateLeaving();

        timer += Time.deltaTime;
        if (timer >= config.SpawnInterval)
        {
            timer = 0f;
            if (line.Count < config.MaxWaiting) Spawn();
        }
    }

    Vector3 SlotPosition(int index)
    {
        return queueHead.position + queueDirection.normalized * (config.QueueSpacing * index);
    }

    void Spawn()
    {
        ProductType[] types = Types();
        if (types == null || types.Length == 0) return;

        GameObject go = pool.Get(spawnPoint.position, Quaternion.LookRotation(Vector3.left));
        Customer c = go.GetComponent<Customer>();
        c.StartOrder(types[Random.Range(0, types.Length)], Random.Range(config.MinQuantity, config.MaxQuantity + 1));

        int slot = line.Count;
        line.Add(c);
        var route = new List<Vector3> { laneCorner.position, SlotPosition(slot) };
        c.WalkPath(route, config.WalkSpeed, Vector3.forward, OnArrivedAtSlot);
    }

    void OnArrivedAtSlot(Customer c)
    {
        if (line.Count > 0 && line[0] == c && !c.IsComplete) c.ShowOrder();
    }

    // Commande livrée en entier : le client se réjouit, puis quitte la file.
    public void Pay(Customer customer)
    {
        leaving.Add(new Leaving { customer = customer, leaveAt = Time.time + config.ExitPause });
    }

    void UpdateLeaving()
    {
        for (int i = leaving.Count - 1; i >= 0; i--)
        {
            if (Time.time < leaving[i].leaveAt) continue;
            Customer c = leaving[i].customer;
            leaving.RemoveAt(i);
            Leave(c);
        }
    }

    void Leave(Customer c)
    {
        line.Remove(c);
        c.HideOrder();

        var route = new List<Vector3>();
        foreach (Transform t in exitPoints) route.Add(t.position);
        c.WalkPath(route, config.WalkSpeed, Vector3.right, done => pool.Release(done.gameObject));

        // Les clients derrière avancent d'une place.
        for (int i = 0; i < line.Count; i++)
            line[i].ReplaceFinalTarget(SlotPosition(i), config.WalkSpeed, Vector3.forward, OnArrivedAtSlot);
    }
}
