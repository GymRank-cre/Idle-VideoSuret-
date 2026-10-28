using UnityEngine;

// Coiffure d'un client, en formes simples posées sur la tête : choisit au hasard une forme
// (courte, mi-longue, hérissée) et une couleur parmi quelques-unes, à chaque apparition.
public class CustomerHair : MonoBehaviour
{
    [SerializeField] GameObject[] styles;   // une racine par forme ; une seule est active à la fois
    [SerializeField] Color[] colors;

    Material material;

    void Awake()
    {
        // un matériau propre à ce client, partagé par toutes ses formes
        Renderer first = styles[0].GetComponentInChildren<Renderer>(true);
        material = new Material(first.sharedMaterial);
        foreach (GameObject style in styles)
            foreach (Renderer r in style.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = material;
    }

    void OnEnable()
    {
        Randomize();
    }

    public void Randomize()
    {
        int chosen = Random.Range(0, styles.Length);
        for (int i = 0; i < styles.Length; i++)
            styles[i].SetActive(i == chosen);

        Color c = colors[Random.Range(0, colors.Length)];
        material.SetColor("_BaseColor", c);
        material.color = c;
    }
}
