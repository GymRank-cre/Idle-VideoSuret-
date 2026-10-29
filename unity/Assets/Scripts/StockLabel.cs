using UnityEngine;

// Au-dessus d'une machine : stocks « entrée > sortie » et voyant (vert = marche, orange = entrée vide, rouge = sortie pleine,
// qui clignote). Lit n'importe quelle machine qui implémente IMachineStatus (poste d'installation, ProductionMachine).
public class StockLabel : MonoBehaviour
{
    [SerializeField] MonoBehaviour machine;   // doit implémenter IMachineStatus
    [SerializeField] TextMesh text;
    [SerializeField] TextMesh outline;        // même texte, décalé derrière, en encre
    [SerializeField] Renderer lamp;
    [SerializeField] Color running = new Color32(0x3D, 0xDC, 0x5A, 0xFF);
    [SerializeField] Color waiting = new Color32(0xFF, 0x7A, 0x1A, 0xFF);
    [SerializeField] Color blocked = new Color32(0xF2, 0x45, 0x3D, 0xFF);

    IMachineStatus status;
    MaterialPropertyBlock block;
    string lastText;
    static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        status = machine as IMachineStatus;
        block = new MaterialPropertyBlock();
    }

    public static string Format(int input, int inputCap, int output, int outputCap)
    {
        return NumberFormat.Short(input) + "/" + NumberFormat.Short(inputCap) + " > " + NumberFormat.Short(output) + "/" + NumberFormat.Short(outputCap);
    }

    void LateUpdate()
    {
        if (status == null) return;
        string s = Format(status.InputCount, status.InputCapacity, status.OutputCount, status.OutputCapacity);
        if (s != lastText)
        {
            lastText = s;
            if (text != null) text.text = s;
            if (outline != null) outline.text = s;
        }

        if (lamp == null) return;
        Color c = status.State == MachineState.Running ? running : status.State == MachineState.WaitingInput ? waiting : blocked;
        if (status.State == MachineState.Blocked && Mathf.Repeat(Time.time, 0.8f) > 0.4f) c *= 0.35f; // clignote
        lamp.GetPropertyBlock(block);
        block.SetColor(ColorId, c);
        lamp.SetPropertyBlock(block);
    }
}
