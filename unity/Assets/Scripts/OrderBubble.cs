using UnityEngine;

// Bulle de commande au-dessus de la tête du client en tête de file : pastille blanche arrondie avec une petite
// copie du produit et « x2 ». Se met à jour à chaque livraison, clignote en vert quand la commande est complète.
public class OrderBubble : MonoBehaviour
{
    [SerializeField] Renderer pill;
    [SerializeField] TextMesh text;
    [SerializeField] TextMesh shadow;
    [SerializeField] Transform iconAnchor;
    [SerializeField] float iconScale = 0.9f;
    [SerializeField] float flashDuration = 0.7f;
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color flashColor = new Color(0.45f, 0.95f, 0.5f);

    ProductType[] types;
    GameObject[] icons;
    Material pillMaterial;
    float flashTimer;

    // Une copie inactive de chaque produit, créée une fois (aucune allocation pendant le jeu).
    public void Setup(ProductType[] productTypes)
    {
        types = productTypes;
        icons = new GameObject[types.Length];
        for (int i = 0; i < types.Length; i++)
        {
            if (types[i] == null || types[i].Prefab == null) continue;
            GameObject icon = Instantiate(types[i].Prefab, iconAnchor);
            Destroy(icon.GetComponent<Product>());
            foreach (Collider c in icon.GetComponentsInChildren<Collider>()) Destroy(c);
            icon.transform.localPosition = new Vector3(0f, -0.13f * iconScale, 0f);
            icon.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            icon.transform.localScale = Vector3.one * iconScale;
            icon.SetActive(false);
            icons[i] = icon;
        }
        gameObject.SetActive(false);
    }

    public void Show(ProductType type, int remaining)
    {
        gameObject.SetActive(true);
        flashTimer = 0f;
        SetPillColor(normalColor);
        Refresh(type, remaining);
    }

    public void Refresh(ProductType type, int remaining)
    {
        if (icons == null) return;
        for (int i = 0; i < icons.Length; i++)
            if (icons[i] != null) icons[i].SetActive(types[i] == type);
        string s = "x" + remaining;
        text.text = s;
        shadow.text = s;
    }

    public void Complete()
    {
        flashTimer = flashDuration;
        text.text = "OK";
        shadow.text = "OK";
    }

    public void Hide()
    {
        flashTimer = 0f;
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (flashTimer <= 0f) return;
        flashTimer -= Time.deltaTime;
        bool on = Mathf.FloorToInt(flashTimer / 0.12f) % 2 == 0; // clignote
        SetPillColor(flashTimer > 0f && on ? flashColor : normalColor);
        if (flashTimer <= 0f) SetPillColor(flashColor);
    }

    void SetPillColor(Color c)
    {
        if (pillMaterial == null) pillMaterial = pill.material;
        pillMaterial.SetColor("_BaseColor", c);
        pillMaterial.color = c;
    }
}
