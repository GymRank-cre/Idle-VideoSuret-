using UnityEngine;
using UnityEngine.UI;

// Affiche le stock de caisses du joueur (en haut à droite) : Count/Capacity,
// avec un léger "pop" à chaque caisse ajoutée.
public class CrateHud : MonoBehaviour
{
    [SerializeField] StackCarrier carrier;
    [SerializeField] Text label;
    [SerializeField] FeedbackConfig config; // facultatif : réglages du pop

    int lastCount = -1;

    void OnEnable()
    {
        carrier.OnCountChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        carrier.OnCountChanged -= Refresh;
    }

    void Refresh()
    {
        // la capacité est plus petite et plus discrète que le nombre de caisses
        label.text = carrier.Count + "<size=44><color=#FFFFFFAA>/" + carrier.Capacity + "</color></size>";

        if (config != null && lastCount >= 0 && carrier.Count > lastCount)
            label.rectTransform.localScale = Vector3.one * config.PopScale;
        lastCount = carrier.Count;
    }

    void Update()
    {
        Transform t = label.rectTransform;
        if (t.localScale == Vector3.one) return;

        float recover = config != null ? config.PopRecover : 8f;
        t.localScale = Vector3.Lerp(t.localScale, Vector3.one, 1f - Mathf.Exp(-recover * Time.deltaTime));
        if ((t.localScale - Vector3.one).sqrMagnitude < 0.0001f)
            t.localScale = Vector3.one;
    }
}
