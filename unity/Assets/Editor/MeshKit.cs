using System.Collections.Generic;
using UnityEngine;

// Petit constructeur de maillage « facettes » (un sommet par face : arêtes vives, style low-poly). Sert à fusionner tout un décor
// de même couleur en UN maillage (donc un seul appel de dessin) : damier, bordures, arbres, route...
public class MeshKit
{
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<int> tris = new List<int>();

    public int VertexCount => verts.Count;

    void Tri(Vector3 a, Vector3 b, Vector3 c)
    {
        int i = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
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
