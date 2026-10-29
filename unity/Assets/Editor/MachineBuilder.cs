using UnityEditor;
using UnityEngine;

// Chaîne de production (11a) : ajoute à chaque machine son voyant et son étiquette de stock (« 3/6 > 1/4 »).
// Appelé par PrototypeSetup une fois les postes construits.
public static class MachineBuilder
{
    public static void Build()
    {
        foreach (InstallStation station in Object.FindObjectsByType<InstallStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            AddStockHud(station.transform, station, 3.0f);
    }

    // Voyant (sphère) + texte contouré au-dessus de `machine`, face caméra.
    public static StockLabel AddStockHud(Transform machine, MonoBehaviour status, float height)
    {
        Transform old = machine.Find("StockHud");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = new GameObject("StockHud");
        root.transform.SetParent(machine, false);
        root.transform.localPosition = new Vector3(0f, height, 0f);
        root.transform.localScale = Vector3.one * 1f / Mathf.Max(0.0001f, machine.lossyScale.x);
        var face = root.AddComponent<FaceCamera>();
        face.enabled = true;

        GameObject lampGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lampGo.name = "Lamp";
        Object.DestroyImmediate(lampGo.GetComponent<Collider>());
        lampGo.transform.SetParent(root.transform, false);
        lampGo.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        lampGo.transform.localScale = Vector3.one * 0.3f;
        Renderer lamp = lampGo.GetComponent<Renderer>();
        Shader lit = Shader.Find("Universal Render Pipeline/Unlit");
        var lampMat = new Material(lit != null ? lit : Shader.Find("Sprites/Default")) { name = "MachineLamp" };
        lamp.sharedMaterial = lampMat;
        lamp.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        TextMesh outline = MakeText("Outline", root.transform, new Vector3(0.02f, -0.02f, 0.01f), UiTheme.Ink);
        TextMesh text = MakeText("Text", root.transform, Vector3.zero, Color.white);

        var label = root.AddComponent<StockLabel>();
        var so = new SerializedObject(label);
        so.FindProperty("machine").objectReferenceValue = status;
        so.FindProperty("text").objectReferenceValue = text;
        so.FindProperty("outline").objectReferenceValue = outline;
        so.FindProperty("lamp").objectReferenceValue = lamp;
        so.ApplyModifiedPropertiesWithoutUndo();
        return label;
    }

    static TextMesh MakeText(string name, Transform parent, Vector3 pos, Color color)
    {
        var go = new GameObject(name, typeof(MeshRenderer), typeof(TextMesh));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        TextMesh tm = go.GetComponent<TextMesh>();
        tm.font = UiTheme.Font;
        tm.fontSize = 64;
        tm.characterSize = 0.075f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.text = "0/0 > 0/0";
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = UiTheme.Font.material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return tm;
    }
}
