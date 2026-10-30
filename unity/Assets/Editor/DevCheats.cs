using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Outils de développement réservés à l'éditeur (dossier Editor : absents du build) : débloquer la chaîne, donner de l'argent,
// prendre une capture au format téléphone (le Game view de l'éditeur n'est pas en portrait).
public static class DevCheats
{
    [MenuItem("Tools/Idle Prototype/Débloquer la chaîne (Play)")]
    public static void UnlockChain()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("Débloquer la chaîne : à lancer en Play."); return; }
        SaveSystem.Suspended = true; // ne jamais écrire la sauvegarde avec un état triché
        foreach (UnlockPad pad in Object.FindObjectsByType<UnlockPad>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (pad.SaveId != null && !pad.SaveId.StartsWith("UpPad_")) pad.Restore(0);
        Debug.Log("Chaîne débloquée (triche d'éditeur, sauvegarde suspendue).");
    }

    [MenuItem("Tools/Idle Prototype/Donner 100 000 $ (Play)")]
    public static void GiveMoney()
    {
        if (!EditorApplication.isPlaying) return;
        SaveSystem.Suspended = true;
        Object.FindFirstObjectByType<Wallet>().Add(100000);
    }

    // Capture au format téléphone, interface comprise : la caméra de jeu rend dans une texture, le Canvas est basculé un instant
    // en « écran caméra » pour y figurer. Appelé par script (execute_code), pas depuis un menu.
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
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                var guide = Object.FindFirstObjectByType<ObjectiveGuide>();
                if (guide != null) { guide.RefreshHand(width / (float)height); Canvas.ForceUpdateCanvases(); }   // la main de tutoriel se place dans le cadre de la capture
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
