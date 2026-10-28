using System.Collections.Generic;
using UnityEngine;

// Pool simple : les objets rendus sont désactivés puis réutilisés.
// Release() accepte aussi un objet créé ailleurs (ex. une caisse de ResourcePile).
public class ObjectPool
{
    readonly GameObject prefab;
    readonly Transform parent;
    readonly List<GameObject> inactive = new List<GameObject>();

    public ObjectPool(GameObject prefab, Transform parent = null)
    {
        this.prefab = prefab;
        this.parent = parent;
    }

    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject go = null;
        while (inactive.Count > 0 && go == null)
        {
            int last = inactive.Count - 1;
            go = inactive[last];
            inactive.RemoveAt(last);
        }

        if (go == null)
            go = Object.Instantiate(prefab, position, rotation, parent);

        go.transform.SetPositionAndRotation(position, rotation);
        go.SetActive(true);
        return go;
    }

    public void Release(GameObject go)
    {
        if (go == null) return;

        go.SetActive(false);
        go.transform.SetParent(parent, true);
        inactive.Add(go);
    }
}
