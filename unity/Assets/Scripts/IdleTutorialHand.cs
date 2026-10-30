using UnityEngine;

// Main de tutoriel (U3d) : pendant les 12 premiers achats, elle tapote le bouton conseillé dès qu'on peut le payer. Les 3 premiers achats passent par le
// bandeau « Meilleur achat », les suivants par le bouton de la ligne, s'il est visible dans la liste. Cachée dès qu'une fenêtre est ouverte.
public class IdleTutorialHand : MonoBehaviour
{
    public const int MaxBuys = 12;
    public const int BarBuys = 3;

    [SerializeField] RectTransform hand;              // suit le bouton visé
    [SerializeField] RectTransform picture;           // l'image qui tapote
    [SerializeField] IdleBestBar bestBar;
    [SerializeField] IdleStationRow[] rows;
    [SerializeField] RectTransform list;
    [SerializeField] IdleSheet[] sheets;
    [SerializeField] GameObject welcomeRoot;
    [SerializeField] float offsetX = 39f;             // vers la droite du milieu du bouton : prix et libellé restent lisibles
    [SerializeField, Range(0f, 1f)] float offsetDown = 1f;     // le bout du doigt se pose sur le bord bas du bouton
    [SerializeField] Vector2 tapMove = new Vector2(-11f, 22f);
    [SerializeField, Range(0.5f, 1f)] float tapScale = 0.92f;
    [SerializeField, Min(0.1f)] float tapPeriod = 0.8f;
    [SerializeField] float tilt = 12f;

    static readonly Vector3[] TargetCorners = new Vector3[4];
    static readonly Vector3[] ListCorners = new Vector3[4];

    RectTransform target;
    bool useBar;

    public static bool Wanted(long buys, bool windowOpen, bool affordable) { return buys < MaxBuys && !windowOpen && affordable; }
    public static bool UseBar(long buys) { return buys < BarBuys; }

    // Appelé par le tiroir à chaque rafraîchissement, avec le conseil du moment.
    public void Refresh(IdleGame game, IdleEconomy.Option best, bool found)
    {
        bool affordable = found && game.Cash >= IdleEconomy.Ceil(best.cost);
        target = null;
        if (!Wanted(game.Buys, WindowOpen(), affordable)) return;
        useBar = UseBar(game.Buys);
        target = useBar ? bestBar.BuyRect : rows[best.station].ButtonFor(best);
    }

    bool WindowOpen()
    {
        if (welcomeRoot.activeSelf) return true;
        foreach (IdleSheet sheet in sheets) if (sheet.gameObject.activeSelf) return true;
        return false;
    }

    bool Visible()
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        if (useBar) return true;
        target.GetWorldCorners(TargetCorners);
        list.GetWorldCorners(ListCorners);
        return TargetCorners[0].y > ListCorners[0].y && TargetCorners[1].y < ListCorners[1].y;
    }

    void Update()
    {
        Place();
    }

    // Suit le bouton visé (la liste peut défiler) ; appelé aussi quand l'écran change de format.
    public void Place()
    {
        bool show = Visible();
        if (hand.gameObject.activeSelf != show) hand.gameObject.SetActive(show);
        if (!show) return;

        Rect r = target.rect;
        hand.position = target.TransformPoint(new Vector3(r.center.x + offsetX, r.yMax - r.height * offsetDown, 0f));

        bool calm = MotionSettings.ReduceMotion;
        float k = calm ? 0f : 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / tapPeriod);
        picture.anchoredPosition = tapMove * k;
        picture.localScale = Vector3.one * Mathf.Lerp(1f, tapScale, k);
        picture.localRotation = Quaternion.Euler(0f, 0f, tilt);
    }
}
