using UnityEngine;
using UnityEngine.EventSystems;

// Retour immédiat sur un bouton : il rétrécit à l'appui et revient en ressort au relâchement.
public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] float pressedScale = 0.9f;
    [SerializeField] float speed = 22f;

    bool pressed;
    bool animating;
    Vector3 baseScale;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!animating) baseScale = transform.localScale;
        pressed = true;
        animating = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pressed = false;
    }

    void OnDisable()
    {
        if (animating) transform.localScale = baseScale;
        animating = false;
        pressed = false;
    }

    void Update()
    {
        if (!animating) return; // au repos on ne touche pas à l'échelle (le guide fait battre certains boutons)

        Vector3 target = baseScale * (pressed ? pressedScale : 1f);
        transform.localScale = Vector3.Lerp(transform.localScale, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
        if (!pressed && (transform.localScale - baseScale).sqrMagnitude < 0.00005f)
        {
            transform.localScale = baseScale;
            animating = false;
        }
    }
}
