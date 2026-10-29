using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Boîte arrondie biseautée (mesh procédural) : machines, plus tard dalles, caisses, comptoir.
// Les meshes sont enregistrés dans Assets/Prototype/Meshes (un par taille/rayon) pour que la scène les référence.
public static class RoundedBox
{
    const string Folder = "Assets/Prototype/Meshes";

    public static Mesh Get(Vector3 size, float radius, int segments = 5)
    {
        radius = Mathf.Min(radius, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.5f - 0.001f);
        string name = "RBox_" + size.x.ToString("0.00") + "x" + size.y.ToString("0.00") + "x" + size.z.ToString("0.00") + "_r" + radius.ToString("0.00");
        string path = Folder + "/" + name.Replace(',', '.') + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Prototype")) AssetDatabase.CreateFolder("Assets", "Prototype");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prototype", "Meshes");
        Mesh mesh = Build(size, radius, segments);
        mesh.name = name;
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // Positions d'échantillonnage le long d'un axe de demi-longueur h : le coin arrondi est subdivisé, le plat n'a que ses extrémités.
    static float[] Axis(float h, float r, int seg)
    {
        var list = new List<float>();
        for (int i = 0; i <= seg; i++) list.Add(-h + r * i / seg);
        for (int i = 0; i <= seg; i++) list.Add(h - r + r * i / seg);
        return list.ToArray();
    }

    static Mesh Build(Vector3 size, float r, int seg)
    {
        Vector3 h = size * 0.5f;
        float[] ax = Axis(h.x, r, seg), ay = Axis(h.y, r, seg), az = Axis(h.z, r, seg);
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();

        // six faces : (axe fixe, signe) ; les deux autres axes forment la grille
        for (int face = 0; face < 6; face++)
        {
            int fixedAxis = face / 2;
            float sign = face % 2 == 0 ? 1f : -1f;
            float[] u = fixedAxis == 0 ? ay : ax;
            float[] v = fixedAxis == 2 ? ay : az;
            int baseIndex = verts.Count;
            for (int j = 0; j < v.Length; j++)
            {
                for (int i = 0; i < u.Length; i++)
                {
                    Vector3 p = Vector3.zero;
                    if (fixedAxis == 0) p = new Vector3(sign * h.x, u[i], v[j]);
                    else if (fixedAxis == 1) p = new Vector3(u[i], sign * h.y, v[j]);
                    else p = new Vector3(u[i], v[j], sign * h.z);

                    Vector3 inner = new Vector3(
                        Mathf.Clamp(p.x, -(h.x - r), h.x - r),
                        Mathf.Clamp(p.y, -(h.y - r), h.y - r),
                        Mathf.Clamp(p.z, -(h.z - r), h.z - r));
                    Vector3 d = p - inner;
                    Vector3 n = d.sqrMagnitude > 1e-10f ? d.normalized : (fixedAxis == 0 ? Vector3.right : fixedAxis == 1 ? Vector3.up : Vector3.forward) * sign;
                    verts.Add(inner + n * r);
                    norms.Add(n);
                }
            }

            int cols = u.Length;
            for (int j = 0; j < v.Length - 1; j++)
            {
                for (int i = 0; i < cols - 1; i++)
                {
                    int a = baseIndex + j * cols + i, b = a + 1, c = a + cols, d2 = c + 1;
                    // orientation : le triangle doit regarder vers l'extérieur
                    Vector3 cross = Vector3.Cross(verts[c] - verts[a], verts[b] - verts[a]);
                    bool outward = Vector3.Dot(cross, norms[a]) > 0f;
                    if (outward) { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d2); }
                    else { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d2); tris.Add(c); }
                }
            }
        }

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Objet de scène (sans collider) portant le mesh arrondi.
    public static Transform Make(Transform parent, string name, Vector3 localPos, Vector3 size, float radius, Material mat)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.GetComponent<MeshFilter>().sharedMesh = Get(size, radius);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }
}
