using UnityEngine;

// Billets qui s'envolent d'un poste vers la pastille d'argent (UW-4), en courbe, par petits lots : 14 au plus en vol, tirés d'un stock fixe.
// Les positions sont en fraction d'écran : elles valent quelle que soit la façon dont le Canvas est rendu. Coupés par « moins d'effets ».
public class IdleBills : MonoBehaviour
{
    public static IdleBills Instance { get; private set; }

    [SerializeField] RectTransform[] bills;
    [SerializeField] RectTransform cashPill;
    [SerializeField, Min(0.1f)] float flightSeconds = 0.7f;
    [SerializeField, Min(0f)] float stagger = 0.06f;     // écart entre deux billets d'un lot
    [SerializeField, Range(0f, 1f)] float bend = 0.22f;  // la courbe s'écarte de la ligne droite
    [SerializeField, Range(0f, 1f)] float shrinkFrom = 0.8f;   // à la fin du vol, le billet est absorbé par la pastille
    [SerializeField, Min(1f)] float popScale = 1.08f;
    [SerializeField, Min(0.1f)] float popRecover = 9f;

    float[] startTime;
    Vector2[] from, control;
    float[] spin;
    bool[] flying;
    float pop;

    void Awake()
    {
        Instance = this;
        int n = bills.Length;
        startTime = new float[n];
        from = new Vector2[n];
        control = new Vector2[n];
        spin = new float[n];
        flying = new bool[n];
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Courbe du vol : t de 0 à 1, part de `a`, tirée vers `c`, arrive en `b`. Fonction pure, testée.
    public static Vector2 Curve(Vector2 a, Vector2 c, Vector2 b, float t)
    {
        float u = 1f - t;
        return a * (u * u) + c * (2f * u * t) + b * (t * t);
    }

    // `world` : point de la scène d'où part le lot.
    public void Send(Vector3 world, int count)
    {
        if (MotionSettings.ReduceMotion) return;
        Vector3 start = Camera.main.WorldToViewportPoint(world);
        Vector2 target = ToViewport(cashPill.position);
        float now = Time.unscaledTime;
        int sent = 0;
        for (int i = 0; i < bills.Length && sent < count; i++)
        {
            if (flying[i]) continue;
            flying[i] = true;
            startTime[i] = now + sent * stagger;
            var a = new Vector2(start.x + Random.Range(-0.03f, 0.03f), start.y + Random.Range(-0.02f, 0.02f));
            Vector2 d = target - a;
            from[i] = a;
            control[i] = (a + target) * 0.5f + new Vector2(-d.y, d.x) * (bend * Random.Range(0.6f, 1.3f));
            spin[i] = Random.Range(-540f, 540f);
            sent++;
        }
    }

    Vector2 ToViewport(Vector3 world)
    {
        var area = (RectTransform)transform;
        Vector3 local = area.InverseTransformPoint(world);
        Rect r = area.rect;
        return new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
    }

    void Update()
    {
        float now = Time.unscaledTime;
        Vector2 target = ToViewport(cashPill.position);
        for (int i = 0; i < bills.Length; i++)
        {
            if (!flying[i]) continue;
            float t = (now - startTime[i]) / flightSeconds;
            RectTransform bill = bills[i];
            if (t < 0f) continue;
            if (t >= 1f)
            {
                flying[i] = false;
                bill.gameObject.SetActive(false);
                pop = 1f;
                continue;
            }
            if (!bill.gameObject.activeSelf) bill.gameObject.SetActive(true);
            bill.anchorMin = bill.anchorMax = Curve(from[i], control[i], target, t * t);   // il accélère vers la pastille, il ne s'y attarde pas
            bill.anchoredPosition = Vector2.zero;
            float size = t > shrinkFrom ? Mathf.Lerp(1f, 0.2f, (t - shrinkFrom) / (1f - shrinkFrom)) : 1f;
            bill.localScale = new Vector3(size, size, 1f);
            bill.localRotation = Quaternion.Euler(0f, 0f, spin[i] * t);
        }
        if (pop <= 0f) return;
        pop = Mathf.Max(0f, pop - Time.unscaledDeltaTime * popRecover);
        float s = Mathf.Lerp(1f, popScale, pop);
        cashPill.localScale = new Vector3(s, s, 1f);
    }
}
