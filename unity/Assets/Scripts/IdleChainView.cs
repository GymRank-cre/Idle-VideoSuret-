using UnityEngine;
using UnityEngine.Rendering;

// Ce qu'on voit du début de la chaîne : les cristaux coupés qui repoussent, les robots au travail, les tas, le drone et le stock. Visuel seulement : tout se lit
// dans IdleGame et IdleChain. Cristaux et tas tiennent chacun dans un maillage, donc un seul appel de dessin : on en écrit les sommets ou on en coupe la liste de triangles.
public class IdleChainView : MonoBehaviour
{
    [SerializeField] MeshFilter crystals;
    [SerializeField, Min(1)] int crystalCount = 9;
    [SerializeField, Min(0.1f)] float regrowSeconds = 2.2f;
    [SerializeField] Transform cutter;
    [SerializeField] Transform etcher;
    [SerializeField] Transform drone;
    [SerializeField] GameObject droneLoad;
    [SerializeField] Vector3 droneFrom;
    [SerializeField] Vector3 droneTo;
    [SerializeField] float droneArc = 0.45f;
    [SerializeField] float droneBob = 0.03f;
    [SerializeField] MeshFilter cutPile;
    [SerializeField] MeshFilter rawPile;
    [SerializeField] MeshFilter stockPile;
    [SerializeField, Min(1)] int cubeIndices = 36;     // indices d'un cube de tas
    [SerializeField, Min(0)] int stockBaseIndices;     // la palette, toujours visible, en tête du maillage du stock
    [SerializeField] TextMesh stockText;
    [SerializeField, Min(0.02f)] float labelEvery = 0.2f;   // le chiffre change sans arrêt : on ne réécrit le texte que de temps en temps
    [SerializeField] FeedbackSpawner feedback;
    [SerializeField] IdleCameraRig rig;
    [SerializeField] float workLean = 16f;
    [SerializeField, Min(0.1f)] float workSpeed = 9f;
    [SerializeField, Min(0.1f)] float appearSpeed = 2.6f;

    Vector3[] baseVertices, vertices, centers;
    float[] growth;
    int perCrystal, nextCrystal;
    float cutPhase, labelTimer;
    Mesh crystalMesh, cutMesh, rawMesh, stockMesh;
    int cutShown = -1, rawShown = -1, stockShown = -1, stockNumber = -1, stockSize = -1;
    readonly float[] appear = { 1f, 1f, 1f };
    readonly int[] fullCount = new int[3];      // indices de chaque tas plein
    Quaternion cutterRest, etcherRest;
    IdleGame subscribed;

    void Awake()
    {
        crystalMesh = crystals.mesh;
        cutMesh = cutPile.mesh;
        rawMesh = rawPile.mesh;
        stockMesh = stockPile.mesh;
        fullCount[0] = (int)cutMesh.GetIndexCount(0);
        fullCount[1] = (int)rawMesh.GetIndexCount(0);
        fullCount[2] = (int)stockMesh.GetIndexCount(0);
        baseVertices = crystalMesh.vertices;
        vertices = (Vector3[])baseVertices.Clone();
        perCrystal = crystalMesh.vertexCount / crystalCount;
        growth = new float[crystalCount];
        centers = new Vector3[crystalCount];
        for (int k = 0; k < crystalCount; k++)
        {
            growth[k] = 1f;
            Vector3 sum = Vector3.zero;
            float bottom = float.MaxValue;
            for (int v = k * perCrystal; v < (k + 1) * perCrystal; v++) { sum += baseVertices[v]; bottom = Mathf.Min(bottom, baseVertices[v].y); }
            sum /= perCrystal;
            centers[k] = new Vector3(sum.x, bottom, sum.z);
        }
        cutterRest = cutter.localRotation;
        etcherRest = etcher.localRotation;
    }

    void OnDestroy()
    {
        if (subscribed != null) subscribed.OnHired -= Hired;
    }

    void Hired(int role)
    {
        if (rig != null && subscribed.CrewReady) rig.FrameDefault(MotionSettings.ReduceMotion);   // la chaîne démarre : la caméra recule et montre l'Assemblage
        if (MotionSettings.ReduceMotion) return;
        appear[role] = 0.001f;
        Transform robot = role == 0 ? cutter : role == 1 ? etcher : drone;
        if (feedback != null) feedback.Confetti(robot.position);
    }

    void Update()
    {
        IdleHost host = IdleHost.Instance;
        if (host == null || host.Game == null) return;
        IdleGame game = host.Game;
        if (subscribed == null) { subscribed = game; game.OnHired += Hired; }

        IdleChain chain = game.Chain;
        float dt = Time.deltaTime, time = Time.time;
        UpdateCrystals(dt, chain.Cutting, game.VisualFrequency(IdleEconomy.SerreStation));
        UpdateRobot(cutter, cutterRest, game.Crew, 0, chain.Cutting, time, dt);
        UpdateRobot(etcher, etcherRest, game.Crew, 1, chain.Etching, time, dt);
        UpdateDrone(game.Crew, chain, time, dt);

        double rateIn = game.Eco.ItemsPerSecond(IdleEconomy.SerreStation, game.Stations[IdleEconomy.SerreStation], game.Globals);
        double rateOut = game.Eco.ItemsPerSecond(IdleEconomy.AssemblyStation, game.Stations[IdleEconomy.AssemblyStation], game.Globals);
        double buffer = chain.BufferSize(rateIn), size = chain.StockSize(rateIn, rateOut);
        ShowPile(cutMesh, 0, chain.Cut / buffer, ref cutShown);
        ShowPile(rawMesh, 0, chain.Raw / buffer, ref rawShown);
        ShowPile(stockMesh, stockBaseIndices, chain.Stock / size, ref stockShown);

        labelTimer -= Time.unscaledDeltaTime;
        if (labelTimer > 0f) return;
        labelTimer = labelEvery;
        int number = (int)chain.Stock, total = (int)size;
        if (number == stockNumber && total == stockSize) return;
        stockNumber = number; stockSize = total;
        stockText.text = Loc.T("chain.stock", NumberFormat.Short(number), NumberFormat.Short(total));
    }

    void UpdateCrystals(float dt, bool cutting, float frequency)
    {
        if (cutting)
        {
            cutPhase += dt * frequency;
            if (cutPhase >= 1f)
            {
                cutPhase -= 1f;
                growth[nextCrystal] = 0f;
                nextCrystal = (nextCrystal + 4) % crystalCount;   // pas dans l'ordre des rangées
            }
        }
        bool changed = false;
        for (int k = 0; k < crystalCount; k++)
        {
            if (growth[k] >= 1f) continue;
            growth[k] = Mathf.Min(1f, growth[k] + dt / regrowSeconds);
            changed = true;
            Vector3 c = centers[k];
            float s = growth[k];
            for (int v = k * perCrystal; v < (k + 1) * perCrystal; v++)
            {
                Vector3 b = baseVertices[v];
                vertices[v] = new Vector3(c.x + (b.x - c.x) * s, c.y + (b.y - c.y) * s, c.z + (b.z - c.z) * s);
            }
        }
        if (changed) crystalMesh.SetVertices(vertices);
    }

    void UpdateRobot(Transform robot, Quaternion rest, int crew, int role, bool working, float time, float dt)
    {
        bool hired = crew > role;
        if (robot.gameObject.activeSelf != hired) robot.gameObject.SetActive(hired);
        if (!hired) return;
        Grow(robot, role, dt);
        float lean = working && !MotionSettings.ReduceMotion ? workLean * Mathf.Sin(time * workSpeed) : 0f;
        robot.localRotation = rest * Quaternion.Euler(lean, 0f, 0f);
    }

    void UpdateDrone(int crew, IdleChain chain, float time, float dt)
    {
        bool hired = crew > 2;
        if (drone.gameObject.activeSelf != hired) drone.gameObject.SetActive(hired);
        if (!hired) return;
        Grow(drone, 2, dt);
        float phase = (float)chain.DronePhase;
        float way = Mathf.SmoothStep(0f, 1f, phase <= 0.5f ? phase * 2f : (1f - phase) * 2f);
        float bob = MotionSettings.ReduceMotion ? 0f : droneBob * Mathf.Sin(time * 5f);
        drone.position = Vector3.Lerp(droneFrom, droneTo, way) + Vector3.up * (droneArc * Mathf.Sin(way * Mathf.PI) + bob);
        bool loaded = chain.Load > 0;
        if (droneLoad.activeSelf != loaded) droneLoad.SetActive(loaded);
    }

    void Grow(Transform robot, int role, float dt)
    {
        if (appear[role] >= 1f) return;
        appear[role] = Mathf.Min(1f, appear[role] + dt * appearSpeed);
        robot.localScale = Vector3.one * IdleStationView.EaseOutBack(appear[role]);
    }

    // Un tas montre une part de ses cubes selon son remplissage.
    void ShowPile(Mesh mesh, int baseIndices, double fill, ref int shown)
    {
        int cubes = (fullCount[mesh == cutMesh ? 0 : mesh == rawMesh ? 1 : 2] - baseIndices) / cubeIndices;
        int count = Mathf.Clamp(Mathf.CeilToInt((float)fill * cubes), 0, cubes);
        if (count == shown) return;
        shown = count;
        mesh.SetSubMesh(0, new SubMeshDescriptor(0, baseIndices + count * cubeIndices), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
    }
}
