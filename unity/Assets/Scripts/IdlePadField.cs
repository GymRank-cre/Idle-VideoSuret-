using UnityEngine;

// Toutes les plaques au sol (UL) : les tient à jour 10 fois par seconde et à chaque changement du jeu, désigne celle qui est sous le doigt,
// et fait flotter l'étoile dorée au-dessus de la plaque conseillée (le « Meilleur achat », calculé par IdleGame).
public class IdlePadField : MonoBehaviour
{
    [SerializeField] IdlePadView[] pads;
    [SerializeField] Transform star;
    [SerializeField] float starHeight = 1.35f;     // assez haut pour ne jamais couvrir un chiffre
    [SerializeField] float starBob = 0.09f;
    [SerializeField, Min(0.1f)] float starBobSpeed = 4f;
    [SerializeField, Min(0.02f)] float refreshEvery = 0.1f;

    IdleGame subscribed;
    float timer;
    bool dirty = true;
    IdlePadView advised;

    public IdlePadView Advised => advised;

    void OnDestroy()
    {
        if (subscribed != null) subscribed.OnChanged -= MarkDirty;
    }

    void MarkDirty() { dirty = true; }

    void Update()
    {
        IdleHost host = IdleHost.Instance;
        if (host == null || host.Game == null) return;
        IdleGame game = host.Game;
        if (subscribed == null) { subscribed = game; game.OnChanged += MarkDirty; }

        timer -= Time.unscaledDeltaTime;
        if (timer <= 0f || dirty)
        {
            timer = refreshEvery;
            dirty = false;
            Refresh(game);
        }

        float time = Time.time, dt = Time.deltaTime;
        for (int i = 0; i < pads.Length; i++) pads[i].Animate(time, dt);
        if (advised != null) star.position = advised.Position + Vector3.up * (starHeight + starBob * Mathf.Sin(time * starBobSpeed));
    }

    void Refresh(IdleGame game)
    {
        bool found, hasGoal;
        IdleEconomy.Option goal;
        IdleEconomy.Option best = game.Advice(out found, out hasGoal, out goal);
        advised = null;
        for (int i = 0; i < pads.Length; i++)
        {
            IdlePadView pad = pads[i];
            pad.Refresh(game);
            if (found && pad.IsVisible && IdleGame.IsAdvised(best, pad.Station, pad.Kind)) advised = pad;
        }
        bool show = advised != null;
        if (star.gameObject.activeSelf != show) star.gameObject.SetActive(show);
    }

    // La plaque la plus proche du point touché, dans sa zone élargie ; null si aucune.
    public IdlePadView Pick(Vector3 ground)
    {
        IdlePadView best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < pads.Length; i++)
        {
            if (!pads[i].IsVisible) continue;
            float d = PadDistance(pads[i].Position, ground);
            if (d <= IdleLayout.PadHitSize * 0.5f && d < bestDistance) { bestDistance = d; best = pads[i]; }
        }
        return best;
    }

    // Distance « carrée » : les plaques sont alignées sur les axes de la dalle.
    public static float PadDistance(Vector3 pad, Vector3 ground)
    {
        return Mathf.Max(Mathf.Abs(ground.x - pad.x), Mathf.Abs(ground.z - pad.z));
    }
}
