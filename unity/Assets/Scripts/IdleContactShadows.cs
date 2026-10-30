using UnityEngine;

// Ombres de contact (UW-1) : toutes dans UN maillage, donc un seul appel de dessin. Chaque groupe (0 = toujours visible, décor ; 1 à 5 = poste i-1 ouvert)
// occupe une plage de triangles ; on ne redessine la liste qu'à l'ouverture d'un poste, jamais à chaque image.
public class IdleContactShadows : MonoBehaviour
{
    [SerializeField] MeshFilter target;
    [SerializeField] int[] groupStart;    // premier indice de triangle de chaque groupe ; la dernière valeur = nombre total d'indices

    int[] allTriangles;
    bool[] shown;
    IdleGame subscribed;

    void Start()
    {
        allTriangles = target.sharedMesh.triangles;
        target.mesh = Instantiate(target.sharedMesh);   // copie propre à cette partie : la scène garde le maillage complet
        shown = new bool[groupStart.Length - 1];
        subscribed = IdleHost.Instance.Game;
        subscribed.OnUnlocked += Unlocked;
        Rebuild();
    }

    void OnDestroy()
    {
        if (subscribed != null) subscribed.OnUnlocked -= Unlocked;
    }

    void Unlocked(int station) { Rebuild(); }

    void Update()
    {
        // une partie neuve ou rechargée change les postes sans événement : on vérifie sans rien allouer
        IdleGame game = subscribed;
        for (int i = 1; i < shown.Length; i++)
            if (shown[i] != game.Stations[i - 1].unlocked) { Rebuild(); return; }
    }

    void Rebuild()
    {
        IdleGame game = subscribed;
        int count = 0;
        for (int i = 0; i < shown.Length; i++)
        {
            shown[i] = i == 0 || game.Stations[i - 1].unlocked;
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
