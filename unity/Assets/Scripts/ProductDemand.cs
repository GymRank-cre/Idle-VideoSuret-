using System.Collections.Generic;
using UnityEngine;

// Dit au poste quel produit fabriquer : d'abord ce que demande le client en tête de file, puis ce qui manque
// aux clients suivants. Les produits déjà sur un plateau ou portés (joueur, livreur) sont comptés.
// Renvoie null si tout est couvert : le poste reprend alors sa rotation.
public class ProductDemand : MonoBehaviour
{
    [SerializeField] CustomerQueue queue;

    readonly Dictionary<ProductType, int> available = new Dictionary<ProductType, int>();

    public ProductType Pick()
    {
        if (queue == null) return null;

        available.Clear();
        foreach (Product p in FindObjectsByType<Product>(FindObjectsSortMode.None))
        {
            if (!p.gameObject.activeInHierarchy || p.Type == null) continue;
            available.TryGetValue(p.Type, out int n);
            available[p.Type] = n + 1;
        }

        foreach (Customer c in queue.Line)
        {
            if (c.IsComplete || c.OrderType == null) continue;
            int need = c.Remaining;
            available.TryGetValue(c.OrderType, out int have);
            int used = Mathf.Min(have, need);
            available[c.OrderType] = have - used;
            if (need > used) return c.OrderType;
        }
        return null;
    }
}
