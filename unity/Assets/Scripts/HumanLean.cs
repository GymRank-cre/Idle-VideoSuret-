using UnityEngine;

// Inclinaison du corps dans le sens de la marche : penché vers l'avant selon la vitesse, léger roulis dans les virages.
// Filtrée (pas d'à-coup) ; ne touche ni au déplacement ni à l'animation d'origine (rotation du modèle seulement).
public class HumanLean : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] float forwardDegrees = 7f;  // à pleine vitesse
    [SerializeField] float rollDegrees = 5f;     // dans un virage serré
    [SerializeField] float smoothing = 10f;

    Quaternion baseRotation;
    float lean;
    float roll;
    float lastYaw;

    void Awake()
    {
        baseRotation = transform.localRotation;
        if (player == null) player = GetComponentInParent<PlayerController>();
        if (player != null) lastYaw = player.transform.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (player == null) return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed01 = Mathf.Clamp01(player.PlanarSpeed / 4.2f);
        float yaw = player.transform.eulerAngles.y;
        float turnRate = Mathf.DeltaAngle(lastYaw, yaw) / dt; // degrés par seconde
        lastYaw = yaw;

        float k = 1f - Mathf.Exp(-smoothing * dt);
        lean = Mathf.Lerp(lean, forwardDegrees * speed01, k);
        roll = Mathf.Lerp(roll, Mathf.Clamp(-turnRate / 540f, -1f, 1f) * rollDegrees * speed01, k);

        transform.localRotation = baseRotation * Quaternion.Euler(lean, 0f, roll);
    }
}
