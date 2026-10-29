using UnityEngine;
using UnityEngine.UI;

// Affiche l'argent du Wallet (en haut à gauche) avec un léger gonflement à chaque gain.
public class MoneyHud : MonoBehaviour
{
    [SerializeField] Wallet wallet;
    [SerializeField] Text label;
    [SerializeField] float punchScale = 1.3f;
    [SerializeField] float recoverSpeed = 8f;

    [SerializeField] Color flashColor = new Color(1f, 0.9f, 0.3f);
    [SerializeField] float countSpeed = 9f; // vitesse du défilement (plus grand = plus vite)

    float displayed;
    float flash;
    Color baseColor;

    void OnEnable()
    {
        wallet.OnChanged += OnMoneyChanged;
        displayed = wallet.Money;
        label.text = Format(wallet.Money);
    }

    void Start()
    {
        baseColor = label.color;
        displayed = wallet.Money; // Wallet.Awake a fixé l'argent de départ
        label.text = Format(wallet.Money);
    }

    void OnDisable()
    {
        wallet.OnChanged -= OnMoneyChanged;
    }

    void OnMoneyChanged(int money)
    {
        if (money > displayed)
        {
            label.rectTransform.localScale = Vector3.one * punchScale; // gonflement + éclat doré à chaque gain
            flash = 1f;
        }
        else
        {
            displayed = money; // une dépense s'affiche tout de suite
            label.text = Format(money);
        }
    }

    void Update()
    {
        // le compteur défile vers l'argent réel
        float target = wallet.Money;
        if (!Mathf.Approximately(displayed, target))
        {
            displayed = Mathf.Lerp(displayed, target, 1f - Mathf.Exp(-countSpeed * Time.deltaTime));
            if (Mathf.Abs(displayed - target) < 0.5f) displayed = target;
            label.text = Format(Mathf.RoundToInt(displayed));
        }

        if (flash > 0f)
        {
            flash = Mathf.Max(0f, flash - Time.deltaTime * 3f);
            label.color = Color.Lerp(baseColor, flashColor, flash);
        }

        Transform t = label.rectTransform;
        if (t.localScale == Vector3.one) return;

        t.localScale = Vector3.Lerp(t.localScale, Vector3.one, 1f - Mathf.Exp(-recoverSpeed * Time.deltaTime));
        if ((t.localScale - Vector3.one).sqrMagnitude < 0.0001f)
            t.localScale = Vector3.one;
    }

    static string Format(int money)
    {
        return NumberFormat.Short(money); // l'icône de billet remplace le "$"
    }
}
