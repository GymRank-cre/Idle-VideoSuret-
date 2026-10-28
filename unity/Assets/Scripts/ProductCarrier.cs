using System;
using System.Collections.Generic;
using UnityEngine;

// Sur le joueur : ramasse automatiquement les produits finis proches et les porte à la main,
// empilés devant le torse (distinct de la pile de caisses dans le dos).
public class ProductCarrier : MonoBehaviour
{
    [SerializeField] Transform carryAnchor;      // devant le torse ; les produits s'empilent vers le haut
    [SerializeField] GameObject trayVisual;      // petit plateau, visible seulement quand on porte quelque chose
    [SerializeField, Min(1)] int capacity = 4;
    [SerializeField] float pickupRadius = 1.4f;
    [SerializeField] float flightDuration = 0.2f;
    [SerializeField] float itemHeight = 0.22f;   // écart vertical entre deux produits portés

    struct Flight
    {
        public Product product;
        public Vector3 from;
        public int slot;
        public float elapsed;
    }

    readonly List<Product> carried = new List<Product>();
    readonly List<Flight> flights = new List<Flight>();
    readonly Collider[] hits = new Collider[32];

    public int Count => carried.Count;
    public int Capacity => capacity;
    public bool IsFull => carried.Count + flights.Count >= capacity;
    public event Action OnCountChanged;

    void Start()
    {
        UpdateTray();
    }

    void Update()
    {
        if (!IsFull) Scan();
        UpdateFlights();
        UpdateTray();
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
            flights.Add(new Flight
            {
                product = product,
                from = product.transform.position,
                slot = carried.Count + flights.Count
            });
        }
    }

    void UpdateFlights()
    {
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            Flight f = flights[i];
            f.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(f.elapsed / flightDuration);

            Vector3 target = carryAnchor.TransformPoint(Vector3.up * (itemHeight * f.slot));
            Vector3 pos = Vector3.Lerp(f.from, target, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * 0.3f;
            f.product.transform.position = pos;

            if (t >= 1f)
            {
                Transform pt = f.product.transform;
                pt.SetParent(carryAnchor, false);
                pt.localPosition = Vector3.up * (itemHeight * f.slot);
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

    // Les produits portés ne vont nulle part avant l'étape 5b : cette méthode les rend au pool (action de test).
    public void Clear()
    {
        foreach (Flight f in flights) f.product.Recycle();
        flights.Clear();
        foreach (Product p in carried) p.Recycle();
        carried.Clear();
        OnCountChanged?.Invoke();
    }
}
