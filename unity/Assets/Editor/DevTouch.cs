using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Outil de vérification en Play : deux vrais doigts sur un écran tactile virtuel (appelé par le MCP), pour tester le pincement sans téléphone.
public static class DevTouch
{
    const int TicksPerStep = 3, Steps = 10;

    static Touchscreen screen;
    static Vector2 a0, a1, b0, b1;
    static int ticks;

    public static string Pinch(Vector2 firstFrom, Vector2 firstTo, Vector2 secondFrom, Vector2 secondTo)
    {
        a0 = firstFrom; a1 = firstTo; b0 = secondFrom; b1 = secondTo;
        ticks = 0;
        if (screen == null) screen = InputSystem.AddDevice<Touchscreen>();
        EditorApplication.update -= Step;
        EditorApplication.update += Step;
        return "pincement demandé";
    }

    static void Finger(int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 position)
    {
        InputSystem.QueueStateEvent(screen, new TouchState { touchId = id, phase = phase, position = position });
    }

    static void Step()
    {
        int step = ticks / TicksPerStep;
        if (ticks % TicksPerStep == 0)
        {
            if (step == 0) Finger(1, UnityEngine.InputSystem.TouchPhase.Began, a0);
            else if (step == 1) Finger(2, UnityEngine.InputSystem.TouchPhase.Began, b0);
            else if (step <= Steps + 1)
            {
                float t = (step - 1f) / Steps;
                Finger(1, UnityEngine.InputSystem.TouchPhase.Moved, Vector2.Lerp(a0, a1, t));
                Finger(2, UnityEngine.InputSystem.TouchPhase.Moved, Vector2.Lerp(b0, b1, t));
            }
            else if (step == Steps + 2) Finger(2, UnityEngine.InputSystem.TouchPhase.Ended, b1);
            else if (step == Steps + 4) Finger(1, UnityEngine.InputSystem.TouchPhase.Ended, a1);
            else if (step > Steps + 6)
            {
                EditorApplication.update -= Step;
                InputSystem.RemoveDevice(screen);
                screen = null;
            }
        }
        ticks++;
    }
}
