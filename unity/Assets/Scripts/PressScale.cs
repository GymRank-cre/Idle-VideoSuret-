using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// États uniformes de tous les boutons (P3) : normal, pressé (échelle x0,9, retour en ressort) et désactivé
// (aucune animation, aucun son). Le son de clic est joué ICI, à l'appui, pour tous les boutons actifs ;
// un bouton qui a son propre son (achat, embauche...) est marqué « silencieux ».
public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] float pressedScale = 0.9f;
    [SerializeField] float speed = 22f;
    [SerializeField] bool silent;     // le bouton joue lui-même son son : pas de clic commun

    Button button;
    bool pressed;
    bool animating;
    Vector3 baseScale;

    public bool IsPressed => pressed;
    public bool Silent { get => silent; set => silent = value; }

    bool Usable
    {
        get
        {
            if (button == null) button = GetComponent<Button>();
            return button == null || button.IsInteractable();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!Usable) return;   // désactivé : ni animation ni son
        if (!animating) baseScale = transform.localScale;
        pressed = true;
        animating = true;
        if (!silent) AudioManager.Play(SfxId.Click);
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
        if (pressed && !Usable) pressed = false;   // devenu inactif pendant l'appui

        Vector3 target = baseScale * (pressed ? pressedScale : 1f);
        transform.localScale = Vector3.Lerp(transform.localScale, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
        if (!pressed && (transform.localScale - baseScale).sqrMagnitude < 0.00005f)
        {
            transform.localScale = baseScale;
            animating = false;
        }
    }
}
