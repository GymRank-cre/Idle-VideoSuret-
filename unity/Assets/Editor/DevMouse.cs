using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Outil de vérification en Play : envoie de vrais événements souris au système d'entrée (appelé par le MCP).
// Le clic se déroule en quelques images d'un seul tenant : entre deux appels du MCP, la souris physique du PC pourrait écraser l'état envoyé.
public static class DevMouse
{
    const int TicksPerStep = 3;

    static Vector2 clickAt;
    static int ticks;

    public static Vector2 CenterOf(string path)
    {
        var go = GameObject.Find(path);
        if (go == null) return Vector2.zero;
        var rt = (RectTransform)go.transform;
        return RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
    }

    public static string Click(Vector2 p)
    {
        clickAt = p;
        ticks = 0;
        EditorApplication.update -= StepClick;
        EditorApplication.update += StepClick;
        return "clic demandé en " + p;
    }

    static void StepClick()
    {
        if (ticks == 0) Send(clickAt, false);
        else if (ticks == TicksPerStep) Send(clickAt, true);
        else if (ticks == TicksPerStep * 2) Send(clickAt, false);
        else if (ticks > TicksPerStep * 3) EditorApplication.update -= StepClick;
        ticks++;
    }

    static void Send(Vector2 p, bool pressed)
    {
        var state = new MouseState { position = p };
        if (pressed) state = state.WithButton(MouseButton.Left, true);
        InputSystem.QueueStateEvent(Mouse.current, state);
    }
}
