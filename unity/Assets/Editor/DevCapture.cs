using System.IO;
using UnityEngine;
using UnityEngine.UI;

// Capture au format téléphone, interface comprise (outil d'éditeur, appelé par script) : la caméra de jeu rend dans une texture
// de la taille demandée, le Canvas est basculé un instant en « écran caméra » pour y figurer.
public static class DevCapture
{
    public static string CapturePhone(string path, int width = 780, int height = 1688)
    {
        Camera cam = Camera.main;
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (cam == null) return "pas de caméra";
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        RenderMode oldMode = canvas != null ? canvas.renderMode : RenderMode.ScreenSpaceOverlay;
        Camera oldCam = canvas != null ? canvas.worldCamera : null;
        float oldDistance = canvas != null ? canvas.planeDistance : 0f;
        RenderTexture oldTarget = cam.targetTexture;
        try
        {
            cam.targetTexture = rt;
            cam.aspect = width / (float)height;   // le cadrage de la scène suit le format de la capture
            var rig = cam.GetComponent<IdleCameraRig>();
            if (rig != null) { rig.enabled = false; rig.enabled = true; }   // OnEnable recalcule le cadrage pour ce format
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                foreach (IdleDrawer drawer in Object.FindObjectsByType<IdleDrawer>(FindObjectsSortMode.None)) drawer.Fit();   // sa hauteur suit le format de la capture
                Canvas.ForceUpdateCanvases();
            }
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return "ok " + path;
        }
        finally
        {
            RenderTexture.active = null;
            cam.targetTexture = oldTarget;
            cam.ResetAspect();
            var rig2 = cam.GetComponent<IdleCameraRig>();
            if (rig2 != null) { rig2.enabled = false; rig2.enabled = true; }
            if (canvas != null)
            {
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCam;
                canvas.planeDistance = oldDistance;
            }
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
