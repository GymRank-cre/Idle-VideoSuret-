using System.IO;
using UnityEngine;
using UnityEngine.UI;

// Capture au format téléphone, interface comprise (outil d'éditeur, appelé par script) : la caméra de jeu rend la scène dans une texture
// de la taille demandée, une caméra à part rend le Canvas dans une image transparente, et les deux sont superposées, dans le même ordre qu'en jeu.
public static class DevCapture
{
    public static string CapturePhone(string path, int width = 780, int height = 1688)
    {
        Camera cam = Camera.main;
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (cam == null) return "pas de caméra";
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var uiRt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        RenderMode oldMode = canvas != null ? canvas.renderMode : RenderMode.ScreenSpaceOverlay;
        Camera oldCam = canvas != null ? canvas.worldCamera : null;
        float oldDistance = canvas != null ? canvas.planeDistance : 0f;
        RenderTexture oldTarget = cam.targetTexture;
        var uiCamObject = new GameObject("CaptureUi");
        var uiCam = uiCamObject.AddComponent<Camera>();
        uiCam.enabled = false;
        uiCam.transform.position = new Vector3(0f, -5000f, 0f);   // loin de la scène : seul le Canvas est dans son champ
        uiCam.orthographic = true;
        uiCam.nearClipPlane = 0.1f;
        uiCam.farClipPlane = 10f;
        uiCam.clearFlags = CameraClearFlags.SolidColor;
        uiCam.backgroundColor = Color.clear;
        uiCam.targetTexture = uiRt;
        try
        {
            cam.targetTexture = rt;
            cam.aspect = width / (float)height;   // le cadrage de la scène suit le format de la capture
            var rig = cam.GetComponent<IdleCameraRig>();
            if (rig != null) { rig.enabled = false; rig.enabled = true; }   // OnEnable recalcule le cadrage pour ce format
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCam;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                foreach (IdleDrawer drawer in Object.FindObjectsByType<IdleDrawer>(FindObjectsSortMode.None)) drawer.Fit();   // sa hauteur suit le format de la capture
                foreach (IdleSheet sheet in Object.FindObjectsByType<IdleSheet>(FindObjectsSortMode.None)) sheet.Fit();
                foreach (IdleToasts toasts in Object.FindObjectsByType<IdleToasts>(FindObjectsSortMode.None)) toasts.Reposition();
                Canvas.ForceUpdateCanvases();
                canvas.enabled = false;
            }
            cam.Render();
            if (canvas != null)
            {
                canvas.enabled = true;
                Canvas.ForceUpdateCanvases();
                uiCam.Render();
            }
            Texture2D tex = Read(rt, width, height);
            if (canvas != null)
            {
                Texture2D ui = Read(uiRt, width, height);
                Color32[] scene = tex.GetPixels32(), over = ui.GetPixels32();
                for (int i = 0; i < scene.Length; i++)   // l'interface sort en alpha prémultiplié
                {
                    int keep = 255 - over[i].a;
                    scene[i].r = (byte)Mathf.Min(255, over[i].r + scene[i].r * keep / 255);
                    scene[i].g = (byte)Mathf.Min(255, over[i].g + scene[i].g * keep / 255);
                    scene[i].b = (byte)Mathf.Min(255, over[i].b + scene[i].b * keep / 255);
                }
                tex.SetPixels32(scene);
                Object.DestroyImmediate(ui);
            }
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
                canvas.enabled = true;
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCam;
                canvas.planeDistance = oldDistance;
            }
            Object.DestroyImmediate(uiCamObject);
            rt.Release();
            Object.DestroyImmediate(rt);
            uiRt.Release();
            Object.DestroyImmediate(uiRt);
        }
    }

    static Texture2D Read(RenderTexture source, int width, int height)
    {
        RenderTexture.active = source;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        return tex;
    }
}
