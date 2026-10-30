using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Construit la scène du jeu idle (`Tools > Idle Prototype > Build scene`). U2a : caméra, dalle en damier à bord jaune, décor (herbe,
// arbres, route, voitures) et 5 emplacements de postes en fantômes avec cadenas. U2b : 4 tapis animés (chevrons et caisses) et
// 5 plaques d'achat au sol touchables. Les machines arrivent en U2c.
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
        BuildBelts(world);
        BuildPlates(world);
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


    // ------------------------------------------------------------------ tapis (U2b)

    const float BeltWidth = 0.5f, BeltHeight = 0.13f, ChevronPeriod = 0.45f;

    static Texture2D ChevronTexture()
    {
        const string path = "Assets/Prototype/Textures/BeltChevron.png";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        if (!AssetDatabase.IsValidFolder("Assets/Prototype/Textures")) AssetDatabase.CreateFolder("Assets/Prototype", "Textures");
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        Color bed = new Color32(0x46, 0x54, 0x6A, 0xFF), mark = new Color32(0x68, 0x7B, 0x98, 0xFF);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float edge = Mathf.Abs(x - (n - 1) * 0.5f);
                float band = 38f - edge * 0.9f;                       // chevron : pointe vers le haut de la texture = sens de la marche
                tex.SetPixel(x, y, Mathf.Abs(y - band) < 4.5f ? mark : bed);
            }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void BuildBelts(Transform parent)
    {
        var root = new GameObject("Belts").transform;
        root.SetParent(parent, false);
        Material bedMat = IdleMaterials.Get("BeltBed", "#46546A");
        Material railMat = IdleMaterials.Get("BeltRail", "#FFC400");
        Material topMat = IdleMaterials.Get("BeltTop", "#FFFFFF");
        Texture2D chevrons = ChevronTexture();
        topMat.SetTexture("_BaseMap", chevrons);
        topMat.mainTexture = chevrons;
        string[] itemHex = { "#D9A25B", "#E9EEF3", "#4FB3FF", "#E5B96B" };
        string[] detailHex = { "#FFC400", "#1FA0FF", "#3DDC5A", "#FFFFFF" };
        var belts = new Transform[IdleLayout.Belts.Length];
        var tops = new Renderer[belts.Length];
        for (int bi = 0; bi < belts.Length; bi++)
        {
            Transform belt = new GameObject("Belt_" + bi).transform;
            belt.SetParent(root, false);
            belts[bi] = belt;
            Vector2[] web = IdleLayout.Belts[bi];
            var body = new MeshKit(); var top = new MeshKit(); var rails = new MeshKit();
            float ext = BeltWidth * 0.5f, acc = 0f;
            for (int k = 0; k < web.Length - 1; k++)
            {
                Vector3 p = IdleLayout.ToWorld(web[k]), q = IdleLayout.ToWorld(web[k + 1]);
                Vector3 dir = (q - p).normalized, nrm = new Vector3(-dir.z, 0f, dir.x);
                float len = Vector3.Distance(p, q);
                Vector3 p2 = p - dir * ext, q2 = q + dir * ext;   // chaque tronçon dépasse d'une demi-largeur : les coins sont pleins
                Vector3 c0 = p2 - nrm * ext, c1 = p2 + nrm * ext, c2 = q2 + nrm * ext, c3 = q2 - nrm * ext;
                Vector3 min = Vector3.Min(Vector3.Min(c0, c1), Vector3.Min(c2, c3)), max = Vector3.Max(Vector3.Max(c0, c1), Vector3.Max(c2, c3));
                body.Box(new Vector3(min.x, 0f, min.z), new Vector3(max.x - min.x, BeltHeight, max.z - min.z));
                float v0 = (acc - ext) / ChevronPeriod, v1 = (acc + len + ext) / ChevronPeriod;
                Vector3 up = new Vector3(0f, BeltHeight + 0.004f, 0f);
                top.UvQuad(c0 + up, c1 + up, c2 + up, c3 + up, new Vector2(0f, v0), new Vector2(1f, v0), new Vector2(1f, v1), new Vector2(0f, v1));
                foreach (float side in new[] { -1f, 1f })   // rails jaunes de chaque côté
                {
                    Vector3 a = p2 + nrm * side * (ext + 0.01f), b = q2 + nrm * side * (ext + 0.01f), t = nrm * 0.0175f;
                    Vector3 rmin = Vector3.Min(Vector3.Min(a - t, a + t), Vector3.Min(b - t, b + t)), rmax = Vector3.Max(Vector3.Max(a - t, a + t), Vector3.Max(b - t, b + t));
                    rails.Box(new Vector3(rmin.x, BeltHeight, rmin.z), new Vector3(rmax.x - rmin.x, 0.04f, rmax.z - rmin.z));
                }
                acc += len;
            }
            body.ToObject("Body", belt, bedMat);
            tops[bi] = top.ToObject("Top", belt, topMat).GetComponent<Renderer>();
            rails.ToObject("Rails", belt, railMat);

            // caisses qui avancent (leur nombre dépend de la longueur ; IdleBeltView les déplace)
            Material itemMat = IdleMaterials.Get("Item_" + itemHex[bi].TrimStart('#'), itemHex[bi]);
            Material detailMat = IdleMaterials.Get("ItemDetail_" + detailHex[bi].TrimStart('#'), detailHex[bi]);
            int count = Mathf.Max(2, Mathf.RoundToInt(acc / 0.95f));
            for (int k = 0; k < count; k++)
            {
                var item = new GameObject("Item_" + k).transform;
                item.SetParent(belt, false);
                float s = bi == 3 ? 0.34f : bi == 0 ? 0.32f : 0.3f, h = bi == 0 || bi == 3 ? 0.3f : 0.22f;
                var box = new MeshKit();
                box.Box(new Vector3(-s * 0.5f, 0f, -s * 0.5f), new Vector3(s, h, s));
                box.ToObject("Box", item, itemMat);
                var detail = new MeshKit();
                if (bi == 0 || bi == 3) detail.Box(new Vector3(-0.04f, h, -s * 0.5f), new Vector3(0.08f, 0.012f, s));   // ruban d'emballage
                else detail.Cylinder(new Vector3(0f, h, 0f), 0.07f, 0.08f);                                        // voyant
                detail.ToObject("Detail", item, detailMat);
            }
        }
        var view = new GameObject("BeltView", typeof(IdleBeltView));
        view.transform.SetParent(root, false);
        var so = new SerializedObject(view.GetComponent<IdleBeltView>());
        SetObjects(so.FindProperty("belts"), belts);
        SetObjects(so.FindProperty("chevronTops"), tops);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetObjects(SerializedProperty array, Object[] objects)
    {
        array.arraySize = objects.Length;
        for (int i = 0; i < objects.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
    }

    // ------------------------------------------------------------------ plaques d'achat au sol (U2b)

    const float PlateSize = 1.25f;

    static void BuildPlates(Transform parent)
    {
        var root = new GameObject("Plates").transform;
        root.SetParent(parent, false);
        Material baseMat = IdleMaterials.Get("PlateBase", "#2A3644");
        Material greenMat = IdleMaterials.Get("PlateGreen", "#3DDC5A");
        Material yellowMat = IdleMaterials.Get("PlateYellow", "#FFC400");
        Material arrowOn = IdleMaterials.Get("PlateArrowOn", "#8CFF9E");
        Material arrowOff = IdleMaterials.Get("PlateArrowOff", "#9AA6B3");
        Font font = UiTheme.Font;
        Vector3 textUp = new Vector3(0f, 0f, -1f);               // « haut » du texte sur le sol : vers le haut-gauche de l'écran
        Vector3 along = new Vector3(-1f, 0f, 0f);                // sens de lecture : de gauche à droite en montant vers la droite
        Quaternion flat = Quaternion.Euler(90f, 180f, 0f);       // texte couché sur le sol, aligné sur l'axe de la dalle
        var views = new IdlePlateView[IdleLayout.Plates.Length];
        for (int i = 0; i < views.Length; i++)
        {
            var plate = new GameObject("Plate_" + i, typeof(IdlePlateView)).transform;
            plate.SetParent(root, false);
            plate.position = IdleLayout.ToWorld(IdleLayout.Plates[i], 0.012f);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(plate, false);
            RoundedBox.Make(visual, "Base", new Vector3(0f, 0.025f, 0f), new Vector3(PlateSize, 0.05f, PlateSize), 0.1f, baseMat);

            float half = PlateSize * 0.5f - 0.08f, lw = 0.07f, top = 0.052f;
            var solid = new MeshKit();
            solid.Box(new Vector3(-half, top, -half), new Vector3(half * 2, 0.006f, lw));
            solid.Box(new Vector3(-half, top, half - lw), new Vector3(half * 2, 0.006f, lw));
            solid.Box(new Vector3(-half, top, -half), new Vector3(lw, 0.006f, half * 2));
            solid.Box(new Vector3(half - lw, top, -half), new Vector3(lw, 0.006f, half * 2));
            GameObject green = solid.ToObject("BorderGreen", visual, greenMat);
            var dashed = new MeshKit();
            for (float t = -half; t < half - 0.1f; t += 0.26f)
            {
                float l = Mathf.Min(0.16f, half - t);
                dashed.Box(new Vector3(t, top, -half), new Vector3(l, 0.006f, lw));
                dashed.Box(new Vector3(t, top, half - lw), new Vector3(l, 0.006f, lw));
                dashed.Box(new Vector3(-half, top, t), new Vector3(lw, 0.006f, l));
                dashed.Box(new Vector3(half - lw, top, t), new Vector3(lw, 0.006f, l));
            }
            GameObject yellow = dashed.ToObject("BorderYellow", visual, yellowMat);

            var arrowKit = new MeshKit();
            arrowKit.FlatArrow(textUp * 0.3f + new Vector3(0f, top + 0.012f, 0f), along, 0.42f, 0.3f);   // flèche vers le haut à droite
            GameObject arrow = arrowKit.ToObject("Arrow", visual, arrowOff);

            var tag = new GameObject("NewTag").transform;
            tag.SetParent(visual, false);
            tag.localPosition = textUp * 0.38f + new Vector3(0f, top + 0.012f, 0f);
            MakeFlatText(tag, "NOUVEAU", font, 70, 0.034f, flat, new Color32(0xFF, 0xC4, 0x00, 0xFF), out _);

            var priceRoot = new GameObject("Price").transform;
            priceRoot.SetParent(visual, false);
            priceRoot.localPosition = -textUp * 0.12f + new Vector3(0f, top + 0.015f, 0f);
            TextMesh main;
            Transform label = MakeFlatText(priceRoot, "0", font, 90, 0.085f, flat, Color.white, out main);
            var texts = new List<TextMesh> { main };
            foreach (TextMesh outline in label.GetComponentsInChildren<TextMesh>()) if (outline != main) texts.Add(outline);

            var view = plate.GetComponent<IdlePlateView>();
            var so = new SerializedObject(view);
            so.FindProperty("station").intValue = i;
            so.FindProperty("visual").objectReferenceValue = visual;
            so.FindProperty("borderAffordable").objectReferenceValue = green;
            so.FindProperty("borderWaiting").objectReferenceValue = yellow;
            so.FindProperty("arrow").objectReferenceValue = arrow;
            so.FindProperty("arrowRenderer").objectReferenceValue = arrow.GetComponent<Renderer>();
            so.FindProperty("arrowOn").objectReferenceValue = arrowOn;
            so.FindProperty("arrowOff").objectReferenceValue = arrowOff;
            so.FindProperty("newTag").objectReferenceValue = tag.gameObject;
            SetObjects(so.FindProperty("priceTexts"), texts.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();
            views[i] = view;
        }
        var input = new GameObject("PlateInput", typeof(IdlePlateInput));
        input.transform.SetParent(root, false);
        var iso = new SerializedObject(input.GetComponent<IdlePlateInput>());
        SetObjects(iso.FindProperty("plates"), views);
        iso.ApplyModifiedPropertiesWithoutUndo();
    }

    // Texte couché sur le sol : le texte principal et 4 copies sombres décalées en diagonale (contour).
    static Transform MakeFlatText(Transform parent, string text, Font font, int fontSize, float characterSize, Quaternion rotation, Color color, out TextMesh main)
    {
        var holder = new GameObject("Text").transform;
        holder.SetParent(parent, false);
        holder.localRotation = rotation;
        float o = 0.03f;
        Vector2[] offsets = { new Vector2(o, o), new Vector2(-o, o), new Vector2(o, -o), new Vector2(-o, -o) };
        foreach (Vector2 off in offsets) MakeTextMesh(holder, "Outline", text, font, fontSize, characterSize, new Vector3(off.x, off.y, 0.002f), UiTheme.Ink);
        main = MakeTextMesh(holder, "Main", text, font, fontSize, characterSize, Vector3.zero, color);
        return holder;
    }

    // Deux matériaux de police : le contour est dessiné avant le texte (file de rendu plus basse), pour ne jamais le recouvrir.
    static Material FontMaterial(Font font, bool main)
    {
        string path = "Assets/Prototype/Materials/Idle_Font" + (main ? "Main" : "Outline") + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(font.material); AssetDatabase.CreateAsset(m, path); }
        m.mainTexture = font.material.mainTexture;
        m.renderQueue = main ? 3010 : 3005;
        EditorUtility.SetDirty(m);
        return m;
    }

    static TextMesh MakeTextMesh(Transform parent, string name, string text, Font font, int fontSize, float characterSize, Vector3 localPos, Color color)
    {
        var go = new GameObject(name, typeof(TextMesh));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var tm = go.GetComponent<TextMesh>();
        tm.font = font;
        tm.text = text;
        tm.fontSize = fontSize;
        tm.characterSize = characterSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = FontMaterial(font, name == "Main");
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return tm;
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
