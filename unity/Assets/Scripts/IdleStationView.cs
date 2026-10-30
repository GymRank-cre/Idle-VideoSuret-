using UnityEngine;

// Une machine qui sait s'animer selon la phase de son cycle (0 -> 1) ; implémentée par chaque machine de la scène idle (U2c).
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

    IMachineAnim anim;
    bool known, wasOpen, wasNext;
    float phase, appear = 1f;

    public int Station => station;
    public float Phase => phase;
    public float Appear => appear;

    void Awake()
    {
        anim = machine != null ? machine.GetComponent<IMachineAnim>() : null;
    }

    void Update()
    {
        IdleHost host = IdleHost.Instance;
        if (host == null || host.Game == null) return;
        IdleGame game = host.Game;
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
        phase += dt * game.VisualFrequency(station);
        if (appear < 1f) appear = Mathf.Min(1f, appear + dt * appearSpeed);
        float scale = appear < 1f ? EaseOutBack(appear) : 1f;
        transform.localScale = new Vector3(scale, scale, scale);   // le poste (centré) pousse ; son fantôme est caché à ce moment-là
        if (anim != null) anim.Animate(phase - Mathf.Floor(phase), Time.time);
    }

    public static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        float c = 1.6f, u = t - 1f;
        return 1f + (c + 1f) * u * u * u + c * u * u;
    }
}
