using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Construit les machines de la scène idle (U2c). Les coordonnées de ce fichier sont celles du web, relatives au coin arrière du poste :
// (x, y, z) du web devient (Z, X, Y) dans Unity. Chaque machine = quelques maillages par couleur + de petits objets mobiles animés
// par un script `IMachineAnim` selon la phase du cycle.
public static class IdleMachineBuilder
{
    const string Wall = "#4F8FD6";

    const float Bevel = 0.035f, Gloss = 0.7f;

    // Boîte aux coordonnées du web (x, y, z, largeur selon x, profondeur selon y, hauteur), arêtes biseautées.
    public static void B(MeshKit k, float x, float y, float z, float w, float d, float h)
    {
        k.BevelBox(new Vector3(y, z, x), new Vector3(d, h, w), Bevel);
    }

    // Pièces d'une machine, fusionnées par objet parent : tout ce qui bouge ensemble n'est qu'un maillage, donc un appel de dessin.
    static readonly Dictionary<Transform, MeshKit> merged = new Dictionary<Transform, MeshKit>();

    static void Flush()
    {
        foreach (var kv in merged) kv.Value.ToObject("Merged", kv.Key, IdleMaterials.Palette());
        merged.Clear();
    }

    static Material M(string hex, float smooth = 0f) { return IdleMaterials.Get("M_" + hex.TrimStart('#'), hex, smooth); }

    static void Part(MeshKit kit, Transform parent, string hex, float gloss = Gloss)
    {
        Part(kit, parent, hex, Matrix4x4.identity, gloss);
    }

    static void Part(MeshKit kit, Transform parent, string hex, Matrix4x4 m, float gloss = Gloss)
    {
        MeshKit into;
        if (!merged.TryGetValue(parent, out into)) { into = new MeshKit(); merged[parent] = into; }
        into.Append(kit, MeshKit.Color(hex, gloss), m);
    }

    // Pièce qui bouge seule : son propre objet (un appel de dessin), mais le matériau commun.
    static GameObject Single(MeshKit kit, string name, Transform parent, string hex)
    {
        var painted = new MeshKit();
        painted.Append(kit, MeshKit.Color(hex, Gloss));
        return painted.ToObject(name, parent, IdleMaterials.Palette());
    }

    // Embase commune à tous les postes (dalle gris clair de 1,7 m).
    static void Platform(Transform root)
    {
        var k = new MeshKit();
        B(k, -0.05f, -0.05f, 0f, 1.7f, 1.7f, 0.12f);
        Part(k, root, "#B8C4D0", 0.3f);
    }

    // Machine du poste `i` ; renvoie la racine (désactivée tant que le poste est verrouillé : c'est IdleStationView qui l'active).
    public static Transform Build(Transform parent, int i)
    {
        var root = new GameObject("Machine").transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(-IdleLayout.StationSize * 0.5f, 0f, -IdleLayout.StationSize * 0.5f);   // coin arrière du poste
        Platform(root);
        Label(root, i);
        switch (i)
        {
            case 0: Reception(root); break;
            case 1: Assembly(root); break;
            case 2: Scanner(root); break;
            case 3: Packer(root); break;
            case 4: Shipping(root); break;
        }
        Flush();
        return root;
    }

    // Étiquette du poste : pastille sombre arrondie avec le nom en blanc, toujours face à la caméra, au-dessus de la machine.
    static readonly float[] LabelHeight = { 1.55f, 1.6f, 2.15f, 2.1f, 1.5f };

    static void Label(Transform root, int i)
    {
        string name = Loc.T(AssetDatabase.LoadAssetAtPath<IdleEconomyConfig>("Assets/Prototype/IdleEconomyConfig.asset").stations[i].nameKey);
        var holder = new GameObject("Label", typeof(FaceCamera)).transform;
        holder.SetParent(root, false);
        holder.localPosition = new Vector3(IdleLayout.StationSize * 0.5f, LabelHeight[i], IdleLayout.StationSize * 0.5f);
        float width = 0.3f + 0.115f * name.Length;
        var pill = new MeshKit();
        pill.Pill(width, 0.34f, 0.01f);   // coins complètement arrondis
        var pillObject = pill.ToObject("Pill", holder, IdleMaterials.Get("LabelPill", "#1E2A38"));
        pillObject.GetComponent<MeshRenderer>().sharedMaterial.SetInt("_Cull", 0);   // visible quel que soit le sens de la face
        IdleSceneBuilder.MakeTextMesh(holder, "Main", name, UiTheme.Font, 80, 0.037f, Vector3.zero, Color.white);
    }

    // ------------------------------------------------------------------ 5. Expédition : quai, camionnette qui part et revient

    static void Shipping(Transform root)
    {
        var wall = new MeshKit();
        B(wall, 0f, 0f, 0.12f, IdleLayout.StationSize, 0.26f, 1.0f);
        Part(wall, root, "#7D93AB");
        var door = new MeshKit();
        B(door, 0.25f, 0.26f, 0.2f, 1.1f, 0.012f, 0.7f);
        Part(door, root, "#22303F");
        var parcel = new MeshKit();
        B(parcel, 1.5f, 1.3f, 0.12f, 0.3f, 0.3f, 0.28f);
        Part(parcel, root, "#E5B96B");

        // camionnette : un objet qui porte tout (corps, cabine, pare-brise, roues) et glisse vers l'avant (axe X)
        var van = new GameObject("Van").transform;
        van.SetParent(root, false);
        var body = new MeshKit();
        B(body, 0.3f, 0.55f, 0.18f, 1.15f, 0.72f, 0.6f);
        Part(body, van, "#FFFFFF");
        var cab = new MeshKit();
        B(cab, 1.45f, 0.6f, 0.18f, 0.5f, 0.6f, 0.42f);
        Part(cab, van, "#1FA0FF");
        var glass = new MeshKit();
        B(glass, 1.95f, 0.67f, 0.32f, 0.012f, 0.43f, 0.18f);
        Part(glass, van, "#BFE3FF");
        foreach (float wx in new[] { 0.6f, 1.2f })   // deux roues du côté visible (y = +0,72 du corps), axe le long de X
        {
            var wk = new MeshKit();
            wk.Cylinder(new Vector3(0f, -0.06f, 0f), 0.17f, 0.12f, 10);
            Part(wk, van, "#1E2A38", Matrix4x4.TRS(new Vector3(0.55f + 0.72f, 0.1f, wx), Quaternion.Euler(0f, 0f, 90f), Vector3.one), 0.2f);
        }

        var machine = root.gameObject.AddComponent<ShipMachine>();
        var so = new SerializedObject(machine);
        so.FindProperty("van").objectReferenceValue = van;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ 4. Emballage : cabine blanche, presse, colis fini

    static void Packer(Transform root)
    {
        var cabinet = new MeshKit();
        B(cabinet, 0.15f, 0.15f, 0.12f, 1.3f, 1.3f, 0.78f);
        Part(cabinet, root, "#F2F5F8");
        var header = new MeshKit();
        B(header, 0.15f, 0.15f, 0.9f, 1.3f, 0.55f, 0.16f);
        Part(header, root, "#FF7A1A");
        var opening = new MeshKit();
        B(opening, 0.3f, 0.7f, 0.35f, 1.0f, 0.012f, 0.43f);
        Part(opening, root, "#22303F");
        var lamp = new MeshKit();
        lamp.Cylinder(new Vector3(0.35f, 1.06f, 1.2f), 0.16f, 0.18f, 8);
        Part(lamp, root, "#FFC400");

        // piston et plateau : maillages d'origine (0,0,0) ; l'animation règle leur hauteur
        Transform ram = MovingBox(root, "Ram", new Vector3(0.72f, 0f, 0.58f), new Vector3(0.44f, 0.7f, 0.44f), "#B5C0CC");
        Transform plate = MovingBox(root, "Plate", new Vector3(0.54f, 0f, 0.4f), new Vector3(0.8f, 0.14f, 0.8f), "#8F9BA9");

        var package = new GameObject("Package").transform;
        package.SetParent(root, false);
        var box = new MeshKit();
        B(box, 0.6f, 1.05f, 0.14f, 0.36f, 0.36f, 0.3f);
        Part(box, package, "#E5B96B");

        var machine = root.gameObject.AddComponent<PackMachine>();
        var so = new SerializedObject(machine);
        so.FindProperty("ram").objectReferenceValue = ram;
        so.FindProperty("plate").objectReferenceValue = plate;
        so.FindProperty("package").objectReferenceValue = package.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Boîte dont l'origine est son coin bas (position XZ fixée ici, hauteur réglée plus tard) ; taille donnée en (profondeur X, hauteur, largeur Z).
    static Transform MovingBox(Transform parent, string name, Vector3 positionXZ, Vector3 size, string hex)
    {
        var kit = new MeshKit();
        kit.BevelBox(Vector3.zero, size, 0.02f);
        GameObject go = Single(kit, name, parent, hex);
        go.transform.localPosition = positionXZ;
        return go.transform;
    }

    // ------------------------------------------------------------------ 3. Test : portique bleu, faisceau qui balaie, écran de résultat

    static void Scanner(Transform root)
    {
        var posts = new MeshKit();
        B(posts, 0f, 0.05f, 0.12f, 0.3f, 1.5f, 0.9f);
        B(posts, 1.3f, 0.05f, 0.12f, 0.3f, 1.5f, 0.9f);
        Part(posts, root, "#2F7FD0");
        var beam = new MeshKit();
        B(beam, -0.02f, 0.05f, 1.02f, IdleLayout.StationSize + 0.04f, 1.5f, 0.34f);
        Part(beam, root, "#4FB3FF");
        var box = new MeshKit();
        B(box, 0.5f, 0.5f, 1.36f, 0.6f, 0.16f, 0.46f);
        Part(box, root, "#1E2A38");

        Material cyan = M("#27D3E6"), green = M("#3DDC5A"), yellow = M("#FFC400");
        var screenKit = new MeshKit();
        B(screenKit, 0.54f, 0.66f, 1.4f, 0.52f, 0.012f, 0.38f);
        GameObject screen = screenKit.ToObject("Screen", root, cyan);
        var beaconKit = new MeshKit();
        beaconKit.Cylinder(new Vector3(0.2f, 1.36f, 0.22f), 0.09f, 0.1f, 8);
        GameObject beacon = beaconKit.ToObject("Beacon", root, yellow);

        // faisceau : plan vertical cyan translucide (boîte très fine, centrée) déplacé par l'animation
        var scan = new MeshKit();
        scan.Box(new Vector3(-0.012f, -0.42f, -0.5f), new Vector3(0.024f, 0.84f, 1.0f));
        GameObject plane = scan.ToObject("Scan", root, IdleMaterials.Get("ScanBeam", "#27D3E6", 0f, true, 0.5f));

        var machine = root.gameObject.AddComponent<TestMachine>();
        var so = new SerializedObject(machine);
        so.FindProperty("scanPlane").objectReferenceValue = plane.transform;
        so.FindProperty("screen").objectReferenceValue = screen.GetComponent<Renderer>();
        so.FindProperty("beacon").objectReferenceValue = beacon.GetComponent<Renderer>();
        so.FindProperty("okMaterial").objectReferenceValue = green;
        so.FindProperty("screenWaiting").objectReferenceValue = cyan;
        so.FindProperty("beaconWaiting").objectReferenceValue = yellow;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ 2. Assemblage : établi, bras orange, pupitre, caméra blanche

    static void Assembly(Transform root)
    {
        var bench = new MeshKit();
        B(bench, 0.1f, 0.55f, 0.12f, 1.4f, 0.85f, 0.5f);
        Part(bench, root, "#8BA0B6");
        var top = new MeshKit();
        B(top, 0.1f, 0.55f, 0.62f, 1.4f, 0.85f, 0.06f);
        Part(top, root, "#C3CCD7");
        var baseKit = new MeshKit();
        baseKit.Cylinder(new Vector3(0.8f, 0.68f, 0.55f), 0.2f, 0.22f, 10);
        Part(baseKit, root, "#FF7A1A");

        // pupitre de commande avec son écran qui clignote
        var console = new MeshKit();
        B(console, 0.1f, 0.12f, 0.12f, 0.5f, 0.3f, 0.75f);
        Part(console, root, "#2B3C50");
        var screenKit = new MeshKit();
        B(screenKit, 0.15f, 0.42f, 0.45f, 0.4f, 0.012f, 0.35f);
        Material screenA = M("#27D3E6"), screenB = M("#1FA0FF");
        GameObject screen = screenKit.ToObject("Screen", root, screenA);

        // bras : deux segments (maillage unité le long de Z) et deux articulations jaunes
        Transform upper = UnitSegment(root, "Upper", 0.24f, "#FF7A1A");
        Transform fore = UnitSegment(root, "Fore", 0.2f, "#FFA24D");
        Transform elbow = Joint(root, "Elbow", 0.3f, "#FFC400");
        Transform hand = Joint(root, "Hand", 0.24f, "#FFC400");

        // caméra blanche déposée sur l'établi à mi-cycle
        var camera = new GameObject("Camera").transform;
        camera.SetParent(root, false);
        var body = new MeshKit();
        B(body, 1.15f, 0.85f, 0.68f, 0.38f, 0.3f, 0.2f);
        Part(body, camera, "#F2F5F8");
        var lens = new MeshKit();
        lens.Cylinder(new Vector3(1.0f, 0.88f, 1.34f), 0.08f, 0.06f, 8);
        Part(lens, camera, "#1FA0FF");

        var machine = root.gameObject.AddComponent<AssemblyMachine>();
        var so = new SerializedObject(machine);
        so.FindProperty("upper").objectReferenceValue = upper;
        so.FindProperty("fore").objectReferenceValue = fore;
        so.FindProperty("elbowJoint").objectReferenceValue = elbow;
        so.FindProperty("handJoint").objectReferenceValue = hand;
        so.FindProperty("product").objectReferenceValue = camera.gameObject;
        so.FindProperty("screen").objectReferenceValue = screen.GetComponent<Renderer>();
        so.FindProperty("screenA").objectReferenceValue = screenA;
        so.FindProperty("screenB").objectReferenceValue = screenB;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Barre de section carrée de longueur 1 centrée sur l'origine, le long de Z (mise à l'échelle en Z par l'animation).
    static Transform UnitSegment(Transform parent, string name, float thickness, string hex)
    {
        var kit = new MeshKit();
        kit.BevelBox(new Vector3(-thickness * 0.5f, -thickness * 0.5f, -0.5f), new Vector3(thickness, thickness, 1f), 0.03f);
        return Single(kit, name, parent, hex).transform;
    }

    static Transform Joint(Transform parent, string name, float size, string hex)
    {
        var kit = new MeshKit();
        kit.BevelBox(new Vector3(-size * 0.5f, -size * 0.5f, -size * 0.5f), new Vector3(size, size, size), 0.05f);
        return Single(kit, name, parent, hex).transform;
    }

    // ------------------------------------------------------------------ 1. Réception : quai, porte à lamelles, gyrophare, palette de caisses

    static void Reception(Transform root)
    {
        var wall = new MeshKit();
        B(wall, 0f, 0f, 0.12f, IdleLayout.StationSize, 0.26f, 1.1f);
        Part(wall, root, Wall);
        var door = new MeshKit();
        B(door, 0.3f, 0.26f, 0.2f, 1.0f, 0.012f, 0.75f);
        Part(door, root, "#22303F");
        var slats = new MeshKit();
        for (int s = 1; s < 5; s++) B(slats, 0.3f, 0.27f, 0.2f + 0.75f * s / 5f, 1.0f, 0.008f, 0.014f);
        Part(slats, root, "#4A5A6C");

        // gyrophare : un cylindre dont le matériau alterne
        var beaconKit = new MeshKit();
        beaconKit.Cylinder(new Vector3(0.13f, 1.22f, IdleLayout.StationSize - 0.22f), 0.1f, 0.12f, 8);
        Material on = M("#FFC400"), off = M("#B58A00");
        GameObject beacon = beaconKit.ToObject("Beacon", root, on);

        var pallet = new MeshKit();
        B(pallet, 0.35f, 0.6f, 0.12f, 0.95f, 0.95f, 0.1f);
        Part(pallet, root, "#B07A3A");

        // 4 caisses sur la palette (la 4e en dernier) : boîte + ruban d'emballage, mises à l'échelle en hauteur par l'animation
        var crates = new Transform[4];
        for (int q = 0; q < 4; q++)
        {
            float qx = 0.42f + (q % 2) * 0.45f, qy = 0.67f + (q / 2) * 0.45f;
            var crate = new GameObject("Crate_" + q).transform;
            crate.SetParent(root, false);
            crate.localPosition = new Vector3(qy + 0.2f, 0.22f, qx + 0.2f);
            var box = new MeshKit();
            box.BevelBox(new Vector3(-0.2f, 0f, -0.2f), new Vector3(0.4f, 0.36f, 0.4f), 0.03f);
            Part(box, crate, "#D9A25B", 0.15f);
            var tape = new MeshKit();
            tape.Box(new Vector3(-0.2f, 0.36f, -0.05f), new Vector3(0.4f, 0.012f, 0.1f));
            Part(tape, crate, "#FFC400", 0.15f);
            crates[q] = crate;
        }

        var machine = root.gameObject.AddComponent<ReceptionMachine>();
        var so = new SerializedObject(machine);
        SerializedProperty list = so.FindProperty("crates");
        list.arraySize = 4;
        for (int q = 0; q < 4; q++) list.GetArrayElementAtIndex(q).objectReferenceValue = crates[q];
        so.FindProperty("beacon").objectReferenceValue = beacon.GetComponent<Renderer>();
        so.FindProperty("beaconOn").objectReferenceValue = on;
        so.FindProperty("beaconOff").objectReferenceValue = off;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
