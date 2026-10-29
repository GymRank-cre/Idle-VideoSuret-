using UnityEngine;

// Caméra fixe en hauteur qui suit le joueur en douceur.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Vector3 offset = new Vector3(0f, 12f, -8f);
    [SerializeField] float smooth = 13f; // 9b : 8 laissait glisser le technicien de ~50 px à l'écran à chaque changement de direction
    [Header("Cadrage selon la forme de l'écran")]
    [SerializeField] float referenceAspect = 0.5625f;  // largeur/hauteur de référence (portrait 9:16) : largeur de vue conservée
    [SerializeField] Vector2 fovLimits = new Vector2(40f, 85f);

    Camera cam;
    float baseFov;

    void Awake()
    {
        cam = GetComponent<Camera>();
        baseFov = cam.fieldOfView;
    }

    // Même largeur de vue quelle que soit la forme de l'écran (étroit ou barres de Safari) : seul le champ vertical change.
    void UpdateFov()
    {
        float aspect = cam.aspect;
        if (aspect <= 0.01f) return;
        float tanHalf = Mathf.Tan(baseFov * 0.5f * Mathf.Deg2Rad) * referenceAspect / aspect;
        cam.fieldOfView = Mathf.Clamp(2f * Mathf.Atan(tanHalf) * Mathf.Rad2Deg, fovLimits.x, fovLimits.y);
    }

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
        if (cam != null) UpdateFov();

        Vector3 focus = target.position;
        if (peekTarget != null)
        {
            peekElapsed += Time.deltaTime;
            float k = Mathf.Clamp01(peekElapsed / peekDuration);
            float bump = Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(k * Mathf.PI))); // sin(pi) peut valoir -1e-7 (WebGL) : racine d'un négatif = NaN = écran gris // aller rapide, court maintien, retour
            focus = Vector3.Lerp(target.position, peekTarget.position, bump);
            if (k >= 1f) peekTarget = null;
        }
        Vector3 desired = focus + offset;
        if (float.IsNaN(desired.x) || float.IsNaN(desired.y) || float.IsNaN(desired.z)) { peekTarget = null; return; } // garde-fou : la caméra ne devient jamais invalide
        float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, t);
    }
}
