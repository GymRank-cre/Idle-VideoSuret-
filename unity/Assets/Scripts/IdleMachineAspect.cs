using UnityEngine;

// Aspect d'une machine (UW-3) : à chaque seuil de niveau, le corps de la machine change de maillage (couleur, bandes, modules). Ça ne coûte aucun
// appel de dessin de plus. Le passage à un aspect supérieur se voit et s'entend : éclair blanc et fanfare ; au chargement, il est appliqué sans effet.
public class IdleMachineAspect : MonoBehaviour
{
    [SerializeField] int station;
    [SerializeField] MeshFilter body;
    [SerializeField] Renderer bodyRenderer;
    [SerializeField] Mesh[] aspects;
    [SerializeField, Min(0.1f)] float flashDecay = 2.2f;
    [SerializeField, Range(0f, 1f)] float flashStrength = 0.75f;

    static readonly int FlashId = Shader.PropertyToID("_Flash");

    int shown = -1;
    float flash;
    MaterialPropertyBlock block;
    IMachineAspect module;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        module = GetComponent<IMachineAspect>();
    }

    void Update()
    {
        IdleHost host = IdleHost.Instance;
        if (host == null || host.Game == null) return;
        IdleGame game = host.Game;
        IdleEconomy.StationLevels st = game.Stations[station];
        int aspect = Mathf.Min(game.Eco.AspectOf(st.speed + st.add), aspects.Length - 1);
        if (aspect != shown)
        {
            bool levelUp = shown >= 0 && aspect > shown;
            Apply(aspect);
            if (levelUp) LevelUp();
        }
        if (flash > 0f)
        {
            flash = Mathf.Max(0f, flash - Time.deltaTime * flashDecay);
            if (flash > 0f) { block.SetFloat(FlashId, flash * flashStrength); bodyRenderer.SetPropertyBlock(block); }
            else bodyRenderer.SetPropertyBlock(null);   // redevient un objet ordinaire pour le regroupement du rendu
        }
    }

    // Montre un aspect tout de suite (aussi utilisé pour la capture des aspects côte à côte).
    public void Apply(int aspect)
    {
        shown = aspect;
        body.sharedMesh = aspects[aspect];
        if (module != null) module.SetAspect(aspect);
    }

    void LevelUp()
    {
        AudioManager.Play(SfxId.LevelUp);
        if (!MotionSettings.ReduceMotion) flash = 1f;
    }
}
