using UnityEngine;
using UnityEngine.Rendering;

// Ce qu'on voit du début de la chaîne et de ses liaisons : les cristaux coupés qui repoussent, les robots au travail, les tas, le drone, les chariots et les stocks. Visuel seulement : tout se lit
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
    [SerializeField, Min(1)] int cubeIndices = 36;     // indices d'un cube de tas
    [SerializeField, Min(0)] int stockBaseIndices;     // la palette, toujours visible, en tête du maillage d'un stock
    [SerializeField] GameObject[] stocks;              // un par poste à partir de l'Assemblage : palette, tas et étiquette
    [SerializeField] MeshFilter[] stockPiles;
    [SerializeField] TextMesh[] stockTexts;
    [SerializeField] GameObject[] stockLabels;
    [SerializeField, Min(0.02f)] float labelEvery = 0.2f;   // le chiffre change sans arrêt : on ne réécrit le texte que de temps en temps
    [SerializeField] Transform[] carts;                // un par poste qui en a un après lui, à partir de l'Assemblage
    [SerializeField] GameObject[] cartLoads;
    [SerializeField] Vector3[] cartFrom;
    [SerializeField] Vector3[] cartTo;
    [SerializeField] FeedbackSpawner feedback;
    [SerializeField] IdleCameraRig rig;
    [SerializeField] float workLean = 16f;
    [SerializeField, Min(0.1f)] float workSpeed = 9f;
    [SerializeField, Min(0.1f)] float appearSpeed = 2.6f;

    Vector3[] baseVertices, vertices, centers;
    float[] growth;
    int perCrystal, nextCrystal;
    float cutPhase, labelTimer;
    Mesh crystalMesh, cutMesh, rawMesh;
    Mesh[] stockMeshes;
    int cutFull, rawFull, cutShown = -1, rawShown = -1;
    int[] stockFull, stockShown, stockNumber, stockTotal;
    readonly float[] appear = { 1f, 1f, 1f };
    Quaternion cutterRest, etcherRest;
    IdleGame subscribed;

    void Awake()
    {
        crystalMesh = crystals.mesh;
        cutMesh = cutPile.mesh;
        rawMesh = rawPile.mesh;
        cutFull = (int)cutMesh.GetIndexCount(0);
        rawFull = (int)rawMesh.GetIndexCount(0);
        int n = stocks.Length;
        stockMeshes = new Mesh[n];
        stockFull = new int[n];
        stockShown = new int[n];
        stockNumber = new int[n];
        stockTotal = new int[n];
        for (int i = 0; i < n; i++)
        {
            stockMeshes[i] = stockPiles[i].mesh;
            stockFull[i] = (int)stockMeshes[i].GetIndexCount(0);
            stockShown[i] = stockNumber[i] = stockTotal[i] = -1;
        }

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
        if (rig != null && subscribed.CrewReady) rig.FrameDefault(MotionSettings.ReduceMotion);   // la chaîne démarre : la caméra montre la rangée de machines
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
        UpdateCrystals(dt, chain.Working[0], game.VisualFrequency(IdleEconomy.SerreStation));
        UpdateRobot(cutter, cutterRest, game.Crew, 0, chain.Working[0], time, dt);
        UpdateRobot(etcher, etcherRest, game.Crew, 1, chain.Etching, time, dt);
        UpdateDrone(game.Crew, chain, time, dt);

        double buffer = chain.BufferSize(game.Rate(IdleEconomy.SerreStation));
        ShowPile(cutMesh, cutFull, 0, chain.Cut / buffer, ref cutShown);
        ShowPile(rawMesh, rawFull, 0, chain.Raw / buffer, ref rawShown);

        labelTimer -= Time.unscaledDeltaTime;
        bool labels = labelTimer <= 0f;
        if (labels) labelTimer = labelEvery;
        for (int i = 0; i < stocks.Length; i++) UpdateStock(game, i, labels);
        for (int i = 0; i < carts.Length; i++) UpdateCart(game, i);
    }

    void UpdateStock(IdleGame game, int index, bool labels)
    {
        int station = index + 1;
        bool open = game.Stations[station].unlocked;
        if (stocks[index].activeSelf != open) stocks[index].SetActive(open);
        if (!open) return;
        double amount = game.Chain.Stock[station], size = game.Chain.StockSize(game.Rate(station));
        ShowPile(stockMeshes[index], stockFull[index], stockBaseIndices, amount / size, ref stockShown[index]);
        bool near = !IdleCameraRig.Overview;
        if (stockLabels[index].activeSelf != near) stockLabels[index].SetActive(near);
        if (!labels || !near) return;
        int number = (int)amount, total = (int)size;
        if (number == stockNumber[index] && total == stockTotal[index]) return;
        stockNumber[index] = number; stockTotal[index] = total;
        stockTexts[index].text = Loc.T("chain.stock", NumberFormat.Short(number), NumberFormat.Short(total));
    }

    void UpdateCart(IdleGame game, int index)
    {
        int station = index + 1;
        bool on = station + 1 < game.Stations.Count && game.Stations[station + 1].unlocked;
        if (carts[index].gameObject.activeSelf != on) carts[index].gameObject.SetActive(on);
        if (!on) return;
        IdleChain.Carrier carrier = game.Chain.Carriers[station];
        float phase = (float)carrier.Phase;
        float way = Mathf.SmoothStep(0f, 1f, phase <= 0.5f ? phase * 2f : (1f - phase) * 2f);
        carts[index].position = Vector3.Lerp(cartFrom[index], cartTo[index], way);
        bool loaded = carrier.Load > 0;
        if (cartLoads[index].activeSelf != loaded) cartLoads[index].SetActive(loaded);
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
        IdleChain.Carrier carrier = chain.Carriers[0];
        float phase = (float)carrier.Phase;
        float way = Mathf.SmoothStep(0f, 1f, phase <= 0.5f ? phase * 2f : (1f - phase) * 2f);
        float bob = MotionSettings.ReduceMotion ? 0f : droneBob * Mathf.Sin(time * 5f);
        drone.position = Vector3.Lerp(droneFrom, droneTo, way) + Vector3.up * (droneArc * Mathf.Sin(way * Mathf.PI) + bob);
        bool loaded = carrier.Load > 0;
        if (droneLoad.activeSelf != loaded) droneLoad.SetActive(loaded);
    }

    void Grow(Transform robot, int role, float dt)
    {
        if (appear[role] >= 1f) return;
        appear[role] = Mathf.Min(1f, appear[role] + dt * appearSpeed);
        robot.localScale = Vector3.one * IdleStationView.EaseOutBack(appear[role]);
    }

    // Un tas montre une part de ses cubes selon son remplissage.
    void ShowPile(Mesh mesh, int fullIndices, int baseIndices, double fill, ref int shown)
    {
        int cubes = (fullIndices - baseIndices) / cubeIndices;
        int count = Mathf.Clamp(Mathf.CeilToInt((float)fill * cubes), 0, cubes);
        if (count == shown) return;
        shown = count;
        mesh.SetSubMesh(0, new SubMeshDescriptor(0, baseIndices + count * cubeIndices), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
    }
}
