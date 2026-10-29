using System;
using UnityEngine;

// Produit fini posé sur le plateau de sortie d'un poste, puis ramassé par le joueur.
// Chaîne de production (11a) : `Stage` 0 = assemblé, 1 = testé (petit voyant vert), 2 = emballé (ruban blanc).
[RequireComponent(typeof(Collider))]
public class Product : MonoBehaviour
{
    Collider col;
    Action<GameObject> release;
    Action<Product> taken;
    Vector3 baseScale;
    bool scaleKnown;
    Transform testedMark;
    Transform packedMark;
    static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    public ProductType Type { get; private set; }
    public bool IsCollecting { get; private set; }
    public int Stage { get; private set; }

    // `releaseToPool` rend l'objet à son pool ; `onTaken` prévient le poste que la place est libre.
    public void Init(ProductType type, Action<GameObject> releaseToPool, Action<Product> onTaken, int stage = 0)
    {
        if (col == null) col = GetComponent<Collider>();
        if (!scaleKnown) { baseScale = transform.localScale; scaleKnown = true; }
        transform.localScale = baseScale;

        Type = type;
        release = releaseToPool;
        taken = onTaken;
        IsCollecting = false;
        col.enabled = true;
        SetStage(stage);
    }

    // Agrandit le produit (tapis) ; Init le remet à sa taille.
    public void SetScaleFactor(float factor)
    {
        if (!scaleKnown) { baseScale = transform.localScale; scaleKnown = true; }
        transform.localScale = baseScale * factor;
    }

    // Change de stade : les repères visuels sont créés à la demande, puis réutilisés avec le pool.
    public void SetStage(int stage)
    {
        Stage = stage;
        if (stage >= 1 && testedMark == null) testedMark = MakeMark("TestedMark", PrimitiveType.Sphere, new Color32(0x3D, 0xDC, 0x5A, 0xFF));
        if (stage >= 2 && packedMark == null) packedMark = MakeMark("PackedMark", PrimitiveType.Cube, new Color32(0xF4, 0xF1, 0xE8, 0xFF));
        if (testedMark != null) testedMark.gameObject.SetActive(stage == 1 || stage >= 2);
        if (packedMark != null) packedMark.gameObject.SetActive(stage >= 2);
    }

    Transform MakeMark(string markName, PrimitiveType shape, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(shape);
        go.name = markName;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);

        Renderer own = GetComponentInChildren<Renderer>();
        Renderer mark = go.GetComponent<Renderer>();
        Bounds b = own != null ? own.bounds : new Bounds(transform.position, Vector3.one * 0.3f);
        float s = Mathf.Max(0.0001f, transform.lossyScale.x);
        if (shape == PrimitiveType.Sphere) // voyant sur le dessus
        {
            go.transform.position = new Vector3(b.center.x, b.max.y, b.center.z);
            go.transform.localScale = Vector3.one * (0.14f / s);
        }
        else // ruban : bande qui ceinture le produit
        {
            go.transform.position = b.center;
            go.transform.localScale = new Vector3(b.size.x * 1.04f / s, b.size.y * 1.04f / s, b.size.z * 0.22f / s);
        }
        if (own != null)
        {
            mark.sharedMaterial = own.sharedMaterial;
            var block = new MaterialPropertyBlock();
            block.SetColor(ColorId, color);
            mark.SetPropertyBlock(block);
        }
        mark.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    // Appelé par le joueur : le produit ne peut plus être ramassé une 2e fois, et libère sa place au poste.
    public void BeginCollect()
    {
        IsCollecting = true;
        col.enabled = false;
        if (taken != null)
        {
            taken(this);
            taken = null;
        }
    }

    public void Recycle()
    {
        if (release != null) release(gameObject);
        else Destroy(gameObject);
    }
}
