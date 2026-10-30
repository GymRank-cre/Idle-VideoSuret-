using UnityEngine;

// Ralenti du palier x2 (UW-4) : le temps de l'animation tombe à 0,35 pendant 0,6 s et la caméra pousse de 8 %. L'économie, elle, ne ralentit pas.
// Coupé par « moins de secousses ».
public class IdleSlowMo : MonoBehaviour
{
    [SerializeField] IdleCameraRig rig;
    [SerializeField, Range(0.05f, 1f)] float timeScale = 0.35f;
    [SerializeField, Min(0.05f)] float seconds = 0.6f;
    [SerializeField, Range(0f, 0.5f)] float push = 0.08f;

    IdleGame subscribed;
    float elapsed = -1f;

    // La poussée monte puis redescend pendant le ralenti. Fonction pure, testée.
    public static float PushAt(float t, float seconds, float push)
    {
        return t <= 0f || t >= seconds ? 0f : push * Mathf.Sin(t / seconds * Mathf.PI);
    }

    void Start()
    {
        subscribed = IdleHost.Instance.Game;
        subscribed.OnMilestone += Milestone;
    }

    void OnDestroy()
    {
        if (subscribed != null) subscribed.OnMilestone -= Milestone;
        Time.timeScale = 1f;
    }

    void Milestone(int station)
    {
        if (MotionSettings.ReduceMotion) return;
        elapsed = 0f;
        Time.timeScale = timeScale;
    }

    void Update()
    {
        if (elapsed < 0f) return;
        elapsed += Time.unscaledDeltaTime;
        rig.Push = PushAt(elapsed, seconds, push);
        if (elapsed < seconds) return;
        elapsed = -1f;
        rig.Push = 0f;
        Time.timeScale = 1f;
    }
}
