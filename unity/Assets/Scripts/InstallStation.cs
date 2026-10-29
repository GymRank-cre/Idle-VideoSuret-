using System.Collections.Generic;
using UnityEngine;

// Poste d'installation : tant que le joueur est dans la zone, il y dépose ses
// caisses une à une. Quand il y en a assez, un travail démarre (barre de
// progression), puis des billets apparaissent au point de sortie.
[RequireComponent(typeof(Collider))]
public class InstallStation : MonoBehaviour, IMachineStatus, IProductSource
{
    [SerializeField] StationConfig config;
    [SerializeField] ResourcePile cratesSource; // son pool de caisses est partagé avec le poste
    [SerializeField] GameObject billPrefab;
    [SerializeField] FeedbackSpawner feedback; // facultatif : éclat quand une caisse arrive
    [SerializeField] UpgradeState upgrades;    // facultatif : amélioration de la vitesse de fabrication
    [SerializeField] ProductDemand demand;     // facultatif : fabrique d'abord ce que demandent les clients
    [SerializeField] ProgressBar3D progressBar;
    [SerializeField] Transform billExit;     // point autour duquel les billets tombent
    [SerializeField] Transform outputTray;   // plateau de sortie des produits finis (places alignées sur son axe x)
    [SerializeField] float traySurface = 0.06f; // hauteur de la surface du plateau, dans son repère
    [SerializeField] float flightDuration = 0.25f;
    [SerializeField] float flightArc = 0.8f;
    [SerializeField] float billScatterRadius = 1f;

    struct Flight
    {
        public Transform item;
        public Vector3 from;
        public float elapsed;
        public bool employee;
    }

    readonly List<Flight> flights = new List<Flight>();
    ObjectPool cratePool;
    ObjectPool billPool;
    readonly Dictionary<ProductType, ObjectPool> productPools = new Dictionary<ProductType, ObjectPool>();
    Product[] slots;
    int productCursor;
    Transform productsHolder;

    int storedCrates;
    bool working;
    float jobTimer;
    float depositTimer;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Awake()
    {
        cratePool = cratesSource.Pool;
        billPool = new ObjectPool(billPrefab, new GameObject(name + " Bills").transform);
    }

    Vector3 CrateTarget => transform.position + Vector3.up * 0.4f;

    void OnTriggerStay(Collider other)
    {
        StackCarrier carrier = other.GetComponent<StackCarrier>();
        if (carrier == null || carrier.IsEmpty) return;
        if (storedCrates + flights.Count >= config.InputCapacity) return;

        depositTimer += Time.deltaTime;
        if (depositTimer < config.DepositInterval) return;
        depositTimer = 0f;

        Transform taken = carrier.RemoveLast();
        if (taken == null) return;

        // La caisse reprise est recyclée dans le pool, puis réutilisée pour le vol.
        Vector3 from = taken.position;
        Quaternion rot = taken.rotation;
        cratePool.Release(taken.gameObject);
        GameObject flying = cratePool.Get(from, rot);
        flights.Add(new Flight { item = flying.transform, from = from, employee = !carrier.MakesSound });
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<StackCarrier>() != null)
            depositTimer = 0f;
    }

    void Update()
    {
        UpdateFlights();
        UpdateJob();
    }

    void UpdateFlights()
    {
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            Flight f = flights[i];
            f.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(f.elapsed / flightDuration);

            Vector3 pos = Vector3.Lerp(f.from, CrateTarget, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * flightArc;
            f.item.position = pos;

            if (t >= 1f)
            {
                cratePool.Release(f.item.gameObject);
                flights.RemoveAt(i);
                storedCrates++;
                if (feedback != null) feedback.CrateArrived(CrateTarget);
                AudioManager.PlayAt(SfxId.Deposit, transform.position, f.employee);
            }
            else
            {
                flights[i] = f;
            }
        }
    }

    float JobDuration => config.JobDuration * (upgrades != null ? upgrades.JobDuration : 1f);

    void UpdateJob()
    {
        if (!working && storedCrates >= config.CratesPerJob)
        {
            storedCrates -= config.CratesPerJob;
            working = true;
            jobTimer = 0f;
        }

        if (!working) return;

        jobTimer += Time.deltaTime;
        if (progressBar != null)
            progressBar.SetProgress(jobTimer / JobDuration);

        if (jobTimer >= JobDuration)
        {
            // Plateau plein : le travail attend (barre pleine) qu'une place se libère, rien n'est perdu.
            if (MakesProducts && FreeSlotCount() < config.ProductsPerJob)
            {
                jobTimer = JobDuration;
                if (progressBar != null) progressBar.SetProgress(1f);
                return;
            }

            working = false;
            if (progressBar != null) progressBar.SetProgress(0f);
            // Les billets viennent des clients quand le poste fabrique des produits (repli : produceProducts faux).
            if (!MakesProducts) SpawnBills();
            SpawnProducts();
        }
    }

    // Un tapis prend un produit du plateau.
    public Product TakeProduct()
    {
        if (slots == null) return null;
        for (int i = 0; i < slots.Length; i++)
        {
            Product p = slots[i];
            if (p == null || p.IsCollecting) continue;
            p.BeginCollect(); // libère la place via OnProductTaken
            return p;
        }
        return null;
    }

    // État pour l'étiquette de stock et le voyant (IMachineStatus).
    public int InputCount => storedCrates;
    public int InputCapacity => config.InputCapacity;
    public int OutputCount => MakesProducts ? ProductsOnTray : 0;
    public int OutputCapacity => MakesProducts ? config.OutputCapacity : 0;
    public MachineState State
    {
        get
        {
            if (working) return jobTimer >= JobDuration ? MachineState.Blocked : MachineState.Running;
            return storedCrates >= config.CratesPerJob ? MachineState.Running : MachineState.WaitingInput;
        }
    }

    // Produits actuellement posés sur le plateau de sortie (le livreur s'en sert pour savoir s'il en reste).
    public int ProductsOnTray
    {
        get
        {
            if (slots == null) return 0;
            int n = 0;
            for (int i = 0; i < slots.Length; i++) if (slots[i] != null) n++;
            return n;
        }
    }

    bool MakesProducts => config.ProduceProducts && outputTray != null
        && config.Products != null && config.Products.Length > 0;

    void EnsureSlots()
    {
        if (slots == null || slots.Length != config.OutputCapacity)
            slots = new Product[config.OutputCapacity];
    }

    int FreeSlotCount()
    {
        EnsureSlots();
        int n = 0;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == null) n++;
        return n;
    }

    // Produits finis : un par travail (ou plus), posés sur la première place libre du plateau.
    void SpawnProducts()
    {
        if (!MakesProducts) return;
        ProductType[] types = config.Products;

        EnsureSlots();
        if (productsHolder == null)
            productsHolder = new GameObject(name + " Products").transform;

        for (int n = 0; n < config.ProductsPerJob; n++)
        {
            int slot = FreeSlot();
            if (slot < 0) return;

            ProductType type = demand != null ? demand.Pick() : null;
            if (type == null) // personne n'attend un produit manquant : rotation
            {
                type = types[productCursor % types.Length];
                productCursor++;
            }
            if (type == null || type.Prefab == null) continue;

            if (!productPools.TryGetValue(type, out ObjectPool pool))
            {
                pool = new ObjectPool(type.Prefab, productsHolder);
                productPools[type] = pool;
            }

            Vector3 pos = SlotPosition(slot);
            GameObject go = pool.Get(pos, Quaternion.Euler(0f, 180f, 0f)); // face à la caméra
            Product product = go.GetComponent<Product>();
            product.Init(type, pool.Release, OnProductTaken);
            slots[slot] = product;
            if (feedback != null) feedback.ProductReady(pos);
            AudioManager.PlayAt(SfxId.ProductReady, pos, false);
        }
    }

    int FreeSlot()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == null) return i;
        return -1;
    }

    Vector3 SlotPosition(int index)
    {
        float x = (index - (slots.Length - 1) * 0.5f) * 0.42f;
        return outputTray.TransformPoint(new Vector3(x, traySurface, 0f));
    }

    void OnProductTaken(Product product)
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == product) slots[i] = null;
    }

    void SpawnBills()
    {
        Vector3 center = billExit != null ? billExit.position : transform.position;
        for (int i = 0; i < config.BillsPerJob; i++)
        {
            Vector2 offset = Random.insideUnitCircle * billScatterRadius;
            Vector3 pos = center + new Vector3(offset.x, 0f, offset.y);
            Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject go = billPool.Get(pos, rot);
            go.GetComponent<Bill>().Init(config.BillValue, billPool.Release);
        }
    }
}
