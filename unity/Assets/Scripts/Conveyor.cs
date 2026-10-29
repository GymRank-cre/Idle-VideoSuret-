using System.Collections.Generic;
using UnityEngine;

// Source de produits finis posés sur un plateau (poste d'installation, machine de la chaîne) : un tapis peut en prendre.
public interface IProductSource
{
    // Retire un produit du plateau (BeginCollect déjà appelé : il ne peut plus être ramassé ailleurs) ; null s'il n'y en a pas.
    Product TakeProduct();
}

// Tapis roulant (11a-3) : prend les produits d'un plateau, les fait glisser le long de `waypoints` puis les dépose
// dans la machine suivante. Un produit attend au bout si la machine est pleine ; rien n'est perdu.
// Les « taquets » visibles (`cleats`) défilent sur la bande pour que le sens et la vitesse se lisent sans texte.
public class Conveyor : MonoBehaviour
{
    struct Item
    {
        public Product product;
        public Vector3 from;
        public float dist;
        public float blend;
    }

    [SerializeField] MonoBehaviour source;          // doit implémenter IProductSource
    [SerializeField] ProcessStation destination;
    [SerializeField] Transform[] waypoints;
    [SerializeField] Transform[] cleats;            // taquets de la bande (déplacés, jamais créés en jeu)
    [SerializeField, Min(0.1f)] float speed = 1.6f; // mètres par seconde
    [SerializeField, Min(0.1f)] float spacing = 0.7f;
    [SerializeField] float height = 0.22f;          // hauteur de la bande
    [SerializeField] float cleatSpacing = 0.5f;

    readonly List<Item> items = new List<Item>(); // [0] = le plus avancé
    float[] cumulative;
    float length;
    IProductSource productSource;

    public int OnBelt => items.Count;
    public float Speed => speed;

    void Awake()
    {
        productSource = source as IProductSource;
        cumulative = new float[waypoints.Length];
        for (int i = 1; i < waypoints.Length; i++)
            cumulative[i] = cumulative[i - 1] + Vector3.Distance(waypoints[i - 1].position, waypoints[i].position);
        length = cumulative[waypoints.Length - 1];
    }

    // Point de la bande à la distance `d` du départ (et sa direction).
    Vector3 PathPoint(float d, out Vector3 direction)
    {
        d = Mathf.Clamp(d, 0f, length);
        int i = 1;
        while (i < waypoints.Length - 1 && cumulative[i] < d) i++;
        Vector3 a = waypoints[i - 1].position, b = waypoints[i].position;
        float seg = Mathf.Max(0.0001f, cumulative[i] - cumulative[i - 1]);
        direction = (b - a) / seg;
        Vector3 p = a + direction * (d - cumulative[i - 1]);
        p.y = height;
        return p;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        MoveItems(dt);
        TakeNew();
        MoveCleats();
    }

    void MoveItems(float dt)
    {
        for (int i = 0; i < items.Count; i++)
        {
            Item it = items[i];
            float limit = i == 0 ? length : items[i - 1].dist - spacing;
            it.dist = Mathf.Min(it.dist + speed * dt, limit);
            it.blend = Mathf.Min(1f, it.blend + dt / 0.25f);
            Vector3 dir;
            Vector3 onBelt = PathPoint(it.dist, out dir);
            it.product.transform.position = Vector3.Lerp(it.from, onBelt, it.blend);
            items[i] = it;
        }

        // le plus avancé est au bout : dans la machine s'il y a de la place
        if (items.Count > 0 && items[0].dist >= length - 0.001f && destination.TryAcceptFromBelt(items[0].product))
            items.RemoveAt(0);
    }

    void TakeNew()
    {
        if (productSource == null) return;
        if (items.Count > 0 && items[items.Count - 1].dist < spacing) return;       // le départ est occupé
        if (destination.FreeInputSlots - items.Count <= 0) return;                  // la machine n'aura pas de place
        Product p = productSource.TakeProduct();
        if (p == null) return;
        items.Add(new Item { product = p, from = p.transform.position, dist = 0f, blend = 0f });
    }

    void MoveCleats()
    {
        if (cleats == null) return;
        float offset = Mathf.Repeat(Time.time * speed, cleatSpacing);
        for (int i = 0; i < cleats.Length; i++)
        {
            if (cleats[i] == null) continue;
            float d = offset + i * cleatSpacing;
            bool visible = d <= length;
            if (cleats[i].gameObject.activeSelf != visible) cleats[i].gameObject.SetActive(visible);
            if (!visible) continue;
            Vector3 dir;
            Vector3 p = PathPoint(d, out dir);
            cleats[i].position = new Vector3(p.x, height + 0.01f, p.z);
            cleats[i].rotation = Quaternion.LookRotation(dir == Vector3.zero ? Vector3.forward : dir, Vector3.up);
        }
    }
}
