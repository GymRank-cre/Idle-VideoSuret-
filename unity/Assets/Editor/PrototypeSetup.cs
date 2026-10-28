using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu Tools > Idle Prototype > Build scene
// Construit la scène du prototype : sol, joueur, caméra qui suit,
// zone de ramassage et prefab de caisse. Relançable sans risque.
public static class PrototypeSetup
{
    const string Root = "Assets/Prototype";

    [MenuItem("Tools/Idle Prototype/Build scene")]
    static void Build()
    {
        EnsureFolder(Root);
        EnsureFolder(Root + "/Materials");

        Material groundMat = MakeMaterial("Ground", new Color(0.62f, 0.65f, 0.69f));
        Material playerMat = MakeMaterial("Player", new Color(0.95f, 0.55f, 0.15f));
        Material crateMat = MakeMaterial("Crate", new Color(0.79f, 0.64f, 0.42f));
        Material pileMat = MakeMaterial("Pile", new Color(0.35f, 0.38f, 0.42f));
        Material maxMat = MakeMaterial("Max", new Color(0.85f, 0.2f, 0.2f));

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Sol
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(4f, 1f, 4f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        // Prefab de caisse
        GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crate.name = "Crate";
        crate.transform.localScale = new Vector3(0.5f, 0.3f, 0.4f);
        crate.GetComponent<Renderer>().sharedMaterial = crateMat;
        Object.DestroyImmediate(crate.GetComponent<Collider>());
        GameObject cratePrefab = PrefabUtility.SaveAsPrefabAsset(crate, Root + "/Crate.prefab");
        Object.DestroyImmediate(crate);

        // Joueur
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = new Vector3(0f, 1f, 0f);
        player.GetComponent<Renderer>().sharedMaterial = playerMat;
        Object.DestroyImmediate(player.GetComponent<Collider>());
        player.AddComponent<CharacterController>();
        player.AddComponent<PlayerController>();
        StackCarrier carrier = player.AddComponent<StackCarrier>();

        GameObject anchor = new GameObject("StackAnchor");
        anchor.transform.SetParent(player.transform, false);
        anchor.transform.localPosition = new Vector3(0f, -0.2f, -0.7f);

        GameObject maxIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        maxIndicator.name = "MaxIndicator";
        maxIndicator.transform.SetParent(player.transform, false);
        maxIndicator.transform.localPosition = new Vector3(0f, 2.4f, 0f);
        maxIndicator.transform.localScale = new Vector3(0.8f, 0.3f, 0.05f);
        maxIndicator.GetComponent<Renderer>().sharedMaterial = maxMat;
        Object.DestroyImmediate(maxIndicator.GetComponent<Collider>());
        maxIndicator.SetActive(false);

        var carrierSo = new SerializedObject(carrier);
        carrierSo.FindProperty("stackAnchor").objectReferenceValue = anchor.transform;
        carrierSo.FindProperty("maxIndicator").objectReferenceValue = maxIndicator;
        carrierSo.ApplyModifiedPropertiesWithoutUndo();

        // Caméra
        Camera cam = Camera.main;
        cam.transform.position = new Vector3(0f, 12f, -8f);
        cam.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        CameraFollow follow = cam.gameObject.AddComponent<CameraFollow>();
        var followSo = new SerializedObject(follow);
        followSo.FindProperty("target").objectReferenceValue = player.transform;
        followSo.ApplyModifiedPropertiesWithoutUndo();

        // Zone de ramassage
        GameObject pile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pile.name = "ResourcePile";
        pile.transform.position = new Vector3(6f, 0.1f, 0f);
        pile.transform.localScale = new Vector3(3f, 0.2f, 3f);
        pile.GetComponent<Renderer>().sharedMaterial = pileMat;
        BoxCollider box = pile.GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1f, 10f, 1f); // assez haute pour englober le joueur
        ResourcePile resourcePile = pile.AddComponent<ResourcePile>();
        var pileSo = new SerializedObject(resourcePile);
        pileSo.FindProperty("itemPrefab").objectReferenceValue = cratePrefab;
        pileSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, Root + "/Prototype.unity");
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = player;
        Debug.Log("Scène du prototype créée : " + Root + "/Prototype.unity. Appuie sur Play, déplace-toi avec WASD jusqu'à la zone grise.");
    }

    static Material MakeMaterial(string name, Color color)
    {
        string path = Root + "/Materials/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        var mat = new Material(shader);
        mat.color = color;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
