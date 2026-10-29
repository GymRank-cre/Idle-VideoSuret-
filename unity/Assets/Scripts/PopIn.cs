using UnityEngine;

// À l'activation, l'objet gonfle de 0 à sa taille d'origine.
public class PopIn : MonoBehaviour
{
    [SerializeField] float duration = 0.45f;

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
        // ressort : dépasse la taille finale puis oscille en s'amortissant
        float u = elapsed / duration;
        float s = t >= 1f ? 1f : 1f - Mathf.Exp(-6f * u) * Mathf.Cos(16f * u);
        transform.localScale = fullScale * s;

        if (t >= 1f)
        {
            transform.localScale = fullScale;
            enabled = false;
        }
    }
}
