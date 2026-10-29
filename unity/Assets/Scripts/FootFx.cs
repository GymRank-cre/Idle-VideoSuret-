using UnityEngine;

// Pas du technicien : un son et, en courant, un petit nuage de poussière, déclenchés aux appuis du pied de l'animation
// (deux fois par cycle, aux positions 0 et 0,5). Les éclats viennent du pool de FeedbackSpawner.
public class FootFx : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] Animator animator;
    [SerializeField] FeedbackSpawner feedback;
    [SerializeField] float minSpeed01 = 0.3f;    // en dessous : pas de pas
    [SerializeField] float runSpeed01 = 0.6f;    // au-dessus : poussière

    float previous = -1f;

    void Update()
    {
        if (player == null || animator == null) return;

        float moving = player.MoveAmount;
        float phase = animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;

        if (moving >= minSpeed01 && previous >= 0f && (Crossed(previous, phase, 0f) || Crossed(previous, phase, 0.5f)))
        {
            AudioManager.Play(SfxId.Step, 0.9f + Random.Range(0f, 0.2f));
            if (moving >= runSpeed01 && feedback != null)
                feedback.Dust(player.transform.position);
        }
        previous = phase;
    }

    // Vrai si la phase a franchi `mark` entre deux images (en gérant le retour à 0).
    static bool Crossed(float a, float b, float mark)
    {
        if (b >= a) return a < mark && b >= mark;
        return mark >= a || mark <= b; // cycle bouclé
    }
}
