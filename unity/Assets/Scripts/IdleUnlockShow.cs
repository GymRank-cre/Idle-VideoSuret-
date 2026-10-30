using UnityEngine;

// Un nouveau poste arrive en spectacle (UW-3) : la caméra glisse vers lui puis revient, poussière au sol, colonne de lumière qui monte puis s'amincit.
// La machine, elle, rebondit déjà en apparaissant (IdleStationView). Tout est coupé par « moins de secousses », sauf le rebond qui reste bref.
public class IdleUnlockShow : MonoBehaviour
{
    [SerializeField] IdleCameraRig cameraRig;
    [SerializeField] FeedbackSpawner feedback;
    [SerializeField] IdleStationView[] stations;
    [SerializeField] Transform beam;
    [SerializeField, Min(0.1f)] float beamSeconds = 1.3f;
    [SerializeField, Min(0.1f)] float beamWidth = 1f;

    IdleGame subscribed;
    float beamTime = -1f;

    void Start()
    {
        subscribed = IdleHost.Instance.Game;
        subscribed.OnUnlocked += Unlocked;
        beam.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (subscribed != null) subscribed.OnUnlocked -= Unlocked;
    }

    void Unlocked(int station)
    {
        if (MotionSettings.ReduceMotion) return;
        Vector3 position = stations[station].transform.position;
        cameraRig.FocusOn(position);
        feedback.DustRing(position);
        beam.position = position;
        beam.gameObject.SetActive(true);
        beamTime = 0f;
    }

    void Update()
    {
        if (beamTime < 0f) return;
        beamTime += Time.unscaledDeltaTime;
        float t = beamTime / beamSeconds;
        if (t >= 1f) { beamTime = -1f; beam.gameObject.SetActive(false); return; }
        float rise = Mathf.Clamp01(t / 0.25f);             // monte vite...
        float width = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, t));   // ...puis s'amincit jusqu'à disparaître
        beam.localScale = new Vector3(beamWidth * width, rise, beamWidth * width);
    }
}
