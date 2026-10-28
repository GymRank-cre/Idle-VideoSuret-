using System;
using UnityEngine;

// Produit fini posé sur le plateau de sortie d'un poste, puis ramassé par le joueur.
[RequireComponent(typeof(Collider))]
public class Product : MonoBehaviour
{
    Collider col;
    Action<GameObject> release;
    Action<Product> taken;

    public ProductType Type { get; private set; }
    public bool IsCollecting { get; private set; }

    // `releaseToPool` rend l'objet à son pool ; `onTaken` prévient le poste que la place est libre.
    public void Init(ProductType type, Action<GameObject> releaseToPool, Action<Product> onTaken)
    {
        if (col == null) col = GetComponent<Collider>();

        Type = type;
        release = releaseToPool;
        taken = onTaken;
        IsCollecting = false;
        col.enabled = true;
    }

    // Appelé par le joueur : le produit ne peut plus être ramassé une 2e fois, et libère sa place au poste.
    public void BeginCollect()
    {
        IsCollecting = true;
        col.enabled = false;
        if (taken != null)
        {
            taken(this);
            taken = null;
        }
    }

    public void Recycle()
    {
        if (release != null) release(gameObject);
        else Destroy(gameObject);
    }
}
