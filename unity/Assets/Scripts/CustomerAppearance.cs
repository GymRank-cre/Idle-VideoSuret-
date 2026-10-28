using UnityEngine;

// Tire au hasard la couleur de chemise et de cheveux d'un client (matériaux propres à l'instance).
public class CustomerAppearance : MonoBehaviour
{
    [SerializeField] SkinnedMeshRenderer shirt;
    [SerializeField] SkinnedMeshRenderer hair;
    [SerializeField] Color[] shirtColors;
    [SerializeField] Color[] hairColors;

    Material shirtMaterial;
    Material hairMaterial;

    void Awake()
    {
        if (shirt != null) shirtMaterial = shirt.material; // crée une copie : les autres clients ne changent pas
        if (hair != null) hairMaterial = hair.material;
    }

    void OnEnable()
    {
        Randomize();
    }

    public void Randomize()
    {
        if (shirtMaterial != null && shirtColors != null && shirtColors.Length > 0)
            Apply(shirtMaterial, shirtColors[Random.Range(0, shirtColors.Length)]);
        if (hairMaterial != null && hairColors != null && hairColors.Length > 0)
            Apply(hairMaterial, hairColors[Random.Range(0, hairColors.Length)]);
    }

    static void Apply(Material m, Color c)
    {
        m.SetColor("_BaseColor", c);
        m.color = c;
    }
}
