using System.Collections.Generic;
using UnityEngine;

// Machine de la chaîne qui transforme un produit (Test : assemblé -> testé, Emballage : testé -> emballé).
// Le joueur y dépose les produits de stade `inputStage` en restant dans sa zone ; la ProductionMachine fait le cycle ;
// le produit de stade suivant apparaît sur le plateau de sortie, d'où le joueur (ou le livreur) le ramasse.
// Rien n'est perdu : plateau plein = la machine s'arrête (goulot visible par le voyant).
[RequireComponent(typeof(Collider))]
public class ProcessStation : MonoBehaviour, IProductSource
{
    struct Flight
    {
        public Product product;
        public Vector3 from;
        public float elapsed;
    }

    [SerializeField] ProductionMachine machine;
    [SerializeField, Range(0, 1)] int inputStage;       // 0 = Test, 1 = Emballage
    [SerializeField] Transform outputTray;               // places alignées sur son axe x
    [SerializeField] FeedbackSpawner feedback;           // facultatif
    [SerializeField, Min(1)] int traySlots = 4;
    [SerializeField] float depositInterval = 0.2f;
    [SerializeField] float flightDuration = 0.25f;
    [SerializeField] float trayHeight = 0.06f;

    readonly List<Flight> flights = new List<Flight>();
    readonly Queue<ProductType> pending = new Queue<ProductType>(); // types en cours de traitement, dans l'ordre
    readonly Dictionary<ProductType, ObjectPool> pools = new Dictionary<ProductType, ObjectPool>();
    Product[] slots;
    Transform holder;
    float timer;

    public int InputStage => inputStage;
    public int ProductsOnTray
    {
        get
        {
            int n = 0;
            if (slots != null) for (int i = 0; i < slots.Length; i++) if (slots[i] != null) n++;
            return n;
        }
    }

    // Places libres en entrée (déposées par le joueur en vol comprises).
    public int FreeInputSlots => machine.InputCapacity - machine.InputCount - flights.Count;

    // Un tapis y dépose un produit ; faux si la machine est pleine (le produit attend sur le tapis).
    public bool TryAcceptFromBelt(Product product)
    {
        if (FreeInputSlots <= 0 || !machine.TryAddInput()) return false;
        pending.Enqueue(product.Type);
        AudioManager.PlayAt(SfxId.Deposit, transform.position, false);
        product.Recycle();
        return true;
    }

    // Un tapis prend un produit du plateau de sortie.
    public Product TakeProduct()
    {
        if (slots == null) return null;
        for (int i = 0; i < slots.Length; i++)
        {
            Product p = slots[i];
            if (p == null || p.IsCollecting) continue;
            p.BeginCollect();
            return p;
        }
        return null;
    }

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void Awake()
    {
        slots = new Product[traySlots];
        holder = new GameObject(name + " Products").transform;
    }

    void OnTriggerStay(Collider other)
    {
        ProductCarrier carrier = other.GetComponent<ProductCarrier>();
        if (carrier == null) return;
        if (machine.InputCount + flights.Count >= machine.InputCapacity) return;

        timer += Time.deltaTime;
        if (timer < depositInterval) return;

        Product product = carrier.TryTakeStage(inputStage);
        if (product == null) return;
        timer = 0f;
        flights.Add(new Flight { product = product, from = product.transform.position });
    }

    void Update()
    {
        UpdateFlights();
        ReleaseOutput();
    }

    void UpdateFlights()
    {
        Vector3 target = transform.position + Vector3.up * 1.1f;
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            Flight f = flights[i];
            f.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(f.elapsed / flightDuration);
            Vector3 pos = Vector3.Lerp(f.from, target, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * 0.5f;
            f.product.transform.position = pos;

            if (t < 1f) { flights[i] = f; continue; }

            flights.RemoveAt(i);
            if (machine.TryAddInput())
            {
                pending.Enqueue(f.product.Type);
                AudioManager.PlayAt(SfxId.Deposit, transform.position, false);
                f.product.Recycle();
            }
            else
            {
                f.product.Recycle(); // ne devrait pas arriver (place réservée au départ) ; pas de fuite d'objet
            }
        }
    }

    // Sortie de la machine -> plateau, tant qu'il y a une place.
    void ReleaseOutput()
    {
        while (machine.OutputCount > 0 && pending.Count > 0)
        {
            int slot = FreeSlot();
            if (slot < 0) return;
            if (!machine.TryTakeOutput()) return;
            Spawn(pending.Dequeue(), slot);
        }
    }

    int FreeSlot()
    {
        for (int i = 0; i < slots.Length; i++) if (slots[i] == null) return i;
        return -1;
    }

    void Spawn(ProductType type, int slot)
    {
        if (type == null || type.Prefab == null) return;
        if (!pools.TryGetValue(type, out ObjectPool pool))
        {
            pool = new ObjectPool(type.Prefab, holder);
            pools[type] = pool;
        }

        float x = (slot - (slots.Length - 1) * 0.5f) * 0.42f;
        Vector3 pos = outputTray.TransformPoint(new Vector3(x, trayHeight, 0f));
        GameObject go = pool.Get(pos, Quaternion.Euler(0f, 180f, 0f));
        Product product = go.GetComponent<Product>();
        product.Init(type, pool.Release, OnTaken, inputStage + 1);
        slots[slot] = product;
        if (feedback != null) feedback.ProductReady(pos);
        AudioManager.PlayAt(SfxId.ProductReady, pos, false);
    }

    void OnTaken(Product product)
    {
        for (int i = 0; i < slots.Length; i++) if (slots[i] == product) slots[i] = null;
    }
}
