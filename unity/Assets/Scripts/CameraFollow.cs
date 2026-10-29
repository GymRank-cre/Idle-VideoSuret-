using UnityEngine;

// Caméra fixe en hauteur qui suit le joueur en douceur.
// Auto-réparation : une valeur invalide (NaN / infinie) ne reste jamais, sinon la scène 3D disparaît et seule l'interface reste.
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
    bool repairLogged;

    public int RepairCount { get; private set; } // lu par le panneau de diagnostic

    void Awake()
    {
        cam = GetComponent<Camera>();
        baseFov = cam.fieldOfView;
    }

    static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);

    void Repair(string what)
    {
        RepairCount++;
        if (repairLogged) return; // une seule trace dans le journal
        repairLogged = true;
        Debug.LogWarning("CameraFollow : " + what + " invalide, caméra recalée.");
    }

    // Même largeur de vue quelle que soit la forme de l'écran (étroit ou barres de Safari) : seul le champ vertical change.
    void UpdateFov()
    {
        if (!Finite(cam.fieldOfView))
        {
            cam.fieldOfView = baseFov;
            Repair("champ de vision");
        }

        float aspect = cam.aspect;
        if (!(aspect > 0.01f) || !Finite(aspect)) return; // aspect NaN, nul ou énorme : on garde le dernier champ valide

        float tanHalf = Mathf.Tan(baseFov * 0.5f * Mathf.Deg2Rad) * referenceAspect / aspect;
        float fov = 2f * Mathf.Atan(tanHalf) * Mathf.Rad2Deg;
        if (!Finite(fov)) { cam.fieldOfView = baseFov; Repair("champ de vision calculé"); return; }
        cam.fieldOfView = Mathf.Clamp(fov, fovLimits.x, fovLimits.y);
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
        if (target == null || !Finite(target.position)) return;
        if (cam != null) UpdateFov();

        // Position actuelle invalide : le lissage d'un NaN reste NaN pour toujours, on recale d'un coup.
        if (!Finite(transform.position))
        {
            transform.position = target.position + offset;
            peekTarget = null;
            Repair("position de la caméra");
        }

        Vector3 focus = target.position;
        if (peekTarget != null)
        {
            if (!Finite(peekTarget.position) || !Finite(Time.deltaTime))
            {
                peekTarget = null;
                Repair("cible du Peek");
            }
            else
            {
                peekElapsed += Time.deltaTime;
                float k = Mathf.Clamp01(peekElapsed / peekDuration);
                // aller rapide, court maintien, retour ; sin(pi) peut valoir -1e-7 (WebGL) : racine d'un négatif = NaN
                float bump = Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(k * Mathf.PI)));
                focus = Vector3.Lerp(target.position, peekTarget.position, bump);
                if (k >= 1f) peekTarget = null;
            }
        }

        Vector3 desired = focus + offset;
        if (!Finite(desired)) { peekTarget = null; Repair("position visée"); return; }
        float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        if (!Finite(t)) t = 1f;
        transform.position = Vector3.Lerp(transform.position, desired, t);
    }
}
