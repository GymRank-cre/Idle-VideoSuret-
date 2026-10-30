using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Construit la scène du jeu idle (`Tools > Idle Prototype > Build scene`). U2a : caméra, dalle en damier à bord jaune, décor (herbe,
// arbres, route, voitures) et 5 emplacements de postes en fantômes avec cadenas. Les machines, tapis et plaques arrivent en U2b/U2c.
// Tout ce qui partage une couleur est fusionné en un seul maillage (peu d'appels de dessin) ; aucune ombre temps réel.
public static class IdleSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Idle.unity";
    const string ConfigPath = "Assets/Prototype/IdleEconomyConfig.asset";
    const float GroundY = -IdleLayout.SlabThickness;

    [MenuItem("Tools/Idle Prototype/Build scene")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildLighting();
        BuildCamera();
        BuildHost();
        var world = new GameObject("World").transform;
        BuildGround(world);
        BuildSlab(world);
        BuildRoad(world);
        BuildTrees(world);
        BuildSlots(world);
        BuildCars(world);
        EditorSceneManager.SaveScene(scene, ScenePath);
        WebGLBuilder.EnsureSceneInBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("Scène idle créée : " + ScenePath);
    }

    // ------------------------------------------------------------------ éclairage, caméra, jeu

    static void BuildLighting()
    {
        var lightGo = new GameObject("Directional Light", typeof(Light));
        lightGo.transform.rotation = Quaternion.Euler(52f, 150f, 0f);
        var light = lightGo.GetComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color32(0xFF, 0xF1, 0xD6, 0xFF);
        light.intensity = 0.92f;
        light.shadows = LightShadows.None;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.78f, 0.88f, 1f) * 0.74f;
        RenderSettings.ambientEquatorColor = new Color(0.97f, 0.95f, 0.9f) * 0.76f;
        RenderSettings.ambientGroundColor = new Color(0.72f, 0.82f, 0.7f) * 0.64f;
    }

    static void BuildCamera()
    {
        var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(UniversalAdditionalCameraData), typeof(IdleCameraRig));
        go.tag = "MainCamera";
        var cam = go.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0x8B, 0xCB, 0x6B, 0xFF);   // le vert de l'herbe
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 400f;
        cam.fieldOfView = 30f;
        go.GetComponent<UniversalAdditionalCameraData>().renderShadows = false;
    }

    static void BuildHost()
    {
        var go = new GameObject("Idle", typeof(IdleHost));
        var so = new SerializedObject(go.GetComponent<IdleHost>());
        so.FindProperty("config").objectReferenceValue = AssetDatabase.LoadAssetAtPath<IdleEconomyConfig>(ConfigPath);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ sol, dalle, route

    static void BuildGround(Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Grass";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(3f, GroundY - 0.1f, 4f);
        go.transform.localScale = new Vector3(160f, 0.2f, 160f);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = IdleMaterials.Get("Grass", "#8BCB6B");
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    static void BuildSlab(Transform parent)
    {
        float w = IdleLayout.FloorDepth, d = IdleLayout.FloorWidth, t = IdleLayout.SlabThickness;
        var slab = new GameObject("Slab").transform;
        slab.SetParent(parent, false);

        var body = new MeshKit();
        body.Box(new Vector3(0, -t, 0), new Vector3(w, t - 0.01f, d));
        body.ToObject("Body", slab, IdleMaterials.Get("SlabSide", "#98A6B4"));

        var top = new MeshKit();
        top.Box(new Vector3(0, -0.01f, 0), new Vector3(w, 0.01f, d));
        top.ToObject("Top", slab, IdleMaterials.Get("SlabTop", "#D5DEE7"));

        // damier : une case sur deux, cases de 1 m (les dernières sont coupées par le bord)
        var checker = new MeshKit();
        for (int x = 0; x < Mathf.CeilToInt(IdleLayout.FloorWidth); x++)
            for (int y = 0; y < Mathf.CeilToInt(IdleLayout.FloorDepth); y++)
            {
                if ((x + y) % 2 != 0) continue;
                float cw = Mathf.Min(1f, IdleLayout.FloorWidth - x), cd = Mathf.Min(1f, IdleLayout.FloorDepth - y);
                checker.FlatQuad(IdleLayout.ToWorld(x + cw * 0.5f, y + cd * 0.5f, 0.004f), cd, cw);
            }
        checker.ToObject("Checker", slab, IdleMaterials.Get("SlabChecker", "#C2CEDB"));

        // bord jaune posé à 12 cm du bord
        var border = new MeshKit();
        float inset = 0.12f, lw = 0.07f, y0 = 0.006f;
        border.Box(new Vector3(inset, y0, inset), new Vector3(w - 2 * inset, 0.004f, lw));
        border.Box(new Vector3(inset, y0, d - inset - lw), new Vector3(w - 2 * inset, 0.004f, lw));
        border.Box(new Vector3(inset, y0, inset), new Vector3(lw, 0.004f, d - 2 * inset));
        border.Box(new Vector3(w - inset - lw, y0, inset), new Vector3(lw, 0.004f, d - 2 * inset));
        border.ToObject("Border", slab, IdleMaterials.Get("SlabBorder", "#FFC400"));
    }

    static void BuildRoad(Transform parent)
    {
        // route derrière la dalle : web x 8,6 -> 10,2 (Z), sur toute la longueur (X)
        var road = new GameObject("Road").transform;
        road.SetParent(parent, false);
        float y = GroundY + 0.01f;
        var asphalt = new MeshKit();
        asphalt.Box(new Vector3(-8f, y, 8.6f), new Vector3(24f, 0.01f, 1.6f));
        asphalt.ToObject("Asphalt", road, IdleMaterials.Get("Asphalt", "#5C6773"));
        var edges = new MeshKit();
        edges.Box(new Vector3(-8f, y + 0.011f, 8.48f), new Vector3(24f, 0.004f, 0.12f));
        edges.Box(new Vector3(-8f, y + 0.011f, 10.2f), new Vector3(24f, 0.004f, 0.12f));
        edges.ToObject("Edges", road, IdleMaterials.Get("RoadEdge", "#EDE6D2"));
        var dashes = new MeshKit();
        for (float x = -8f; x < 16f; x += 1.6f) dashes.Box(new Vector3(x, y + 0.011f, 9.38f), new Vector3(0.8f, 0.004f, 0.06f));
        dashes.ToObject("Dashes", road, IdleMaterials.Get("RoadDash", "#FFD84A"));
    }

    // ------------------------------------------------------------------ arbres (même tirage que le web : graine 7)

    static void BuildTrees(Transform parent)
    {
        var trees = new GameObject("Trees").transform;
        trees.SetParent(parent, false);
        long seed = 7;
        System.Func<float> r = () => { seed = (seed * 16807) % 2147483647; return seed / 2147483647f; };
        var groups = new Dictionary<string, MeshKit>();
        MeshKit Group(string hex) { MeshKit m; if (!groups.TryGetValue(hex, out m)) { m = new MeshKit(); groups[hex] = m; } return m; }
        string[] pineDark = { "#2FA55B", "#1E8A4C", "#27994F" }, pineLight = { "#6BD65A", "#3FB84A" };
        for (int n = 0; n < 110; n++)
        {
            float x = -4f + r() * 16f, yy = -4f + r() * 14f;
            bool inFloor = x > -0.9f && x < IdleLayout.FloorWidth + 0.9f && yy > -0.9f && yy < IdleLayout.FloorDepth + 1.1f;
            bool onRoad = x > 8.2f && x < 10.8f;
            if (inFloor || onRoad) continue;
            float s = (0.5f + r() * 0.3f) * 1.5f;
            bool light = r() < 0.25f;
            Vector3 b = IdleLayout.ToWorld(x, yy, GroundY);
            Group("#8A5A2B").Cylinder(b, 0.07f * s, 0.4f * s);
            string[] cols = light ? pineLight : pineDark;
            int cones = light ? 2 : 3;
            for (int i = 0; i < cones; i++)
                Group(cols[i % cols.Length]).Cone(b + new Vector3(0, (0.3f + i * 0.34f) * s, 0), (0.5f - i * 0.1f) * s, 0.62f * s);
        }
        foreach (var kv in groups) kv.Value.ToObject("Trees_" + kv.Key.TrimStart('#'), trees, IdleMaterials.Get("Tree_" + kv.Key.TrimStart('#'), kv.Key));
    }

    // ------------------------------------------------------------------ emplacements fantômes

    static void BuildSlots(Transform parent)
    {
        var slots = new GameObject("Slots").transform;
        slots.SetParent(parent, false);
        Material ghost = IdleMaterials.Get("Ghost", "#CFE3F6", 0f, true, 0.42f);
        Material ghostEdge = IdleMaterials.Get("GhostEdge", "#FFFFFF", 0f, true, 0.7f);
        Sprite lockSprite = UiSprites.Picto("lock");
        for (int i = 0; i < IdleLayout.Stations.Length; i++)
        {
            var slot = new GameObject("Slot_" + i).transform;
            slot.SetParent(slots, false);
            slot.position = IdleLayout.StationCenter(i);
            float s = IdleLayout.StationSize;
            var body = new MeshKit();
            body.Box(new Vector3(-s * 0.5f, 0.01f, -s * 0.5f), new Vector3(s, 0.9f, s));
            body.ToObject("Ghost", slot, ghost);
            var edge = new MeshKit();   // cadre blanc au sol
            float e = 0.05f;
            edge.Box(new Vector3(-s * 0.5f, 0.012f, -s * 0.5f), new Vector3(s, 0.004f, e));
            edge.Box(new Vector3(-s * 0.5f, 0.012f, s * 0.5f - e), new Vector3(s, 0.004f, e));
            edge.Box(new Vector3(-s * 0.5f, 0.012f, -s * 0.5f), new Vector3(e, 0.004f, s));
            edge.Box(new Vector3(s * 0.5f - e, 0.012f, -s * 0.5f), new Vector3(e, 0.004f, s));
            edge.ToObject("Frame", slot, ghostEdge);
            if (i == 0) continue;   // la Réception est ouverte dès le départ : pas de cadenas
            var lockGo = new GameObject("Lock", typeof(SpriteRenderer), typeof(FaceCamera));
            lockGo.transform.SetParent(slot, false);
            lockGo.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            lockGo.transform.localScale = Vector3.one * 0.55f;
            var sr = lockGo.GetComponent<SpriteRenderer>();
            sr.sprite = lockSprite;
            sr.color = new Color32(0xB8, 0xC2, 0xCC, 0xFF);
        }
    }

    // ------------------------------------------------------------------ voitures qui passent sur la route

    static void BuildCars(Transform parent)
    {
        var cars = new GameObject("Cars").transform;
        cars.SetParent(parent, false);
        MakeCar(cars, "CarRed", "#F2453D", new Vector3(-9f, 0f, 8.78f), new Vector3(17f, 0f, 8.78f), 15f, 8f, 3f);
        MakeCar(cars, "CarBlue", "#1FA0FF", new Vector3(17f, 0f, 9.52f), new Vector3(-9f, 0f, 9.52f), 21f, 9f, 11f);
    }

    static void MakeCar(Transform parent, string name, string hex, Vector3 from, Vector3 to, float interval, float travel, float first)
    {
        float y = GroundY + 0.02f;
        from.y = to.y = y;
        var car = new GameObject(name, typeof(CarLoop)).transform;
        car.SetParent(parent, false);
        car.position = from;
        Material body = IdleMaterials.Get("Car_" + hex.TrimStart('#'), hex, 0.15f);
        Material glass = IdleMaterials.Get("CarGlass", "#BFE6FF", 0.5f);
        RoundedBox.Make(car, "Body", new Vector3(0f, 0.17f, 0f), new Vector3(0.55f, 0.34f, 1.0f), 0.07f, body);
        RoundedBox.Make(car, "Cabin", new Vector3(0f, 0.45f, -0.05f), new Vector3(0.46f, 0.26f, 0.55f), 0.07f, glass);
        var so = new SerializedObject(car.GetComponent<CarLoop>());
        so.FindProperty("from").vector3Value = from;
        so.FindProperty("to").vector3Value = to;
        so.FindProperty("interval").floatValue = interval;
        so.FindProperty("travelTime").floatValue = travel;
        so.FindProperty("firstDelay").floatValue = first;
        so.ApplyModifiedPropertiesWithoutUndo();
        car.rotation = Quaternion.LookRotation((to - from).normalized);   // le modèle est long selon Z local
    }
}
