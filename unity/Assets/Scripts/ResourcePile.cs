using System.Collections.Generic;
using UnityEngine;

// Zone de ramassage : tant que le joueur reste dedans et que sa pile n'est
// pas pleine, un objet apparaît à intervalle régulier et vole vers son dos.
// Les caisses viennent d'un pool, partagé avec le poste qui les y remet.
[RequireComponent(typeof(Collider))]
public class ResourcePile : MonoBehaviour
{
    [SerializeField] GameObject itemPrefab;
    [SerializeField] float interval = 0.15f;
    [SerializeField] float flightDuration = 0.2f; // la caisse vole en arc vers la pile (0,15 à 0,25 s)
    [SerializeField] float flightArc = 0.8f;

    struct Flight
    {
        public Transform item;
        public StackCarrier carrier;
        public Vector3 from;
        public float elapsed;
    }

    readonly List<Flight> flights = new List<Flight>();

    float timer;
    ObjectPool pool;

    public ObjectPool Pool
    {
        get
        {
            if (pool == null)
                pool = new ObjectPool(itemPrefab, new GameObject(name + " Items").transform);
            return pool;
        }
    }

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerStay(Collider other)
    {
        StackCarrier carrier = other.GetComponent<StackCarrier>();
        if (carrier == null || carrier.Count + InFlightFor(carrier) >= carrier.Capacity) return;

        timer += Time.deltaTime;
        if (timer < interval) return;
        timer = 0f;

        GameObject item = Pool.Get(transform.position + Vector3.up * 0.5f, Quaternion.identity);
        flights.Add(new Flight { item = item.transform, carrier = carrier, from = item.transform.position });
    }

    int InFlightFor(StackCarrier carrier)
    {
        int n = 0;
        for (int i = 0; i < flights.Count; i++)
            if (flights[i].carrier == carrier) n++;
        return n;
    }

    void Update()
    {
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            Flight f = flights[i];
            f.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(f.elapsed / flightDuration);
            Vector3 pos = Vector3.Lerp(f.from, f.carrier.NextSlotPosition, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * flightArc;
            f.item.position = pos;

            if (t >= 1f)
            {
                flights.RemoveAt(i);
                if (!f.carrier.TryAdd(f.item)) Pool.Release(f.item.gameObject); // pile pleine entre-temps : la caisse revient au tas
            }
            else
            {
                flights[i] = f;
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<StackCarrier>() != null)
            timer = 0f;
    }
}
