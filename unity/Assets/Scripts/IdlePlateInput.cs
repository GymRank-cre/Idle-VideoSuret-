using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Touche ou clic sur une plaque d'achat (U2b) : un rayon part de la caméra vers le sol, la plaque visible la plus proche du point
// touché achète ce qu'elle propose (`IdleGame.BuyPlate`). Ignore les touchers qui tombent sur l'interface (tiroir, boutons).
public class IdlePlateInput : MonoBehaviour
{
    [SerializeField] IdlePlateView[] plates;

    // (poste, résultat) : sons, éclat et secousse sont branchés par les autres scripts (U2d, U3).
    public static event Action<int, IdleGame.PurchaseResult> OnPlateTouched;

    void Update()
    {
        IdleHost host = IdleHost.Instance;
        Camera cam = Camera.main;
        if (host == null || host.Game == null || cam == null) return;

        Vector2 screen;
        if (!PressedThisFrame(out screen)) return;
        if (OverUi()) return;

        // intersection du rayon avec le plan du sol (y = 0)
        Ray ray = cam.ScreenPointToRay(screen);
        if (Mathf.Abs(ray.direction.y) < 1e-4f) return;
        float t = -ray.origin.y / ray.direction.y;
        if (t < 0f) return;
        Vector3 ground = ray.origin + ray.direction * t;

        IdlePlateView best = null;
        float bestDistance = float.MaxValue;
        foreach (IdlePlateView p in plates)
        {
            if (p == null || !p.IsVisible || !p.Contains(ground)) continue;
            float d = p.SqrDistance(ground);
            if (d < bestDistance) { bestDistance = d; best = p; }
        }
        if (best == null) return;

        bool wasUnlock = host.Game.PlateStateOf(best.Station) == IdleGame.PlateState.Unlock;
        long price = host.Game.PlatePrice(best.Station);
        IdleGame.PurchaseResult result = host.Game.BuyPlate(best.Station);
        OnPlateTouched?.Invoke(best.Station, result);
        IdlePurchaseSound.Play(result, wasUnlock);
        if (IdlePurchaseSound.Bought(result)) IdleChips.Spent(new Vector3(screen.x, screen.y, 0f), price);   // part du doigt (Canvas en superposition : position = pixels de l'écran)
    }

    static bool PressedThisFrame(out Vector2 position)
    {
        position = Vector2.zero;
        Touchscreen ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) { position = ts.primaryTouch.position.ReadValue(); return true; }
        Mouse m = Mouse.current;
        if (m != null && m.leftButton.wasPressedThisFrame) { position = m.position.ReadValue(); return true; }
        return false;
    }

    static bool OverUi()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;
        Touchscreen ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.isPressed) return es.IsPointerOverGameObject(ts.primaryTouch.touchId.ReadValue());
        return es.IsPointerOverGameObject();
    }
}
