using UnityEngine;

// Caméra libre du jeu idle (UL) : vue isométrique en perspective (inclinaison ~55 degrés, vue en diagonale), qui se déplace au-dessus de la dalle. Elle vise un
// point du sol (IdleFraming) avec un zoom de 1 (toute la dalle) à 3 ; glisser la déplace, avec une légère inertie, et elle rejoint sa cible en douceur.
// Repère de la scène : le plan (x, y) du web devient (Z, X) dans Unity (X = y du web, Z = x du web) ; le dessus de la dalle est à y = 0.
public class IdleCameraRig : MonoBehaviour
{
    enum Frame { Default, All, Free }

    [SerializeField] Vector3 slabMin = new Vector3(0f, -0.3f, 0f);      // le dessus de la dalle est à y = 0
    [SerializeField] Vector3 slabSize = new Vector3(7.6f, 0.3f, 7.8f);   // X = web y (7,6), Y = épaisseur, Z = web x (7,8)
    [SerializeField, Range(10f, 60f)] float fieldOfView = 30f;
    [SerializeField, Range(30f, 80f)] float pitch = 55f;
    [SerializeField] float yaw = 225f;                                   // regarde vers -X, -Z : le coin (0,0) est au fond
    [SerializeField, Range(0.5f, 1f)] float fill = 0.96f;                // au zoom 1, part de la largeur occupée par la dalle
    [SerializeField, Range(0.3f, 0.9f)] float centerHeight = 0.56f;      // hauteur (viewport) du point visé : 44 % depuis le haut, comme le web
    [SerializeField, Range(0f, 0.4f)] float rightColumn = 0.18f;         // part de la largeur occupée par les boutons de droite
    [SerializeField, Min(0.1f)] float smoothing = 6f;
    [SerializeField, Min(0.1f)] float inertiaDecay = 8f;

    Camera cam;
    Frame frame = Frame.Default;
    float lastAspect, fitDistance = 20f;
    Vector2 focus, targetFocus, velocity;
    float zoom = 1f, targetZoom = 1f, viewportX = 0.5f, targetViewportX = 0.5f;
    bool dragging;
    float lastMoveTime;

    public float TargetZoom => targetZoom;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void OnEnable()
    {
        Refit(true);
        Place();
    }

    void Start()
    {
        FrameDefault(true);
        Place();
    }

    void LateUpdate()
    {
        Refit(false);
        Step(Time.unscaledDeltaTime);
        Place();
    }

    // Le format de l'écran a changé : la distance au zoom 1 et le cadrage suivent.
    void Refit(bool force)
    {
        if (cam == null) cam = GetComponent<Camera>();
        float aspect = Mathf.Max(0.2f, cam.aspect);
        if (!force && Mathf.Abs(aspect - lastAspect) < 0.0005f) return;
        lastAspect = aspect;
        cam.fieldOfView = fieldOfView;
        Vector3 position; Quaternion rotation;
        Compute(aspect, fieldOfView, pitch, yaw, slabMin, slabSize, fill, 0.5f, out position, out rotation);
        transform.rotation = rotation;
        Vector3 center = slabMin + new Vector3(slabSize.x * 0.5f, slabSize.y, slabSize.z * 0.5f);
        fitDistance = Vector3.Dot(center - position, rotation * Vector3.forward);
        if (frame == Frame.Default) FrameDefault(true);
        else if (frame == Frame.All) FrameAll(true);
    }

    void Step(float dt)
    {
        if (!dragging)
        {
            if (Mathf.Abs(velocity.x) + Mathf.Abs(velocity.y) > 0.05f)
            {
                targetFocus += velocity * dt;
                velocity *= Mathf.Exp(-dt * inertiaDecay);
            }
            float k = 1f - Mathf.Exp(-dt * smoothing);
            focus += (targetFocus - focus) * k;
            zoom += (targetZoom - zoom) * k;
            viewportX += (targetViewportX - viewportX) * k;
        }
        targetFocus = IdleFraming.Clamp(targetFocus);
        if (dragging) focus = targetFocus;
    }

    void Place()
    {
        float tanHalf = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
        float distance = fitDistance / Mathf.Max(0.1f, zoom);
        float shiftUp = (centerHeight - 0.5f) * 2f * tanHalf * distance;
        float shiftRight = (viewportX - 0.5f) * 2f * tanHalf * distance * lastAspect;
        Quaternion rotation = transform.rotation;
        transform.position = IdleFraming.ToWorld(focus) - rotation * Vector3.forward * distance - rotation * Vector3.up * shiftUp - rotation * Vector3.right * shiftRight;
    }

    void Apply(IdleFraming.View view, Frame mode, bool snap)
    {
        frame = mode;
        targetFocus = IdleFraming.Clamp(view.focus);
        targetZoom = view.zoom;
        targetViewportX = view.viewportX;
        velocity = Vector2.zero;
        if (!snap) return;
        focus = targetFocus;
        zoom = targetZoom;
        viewportX = targetViewportX;
    }

    // ------------------------------------------------------------------ cadrages

    public void FrameDefault(bool snap)
    {
        IdleHost host = IdleHost.Instance;
        if (host == null || host.Game == null) { FrameAll(snap); return; }
        float height = 2f * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) * fitDistance;
        Apply(IdleFraming.Default(host.Game, height * lastAspect, height, pitch, rightColumn), Frame.Default, snap);
    }

    public void FrameAll(bool snap) { Apply(IdleFraming.All(), Frame.All, snap); }

    public void FrameStation(int station) { Apply(IdleFraming.Station(station, targetZoom), Frame.Free, false); }

    // La loupe : vue d'ensemble, puis retour au cadrage par défaut.
    public void ToggleOverview()
    {
        if (targetZoom > 1.2f) FrameAll(false);
        else FrameDefault(false);
    }

    public bool IsOnScreen(Vector3 world)
    {
        Vector3 v = cam.WorldToViewportPoint(world);
        return v.x > 0.05f && v.x < 0.95f && v.y > 0.26f && v.y < 0.89f;   // hors du haut de l'interface et du bas
    }

    // ------------------------------------------------------------------ gestes

    public bool GroundPoint(Vector2 screen, out Vector3 ground)
    {
        Ray ray = cam.ScreenPointToRay(screen);
        ground = Vector3.zero;
        if (Mathf.Abs(ray.direction.y) < 1e-4f) return false;
        float t = -ray.origin.y / ray.direction.y;
        if (t < 0f) return false;
        ground = ray.origin + ray.direction * t;
        return true;
    }

    // Le point du sol sous le doigt suit le doigt.
    public void Drag(Vector2 from, Vector2 to)
    {
        Vector3 a, b;
        if (!GroundPoint(from, out a) || !GroundPoint(to, out b)) return;
        Vector2 delta = IdleFraming.ToPlan(a) - IdleFraming.ToPlan(b);
        float now = Time.unscaledTime, dt = Mathf.Max(0.008f, now - lastMoveTime);
        lastMoveTime = now;
        velocity = velocity * 0.5f + delta / dt * 0.5f;
        targetFocus += delta;
        frame = Frame.Free;
        dragging = true;
    }

    public void StopInertia() { velocity = Vector2.zero; lastMoveTime = Time.unscaledTime; }

    public void EndDrag()
    {
        dragging = false;
        if (Time.unscaledTime - lastMoveTime > 0.09f) velocity = Vector2.zero;   // le doigt s'était arrêté avant de se lever
    }

    public void SetZoom(float value)
    {
        zoom = targetZoom = IdleFraming.ClampZoom(value);
        frame = Frame.Free;
    }

    // Molette : le zoom rejoint sa cible en douceur.
    public void ZoomBy(float factor)
    {
        targetZoom = IdleFraming.ClampZoom(targetZoom * factor);
        frame = Frame.Free;
    }

    // Position et rotation de la caméra : la dalle (boîte slabMin..slabMin+slabSize) tient dans
    // `fill` de la largeur ; son centre est à la hauteur `viewportY` (0 = bas, 1 = haut). Fonction pure, testée.
    public static void Compute(float aspect, float fov, float pitch, float yaw, Vector3 slabMin, Vector3 slabSize, float fill, float viewportY,
                               out Vector3 position, out Quaternion rotation)
    {
        rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 center = slabMin + new Vector3(slabSize.x * 0.5f, slabSize.y, slabSize.z * 0.5f);   // centre du dessus (y = 0)
        Vector3 forward = rotation * Vector3.forward, up = rotation * Vector3.up;
        float tanHalf = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        Vector3[] corners = new Vector3[8];
        for (int i = 0; i < 8; i++)
            corners[i] = slabMin + new Vector3((i & 1) != 0 ? slabSize.x : 0f, (i & 2) != 0 ? slabSize.y : 0f, (i & 4) != 0 ? slabSize.z : 0f);

        float dist = 20f;
        position = center;
        for (int iter = 0; iter < 12; iter++)
        {
            float shift = (viewportY - 0.5f) * 2f * tanHalf * dist;          // le centre de la dalle monte à `viewportY`
            position = center - forward * dist - up * shift;
            Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
            Matrix4x4 clip = Matrix4x4.Perspective(fov, aspect, 0.3f, 400f) * view;
            float maxX = 0.0001f, minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector3 c in corners)
            {
                Vector4 p = clip * new Vector4(c.x, c.y, c.z, 1f);
                float nx = p.x / p.w, ny = p.y / p.w;
                maxX = Mathf.Max(maxX, Mathf.Abs(nx));
                minY = Mathf.Min(minY, ny); maxY = Mathf.Max(maxY, ny);
            }
            float needX = maxX / fill;
            float needY = Mathf.Max(Mathf.Abs(maxY - viewportY * 2f + 1f), Mathf.Abs(minY - viewportY * 2f + 1f)) / 0.98f;   // reste dans l'écran
            float ratio = Mathf.Max(needX, aspect >= 0.8f ? needY : 0f);
            if (Mathf.Abs(ratio - 1f) < 0.0005f) break;
            dist *= Mathf.Lerp(1f, ratio, 0.9f);
        }
    }
}
