using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Construit le début de la chaîne dans la scène idle : cristaux, deux robots, drone, tas et stock, en formes simples pour juger le jeu avant les vrais modèles.
// Les maillages sont écrits en coordonnées de scène, sauf ceux qui bougent (robots, drone), centrés sur leur origine. C'est IdleChainView qui anime le tout.
public static class IdleChainBuilder
{
    const int Rows = 3, Columns = 3, CrystalCount = Rows * Columns;
    const float BedHeight = 0.19f, CutCube = 0.13f, ChipCube = 0.1f, StockCube = 0.13f;
    const string Crystal = "#4FC8FF", CrystalLight = "#A6F0FF", Chip = "#3DDC5A", ChipLight = "#8CFF9E", Ink = "#1E2A38", Metal = "#C3CCD7";

    public static void Build(Transform parent, FeedbackSpawner feedback)
    {
        var root = new GameObject("Chain", typeof(IdleChainView)).transform;
        root.SetParent(parent, false);

        GameObject crystals = Crystals().ToObject("Crystals", root, IdleMaterials.Palette());
        Transform cutter = Robot(root, "Cutter", "#2F7BE0", "#F2453D", IdleLayout.CutterSpot);
        Transform etcher = Robot(root, "Etcher", "#16A5B8", "#3DDC5A", IdleLayout.EtcherSpot);
        GameObject load;
        Transform drone = Drone(root, out load);

        int cubeIndices;
        GameObject cutPile = Pile(IdleLayout.CutPile, 0.13f, 2, 2, 2, CutCube, Crystal, CrystalLight, out cubeIndices).ToObject("CutPile", root, IdleMaterials.Palette());
        GameObject rawPile = Pile(IdleLayout.RawPile, 0.13f, 3, 2, 2, ChipCube, Chip, ChipLight, out cubeIndices).ToObject("RawPile", root, IdleMaterials.Palette());

        var stock = new MeshKit();
        Pallet(stock);
        int stockBase = stock.IndexCount;
        AddCubes(stock, IdleLayout.Stock, 0.11f, 4, 4, 3, StockCube, Chip, ChipLight);
        GameObject stockPile = stock.ToObject("StockPile", root, IdleMaterials.Palette());
        TextMesh stockText = StockLabel(root);

        Vector3 from = IdleLayout.ToWorld(IdleLayout.RawPile, 0.75f), to = IdleLayout.ToWorld(IdleLayout.Stock, 1.05f);
        drone.position = from;

        var so = new SerializedObject(root.GetComponent<IdleChainView>());
        so.FindProperty("crystals").objectReferenceValue = crystals.GetComponent<MeshFilter>();
        so.FindProperty("crystalCount").intValue = CrystalCount;
        so.FindProperty("cutter").objectReferenceValue = cutter;
        so.FindProperty("etcher").objectReferenceValue = etcher;
        so.FindProperty("drone").objectReferenceValue = drone;
        so.FindProperty("droneLoad").objectReferenceValue = load;
        so.FindProperty("droneFrom").vector3Value = from;
        so.FindProperty("droneTo").vector3Value = to;
        so.FindProperty("cutPile").objectReferenceValue = cutPile.GetComponent<MeshFilter>();
        so.FindProperty("rawPile").objectReferenceValue = rawPile.GetComponent<MeshFilter>();
        so.FindProperty("stockPile").objectReferenceValue = stockPile.GetComponent<MeshFilter>();
        so.FindProperty("cubeIndices").intValue = cubeIndices;
        so.FindProperty("stockBaseIndices").intValue = stockBase;
        so.FindProperty("stockText").objectReferenceValue = stockText;
        so.FindProperty("feedback").objectReferenceValue = feedback;
        so.FindProperty("rig").objectReferenceValue = Object.FindFirstObjectByType<IdleCameraRig>();
        so.ApplyModifiedPropertiesWithoutUndo();
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

    // Robot sur roues : l'avant regarde les cristaux.
    static Transform Robot(Transform parent, string name, string hex, string tool, Vector2 web)
    {
        var kit = new MeshKit();
        kit.SetPaint(Ink, 0.2f);
        kit.Box(new Vector3(-0.15f, 0f, -0.15f), new Vector3(0.3f, 0.07f, 0.3f));
        kit.SetPaint(hex, 0.7f);
        kit.BevelBox(new Vector3(-0.12f, 0.07f, -0.12f), new Vector3(0.24f, 0.26f, 0.24f), 0.03f);
        kit.SetPaint("#E9EEF3", 0.7f);
        kit.BevelBox(new Vector3(-0.1f, 0.33f, -0.1f), new Vector3(0.2f, 0.15f, 0.2f), 0.03f);
        kit.SetPaint("#27D3E6", 1f);
        kit.Box(new Vector3(-0.07f, 0.38f, 0.098f), new Vector3(0.05f, 0.05f, 0.008f));
        kit.Box(new Vector3(0.02f, 0.38f, 0.098f), new Vector3(0.05f, 0.05f, 0.008f));
        kit.SetPaint(Metal, 0.7f);
        kit.Box(new Vector3(-0.025f, 0.2f, 0.11f), new Vector3(0.05f, 0.05f, 0.24f));
        kit.SetPaint(tool, 1f);
        kit.Box(new Vector3(-0.04f, 0.19f, 0.33f), new Vector3(0.08f, 0.07f, 0.06f));
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

    static void Pallet(MeshKit kit)
    {
        Vector3 c = IdleLayout.ToWorld(IdleLayout.Stock, 0f);
        kit.SetPaint("#8A5A2B", 0.1f);
        kit.BevelBox(c + new Vector3(-0.39f, 0f, -0.39f), new Vector3(0.78f, 0.06f, 0.78f), 0.015f);
        kit.SetPaint("#B07A3A", 0.1f);
        for (int s = 0; s < 4; s++) kit.Box(c + new Vector3(-0.39f + s * 0.2f, 0.06f, -0.39f), new Vector3(0.18f, 0.04f, 0.78f));
    }

    // Pastille « Stock 12/30 », toujours face à la caméra.
    static TextMesh StockLabel(Transform parent)
    {
        var holder = new GameObject("StockLabel", typeof(FaceCamera)).transform;
        holder.SetParent(parent, false);
        holder.position = IdleLayout.ToWorld(IdleLayout.Stock, 0.82f);
        var pill = new MeshKit();
        pill.Pill(1.44f, 0.34f, 0.01f);
        pill.ToObject("Pill", holder, IdleMaterials.Overlay("LabelPill", "#1E2A38", 3541));
        TextMesh text = IdleSceneBuilder.MakeTextMesh(holder, "Main", "", UiTheme.Font, 80, 0.035f, Vector3.zero, Color.white, true);
        text.GetComponent<MeshRenderer>().sharedMaterial = IdleSceneBuilder.LabelFontMaterial(UiTheme.Font);
        return text;
    }
}
