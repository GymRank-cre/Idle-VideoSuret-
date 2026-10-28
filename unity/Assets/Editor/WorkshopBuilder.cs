using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Menu Tools > Idle Prototype > Build workshop
// Habille le prototype : ambiance URP, atelier, technicien, détails.
// Tout est construit avec les formes de Unity (aucun asset externe).
// Relançable : l'atelier ("Workshop") est refait à chaque fois, le gameplay n'est pas touché.
public static class WorkshopBuilder
{
    const string Root = "Assets/Prototype";

    // ---- Dimensions de la pièce (resserrée autour de la zone de jeu) ----
    // La pièce couvre tout ce que la caméra peut voir quand le joueur est dans la zone jouable.
    const float RoomLeft = -8.5f, RoomRight = 14f, RoomFront = -14f, RoomBack = 11.5f;
    const float WallHeight = 4f;
    const float FrontWallHeight = 0.9f; // mur bas au sud (côté caméra)

    // Porte du mur de droite : les clients entrent et sortent par là (voir CustomerBuilder).
    public const float DoorCenterZ = -10.9f, DoorWidth = 2.6f, DoorHeight = 2.4f;
    public const float RightWallX = RoomRight;

    // Zone jouable : limites invisibles (centre du joueur).
    const float PlayMinX = -7.2f, PlayMaxX = 8.2f, PlayMinZ = -5f, PlayMaxZ = 10.8f;

    // ---- Palette (8 à 10 couleurs de base) ----
    static readonly Color Wood = new Color(0.80f, 0.62f, 0.40f);
    static readonly Color Steel = new Color(0.35f, 0.43f, 0.52f);
    static readonly Color OffWhite = new Color(0.93f, 0.92f, 0.88f);
    static readonly Color Yellow = new Color(1.00f, 0.80f, 0.10f);
    static readonly Color Orange = new Color(1.00f, 0.45f, 0.08f);
    static readonly Color Dark = new Color(0.13f, 0.15f, 0.18f);
    static readonly Color FloorA = new Color(0.82f, 0.84f, 0.86f);
    static readonly Color FloorB = new Color(0.77f, 0.79f, 0.82f);
    static readonly Color Carton = new Color(0.72f, 0.52f, 0.30f);
    static readonly Color Skin = new Color(0.96f, 0.76f, 0.60f);
    static readonly Color Sky = new Color(0.169f, 0.204f, 0.255f); // fond uni #2B3441

    // ---- Matériaux ----
    static Material mSteelDark, mWood, mSteel, mWhite, mYellow, mOrange, mDark, mCarton, mTape, mSkin, mPants, mBelt,
        mMetal, mGlass, mScreen, mNeon, mLedGreen, mLedRed, mReflect, mGreen, mGreenDark, mRed,
        mFloor, mStripes, mShadow, mHalo, mMat;

    static Font font;

    [MenuItem("Tools/Idle Prototype/Build workshop")]
    static void BuildMenu()
    {
        Run();
        EditorSceneManager.SaveOpenScenes();
    }

    // Appelé aussi par PrototypeSetup.Build à la fin de la construction de la scène.
    public static void Run()
    {
        EnsureFolder(Root + "/Materials");
        EnsureFolder(Root + "/Textures");
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        MakeMaterials();

        BuildAtmosphere();

        GameObject workshop = ResetRoot("Workshop");
        BuildRoom(workshop.transform);
        BuildWallDecor(workshop.transform);
        BuildFloorDecor(workshop.transform);

        ResourcePile pile = Object.FindFirstObjectByType<ResourcePile>();
        if (pile != null) BuildPallet(workshop.transform, pile);

        foreach (InstallStation st in Object.FindObjectsByType<InstallStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            BuildBench(st);

        UnlockPad pad = Object.FindFirstObjectByType<UnlockPad>(FindObjectsInactive.Include);
        if (pad != null) DressPad(pad);

        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null) BuildTechnician(player);

        DressPrefabs();

        // Décor fixe : fusionné par le batching statique (économise des draw calls sur téléphone).
        foreach (Transform t in workshop.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Atelier construit (Tools > Idle Prototype > Build workshop).");
    }

    // ------------------------------------------------------------------ ambiance

    static void BuildAtmosphere()
    {
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            l.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            l.color = new Color(1f, 0.96f, 0.88f);
            l.intensity = 1.15f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.75f;
        }

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.62f, 0.56f, 0.50f); // ambiance chaude
        RenderSettings.fog = false;

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Sky;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        // Volume global : tonemapping ACES, bloom léger, vignette légère, contraste/saturation.
        string path = Root + "/PostFX.asset";
        AssetDatabase.DeleteAsset(path);
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var tone = AddEffect<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.ACES);

        var bloom = AddEffect<Bloom>(profile);
        bloom.threshold.Override(1f);
        bloom.intensity.Override(0.25f);
        bloom.scatter.Override(0.6f);

        var vignette = AddEffect<Vignette>(profile);
        vignette.intensity.Override(0.22f);
        vignette.smoothness.Override(0.4f);

        var color = AddEffect<ColorAdjustments>(profile);
        color.contrast.Override(10f);
        color.saturation.Override(10f);

        EditorUtility.SetDirty(profile);

        GameObject old = GameObject.Find("PostFX");
        if (old != null) Object.DestroyImmediate(old);
        GameObject go = new GameObject("PostFX");
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    static T AddEffect<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T effect = ScriptableObject.CreateInstance<T>();
        effect.name = typeof(T).Name;
        profile.components.Add(effect);
        AssetDatabase.AddObjectToAsset(effect, profile);
        return effect;
    }

    // ------------------------------------------------------------------ pièce

    static void BuildRoom(Transform parent)
    {
        float midX = (RoomLeft + RoomRight) * 0.5f, midZ = (RoomFront + RoomBack) * 0.5f;
        float width = RoomRight - RoomLeft, depth = RoomBack - RoomFront;

        // Sol : damier de dalles de 1 m (le plan "Ground" du jeu garde son collider).
        GameObject ground = GameObject.Find("Ground");
        if (ground != null)
        {
            ground.transform.position = new Vector3(midX, 0f, midZ);
            ground.transform.localScale = new Vector3(width / 10f, 1f, depth / 10f); // un plan fait 10 m
            ground.GetComponent<Renderer>().sharedMaterial = mFloor;
            mFloor.SetTextureScale("_BaseMap", new Vector2(width / 2f, depth / 2f)); // la texture couvre 2 m
        }

        // Murs du fond et de gauche, ~4 m ; rien côté caméra.
        Prim(PrimitiveType.Cube, "WallBack", parent, new Vector3(midX - 0.15f, WallHeight * 0.5f, RoomBack + 0.15f),
            new Vector3(width + 0.3f, WallHeight, 0.3f), mWhite);
        Prim(PrimitiveType.Cube, "WallLeft", parent, new Vector3(RoomLeft - 0.15f, WallHeight * 0.5f, midZ),
            new Vector3(0.3f, WallHeight, depth), mWhite);

        // Bande de couleur
        Prim(PrimitiveType.Cube, "StripeBack", parent, new Vector3(midX, 1.0f, RoomBack - 0.02f), new Vector3(width, 0.3f, 0.06f), mSteel);
        Prim(PrimitiveType.Cube, "StripeLeft", parent, new Vector3(RoomLeft + 0.02f, 1.0f, midZ), new Vector3(0.06f, 0.3f, depth), mSteel);

        // Plinthe acier posée sur le sol, collée au mur (aucune face sombre, aucun espace).
        Prim(PrimitiveType.Cube, "BaseboardBack", parent, new Vector3(midX, 0.09f, RoomBack - 0.04f), new Vector3(width, 0.18f, 0.08f), mSteelDark);
        Prim(PrimitiveType.Cube, "BaseboardLeft", parent, new Vector3(RoomLeft + 0.04f, 0.09f, midZ), new Vector3(0.08f, 0.18f, depth), mSteelDark);

        // Liseré jaune et moulure haute qui ferment la pièce.
        Prim(PrimitiveType.Cube, "TopTrimBack", parent, new Vector3(midX, WallHeight - 0.45f, RoomBack - 0.03f), new Vector3(width, 0.06f, 0.06f), mYellow);
        Prim(PrimitiveType.Cube, "TopTrimLeft", parent, new Vector3(RoomLeft + 0.03f, WallHeight - 0.45f, midZ), new Vector3(0.06f, 0.06f, depth), mYellow);
        Prim(PrimitiveType.Cube, "CorniceBack", parent, new Vector3(midX - 0.15f, WallHeight - 0.1f, RoomBack - 0.1f), new Vector3(width + 0.3f, 0.2f, 0.22f), mSteelDark);
        Prim(PrimitiveType.Cube, "CorniceLeft", parent, new Vector3(RoomLeft + 0.1f, WallHeight - 0.1f, midZ), new Vector3(0.22f, 0.2f, depth), mSteelDark);

        // Mur de droite (comme celui de gauche) et mur bas au sud : la caméra ne voit jamais le vide.
        // Le mur de droite est percé d'une porte (par où arrivent et repartent les clients) : deux tronçons + un linteau.
        float doorMin = DoorCenterZ - DoorWidth * 0.5f, doorMax = DoorCenterZ + DoorWidth * 0.5f;
        RightWallPart(parent, "WallRight", RoomRight + 0.15f, WallHeight * 0.5f, 0.3f, WallHeight, doorMin, doorMax, mWhite);
        RightWallPart(parent, "StripeRight", RoomRight - 0.02f, 1.0f, 0.06f, 0.3f, doorMin, doorMax, mSteel);
        RightWallPart(parent, "BaseboardRight", RoomRight - 0.04f, 0.09f, 0.08f, 0.18f, doorMin, doorMax, mSteelDark);
        RightWallPart(parent, "TopTrimRight", RoomRight - 0.03f, WallHeight - 0.45f, 0.06f, 0.06f, doorMin, doorMax, mYellow);
        RightWallPart(parent, "CorniceRight", RoomRight - 0.1f, WallHeight - 0.1f, 0.22f, 0.2f, doorMin, doorMax, mSteelDark);
        Prim(PrimitiveType.Cube, "DoorLintel", parent, new Vector3(RoomRight + 0.15f, (DoorHeight + WallHeight) * 0.5f, DoorCenterZ),
            new Vector3(0.3f, WallHeight - DoorHeight, DoorWidth), mWhite);
        // cadre de porte jaune
        Prim(PrimitiveType.Cube, "DoorPostA", parent, new Vector3(RoomRight - 0.05f, DoorHeight * 0.5f, doorMin + 0.06f), new Vector3(0.14f, DoorHeight, 0.12f), mYellow);
        Prim(PrimitiveType.Cube, "DoorPostB", parent, new Vector3(RoomRight - 0.05f, DoorHeight * 0.5f, doorMax - 0.06f), new Vector3(0.14f, DoorHeight, 0.12f), mYellow);
        Prim(PrimitiveType.Cube, "DoorTop", parent, new Vector3(RoomRight - 0.05f, DoorHeight - 0.06f, DoorCenterZ), new Vector3(0.14f, 0.12f, DoorWidth), mYellow);
        Prim(PrimitiveType.Cube, "DoorSill", parent, new Vector3(RoomRight, 0.01f, DoorCenterZ), new Vector3(0.5f, 0.02f, DoorWidth), mDark);

        Prim(PrimitiveType.Cube, "WallFront", parent, new Vector3(midX, FrontWallHeight * 0.5f, RoomFront - 0.15f),
            new Vector3(width + 0.6f, FrontWallHeight, 0.3f), mWhite);
        Prim(PrimitiveType.Cube, "StripeFront", parent, new Vector3(midX, 0.45f, RoomFront + 0.02f), new Vector3(width, 0.2f, 0.06f), mSteel);
        Prim(PrimitiveType.Cube, "CapFront", parent, new Vector3(midX, FrontWallHeight + 0.03f, RoomFront - 0.15f), new Vector3(width + 0.6f, 0.06f, 0.36f), mYellow);

        // Limites invisibles de la zone jouable (le CharacterController les rencontre).
        Transform bounds = Group(parent, "PlayBounds", Vector3.zero);
        float spanX = PlayMaxX - PlayMinX + 6f, spanZ = PlayMaxZ - PlayMinZ + 6f;
        float cX = (PlayMinX + PlayMaxX) * 0.5f, cZ = (PlayMinZ + PlayMaxZ) * 0.5f;
        AddBoundsBox(bounds, "Left", new Vector3(PlayMinX - 1f, 2f, cZ), new Vector3(2f, 4f, spanZ));
        AddBoundsBox(bounds, "Right", new Vector3(PlayMaxX + 1f, 2f, cZ), new Vector3(2f, 4f, spanZ));
        AddBoundsBox(bounds, "South", new Vector3(cX, 2f, PlayMinZ - 1f), new Vector3(spanX, 4f, 2f));
        AddBoundsBox(bounds, "North", new Vector3(cX, 2f, PlayMaxZ + 1f), new Vector3(spanX, 4f, 2f));

        // Lignes de circulation (jaune et blanc, 0,2 m) : couloir du tas au poste, puis du poste à la dalle.
        BuildFloorLines(parent);
    }

    // Tronçon du mur de droite : de RoomFront à la porte, et de la porte à RoomBack.
    static void RightWallPart(Transform parent, string name, float x, float y, float thickness, float height,
        float doorMin, float doorMax, Material mat)
    {
        float a0 = RoomFront - 0.3f, a1 = doorMin;
        Prim(PrimitiveType.Cube, name + "A", parent, new Vector3(x, y, (a0 + a1) * 0.5f), new Vector3(thickness, height, a1 - a0), mat);
        float b0 = doorMax, b1 = RoomBack + 0.3f;
        Prim(PrimitiveType.Cube, name + "B", parent, new Vector3(x, y, (b0 + b1) * 0.5f), new Vector3(thickness, height, b1 - b0), mat);
    }

    static void AddBoundsBox(Transform parent, string name, Vector3 center, Vector3 size)
    {
        GameObject go = new GameObject("Bound" + name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.AddComponent<BoxCollider>().size = size; // invisible : pas de renderer
    }

    static void BuildFloorLines(Transform parent)
    {
        ResourcePile pile = Object.FindFirstObjectByType<ResourcePile>();
        UnlockPad pad = Object.FindFirstObjectByType<UnlockPad>(FindObjectsInactive.Include);
        InstallStation station1 = null, station2 = null;
        foreach (InstallStation st in Object.FindObjectsByType<InstallStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (st.name == "InstallStation") station1 = st;
            else if (st.name == "InstallStation2") station2 = st;
        }
        if (pile == null || pad == null || station1 == null || station2 == null) return;

        Transform lines = Group(parent, "FloorLines", Vector3.zero);
        const float w = 0.2f, y = 0.008f, half = 1.5f; // demi-côté des zones 3 x 3
        Vector3 p = pile.transform.position, s1 = station1.transform.position;
        Vector3 d = pad.transform.position, s2 = station2.transform.position;

        // Couloir tas -> poste : deux lignes blanches pleines et un axe jaune en tirets.
        float x0 = s1.x + half, x1 = p.x - half, len = x1 - x0, cx = (x0 + x1) * 0.5f;
        Prim(PrimitiveType.Cube, "LineN", lines, new Vector3(cx, y, s1.z + 1.3f), new Vector3(len, 0.01f, w), mWhite);
        Prim(PrimitiveType.Cube, "LineS", lines, new Vector3(cx, y, s1.z - 1.3f), new Vector3(len, 0.01f, w), mWhite);
        int dashes = Mathf.Max(1, Mathf.FloorToInt(len));
        for (int i = 0; i < dashes; i++)
            Prim(PrimitiveType.Cube, "Dash", lines, new Vector3(x0 + (i + 0.5f) * len / dashes, y, s1.z), new Vector3(0.6f, 0.01f, w), mYellow);

        // Poste -> dalle, puis dalle -> poste 2 : deux lignes jaunes de chaque côté du chemin.
        foreach (float dx in new[] { -1.3f, 1.3f })
        {
            float a = s1.z + half, b = d.z - half;
            if (b > a) Prim(PrimitiveType.Cube, "PathLine", lines, new Vector3(d.x + dx, y, (a + b) * 0.5f), new Vector3(w, 0.01f, b - a), mYellow);
            a = d.z + half; b = s2.z - half;
            if (b > a) Prim(PrimitiveType.Cube, "PathLine", lines, new Vector3(d.x + dx, y, (a + b) * 0.5f), new Vector3(w, 0.01f, b - a), mYellow);
        }
    }

    // ------------------------------------------------------------------ décor mural

    static void BuildWallDecor(Transform parent)
    {
        const float z = RoomBack - 0.05f; // face intérieure du mur du fond

        // Fenêtre
        Prim(PrimitiveType.Cube, "WindowFrame", parent, new Vector3(3.2f, 1.85f, z - 0.03f), new Vector3(2.8f, 1.7f, 0.12f), mWhite);
        Prim(PrimitiveType.Cube, "WindowGlass", parent, new Vector3(3.2f, 1.85f, z - 0.09f), new Vector3(2.5f, 1.4f, 0.05f), mGlass);
        Prim(PrimitiveType.Cube, "WindowBarV", parent, new Vector3(3.2f, 1.85f, z - 0.11f), new Vector3(0.08f, 1.4f, 0.05f), mWhite);
        Prim(PrimitiveType.Cube, "WindowBarH", parent, new Vector3(3.2f, 1.85f, z - 0.11f), new Vector3(2.5f, 0.08f, 0.05f), mWhite);
        Prim(PrimitiveType.Cube, "WindowSill", parent, new Vector3(3.2f, 1.0f, z - 0.12f), new Vector3(3.0f, 0.06f, 0.25f), mWood);

        // Enseigne néon "SÉCURITÉ"
        Prim(PrimitiveType.Cube, "NeonPlate", parent, new Vector3(-4.3f, 1.8f, z - 0.05f), new Vector3(4.2f, 0.8f, 0.08f), mDark);
        Prim(PrimitiveType.Cube, "NeonTop", parent, new Vector3(-4.3f, 2.22f, z - 0.11f), new Vector3(4.2f, 0.04f, 0.04f), mNeon);
        Prim(PrimitiveType.Cube, "NeonBottom", parent, new Vector3(-4.3f, 1.38f, z - 0.11f), new Vector3(4.2f, 0.04f, 0.04f), mNeon);
        Prim(PrimitiveType.Cube, "NeonLeft", parent, new Vector3(-6.38f, 1.8f, z - 0.11f), new Vector3(0.04f, 0.84f, 0.04f), mNeon);
        Prim(PrimitiveType.Cube, "NeonRight", parent, new Vector3(-2.22f, 1.8f, z - 0.11f), new Vector3(0.04f, 0.84f, 0.04f), mNeon);
        TextMesh sign = MakeText(parent, "NeonText", "SÉCURITÉ", new Vector3(-4.3f, 1.8f, z - 0.13f), 0.09f, 100,
            new Color(0.45f, 1f, 0.85f));
        sign.fontStyle = FontStyle.Bold;

        // Tableau d'outils
        Transform board = Group(parent, "ToolBoard", new Vector3(-0.3f, 1.7f, z - 0.05f));
        Prim(PrimitiveType.Cube, "Board", board, Vector3.zero, new Vector3(2.6f, 1.5f, 0.08f), mWood);
        for (int i = 0; i < 3; i++) // tournevis
        {
            float x = -0.9f + i * 0.28f;
            Prim(PrimitiveType.Cylinder, "ScrewdriverHandle", board, new Vector3(x, 0.35f, -0.07f), new Vector3(0.07f, 0.13f, 0.07f), i == 1 ? mYellow : mOrange);
            Prim(PrimitiveType.Cylinder, "ScrewdriverShaft", board, new Vector3(x, 0.02f, -0.07f), new Vector3(0.025f, 0.2f, 0.025f), mMetal);
        }
        // pince
        Prim(PrimitiveType.Cube, "PliersA", board, new Vector3(0.35f, 0.25f, -0.07f), new Vector3(0.07f, 0.55f, 0.03f), mMetal, new Vector3(0, 0, 10));
        Prim(PrimitiveType.Cube, "PliersB", board, new Vector3(0.43f, 0.25f, -0.075f), new Vector3(0.07f, 0.55f, 0.03f), mOrange, new Vector3(0, 0, -10));
        // bobines de câble et câbles qui pendent
        Prim(PrimitiveType.Cylinder, "CableCoilA", board, new Vector3(-0.6f, -0.35f, -0.07f), new Vector3(0.45f, 0.03f, 0.45f), mDark, new Vector3(90, 0, 0));
        Prim(PrimitiveType.Cylinder, "CableCoilB", board, new Vector3(0.2f, -0.38f, -0.07f), new Vector3(0.38f, 0.03f, 0.38f), mOrange, new Vector3(90, 0, 0));
        Prim(PrimitiveType.Cylinder, "CableHang", board, new Vector3(0.85f, -0.1f, -0.07f), new Vector3(0.03f, 0.35f, 0.03f), mDark);
        Prim(PrimitiveType.Cylinder, "CableHang2", board, new Vector3(0.95f, -0.05f, -0.07f), new Vector3(0.03f, 0.3f, 0.03f), mYellow);

        // Mur de caméras dôme (2 x 3)
        Prim(PrimitiveType.Cube, "CamPanel", parent, new Vector3(7.0f, 1.8f, z - 0.03f), new Vector3(3.6f, 1.7f, 0.06f), mDark);
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                Vector3 p = new Vector3(5.9f + col * 1.1f, 1.45f + row * 0.75f, z - 0.1f);
                Prim(PrimitiveType.Cylinder, "DomeBase", parent, p, new Vector3(0.42f, 0.02f, 0.42f), mWhite, new Vector3(90, 0, 0));
                Prim(PrimitiveType.Sphere, "Dome", parent, p + new Vector3(0, 0, -0.04f), new Vector3(0.36f, 0.36f, 0.22f), mGlass);
                Prim(PrimitiveType.Sphere, "DomeLens", parent, p + new Vector3(0, -0.02f, -0.1f), new Vector3(0.1f, 0.1f, 0.06f), mDark);
                Prim(PrimitiveType.Sphere, "DomeLed", parent, p + new Vector3(0.17f, 0.15f, -0.07f), new Vector3(0.03f, 0.03f, 0.03f), mLedRed);
            }
        }

        // Armoire électrique avec voyants
        Prim(PrimitiveType.Cube, "CabinetBody", parent, new Vector3(10.3f, 1.1f, z - 0.25f), new Vector3(1.1f, 2.2f, 0.5f), mSteel);
        Prim(PrimitiveType.Cube, "CabinetDoor", parent, new Vector3(10.3f, 1.1f, z - 0.52f), new Vector3(1.0f, 2.1f, 0.04f), mMetal);
        Prim(PrimitiveType.Cube, "CabinetHandle", parent, new Vector3(10.7f, 1.1f, z - 0.57f), new Vector3(0.05f, 0.3f, 0.05f), mDark);
        for (int i = 0; i < 4; i++)
        {
            Material led = i % 3 == 2 ? mLedRed : mLedGreen;
            Prim(PrimitiveType.Sphere, "CabinetLed", parent, new Vector3(9.95f + i * 0.17f, 1.85f, z - 0.56f), Vector3.one * 0.07f, led);
        }
        Prim(PrimitiveType.Cube, "CabinetLabel", parent, new Vector3(10.1f, 1.45f, z - 0.55f), new Vector3(0.5f, 0.18f, 0.01f), mYellow);

        // Poster
        Prim(PrimitiveType.Cube, "PosterFrame", parent, new Vector3(-7.6f, 1.8f, z - 0.04f), new Vector3(1.3f, 1.8f, 0.05f), mDark);
        Prim(PrimitiveType.Cube, "PosterPaper", parent, new Vector3(-7.6f, 1.8f, z - 0.075f), new Vector3(1.15f, 1.65f, 0.02f), mWhite);
        Prim(PrimitiveType.Cylinder, "PosterShield", parent, new Vector3(-7.6f, 2.0f, z - 0.095f), new Vector3(0.7f, 0.01f, 0.7f), mYellow, new Vector3(90, 0, 0));
        Prim(PrimitiveType.Cylinder, "PosterLens", parent, new Vector3(-7.6f, 2.0f, z - 0.11f), new Vector3(0.3f, 0.01f, 0.3f), mDark, new Vector3(90, 0, 0));
        Prim(PrimitiveType.Cube, "PosterBand", parent, new Vector3(-7.6f, 1.25f, z - 0.095f), new Vector3(0.95f, 0.2f, 0.01f), mSteel);
    }

    // ------------------------------------------------------------------ décor au sol

    static void BuildFloorDecor(Transform parent)
    {
        // Étagères métalliques avec cartons contre le mur de gauche
        float shelfX = RoomLeft + 0.45f;
        BuildShelf(parent, new Vector3(shelfX, 0f, 1.2f));
        BuildShelf(parent, new Vector3(shelfX, 0f, 4.5f));
        BuildShelf(parent, new Vector3(shelfX, 0f, 8.0f));

        // Bobbines de câble contre les bords, jamais sur le chemin du joueur
        BuildReel(parent, new Vector3(-6.8f, 0f, -5.5f), mOrange);
        BuildReel(parent, new Vector3(-5.8f, 0f, -6.6f), mYellow);
        BuildReel(parent, new Vector3(9.8f, 0f, 9.5f), mOrange);
        BuildReel(parent, new Vector3(8.8f, 0f, 10.2f), mDark);

        // Quelques cartons isolés, côté droit
        BuildCarton(parent, new Vector3(10.2f, 0.3f, 3.5f), new Vector3(0.9f, 0.6f, 0.7f), 15f);
        BuildCarton(parent, new Vector3(10.3f, 0.3f, 4.5f), new Vector3(0.8f, 0.6f, 0.7f), -8f);
        BuildCarton(parent, new Vector3(10.25f, 0.9f, 4.0f), new Vector3(0.7f, 0.5f, 0.6f), 30f);
        Shadow(parent, new Vector3(10.2f, 0.01f, 4.0f), 2.4f);

        // Deux détails autour du centre vide
        BuildReel(parent, new Vector3(2.2f, 0f, 6.2f), mOrange);
        BuildCarton(parent, new Vector3(7.2f, 0.3f, 3.2f), new Vector3(0.9f, 0.6f, 0.7f), 20f);
        BuildCarton(parent, new Vector3(7.9f, 0.3f, 3.6f), new Vector3(0.7f, 0.5f, 0.6f), -12f);
        Shadow(parent, new Vector3(7.5f, 0.01f, 3.4f), 2.2f);
    }

    static void BuildShelf(Transform parent, Vector3 basePos)
    {
        Transform shelf = Group(parent, "Shelf", basePos);
        float[] levels = { 0.15f, 0.8f, 1.45f, 2.1f };
        foreach (float sx in new[] { -0.27f, 0.27f })
            foreach (float sz in new[] { -1.0f, 1.0f })
                Prim(PrimitiveType.Cube, "Upright", shelf, new Vector3(sx, 1.1f, sz), new Vector3(0.06f, 2.2f, 0.06f), mMetal);
        for (int i = 0; i < levels.Length; i++)
        {
            Prim(PrimitiveType.Cube, "Board", shelf, new Vector3(0f, levels[i], 0f), new Vector3(0.6f, 0.04f, 2.1f), mSteel);
            if (i == levels.Length - 1) continue;
            float h = 0.45f;
            BuildCarton(shelf, new Vector3(0f, levels[i] + 0.02f + h * 0.5f, -0.6f), new Vector3(0.45f, h, 0.55f), 0f, true);
            if (i % 2 == 0)
                BuildCarton(shelf, new Vector3(0f, levels[i] + 0.02f + h * 0.5f, 0.05f), new Vector3(0.45f, h * 0.8f, 0.5f), 0f, true);
            BuildCarton(shelf, new Vector3(0f, levels[i] + 0.02f + h * 0.4f, 0.65f), new Vector3(0.4f, h * 0.7f, 0.45f), 0f, true);
        }
    }

    static void BuildReel(Transform parent, Vector3 pos, Material cable)
    {
        Transform reel = Group(parent, "CableReel", pos);
        Prim(PrimitiveType.Cylinder, "FlangeBottom", reel, new Vector3(0f, 0.04f, 0f), new Vector3(0.9f, 0.02f, 0.9f), mWood);
        Prim(PrimitiveType.Cylinder, "FlangeTop", reel, new Vector3(0f, 0.46f, 0f), new Vector3(0.9f, 0.02f, 0.9f), mWood);
        Prim(PrimitiveType.Cylinder, "Core", reel, new Vector3(0f, 0.25f, 0f), new Vector3(0.35f, 0.2f, 0.35f), mDark);
        Prim(PrimitiveType.Cylinder, "Cable", reel, new Vector3(0f, 0.25f, 0f), new Vector3(0.75f, 0.19f, 0.75f), cable);
        Shadow(reel, new Vector3(0f, 0.01f, 0f), 1.6f);
    }

    // Carton avec bande de ruban clair.
    static void BuildCarton(Transform parent, Vector3 pos, Vector3 size, float yaw, bool local = false)
    {
        GameObject box = Prim(PrimitiveType.Cube, "Carton", parent, pos, size, mCarton, new Vector3(0f, yaw, 0f));
        // Le ruban est en espace "unité" du carton : un peu plus grand que lui sur y et z.
        Prim(PrimitiveType.Cube, "Tape", box.transform, Vector3.zero, new Vector3(0.2f, 1.02f, 1.02f), mTape);
    }

    // ------------------------------------------------------------------ palette de ramassage

    static void BuildPallet(Transform parent, ResourcePile pile)
    {
        MeshRenderer pileRenderer = pile.GetComponent<MeshRenderer>();
        if (pileRenderer != null) pileRenderer.enabled = false; // le tas gris : on garde le trigger

        Vector3 c = pile.transform.position;
        Transform pallet = Group(parent, "Pallet", new Vector3(c.x, 0f, c.z));
        for (int i = -1; i <= 1; i++)
            Prim(PrimitiveType.Cube, "Bearer", pallet, new Vector3(0f, 0.06f, i * 1.0f), new Vector3(2.6f, 0.12f, 0.22f), mWood);
        for (int i = 0; i < 6; i++)
            Prim(PrimitiveType.Cube, "Board", pallet, new Vector3(-1.1f + i * 0.44f, 0.145f, 0f), new Vector3(0.38f, 0.05f, 2.6f), mWood);

        BuildCarton(pallet, new Vector3(-0.6f, 0.495f, -0.55f), new Vector3(0.95f, 0.6f, 0.75f), 4f);
        BuildCarton(pallet, new Vector3(0.6f, 0.495f, -0.5f), new Vector3(0.95f, 0.6f, 0.75f), -5f);
        BuildCarton(pallet, new Vector3(-0.55f, 0.495f, 0.55f), new Vector3(0.95f, 0.6f, 0.75f), -3f);
        BuildCarton(pallet, new Vector3(0.6f, 0.495f, 0.55f), new Vector3(0.95f, 0.6f, 0.75f), 6f);
        BuildCarton(pallet, new Vector3(-0.1f, 1.05f, 0.0f), new Vector3(1.0f, 0.5f, 0.8f), 14f);
        Shadow(pallet, new Vector3(0f, 0.012f, 0f), 4.2f);
    }

    // ------------------------------------------------------------------ établi

    static void BuildBench(InstallStation station)
    {
        Transform st = station.transform;
        Clear(st, "Bench");
        Transform bench = Group(st, "Bench", Vector3.zero);
        bench.localPosition = Vector3.zero;

        // Dalle du poste : acier, avec un tapis de travail sombre dessus
        Transform visual = st.Find("Visual");
        if (visual != null) visual.GetComponent<Renderer>().sharedMaterial = mSteel;
        Prim(PrimitiveType.Cube, "WorkMat", bench, new Vector3(0f, 0.11f, -0.1f), new Vector3(2.5f, 0.03f, 2.2f), mMat);

        // Établi au fond du poste (z local +1) : les caisses arrivent au centre, devant.
        float topY = 0.85f;
        foreach (float lx in new[] { -1.2f, 1.2f })
            foreach (float lz in new[] { 0.7f, 1.35f })
                Prim(PrimitiveType.Cylinder, "Leg", bench, new Vector3(lx, 0.35f, lz), new Vector3(0.1f, 0.45f, 0.1f), mMetal);
        Prim(PrimitiveType.Cube, "Top", bench, new Vector3(0f, topY, 1.03f), new Vector3(2.7f, 0.1f, 0.95f), mWood);
        Prim(PrimitiveType.Cube, "LowerShelf", bench, new Vector3(0f, 0.25f, 1.03f), new Vector3(2.5f, 0.04f, 0.8f), mSteel);
        BuildCarton(bench, new Vector3(0.6f, 0.5f, 1.05f), new Vector3(0.6f, 0.4f, 0.5f), 8f);

        // Écran d'ordinateur
        Prim(PrimitiveType.Cube, "MonitorBase", bench, new Vector3(-0.8f, topY + 0.07f, 1.1f), new Vector3(0.34f, 0.04f, 0.24f), mDark);
        Prim(PrimitiveType.Cube, "MonitorStand", bench, new Vector3(-0.8f, topY + 0.2f, 1.14f), new Vector3(0.06f, 0.24f, 0.05f), mDark);
        Prim(PrimitiveType.Cube, "MonitorBody", bench, new Vector3(-0.8f, topY + 0.5f, 1.1f), new Vector3(0.8f, 0.5f, 0.05f), mDark);
        Prim(PrimitiveType.Cube, "MonitorScreen", bench, new Vector3(-0.8f, topY + 0.5f, 1.07f), new Vector3(0.72f, 0.42f, 0.01f), mScreen);

        // Caméra dôme en cours d'assemblage + vis + tournevis
        Vector3 cam = new Vector3(0.45f, topY + 0.1f, 0.95f);
        Prim(PrimitiveType.Cylinder, "DomeBase", bench, cam, new Vector3(0.45f, 0.04f, 0.45f), mWhite);
        Prim(PrimitiveType.Sphere, "Dome", bench, cam + new Vector3(0, 0.03f, 0), new Vector3(0.36f, 0.3f, 0.36f), mGlass);
        Prim(PrimitiveType.Sphere, "DomeLens", bench, cam + new Vector3(0, 0.1f, -0.08f), new Vector3(0.1f, 0.1f, 0.1f), mDark);
        Prim(PrimitiveType.Cylinder, "DomeCover", bench, new Vector3(0.95f, topY + 0.1f, 0.85f), new Vector3(0.3f, 0.03f, 0.3f), mWhite);
        for (int i = 0; i < 3; i++)
            Prim(PrimitiveType.Cylinder, "Screw", bench, new Vector3(0.1f + i * 0.08f, topY + 0.065f, 0.62f), new Vector3(0.025f, 0.015f, 0.025f), mMetal);
        Prim(PrimitiveType.Cylinder, "Screwdriver", bench, new Vector3(0.1f, topY + 0.08f, 0.5f), new Vector3(0.05f, 0.1f, 0.05f), mOrange, new Vector3(0, 0, 90));

        Shadow(bench, new Vector3(0f, -0.09f, 0.2f), 4.6f);

        // Barre de progression : plus haute (hors de la tête du joueur), plus courte, fond arrondi et sombre.
        ProgressBar3D bar = station.GetComponentInChildren<ProgressBar3D>(true);
        if (bar != null)
        {
            bar.transform.localPosition = new Vector3(0f, 2.8f, 0f);
            var barSo = new SerializedObject(bar);
            barSo.FindProperty("width").floatValue = 1.1f;
            barSo.ApplyModifiedPropertiesWithoutUndo();

            Clear(bar.transform, "Back");
            Prim(PrimitiveType.Capsule, "Back", bar.transform, Vector3.zero, new Vector3(0.2f, 0.66f, 0.05f),
                Mat("WS_BarBack", new Color(0.06f, 0.07f, 0.09f), 0.2f), new Vector3(0f, 0f, 90f));

            Transform fill = (Transform)barSo.FindProperty("fill").objectReferenceValue;
            if (fill != null)
            {
                Vector3 fs = fill.localScale;
                fill.localScale = new Vector3(fs.x, 0.12f, 0.04f);
                Vector3 fp = fill.localPosition;
                fill.localPosition = new Vector3(fp.x, 0f, -0.04f);
            }
        }
    }

    // ------------------------------------------------------------------ dalle de déblocage

    static void DressPad(UnlockPad pad)
    {
        Transform t = pad.transform;
        Transform visual = t.Find("Visual");
        if (visual != null) visual.GetComponent<Renderer>().sharedMaterial = mStripes;

        Clear(t, "Halo");
        GameObject halo = Prim(PrimitiveType.Quad, "Halo", t, new Vector3(0f, 0.004f, 0f), new Vector3(5.6f, 5.6f, 1f), mHalo, new Vector3(90f, 0f, 0f));
        SetNoShadows(halo);

        // Étiquette du prix : contour sombre par un texte décalé derrière, recopié par TextMirror.
        TextMesh label = pad.GetComponentInChildren<TextMesh>(true);
        if (label == null) return;
        label.fontStyle = FontStyle.Bold;
        label.characterSize = 0.075f;
        label.color = Color.white;
        Clear(label.transform, "Outline");
        GameObject outlineGo = new GameObject("Outline", typeof(TextMesh));
        outlineGo.transform.SetParent(label.transform, false);
        outlineGo.transform.localPosition = new Vector3(0.025f, -0.025f, 0.02f);
        TextMesh outline = outlineGo.GetComponent<TextMesh>();
        CopyTextSettings(label, outline);
        outline.color = new Color(0.05f, 0.05f, 0.08f);
        outline.text = label.text;

        TextMirror mirror = label.GetComponent<TextMirror>();
        if (mirror == null) mirror = label.gameObject.AddComponent<TextMirror>();
        var so = new SerializedObject(mirror);
        so.FindProperty("source").objectReferenceValue = label;
        so.FindProperty("copy").objectReferenceValue = outline;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CopyTextSettings(TextMesh from, TextMesh to)
    {
        to.font = from.font;
        to.fontSize = from.fontSize;
        to.characterSize = from.characterSize;
        to.anchor = from.anchor;
        to.alignment = from.alignment;
        to.fontStyle = from.fontStyle;
        to.GetComponent<MeshRenderer>().sharedMaterial = from.GetComponent<MeshRenderer>().sharedMaterial;
    }

    // ------------------------------------------------------------------ technicien

    // true : vrai personnage humain (Mixamo) ; false : l'ancien technicien en formes simples (code gardé).
    const bool UseHumanCharacter = true;
    const float HumanHeight = 1.7f;

    static bool BuildHumanTechnician(PlayerController player)
    {
        GameObject prefab = HumanCharacterBuilder.EnsurePrefab();
        if (prefab == null) return false; // FBX absents : on garde le technicien en formes simples

        Transform p = player.transform;
        Clear(p, "Model");
        Clear(p, "Human");
        Clear(p, "ShadowBlob");

        MeshRenderer capsule = p.GetComponent<MeshRenderer>();
        if (capsule != null) capsule.enabled = false;

        var human = (GameObject)PrefabUtility.InstantiatePrefab(prefab, p);
        human.name = "Human";
        human.transform.localPosition = Vector3.zero;
        human.transform.localRotation = Quaternion.identity;
        human.transform.localScale = Vector3.one;

        // Échelle : le personnage mesure 1,7 m dans la scène.
        Bounds bounds = new Bounds();
        bool first = true;
        foreach (SkinnedMeshRenderer r in human.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }
        float height = Mathf.Max(0.1f, bounds.size.y / Mathf.Max(0.0001f, p.lossyScale.y));
        human.transform.localScale = Vector3.one * (HumanHeight / height);
        // Les pieds sur le sol : le centre du CharacterController est à ~1,08 m
        human.transform.localPosition = new Vector3(0f, -1.075f, 0f);

        HumanAnimator anim = human.GetComponent<HumanAnimator>();
        if (anim == null) anim = human.AddComponent<HumanAnimator>();
        var so = new SerializedObject(anim);
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("animator").objectReferenceValue = human.GetComponent<Animator>();
        so.ApplyModifiedPropertiesWithoutUndo();

        Shadow(p, new Vector3(0f, -1.07f, 0f), 1.4f).name = "ShadowBlob";
        return true;
    }

    static void BuildTechnician(PlayerController player)
    {
        if (UseHumanCharacter && BuildHumanTechnician(player)) return;

        Transform p = player.transform;
        Clear(p, "Model");
        Clear(p, "ShadowBlob");

        MeshRenderer capsule = p.GetComponent<MeshRenderer>();
        if (capsule != null) capsule.enabled = false; // la capsule orange : on garde le reste

        Transform model = Group(p, "Model", Vector3.zero);
        Transform body = Group(model, "Body", Vector3.zero);

        // Torse : gilet orange haute visibilité + bandes réfléchissantes + ceinture d'outils
        Transform torso = Group(body, "Torso", new Vector3(0f, -0.12f, 0f));
        Prim(PrimitiveType.Capsule, "Vest", torso, Vector3.zero, new Vector3(0.62f, 0.3f, 0.46f), mOrange);
        Prim(PrimitiveType.Cylinder, "BandLow", torso, new Vector3(0f, -0.08f, 0f), new Vector3(0.66f, 0.02f, 0.5f), mReflect);
        Prim(PrimitiveType.Cylinder, "BandHigh", torso, new Vector3(0f, 0.12f, 0f), new Vector3(0.62f, 0.02f, 0.47f), mReflect);
        Prim(PrimitiveType.Cylinder, "Belt", torso, new Vector3(0f, -0.28f, 0f), new Vector3(0.64f, 0.04f, 0.48f), mBelt);
        Prim(PrimitiveType.Cube, "PouchL", torso, new Vector3(-0.3f, -0.34f, 0.1f), new Vector3(0.12f, 0.14f, 0.1f), mBelt);
        Prim(PrimitiveType.Cube, "PouchR", torso, new Vector3(0.3f, -0.34f, 0.1f), new Vector3(0.12f, 0.14f, 0.1f), mBelt);
        Prim(PrimitiveType.Cube, "Tool", torso, new Vector3(0.34f, -0.4f, -0.05f), new Vector3(0.04f, 0.2f, 0.04f), mMetal);

        // Tête, yeux, casque de chantier jaune
        Prim(PrimitiveType.Cylinder, "Neck", body, new Vector3(0f, 0.2f, 0f), new Vector3(0.22f, 0.06f, 0.22f), mSkin);
        Prim(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 0.46f, 0f), Vector3.one * 0.62f, mSkin);
        Prim(PrimitiveType.Sphere, "EyeL", body, new Vector3(-0.12f, 0.38f, 0.28f), Vector3.one * 0.07f, mDark);
        Prim(PrimitiveType.Sphere, "EyeR", body, new Vector3(0.12f, 0.38f, 0.28f), Vector3.one * 0.07f, mDark);
        Prim(PrimitiveType.Sphere, "Helmet", body, new Vector3(0f, 0.56f, -0.01f), new Vector3(0.68f, 0.46f, 0.68f), mYellow);
        Prim(PrimitiveType.Cylinder, "Brim", body, new Vector3(0f, 0.44f, 0.02f), new Vector3(0.8f, 0.015f, 0.8f), mYellow);
        // Arête basse allongée de l'avant vers l'arrière, enfoncée dans la coque
        Prim(PrimitiveType.Sphere, "HelmetRidge", body, new Vector3(0f, 0.765f, -0.01f), new Vector3(0.08f, 0.07f, 0.5f), mYellow);

        // Petit sourire : arc de petites billes sombres sur la face
        for (int i = -2; i <= 2; i++)
        {
            float x = i * 0.04f;
            float y = 0.255f + 0.012f * i * i; // les coins remontent
            float zz = Mathf.Sqrt(Mathf.Max(0f, 0.31f * 0.31f - x * x - (y - 0.46f) * (y - 0.46f)));
            Prim(PrimitiveType.Sphere, "Smile", body, new Vector3(x, y, zz), new Vector3(0.03f, 0.026f, 0.016f), mDark);
        }

        // Bras (pivot à l'épaule)
        Transform armL = MakeArm(body, "ArmL", -0.4f);
        Transform armR = MakeArm(body, "ArmR", 0.4f);

        // Jambes (pivot à la hanche) et bottes noires
        Transform legL = MakeLeg(body, "LegL", -0.17f);
        Transform legR = MakeLeg(body, "LegR", 0.17f);

        // Ombre ronde douce
        Shadow(p, new Vector3(0f, -1.06f, 0f), 1.5f).name = "ShadowBlob";

        CharacterAnimator anim = model.gameObject.GetComponent<CharacterAnimator>();
        if (anim == null) anim = model.gameObject.AddComponent<CharacterAnimator>();
        var so = new SerializedObject(anim);
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("body").objectReferenceValue = body;
        so.FindProperty("torso").objectReferenceValue = torso;
        so.FindProperty("leftArm").objectReferenceValue = armL;
        so.FindProperty("rightArm").objectReferenceValue = armR;
        so.FindProperty("leftLeg").objectReferenceValue = legL;
        so.FindProperty("rightLeg").objectReferenceValue = legR;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Transform MakeArm(Transform body, string name, float x)
    {
        Transform arm = Group(body, name, new Vector3(x, 0.1f, 0f));
        Prim(PrimitiveType.Capsule, "Sleeve", arm, new Vector3(0f, -0.2f, 0f), new Vector3(0.16f, 0.2f, 0.16f), mOrange);
        Prim(PrimitiveType.Sphere, "Hand", arm, new Vector3(0f, -0.42f, 0f), Vector3.one * 0.14f, mSkin);
        return arm;
    }

    static Transform MakeLeg(Transform body, string name, float x)
    {
        Transform leg = Group(body, name, new Vector3(x, -0.44f, 0f));
        Prim(PrimitiveType.Cylinder, "Pants", leg, new Vector3(0f, -0.2f, 0f), new Vector3(0.2f, 0.2f, 0.2f), mPants);
        Prim(PrimitiveType.Cube, "Boot", leg, new Vector3(0f, -0.48f, 0.05f), new Vector3(0.26f, 0.16f, 0.36f), mDark);
        return leg;
    }

    // ------------------------------------------------------------------ prefabs (caisse, billet)

    static void DressPrefabs()
    {
        // Caisse : carton avec bande de ruban clair
        string cratePath = Root + "/Crate.prefab";
        if (File.Exists(cratePath))
        {
            GameObject crate = PrefabUtility.LoadPrefabContents(cratePath);
            crate.GetComponent<Renderer>().sharedMaterial = mCarton;
            Clear(crate.transform, "Tape");
            Prim(PrimitiveType.Cube, "Tape", crate.transform, Vector3.zero, new Vector3(0.2f, 1.03f, 1.03f), mTape);
            PrefabUtility.SaveAsPrefabAsset(crate, cratePath);
            PrefabUtility.UnloadPrefabContents(crate);
        }

        // Billet : liseré clair, centre plus sombre, flotte doucement (Bobbing sur "Visual").
        string billPath = Root + "/Bill.prefab";
        if (File.Exists(billPath))
        {
            GameObject bill = PrefabUtility.LoadPrefabContents(billPath);
            bill.GetComponent<Renderer>().enabled = false; // le visuel passe dans "Visual"
            Clear(bill.transform, "Visual");
            Transform visual = Group(bill.transform, "Visual", Vector3.zero);
            Prim(PrimitiveType.Cube, "Border", visual, Vector3.zero, Vector3.one, mGreen);
            Prim(PrimitiveType.Cube, "Center", visual, new Vector3(0f, 0.1f, 0f), new Vector3(0.84f, 1.0f, 0.62f), mGreenDark);
            Prim(PrimitiveType.Cube, "Seal", visual, new Vector3(0f, 0.2f, 0f), new Vector3(0.22f, 1.0f, 0.34f), mGreen);
            if (visual.GetComponent<Bobbing>() == null) visual.gameObject.AddComponent<Bobbing>();
            PrefabUtility.SaveAsPrefabAsset(bill, billPath);
            PrefabUtility.UnloadPrefabContents(bill);
        }
    }

    // ------------------------------------------------------------------ matériaux et textures

    static void MakeMaterials()
    {
        mWood = Mat("WS_Wood", Wood, 0.25f);
        mSteel = Mat("WS_Steel", Steel, 0.4f, 0.2f);
        mSteelDark = Mat("WS_SteelDark", new Color(0.42f, 0.50f, 0.59f), 0.35f, 0.1f);
        mWhite = Mat("WS_OffWhite", OffWhite, 0.15f);
        mYellow = Mat("WS_Yellow", Yellow, 0.3f);
        mOrange = Mat("WS_Orange", Orange, 0.25f);
        mDark = Mat("WS_Dark", Dark, 0.3f);
        mCarton = Mat("WS_Carton", Carton, 0.1f);
        mTape = Mat("WS_Tape", new Color(0.92f, 0.84f, 0.64f), 0.3f);
        mSkin = Mat("WS_Skin", Skin, 0.2f);
        mPants = Mat("WS_Pants", new Color(0.22f, 0.27f, 0.35f), 0.15f);
        mBelt = Mat("WS_Belt", new Color(0.40f, 0.26f, 0.14f), 0.2f);
        mMetal = Mat("WS_Metal", new Color(0.66f, 0.69f, 0.73f), 0.65f, 0.8f);
        mGlass = Mat("WS_Glass", new Color(0.70f, 0.85f, 0.95f), 0.9f, 0f, new Color(0.30f, 0.45f, 0.55f));
        mScreen = Mat("WS_Screen", new Color(0.15f, 0.55f, 0.85f), 0.6f, 0f, new Color(0.2f, 0.75f, 1.2f));
        mNeon = Mat("WS_Neon", new Color(0.3f, 1f, 0.85f), 0.5f, 0f, new Color(0.4f, 2.2f, 1.8f));
        mLedGreen = Mat("WS_LedGreen", new Color(0.2f, 0.9f, 0.3f), 0.5f, 0f, new Color(0.3f, 2.2f, 0.5f));
        mLedRed = Mat("WS_LedRed", new Color(0.95f, 0.15f, 0.15f), 0.5f, 0f, new Color(2.2f, 0.3f, 0.3f));
        mReflect = Mat("WS_Reflect", new Color(0.85f, 0.86f, 0.80f), 0.5f, 0.3f, new Color(0.25f, 0.25f, 0.22f));
        mGreen = Mat("WS_BillGreen", new Color(0.45f, 0.85f, 0.45f), 0.3f);
        mGreenDark = Mat("WS_BillDark", new Color(0.22f, 0.62f, 0.32f), 0.3f);
        mRed = Mat("WS_Red", new Color(0.85f, 0.2f, 0.2f), 0.3f);
        mMat = Mat("WS_WorkMat", new Color(0.30f, 0.35f, 0.42f), 0.1f);

        // Textures générées par code
        Texture2D checker = SaveTexture("Checker", 256, 256, (x, y) =>
        {
            bool a = (x < 128) == (y < 128);
            Color c = a ? FloorA : FloorB;
            int tx = x % 128, ty = y % 128;
            if (tx < 3 || ty < 3 || tx > 124 || ty > 124) c = Color.Lerp(c, new Color(0.68f, 0.71f, 0.75f), 0.6f);
            return c;
        }, true);
        mFloor = Mat("WS_Floor", Color.white, 0.35f);
        mFloor.SetTexture("_BaseMap", checker);
        mFloor.SetTextureScale("_BaseMap", new Vector2(6f, 5.75f));

        Texture2D stripes = SaveTexture("HazardStripes", 256, 256, (x, y) =>
        {
            int margin = 44;
            bool inner = x >= margin && x < 256 - margin && y >= margin && y < 256 - margin;
            if (inner) return Yellow;
            bool black = ((x + y) / 28) % 2 == 0;
            return black ? new Color(0.08f, 0.08f, 0.09f) : Yellow;
        }, false);
        mStripes = Mat("WS_Stripes", Color.white, 0.25f);
        mStripes.SetTexture("_BaseMap", stripes);

        Texture2D radial = SaveTexture("Radial", 128, 128, (x, y) =>
        {
            float dx = (x - 63.5f) / 64f, dy = (y - 63.5f) / 64f;
            float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
            float a = Mathf.SmoothStep(0f, 1f, 1f - d);
            return new Color(1f, 1f, 1f, a);
        }, false);
        mShadow = TransparentMat("WS_Shadow", new Color(0f, 0f, 0f, 0.45f), radial);
        mHalo = TransparentMat("WS_Halo", new Color(1f, 0.85f, 0.2f, 0.9f), radial);
    }

    static Material Mat(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
    {
        string path = Root + "/Materials/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", color);
        mat.color = color;
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Matériau transparent sans lumière (ombres rondes, halo).
    static Material TransparentMat(string name, Color color, Texture2D tex)
    {
        string path = Root + "/Materials/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetShaderPassEnabled("ShadowCaster", false);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Texture2D SaveTexture(string name, int w, int h, System.Func<int, int, Color> pixel, bool tile)
    {
        string assetPath = Root + "/Textures/" + name + ".png";
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, pixel(x, y));
        tex.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), assetPath), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.anisoLevel = tile ? 4 : 1;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    // ------------------------------------------------------------------ outils de construction

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale,
        Material mat, Vector3 euler = default)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.transform.localEulerAngles = euler;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static Transform Group(Transform parent, string name, Vector3 localPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.transform;
    }

    // Disque d'ombre douce : quad à plat avec dégradé radial, sans collider ni ombre portée.
    static Transform Shadow(Transform parent, Vector3 localPos, float size)
    {
        GameObject s = Prim(PrimitiveType.Quad, "Shadow", parent, localPos, new Vector3(size, size, 1f), mShadow, new Vector3(90f, 0f, 0f));
        SetNoShadows(s);
        return s.transform;
    }

    static void SetNoShadows(GameObject go)
    {
        Renderer r = go.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    static TextMesh MakeText(Transform parent, string name, string text, Vector3 pos, float charSize, int fontSize, Color color)
    {
        GameObject go = new GameObject(name, typeof(TextMesh));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        TextMesh tm = go.GetComponent<TextMesh>();
        tm.font = font;
        tm.text = text;
        tm.fontSize = fontSize;
        tm.characterSize = charSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        return tm;
    }

    static GameObject ResetRoot(string name)
    {
        GameObject old = GameObject.Find(name);
        if (old != null) Object.DestroyImmediate(old);
        return new GameObject(name);
    }

    static void Clear(Transform parent, string childName)
    {
        Transform old = parent.Find(childName);
        while (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
            old = parent.Find(childName);
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
