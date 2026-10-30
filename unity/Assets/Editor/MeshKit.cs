using System.Collections.Generic;
using UnityEngine;

// Petit constructeur de maillage « facettes » (un sommet par face : arêtes vives, style low-poly). Sert à fusionner tout un décor
// de même couleur en UN maillage (donc un seul appel de dessin) : damier, bordures, arbres, route...
public class MeshKit
{
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<int> tris = new List<int>();
    readonly List<Vector2> uvs = new List<Vector2>();   // un UV par sommet (zéro sauf pour UvQuad)
    readonly List<Color32> colors = new List<Color32>();   // alpha = brillance (matériau Idle/Palette)
    bool hasUv;

    public int VertexCount => verts.Count;
    public int IndexCount => tris.Count;
    public Color32 Paint = new Color32(255, 255, 255, 0);    // couleur des sommets ajoutés ensuite

    // Couleur écrite en sRGB ; `gloss` de 0 (mat) à 1 (brillant).
    public static Color32 Color(string hex, float gloss = 0f)
    {
        UnityEngine.Color c;
        ColorUtility.TryParseHtmlString(hex, out c);
        Color32 result = c;
        result.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(gloss) * 255f);
        return result;
    }

    public void SetPaint(string hex, float gloss = 0f) { Paint = Color(hex, gloss); }

    // Recopie un autre maillage ici, d'une seule couleur, éventuellement déplacé (`m`).
    public void Append(MeshKit other, Color32 color, Matrix4x4 m)
    {
        int offset = verts.Count;
        foreach (Vector3 v in other.verts) verts.Add(m.MultiplyPoint3x4(v));
        foreach (Vector2 uv in other.uvs) uvs.Add(uv);
        for (int i = 0; i < other.verts.Count; i++) colors.Add(color);
        foreach (int t in other.tris) tris.Add(t + offset);
    }

    public void Append(MeshKit other, Color32 color) { Append(other, color, Matrix4x4.identity); }

    // Comme Append, mais garde les couleurs de `other`.
    public void AppendPainted(MeshKit other)
    {
        int offset = verts.Count;
        verts.AddRange(other.verts);
        uvs.AddRange(other.uvs);
        colors.AddRange(other.colors);
        foreach (int t in other.tris) tris.Add(t + offset);
    }

    // Quadrilatère dont chaque coin a sa couleur (dégradés de l'herbe). Sens corrigé : la face regarde vers le haut.
    public void ColoredQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 ca, Color32 cb, Color32 cc, Color32 cd)
    {
        if (Vector3.Cross(b - a, c - a).y < 0f) { Vector3 tp = b; b = d; d = tp; Color32 tc = cb; cb = cd; cd = tc; }
        int i = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
        for (int k = 0; k < 4; k++) uvs.Add(Vector2.zero);
        colors.Add(ca); colors.Add(cb); colors.Add(cc); colors.Add(cd);
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
    }

    // Boîte aux arêtes biseautées : 6 faces réduites, 12 chanfreins, 8 coins. Les chanfreins prennent la lumière : c'est ce qui enlève l'aspect « cube ».
    public void BevelBox(Vector3 min, Vector3 size, float bevel)
    {
        float e = Mathf.Min(bevel, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.3f);
        if (e < 0.004f) { Box(min, size); return; }
        Vector3 max = min + size, center = min + size * 0.5f;

        // sommet du coin (sx, sy, sz) situé sur la face perpendiculaire à l'axe `axis`
        Vector3 P(int sx, int sy, int sz, int axis)
        {
            return new Vector3(
                (sx == 0 ? min.x : max.x) + (axis == 0 ? 0f : sx == 0 ? e : -e),
                (sy == 0 ? min.y : max.y) + (axis == 1 ? 0f : sy == 0 ? e : -e),
                (sz == 0 ? min.z : max.z) + (axis == 2 ? 0f : sz == 0 ? e : -e));
        }
        Vector3 Pa(int[] s, int axis) { return P(s[0], s[1], s[2], axis); }

        // faces
        for (int a = 0; a < 3; a++)
            for (int side = 0; side < 2; side++)
            {
                int b = (a + 1) % 3, c = (a + 2) % 3;
                var s = new int[3];
                s[a] = side;
                Vector3 q0, q1, q2, q3;
                s[b] = 0; s[c] = 0; q0 = Pa(s, a);
                s[b] = 1; s[c] = 0; q1 = Pa(s, a);
                s[b] = 1; s[c] = 1; q2 = Pa(s, a);
                s[b] = 0; s[c] = 1; q3 = Pa(s, a);
                Facing(q0, q1, q2, q3, (q0 + q2) * 0.5f - center);
            }
        // chanfreins : l'arête suit l'axe `a`, les deux autres côtés sont fixés
        for (int a = 0; a < 3; a++)
            for (int su = 0; su < 2; su++)
                for (int sv = 0; sv < 2; sv++)
                {
                    int b = (a + 1) % 3, c = (a + 2) % 3;
                    var s = new int[3];
                    s[b] = su; s[c] = sv;
                    s[a] = 0; Vector3 q0 = Pa(s, b), q1 = Pa(s, c);
                    s[a] = 1; Vector3 q2 = Pa(s, c), q3 = Pa(s, b);
                    Facing(q0, q1, q2, q3, (q0 + q2) * 0.5f - center);
                }
        // coins
        for (int sx = 0; sx < 2; sx++)
            for (int sy = 0; sy < 2; sy++)
                for (int sz = 0; sz < 2; sz++)
                {
                    Vector3 q0 = P(sx, sy, sz, 0), q1 = P(sx, sy, sz, 1), q2 = P(sx, sy, sz, 2);
                    if (Vector3.Dot(Vector3.Cross(q1 - q0, q2 - q0), q0 - center) < 0f) Tri(q0, q2, q1); else Tri(q0, q1, q2);
                }
    }

    void Facing(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) Quad(a, d, c, b); else Quad(a, b, c, d);
    }

    void Tri(Vector3 a, Vector3 b, Vector3 c)
    {
        int i = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        uvs.Add(Vector2.zero); uvs.Add(Vector2.zero); uvs.Add(Vector2.zero);
        colors.Add(Paint); colors.Add(Paint); colors.Add(Paint);
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
    }

    void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Tri(a, b, c);
        Tri(a, c, d);
    }

    // Boîte alignée sur les axes (min, taille).
    public void Box(Vector3 min, Vector3 size)
    {
        Vector3 a = min, b = min + new Vector3(size.x, 0, 0), c = min + new Vector3(size.x, 0, size.z), d = min + new Vector3(0, 0, size.z);
        Vector3 up = new Vector3(0, size.y, 0);
        Quad(a + up, d + up, c + up, b + up);   // dessus
        Quad(a, a + up, b + up, b);             // face -Z (sens horaire vu de l'extérieur)
        Quad(b, b + up, c + up, c);             // face +X
        Quad(c, c + up, d + up, d);             // face +Z
        Quad(d, d + up, a + up, a);             // face -X
        Quad(a, b, c, d);                       // dessous
    }

    // Rectangle plat horizontal (dessus uniquement) : centre, tailles X et Z, hauteur.
    public void FlatQuad(Vector3 center, float sizeX, float sizeZ)
    {
        float hx = sizeX * 0.5f, hz = sizeZ * 0.5f;
        Quad(center + new Vector3(-hx, 0, -hz), center + new Vector3(-hx, 0, hz), center + new Vector3(hx, 0, hz), center + new Vector3(hx, 0, -hz));
    }

    // Quadrilatère horizontal texturé : a, b, c, d = coins dans l'ordre, uv* = leurs coordonnées de texture. Le sens est corrigé pour que
    // la face regarde vers le haut.
    public void UvQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
    {
        if (Vector3.Cross(b - a, c - a).y < 0f) { Vector3 tp = b; b = d; d = tp; Vector2 tu = uvB; uvB = uvD; uvD = tu; }
        int i = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
        uvs.Add(uvA); uvs.Add(uvB); uvs.Add(uvC); uvs.Add(uvD);
        for (int k = 0; k < 4; k++) colors.Add(Paint);
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        hasUv = true;
    }

    // Flèche plate vers le haut (dessus uniquement) : centre, direction horizontale unitaire, longueur et largeur de la tête.
    public void FlatArrow(Vector3 center, Vector3 direction, float length, float headWidth)
    {
        direction.y = 0f; direction.Normalize();
        Vector3 side = new Vector3(-direction.z, 0f, direction.x);
        Vector3 tip = center + direction * length * 0.5f, neck = center + direction * length * 0.02f, tail = center - direction * length * 0.5f;
        float shaft = headWidth * 0.36f;
        Tri(tip, neck + side * headWidth * 0.5f, neck - side * headWidth * 0.5f);
        Quad(tail - side * shaft * 0.5f, tail + side * shaft * 0.5f, neck + side * shaft * 0.5f, neck - side * shaft * 0.5f);
        // les deux faces sont émises pour être visibles quel que soit le sens des sommets
        Tri(tip, neck - side * headWidth * 0.5f, neck + side * headWidth * 0.5f);
        Quad(tail - side * shaft * 0.5f, neck - side * shaft * 0.5f, neck + side * shaft * 0.5f, tail + side * shaft * 0.5f);
    }

    // Pastille plate (rectangle à demi-cercles aux extrémités), dans le plan XY, visible depuis -Z (face caméra d'une étiquette).
    public void Pill(float width, float height, float z, int arcSegments = 10)
    {
        float r = height * 0.5f, cx = Mathf.Max(0f, width * 0.5f - r);
        var ring = new List<Vector3> { new Vector3(-cx, r, z), new Vector3(cx, r, z) };
        for (int i = 1; i < arcSegments; i++)   // arc droit, de haut en bas
        {
            float a = Mathf.PI * 0.5f - Mathf.PI * i / arcSegments;
            ring.Add(new Vector3(cx + Mathf.Cos(a) * r, Mathf.Sin(a) * r, z));
        }
        ring.Add(new Vector3(cx, -r, z)); ring.Add(new Vector3(-cx, -r, z));
        for (int i = 1; i < arcSegments; i++)   // arc gauche, de bas en haut
        {
            float a = -Mathf.PI * 0.5f - Mathf.PI * i / arcSegments;
            ring.Add(new Vector3(-cx + Mathf.Cos(a) * r, Mathf.Sin(a) * r, z));
        }
        Vector3 center = new Vector3(0f, 0f, z);
        for (int i = 0; i < ring.Count; i++) Tri(center, ring[i], ring[(i + 1) % ring.Count]);
    }

    // Cône à base circulaire (pointe vers le haut).
    public void Cone(Vector3 baseCenter, float radius, float height, int segments = 7)
    {
        Vector3 tip = baseCenter + new Vector3(0, height, 0);
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
            Vector3 p0 = baseCenter + new Vector3(Mathf.Cos(a0) * radius, 0, Mathf.Sin(a0) * radius);
            Vector3 p1 = baseCenter + new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius);
            Tri(p0, tip, p1);
            Tri(baseCenter, p0, p1);
        }
    }

    // Cylindre (tronc) sans fond.
    public void Cylinder(Vector3 baseCenter, float radius, float height, int segments = 6)
    {
        Vector3 up = new Vector3(0, height, 0);
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
            Vector3 p0 = baseCenter + new Vector3(Mathf.Cos(a0) * radius, 0, Mathf.Sin(a0) * radius);
            Vector3 p1 = baseCenter + new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius);
            Quad(p0, p0 + up, p1 + up, p1);
        }
    }

    public Mesh ToMesh(string name)
    {
        var mesh = new Mesh { name = name, indexFormat = verts.Count > 60000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        if (hasUv) mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Objet de scène portant ce maillage avec le matériau donné.
    public GameObject ToObject(string name, Transform parent, Material material)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = ToMesh(name);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }
}
