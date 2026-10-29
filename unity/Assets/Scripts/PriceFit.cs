using UnityEngine;

// Prix d'une dalle : le texte et l'icône de billet restent centrés et entièrement dans la plaque, même pour « 1,2K ».
// Le texte est réduit si l'ensemble dépasse `maxWidth` (mètres, repère de la dalle).
public class PriceFit : MonoBehaviour
{
    [SerializeField] TextMesh label;     // ancré à gauche
    [SerializeField] Transform icon;     // enfant de la dalle, pas du texte
    [SerializeField] float maxWidth = 2.2f;
    [SerializeField] float iconWidth = 0.9f;
    [SerializeField] float gap = 0.08f;
    [SerializeField] float baseScale = 1f;
    [SerializeField] float height = 0.55f; // position z de la ligne (repère de la dalle)

    MeshRenderer meshRenderer;
    float scale = 1f;

    void LateUpdate()
    {
        if (label == null) return;
        if (meshRenderer == null) meshRenderer = label.GetComponent<MeshRenderer>();
        float parentScale = transform.lossyScale.x > 0.0001f ? transform.lossyScale.x : 1f;
        float raw = meshRenderer.bounds.size.x / Mathf.Max(0.0001f, scale * baseScale) / parentScale;
        if (raw <= 0.0001f) return;

        float available = maxWidth - iconWidth - gap;
        scale = Mathf.Min(1f, available / raw);
        float textWidth = raw * scale * baseScale;
        float total = iconWidth + gap + textWidth;
        float left = -total * 0.5f;

        label.transform.localScale = Vector3.one * (scale * baseScale);
        Vector3 p = label.transform.localPosition;
        label.transform.localPosition = new Vector3(left + iconWidth + gap, p.y, p.z);
        if (icon != null)
        {
            Vector3 ip = icon.localPosition;
            icon.localPosition = new Vector3(left + iconWidth * 0.5f, ip.y, ip.z);
        }
    }
}
