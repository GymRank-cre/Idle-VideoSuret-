using System;
using UnityEngine;

// Billet au sol. Sa valeur est fixée à l'apparition par le poste ;
// il retourne à son pool une fois ramassé.
[RequireComponent(typeof(Collider))]
public class Bill : MonoBehaviour
{
    Collider col;
    Action<GameObject> release;

    public int Value { get; private set; }
    public bool IsCollecting { get; private set; }

    public void Init(int value, Action<GameObject> releaseToPool)
    {
        if (col == null) col = GetComponent<Collider>();

        Value = value;
        release = releaseToPool;
        IsCollecting = false;
        col.enabled = true;
    }

    // Appelé par le collecteur : le billet ne peut plus être ramassé une 2e fois.
    public void BeginCollect()
    {
        IsCollecting = true;
        col.enabled = false;
    }

    public void Recycle()
    {
        if (release != null) release(gameObject);
        else Destroy(gameObject);
    }
}
