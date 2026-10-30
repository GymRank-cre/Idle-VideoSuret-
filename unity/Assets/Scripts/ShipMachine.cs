using UnityEngine;

// Expédition (poste 5) : un quai (mur gris-bleu et porte sombre) devant lequel une camionnette blanche attend, puis part vers l'avant
// (72 % -> 88 % du cycle, accélération) pendant qu'une autre arrive de l'arrière (88 % -> 100 %, freinage). Même séquence que le web.
public class ShipMachine : MonoBehaviour, IMachineAnim
{
    [SerializeField] Transform van;
    [SerializeField] float leaveDistance = 2.6f;    // distance parcourue en partant
    [SerializeField] float arriveDistance = 2.2f;   // distance de départ de la camionnette qui arrive

    float baseX;
    bool ready;

    // Décalage de la camionnette (mètres, vers l'avant) selon la phase du cycle : 0 à l'arrêt.
    public static float VanOffset(float phase, float leave, float arrive)
    {
        if (phase > 0.72f && phase < 0.88f) { float t = (phase - 0.72f) / 0.16f; return t * t * leave; }
        if (phase >= 0.88f) { float t = (phase - 0.88f) / 0.12f; float e = 1f - (1f - t) * (1f - t); return (1f - e) * -arrive; }
        return 0f;
    }

    public void Animate(float phase, float time)
    {
        if (!ready) { baseX = van.localPosition.x; ready = true; }
        Vector3 p = van.localPosition;
        p.x = baseX + VanOffset(phase, leaveDistance, arriveDistance);
        van.localPosition = p;
    }
}
