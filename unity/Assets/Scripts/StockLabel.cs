using UnityEngine;

// Petite pastille ardoise au-dessus d'une machine : stock d'entrée (caisse) et de sortie (produit), et voyant de la lampe
// (vert = marche, orange = entrée vide, rouge = sortie pleine, qui clignote). Lit n'importe quelle machine qui implémente
// IMachineStatus. La pastille n'apparaît que si le joueur est à moins de `showDistance` ou si la machine est bloquée
// (pastille rouge qui clignote), et jamais sous le HUD du haut de l'écran.
public class StockLabel : MonoBehaviour
{
    [SerializeField] MonoBehaviour machine;   // doit implémenter IMachineStatus
    [SerializeField] GameObject visual;       // pastille et textes (racine, face caméra)
    [SerializeField] SpriteRenderer pill;
    [SerializeField] TextMesh[] inputTexts;   // texte + contour
    [SerializeField] TextMesh[] outputTexts;
    [SerializeField] Renderer lamp;
    [SerializeField] float showDistance = 6f;
    [SerializeField, Range(0.5f, 1f)] float hudLimit = 0.74f; // au-dessus de cette hauteur d'écran (0-1) : caché
    [SerializeField] Color slate = new Color32(0x1E, 0x2A, 0x38, 0xFF);
    [SerializeField] Color running = new Color32(0x3D, 0xDC, 0x5A, 0xFF);
    [SerializeField] Color waiting = new Color32(0xFF, 0x7A, 0x1A, 0xFF);
    [SerializeField] Color blocked = new Color32(0xF2, 0x45, 0x3D, 0xFF);

    IMachineStatus status;
    Transform player;
    MaterialPropertyBlock block;
    string lastInput, lastOutput;
    static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        status = machine as IMachineStatus;
        block = new MaterialPropertyBlock();
        PlayerController p = FindFirstObjectByType<PlayerController>();
        if (p != null) player = p.transform;
    }

    public static string Format(int input, int inputCap, int output, int outputCap)
    {
        return NumberFormat.Short(input) + "/" + NumberFormat.Short(inputCap) + " > " + NumberFormat.Short(output) + "/" + NumberFormat.Short(outputCap);
    }

    static void SetAll(TextMesh[] texts, string s)
    {
        if (texts == null) return;
        foreach (TextMesh t in texts) if (t != null) t.text = s;
    }

    void LateUpdate()
    {
        if (status == null) return;
        MachineState state = status.State;

        string i = NumberFormat.Short(status.InputCount) + "/" + NumberFormat.Short(status.InputCapacity);
        string o = NumberFormat.Short(status.OutputCount) + "/" + NumberFormat.Short(status.OutputCapacity);
        if (i != lastInput) { lastInput = i; SetAll(inputTexts, i); }
        if (o != lastOutput) { lastOutput = o; SetAll(outputTexts, o); }

        bool blink = state == MachineState.Blocked && Mathf.Repeat(Time.time, 0.8f) > 0.4f;
        if (lamp != null)
        {
            Color c = state == MachineState.Running ? running : state == MachineState.WaitingInput ? waiting : blocked;
            if (blink) c *= 0.35f;
            lamp.GetPropertyBlock(block);
            block.SetColor(ColorId, c);
            lamp.SetPropertyBlock(block);
        }

        if (visual == null) return;
        bool near = player != null && (player.position - transform.position).sqrMagnitude < showDistance * showDistance;
        bool show = near || state == MachineState.Blocked;
        if (show)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(visual.transform.position);
                if (vp.z <= 0f || vp.y > hudLimit || vp.x < 0.06f || vp.x > 0.94f) show = false; // jamais sous le HUD ni au bord
            }
        }
        if (visual.activeSelf != show) visual.SetActive(show);
        if (show && pill != null) pill.color = state == MachineState.Blocked ? (blink ? new Color32(0x8A, 0x22, 0x1E, 0xFF) : blocked) : slate;
    }
}
