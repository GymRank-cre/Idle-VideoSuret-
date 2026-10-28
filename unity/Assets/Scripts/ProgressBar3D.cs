using UnityEngine;

// Petite barre de progression en 3D (deux cubes) qui fait face à la caméra.
public class ProgressBar3D : MonoBehaviour
{
    [SerializeField] Transform fill;   // cube de remplissage, enfant de cet objet
    [SerializeField] float width = 1.6f;

    Camera cam;

    void Start()
    {
        cam = Camera.main;
        SetProgress(0f);
    }

    // progress entre 0 et 1.
    public void SetProgress(float progress)
    {
        if (fill == null) return;

        progress = Mathf.Clamp01(progress);
        Vector3 scale = fill.localScale;
        scale.x = Mathf.Max(0.0001f, width * progress);
        fill.localScale = scale;

        Vector3 pos = fill.localPosition;
        pos.x = -width * 0.5f + scale.x * 0.5f; // reste collé à gauche
        fill.localPosition = pos;
    }

    void LateUpdate()
    {
        if (cam != null)
            transform.rotation = cam.transform.rotation;
    }
}
