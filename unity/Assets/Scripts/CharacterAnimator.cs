using UnityEngine;

// Animation du technicien sans package : balancement des bras et des jambes,
// rebond et inclinaison à la marche, petit "souffle" à l'arrêt.
// Lit PlayerController.IsMoving ; n'agit que sur le visuel.
public class CharacterAnimator : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] Transform body;      // tout le visuel (rebond + inclinaison)
    [SerializeField] Transform torso;     // reçoit le "souffle"
    [SerializeField] Transform leftArm;
    [SerializeField] Transform rightArm;
    [SerializeField] Transform leftLeg;
    [SerializeField] Transform rightLeg;
    [SerializeField] float stepRate = 10f;     // rad/s de la foulée
    [SerializeField] float swingAngle = 40f;   // degrés
    [SerializeField] float bobHeight = 0.07f;
    [SerializeField] float leanAngle = 10f;
    [SerializeField] float breathAmount = 0.02f;

    Vector3 bodyBase;
    float phase;
    float moveBlend;

    void Start()
    {
        if (player == null) player = GetComponentInParent<PlayerController>();
        bodyBase = body.localPosition;
    }

    void Update()
    {
        float target = player != null && player.IsMoving ? 1f : 0f;
        moveBlend = Mathf.MoveTowards(moveBlend, target, 6f * Time.deltaTime);
        phase += stepRate * moveBlend * Time.deltaTime;

        float swing = Mathf.Sin(phase) * swingAngle * moveBlend;
        float idle = 1f - moveBlend;
        float idleSway = Mathf.Sin(Time.time * 1.6f) * 4f * idle;

        leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
        rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        leftArm.localRotation = Quaternion.Euler(-swing, 0f, idleSway);
        rightArm.localRotation = Quaternion.Euler(swing, 0f, -idleSway);

        float bob = Mathf.Abs(Mathf.Sin(phase)) * bobHeight * moveBlend;
        body.localPosition = bodyBase + Vector3.up * bob;
        body.localRotation = Quaternion.Euler(leanAngle * moveBlend, 0f, 0f);

        float breath = 1f + Mathf.Sin(Time.time * 2.2f) * breathAmount * idle;
        torso.localScale = new Vector3(1f, breath, 1f);
    }
}
