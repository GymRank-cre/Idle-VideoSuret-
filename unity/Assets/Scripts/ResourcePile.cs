using UnityEngine;

// Zone de ramassage : tant que le joueur reste dedans et que sa pile n'est
// pas pleine, un objet apparaît à intervalle régulier et vole vers son dos.
// Les caisses viennent d'un pool, partagé avec le poste qui les y remet.
[RequireComponent(typeof(Collider))]
public class ResourcePile : MonoBehaviour
{
    [SerializeField] GameObject itemPrefab;
    [SerializeField] float interval = 0.15f;

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
        if (carrier == null || carrier.IsFull) return;

        timer += Time.deltaTime;
        if (timer < interval) return;
        timer = 0f;

        GameObject item = Pool.Get(transform.position + Vector3.up * 0.5f, Quaternion.identity);
        carrier.TryAdd(item.transform);
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<StackCarrier>() != null)
            timer = 0f;
    }
}
