using UnityEngine;
using UnityEngine.Rendering.Universal;

// WebGL : le post-traitement (tonemapping, bloom, vignette) fait échouer le dessin final (« Mismatch between texture format
// and sampler type ») et la scène reste noire. Sur WebGL on le coupe ; ailleurs rien ne change.
[RequireComponent(typeof(Camera))]
public class WebGlPostFxGate : MonoBehaviour
{
    [SerializeField] bool disableOnWebGl = true;

    void Awake()
    {
        if (!disableOnWebGl || Application.platform != RuntimePlatform.WebGLPlayer) return;
        UniversalAdditionalCameraData data = GetComponent<UniversalAdditionalCameraData>();
        if (data != null) data.renderPostProcessing = false;
    }
}
