using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Vue libre (11e) : un bouton carré « Vue » bascule en mode vue. Le joystick est coupé ; un doigt qui glisse déplace la caméra
// (inertie douce, limites de l'atelier), deux doigts qui pincent zooment (0,6x à 2x) ; le bouton ramène la caméra sur le technicien.
// Toucher une machine affiche sa fiche ; toucher une dalle la désigne par une flèche. Rien ne se paie à distance.
public class FreeViewController : MonoBehaviour
{
    [SerializeField] CameraFollow cameraFollow;
    [SerializeField] PlayerController player;
    [SerializeField] GameObject joystickRoot;
    [SerializeField] Button toggleButton;
    [SerializeField] Image toggleFace;
    [SerializeField] GameObject revealRoot;           // la chaîne existe (machine Test) : le bouton apparaît
    [SerializeField] GameObject infoPanel;
    [SerializeField] Text infoTitle;
    [SerializeField] Text infoLine;
    [SerializeField] Transform pointer;               // flèche qui désigne une dalle
    [SerializeField] MachineUpgradeState machineUpgrades;
    [SerializeField] GameObject buttonObject;
    [SerializeField] float inertiaDecay = 6f;

    bool active;
    Vector3 center;
    Vector3 velocity;
    float distanceMul = 1f;
    float pinchPrev;
    Vector2 lastPos;
    Vector2 downPos;
    float downTime;
    bool dragging;
    Transform pointerTarget;
    float pointerUntil;
    Transform infoTarget;
    float revealTimer;
    readonly Color idleColor = new Color32(0xFF, 0x7A, 0x1A, 0xFF);
    readonly Color activeColor = new Color32(0x1F, 0xA0, 0xFF, 0xFF);

    public bool IsActive => active;

    void Awake()
    {
        buttonObject.SetActive(false);
        infoPanel.SetActive(false);
        pointer.gameObject.SetActive(false);
        toggleButton.onClick.AddListener(Toggle);
    }

    void Toggle()
    {
        SetActive(!active);
    }

    public void SetActive(bool on)
    {
        if (on == active) return;
        active = on;
        if (joystickRoot != null) joystickRoot.SetActive(!on);   // le joystick ne gêne plus les gestes de caméra
        if (on) player.SetMoveInput(Vector2.zero);
        toggleFace.color = on ? activeColor : idleColor;
        if (on) { center = FreeViewMath.ClampCenter(player.transform.position, player.transform.position); distanceMul = 1f; velocity = Vector3.zero; }
        else { infoPanel.SetActive(false); pointer.gameObject.SetActive(false); infoTarget = null; }
        cameraFollow.SetFreeView(on, center, distanceMul);
    }

    void Update()
    {
        // le bouton apparaît quand la chaîne existe
        revealTimer += Time.unscaledDeltaTime;
        if (revealTimer > 0.5f)
        {
            revealTimer = 0f;
            if (!buttonObject.activeSelf && revealRoot != null && revealRoot.activeInHierarchy) buttonObject.SetActive(true);
        }

        if (pointer.gameObject.activeSelf)
        {
            if (!active || Time.time > pointerUntil || pointerTarget == null) pointer.gameObject.SetActive(false);
            else pointer.position = pointerTarget.position + Vector3.up * (2.6f + Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 0.3f);
        }
        if (infoTarget != null && infoPanel.activeSelf) RefreshInfo();

        if (!active) return;
        ReadInput();
        center = FreeViewMath.ClampCenter(center + velocity * Time.unscaledDeltaTime, center);
        velocity = Vector3.Lerp(velocity, Vector3.zero, 1f - Mathf.Exp(-inertiaDecay * Time.unscaledDeltaTime));
        cameraFollow.SetFreeView(true, center, distanceMul);
    }

    // ------------------------------------------------------------------ gestes

    void ReadInput()
    {
        var touches = Touchscreen.current != null ? Touchscreen.current.touches : default;
        int count = 0;
        Vector2 a = Vector2.zero, b = Vector2.zero;
        int firstId = -1;
        if (Touchscreen.current != null)
        {
            foreach (var t in touches)
            {
                if (!t.press.isPressed) continue;
                if (count == 0) { a = t.position.ReadValue(); firstId = t.touchId.ReadValue(); }
                else if (count == 1) b = t.position.ReadValue();
                count++;
            }
        }

        if (count >= 2)   // pincement : zoom, pas de déplacement
        {
            float d = Vector2.Distance(a, b);
            if (pinchPrev > 0f) distanceMul = FreeViewMath.PinchZoom(distanceMul, pinchPrev, d);
            pinchPrev = d;
            dragging = false;
            velocity = Vector3.zero;
            return;
        }
        pinchPrev = 0f;

        if (count == 1) { Pointer(a, true, firstId); return; }

        // souris dans l'éditeur : glisser pour déplacer, molette pour zoomer
        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) distanceMul = Mathf.Clamp(distanceMul * (scroll > 0f ? 0.92f : 1.08f), FreeViewMath.MinDistanceMul, FreeViewMath.MaxDistanceMul);
            Pointer(Mouse.current.position.ReadValue(), Mouse.current.leftButton.isPressed, -2);
            return;
        }
        Pointer(Vector2.zero, false, -1);
    }

    void Pointer(Vector2 pos, bool pressed, int id)
    {
        if (pressed)
        {
            if (!dragging)
            {
                if (OverUi(id)) return;                     // un bouton ou un panneau : on ne bouge pas la caméra
                dragging = true; downPos = pos; lastPos = pos; downTime = Time.unscaledTime; velocity = Vector3.zero;
                return;
            }
            Vector3 delta = GroundPoint(lastPos) - GroundPoint(pos);   // le sol suit le doigt
            if (float.IsNaN(delta.x) || float.IsNaN(delta.z)) delta = Vector3.zero;
            center = FreeViewMath.ClampCenter(center + delta, center);
            velocity = Vector3.ClampMagnitude(delta / Mathf.Max(Time.unscaledDeltaTime, 0.008f), 30f);
            lastPos = pos;
        }
        else if (dragging)
        {
            dragging = false;
            bool tap = Vector2.Distance(lastPos, downPos) < 14f && Time.unscaledTime - downTime < 0.35f;
            if (tap) Tap(lastPos);
        }
    }

    bool OverUi(int id)
    {
        if (EventSystem.current == null) return false;
        return id >= 0 ? EventSystem.current.IsPointerOverGameObject(id) : EventSystem.current.IsPointerOverGameObject();
    }

    Vector3 GroundPoint(Vector2 screen)
    {
        Camera cam = Camera.main;
        Ray ray = cam.ScreenPointToRay(screen);
        var plane = new Plane(Vector3.up, 0f);
        return plane.Raycast(ray, out float t) ? ray.GetPoint(t) : center;
    }

    // ------------------------------------------------------------------ toucher une machine ou une dalle

    void Tap(Vector2 screen)
    {
        Camera cam = Camera.main;
        RaycastHit[] hits = Physics.RaycastAll(cam.ScreenPointToRay(screen), 100f, ~0, QueryTriggerInteraction.Collide);
        foreach (RaycastHit h in hits)
        {
            var status = h.collider.GetComponentInParent<IMachineStatus>();
            if (status is MonoBehaviour mb && mb.gameObject.activeInHierarchy)
            {
                infoTarget = mb.transform;
                infoPanel.SetActive(true);
                RefreshInfo();
                return;
            }
        }
        foreach (RaycastHit h in hits)
        {
            UnlockPad pad = h.collider.GetComponentInParent<UnlockPad>();
            if (pad != null && pad.gameObject.activeInHierarchy)
            {
                pointerTarget = pad.transform;
                pointer.gameObject.SetActive(true);
                pointerUntil = Time.time + 6f;
                infoTarget = pad.transform;
                infoPanel.SetActive(true);
                RefreshInfo();
                return;
            }
        }
        infoPanel.SetActive(false);
        infoTarget = null;
    }

    void RefreshInfo()
    {
        if (infoTarget == null) { infoPanel.SetActive(false); return; }
        var status = infoTarget.GetComponent<IMachineStatus>();
        if (status != null)
        {
            string id = infoTarget.name.StartsWith("InstallStation") ? "assembly" : infoTarget.name.StartsWith("Test") ? "test" : infoTarget.name.StartsWith("Pack") ? "pack" : "";
            string title = id == "assembly" ? "Assemblage" : id == "test" ? "Test" : id == "pack" ? "Emballage" : infoTarget.name;
            int level = machineUpgrades != null && id != "" ? machineUpgrades.TotalLevel(id) : 0;
            infoTitle.text = level > 0 ? title + "  " + Loc.T("machine.level", level) : title;
            var thr = infoTarget.GetComponent<IThroughput>();
            string state = status.State == MachineState.Running ? Loc.T("view.running") : status.State == MachineState.Blocked ? Loc.T("view.blocked") : Loc.T("view.waiting");
            infoLine.text = state + (thr != null ? "   " + NumberFormat.Short(Mathf.RoundToInt(thr.ThroughputPerSecond * 60f)) + " / min" : "");
            return;
        }
        UnlockPad pad = infoTarget.GetComponent<UnlockPad>();
        if (pad != null)
        {
            infoTitle.text = Loc.T("view.pad");
            infoLine.text = "$ " + NumberFormat.Short(pad.RemainingPrice);
        }
    }
}
