using System;
using System.Collections.Generic;
using UnityEngine;

// Sur le joueur : ramasse automatiquement les produits finis proches et les porte à la main,
// à plat sur un petit plateau devant le torse (2 x 2), distinct de la pile de caisses dans le dos.
public class ProductCarrier : MonoBehaviour
{
    [SerializeField] Transform carryAnchor;      // devant le torse ; les places sont posées autour de ce point
    [SerializeField] GameObject trayVisual;      // petit plateau, visible seulement quand on porte quelque chose
    [SerializeField, Min(1)] int capacity = 4;
    [SerializeField] UpgradeState upgrades;      // facultatif : « Capacité des employés » (livreur seulement)
    [SerializeField] float pickupRadius = 1.4f;
    [SerializeField] float flightDuration = 0.2f;
    [SerializeField] float slotSpacingX = 0.3f;  // écart entre les 2 colonnes
    [SerializeField] float slotSpacingZ = 0.26f; // écart entre les 2 rangées
    [SerializeField] float settleSpeed = 14f;    // vitesse à laquelle un produit rejoint sa place

    struct Flight
    {
        public Product product;
        public Vector3 from;
        public float elapsed;
    }

    readonly List<Product> carried = new List<Product>();
    readonly List<Flight> flights = new List<Flight>();
    readonly Collider[] hits = new Collider[32];

    public int Count => carried.Count;
    public int Capacity => capacity + (upgrades != null ? upgrades.EmployeeExtraCapacity : 0);
    public bool IsFull => carried.Count + flights.Count >= Capacity;
    public event Action OnCountChanged;

    void Start()
    {
        UpdateTray();
    }

    void Update()
    {
        if (!IsFull) Scan();
        UpdateFlights();
        SettleCarried();
        UpdateTray();
    }

    // Place d'un produit sur le plateau : 2 colonnes x 2 rangées, à plat (un seul niveau).
    Vector3 SlotLocal(int index)
    {
        float x = (index % 2 - 0.5f) * slotSpacingX;
        float z = (0.5f - (index / 2) % 2) * slotSpacingZ;
        return new Vector3(x, 0f, z);
    }

    void UpdateTray()
    {
        if (trayVisual != null)
            trayVisual.SetActive(carried.Count + flights.Count > 0);
    }

    void Scan()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, pickupRadius, hits, ~0, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count && !IsFull; i++)
        {
            Product product = hits[i].GetComponent<Product>();
            if (product == null || product.IsCollecting) continue;

            product.BeginCollect();
            flights.Add(new Flight { product = product, from = product.transform.position });
        }
    }

    void UpdateFlights()
    {
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            Flight f = flights[i];
            f.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(f.elapsed / flightDuration);

            Vector3 target = carryAnchor.TransformPoint(SlotLocal(carried.Count + i));
            Vector3 pos = Vector3.Lerp(f.from, target, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * 0.3f;
            f.product.transform.position = pos;

            if (t >= 1f)
            {
                Transform pt = f.product.transform;
                pt.SetParent(carryAnchor, false);
                pt.localPosition = SlotLocal(carried.Count);
                pt.localRotation = Quaternion.identity;
                carried.Add(f.product);
                flights.RemoveAt(i);
                OnCountChanged?.Invoke();
            }
            else
            {
                flights[i] = f;
            }
        }
    }

    // Quand un produit est retiré, les autres se serrent vers les premières places.
    void SettleCarried()
    {
        float k = 1f - Mathf.Exp(-settleSpeed * Time.deltaTime);
        for (int i = 0; i < carried.Count; i++)
        {
            Transform t = carried[i].transform;
            t.localPosition = Vector3.Lerp(t.localPosition, SlotLocal(i), k);
        }
    }

    public bool Has(ProductType type)
    {
        foreach (Product p in carried)
            if (p.Type == type) return true;
        return false;
    }

    public bool HasTypeStage(ProductType type, int stage)
    {
        foreach (Product p in carried) if (p.Type == type && p.Stage == stage) return true;
        return false;
    }

    // Retire un produit d'un type et d'un stade précis (quai d'expédition) ; null s'il n'y en a pas.
    public Product TryTakeOfType(ProductType type, int stage)
    {
        for (int i = 0; i < carried.Count; i++)
        {
            if (carried[i].Type != type || carried[i].Stage != stage) continue;
            Product p = carried[i];
            carried.RemoveAt(i);
            p.transform.SetParent(null, true);
            OnCountChanged?.Invoke();
            return p;
        }
        return null;
    }

    // Retire un produit du stade demandé (dépôt dans une machine de la chaîne) ; null si le joueur n'en porte pas.
    public Product TryTakeStage(int stage)
    {
        for (int i = 0; i < carried.Count; i++)
        {
            if (carried[i].Stage != stage) continue;
            Product p = carried[i];
            carried.RemoveAt(i);
            p.transform.SetParent(null, true);
            OnCountChanged?.Invoke();
            return p;
        }
        return null;
    }

    // Retire un produit du type demandé (livraison à un client), le plus avancé dans la chaîne d'abord ;
    // renvoie null si le joueur n'en porte pas.
    public Product TryTake(ProductType type)
    {
        int best = -1;
        for (int i = 0; i < carried.Count; i++)
        {
            if (carried[i].Type != type) continue;
            if (best < 0 || carried[i].Stage > carried[best].Stage) best = i;
        }

        if (best >= 0)
        {
            int i = best;
            Product p = carried[i];
            carried.RemoveAt(i);
            p.transform.SetParent(null, true);
            OnCountChanged?.Invoke();
            return p;
        }
        return null;
    }

    // Rend tous les produits portés à leur pool (corbeille, action de test).
    public void Clear()
    {
        foreach (Flight f in flights) f.product.Recycle();
        flights.Clear();
        foreach (Product p in carried) p.Recycle();
        carried.Clear();
        OnCountChanged?.Invoke();
    }
}
