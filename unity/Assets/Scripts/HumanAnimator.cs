using UnityEngine;

// Pilote l'Animator du technicien humain (Idle / Walk / Run) à partir de la
// vitesse du PlayerController. Ne touche ni au déplacement ni aux règles.
public class HumanAnimator : MonoBehaviour
{
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int AnimSpeedHash = Animator.StringToHash("AnimSpeed");

    [SerializeField] PlayerController player;
    [SerializeField] Animator animator;
    [SerializeField] float smoothing = 12f;
    [SerializeField] float runThreshold = 0.45f; // en dessous : marche ; au-dessus : course
    [SerializeField] float walkClipSpeed = 1.4f; // m/s couverts par l'animation de marche
    [SerializeField] float runClipSpeed = 3.4f;  // m/s couverts par l'animation de course (mesuré sur le pied d'appui)

    float speed;

    void Reset()
    {
        animator = GetComponent<Animator>();
        player = GetComponentInParent<PlayerController>();
    }

    void Update()
    {
        if (player == null || animator == null) return;

        // Speed (0 à 1) suit l'intensité de l'entrée, adoucie.
        speed = Mathf.Lerp(speed, player.MoveAmount, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        animator.SetFloat(SpeedHash, speed);

        // La vitesse de lecture suit la vitesse réelle pour que les pieds ne glissent pas.
        float reference = speed < runThreshold ? walkClipSpeed : runClipSpeed;
        float playback = player.PlanarSpeed > 0.05f ? player.PlanarSpeed / reference : 1f;
        animator.SetFloat(AnimSpeedHash, Mathf.Clamp(playback, 0.5f, 1.6f));
    }
}
