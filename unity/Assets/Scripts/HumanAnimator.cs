using UnityEngine;
using UnityEngine.AI;

// Pilote l'Animator d'un humain (Idle / Walk / Run) à partir de sa vitesse : celle du PlayerController pour le
// technicien, celle du NavMeshAgent pour un employé. Ne touche ni au déplacement ni aux règles.
public class HumanAnimator : MonoBehaviour
{
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int AnimSpeedHash = Animator.StringToHash("AnimSpeed");

    [SerializeField] PlayerController player;
    [SerializeField] NavMeshAgent agent;         // à la place du joueur : un employé
    [SerializeField] Animator animator;
    [SerializeField] float smoothing = 12f;
    [SerializeField] float runThreshold = 0.45f; // en dessous : marche ; au-dessus : course
    [SerializeField] float walkClipSpeed = 1.4f; // m/s couverts par l'animation de marche
    [SerializeField] float runClipSpeed = 4.2f;  // m/s de course du joueur : l'animation tourne alors à sa vitesse d'origine

    float speed;

    void Reset()
    {
        animator = GetComponent<Animator>();
        player = GetComponentInParent<PlayerController>();
    }

    void Update()
    {
        if (animator == null) return;

        float moveAmount, planarSpeed;
        if (player != null)
        {
            moveAmount = player.MoveAmount;
            planarSpeed = player.PlanarSpeed;
        }
        else if (agent != null)
        {
            // même échelle que le joueur : la vitesse de course du joueur correspond à Speed = 1
            planarSpeed = agent.velocity.magnitude;
            moveAmount = Mathf.Clamp01(planarSpeed / runClipSpeed);
        }
        else
        {
            return;
        }

        // Speed (0 à 1) suit l'intensité du mouvement, adoucie.
        speed = Mathf.Lerp(speed, moveAmount, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        animator.SetFloat(SpeedHash, speed);

        // La vitesse de lecture suit la vitesse réelle pour que les pieds ne glissent pas.
        float reference = speed < runThreshold ? walkClipSpeed : runClipSpeed;
        float playback = planarSpeed > 0.05f ? planarSpeed / reference : 1f;
        animator.SetFloat(AnimSpeedHash, Mathf.Clamp(playback, 0.5f, 1.6f));
    }
}
