using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Outil de vérification en Play : envoie de vrais événements souris au système d'entrée (appelé par le MCP, une étape par image).
public static class DevMouse
{
    public static string Move(Vector2 p) { return Send(p, false); }
    public static string Down(Vector2 p) { return Send(p, true); }
    public static string Up(Vector2 p) { return Send(p, false); }

    public static Vector2 CenterOf(string path)
    {
        var go = GameObject.Find(path);
        if (go == null) return Vector2.zero;
        var rt = (RectTransform)go.transform;
        return RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
    }

    static string Send(Vector2 p, bool pressed)
    {
        var state = new MouseState { position = p };
        if (pressed) state = state.WithButton(MouseButton.Left, true);
        InputSystem.QueueStateEvent(Mouse.current, state);
        return p + (pressed ? " appuyé" : " relâché");
    }
}
