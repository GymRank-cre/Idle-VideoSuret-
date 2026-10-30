using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Construit les machines de la scène idle (U2c, UW-3 ; la serre : seulement sa charpente). Les coordonnées de ce fichier sont celles du web, relatives au coin arrière du poste :
// (x, y, z) du web devient (Z, X, Y) dans Unity. Chaque machine a un corps fixe en plusieurs aspects (un maillage par aspect, échangés selon le niveau)
// et de petites pièces mobiles animées par un script `IMachineAnim` selon la phase du cycle.
public static class IdleMachineBuilder
{
    const float Bevel = 0.035f, Gloss = 0.7f;
    const string Yellow = "#FFC400", Ink = "#1E2A38", Screen = "#27D3E6", Metal = "#C3CCD7";

    // couleur principale par aspect, la même pour tous les postes : le joueur voit l'usine monter en gamme
    static readonly string[] Paint = { "#2F7BE0", "#16A5B8", "#7A55E0", "#E0483E", "#F0B21C" };

    public const int AspectCount = 5;

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

    static void Part(MeshKit kit, Transform parent, Color32 color, Matrix4x4 m)
    {
        MeshKit into;
        if (!merged.TryGetValue(parent, out into)) { into = new MeshKit(); merged[parent] = into; }
        into.Append(kit, color, m);
    }

    static void Part(MeshKit kit, Transform parent, string hex, float gloss = Gloss) { Part(kit, parent, MeshKit.Color(hex, gloss), Matrix4x4.identity); }

    // Une boîte d'une couleur, ajoutée au maillage de `parent`.
    static void Box(Transform parent, string hex, float x, float y, float z, float w, float d, float h, float gloss = Gloss)
    {
        var k = new MeshKit();
        B(k, x, y, z, w, d, h);
        Part(k, parent, hex, gloss);
    }

    // Pièce fine, sans biseau.
    static void Flat(Transform parent, string hex, float x, float y, float z, float w, float d, float h, float gloss = Gloss)
    {
        var k = new MeshKit();
        k.Box(new Vector3(y, z, x), new Vector3(d, h, w));
        Part(k, parent, hex, gloss);
    }

    static void Round(Transform parent, string hex, float x, float y, float z, float radius, float h, int segments = 8, float gloss = Gloss)
    {
        var k = new MeshKit();
        k.Cylinder(new Vector3(y, z, x), radius, h, segments);
        Part(k, parent, hex, gloss);
    }

    static Color32 Shade(string hex, float amount, float gloss = Gloss)
    {
        Color32 c = MeshKit.Color(hex, gloss);
        byte a = c.a;
        c = amount >= 0f ? Color32.Lerp(c, new Color32(255, 255, 255, 255), amount) : Color32.Lerp(c, new Color32(0, 0, 0, 255), -amount);
        c.a = a;
        return c;
    }

    // Bande de sécurité jaune et noire, couchée sur le dessus d'une pièce ou posée sur une face.
    static void Hazard(Transform root, float x, float y, float z, float length, bool alongX, float width = 0.07f, float height = 0.012f)
    {
        const float Step = 0.16f;
        int n = Mathf.Max(1, Mathf.RoundToInt(length / Step));
        float s = length / n;
        for (int i = 0; i < n; i++)
        {
            string hex = i % 2 == 0 ? Yellow : Ink;
            if (alongX) Flat(root, hex, x + i * s, y, z, s, width, height, 0.3f);
            else Flat(root, hex, x, y + i * s, z, width, s, height, 0.3f);
        }
    }

    // Trois petits boutons colorés sur une console.
    static void Buttons(Transform root, float x, float y, float z, float spacing)
    {
        string[] hex = { "#3DDC5A", "#FFC400", "#F2453D" };
        for (int i = 0; i < hex.Length; i++) Flat(root, hex[i], x + i * spacing, y, z, 0.07f, 0.02f, 0.07f, 0.9f);
    }

    static void Antenna(Transform root, float x, float y, float z)
    {
        Round(root, Metal, x, y, z, 0.022f, 0.42f, 6);
        Round(root, Metal, x, y, z + 0.26f, 0.11f, 0.03f, 8);
        Round(root, "#F2453D", x, y, z + 0.42f, 0.04f, 0.06f, 6, 0.9f);
    }

    // Embase commune : plus l'aspect monte, plus elle s'équipe (bandes de sécurité, balises, liseré doré).
    static void Platform(Transform root, int aspect)
    {
        Box(root, aspect >= 4 ? "#F0D060" : "#B8C4D0", -0.05f, -0.05f, 0f, 1.7f, 1.7f, 0.12f, 0.3f);
        if (aspect >= 1)
        {
            Hazard(root, -0.02f, 1.54f, 0.12f, 1.64f, true);
            Hazard(root, 1.54f, -0.02f, 0.12f, 1.56f, false);
        }
        if (aspect >= 2)
        {
            foreach (float x in new[] { -0.02f, 1.5f })
            {
                Box(root, Ink, x, 1.5f, 0.12f, 0.12f, 0.12f, 0.1f);
                Box(root, "#8DF1FF", x + 0.02f, 1.52f, 0.22f, 0.08f, 0.08f, 0.1f, 1f);
            }
        }
    }

    // Machine du poste `i` ; renvoie la racine (désactivée tant que le poste est verrouillé : c'est IdleStationView qui l'active).
    public static Transform Build(Transform parent, int i)
    {
        var root = new GameObject("Machine").transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(-IdleLayout.StationSize * 0.5f, 0f, -IdleLayout.StationSize * 0.5f);   // coin arrière du poste
        Label(root, i);

        var meshes = new Mesh[AspectCount];
        for (int a = 0; a < AspectCount; a++)
        {
            Platform(root, a);
            switch (i)
            {
                case 0: SerreBody(root, a); break;
                case 1: AssemblyBody(root, a); break;
                case 2: ScannerBody(root, a); break;
                case 3: PackerBody(root, a); break;
                case 4: ShippingBody(root, a); break;
            }
            meshes[a] = merged[root].ToMesh("Machine" + i + "_Aspect" + a);
            merged.Remove(root);
        }

        switch (i)
        {
            case 1: AssemblyParts(root); break;
            case 2: ScannerParts(root); break;
            case 3: PackerParts(root); break;
            case 4: ShippingParts(root); break;
        }
        Flush();

        var body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
        body.transform.SetParent(root, false);
        body.GetComponent<MeshFilter>().sharedMesh = meshes[0];
        var renderer = body.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = IdleMaterials.Palette();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var aspect = root.gameObject.AddComponent<IdleMachineAspect>();
        var so = new SerializedObject(aspect);
        so.FindProperty("station").intValue = i;
        so.FindProperty("body").objectReferenceValue = body.GetComponent<MeshFilter>();
        so.FindProperty("bodyRenderer").objectReferenceValue = renderer;
        SerializedProperty list = so.FindProperty("aspects");
        list.arraySize = AspectCount;
        for (int a = 0; a < AspectCount; a++) list.GetArrayElementAtIndex(a).objectReferenceValue = meshes[a];
        so.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    static Material M(string hex, float smooth = 0f) { return IdleMaterials.Get("M_" + hex.TrimStart('#'), hex, smooth); }

    // Pièce qui bouge seule : son propre objet (un appel de dessin), mais le matériau commun.
    static GameObject Single(MeshKit kit, string name, Transform parent, string hex)
    {
        var painted = new MeshKit();
        painted.Append(kit, MeshKit.Color(hex, Gloss));
        return painted.ToObject(name, parent, IdleMaterials.Palette());
    }

    // Étiquette du poste : pastille sombre arrondie avec le nom en blanc, toujours face à la caméra, au-dessus de la machine.
    static readonly float[] LabelHeight = { 1.7f, 1.6f, 1.15f, 2.1f, 1.6f };   // Test : posée sur le portique, pour ne pas couvrir les prix de l'Assemblage juste derrière

    static void Label(Transform root, int i)
    {
        string name = Loc.T(AssetDatabase.LoadAssetAtPath<IdleEconomyConfig>("Assets/Prototype/IdleEconomyConfig.asset").stations[i].nameKey);
        var holder = new GameObject("Label", typeof(FaceCamera)).transform;
        holder.SetParent(root, false);
        holder.localPosition = new Vector3(IdleLayout.StationSize * 0.5f, LabelHeight[i], IdleLayout.StationSize * 0.5f);

        var pills = new Mesh[3];
        for (int digits = 1; digits <= pills.Length; digits++)
        {
            int length = Loc.T("label.level", name, 0).Length + digits - 1;
            var pill = new MeshKit();
            pill.Pill(0.34f + 0.142f * length, 0.34f, 0.01f);   // coins complètement arrondis
            pills[digits - 1] = pill.ToMesh("LabelPill_" + i + "_" + digits);
        }
        // dessinée après les plaques : jamais coupée par l'une d'elles
        var pillObject = new MeshKit().ToObject("Pill", holder, IdleMaterials.Overlay("LabelPill", "#1E2A38", 3541));
        pillObject.GetComponent<MeshFilter>().sharedMesh = pills[0];
        TextMesh text = IdleSceneBuilder.MakeTextMesh(holder, "Main", name, UiTheme.Font, 80, 0.037f, Vector3.zero, Color.white, true);
        text.GetComponent<MeshRenderer>().sharedMaterial = IdleSceneBuilder.LabelFontMaterial(UiTheme.Font);

        var label = holder.gameObject.AddComponent<IdleStationLabel>();
        var so = new SerializedObject(label);
        so.FindProperty("station").intValue = i;
        so.FindProperty("stationName").stringValue = name;
        so.FindProperty("text").objectReferenceValue = text;
        so.FindProperty("pill").objectReferenceValue = pillObject.GetComponent<MeshFilter>();
        SerializedProperty list = so.FindProperty("pillByDigits");
        list.arraySize = pills.Length;
        for (int d = 0; d < pills.Length; d++) list.GetArrayElementAtIndex(d).objectReferenceValue = pills[d];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static MeshKit RoofKit2(int a)
    {
        var k = new MeshKit();
        B(k, 0.15f, 0.15f, 0.98f, 1.3f, 0.55f, 0.1f);
        return k;
    }

    static MeshKit RoofKit(float z)
    {
        var k = new MeshKit();
        B(k, -0.03f, -0.03f, z, IdleLayout.StationSize + 0.06f, 0.32f, 0.08f);
        return k;
    }

    // ------------------------------------------------------------------ 5. Expédition : quai, camionnette qui part et revient

    static void ShippingBody(Transform root, int a)
    {
        string wall = Paint[a];
        Box(root, wall, 0f, 0f, 0.12f, IdleLayout.StationSize, 0.26f, 1.0f);
        Part(RoofKit(1.12f), root, Shade(wall, -0.25f), Matrix4x4.identity);
        Box(root, Yellow, 0.2f, 0.26f, 0.14f, 1.2f, 0.02f, 0.82f);
        Flat(root, "#22303F", 0.25f, 0.27f, 0.2f, 1.1f, 0.012f, 0.7f);
        for (int s = 1; s < 6; s++) Flat(root, "#4A5A6C", 0.25f, 0.28f, 0.2f + 0.7f * s / 6f, 1.1f, 0.008f, 0.016f);
        Box(root, Yellow, 0.06f, 0.3f, 0.12f, 0.1f, 0.1f, 0.42f);
        Box(root, Yellow, 1.44f, 0.3f, 0.12f, 0.1f, 0.1f, 0.42f);
        Box(root, "#E5B96B", 1.5f, 1.3f, 0.12f, 0.3f, 0.3f, 0.28f, 0.15f);
        Flat(root, Yellow, 1.5f, 1.42f, 0.4f, 0.3f, 0.06f, 0.012f, 0.15f);
        Hazard(root, 0.02f, 1.3f, 0.12f, 0.9f, true, 0.07f);

        // auvent de la couleur de l'aspect au-dessus du quai, sur deux poteaux du côté où la camionnette ne passe pas
        Box(root, Metal, 0.08f, 0.3f, 0.12f, 0.07f, 0.07f, 0.9f);
        Box(root, Metal, 0.08f, 0.8f, 0.12f, 0.07f, 0.07f, 0.9f);
        Box(root, wall, 0f, 0.26f, 1.0f, IdleLayout.StationSize, 0.68f, 0.1f);
        Flat(root, Yellow, 0f, 0.94f, 1.03f, IdleLayout.StationSize, 0.012f, 0.05f, 0.9f);
        if (a >= 2)   // module : plus de colis qui attendent
        {
            Box(root, "#E5B96B", 1.15f, 1.3f, 0.12f, 0.3f, 0.3f, 0.28f, 0.15f);
            Box(root, "#D9A25B", 1.32f, 1.33f, 0.4f, 0.3f, 0.26f, 0.24f, 0.15f);
        }
        if (a >= 3) Antenna(root, 1.3f, 0.12f, 1.2f);
    }

    static void ShippingParts(Transform root)
    {
        // camionnette : un objet qui porte tout (corps, cabine, pare-brise, roues) et glisse vers l'avant (axe X)
        var van = new GameObject("Van").transform;
        van.SetParent(root, false);
        Box(van, "#FFFFFF", 0.3f, 0.55f, 0.18f, 1.15f, 0.72f, 0.6f, 0.9f);
        Flat(van, "#1FA0FF", 0.3f, 1.27f, 0.42f, 1.15f, 0.01f, 0.1f);              // bande bleue
        Box(van, "#1FA0FF", 1.45f, 0.6f, 0.18f, 0.5f, 0.6f, 0.42f);
        Box(van, "#FFFFFF", 1.45f, 0.6f, 0.6f, 0.34f, 0.6f, 0.14f, 0.9f);
        Flat(van, "#BFE3FF", 1.95f, 0.67f, 0.32f, 0.012f, 0.43f, 0.18f, 1f);
        Flat(van, "#FFE27A", 1.95f, 0.64f, 0.22f, 0.012f, 0.08f, 0.06f, 1f);       // phares
        Flat(van, "#FFE27A", 1.95f, 1.1f, 0.22f, 0.012f, 0.08f, 0.06f, 1f);
        foreach (float wx in new[] { 0.6f, 1.2f })   // deux roues du côté visible (y = +0,72 du corps), axe le long de X
        {
            var wk = new MeshKit();
            wk.Cylinder(new Vector3(0f, -0.06f, 0f), 0.17f, 0.12f, 10);
            Part(wk, van, MeshKit.Color(Ink, 0.2f), Matrix4x4.TRS(new Vector3(0.55f + 0.72f, 0.1f, wx), Quaternion.Euler(0f, 0f, 90f), Vector3.one));
        }

        var machine = root.gameObject.AddComponent<ShipMachine>();
        var so = new SerializedObject(machine);
        so.FindProperty("van").objectReferenceValue = van;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ 4. Emballage : cabine blanche, presse, colis fini

    static void PackerBody(Transform root, int a)
    {
        Box(root, "#F2F5F8", 0.15f, 0.15f, 0.12f, 1.3f, 1.3f, 0.78f);            // façade crème
        Box(root, Paint[a], 0.11f, 0.11f, 0.9f, 1.38f, 1.38f, 0.08f);             // toit et panneaux de la couleur de l'aspect
        Box(root, Paint[a], 0.12f, 0.14f, 0.12f, 0.06f, 1.3f, 0.78f);
        Part(RoofKit2(a), root, Shade(Paint[a], 0.18f), Matrix4x4.identity);
        Box(root, "#22303F", 0.3f, 1.42f, 0.34f, 1.0f, 0.03f, 0.44f, 0.3f);       // ouverture
        Hazard(root, 0.3f, 1.44f, 0.8f, 1.0f, true, 0.02f, 0.07f);
        Flat(root, "#FF7A1A", 1.45f, 0.5f, 0.3f, 0.02f, 0.6f, 0.5f);              // pupitre sur le flanc
        Flat(root, Screen, 1.47f, 0.58f, 0.52f, 0.012f, 0.44f, 0.2f, 1f);
        for (int i = 0; i < 3; i++) Flat(root, i == 0 ? "#3DDC5A" : i == 1 ? Yellow : "#F2453D", 1.47f, 0.6f + i * 0.14f, 0.36f, 0.012f, 0.08f, 0.08f, 0.9f);
        Box(root, Yellow, 0.05f, 1.42f, 0.12f, 0.12f, 0.1f, 0.24f);               // butées
        Box(root, Yellow, 1.43f, 1.42f, 0.12f, 0.12f, 0.1f, 0.24f);
        Round(root, Yellow, 0.35f, 1.2f, 1.06f, 0.16f, 0.18f);
        if (a >= 3) Antenna(root, 1.2f, 0.4f, 1.06f);
    }

    static void PackerParts(Transform root)
    {
        // piston et plateau : maillages d'origine (0,0,0) ; l'animation règle leur hauteur
        Transform ram = MovingBox(root, "Ram", new Vector3(0.72f, 0f, 0.58f), new Vector3(0.44f, 0.7f, 0.44f), "#B5C0CC");
        Transform plate = MovingBox(root, "Plate", new Vector3(0.54f, 0f, 0.4f), new Vector3(0.8f, 0.14f, 0.8f), "#8F9BA9");

        var package = new GameObject("Package").transform;
        package.SetParent(root, false);
        Box(package, "#E5B96B", 0.6f, 1.05f, 0.14f, 0.36f, 0.36f, 0.3f, 0.15f);
        Flat(package, Yellow, 0.74f, 1.05f, 0.44f, 0.08f, 0.36f, 0.012f, 0.15f);

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

    static void ScannerBody(Transform root, int a)
    {
        string body = Paint[a];
        Box(root, body, 0f, 0.05f, 0.12f, 0.3f, 1.5f, 0.9f);
        Box(root, body, 1.3f, 0.05f, 0.12f, 0.3f, 1.5f, 0.9f);
        var beam = new MeshKit();
        B(beam, -0.02f, 0.05f, 1.02f, IdleLayout.StationSize + 0.04f, 1.5f, 0.34f);
        Part(beam, root, Shade(body, 0.2f), Matrix4x4.identity);
        Box(root, Ink, 0.5f, 0.5f, 1.36f, 0.6f, 0.16f, 0.46f);

        // bandes lumineuses à l'entrée du portique, pieds jaunes
        Flat(root, "#8DF1FF", 0.3f, 1.54f, 0.25f, 0.03f, 0.02f, 0.7f, 1f);
        Flat(root, "#8DF1FF", 1.27f, 1.54f, 0.25f, 0.03f, 0.02f, 0.7f, 1f);
        Box(root, Yellow, -0.03f, 1.42f, 0.12f, 0.36f, 0.16f, 0.14f);
        Box(root, Yellow, 1.27f, 1.42f, 0.12f, 0.36f, 0.16f, 0.14f);
        Hazard(root, 0.02f, 1.54f, 1.06f, 1.56f, true, 0.02f, 0.06f);
        if (a >= 2)   // module : rampes lumineuses sur le flanc
        {
            Flat(root, "#8DF1FF", 1.6f, 0.3f, 0.4f, 0.02f, 1.0f, 0.04f, 1f);
            Flat(root, "#8DF1FF", 1.6f, 0.3f, 0.7f, 0.02f, 1.0f, 0.04f, 1f);
        }
        if (a >= 3) Antenna(root, 1.3f, 0.3f, 1.36f);
    }

    static void ScannerParts(Transform root)
    {
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

    static void AssemblyBody(Transform root, int a)
    {
        Box(root, Paint[a], 0.1f, 0.55f, 0.12f, 1.4f, 0.85f, 0.5f);
        Box(root, Metal, 0.1f, 0.55f, 0.62f, 1.4f, 0.85f, 0.06f, 0.9f);
        Hazard(root, 0.12f, 1.33f, 0.68f, 1.36f, true, 0.06f);
        Hazard(root, 0.12f, 1.4f, 0.3f, 1.36f, true, 0.012f, 0.08f);
        Round(root, "#3B4758", 0.8f, 0.68f, 0.55f, 0.24f, 0.1f, 12);              // socle du bras
        Round(root, "#FF7A1A", 0.8f, 0.68f, 0.65f, 0.2f, 0.12f, 12);

        // pupitre de commande : son écran clignote, boutons colorés
        Box(root, "#2B3C50", 0.1f, 0.12f, 0.12f, 0.5f, 0.3f, 0.75f);
        Buttons(root, 0.18f, 0.42f, 0.32f, 0.12f);
        if (a >= 2)   // module : un second pupitre
        {
            Box(root, "#2B3C50", 1.0f, 0.12f, 0.12f, 0.5f, 0.3f, 0.5f);
            Flat(root, Screen, 1.05f, 0.42f, 0.3f, 0.4f, 0.012f, 0.24f, 1f);
        }
        if (a >= 3)   // module : un petit bras fixe de plus au bout de l'établi
        {
            Round(root, "#3B4758", 1.32f, 0.7f, 0.68f, 0.13f, 0.08f, 10);
            Box(root, "#FF7A1A", 1.26f, 0.64f, 0.76f, 0.12f, 0.12f, 0.42f);
            Box(root, "#FFA24D", 1.26f, 0.64f, 1.14f, 0.12f, 0.5f, 0.1f);
            Box(root, Yellow, 1.24f, 1.08f, 1.08f, 0.16f, 0.14f, 0.16f);
        }
    }

    static void AssemblyParts(Transform root)
    {
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
        Box(camera, "#F2F5F8", 1.15f, 0.85f, 0.68f, 0.38f, 0.3f, 0.2f);
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

    // ------------------------------------------------------------------ 1. Serre : bac à cristaux sous une charpente (robots, tas et drone : IdleChainBuilder)

    static void SerreBody(Transform root, int a)
    {
        string frame = Paint[a];
        Box(root, "#22303F", 0.06f, 0.06f, 0.12f, 1.48f, 1.02f, 0.07f, 0.1f);
        Box(root, frame, 0.02f, 0.02f, 0.12f, 1.56f, 0.05f, 0.16f);
        Box(root, frame, 0.02f, 1.05f, 0.12f, 1.56f, 0.05f, 0.16f);
        Box(root, frame, 0.02f, 0.02f, 0.12f, 0.05f, 1.08f, 0.16f);
        Box(root, frame, 1.53f, 0.02f, 0.12f, 0.05f, 1.08f, 0.16f);

        // charpente légère : on doit voir les cristaux à travers
        foreach (float x in new[] { 0.02f, 1.5f })
            foreach (float y in new[] { 0.02f, 1.02f })
                Box(root, frame, x, y, 0.12f, 0.08f, 0.08f, 0.98f);
        string beam = "#" + ColorUtility.ToHtmlStringRGB(Shade(frame, -0.25f));
        Box(root, beam, 0.02f, 0.02f, 1.1f, 1.56f, 0.08f, 0.08f);
        Box(root, beam, 0.02f, 1.02f, 1.1f, 1.56f, 0.08f, 0.08f);
        Box(root, beam, 0.02f, 0.02f, 1.1f, 0.08f, 1.08f, 0.08f);
        Box(root, beam, 1.5f, 0.02f, 1.1f, 0.08f, 1.08f, 0.08f);

        if (a >= 2)   // module : rampes de lumière
            for (int s = 0; s < 2; s++) Box(root, "#8DF1FF", 0.1f, 0.36f + s * 0.4f, 1.1f, 1.4f, 0.05f, 0.05f, 1f);
        if (a >= 3) Antenna(root, 0.5f, 0.05f, 1.18f);
    }
}
