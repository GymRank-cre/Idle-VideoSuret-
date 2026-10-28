using UnityEngine;

// Petite barre de progression en 3D (deux cubes) qui fait face à la caméra.
// Cachée tant que la progression est à 0 (poste au repos).
public class ProgressBar3D : MonoBehaviour
{
    [SerializeField] Transform fill;   // cube de remplissage, enfant de cet objet
    [SerializeField] float width = 1.6f;

    Camera cam;
    bool visible = true;

    void Start()
    {
        cam = Camera.main;
        SetProgress(0f);
    }

    // progress entre 0 et 1 ; à 0 la barre disparaît.
    public void SetProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);
        SetVisible(progress > 0.001f);
        if (fill == null) return;

        Vector3 scale = fill.localScale;
        scale.x = Mathf.Max(0.0001f, width * progress);
        fill.localScale = scale;

        Vector3 pos = fill.localPosition;
        pos.x = -width * 0.5f + scale.x * 0.5f; // reste collé à gauche
        fill.localPosition = pos;
    }

    void SetVisible(bool value)
    {
        if (value == visible) return;
        visible = value;
        for (int i = 0; i < transform.childCount; i++)
            transform.GetChild(i).gameObject.SetActive(value);
    }

    void LateUpdate()
    {
        if (cam != null)
            transform.rotation = cam.transform.rotation;
    }
}
