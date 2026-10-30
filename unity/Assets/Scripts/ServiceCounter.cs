using System.Collections.Generic;
using UnityEngine;

// Comptoir de service : quand le joueur est dans la zone et porte le produit commandé par le client en tête,
// les produits volent vers lui un par un. Commande complète : des billets apparaissent sur le comptoir
// (valeur du produit × quantité), ramassés ensuite par le BillCollector du joueur.
[RequireComponent(typeof(Collider))]
public class ServiceCounter : MonoBehaviour
{
    struct Delivery
    {
        public Product product;
        public Customer customer;
        public Vector3 from;
        public float elapsed;
        public bool employee;
        public int stage;
    }

    [SerializeField] CustomerConfig config;
    [SerializeField] CustomerQueue queue;
    [SerializeField] GameObject billPrefab;
    [SerializeField] Transform billPoint;        // sur le comptoir, du côté du joueur
    [SerializeField] float billScatter = 0.55f;
    [SerializeField] float targetHeight = 1.1f;  // hauteur visée sur le client
    [SerializeField] UpgradeState upgrades;      // facultatif : amélioration du paiement
    [SerializeField] FeedbackSpawner feedback;   // facultatif
    [SerializeField] MachineConfig[] stageMachines; // [0] = Test, [1] = Emballage : leur multiplicateur de valeur

    readonly List<Delivery> deliveries = new List<Delivery>();
    ObjectPool billPool;
    float timer;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Awake()
    {
        billPool = new ObjectPool(billPrefab, new GameObject("Counter Bills").transform);
    }

    void OnTriggerStay(Collider other)
    {
        ProductCarrier carrier = other.GetComponent<ProductCarrier>();
        if (carrier == null) return;

        Customer head = queue.Head;
        if (head == null) return;

        // produits déjà en vol vers ce client
        int inFlight = 0;
        foreach (Delivery d in deliveries) if (d.customer == head) inFlight++;
        if (head.Remaining - inFlight <= 0) return;

        timer += Time.deltaTime;
        if (timer < config.DeliveryInterval) return;

        Product product = carrier.TryTake(head.OrderType);
        if (product == null) return; // le joueur ne porte pas le bon produit
        timer = 0f;

        deliveries.Add(new Delivery { product = product, customer = head, stage = product.Stage, from = product.transform.position,
            employee = other.GetComponent<PlayerController>() == null });
    }

    void Update()
    {
        for (int i = deliveries.Count - 1; i >= 0; i--)
        {
            Delivery d = deliveries[i];
            d.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(d.elapsed / config.DeliveryFlight);

            Vector3 target = d.customer.transform.position + Vector3.up * targetHeight;
            Vector3 pos = Vector3.Lerp(d.from, target, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * 0.4f;
            d.product.transform.position = pos;

            if (t < 1f)
            {
                deliveries[i] = d;
                continue;
            }

            deliveries.RemoveAt(i);
            d.product.Recycle();
            d.customer.Receive(StageFactor(d.stage));
            if (feedback != null) feedback.CrateArrived(target);

            if (d.customer.IsComplete && !HasDeliveryFor(d.customer))
                CompleteOrder(d.customer, d.employee);
        }
    }

    // Valeur d'un produit selon son stade : x1 assemblé, puis le multiplicateur de la machine qui l'a traité.
    public float StageFactor(int stage)
    {
        if (stage <= 0 || stageMachines == null || stage > stageMachines.Length || stageMachines[stage - 1] == null) return 1f;
        return stageMachines[stage - 1].ValueMultiplier;
    }

    bool HasDeliveryFor(Customer c)
    {
        foreach (Delivery d in deliveries) if (d.customer == c) return true;
        return false;
    }

    void CompleteOrder(Customer customer, bool byEmployee = false)
    {
        int total = Mathf.RoundToInt(customer.OrderType.Value * customer.Quantity * customer.AverageValueFactor * (upgrades != null ? upgrades.CustomerPay : 1f) * Bonuses.Revenue);
        SpawnBills(total);
        AudioManager.PlayAt(SfxId.OrderPaid, customer.transform.position, byEmployee);
        if (feedback != null) feedback.ProductReady(customer.transform.position + Vector3.up * 1.9f); // pop de bonheur
        queue.Pay(customer);
    }

    // Le prix est découpé en billets d'au plus `maxBillValue`.
    void SpawnBills(int total)
    {
        while (total > 0)
        {
            int piece = Mathf.Min(config.MaxBillValue, total);
            total -= piece;

            Vector2 offset = Random.insideUnitCircle * billScatter;
            Vector3 pos = billPoint.position + new Vector3(offset.x * 1.6f, 0f, offset.y * 0.5f);
            GameObject go = billPool.Get(pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            go.GetComponent<Bill>().Init(piece, billPool.Release);
        }
    }
}
