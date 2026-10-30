using System.Collections.Generic;
using UnityEngine;

// Quai d'expédition (11c) : le joueur ou le livreur y déposent des colis (stade 2) ; chaque colis du bon type remplit la
// commande en cours (OrderBoard). Les colis qui ne servent à rien restent portés (le comptoir les prendra).
[RequireComponent(typeof(Collider))]
public class ShippingDock : MonoBehaviour
{
    struct Flight
    {
        public Product product;
        public ProductType type;
        public Vector3 from;
        public float elapsed;
    }

    [SerializeField] OrderBoard board;
    [SerializeField] Transform target;      // où volent les colis (la camionnette)
    [SerializeField] FeedbackSpawner feedback;
    [SerializeField] float depositInterval = 0.2f;
    [SerializeField] float flightDuration = 0.3f;

    readonly List<Flight> flights = new List<Flight>();
    readonly List<ProductType> needed = new List<ProductType>();
    float timer;

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    int InFlight(ProductType t)
    {
        int n = 0;
        foreach (Flight f in flights) if (f.type == t) n++;
        return n;
    }

    void OnTriggerStay(Collider other)
    {
        ProductCarrier carrier = other.GetComponent<ProductCarrier>();
        if (carrier == null || !board.HasActive) return;

        timer += Time.deltaTime;
        if (timer < depositInterval) return;

        board.GetNeeded(needed);
        foreach (ProductType t in needed)
        {
            // places restantes de ce type moins les colis déjà en vol
            if (board.Remaining(board.IndexOf(t)) - InFlight(t) <= 0) continue;
            Product p = carrier.TryTakeOfType(t, 2);
            if (p == null) continue;
            timer = 0f;
            flights.Add(new Flight { product = p, type = t, from = p.transform.position });
            return;
        }
    }

    void Update()
    {
        Vector3 end = target != null ? target.position + Vector3.up * 1.2f : transform.position + Vector3.up;
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            Flight f = flights[i];
            f.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(f.elapsed / flightDuration);
            Vector3 pos = Vector3.Lerp(f.from, end, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * 0.8f;
            f.product.transform.position = pos;
            if (t < 1f) { flights[i] = f; continue; }

            flights.RemoveAt(i);
            bool counted = board.Deliver(f.type);
            AudioManager.PlayAt(counted ? SfxId.OrderPaid : SfxId.Deposit, end, false);
            if (feedback != null && counted) feedback.CrateArrived(end);
            f.product.Recycle();
        }
    }
}
