using UnityEngine;

// Une machine qui travaille se voit : lampe qui pulse, léger tremblement du corps (±1 cm à ~12 Hz), étincelles toutes
// les secondes. À l'arrêt (sortie pleine) ou en attente, plus rien ne bouge.
public class MachineFx : MonoBehaviour
{
    [SerializeField] MonoBehaviour machine;     // doit implémenter IMachineStatus
    [SerializeField] Transform body;            // la partie qui vibre
    [SerializeField] Transform lamp;            // la lampe qui pulse
    [SerializeField] Transform sparkPoint;      // d'où partent les étincelles
    [SerializeField] FeedbackSpawner feedback;  // facultatif
    [SerializeField] float shakeAmount = 0.01f;
    [SerializeField] float shakeHz = 12f;
    [SerializeField] float sparkInterval = 1f;

    IMachineStatus status;
    Vector3 bodyRest;
    Vector3 lampRest;
    float sparkTimer;

    void Awake()
    {
        status = machine as IMachineStatus;
        if (body != null) bodyRest = body.localPosition;
        if (lamp != null) lampRest = lamp.localScale;
    }

    void Update()
    {
        bool working = status != null && status.State == MachineState.Running && IsCycling();
        if (body != null)
        {
            Vector3 p = bodyRest;
            if (working) p.x += Mathf.Sin(Time.time * shakeHz * Mathf.PI * 2f) * shakeAmount;
            body.localPosition = p;
        }
        if (lamp != null)
            lamp.localScale = working ? lampRest * (1f + 0.18f * Mathf.Sin(Time.time * 9f)) : lampRest;

        if (!working) { sparkTimer = 0f; return; }
        sparkTimer += Time.deltaTime;
        if (sparkTimer >= sparkInterval && feedback != null)
        {
            sparkTimer = 0f;
            feedback.Sparkle((sparkPoint != null ? sparkPoint : transform).position);
        }
    }

    // « Running » inclut « prêt à démarrer » : on ne bouge que si un cycle est réellement en cours.
    bool IsCycling()
    {
        var pm = machine as ProductionMachine;
        if (pm != null) return pm.Progress > 0f;
        return true; // le poste d'installation ne passe Running que pendant un travail ou juste avant
    }
}
