using System.Collections.Generic;
using UnityEngine;

// Pile d'objets portée dans le dos. Les objets du haut suivent avec un
// léger retard, ce qui donne l'effet de pile qui se balance.
// À mettre sur le même objet que le CharacterController du joueur.
public class StackCarrier : MonoBehaviour
{
    [SerializeField] Transform stackAnchor;   // vide placé dans le dos du joueur
    [SerializeField] int capacity = 8;
    [SerializeField] float itemHeight = 0.35f;
    [SerializeField] float followSpeed = 14f;
    [SerializeField] GameObject maxIndicator; // étiquette "MAX", facultative

    readonly List<Transform> items = new List<Transform>();
    Camera cam;

    public int Count => items.Count;
    public int Capacity => capacity;
    public event System.Action OnCountChanged;
    public bool IsFull => items.Count >= capacity;
    public bool IsEmpty => items.Count == 0;

    void Start()
    {
        cam = Camera.main;
        UpdateIndicator();
    }

    public bool TryAdd(Transform item)
    {
        if (IsFull) return false;

        item.SetParent(null); // suit en coordonnées monde, pour l'effet de balancement
        items.Add(item);
        UpdateIndicator();
        return true;
    }

    // Retire l'objet du haut de la pile (pour le déposer à un poste, par exemple).
    public Transform RemoveLast()
    {
        if (IsEmpty) return null;

        Transform item = items[items.Count - 1];
        items.RemoveAt(items.Count - 1);
        UpdateIndicator();
        return item;
    }

    void LateUpdate()
    {
        if (stackAnchor == null) return;

        // Nettoie les objets détruits ailleurs.
        if (items.RemoveAll(i => i == null) > 0)
            UpdateIndicator();

        for (int i = 0; i < items.Count; i++)
        {
            Vector3 targetPos = stackAnchor.position + Vector3.up * (itemHeight * i);

            // Plus l'objet est haut, plus il réagit lentement.
            float t = 1f - Mathf.Exp(-followSpeed / (1f + i * 0.35f) * Time.deltaTime);

            items[i].position = Vector3.Lerp(items[i].position, targetPos, t);
            items[i].rotation = Quaternion.Slerp(items[i].rotation, stackAnchor.rotation, t);
        }

        // L'indicateur MAX fait toujours face à la caméra.
        if (maxIndicator != null && maxIndicator.activeSelf && cam != null)
            maxIndicator.transform.rotation = cam.transform.rotation;
    }

    void UpdateIndicator()
    {
        if (maxIndicator != null)
            maxIndicator.SetActive(IsFull);
        OnCountChanged?.Invoke();
    }
}
