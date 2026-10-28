using UnityEngine;
using UnityEngine.Rendering;

// Fait toujours face à la caméra : rotation = rotation de la caméra, sans inclinaison.
// Mis à jour juste avant le rendu de chaque caméra de jeu (donc aussi pour une caméra déplacée à la main).
public class FaceCamera : MonoBehaviour
{
    [SerializeField] bool keepScreenSize;          // même taille à l'écran quelle que soit la distance à la caméra
    [SerializeField] float referenceDistance = 14f; // distance de la caméra de jeu pour laquelle l'échelle de départ est exacte

    Vector3 baseScale;

    void Awake()
    {
        baseScale = transform.localScale;
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (cam.cameraType != CameraType.Game) return;
        transform.rotation = cam.transform.rotation;
        ApplyScale(cam);
    }

    void ApplyScale(Camera cam)
    {
        if (!keepScreenSize) return;
        float distance = Vector3.Distance(cam.transform.position, transform.position);
        transform.localScale = baseScale * (distance / referenceDistance);
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        transform.rotation = cam.transform.rotation;
        ApplyScale(cam);
    }
}
