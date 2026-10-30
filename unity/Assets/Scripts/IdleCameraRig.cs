using UnityEngine;

// Caméra fixe du jeu idle (U2a) : vue isométrique en perspective (inclinaison ~55 degrés, vue en diagonale), placée pour que la
// dalle remplisse la largeur de l'écran, quelle que soit sa forme. En portrait la dalle occupe la partie haute (le tiroir couvre le bas).
// Repère de la scène : le plan (x, y) du web devient (Z, X) dans Unity (X = y du web, Z = x du web) ; le dessus de la dalle est à y = 0.
public class IdleCameraRig : MonoBehaviour
{
    [SerializeField] Vector3 slabMin = new Vector3(0f, -0.3f, 0f);      // le dessus de la dalle est à y = 0
    [SerializeField] Vector3 slabSize = new Vector3(7.6f, 0.3f, 7.8f);   // X = web y (7,6), Y = épaisseur, Z = web x (7,8)
    [SerializeField, Range(10f, 60f)] float fieldOfView = 30f;
    [SerializeField, Range(30f, 80f)] float pitch = 55f;
    [SerializeField] float yaw = 225f;                                   // regarde vers -X, -Z : le coin (0,0) est au fond
    [SerializeField, Range(0.5f, 1f)] float fill = 0.96f;                // part de la largeur occupée par la dalle
    [SerializeField, Range(0.3f, 0.9f)] float portraitHeight = 0.72f;    // hauteur (viewport) du centre de la dalle en portrait
    [SerializeField, Range(0.3f, 1f)] float portraitLimit = 0.8f;        // en dessous de ce rapport L/H on considère l'écran « portrait »

    Camera cam;
    float lastAspect;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void OnEnable() { Apply(true); }

    void LateUpdate() { Apply(false); }

    void Apply(bool force)
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;
        float aspect = Mathf.Max(0.2f, cam.aspect);
        if (!force && Mathf.Abs(aspect - lastAspect) < 0.0005f) return;
        lastAspect = aspect;
        cam.fieldOfView = fieldOfView;
        Vector3 pos; Quaternion rot;
        Compute(aspect, fieldOfView, pitch, yaw, slabMin, slabSize, fill, aspect < portraitLimit ? portraitHeight : 0.5f, out pos, out rot);
        transform.SetPositionAndRotation(pos, rot);
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
