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

    [Header("Vue d'ensemble (11a-3)")]
    [SerializeField] Transform overviewFocus;   // actif seulement quand la chaîne existe (enfant d'une machine débloquée)
    [SerializeField] float overviewScale = 1.8f; // distance de la caméra x1,8 quand le joueur ne touche plus l'écran
    [SerializeField] float idleDelay = 1.5f;
    [SerializeField, Range(0f, 1f)] float overviewWeight = 0.65f; // part du chemin du joueur vers le centre de l'atelier

    // vue libre (11e) : centre et distance choisis par le joueur, fondu depuis / vers le suivi du technicien
    bool freeActive;
    Vector3 freeCenter;
    float freeMul = 1f;
    float freeK, freeVel;

    Camera cam;
    PlayerController player;
    float idleTime;
    float overviewK, overviewVel;
    float baseFov;
    bool repairLogged;
    Vector3 smoothPos;           // position lissée, sans la secousse
    bool smoothInit;
    float shakeAmp, shakeDur, shakeTime = -1f;
    float zoomAmp, zoomDur, zoomTime = -1f;

    public int RepairCount { get; private set; } // lu par le panneau de diagnostic

    void Awake()
    {
        cam = GetComponent<Camera>();
        baseFov = cam.fieldOfView;
        if (target != null) player = target.GetComponent<PlayerController>();
    }

    // Secousse légère (≤ 0,1 m) : décroît jusqu'à zéro, ne bloque aucune commande.
    public void Shake(float amplitude, float duration)
    {
        amplitude = MotionSettings.ShakeAmplitude(amplitude);   // option « Moins de secousses »
        if (amplitude <= 0f) return;
        shakeAmp = Mathf.Min(amplitude, 0.1f);
        shakeDur = Mathf.Max(0.05f, duration);
        shakeTime = 0f;
    }

    // Pulsation de zoom : le champ se resserre un instant puis revient.
    public void ZoomPulse(float strength, float duration)
    {
        if (!MotionSettings.AllowCameraGlide) return;   // option « Moins de secousses »
        zoomAmp = Mathf.Clamp(strength, 0f, 0.15f);
        zoomDur = Mathf.Max(0.05f, duration);
        zoomTime = 0f;
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
        float zoom = 1f;
        if (zoomTime >= 0f)
        {
            zoomTime += Time.deltaTime;
            if (zoomTime >= zoomDur) zoomTime = -1f;
            else zoom = 1f - zoomAmp * Mathf.Sin(zoomTime / zoomDur * Mathf.PI);
        }
        cam.fieldOfView = Mathf.Clamp(fov, fovLimits.x, fovLimits.y) * zoom;
    }

    Transform peekTarget;
    float peekDuration;
    float peekElapsed;

    // Déplace doucement la caméra vers `look` puis la ramène sur le joueur (les commandes ne sont pas bloquées).
    public void Peek(Transform look, float duration)
    {
        if (!MotionSettings.AllowCameraGlide) return;   // option « Moins de secousses » : la caméra ne glisse plus
        peekTarget = look;
        peekDuration = Mathf.Max(0.1f, duration);
        peekElapsed = 0f;
    }

    public void SetFreeView(bool on, Vector3 center, float distanceMultiplier)
    {
        freeActive = on;
        if (Finite(center)) freeCenter = center;
        if (Finite(distanceMultiplier)) freeMul = Mathf.Clamp(distanceMultiplier, 0.6f, 2f);
    }

    // 0 = cadrage normal, 1 = vue d'ensemble ; revient dès que le joueur bouge ou qu'un Peek est en cours.
    float UpdateOverview()
    {
        if (overviewFocus == null || !overviewFocus.gameObject.activeInHierarchy || !Finite(Time.deltaTime))
        {
            overviewK = 0f; overviewVel = 0f; idleTime = 0f;
            return 0f;
        }
        bool active = (player != null && player.MoveAmount > 0.05f) || peekTarget != null;
        idleTime = active ? 0f : idleTime + Time.deltaTime;
        float want = idleTime >= idleDelay ? 1f : 0f;
        overviewK = Mathf.SmoothDamp(overviewK, want, ref overviewVel, 0.4f);
        if (!Finite(overviewK)) { overviewK = 0f; overviewVel = 0f; Repair("vue d'ensemble"); }
        return Mathf.Clamp01(overviewK);
    }

    void LateUpdate()
    {
        if (target == null || !Finite(target.position)) return;
        if (cam != null) UpdateFov();

        // Position actuelle invalide : le lissage d'un NaN reste NaN pour toujours, on recale d'un coup.
        if (!smoothInit) { smoothPos = transform.position; smoothInit = true; }
        if (!Finite(smoothPos) || !Finite(transform.position))
        {
            smoothPos = target.position + offset;
            transform.position = smoothPos;
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

        freeK = Mathf.SmoothDamp(freeK, freeActive ? 1f : 0f, ref freeVel, 0.25f, Mathf.Infinity, Time.unscaledDeltaTime);
        if (!Finite(freeK)) { freeK = freeActive ? 1f : 0f; freeVel = 0f; Repair("vue libre"); }
        float ov = freeActive || freeK > 0.001f ? 0f : UpdateOverview();   // le recul automatique attend la fin de la vue libre
        if (ov > 0.0001f) focus = Vector3.Lerp(focus, overviewFocus.position, overviewWeight * ov);
        if (freeK > 0.0001f) focus = Vector3.Lerp(focus, freeCenter, freeK);
        Vector3 desired = focus + offset * Mathf.Lerp(Mathf.Lerp(1f, overviewScale, ov), freeMul, freeK);
        if (!Finite(desired)) { peekTarget = null; Repair("position visée"); return; }
        float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        if (!Finite(t)) t = 1f;
        smoothPos = Vector3.Lerp(smoothPos, desired, t);

        Vector3 shake = Vector3.zero;
        if (shakeTime >= 0f)
        {
            shakeTime += Time.deltaTime;
            if (shakeTime >= shakeDur) shakeTime = -1f;
            else shake = Random.insideUnitSphere * (shakeAmp * (1f - shakeTime / shakeDur));
        }
        transform.position = smoothPos + shake;
    }
}
