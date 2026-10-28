using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Menu Tools > Idle Prototype > Setup human character
// Prépare le technicien humain (modèle Mixamo) à partir des 4 FBX de Assets/Characters :
// import Humanoid, boucles des animations, matériaux URP, AnimatorController, prefab équipé.
// Relançable. La mise en scène (échelle, ombre, branchement) est faite par WorkshopBuilder.
public static class HumanCharacterBuilder
{
    const string Folder = "Assets/Characters";
    public const string ModelPath = Folder + "/Technicien.fbx";
    const string IdlePath = Folder + "/Breathing Idle.fbx";
    const string WalkPath = Folder + "/Walking.fbx";
    const string RunPath = Folder + "/Running.fbx";
    public const string PrefabPath = Folder + "/TechnicienEquipe.prefab";
    public const string ControllerPath = Folder + "/Technicien.controller";

    [MenuItem("Tools/Idle Prototype/Setup human character")]
    static void SetupMenu()
    {
        GameObject prefab = EnsurePrefab();
        Debug.Log(prefab != null ? "Technicien humain prêt : " + PrefabPath : "Technicien humain : fichiers FBX manquants dans " + Folder);
    }

    // Renvoie le prefab du technicien équipé, ou null si les FBX sont absents.
    public static GameObject EnsurePrefab()
    {
        if (!File.Exists(ModelPath) || !File.Exists(IdlePath) || !File.Exists(WalkPath) || !File.Exists(RunPath))
            return null;

        Avatar avatar = ConfigureCharacter();
        if (avatar == null) return null;

        AnimationClip idle = ConfigureAnimation(IdlePath, avatar, "Idle");
        AnimationClip walk = ConfigureAnimation(WalkPath, avatar, "Walk");
        AnimationClip run = ConfigureAnimation(RunPath, avatar, "Run");
        if (idle == null || walk == null || run == null) return null;

        BuildController(idle, walk, run);
        return BuildPrefab();
    }

    // ------------------------------------------------------------------ import

    static Avatar ConfigureCharacter()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        if (importer.animationType != ModelImporterAnimationType.Human
            || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
            || importer.importAnimation)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false; // le modèle n'a qu'une pose en T, pas d'animation utile
            importer.SaveAndReimport();
        }

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            Debug.LogError("Technicien.fbx : l'avatar Humanoid n'est pas valide.");
            return null;
        }
        return avatar;
    }

    static AnimationClip ConfigureAnimation(string path, Avatar avatar, string clipName)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        bool needsSetup = importer.animationType != ModelImporterAnimationType.Human
            || importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther
            || importer.sourceAvatar != avatar
            || importer.clipAnimations == null || importer.clipAnimations.Length == 0
            || !importer.clipAnimations[0].loopTime;

        if (needsSetup)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.importAnimation = true;
            importer.SaveAndReimport();

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = clipName;
                clip.loopTime = true;   // l'animation tourne en boucle
                clip.loopPose = true;
                // « In Place » : on fige la position et la rotation de la racine dans la pose
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview"));
    }

    // ------------------------------------------------------------------ animator

    static void BuildController(AnimationClip idle, AnimationClip walk, AnimationClip run)
    {
        // Le contrôleur est référencé par les prefabs (technicien, clients) : on ne le recrée que s'il est invalide,
        // sinon chaque reconstruction casserait ces références.
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existing != null && ControllerMatches(existing, idle, walk, run)) return;

        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("AnimSpeed", AnimatorControllerParameterType.Float);
        AnimatorControllerParameter[] ps = controller.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].name == "AnimSpeed") ps[i].defaultFloat = 1f;
        controller.parameters = ps;

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idleState = sm.AddState("Idle", new Vector3(250f, 0f));
        AnimatorState walkState = sm.AddState("Walk", new Vector3(500f, -60f));
        AnimatorState runState = sm.AddState("Run", new Vector3(750f, 0f));
        idleState.motion = idle;
        walkState.motion = walk;
        runState.motion = run;
        // la vitesse de lecture suit la vitesse réelle (pas de glissement des pieds)
        walkState.speedParameterActive = true;
        walkState.speedParameter = "AnimSpeed";
        runState.speedParameterActive = true;
        runState.speedParameter = "AnimSpeed";
        sm.defaultState = idleState;

        AddTransition(idleState, walkState, AnimatorConditionMode.Greater, 0.1f);
        AddTransition(walkState, idleState, AnimatorConditionMode.Less, 0.06f);
        AddTransition(walkState, runState, AnimatorConditionMode.Greater, 0.5f);
        AddTransition(runState, walkState, AnimatorConditionMode.Less, 0.4f);
        EditorUtility.SetDirty(controller);
    }

    static bool ControllerMatches(AnimatorController controller, AnimationClip idle, AnimationClip walk, AnimationClip run)
    {
        if (controller.layers.Length == 0) return false;
        int found = 0;
        foreach (ChildAnimatorState s in controller.layers[0].stateMachine.states)
        {
            if (s.state.name == "Idle" && s.state.motion == idle) found++;
            else if (s.state.name == "Walk" && s.state.motion == walk) found++;
            else if (s.state.name == "Run" && s.state.motion == run) found++;
        }
        return found == 3;
    }

    static void AddTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = 0.1f; // transitions courtes
        t.AddCondition(mode, threshold, "Speed");
    }

    // ------------------------------------------------------------------ prefab équipé

    static GameObject BuildPrefab()
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        go.name = "TechnicienEquipe";
        try
        {
            var animator = go.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            animator.applyRootMotion = false; // le déplacement reste celui du PlayerController
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            DressMaterials(go);
            AddEquipment(go, animator);

            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    // Le FBX n'a pas de texture : un matériau URP/Lit uni par pièce (casque jaune, gilet orange...).
    static void DressMaterials(GameObject go)
    {
        EnsureFolder(Folder + "/Materials");
        Material skin = Mat("Tech_Skin", new Color(0.87f, 0.67f, 0.52f), 0.2f);
        Material hair = Mat("Tech_Hair", new Color(0.14f, 0.10f, 0.07f), 0.3f);
        Material lashes = Mat("Tech_Lashes", new Color(0.06f, 0.05f, 0.05f), 0.1f);
        Material boots = Mat("Tech_Boots", new Color(0.08f, 0.08f, 0.09f), 0.35f);
        Material pants = Mat("Tech_Pants", new Color(0.16f, 0.21f, 0.31f), 0.15f);
        Material shirt = Mat("Tech_Shirt", new Color(0.42f, 0.48f, 0.56f), 0.15f);
        Material vest = Mat("Tech_Vest", new Color(1f, 0.45f, 0.08f), 0.25f);
        Material helmet = Mat("Tech_Helmet", new Color(1f, 0.80f, 0.10f), 0.55f);

        foreach (SkinnedMeshRenderer r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Material m = skin;
            string n = r.name.ToLowerInvariant();
            if (n.Contains("hair")) m = hair;
            else if (n.Contains("eyelash")) m = lashes;
            else if (n.Contains("boots")) m = boots;
            else if (n.Contains("pants")) m = pants;
            else if (n.Contains("shirt")) m = shirt;
            else if (n.Contains("vest")) m = vest;
            else if (n.Contains("helmet")) m = helmet;

            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats;
        }
    }

    // Équipement en formes simples attaché aux os : bandes réfléchissantes, arête du casque,
    // ceinture d'outils, yeux et sourire. Les mesures viennent des maillages du modèle (en mètres).
    static void AddEquipment(GameObject go, Animator animator)
    {
        Material reflect = Mat("Tech_Reflect", new Color(0.88f, 0.89f, 0.82f), 0.5f, new Color(0.3f, 0.3f, 0.26f));
        Material helmetMat = Mat("Tech_Helmet", new Color(1f, 0.80f, 0.10f), 0.55f);
        Material belt = Mat("Tech_Belt", new Color(0.40f, 0.26f, 0.14f), 0.2f);
        Material metal = Mat("Tech_Metal", new Color(0.66f, 0.69f, 0.73f), 0.65f);
        Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);

        Bounds vestB = BoundsOf(go, "vest");

        // 2 bandes réfléchissantes autour du gilet, calées sur les vrais sommets du maillage à chaque hauteur.
        // Chaque bande est un anneau de 4 cm qui suit la surface du gilet (+2 mm), ouvert là où le gilet est ouvert.
        reflect.SetFloat("_Cull", 0f); // visible des deux côtés
        Transform lowerBone = animator.GetBoneTransform(HumanBodyBones.Spine);
        ReflectiveBand(go, "ReflectiveBandLow", lowerBone != null ? lowerBone : chest, vestB.min.y + vestB.size.y * 0.28f, 0.04f, reflect);
        ReflectiveBand(go, "ReflectiveBandHigh", chest, vestB.min.y + vestB.size.y * 0.58f, 0.04f, reflect);
        SliceOf(go, "vest", vestB.min.y + 0.05f, 0.04f, out Vector3 hipC, out float hipX, out float hipZ);
        float rx = hipX * 0.5f, rz = hipZ * 0.5f, zc = hipC.z;

        // Arête basse du casque, de l'avant vers l'arrière, posée sur le vrai sommet du maillage
        HelmetTop(go, out float topY, out float topZ, out float depthZ);
        Part(PrimitiveType.Sphere, "HelmetRidge", head, new Vector3(0f, topY - 0.01f, topZ), new Vector3(0.03f, 0.024f, depthZ * 0.7f), helmetMat);

        // Ceinture d'outils sur les hanches : anneau, deux pochettes, un outil
        float bx = rx * 0.98f, bz = rz * 1.02f; // à peine plus large que le gilet, qui descend jusqu'aux hanches
        float beltY = vestB.min.y + 0.035f;
        Part(PrimitiveType.Cylinder, "ToolBelt", hips, new Vector3(0f, beltY, zc), new Vector3(bx * 2f + 0.02f, 0.022f, bz * 2f + 0.02f), belt);
        Part(PrimitiveType.Cube, "PouchL", hips, new Vector3(-bx - 0.005f, beltY - 0.045f, zc + 0.03f), new Vector3(0.04f, 0.085f, 0.075f), belt);
        Part(PrimitiveType.Cube, "PouchR", hips, new Vector3(bx + 0.005f, beltY - 0.045f, zc + 0.03f), new Vector3(0.04f, 0.085f, 0.075f), belt);
        Part(PrimitiveType.Cube, "Tool", hips, new Vector3(bx + 0.03f, beltY - 0.1f, zc - 0.03f), new Vector3(0.018f, 0.13f, 0.018f), metal);

        AddFace(go, animator);
    }

    // Visage : yeux, sourcils et sourire sur la face (les cils du modèle donnent la hauteur des yeux).
    // Le modèle n'a pas de texture, donc pas d'yeux : utilisé aussi pour les clients.
    public static void AddFace(GameObject go, Animator animator)
    {
        Material white = Mat("Tech_EyeWhite", new Color(0.95f, 0.95f, 0.93f), 0.5f);
        Material dark = Mat("Tech_Dark", new Color(0.07f, 0.06f, 0.06f), 0.3f);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Vector3 eyeC = BoundsOf(go, "eyelash").center;

        foreach (float sx in new[] { -0.032f, 0.032f })
        {
            Part(PrimitiveType.Sphere, "EyeWhite", head, new Vector3(sx, eyeC.y - 0.002f, eyeC.z + 0.006f), new Vector3(0.017f, 0.010f, 0.010f), white);
            Part(PrimitiveType.Sphere, "Pupil", head, new Vector3(sx, eyeC.y - 0.002f, eyeC.z + 0.012f), new Vector3(0.0085f, 0.0085f, 0.005f), dark);
            Part(PrimitiveType.Cube, "Eyebrow", head, new Vector3(sx, eyeC.y + 0.016f, eyeC.z + 0.004f), new Vector3(0.032f, 0.006f, 0.008f), dark);
        }
        for (int i = -2; i <= 2; i++)
        {
            Vector3 p = new Vector3(i * 0.011f, eyeC.y - 0.062f + 0.0025f * i * i, eyeC.z + 0.004f);
            Part(PrimitiveType.Sphere, "Smile", head, p, new Vector3(0.0085f, 0.0075f, 0.006f), dark);
        }
    }

    // Crée une pièce à la position MONDE donnée (modèle non animé, en pose de référence) puis l'attache à l'os.
    static void Part(PrimitiveType type, string name, Transform bone, Vector3 worldPos, Vector3 worldScale, Material mat)
    {
        GameObject p = GameObject.CreatePrimitive(type);
        p.name = name;
        Object.DestroyImmediate(p.GetComponent<Collider>());
        p.GetComponent<Renderer>().sharedMaterial = mat;
        p.transform.position = worldPos;
        p.transform.localScale = worldScale;
        p.transform.SetParent(bone, true);
    }

    // Anneau mince collé au gilet : pour chaque secteur autour du torse, le rayon est celui du sommet
    // le plus extérieur du gilet à cette hauteur (+2 mm). Les petits trous sont comblés (dos fermé) ;
    // un grand trou (ouverture du devant) reste ouvert.
    static void ReflectiveBand(GameObject go, string name, Transform bone, float y, float height, Material mat)
    {
        const int sectors = 72;
        const float offset = 0.002f;
        var radius = new float[sectors];
        for (int i = 0; i < sectors; i++) radius[i] = -1f;

        SkinnedMeshRenderer vest = null;
        foreach (SkinnedMeshRenderer r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (r.name.ToLowerInvariant().Contains("vest")) { vest = r; break; }
        if (vest == null) return;

        var baked = new Mesh();
        vest.BakeMesh(baked);
        var points = new System.Collections.Generic.List<Vector3>();
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (Vector3 v in baked.vertices)
        {
            Vector3 w = vest.transform.TransformPoint(v);
            if (Mathf.Abs(w.y - y) > height * 0.5f + 0.02f) continue;
            points.Add(w);
            minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.x);
            minZ = Mathf.Min(minZ, w.z); maxZ = Mathf.Max(maxZ, w.z);
        }
        Object.DestroyImmediate(baked);
        if (points.Count < 8) return;

        float cx = (minX + maxX) * 0.5f, cz = (minZ + maxZ) * 0.5f;
        foreach (Vector3 w in points)
        {
            float dx = w.x - cx, dz = w.z - cz;
            float angle = Mathf.Atan2(dx, dz); // 0 = devant (+z)
            int s = Mathf.Clamp(Mathf.FloorToInt((angle + Mathf.PI) / (2f * Mathf.PI) * sectors), 0, sectors - 1);
            radius[s] = Mathf.Max(radius[s], Mathf.Sqrt(dx * dx + dz * dz));
        }

        // combler les trous de 1 à 4 secteurs (le reste est l'ouverture du gilet)
        for (int i = 0; i < sectors; i++)
        {
            if (radius[i] >= 0f) continue;
            int prev = i, next = i, gap = 0;
            while (radius[(prev + sectors - 1) % sectors] < 0f && gap < sectors) { prev = (prev + sectors - 1) % sectors; gap++; }
            while (radius[(next + 1) % sectors] < 0f && gap < sectors) { next = (next + 1) % sectors; gap++; }
            int left = (prev + sectors - 1) % sectors, right = (next + 1) % sectors;
            int width = gap + 1;
            if (width > 4 || radius[left] < 0f || radius[right] < 0f) { i = next; continue; }
            for (int k = prev, n = 1; k != (next + 1) % sectors; k = (k + 1) % sectors, n++)
                radius[k] = Mathf.Lerp(radius[left], radius[right], n / (float)(width + 1));
            i = next;
        }

        // lissage (2 passes) : enlève les petits décalages entre secteurs voisins, surtout visibles de dos
        for (int pass = 0; pass < 2; pass++)
        {
            var smoothed = (float[])radius.Clone();
            for (int i = 0; i < sectors; i++)
            {
                float a = radius[(i + sectors - 1) % sectors], b = radius[i], c = radius[(i + 1) % sectors];
                if (a >= 0f && b >= 0f && c >= 0f) smoothed[i] = 0.25f * a + 0.5f * b + 0.25f * c;
            }
            radius = smoothed;
        }

        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        for (int i = 0; i < sectors; i++)
        {
            int j = (i + 1) % sectors;
            if (radius[i] < 0f || radius[j] < 0f) continue;
            float a0 = -Mathf.PI + (i + 0.5f) / sectors * 2f * Mathf.PI;
            float a1 = -Mathf.PI + (j + 0.5f) / sectors * 2f * Mathf.PI;
            if (j == 0) a1 += 2f * Mathf.PI;
            Vector3 p0 = new Vector3(cx + Mathf.Sin(a0) * (radius[i] + offset), 0f, cz + Mathf.Cos(a0) * (radius[i] + offset));
            Vector3 p1 = new Vector3(cx + Mathf.Sin(a1) * (radius[j] + offset), 0f, cz + Mathf.Cos(a1) * (radius[j] + offset));
            int b = vertices.Count;
            vertices.Add(new Vector3(p0.x, y - height * 0.5f, p0.z));
            vertices.Add(new Vector3(p0.x, y + height * 0.5f, p0.z));
            vertices.Add(new Vector3(p1.x, y - height * 0.5f, p1.z));
            vertices.Add(new Vector3(p1.x, y + height * 0.5f, p1.z));
            triangles.AddRange(new[] { b, b + 1, b + 2, b + 2, b + 1, b + 3 });
        }
        if (vertices.Count == 0) return;

        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EnsureFolder(Folder + "/Meshes");
        string meshPath = Folder + "/Meshes/" + name + ".asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath); // un prefab ne garde pas un maillage créé par code : on le sauve en asset

        var band = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        band.GetComponent<MeshFilter>().sharedMesh = mesh;
        band.GetComponent<MeshRenderer>().sharedMaterial = mat;
        band.transform.SetParent(bone, true); // le maillage est déjà en coordonnées monde
    }

    // Tranche horizontale d'un maillage animé (pose actuelle) : centre et largeur/profondeur totales à la hauteur y.
    static void SliceOf(GameObject go, string nameContains, float y, float halfThickness, out Vector3 center, out float width, out float depth)
    {
        center = new Vector3(0f, y, 0f);
        width = depth = 0.3f;
        foreach (SkinnedMeshRenderer r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!r.name.ToLowerInvariant().Contains(nameContains)) continue;

            var baked = new Mesh();
            r.BakeMesh(baked);
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (Vector3 v in baked.vertices)
            {
                Vector3 w = r.transform.TransformPoint(v);
                if (Mathf.Abs(w.y - y) > halfThickness) continue;
                minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.x);
                minZ = Mathf.Min(minZ, w.z); maxZ = Mathf.Max(maxZ, w.z);
            }
            Object.DestroyImmediate(baked);
            if (minX < maxX)
            {
                center = new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f);
                width = maxX - minX;
                depth = maxZ - minZ;
            }
            return;
        }
    }

    // Sommet réel du casque : hauteur max des sommets, centre et profondeur de la calotte près du sommet.
    static void HelmetTop(GameObject go, out float topY, out float topZ, out float depthZ)
    {
        Bounds b = BoundsOf(go, "helmet");
        topY = b.max.y; topZ = b.center.z; depthZ = 0.2f;
        foreach (SkinnedMeshRenderer r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!r.name.ToLowerInvariant().Contains("helmet")) continue;
            var baked = new Mesh();
            r.BakeMesh(baked);
            float maxY = float.MinValue;
            var world = new System.Collections.Generic.List<Vector3>();
            foreach (Vector3 v in baked.vertices) { Vector3 w = r.transform.TransformPoint(v); world.Add(w); maxY = Mathf.Max(maxY, w.y); }
            Object.DestroyImmediate(baked);
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (Vector3 w in world)
            {
                if (w.y < maxY - 0.05f) continue; // la calotte à moins de 5 cm du sommet
                minZ = Mathf.Min(minZ, w.z); maxZ = Mathf.Max(maxZ, w.z);
            }
            topY = maxY;
            if (minZ < maxZ) { topZ = (minZ + maxZ) * 0.5f; depthZ = Mathf.Max(0.12f, (maxZ - minZ) * 1.8f); }
            return;
        }
    }

    public static Bounds BoundsOf(GameObject go, string nameContains)
    {
        foreach (SkinnedMeshRenderer r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (r.name.ToLowerInvariant().Contains(nameContains)) return r.bounds;
        return new Bounds(Vector3.zero, Vector3.one * 0.1f);
    }

    public static Material Mat(string name, Color color, float smoothness, Color? emission = null)
    {
        string path = Folder + "/Materials/" + name + ".mat";
        EnsureFolder(Folder + "/Materials");
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
        mat.SetFloat("_Metallic", 0f);
        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
