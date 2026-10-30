using System.Collections.Generic;
using UnityEngine;

// Petit constructeur de maillage « facettes » (un sommet par face : arêtes vives, style low-poly). Sert à fusionner tout un décor
// de même couleur en UN maillage (donc un seul appel de dessin) : damier, bordures, arbres, route...
public class MeshKit
{
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<int> tris = new List<int>();
    readonly List<Vector2> uvs = new List<Vector2>();   // un UV par sommet (zéro sauf pour UvQuad)
    bool hasUv;

    public int VertexCount => verts.Count;

    void Tri(Vector3 a, Vector3 b, Vector3 c)
    {
        int i = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        uvs.Add(Vector2.zero); uvs.Add(Vector2.zero); uvs.Add(Vector2.zero);
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
