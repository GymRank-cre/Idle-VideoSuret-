using System.Collections.Generic;
using UnityEngine;

// Sur le joueur : les billets dans le rayon de ramassage volent vers lui,
// puis leur valeur est ajoutée au porte-monnaie et ils retournent au pool.
public class BillCollector : MonoBehaviour
{
    [SerializeField] Wallet wallet;
    [SerializeField] FeedbackSpawner feedback; // facultatif : "+valeur" et éclat à chaque billet
    [SerializeField] float pickupRadius = 1.4f;
    [SerializeField] float flightDuration = 0.2f;
    [SerializeField] float targetHeight = 1f;

    struct Flight
    {
        public Bill bill;
        public Vector3 from;
        public float elapsed;
    }

    readonly List<Flight> flights = new List<Flight>();
    readonly Collider[] hits = new Collider[32];

    void Update()
    {
        Scan();
        UpdateFlights();
    }

    void Scan()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, pickupRadius, hits, ~0, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Bill bill = hits[i].GetComponent<Bill>();
            if (bill == null || bill.IsCollecting) continue;

            bill.BeginCollect();
            flights.Add(new Flight { bill = bill, from = bill.transform.position });
        }
    }

    void UpdateFlights()
    {
        Vector3 target = transform.position + Vector3.up * targetHeight;

        for (int i = flights.Count - 1; i >= 0; i--)
        {
            Flight f = flights[i];
            f.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(f.elapsed / flightDuration);
            f.bill.transform.position = Vector3.Lerp(f.from, target, t * t); // aspiré : le billet accélère vers le joueur

            if (t >= 1f)
            {
                wallet.Add(f.bill.Value);
                AudioManager.PlayBill();
                if (feedback != null) feedback.BillCollected(target, f.bill.Value);
                f.bill.Recycle();
                flights.RemoveAt(i);
            }
            else
            {
                flights[i] = f;
            }
        }
    }
}
