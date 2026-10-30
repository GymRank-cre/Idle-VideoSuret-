using UnityEngine;

// Ombres de contact (UW-1) : toutes dans UN maillage, donc un seul appel de dessin. Le groupe 0 est toujours visible (décor) ; chaque autre groupe suit une machine
// (poste et emplacement) et occupe une plage de triangles ; on ne redessine la liste que lorsqu'une machine arrive, jamais à chaque image.
public class IdleContactShadows : MonoBehaviour
{
    [SerializeField] MeshFilter target;
    [SerializeField] int[] groupStart;    // premier indice de triangle de chaque groupe ; la dernière valeur = nombre total d'indices
    [SerializeField] int[] groupStation;  // poste de chaque groupe (le groupe 0 n'en a pas : -1)
    [SerializeField] int[] groupSlot;     // emplacement de chaque groupe

    int[] allTriangles;
    bool[] shown;
    IdleGame subscribed;

    void Start()
    {
        allTriangles = target.sharedMesh.triangles;
        target.mesh = Instantiate(target.sharedMesh);   // copie propre à cette partie : la scène garde le maillage complet
        shown = new bool[groupStart.Length - 1];
        subscribed = IdleHost.Instance.Game;
        Rebuild();
    }

    bool Wanted(int group)
    {
        int station = groupStation[group];
        if (station < 0) return true;
        IdleEconomy.StationLevels st = subscribed.Stations[station];
        return st.unlocked && st.slots > groupSlot[group];
    }

    void Update()
    {
        // une partie neuve ou rechargée change les postes sans événement : on vérifie sans rien allouer
        for (int i = 1; i < shown.Length; i++)
            if (shown[i] != Wanted(i)) { Rebuild(); return; }
    }

    void Rebuild()
    {
        int count = 0;
        for (int i = 0; i < shown.Length; i++)
        {
            shown[i] = Wanted(i);
            if (shown[i]) count += groupStart[i + 1] - groupStart[i];
        }
        var visible = new int[count];
        int at = 0;
        for (int i = 0; i < shown.Length; i++)
        {
            if (!shown[i]) continue;
            int length = groupStart[i + 1] - groupStart[i];
            System.Array.Copy(allTriangles, groupStart[i], visible, at, length);
            at += length;
        }
        target.mesh.SetTriangles(visible, 0, false);
    }
}
