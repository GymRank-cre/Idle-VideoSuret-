using UnityEngine;

// Fait toujours face à la caméra (texte 3D, étiquette).
public class FaceCamera : MonoBehaviour
{
    Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    void LateUpdate()
    {
        if (cam != null)
            transform.rotation = cam.transform.rotation;
    }
}
