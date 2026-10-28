using UnityEngine;

// À l'activation, l'objet gonfle de 0 à sa taille d'origine.
public class PopIn : MonoBehaviour
{
    [SerializeField] float duration = 0.3f;

    Vector3 fullScale;
    bool initialized;
    float elapsed;

    void OnEnable()
    {
        if (!initialized)
        {
            fullScale = transform.localScale;
            initialized = true;
        }
        elapsed = 0f;
        transform.localScale = Vector3.zero;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        // léger dépassement puis retour, pour un effet "pop"
        float s = 1f + 1.70158f * Mathf.Pow(t - 1f, 3f) + 0.70158f * Mathf.Pow(t - 1f, 2f);
        transform.localScale = fullScale * s;

        if (t >= 1f)
        {
            transform.localScale = fullScale;
            enabled = false;
        }
    }
}
