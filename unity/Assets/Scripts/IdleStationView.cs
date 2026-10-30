using UnityEngine;

// Une machine qui sait s'animer selon la phase de son cycle (0 -> 1) ; implémentée par chaque machine de la scène idle (U2c).
// Une machine qui change avec les achats : son aspect (paliers) et sa charge (« Ajouter »).
public interface IMachineAspect
{
    void SetAspect(int aspect);
}

public interface IMachineLoad
{
    void SetLoad(int count);
}

public interface IMachineAnim
{
    void Animate(float phase, float time);
}

// Un poste de la scène idle (U2c) : tant qu'il est verrouillé on voit son fantôme (cadenas jaune pour le prochain poste, gris pour les
// suivants) ; une fois ouvert la machine apparaît et s'anime selon la phase de son cycle. Visuel seulement.
public class IdleStationView : MonoBehaviour
{
    [SerializeField] int station;
    [SerializeField] GameObject ghost;
    [SerializeField] Renderer ghostBody;
    [SerializeField] Material ghostNext;
    [SerializeField] Material ghostFar;
    [SerializeField] SpriteRenderer lockIcon;
    [SerializeField] GameObject machine;
    [SerializeField] Color lockNextColor = new Color32(0xFF, 0xC4, 0x00, 0xFF);
    [SerializeField] Color lockFarColor = new Color32(0xB8, 0xC2, 0xCC, 0xFF);
    [SerializeField, Min(0.1f)] float appearSpeed = 2.2f;
    [SerializeField] FeedbackSpawner feedback;      // textes « +x » et confettis (pools)
    [SerializeField] IdleStationFx fx;              // particules propres au poste
    [SerializeField, Range(0f, 1f)] float fxPhase;  // moment du cycle où elles partent
    [SerializeField, Min(0.5f)] float lotEvery = 4f;   // un lot de billets s'envole au plus toutes ces secondes
    [SerializeField, Min(1)] int lotSize = 6;
    [SerializeField] float floatHeight = 2.7f;      // hauteur de départ du « +x » au-dessus du poste
    [SerializeField, Min(0.1f)] float squashDecay = 2.4f;   // le « squash » d'un achat s'éteint en ~0,4 s
    [SerializeField] float squashAmount = 0.12f;

    IMachineAnim anim;
    IMachineLoad load;
    bool known, wasOpen, wasNext;
    float phase, appear = 1f, bump, sinceLot;
    IdleGame subscribed;

    public int Station => station;
    public float Phase => phase;
    public float Appear => appear;

    void Awake()
    {
        anim = machine != null ? machine.GetComponent<IMachineAnim>() : null;
        load = machine != null ? machine.GetComponent<IMachineLoad>() : null;
    }

    void Subscribe(IdleGame game)
    {
        if (subscribed != null) return;
        subscribed = game;
        game.OnPurchased += OnBought;
        game.OnMilestone += OnMilestone;
        game.OnUnlocked += OnUnlocked;
    }

    void OnDestroy()
    {
        if (subscribed == null) return;
        subscribed.OnPurchased -= OnBought;
        subscribed.OnMilestone -= OnMilestone;
        subscribed.OnUnlocked -= OnUnlocked;
    }

    void OnBought(int s) { if (s == station && !MotionSettings.ReduceMotion) bump = 1f; }                 // petit « squash » du poste acheté
    void OnMilestone(int s) { if (s == station && feedback != null && !MotionSettings.ReduceMotion) feedback.Confetti(transform.position); }   // palier x2
    void OnUnlocked(int s) { if (s == station && feedback != null && !MotionSettings.ReduceMotion) feedback.Confetti(transform.position); }    // poste ouvert

    void Update()
    {
        IdleHost host = IdleHost.Instance;
        if (host == null || host.Game == null) return;
        IdleGame game = host.Game;
        Subscribe(game);
        bool open = game.Stations[station].unlocked;
        bool next = !open && game.PlateStateOf(station) == IdleGame.PlateState.Unlock;

        if (!known || open != wasOpen)
        {
            if (known && open) appear = 0.001f;       // un poste qui vient d'être acheté « pousse » sous les yeux du joueur
            known = true; wasOpen = open;
            if (ghost != null) ghost.SetActive(!open);
            if (machine != null) machine.SetActive(open);
            wasNext = !next;                           // force la mise à jour du fantôme
        }
        if (!open)
        {
            if (next != wasNext)
            {
                wasNext = next;
                if (ghostBody != null) ghostBody.sharedMaterial = next ? ghostNext : ghostFar;
                if (lockIcon != null) { lockIcon.color = next ? lockNextColor : lockFarColor; lockIcon.transform.localScale = Vector3.one * (next ? 0.65f : 0.5f); }
            }
            return;
        }

        float dt = Time.deltaTime;
        float before = phase;
        phase += dt * game.VisualFrequency(station);
        if (feedback != null && Mathf.FloorToInt(phase) != Mathf.FloorToInt(before) && appear >= 1f)   // un cycle visuel vient de finir : « +x » qui monte
            feedback.StationGain(station, transform.position + Vector3.up * floatHeight, "+" + NumberFormat.Gain(game.CycleGain(station)));
        sinceLot += dt;
        if (appear >= 1f && !MotionSettings.ReduceMotion)
        {
            if (fx != null && Mathf.FloorToInt(phase - fxPhase) != Mathf.FloorToInt(before - fxPhase)) fx.Cycle(station, transform.position);
            if (sinceLot >= lotEvery && Mathf.FloorToInt(phase) != Mathf.FloorToInt(before) && IdleBills.Instance != null)
            {
                sinceLot = 0f;
                IdleBills.Instance.Send(transform.position + Vector3.up * 0.8f, lotSize);
            }
        }
        if (bump > 0f) bump = Mathf.Max(0f, bump - dt * squashDecay);
        if (appear < 1f) appear = Mathf.Min(1f, appear + dt * appearSpeed);
        float scale = appear < 1f ? EaseOutBack(appear) : 1f;
        float squash = bump * squashAmount;   // écrasé en hauteur, élargi sur les côtés, puis retour en ressort
        transform.localScale = new Vector3(scale * (1f + squash * 0.6f), scale * (1f - squash), scale * (1f + squash * 0.6f));   // le poste (centré) pousse ; son fantôme est caché à ce moment-là
        if (load != null) load.SetLoad(game.StackSize(station));
        if (anim != null) anim.Animate(phase - Mathf.Floor(phase), Time.time);
    }

    public static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        float c = 1.6f, u = t - 1f;
        return 1f + (c + 1f) * u * u * u + c * u * u;
    }
}
