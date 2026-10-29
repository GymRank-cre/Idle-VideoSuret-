using UnityEngine;

// Caméra fixe en hauteur qui suit le joueur en douceur.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Vector3 offset = new Vector3(0f, 12f, -8f);
    [SerializeField] float smooth = 8f;

    Transform peekTarget;
    float peekDuration;
    float peekElapsed;

    // Déplace doucement la caméra vers `look` puis la ramène sur le joueur (les commandes ne sont pas bloquées).
    public void Peek(Transform look, float duration)
    {
        peekTarget = look;
        peekDuration = Mathf.Max(0.1f, duration);
        peekElapsed = 0f;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 focus = target.position;
        if (peekTarget != null)
        {
            peekElapsed += Time.deltaTime;
            float k = Mathf.Clamp01(peekElapsed / peekDuration);
            float bump = Mathf.Sqrt(Mathf.Sin(k * Mathf.PI)); // aller rapide, court maintien, retour
            focus = Vector3.Lerp(target.position, peekTarget.position, bump);
            if (k >= 1f) peekTarget = null;
        }
        Vector3 desired = focus + offset;
        float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, t);
    }
}
