using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Construit ce qui relie les machines dans la scène idle : cristaux, deux robots, drone, tas, stocks et chariots, en formes simples pour juger le jeu avant les vrais modèles.
// Les maillages sont écrits en coordonnées de scène, sauf ceux qui bougent (robots, drone, chariots), centrés sur leur origine. C'est IdleChainView qui anime le tout.
public static class IdleChainBuilder
{
    const int Rows = 3, Columns = 3, CrystalCount = Rows * Columns;
    const float BedHeight = 0.19f, CutCube = 0.13f, ChipCube = 0.1f, StockCube = 0.13f, RobotScale = 1.5f;
    const string Crystal = "#4FC8FF", CrystalLight = "#A6F0FF", Chip = "#3DDC5A", ChipLight = "#8CFF9E", Ink = "#1E2A38", Metal = "#C3CCD7";
    const string CutterColor = "#2F7BE0", EtcherColor = "#16A5B8";

    // Couleur de ce qui attend devant chaque poste, à partir de l'Assemblage.
    static readonly string[] StockHex = { Chip, "#E9EEF3", "#4FB3FF", "#E5B96B", "#D9A25B" };
    static readonly string[] StockLight = { ChipLight, "#FFFFFF", "#8FD0FF", "#F2D6A8", "#E5B96B" };

    // Peint sur la dalle : les emplacements, marqués au sol avant même d'être achetés, et un anneau par robot.
    public static void PaintFloor(MeshKit decor)
    {
        var frame = new MeshKit();
        frame.RoundedRing(IdleLayout.StationSize * 0.5f + 0.06f, 0.1f, 0.05f, 0f, 0.22f, 0.14f);
        for (int i = 0; i < IdleLayout.Slots.Length; i++)
            for (int k = 0; k < IdleLayout.Slots[i].Length; k++)
                decor.Append(frame, MeshKit.Color("#FFFFFF"), Matrix4x4.Translate(IdleLayout.SlotCenter(i, k) + Vector3.up * 0.008f));

        var ring = new MeshKit();
        ring.RoundedRing(0.36f, 0.3f, 0.06f, 0f);
        decor.Append(ring, MeshKit.Color(CutterColor), Matrix4x4.Translate(IdleLayout.ToWorld(IdleLayout.CutterSpot, 0.125f)));
        decor.Append(ring, MeshKit.Color(EtcherColor), Matrix4x4.Translate(IdleLayout.ToWorld(IdleLayout.EtcherSpot, 0.125f)));
    }

    public static void Build(Transform parent, FeedbackSpawner feedback)
    {
        var root = new GameObject("Chain", typeof(IdleChainView)).transform;
        root.SetParent(parent, false);

        GameObject crystals = Crystals().ToObject("Crystals", root, IdleMaterials.Palette());
        Transform cutter = Robot(root, "Cutter", CutterColor, "#F2453D", IdleLayout.CutterSpot);
        Transform etcher = Robot(root, "Etcher", EtcherColor, "#3DDC5A", IdleLayout.EtcherSpot);
        GameObject load;
        Transform drone = Drone(root, out load);

        int cubeIndices;
        GameObject cutPile = Pile(IdleLayout.CutPile, 0.13f, 2, 2, 2, CutCube, Crystal, CrystalLight, out cubeIndices).ToObject("CutPile", root, IdleMaterials.Palette());
        GameObject rawPile = Pile(IdleLayout.RawPile, 0.13f, 3, 2, 2, ChipCube, Chip, ChipLight, out cubeIndices).ToObject("RawPile", root, IdleMaterials.Palette());

        int stockCount = IdleLayout.Stocks.Length - 1;
        var stocks = new GameObject[stockCount];
        var piles = new MeshFilter[stockCount];
        var texts = new TextMesh[stockCount];
        int stockBase = 0;
        for (int i = 0; i < stockCount; i++)
        {
            Vector2 at = IdleLayout.Stocks[i + 1];
            var stock = new MeshKit();
            Pallet(stock, at);
            stockBase = stock.IndexCount;
            AddCubes(stock, at, 0.11f, 4, 4, 3, StockCube, StockHex[i], StockLight[i]);
            var holder = new GameObject("Stock_" + (i + 1));
            holder.transform.SetParent(root, false);
            stocks[i] = holder;
            piles[i] = stock.ToObject("Pile", holder.transform, IdleMaterials.Palette()).GetComponent<MeshFilter>();
            texts[i] = StockLabel(holder.transform, at);
        }

        var carts = new Transform[IdleLayout.CartFrom.Length - 1];
        var cartLoads = new GameObject[carts.Length];
        var from = new Vector3[carts.Length];
        var to = new Vector3[carts.Length];
        for (int i = 0; i < carts.Length; i++)
        {
            from[i] = IdleLayout.ToWorld(IdleLayout.CartFrom[i + 1], 0.02f);
            to[i] = IdleLayout.ToWorld(IdleLayout.Stocks[i + 2], 0.02f);
            carts[i] = Cart(root, "Cart_" + (i + 1), out cartLoads[i], StockHex[i + 1]);
            carts[i].position = from[i];
            carts[i].rotation = Quaternion.LookRotation((to[i] - from[i]).normalized);
        }

        Vector3 droneStart = IdleLayout.ToWorld(IdleLayout.RawPile, 0.75f), droneEnd = IdleLayout.ToWorld(IdleLayout.Stocks[1], 1.05f);
        drone.position = droneStart;

        var so = new SerializedObject(root.GetComponent<IdleChainView>());
        so.FindProperty("crystals").objectReferenceValue = crystals.GetComponent<MeshFilter>();
        so.FindProperty("crystalCount").intValue = CrystalCount;
        so.FindProperty("cutter").objectReferenceValue = cutter;
        so.FindProperty("etcher").objectReferenceValue = etcher;
        so.FindProperty("drone").objectReferenceValue = drone;
        so.FindProperty("droneLoad").objectReferenceValue = load;
        so.FindProperty("droneFrom").vector3Value = droneStart;
        so.FindProperty("droneTo").vector3Value = droneEnd;
        so.FindProperty("cutPile").objectReferenceValue = cutPile.GetComponent<MeshFilter>();
        so.FindProperty("rawPile").objectReferenceValue = rawPile.GetComponent<MeshFilter>();
        so.FindProperty("cubeIndices").intValue = cubeIndices;
        so.FindProperty("stockBaseIndices").intValue = stockBase;
        Objects(so.FindProperty("stocks"), stocks);
        Objects(so.FindProperty("stockPiles"), piles);
        Objects(so.FindProperty("stockTexts"), texts);
        Objects(so.FindProperty("carts"), carts);
        Objects(so.FindProperty("cartLoads"), cartLoads);
        Vectors(so.FindProperty("cartFrom"), from);
        Vectors(so.FindProperty("cartTo"), to);
        so.FindProperty("feedback").objectReferenceValue = feedback;
        so.FindProperty("rig").objectReferenceValue = Object.FindFirstObjectByType<IdleCameraRig>();
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Objects(SerializedProperty array, Object[] objects)
    {
        array.arraySize = objects.Length;
        for (int i = 0; i < objects.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
    }

    static void Vectors(SerializedProperty array, Vector3[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).vector3Value = values[i];
    }

    // Un même dessin par cristal, donc le même nombre de sommets : IdleChainView les fait repousser un par un.
    static MeshKit Crystals()
    {
        Vector2 corner = IdleLayout.Stations[0];
        var kit = new MeshKit();
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
            {
                Vector3 at = IdleLayout.ToWorld(corner.x + 0.3f + c * 0.5f, corner.y + 0.26f + r * 0.32f, BedHeight);
                kit.SetPaint((r + c) % 2 == 0 ? Crystal : "#2FA8F0", 0.9f);
                kit.Cone(at, 0.15f, 0.62f, 5);
                kit.SetPaint(CrystalLight, 0.9f);
                kit.Cone(at + new Vector3(0.1f, 0f, 0.08f), 0.08f, 0.34f, 5);
            }
        return kit;
    }

    static void RobotBox(MeshKit kit, float x, float y, float z, float w, float h, float d)
    {
        kit.Box(new Vector3(x, y, z) * RobotScale, new Vector3(w, h, d) * RobotScale);
    }

    static void RobotBevel(MeshKit kit, float x, float y, float z, float w, float h, float d, float bevel)
    {
        kit.BevelBox(new Vector3(x, y, z) * RobotScale, new Vector3(w, h, d) * RobotScale, bevel);
    }

    // Robot sur roues : l'avant regarde les cristaux.
    static Transform Robot(Transform parent, string name, string hex, string tool, Vector2 web)
    {
        var kit = new MeshKit();
        kit.SetPaint(Ink, 0.2f);
        RobotBox(kit, -0.15f, 0f, -0.15f, 0.3f, 0.07f, 0.3f);
        kit.SetPaint(hex, 0.7f);
        RobotBevel(kit, -0.12f, 0.07f, -0.12f, 0.24f, 0.26f, 0.24f, 0.04f);
        kit.SetPaint("#E9EEF3", 0.7f);
        RobotBevel(kit, -0.1f, 0.33f, -0.1f, 0.2f, 0.15f, 0.2f, 0.04f);
        kit.SetPaint("#27D3E6", 1f);
        RobotBox(kit, -0.07f, 0.38f, 0.098f, 0.05f, 0.05f, 0.008f);
        RobotBox(kit, 0.02f, 0.38f, 0.098f, 0.05f, 0.05f, 0.008f);
        kit.SetPaint(Metal, 0.7f);
        RobotBox(kit, -0.025f, 0.2f, 0.11f, 0.05f, 0.05f, 0.2f);
        kit.SetPaint(tool, 1f);
        RobotBox(kit, -0.04f, 0.19f, 0.29f, 0.08f, 0.07f, 0.06f);
        GameObject robot = kit.ToObject(name, parent, IdleMaterials.Palette());
        robot.transform.position = IdleLayout.ToWorld(web, 0.12f);
        robot.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        robot.SetActive(false);
        return robot.transform;
    }

    static Transform Drone(Transform parent, out GameObject load)
    {
        var kit = new MeshKit();
        kit.SetPaint("#FFC400", 0.7f);
        kit.BevelBox(new Vector3(-0.13f, 0f, -0.13f), new Vector3(0.26f, 0.09f, 0.26f), 0.025f);
        foreach (float x in new[] { -0.2f, 0.2f })
            foreach (float z in new[] { -0.2f, 0.2f })
            {
                kit.SetPaint(Ink, 0.3f);
                kit.Box(new Vector3(Mathf.Min(x, 0f), 0.055f, z - 0.015f), new Vector3(0.2f, 0.02f, 0.03f));
                kit.Cylinder(new Vector3(x, 0.07f, z), 0.025f, 0.05f, 6);
                kit.SetPaint(Metal, 0.8f);
                kit.Cylinder(new Vector3(x, 0.12f, z), 0.1f, 0.012f, 8);
            }
        GameObject drone = kit.ToObject("Drone", parent, IdleMaterials.Palette());

        var crate = new MeshKit();
        crate.SetPaint(Chip, 0.5f);
        crate.Box(new Vector3(-0.09f, -0.17f, -0.09f), new Vector3(0.18f, 0.16f, 0.18f));
        load = crate.ToObject("Load", drone.transform, IdleMaterials.Palette());
        load.SetActive(false);
        drone.SetActive(false);
        return drone.transform;
    }

    // Chariot autonome : châssis jaune, plateau, voyant. Son avant est +Z local.
    static Transform Cart(Transform parent, string name, out GameObject load, string hex)
    {
        var kit = new MeshKit();
        kit.SetPaint(Ink, 0.2f);
        kit.Box(new Vector3(-0.3f, 0f, -0.42f), new Vector3(0.6f, 0.1f, 0.84f));
        kit.SetPaint("#FFC400", 0.7f);
        kit.BevelBox(new Vector3(-0.27f, 0.1f, -0.38f), new Vector3(0.54f, 0.14f, 0.76f), 0.03f);
        kit.SetPaint("#E9EEF3", 0.7f);
        kit.BevelBox(new Vector3(-0.2f, 0.24f, 0.2f), new Vector3(0.4f, 0.24f, 0.18f), 0.03f);
        kit.SetPaint("#27D3E6", 1f);
        kit.Box(new Vector3(-0.14f, 0.32f, 0.38f), new Vector3(0.28f, 0.08f, 0.01f));
        GameObject cart = kit.ToObject(name, parent, IdleMaterials.Palette());

        var crate = new MeshKit();
        crate.SetPaint(hex, 0.5f);
        crate.BevelBox(new Vector3(-0.2f, 0.24f, -0.3f), new Vector3(0.4f, 0.3f, 0.44f), 0.02f);
        load = crate.ToObject("Load", cart.transform, IdleMaterials.Palette());
        load.SetActive(false);
        cart.SetActive(false);
        return cart.transform;
    }

    static MeshKit Pile(Vector2 web, float height, int columns, int rows, int layers, float cube, string hex, string light, out int cubeIndices)
    {
        var kit = new MeshKit();
        cubeIndices = AddCubes(kit, web, height, columns, rows, layers, cube, hex, light);
        return kit;
    }

    // Cubes rangés de bas en haut : un tas à moitié plein n'en montre que le bas. Renvoie le nombre d'indices d'un cube.
    static int AddCubes(MeshKit kit, Vector2 web, float height, int columns, int rows, int layers, float cube, string hex, string light)
    {
        float step = cube * 1.08f;
        Vector3 start = IdleLayout.ToWorld(web, height) - new Vector3((rows - 1) * step + cube, 0f, (columns - 1) * step + cube) * 0.5f;
        int perCube = 0;
        for (int l = 0; l < layers; l++)
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                {
                    int before = kit.IndexCount;
                    kit.SetPaint((r + c + l) % 2 == 0 ? hex : light, 0.6f);
                    kit.Box(start + new Vector3(r * step, l * step, c * step), new Vector3(cube, cube, cube));
                    perCube = kit.IndexCount - before;
                }
        return perCube;
    }

    static void Pallet(MeshKit kit, Vector2 web)
    {
        Vector3 c = IdleLayout.ToWorld(web, 0f);
        kit.SetPaint("#8A5A2B", 0.1f);
        kit.BevelBox(c + new Vector3(-0.39f, 0f, -0.39f), new Vector3(0.78f, 0.06f, 0.78f), 0.015f);
        kit.SetPaint("#B07A3A", 0.1f);
        for (int s = 0; s < 4; s++) kit.Box(c + new Vector3(-0.39f + s * 0.2f, 0.06f, -0.39f), new Vector3(0.18f, 0.04f, 0.78f));
    }

    // Pastille « Stock 12/30 », toujours face à la caméra, au-dessus du tas.
    static TextMesh StockLabel(Transform parent, Vector2 web)
    {
        var holder = new GameObject("StockLabel", typeof(FaceCamera)).transform;
        holder.SetParent(parent, false);
        holder.position = IdleLayout.ToWorld(web, 0.62f);
        var pill = new MeshKit();
        pill.Pill(1.44f, 0.34f, 0.01f);
        pill.ToObject("Pill", holder, IdleMaterials.Overlay("LabelPill", "#1E2A38", 3541));
        TextMesh text = IdleSceneBuilder.MakeTextMesh(holder, "Main", "", UiTheme.Font, 80, 0.035f, Vector3.zero, Color.white, true);
        text.GetComponent<MeshRenderer>().sharedMaterial = IdleSceneBuilder.LabelFontMaterial(UiTheme.Font);
        return text;
    }
}
