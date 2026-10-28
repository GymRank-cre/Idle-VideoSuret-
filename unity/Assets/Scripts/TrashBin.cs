using UnityEngine;

// Corbeille : le joueur qui y entre vide tous ses produits portés (sans pénalité), pour ne jamais rester
// bloqué avec un produit inutile. Les produits retournent à leur pool.
[RequireComponent(typeof(Collider))]
public class TrashBin : MonoBehaviour
{
    [SerializeField] Transform burstPoint;
    [SerializeField] FeedbackSpawner feedback; // facultatif

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerStay(Collider other)
    {
        ProductCarrier carrier = other.GetComponent<ProductCarrier>();
        if (carrier == null || carrier.Count == 0) return;

        carrier.Clear();
        if (feedback != null && burstPoint != null) feedback.CrateArrived(burstPoint.position);
    }
}
