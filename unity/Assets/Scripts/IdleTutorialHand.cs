using UnityEngine;

// Main de tutoriel (UL) : pendant les 12 premiers achats, dès qu'on peut payer le conseil, elle tapote la plaque au sol ; si cette plaque est hors de l'écran,
// elle tapote le bouton ACHETER du bandeau. Cachée quand une fenêtre, l'écran de retour ou la fiche d'un poste sont ouverts.
public class IdleTutorialHand : MonoBehaviour
{
    public const int MaxBuys = 12;

    [SerializeField] RectTransform hand;              // suit sa cible
    [SerializeField] RectTransform picture;           // l'image qui tapote
    [SerializeField] IdleBestBar bestBar;
    [SerializeField] IdlePadField pads;
    [SerializeField] IdleSelection selection;
    [SerializeField] IdleSheet[] sheets;
    [SerializeField] GameObject welcomeRoot;
    [SerializeField] Vector2 padOffset = new Vector2(-17f, 11f);
    [SerializeField] float barOffsetX = 39f;          // vers la droite du milieu du bouton : prix et libellé restent lisibles
    [SerializeField, Range(0f, 1f)] float barOffsetDown = 0.62f;  // le bandeau est tout en bas : la main reste à l'écran, à côté du prix
    [SerializeField] Vector2 tapMove = new Vector2(-11f, 22f);
    [SerializeField, Range(0.5f, 1f)] float tapScale = 0.92f;
    [SerializeField, Min(0.1f)] float tapPeriod = 0.8f;
    [SerializeField] float tilt = 12f;

    bool wanted;

    public static bool Wanted(long buys, bool windowOpen, bool affordable) { return buys < MaxBuys && !windowOpen && affordable; }

    // La plaque est-elle visible entre le haut de l'interface et le bas ? (viewport, 0 en bas)
    public static bool PadOnScreen(float x, float y) { return x > 0.08f && x < 0.9f && y > 0.27f && y < 0.88f; }

    // Appelé par le bas de l'écran à chaque rafraîchissement, avec le conseil du moment.
    public void Refresh(IdleGame game, IdleEconomy.Option best, bool found)
    {
        bool affordable = found && game.Cash >= IdleEconomy.Ceil(best.cost);
        wanted = pads.Advised != null && Wanted(game.Buys, WindowOpen(), affordable);
    }

    bool WindowOpen()
    {
        if (welcomeRoot.activeSelf || selection.Selected >= 0) return true;
        foreach (IdleSheet sheet in sheets) if (sheet.gameObject.activeSelf) return true;
        return false;
    }

    void Update()
    {
        Place();
    }

    // Suit sa cible (la caméra peut bouger) ; appelé aussi quand l'écran change de format.
    public void Place()
    {
        if (hand.gameObject.activeSelf != wanted) hand.gameObject.SetActive(wanted);
        if (!wanted || pads.Advised == null) return;

        Vector3 v = Camera.main.WorldToViewportPoint(pads.Advised.Position);
        if (PadOnScreen(v.x, v.y))
        {
            hand.anchorMin = hand.anchorMax = new Vector2(v.x, v.y);
            hand.anchoredPosition = padOffset;
        }
        else
        {
            RectTransform button = bestBar.BuyRect;
            Rect r = button.rect;
            hand.position = button.TransformPoint(new Vector3(r.center.x + barOffsetX, r.yMax - r.height * barOffsetDown, 0f));
        }

        bool calm = MotionSettings.ReduceMotion;
        float k = calm ? 0f : 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / tapPeriod);
        picture.anchoredPosition = tapMove * k;
        picture.localScale = Vector3.one * Mathf.Lerp(1f, tapScale, k);
        picture.localRotation = Quaternion.Euler(0f, 0f, tilt);
    }
}
