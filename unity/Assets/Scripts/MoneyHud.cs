using UnityEngine;
using UnityEngine.UI;

// Affiche l'argent du Wallet (en haut à gauche) avec un léger gonflement à chaque gain.
public class MoneyHud : MonoBehaviour
{
    [SerializeField] Wallet wallet;
    [SerializeField] Text label;
    [SerializeField] float punchScale = 1.3f;
    [SerializeField] float recoverSpeed = 8f;

    void OnEnable()
    {
        wallet.OnChanged += OnMoneyChanged;
        label.text = Format(wallet.Money);
    }

    void OnDisable()
    {
        wallet.OnChanged -= OnMoneyChanged;
    }

    void OnMoneyChanged(int money)
    {
        label.text = Format(money);
        label.rectTransform.localScale = Vector3.one * punchScale;
    }

    void Update()
    {
        Transform t = label.rectTransform;
        if (t.localScale == Vector3.one) return;

        t.localScale = Vector3.Lerp(t.localScale, Vector3.one, 1f - Mathf.Exp(-recoverSpeed * Time.deltaTime));
        if ((t.localScale - Vector3.one).sqrMagnitude < 0.0001f)
            t.localScale = Vector3.one;
    }

    static string Format(int money)
    {
        return "$ " + money;
    }
}
