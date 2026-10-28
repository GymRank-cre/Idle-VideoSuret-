using UnityEngine;

// Un produit fini fabriqué par un poste (caméra dôme, badgeuse, alarme...).
[CreateAssetMenu(fileName = "ProductType", menuName = "Idle/Product Type")]
public class ProductType : ScriptableObject
{
    [SerializeField] string displayName = "Produit";
    [SerializeField] GameObject prefab;       // forme simple, avec un collider trigger et le script Product
    [SerializeField, Min(0)] int value = 25;  // valeur de vente (utilisée à l'étape 5b)
    [SerializeField] Color accentColor = Color.white;

    public string DisplayName => displayName;
    public GameObject Prefab => prefab;
    public int Value => value;
    public Color AccentColor => accentColor;
}
