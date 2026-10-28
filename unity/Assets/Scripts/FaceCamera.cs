using UnityEngine;
using UnityEngine.Rendering;

// Fait toujours face à la caméra : rotation = rotation de la caméra, sans inclinaison.
// Mis à jour juste avant le rendu de chaque caméra de jeu (donc aussi pour une caméra déplacée à la main).
public class FaceCamera : MonoBehaviour
{
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
        if (cam.cameraType == CameraType.Game)
            transform.rotation = cam.transform.rotation;
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam != null)
            transform.rotation = cam.transform.rotation;
    }
}
