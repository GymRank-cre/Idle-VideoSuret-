using UnityEditor;
using UnityEngine;

// Construit les machines de la scène idle (U2c). Les coordonnées de ce fichier sont celles du web, relatives au coin arrière du poste :
// (x, y, z) du web devient (Z, X, Y) dans Unity. Chaque machine = quelques maillages par couleur + de petits objets mobiles animés
// par un script `IMachineAnim` selon la phase du cycle.
public static class IdleMachineBuilder
{
    const string Wall = "#4F8FD6";

    // Boîte aux coordonnées du web (x, y, z, largeur selon x, profondeur selon y, hauteur).
    public static void B(MeshKit k, float x, float y, float z, float w, float d, float h)
    {
        k.Box(new Vector3(y, z, x), new Vector3(d, h, w));
    }

    static Material M(string hex, float smooth = 0f) { return IdleMaterials.Get("M_" + hex.TrimStart('#'), hex, smooth); }

    static GameObject Part(MeshKit kit, string name, Transform parent, string hex)
    {
        return kit.ToObject(name, parent, M(hex));
    }

    // Embase commune à tous les postes (dalle gris clair de 1,7 m).
    static void Platform(Transform root)
    {
        var k = new MeshKit();
        B(k, -0.05f, -0.05f, 0f, 1.7f, 1.7f, 0.12f);
        Part(k, "Platform", root, "#B8C4D0");
    }

    // Machine du poste `i` ; renvoie la racine (désactivée tant que le poste est verrouillé : c'est IdleStationView qui l'active).
    public static Transform Build(Transform parent, int i)
    {
        var root = new GameObject("Machine").transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(-IdleLayout.StationSize * 0.5f, 0f, -IdleLayout.StationSize * 0.5f);   // coin arrière du poste
        Platform(root);
        switch (i)
        {
            case 0: Reception(root); break;
        }
        return root;
    }

    // ------------------------------------------------------------------ 1. Réception : quai, porte à lamelles, gyrophare, palette de caisses

    static void Reception(Transform root)
    {
        var wall = new MeshKit();
        B(wall, 0f, 0f, 0.12f, IdleLayout.StationSize, 0.26f, 1.1f);
        Part(wall, "Wall", root, Wall);
        var door = new MeshKit();
        B(door, 0.3f, 0.26f, 0.2f, 1.0f, 0.012f, 0.75f);
        Part(door, "Door", root, "#22303F");
        var slats = new MeshKit();
        for (int s = 1; s < 5; s++) B(slats, 0.3f, 0.27f, 0.2f + 0.75f * s / 5f, 1.0f, 0.008f, 0.014f);
        Part(slats, "Slats", root, "#4A5A6C");

        // gyrophare : un cylindre dont le matériau alterne
        var beaconKit = new MeshKit();
        beaconKit.Cylinder(new Vector3(0.13f, 1.22f, IdleLayout.StationSize - 0.22f), 0.1f, 0.12f, 8);
        Material on = M("#FFC400"), off = M("#B58A00");
        GameObject beacon = beaconKit.ToObject("Beacon", root, on);

        var pallet = new MeshKit();
        B(pallet, 0.35f, 0.6f, 0.12f, 0.95f, 0.95f, 0.1f);
        Part(pallet, "Pallet", root, "#B07A3A");

        // 4 caisses sur la palette (la 4e en dernier) : boîte + ruban d'emballage, mises à l'échelle en hauteur par l'animation
        var crates = new Transform[4];
        Material crateMat = M("#D9A25B"), tapeMat = M("#FFC400");
        for (int q = 0; q < 4; q++)
        {
            float qx = 0.42f + (q % 2) * 0.45f, qy = 0.67f + (q / 2) * 0.45f;
            var crate = new GameObject("Crate_" + q).transform;
            crate.SetParent(root, false);
            crate.localPosition = new Vector3(qy + 0.2f, 0.22f, qx + 0.2f);
            var box = new MeshKit();
            box.Box(new Vector3(-0.2f, 0f, -0.2f), new Vector3(0.4f, 0.36f, 0.4f));
            box.ToObject("Box", crate, crateMat);
            var tape = new MeshKit();
            tape.Box(new Vector3(-0.2f, 0.36f, -0.05f), new Vector3(0.4f, 0.012f, 0.1f));
            tape.ToObject("Tape", crate, tapeMat);
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
