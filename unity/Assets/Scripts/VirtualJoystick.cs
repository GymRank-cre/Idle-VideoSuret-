using UnityEngine;
using UnityEngine.EventSystems;

// Joystick tactile flottant : il apparaît là où le doigt se pose, dans la zone
// qui porte ce script, et envoie la direction au PlayerController.
// Fonctionne aussi à la souris dans l'éditeur (clic maintenu + glisser).
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] PlayerController player;
    [SerializeField] RectTransform background; // rond de fond, enfant de cette zone
    [SerializeField] RectTransform knob;       // petit rond mobile, enfant du fond
    [SerializeField] float radius = 150f;      // course max du knob, en unités du Canvas
    [SerializeField, Range(0f, 0.5f)] float deadZone = 0.13f;

    RectTransform area;
    int activePointer = int.MinValue;

    void Awake()
    {
        area = (RectTransform)transform;
        Hide();
    }

    void OnDisable()
    {
        Release();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointer != int.MinValue) return; // un seul doigt pilote le joystick

        activePointer = eventData.pointerId;
        background.anchoredPosition = ToLocal(eventData);
        knob.anchoredPosition = Vector2.zero;
        background.gameObject.SetActive(true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointer) return;

        Vector2 delta = ToLocal(eventData) - background.anchoredPosition;
        if (delta.magnitude > radius)
        {
            // joystick dynamique : au-delà du rayon le rond de fond suit le doigt ; la direction reste celle du doigt par rapport au centre
            background.anchoredPosition += delta - Vector2.ClampMagnitude(delta, radius);
            delta = ToLocal(eventData) - background.anchoredPosition;
        }
        delta = Vector2.ClampMagnitude(delta, radius);
        knob.anchoredPosition = delta;

        Vector2 input = delta / radius;
        if (input.magnitude < deadZone) input = Vector2.zero;
        if (player != null) player.SetMoveInput(input);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointer) return;
        Release();
    }

    void Release()
    {
        activePointer = int.MinValue;
        if (player != null) player.SetMoveInput(Vector2.zero);
        Hide();
    }

    void Hide()
    {
        if (background != null) background.gameObject.SetActive(false);
    }

    Vector2 ToLocal(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            area, eventData.position, eventData.pressEventCamera, out Vector2 local);
        return local;
    }
}
