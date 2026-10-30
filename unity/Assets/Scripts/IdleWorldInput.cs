using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Gestes sur le monde (UL) : un toucher court achète la plaque visée ou sélectionne le poste ; un glissement déplace la caméra et n'achète jamais ;
// deux doigts ou la molette zooment. Ce qui commence sur l'interface est ignoré.
public class IdleWorldInput : MonoBehaviour
{
    const float SlopPixels = 9f, TapSeconds = 0.6f, ReferenceWidth = 390f, WheelZoom = 0.0015f;

    [SerializeField] IdleCameraRig rig;
    [SerializeField] IdlePadField pads;
    [SerializeField] IdleSelection selection;
    [SerializeField] IdleStationFx fx;

    static readonly Vector3[] Corners = new Vector3[8];
    readonly List<RaycastResult> hits = new List<RaycastResult>();
    PointerEventData pointerData;
    bool tracking, moved;
    float pressTime, pinchStart = -1f, pinchZoom;
    Vector2 pressAt, lastAt;

    // Le doigt a assez bougé pour que ce ne soit plus un toucher ; `unit` : pixels d'écran par pixel CSS.
    public static bool Moved(float pixels, float unit) { return pixels > SlopPixels * unit; }

    public static bool IsTap(bool moved, float seconds) { return !moved && seconds < TapSeconds; }

    void Update()
    {
        IdleHost host = IdleHost.Instance;
        if (host == null || host.Game == null) return;
        // les fronts plutôt que l'état : un toucher très bref peut s'appuyer et se relâcher dans la même image
        Vector2 position;
        ButtonControl button = ReadPointer(out position);
        if (button == null) return;
        if (button.wasPressedThisFrame) Press(position);
        if (Pinch()) return;
        if (tracking && button.isPressed) Move(position);
        if (button.wasReleasedThisFrame) Release(position, host.Game);

        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        float wheel = mouse.scroll.ReadValue().y;
        if (wheel != 0f && !OverUi(mouse.position.ReadValue())) rig.ZoomBy(Mathf.Exp(wheel * WheelZoom));
    }

    static ButtonControl ReadPointer(out Vector2 position)
    {
        Touchscreen touch = Touchscreen.current;
        if (touch != null)
        {
            ButtonControl press = touch.primaryTouch.press;
            if (press.isPressed || press.wasPressedThisFrame || press.wasReleasedThisFrame)
            {
                position = touch.primaryTouch.position.ReadValue();
                return press;
            }
        }
        Mouse mouse = Mouse.current;
        position = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        return mouse != null ? mouse.leftButton : null;
    }

    bool Pinch()
    {
        Touchscreen touch = Touchscreen.current;
        if (touch == null || !touch.touches[0].press.isPressed || !touch.touches[1].press.isPressed)
        {
            pinchStart = -1f;
            return false;
        }
        float distance = Mathf.Max(1f, Vector2.Distance(touch.touches[0].position.ReadValue(), touch.touches[1].position.ReadValue()));
        if (pinchStart < 0f)
        {
            if (!tracking) return false;    // le premier doigt est sur l'interface
            pinchStart = distance;
            pinchZoom = rig.TargetZoom;
            tracking = false;               // jusqu'au dernier doigt levé : ni glissement ni toucher
            rig.EndDrag();
        }
        rig.SetZoom(pinchZoom * distance / pinchStart);
        return true;
    }

    void Press(Vector2 position)
    {
        tracking = !OverUi(position);
        if (!tracking) return;
        moved = false;
        pressAt = lastAt = position;
        pressTime = Time.unscaledTime;
        rig.StopInertia();
    }

    void Move(Vector2 position)
    {
        float unit = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / ReferenceWidth);
        if (!moved && Moved(Vector2.Distance(position, pressAt), unit)) moved = true;
        if (moved && position != lastAt) rig.Drag(lastAt, position);
        lastAt = position;
    }

    void Release(Vector2 position, IdleGame game)
    {
        if (!tracking) return;
        tracking = false;
        rig.EndDrag();
        if (IsTap(moved, Time.unscaledTime - pressTime)) Tap(position, game);
    }

    void Tap(Vector2 screen, IdleGame game)
    {
        Vector3 ground;
        IdlePadView pad = rig.GroundPoint(screen, out ground) ? pads.Pick(ground) : null;
        if (pad != null) { Buy(pad, screen, game); return; }

        int station = StationAt(screen, game);
        if (station >= 0 && game.CrewReady) selection.Tap(station);
        else selection.Deselect();
    }

    void Buy(IdlePadView pad, Vector2 screen, IdleGame game)
    {
        long price = game.PadPrice(pad.Station, pad.Kind);
        IdleGame.PurchaseResult result = game.BuyPad(pad.Station, pad.Kind);
        pad.Touched();
        IdlePurchaseSound.Play(result, pad.Kind != IdleGame.PadKind.Speed && pad.Kind != IdleGame.PadKind.Add);
        if (!IdlePurchaseSound.Bought(result)) return;
        IdleChips.Spent(new Vector3(screen.x, screen.y, 0f), price);   // part du doigt (Canvas en superposition : position = pixels de l'écran)
        if (!MotionSettings.ReduceMotion) fx.PadRing(pad.Position);
    }

    // Le poste sous le doigt : rectangle d'écran englobant sa machine ; si plusieurs conviennent, le centre le plus proche.
    static int StationAt(Vector2 screen, IdleGame game)
    {
        Camera cam = Camera.main;
        int found = -1;
        float bestDistance = float.MaxValue;
        float half = IdleLayout.StationSize * 0.5f;
        for (int i = 0; i < IdleLayout.Slots.Length; i++)
        for (int k = 0; k < game.Stations[i].slots; k++)
        {
            Vector3 center = IdleLayout.SlotCenter(i, k);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int c = 0; c < 8; c++)
            {
                Vector3 p = cam.WorldToScreenPoint(center + new Vector3((c & 1) != 0 ? half : -half, (c & 2) != 0 ? 1.7f : 0f, (c & 4) != 0 ? half : -half));
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            if (screen.x < min.x || screen.x > max.x || screen.y < min.y || screen.y > max.y) continue;
            float distance = Vector2.Distance(screen, cam.WorldToScreenPoint(center + Vector3.up * 0.6f));
            if (distance < bestDistance) { bestDistance = distance; found = i; }
        }
        return found;
    }

    // Raycast direct plutôt que l'état du module : au tout premier instant d'un toucher, le module n'a pas encore vu le doigt.
    bool OverUi(Vector2 position)
    {
        EventSystem system = EventSystem.current;
        if (system == null) return false;
        if (pointerData == null) pointerData = new PointerEventData(system);
        pointerData.position = position;
        system.RaycastAll(pointerData, hits);
        return hits.Count > 0;
    }
}
