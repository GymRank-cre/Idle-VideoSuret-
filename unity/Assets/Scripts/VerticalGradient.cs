using UnityEngine;
using UnityEngine.UI;

// Dégradé vertical d'une image d'interface (haut -> bas), calculé sur les sommets : pas de texture en plus, fonctionne en 9 zones.
// À placer avant les composants Shadow, qui recopient ensuite le maillage dans leur propre couleur.
public class VerticalGradient : BaseMeshEffect
{
    [SerializeField] Color top = Color.white;
    [SerializeField] Color bottom = Color.white;

    public void Set(Color newTop, Color newBottom)
    {
        if (top == newTop && bottom == newBottom) return;
        top = newTop;
        bottom = newBottom;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;
        Rect rect = ((RectTransform)transform).rect;
        var v = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            float t = Mathf.InverseLerp(rect.yMin, rect.yMax, v.position.y);
            v.color = Color.Lerp(bottom, top, t) * (Color)v.color;
            vh.SetUIVertex(v, i);
        }
    }
}
