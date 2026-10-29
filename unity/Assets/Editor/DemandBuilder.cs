using UnityEditor;
using UnityEngine;

// Étape 6c : relie les postes à ProductDemand (le poste fabrique d'abord ce que demande la file de clients).
public static class DemandBuilder
{
    public static void Build()
    {
        CustomerQueue queue = Object.FindFirstObjectByType<CustomerQueue>();
        if (queue == null) return;

        GameObject old = GameObject.Find("ProductDemand");
        if (old != null) Object.DestroyImmediate(old);

        GameObject go = new GameObject("ProductDemand", typeof(ProductDemand));
        var so = new SerializedObject(go.GetComponent<ProductDemand>());
        so.FindProperty("queue").objectReferenceValue = queue;
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (InstallStation station in Object.FindObjectsByType<InstallStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var sso = new SerializedObject(station);
            sso.FindProperty("demand").objectReferenceValue = go.GetComponent<ProductDemand>();
            sso.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
