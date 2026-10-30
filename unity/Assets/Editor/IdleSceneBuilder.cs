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
    const float RoadShift = IdleLayout.FloorWidth - 7.8f;   // la route longe la dalle

    [MenuItem("Tools/Idle Prototype/Build scene")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildLighting();
        BuildCamera();
        BuildHost();
        var world = new GameObject("World").transform;
        var decor = new MeshKit();
        var shadows = new MeshKit();
        BuildGround(decor);
        BuildGrassDetails(decor);
        BuildSlab(decor, shadows);
        IdleChainBuilder.PaintFloor(decor);
        BuildRoad(decor);
        BuildTrees(decor, shadows);
        decor.ToObject("Decor", world, IdleMaterials.Palette("PaletteGround", GroundY));   // tout le décor fixe : un seul appel de dessin
        BuildSlots(world);
        BuildFeedback(world);
        BuildBelts(world);
        IdleChainBuilder.Build(world, Object.FindFirstObjectByType<FeedbackSpawner>());
        BuildPads(world);
        BuildCars(world);
        BuildContactShadows(world, shadows);
        BuildUnlockShow(world);
        IdleHudBuilder.Build();
        EditorSceneManager.SaveScene(scene, ScenePath);
        WebGLBuilder.EnsureSceneInBuildSettings();
        IdleAppIcon.Apply();
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
        light.color = new Color32(0xFF, 0xE8, 0xC4, 0xFF);   // soleil chaud
        light.intensity = 0.86f;
        light.shadows = LightShadows.None;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.72f, 0.84f, 1f) * 0.66f;
        RenderSettings.ambientEquatorColor = new Color(0.97f, 0.93f, 0.86f) * 0.62f;
        RenderSettings.ambientGroundColor = new Color(0.62f, 0.74f, 0.58f) * 0.46f;
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

        var audio = new GameObject("AudioManager", typeof(AudioManager), typeof(IdleAudioUnlock));
        var audioSo = new SerializedObject(audio.GetComponent<AudioManager>());
        audioSo.FindProperty("config").objectReferenceValue = AudioBuilder.LoadOrCreateConfig();
        audioSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ sol, dalle, route

    // Herbe : deux verts qui alternent par grandes plaques aux contours doux (couleurs aux coins d'une grille).
    static Color32 GrassColor(float x, float z)
    {
        float n = Mathf.Sin(x * 0.33f + 1.3f) + Mathf.Sin(z * 0.29f + 0.7f) + Mathf.Sin((x + z) * 0.17f + 2.1f) * 0.8f + Mathf.Sin((x - z) * 0.41f) * 0.5f;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.45f, 0.45f, n));
        return Color32.Lerp(MeshKit.Color("#5AAE38"), MeshKit.Color("#8ED74F"), t);
    }

    static void BuildGround(MeshKit decor)
    {
        const float Cell = 2f;
        float y = GroundY;
        decor.SetPaint("#74C445");
        decor.Box(new Vector3(-77f, y - 0.2f, -76f), new Vector3(160f, 0.19f, 160f));   // au loin, un seul vert
        for (float x = -22f; x < 28f; x += Cell)
            for (float z = -18f; z < 32f; z += Cell)
                decor.ColoredQuad(new Vector3(x, y, z), new Vector3(x, y, z + Cell), new Vector3(x + Cell, y, z + Cell), new Vector3(x + Cell, y, z),
                    GrassColor(x, z), GrassColor(x, z + Cell), GrassColor(x + Cell, z + Cell), GrassColor(x + Cell, z));
    }

    static bool OnSlabOrRoad(float x, float z, float margin)
    {
        bool slab = x > -margin && x < IdleLayout.FloorDepth + margin && z > -margin && z < IdleLayout.FloorWidth + margin;
        bool road = z > 8.3f + RoadShift - margin && z < 10.4f + RoadShift + margin;
        return slab || road;
    }

    // Touffes d'herbe et fleurs, semées au hasard (graine fixe) autour de la dalle.
    static void BuildGrassDetails(MeshKit decor)
    {
        long seed = 11;
        System.Func<float> r = () => { seed = (seed * 16807) % 2147483647; return seed / 2147483647f; };
        string[] blades = { "#4FA535", "#5DB53D", "#3F9430" };
        float y = GroundY;
        for (int n = 0; n < 400; n++)
        {
            float x = -11f + r() * 37f, z = -11f + r() * 37f, size = 1.05f + r() * 0.8f;
            string color = blades[n % blades.Length];
            if (OnSlabOrRoad(x, z, 0.25f)) continue;
            decor.SetPaint(color);
            for (int k = 0; k < 3; k++)
                decor.Cone(new Vector3(x + (k - 1) * 0.055f * size, y, z + ((k * 7) % 3 - 1) * 0.04f * size), 0.036f * size, (0.14f + (k % 2) * 0.09f) * size, 3);
        }
        for (int n = 0; n < 230; n++)
        {
            float x = -11f + r() * 37f, z = -11f + r() * 37f;
            bool yellow = r() < 0.3f;
            if (OnSlabOrRoad(x, z, 0.2f)) continue;
            int count = 1 + (int)(r() * 3f);
            for (int k = 0; k < count; k++)
            {
                float fx = x + (r() - 0.5f) * 0.5f, fz = z + (r() - 0.5f) * 0.5f;
                decor.SetPaint(yellow ? "#FFD23C" : "#FFFFFF");
                decor.Cone(new Vector3(fx, y + 0.004f, fz), 0.1f, 0.02f, 6);
                decor.SetPaint(yellow ? "#FF9A2E" : "#FFC400");
                decor.Cone(new Vector3(fx, y + 0.012f, fz), 0.04f, 0.022f, 6);
            }
        }
    }

    // Ombre douce : un rectangle d'opacité `alpha` (0 à 1) dont les bords s'effacent sur `soft` mètres.
    static void SoftShadow(MeshKit kit, Vector3 center, float halfX, float halfZ, float soft, float alpha)
    {
        Color32 inner = new Color32(20, 40, 20, (byte)Mathf.RoundToInt(alpha * 255f)), outer = new Color32(20, 40, 20, 0);
        float x0 = center.x - halfX, x1 = center.x + halfX, z0 = center.z - halfZ, z1 = center.z + halfZ, y = center.y;
        System.Func<float, float, Vector3> P = (x, z) => new Vector3(x, y, z);
        kit.ColoredQuad(P(x0, z0), P(x0, z1), P(x1, z1), P(x1, z0), inner, inner, inner, inner);
        kit.ColoredQuad(P(x0 - soft, z0), P(x0 - soft, z1), P(x0, z1), P(x0, z0), outer, outer, inner, inner);
        kit.ColoredQuad(P(x1, z0), P(x1, z1), P(x1 + soft, z1), P(x1 + soft, z0), inner, inner, outer, outer);
        kit.ColoredQuad(P(x0, z0 - soft), P(x0, z0), P(x1, z0), P(x1, z0 - soft), outer, inner, inner, outer);
        kit.ColoredQuad(P(x0, z1), P(x0, z1 + soft), P(x1, z1 + soft), P(x1, z1), inner, outer, outer, inner);
        kit.ColoredQuad(P(x0 - soft, z0 - soft), P(x0 - soft, z0), P(x0, z0), P(x0, z0 - soft), outer, outer, inner, outer);
        kit.ColoredQuad(P(x0 - soft, z1), P(x0 - soft, z1 + soft), P(x0, z1 + soft), P(x0, z1), outer, outer, outer, inner);
        kit.ColoredQuad(P(x1, z0 - soft), P(x1, z0), P(x1 + soft, z0), P(x1 + soft, z0 - soft), outer, inner, outer, outer);
        kit.ColoredQuad(P(x1, z1), P(x1, z1 + soft), P(x1 + soft, z1 + soft), P(x1 + soft, z1), inner, outer, outer, outer);
    }

    static void BuildSlab(MeshKit decor, MeshKit shadows)
    {
        float w = IdleLayout.FloorDepth, d = IdleLayout.FloorWidth, t = IdleLayout.SlabThickness;
        const float Wall = 0.07f, Rim = 0.15f;

        // muret gris tout autour, joints sombres
        decor.SetPaint("#98A6B4", 0.15f);
        decor.BevelBox(new Vector3(-Wall, -t, -Wall), new Vector3(w + 2f * Wall, t - 0.03f, d + 2f * Wall), 0.02f);
        decor.SetPaint("#76838F");
        const float Seam = 0.016f, Block = 0.8f;
        for (float x = Block; x < w; x += Block)
        {
            decor.Box(new Vector3(x, -t, d + Wall - 0.002f), new Vector3(Seam, t - 0.04f, 0.006f));
            decor.Box(new Vector3(x, -t, -Wall - 0.004f), new Vector3(Seam, t - 0.04f, 0.006f));
        }
        for (float z = Block; z < d; z += Block)
        {
            decor.Box(new Vector3(w + Wall - 0.002f, -t, z), new Vector3(0.006f, t - 0.04f, Seam));
            decor.Box(new Vector3(-Wall - 0.004f, -t, z), new Vector3(0.006f, t - 0.04f, Seam));
        }

        decor.SetPaint("#D5DEE7", 0.35f);
        decor.Box(new Vector3(0, -0.03f, 0), new Vector3(w, 0.03f, d));

        // damier : une case sur deux, cases de 1 m (les dernières sont coupées par le bord)
        decor.SetPaint("#C6D1DD", 0.35f);
        for (int x = 0; x < Mathf.CeilToInt(IdleLayout.FloorWidth); x++)
            for (int y = 0; y < Mathf.CeilToInt(IdleLayout.FloorDepth); y++)
            {
                if ((x + y) % 2 != 0) continue;
                float cw = Mathf.Min(1f, IdleLayout.FloorWidth - x), cd = Mathf.Min(1f, IdleLayout.FloorDepth - y);
                decor.FlatQuad(IdleLayout.ToWorld(x + cw * 0.5f, y + cd * 0.5f, 0.004f), cd, cw);
            }

        // liseré jaune en relief sur tout le bord
        decor.SetPaint("#FFC400", 0.6f);
        const float RimY = -0.04f, RimH = 0.075f, RimBevel = 0.03f;
        decor.BevelBox(new Vector3(-Wall, RimY, -Wall), new Vector3(w + 2f * Wall, RimH, Rim), RimBevel);
        decor.BevelBox(new Vector3(-Wall, RimY, d + Wall - Rim), new Vector3(w + 2f * Wall, RimH, Rim), RimBevel);
        decor.BevelBox(new Vector3(-Wall, RimY, -Wall), new Vector3(Rim, RimH, d + 2f * Wall), RimBevel);
        decor.BevelBox(new Vector3(w + Wall - Rim, RimY, -Wall), new Vector3(Rim, RimH, d + 2f * Wall), RimBevel);

        SoftShadow(shadows, new Vector3(w * 0.5f + 0.1f, GroundY + 0.012f, d * 0.5f + 0.1f), w * 0.5f + Wall, d * 0.5f + Wall, 0.5f, 0.5f);
    }

    static void BuildRoad(MeshKit decor)
    {
        // route derrière la dalle : web x 8,6 -> 10,2 (Z), sur toute la longueur (X)
        float y = GroundY + 0.01f;
        decor.SetPaint("#4E5864", 0.1f);
        decor.Box(new Vector3(-22f, y, 8.6f + RoadShift), new Vector3(50f, 0.01f, 1.6f));
        decor.SetPaint("#D9D2BE");
        decor.Box(new Vector3(-22f, y + 0.011f, 8.48f + RoadShift), new Vector3(50f, 0.004f, 0.12f));
        decor.Box(new Vector3(-22f, y + 0.011f, 10.2f + RoadShift), new Vector3(50f, 0.004f, 0.12f));
        decor.SetPaint("#FFD23C");
        for (float x = -22f; x < 28f; x += 1.6f) decor.Box(new Vector3(x, y + 0.011f, 9.38f + RoadShift), new Vector3(0.8f, 0.004f, 0.06f));
    }

    // ------------------------------------------------------------------ arbres (même tirage que le web : graine 7)

    static void BuildTrees(MeshKit decor, MeshKit shadows)
    {
        long seed = 7;
        System.Func<float> r = () => { seed = (seed * 16807) % 2147483647; return seed / 2147483647f; };
        string[] pineDark = { "#2FA55B", "#1E8A4C", "#27994F" }, pineLight = { "#6BD65A", "#3FB84A" };
        for (int n = 0; n < 650; n++)
        {
            float x = -7f + r() * 26f, yy = -7f + r() * 30f;
            bool inFloor = x > -0.9f && x < IdleLayout.FloorWidth + 0.9f && yy > -0.9f && yy < IdleLayout.FloorDepth + 1.1f;
            bool onRoad = x > 8.3f + RoadShift && x < 10.6f + RoadShift;
            if (inFloor || onRoad) continue;
            float s = (0.5f + r() * 0.3f) * 1.5f;
            bool light = r() < 0.25f;
            Vector3 b = IdleLayout.ToWorld(x, yy, GroundY);
            decor.SetPaint("#8A5A2B");
            decor.Cylinder(b, 0.07f * s, 0.4f * s);
            string[] cols = light ? pineLight : pineDark;
            int cones = light ? 2 : 3;
            for (int i = 0; i < cones; i++)
            {
                decor.SetPaint(cols[i % cols.Length], 0.2f);
                decor.Cone(b + new Vector3(0, (0.3f + i * 0.34f) * s, 0), (0.5f - i * 0.1f) * s, 0.62f * s);
            }
            SoftShadow(shadows, b + new Vector3(0.14f * s, 0.012f, 0.14f * s), 0.2f * s, 0.2f * s, 0.36f * s, 0.5f);
        }
    }

    // ------------------------------------------------------------------ arrivée d'un poste : colonne de lumière, poussière, caméra

    static void BuildUnlockShow(Transform parent)
    {
        var kit = new MeshKit();
        kit.Tube(0.95f, 7f, 16, new Color32(255, 240, 170, 170), new Color32(255, 240, 170, 0));
        GameObject beam = kit.ToObject("UnlockBeam", parent, IdleMaterials.ContactShadow());
        var show = new GameObject("UnlockShow", typeof(IdleUnlockShow));
        show.transform.SetParent(parent, false);
        var so = new SerializedObject(show.GetComponent<IdleUnlockShow>());
        so.FindProperty("cameraRig").objectReferenceValue = Object.FindFirstObjectByType<IdleCameraRig>();
        so.FindProperty("feedback").objectReferenceValue = Object.FindFirstObjectByType<FeedbackSpawner>();
        so.FindProperty("beam").objectReferenceValue = beam.transform;
        var slow = new GameObject("SlowMo", typeof(IdleSlowMo));
        slow.transform.SetParent(parent, false);
        var slowSo = new SerializedObject(slow.GetComponent<IdleSlowMo>());
        slowSo.FindProperty("rig").objectReferenceValue = Object.FindFirstObjectByType<IdleCameraRig>();
        slowSo.ApplyModifiedPropertiesWithoutUndo();
        IdleStationView[] views = FirstSlots();
        SerializedProperty list = so.FindProperty("stations");
        list.arraySize = views.Length;
        for (int i = 0; i < views.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // La vue du premier emplacement de chaque poste : c'est elle qui parle pour le poste.
    internal static IdleStationView[] FirstSlots()
    {
        var views = new IdleStationView[IdleLayout.Stations.Length];
        foreach (IdleStationView v in Object.FindObjectsByType<IdleStationView>(FindObjectsSortMode.None))
        {
            var so = new SerializedObject(v);
            if (so.FindProperty("slot").intValue == 0) views[so.FindProperty("station").intValue] = v;
        }
        return views;
    }

    // ------------------------------------------------------------------ ombres de contact : un seul maillage, un groupe par machine

    static void BuildContactShadows(Transform parent, MeshKit kit)
    {
        var starts = new List<int> { 0, kit.IndexCount };   // groupe 0 : dalle et arbres, déjà dans le maillage
        var stations = new List<int> { -1 };
        var slots = new List<int> { 0 };
        const float Y = 0.012f, Offset = 0.1f;
        for (int i = 0; i < IdleLayout.Slots.Length; i++)
            for (int k = 0; k < IdleLayout.Slots[i].Length; k++)
            {
                Vector3 c = IdleLayout.SlotCenter(i, k);
                SoftShadow(kit, new Vector3(c.x + Offset, Y, c.z + Offset), 0.85f, 0.85f, 0.28f, 0.46f);
                if (k == 0 && i > 0 && i - 1 < IdleLayout.Belts.Length)   // le tapis de la rangée apparaît avec sa première machine
                {
                    Vector2[] web = IdleLayout.Belts[i - 1];
                    for (int s = 0; s + 1 < web.Length; s++)
                    {
                        Vector3 p = IdleLayout.ToWorld(web[s], Y), q = IdleLayout.ToWorld(web[s + 1], Y);
                        Vector3 mid = (p + q) * 0.5f + new Vector3(Offset * 0.6f, 0f, Offset * 0.6f);
                        float halfX = Mathf.Abs(p.x - q.x) * 0.5f + BeltWidth * 0.5f, halfZ = Mathf.Abs(p.z - q.z) * 0.5f + BeltWidth * 0.5f;
                        SoftShadow(kit, mid, halfX, halfZ, 0.15f, 0.36f);
                    }
                }
                starts.Add(kit.IndexCount);
                stations.Add(i);
                slots.Add(k);
            }
        GameObject go = kit.ToObject("ContactShadows", parent, IdleMaterials.ContactShadow());
        var script = go.AddComponent<IdleContactShadows>();
        var so = new SerializedObject(script);
        so.FindProperty("target").objectReferenceValue = go.GetComponent<MeshFilter>();
        SerializedProperty list = so.FindProperty("groupStart");
        list.arraySize = starts.Count;
        for (int i = 0; i < starts.Count; i++) list.GetArrayElementAtIndex(i).intValue = starts[i];
        SerializedProperty groupStation = so.FindProperty("groupStation"), groupSlot = so.FindProperty("groupSlot");
        groupStation.arraySize = groupSlot.arraySize = stations.Count;
        for (int i = 0; i < stations.Count; i++)
        {
            groupStation.GetArrayElementAtIndex(i).intValue = stations[i];
            groupSlot.GetArrayElementAtIndex(i).intValue = slots[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ postes : fantôme (verrouillé) et machine (ouvert)

    static void BuildSlots(Transform parent)
    {
        var slots = new GameObject("Slots").transform;
        slots.SetParent(parent, false);
        Material ghostNext = IdleMaterials.Get("GhostNext", "#CFE3F6", 0f, true, 0.42f);
        Material ghostFar = IdleMaterials.Get("GhostFar", "#CFE3F6", 0f, true, 0.24f);
        Sprite lockSprite = UiSprites.Picto("lock");
        for (int i = 0; i < IdleLayout.Slots.Length; i++)
        {
            Transform first = null;
            for (int k = 0; k < IdleLayout.Slots[i].Length; k++)
            {
                var slot = new GameObject("Slot_" + i + "_" + k, typeof(IdleStationView)).transform;
                slot.SetParent(slots, false);
                slot.position = IdleLayout.SlotCenter(i, k);
                var so = new SerializedObject(slot.GetComponent<IdleStationView>());
                so.FindProperty("station").intValue = i;
                so.FindProperty("slot").intValue = k;
                if (k == 0)
                {
                    float s = IdleLayout.StationSize;
                    var ghost = new GameObject("Ghost").transform;
                    ghost.SetParent(slot, false);
                    var body = new MeshKit();
                    body.Box(new Vector3(-s * 0.5f, 0.01f, -s * 0.5f), new Vector3(s, 0.9f, s));
                    GameObject bodyGo = body.ToObject("Body", ghost, ghostFar);
                    var lockGo = new GameObject("Lock", typeof(SpriteRenderer), typeof(FaceCamera));
                    lockGo.transform.SetParent(ghost, false);
                    lockGo.transform.localPosition = new Vector3(0f, 1.9f, 0f);   // au-dessus du mot « NOUVEAU » de la plaque
                    lockGo.transform.localScale = Vector3.one * 0.5f;
                    var sr = lockGo.GetComponent<SpriteRenderer>();
                    sr.sprite = lockSprite;
                    sr.color = new Color32(0xB8, 0xC2, 0xCC, 0xFF);
                    first = IdleMachineBuilder.Build(slot, i);
                    so.FindProperty("ghost").objectReferenceValue = ghost.gameObject;
                    so.FindProperty("ghostBody").objectReferenceValue = bodyGo.GetComponent<Renderer>();
                    so.FindProperty("ghostNext").objectReferenceValue = ghostNext;
                    so.FindProperty("ghostFar").objectReferenceValue = ghostFar;
                    so.FindProperty("lockIcon").objectReferenceValue = sr;
                    so.FindProperty("machine").objectReferenceValue = first.gameObject;
                    so.FindProperty("floatHeight").floatValue = IdleLabelPlacement.StationHeight(i) + 0.5f;   // le « +x » part au-dessus de l'étiquette
                }
                else
                {
                    // même maillages que la première machine : un seul appel de dessin par type là où le matériel sait les instancier
                    GameObject copy = Object.Instantiate(first.gameObject, slot, false);
                    copy.name = "Machine";
                    Object.DestroyImmediate(copy.transform.Find("Label").gameObject);
                    so.FindProperty("machine").objectReferenceValue = copy;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    // ------------------------------------------------------------------ retour visuel (U2d) : textes flottants et confettis

    static void BuildFeedback(Transform parent)
    {
        var go = new GameObject("Feedback", typeof(FeedbackSpawner));
        go.transform.SetParent(parent, false);
        var so = new SerializedObject(go.GetComponent<FeedbackSpawner>());
        so.FindProperty("config").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FeedbackConfig>("Assets/Prototype/FeedbackConfig.asset");
        so.FindProperty("floatingTextPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prototype/FX_FloatingText.prefab");
        so.FindProperty("burstPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prototype/FX_Burst.prefab");
        so.FindProperty("confettiMaterial").objectReferenceValue = ConfettiMaterial();
        so.ApplyModifiedPropertiesWithoutUndo();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prototype/FX_FloatingText.prefab");
        var textSo = new SerializedObject(prefab.GetComponent<FloatingText>());
        textSo.FindProperty("outlinedFont").objectReferenceValue = FloatFontMaterial(UiTheme.Font);
        textSo.FindProperty("messageScale").floatValue = 0.68f;
        textSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(prefab);
        // particules des postes : un seul système ; chaque poste a son moment dans le cycle
        var fxObject = new GameObject("StationFx", typeof(ParticleSystem), typeof(IdleStationFx));
        fxObject.transform.SetParent(parent, false);
        Material spark = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prototype/FX_Burst.prefab").GetComponent<ParticleSystemRenderer>().sharedMaterial;
        fxObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = spark;
        var fxSo = new SerializedObject(fxObject.GetComponent<IdleStationFx>());
        fxSo.FindProperty("system").objectReferenceValue = fxObject.GetComponent<ParticleSystem>();
        fxSo.ApplyModifiedPropertiesWithoutUndo();

        float[] fxPhase = { 0.02f, 0.45f, 0.5f, 0.6f, 0.5f, 0.72f };   // cristal coupé, caméra montée, programme chargé, balayage, presse en bas, départ de la camionnette
        foreach (IdleStationView view in Object.FindObjectsByType<IdleStationView>(FindObjectsSortMode.None))
        {
            var vso = new SerializedObject(view);
            vso.FindProperty("rig").objectReferenceValue = Object.FindFirstObjectByType<IdleCameraRig>();
            if (vso.FindProperty("slot").intValue == 0)
            {
                vso.FindProperty("feedback").objectReferenceValue = go.GetComponent<FeedbackSpawner>();
                vso.FindProperty("fx").objectReferenceValue = fxObject.GetComponent<IdleStationFx>();
                vso.FindProperty("fxPhase").floatValue = fxPhase[vso.FindProperty("station").intValue];
            }
            vso.ApplyModifiedPropertiesWithoutUndo();
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
        Material topMat = IdleMaterials.Get("BeltTop", "#FFFFFF");
        Texture2D chevrons = ChevronTexture();
        topMat.SetTexture("_BaseMap", chevrons);
        topMat.mainTexture = chevrons;
        string[] itemHex = { "#3DDC5A", "#E9EEF3", "#4FB3FF", "#E5B96B" };
        string[] detailHex = { "#FFC400", "#1FA0FF", "#3DDC5A", "#FFFFFF" };
        var belts = new Transform[IdleLayout.Belts.Length];
        var tops = new Renderer[belts.Length];
        for (int bi = 0; bi < belts.Length; bi++)
        {
            Transform belt = new GameObject("Belt_" + bi).transform;
            belt.SetParent(root, false);
            belts[bi] = belt;
            Vector2[] web = IdleLayout.Belts[bi];
            var body = new MeshKit(); var top = new MeshKit();
            float ext = BeltWidth * 0.5f, acc = 0f;
            for (int k = 0; k < web.Length - 1; k++)
            {
                Vector3 p = IdleLayout.ToWorld(web[k]), q = IdleLayout.ToWorld(web[k + 1]);
                Vector3 dir = (q - p).normalized, nrm = new Vector3(-dir.z, 0f, dir.x);
                float len = Vector3.Distance(p, q);
                Vector3 p2 = p - dir * ext, q2 = q + dir * ext;   // chaque tronçon dépasse d'une demi-largeur : les coins sont pleins
                Vector3 c0 = p2 - nrm * ext, c1 = p2 + nrm * ext, c2 = q2 + nrm * ext, c3 = q2 - nrm * ext;
                Vector3 min = Vector3.Min(Vector3.Min(c0, c1), Vector3.Min(c2, c3)), max = Vector3.Max(Vector3.Max(c0, c1), Vector3.Max(c2, c3));
                body.SetPaint("#3C485A", 0.25f);
                body.BevelBox(new Vector3(min.x, 0f, min.z), new Vector3(max.x - min.x, BeltHeight, max.z - min.z), 0.02f);
                float v0 = (acc - ext) / ChevronPeriod, v1 = (acc + len + ext) / ChevronPeriod;
                Vector3 up = new Vector3(0f, BeltHeight + 0.004f, 0f);
                top.UvQuad(c0 + up, c1 + up, c2 + up, c3 + up, new Vector2(0f, v0), new Vector2(1f, v0), new Vector2(1f, v1), new Vector2(0f, v1));
                foreach (float side in new[] { -1f, 1f })   // rails jaunes de chaque côté
                {
                    Vector3 a = p2 + nrm * side * (ext + 0.01f), b = q2 + nrm * side * (ext + 0.01f), t = nrm * 0.0175f;
                    Vector3 rmin = Vector3.Min(Vector3.Min(a - t, a + t), Vector3.Min(b - t, b + t)), rmax = Vector3.Max(Vector3.Max(a - t, a + t), Vector3.Max(b - t, b + t));
                    body.SetPaint("#FFC400", 0.6f);
                    body.BevelBox(new Vector3(rmin.x, BeltHeight, rmin.z), new Vector3(rmax.x - rmin.x, 0.05f, rmax.z - rmin.z), 0.012f);
                }
                acc += len;
            }
            body.ToObject("Body", belt, IdleMaterials.Palette());
            tops[bi] = top.ToObject("Top", belt, topMat).GetComponent<Renderer>();

            // caisses qui avancent (leur nombre dépend de la longueur ; IdleBeltView les déplace)
            int count = Mathf.Clamp(Mathf.RoundToInt(acc / 0.5f), 3, 4);   // réserve : « Ajouter » en montre de plus en plus
            float s = bi == 3 ? 0.34f : bi == 0 ? 0.32f : 0.3f, h = bi == 0 || bi == 3 ? 0.3f : 0.22f;
            var itemKit = new MeshKit();   // boîte + détail dans un maillage, le même pour toutes les caisses du tapis
            itemKit.SetPaint(itemHex[bi], 0.3f);
            itemKit.BevelBox(new Vector3(-s * 0.5f, 0f, -s * 0.5f), new Vector3(s, h, s), 0.025f);
            itemKit.SetPaint(detailHex[bi], 0.5f);
            if (bi == 0 || bi == 3) itemKit.Box(new Vector3(-0.04f, h, -s * 0.5f), new Vector3(0.08f, 0.012f, s));   // ruban d'emballage
            else itemKit.Cylinder(new Vector3(0f, h, 0f), 0.07f, 0.08f);                                            // voyant
            Mesh itemMesh = itemKit.ToMesh("BeltItem_" + bi);
            for (int k = 0; k < count; k++)
            {
                var item = new GameObject("Item_" + k).transform;
                item.SetParent(belt, false);
                item.gameObject.AddComponent<MeshFilter>().sharedMesh = itemMesh;
                var itemRenderer = item.gameObject.AddComponent<MeshRenderer>();
                itemRenderer.sharedMaterial = IdleMaterials.Palette();
                itemRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                itemRenderer.receiveShadows = false;
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

    // ------------------------------------------------------------------ plaques d'achat au sol, gestes, sélection (UL)

    const float PadUnit = IdleLayout.PadSize / 50f;   // le dessin du web fait 50 de côté

    // Point du dessin du web (u vers la droite, v vers le bas de la plaque) dans le repère de la plaque.
    static Vector3 PadPoint(float u, float v, float y) { return new Vector3(-u * PadUnit, y, v * PadUnit); }

    const float IconLift = 0.31f, IconScale = 0.62f;

    // Point de l'icône : debout face à la caméra, qui ne tourne jamais, juste au-dessus du prix. Elle reste dans le maillage de la plaque : aucun appel de dessin de plus.
    // `u` va vers la droite de l'écran, `v` vers le bas ; (0, 0) est le centre de l'icône.
    static Vector3 PadIcon(float u, float v)
    {
        Quaternion view = Quaternion.Euler(IdleCameraRig.Pitch, IdleCameraRig.Yaw, 0f);
        return PadPoint(-5f, 5f, 0.01f) + view * new Vector3(u * PadUnit * IconScale, IconLift - v * PadUnit * IconScale, 0f);
    }

    static void IconBar(MeshKit k, float u, float v, float width, float height)
    {
        float w = width * 0.5f, h = height * 0.5f;
        k.Polygon(PadIcon(u - w, v - h), PadIcon(u + w, v - h), PadIcon(u + w, v + h), PadIcon(u - w, v + h));
    }

    // Un maillage par sorte de plaque et par état : tout tient dans un seul appel de dessin, dans l'ordre d'écriture (pas de test de profondeur).
    static Mesh PadMesh(IdleGame.PadKind kind, bool affordable)
    {
        const float Corner = 0.19f;
        float half = IdleLayout.PadSize * 0.5f, width = 4f * PadUnit;
        var k = new MeshKit();
        k.SetPaint("#141C26");
        k.RoundedPlate(half + 0.02f, Corner + 0.02f, 0f);
        k.SetPaint(kind == IdleGame.PadKind.Unlock || kind == IdleGame.PadKind.Slot ? "#26323F" : "#2A3644");
        k.RoundedPlate(half, Corner, 0.002f);
        k.SetPaint(affordable ? "#3DDC5A" : "#FFC400");
        if (affordable) k.RoundedRing(half, Corner, width, 0.004f);
        else k.RoundedRing(half, Corner, width, 0.004f, 0.13f, 0.09f);
        if (kind == IdleGame.PadKind.Speed)
        {
            k.SetPaint(affordable ? "#FFD84A" : "#9AA6B3");
            k.Polygon(PadIcon(-1.5f, 3.5f), PadIcon(-5.5f, 14.5f), PadIcon(9.5f, -3.5f), PadIcon(1.5f, -3.5f), PadIcon(4.5f, -14.5f), PadIcon(-9.5f, 3.5f));
        }
        else if (kind == IdleGame.PadKind.Add)
        {
            k.SetPaint(affordable ? "#FFFFFF" : "#9AA6B3");
            IconBar(k, 0f, 0f, 7f, 22f);
            IconBar(k, 0f, 0f, 22f, 7f);
        }
        else if (kind == IdleGame.PadKind.Hire)
        {
            k.SetPaint(affordable ? "#7FE3FF" : "#9AA6B3");     // tête de robot
            IconBar(k, 0f, 2f, 24f, 18f);
            IconBar(k, 0f, -10f, 3f, 7f);
            k.SetPaint("#141C26");
            IconBar(k, -5.5f, 1f, 5f, 6f);
            IconBar(k, 5.5f, 1f, 5f, 6f);
        }
        return k.ToMesh("Pad_" + kind + (affordable ? "_On" : "_Wait"));
    }

    static void BuildPads(Transform parent)
    {
        var root = new GameObject("Pads").transform;
        root.SetParent(parent, false);
        Material face = IdleMaterials.Overlay("PadFace", "#FFFFFF", 3500);   // les plaques sont dessinées par-dessus tout, comme dans le web
        Font font = UiTheme.Font;
        var kinds = new[] { IdleGame.PadKind.Speed, IdleGame.PadKind.Add, IdleGame.PadKind.Unlock, IdleGame.PadKind.Hire, IdleGame.PadKind.Slot };
        var on = new Mesh[kinds.Length];
        var wait = new Mesh[kinds.Length];
        for (int k = 0; k < kinds.Length; k++) { on[k] = PadMesh(kinds[k], true); wait[k] = PadMesh(kinds[k], false); }

        var views = new List<IdlePadView>();
        for (int i = 0; i < IdleLayout.Stations.Length; i++)
            for (int k = 0; k < kinds.Length; k++)
            {
                IdleGame.PadKind kind = kinds[k];
                if (kind == IdleGame.PadKind.Hire && i >= IdleLayout.PadHire.Length) continue;   // une plaque par robot à recruter
                if (kind == IdleGame.PadKind.Slot && IdleLayout.Slots[i].Length < 2) continue;   // une plaque qui va d'emplacement en emplacement
                var pad = new GameObject("Pad_" + i + "_" + kind, typeof(IdlePadView)).transform;
                pad.SetParent(root, false);
                pad.position = IdleLayout.ToWorld(IdleLayout.PadPosition(i, kind, 1), 0.03f);
                var visual = new GameObject("Visual").transform;
                visual.SetParent(pad, false);

                var faceObject = new GameObject("Face", typeof(MeshFilter), typeof(MeshRenderer));
                faceObject.transform.SetParent(visual, false);
                faceObject.GetComponent<MeshFilter>().sharedMesh = wait[k];
                var renderer = faceObject.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = face;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                GameObject tagRoot = null;
                if (kind == IdleGame.PadKind.Unlock || kind == IdleGame.PadKind.Hire || kind == IdleGame.PadKind.Slot)
                {
                    var tag = new GameObject("NewTag").transform;
                    tagRoot = tag.gameObject;
                    tag.SetParent(visual, false);
                    tag.localPosition = PadPoint(9f, -9f, 0.56f);
                    tag.gameObject.AddComponent<FaceCamera>();
                    MakeFlatText(tag, Loc.T(kind == IdleGame.PadKind.Hire ? "pad.hire" : kind == IdleGame.PadKind.Slot ? "pad.slot" : "pad.new"), font, 70, 0.0345f, Quaternion.identity, new Color32(0xFF, 0xC4, 0x00, 0xFF), out _, true);
                }
                var priceRoot = new GameObject("Price").transform;
                priceRoot.SetParent(visual, false);
                priceRoot.localPosition = PadPoint(-5f, 5f, 0.01f);
                priceRoot.gameObject.AddComponent<FaceCamera>();
                TextMesh price;
                MakeFlatText(priceRoot, "0", font, 130, 0.0385f, Quaternion.identity, Color.white, out price, true);

                var view = pad.GetComponent<IdlePadView>();
                var so = new SerializedObject(view);
                so.FindProperty("station").intValue = i;
                so.FindProperty("kind").enumValueIndex = k;
                so.FindProperty("visual").objectReferenceValue = visual;
                so.FindProperty("face").objectReferenceValue = faceObject.GetComponent<MeshFilter>();
                so.FindProperty("meshAffordable").objectReferenceValue = on[k];
                so.FindProperty("meshWaiting").objectReferenceValue = wait[k];
                so.FindProperty("priceText").objectReferenceValue = price;
                so.FindProperty("priceRoot").objectReferenceValue = priceRoot;
                so.FindProperty("tagRoot").objectReferenceValue = tagRoot;
                so.ApplyModifiedPropertiesWithoutUndo();
                views.Add(view);
            }

        // étoile dorée au-dessus de la plaque conseillée
        var starKit = new MeshKit();
        starKit.SetPaint("#0E1620");
        starKit.Star(0.3f, 0.15f, 0f);
        starKit.SetPaint("#FFC400");
        starKit.Star(0.21f, 0.1f, -0.001f);
        GameObject star = starKit.ToObject("AdvisedStar", root, IdleMaterials.Overlay("PadStar", "#FFFFFF", 3530));
        star.AddComponent<FaceCamera>();
        star.SetActive(false);

        // anneau jaune sous le poste sélectionné
        var ringKit = new MeshKit();
        ringKit.Paint = new Color32(255, 196, 0, 46);
        ringKit.RoundedPlate(1.0f, 0.1f, 0f);
        ringKit.Paint = new Color32(255, 196, 0, 255);
        ringKit.RoundedRing(1.04f, 0.12f, 0.1f, 0.003f);
        GameObject ring = ringKit.ToObject("SelectionRing", root, IdleMaterials.ContactShadow());
        
        var field = new GameObject("PadField", typeof(IdlePadField));
        field.transform.SetParent(root, false);
        var fso = new SerializedObject(field.GetComponent<IdlePadField>());
        SetObjects(fso.FindProperty("pads"), views.ToArray());
        fso.FindProperty("star").objectReferenceValue = star.transform;
        fso.ApplyModifiedPropertiesWithoutUndo();

        var input = new GameObject("WorldInput", typeof(IdleWorldInput), typeof(IdleSelection));
        input.transform.SetParent(root, false);
        var rig = Object.FindFirstObjectByType<IdleCameraRig>();
        var iso = new SerializedObject(input.GetComponent<IdleWorldInput>());
        iso.FindProperty("rig").objectReferenceValue = rig;
        iso.FindProperty("pads").objectReferenceValue = field.GetComponent<IdlePadField>();
        iso.FindProperty("selection").objectReferenceValue = input.GetComponent<IdleSelection>();
        iso.FindProperty("fx").objectReferenceValue = Object.FindFirstObjectByType<IdleStationFx>();
        iso.ApplyModifiedPropertiesWithoutUndo();
        var sso = new SerializedObject(input.GetComponent<IdleSelection>());
        sso.FindProperty("rig").objectReferenceValue = rig;
        sso.FindProperty("ring").objectReferenceValue = ring.transform;
        sso.ApplyModifiedPropertiesWithoutUndo();
    }

    // Texte couché sur le sol : le texte principal et 4 copies sombres décalées en diagonale (contour).
    static Transform MakeFlatText(Transform parent, string text, Font font, int fontSize, float characterSize, Quaternion rotation, Color color, out TextMesh main, bool overlay = false)
    {
        var holder = new GameObject("Text").transform;
        holder.SetParent(parent, false);
        holder.localRotation = rotation;
        if (overlay)   // texte des plaques : le contour est dessiné par le shader, un seul maillage
        {
            main = MakeTextMesh(holder, "Main", text, font, fontSize, characterSize, Vector3.zero, color, true);
            return holder;
        }
        float o = 0.024f;
        Vector2[] offsets = { new Vector2(o, o), new Vector2(-o, o), new Vector2(o, -o), new Vector2(-o, -o) };
        foreach (Vector2 off in offsets) MakeTextMesh(holder, "Outline", text, font, fontSize, characterSize, new Vector3(off.x, off.y, 0.002f), UiTheme.Ink, false);
        main = MakeTextMesh(holder, "Main", text, font, fontSize, characterSize, Vector3.zero, color, false);
        return holder;
    }

    // Deux matériaux de police : le contour est dessiné avant le texte (file de rendu plus basse), pour ne jamais le recouvrir.
    // Police pour le texte des plaques : même chose, mais dessinée par-dessus tout (sans test de profondeur).
    static Material OverlayFontMaterial(Font font, bool main)
    {
        string path = "Assets/Prototype/Materials/Idle_OverlayFont" + (main ? "Main" : "Outline") + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Idle/OverlayText")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Idle/OverlayText");
        m.mainTexture = font.material.mainTexture;
        m.SetColor("_OutlineColor", UiTheme.Ink);
        m.SetFloat("_OutlineWidth", 5f);
        m.renderQueue = 3540;
        EditorUtility.SetDirty(m);
        return m;
    }

    // Confettis : rectangles opaques, la couleur vient de chaque particule.
    static Material ConfettiMaterial()
    {
        const string path = "Assets/Prototype/Materials/Idle_Confetti.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
        return m;
    }

    // Police des étiquettes : par-dessus les plaques et leur pastille.
    internal static Material LabelFontMaterial(Font font)
    {
        const string path = "Assets/Prototype/Materials/Idle_LabelFont.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Idle/OverlayText")); AssetDatabase.CreateAsset(m, path); }
        m.mainTexture = font.material.mainTexture;
        m.SetColor("_OutlineColor", UiTheme.Ink);
        m.SetFloat("_OutlineWidth", 2f);
        m.renderQueue = 3542;
        EditorUtility.SetDirty(m);
        return m;
    }

    // Police des « +x » : contour plus épais que sur les plaques (texte plus petit à l'écran, ~12 % du corps).
    static Material FloatFontMaterial(Font font)
    {
        const string path = "Assets/Prototype/Materials/Idle_FloatFont.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Idle/OverlayText")); AssetDatabase.CreateAsset(m, path); }
        m.mainTexture = font.material.mainTexture;
        m.SetColor("_OutlineColor", UiTheme.Ink);
        m.SetFloat("_OutlineWidth", 5f);
        m.renderQueue = 3545;
        EditorUtility.SetDirty(m);
        return m;
    }

    internal static Material FontMaterial(Font font, bool main)
    {
        string path = "Assets/Prototype/Materials/Idle_Font" + (main ? "Main" : "Outline") + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(font.material); AssetDatabase.CreateAsset(m, path); }
        m.mainTexture = font.material.mainTexture;
        m.renderQueue = main ? 3010 : 3005;
        EditorUtility.SetDirty(m);
        return m;
    }

    internal static TextMesh MakeTextMesh(Transform parent, string name, string text, Font font, int fontSize, float characterSize, Vector3 localPos, Color color, bool overlay = false)
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
        r.sharedMaterial = overlay ? OverlayFontMaterial(font, name == "Main") : FontMaterial(font, name == "Main");
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return tm;
    }

    // ------------------------------------------------------------------ voitures qui passent sur la route

    static void BuildCars(Transform parent)
    {
        var cars = new GameObject("Cars").transform;
        cars.SetParent(parent, false);
        MakeCar(cars, "CarRed", "#F2453D", new Vector3(-9f, 0f, 8.78f + RoadShift), new Vector3(25f, 0f, 8.78f + RoadShift), 15f, 8f, 3f);
        MakeCar(cars, "CarBlue", "#1FA0FF", new Vector3(25f, 0f, 9.52f + RoadShift), new Vector3(-9f, 0f, 9.52f + RoadShift), 21f, 9f, 11f);
    }

    static void MakeCar(Transform parent, string name, string hex, Vector3 from, Vector3 to, float interval, float travel, float first)
    {
        float y = GroundY + 0.02f;
        from.y = to.y = y;
        var car = new GameObject(name, typeof(CarLoop)).transform;
        car.SetParent(parent, false);
        car.position = from;
        var kit = new MeshKit();
        kit.SetPaint(hex, 0.8f);
        kit.BevelBox(new Vector3(-0.275f, 0.06f, -0.5f), new Vector3(0.55f, 0.28f, 1.0f), 0.07f);
        kit.SetPaint("#BFE6FF", 0.9f);
        kit.BevelBox(new Vector3(-0.23f, 0.33f, -0.325f), new Vector3(0.46f, 0.25f, 0.55f), 0.07f);
        var wheel = new MeshKit();
        wheel.Cylinder(new Vector3(0f, -0.05f, 0f), 0.11f, 0.1f, 8);
        foreach (float side in new[] { -0.26f, 0.26f })
            foreach (float along in new[] { -0.3f, 0.3f })
                kit.Append(wheel, MeshKit.Color("#1E2A38"), Matrix4x4.TRS(new Vector3(side, 0.11f, along), Quaternion.Euler(0f, 0f, 90f), Vector3.one));
        kit.ToObject("Body", car, IdleMaterials.Palette("PaletteGround", GroundY));
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
