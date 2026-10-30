using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Test des gestes de la vue libre (11e) : injecte de VRAIS événements tactiles dans l'Input System en Play
// (un doigt = glisser, deux doigts = pincement) et vérifie que la caméra bouge et que le zoom change.
// Menu : Tools > Idle Prototype > Test gestes tactiles (Play). Résultat dans la console (« Gestes tactiles : ... »).
public static class TouchProbe
{
    static int frame;
    static Touchscreen screen;
    static FreeViewController view;
    static CameraFollow cam;
    static Vector3 camStart, camAfterDrag;
    static float zoomStart, zoomAfterPinch;
    static readonly StringBuilder report = new StringBuilder();
    static bool dragOk, pinchOk, exitOk;

    [MenuItem("Tools/Idle Prototype/Test gestes tactiles (Play)")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("Test gestes tactiles : lancer le Play d'abord (et Débloquer la chaîne)."); return; }
        view = Object.FindFirstObjectByType<FreeViewController>(FindObjectsInactive.Include);
        cam = Object.FindFirstObjectByType<CameraFollow>();
        if (view == null || cam == null) { Debug.LogError("Test gestes tactiles : FreeViewController ou CameraFollow introuvable."); return; }
        screen = Touchscreen.current != null ? Touchscreen.current : InputSystem.AddDevice<Touchscreen>();
        frame = 0; report.Length = 0; dragOk = pinchOk = exitOk = false;
        view.SetActive(true);
        EditorApplication.update += Tick;
    }

    static float Zoom() { return (float)typeof(FreeViewController).GetField("distanceMul", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view); }

    static void Touch(int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 pos)
    {
        InputSystem.QueueStateEvent(screen, new TouchState { touchId = id, phase = phase, position = pos, pressure = 1f });
        InputSystem.Update();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        float w = Screen.width, h = Screen.height;
        frame++;

        // 1. glisser d'un doigt : 40 images
        if (frame == 5) { camStart = cam.transform.position; zoomStart = Zoom(); Touch(1, UnityEngine.InputSystem.TouchPhase.Began, new Vector2(w * 0.5f, h * 0.65f)); }
        else if (frame > 5 && frame <= 45) Touch(1, UnityEngine.InputSystem.TouchPhase.Moved, new Vector2(w * 0.5f, h * (0.65f - 0.3f * (frame - 5) / 40f)));
        else if (frame == 46) Touch(1, UnityEngine.InputSystem.TouchPhase.Ended, new Vector2(w * 0.5f, h * 0.35f));
        else if (frame == 80)
        {
            camAfterDrag = cam.transform.position;
            dragOk = Vector3.Distance(camStart, camAfterDrag) > 0.3f;
            report.Append("glisser un doigt : caméra déplacée de ").Append(Vector3.Distance(camStart, camAfterDrag).ToString("0.00")).Append(" m -> ").Append(dragOk ? "OK" : "ECHEC").Append('\n');
            zoomStart = Zoom();
        }
        // 2. pincement : deux doigts qui s'écartent
        else if (frame == 85) { Touch(2, UnityEngine.InputSystem.TouchPhase.Began, new Vector2(w * 0.40f, h * 0.5f)); Touch(3, UnityEngine.InputSystem.TouchPhase.Began, new Vector2(w * 0.60f, h * 0.5f)); }
        else if (frame > 85 && frame <= 125)
        {
            float k = (frame - 85) / 40f;
            Touch(2, UnityEngine.InputSystem.TouchPhase.Moved, new Vector2(w * (0.40f - 0.25f * k), h * 0.5f));
            Touch(3, UnityEngine.InputSystem.TouchPhase.Moved, new Vector2(w * (0.60f + 0.25f * k), h * 0.5f));
        }
        else if (frame == 126) { Touch(2, UnityEngine.InputSystem.TouchPhase.Ended, new Vector2(w * 0.15f, h * 0.5f)); Touch(3, UnityEngine.InputSystem.TouchPhase.Ended, new Vector2(w * 0.85f, h * 0.5f)); }
        else if (frame == 140)
        {
            zoomAfterPinch = Zoom();
            pinchOk = Mathf.Abs(zoomAfterPinch - zoomStart) > 0.05f;
            report.Append("pincement : zoom ").Append(zoomStart.ToString("0.00")).Append(" -> ").Append(zoomAfterPinch.ToString("0.00")).Append(" -> ").Append(pinchOk ? "OK" : "ECHEC").Append('\n');
            // 3. repli sûr : le bouton remet la main au joueur (joystick rétabli)
            view.SetActive(false);
        }
        else if (frame == 150)
        {
            exitOk = !view.IsActive;
            report.Append("sortie de la vue libre : ").Append(exitOk ? "OK" : "ECHEC").Append('\n');
            Debug.Log("Gestes tactiles : " + (dragOk && pinchOk && exitOk ? "TOUT OK" : "ECHEC") + "\n" + report);
            EditorApplication.update -= Tick;
        }
    }
}
